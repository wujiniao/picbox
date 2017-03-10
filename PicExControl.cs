using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MyControl
{
    /// <summary>
    /// 支持缩放和画框以及对框进行微调的图片显示控件
    /// </summary>
    public partial class PicExControl : UserControl
    {
        public PicExControl()
        {
            InitializeComponent();
            _fineTuningRect = new FineTuningRect {FatherControl = this};
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        #region 字段和事件
        public delegate void MouseWheelDraw(object sender, MouseEventArgs e);  //显示树
        public event MouseWheelDraw MouseWheelDrawEvent;
        public delegate void AfterDraw(bool isMove,bool isRight);
        public event AfterDraw AfterDrawEvent;
        public delegate void Recognize(Bitmap image);
        public static event Recognize RecognizeEvent;


        private Image _image; //原图
        private FineTuningRect _fineTuningRect;
        private PosSizableRect _nodeSelected = PosSizableRect.None;
        private TreatmentType _treatmentType = TreatmentType.Zoom;
        private TreatmentType _lasttreatmentType = TreatmentType.None;
        private Rectangle _imageRect;
        private PointF _luPonit = new PointF(0,0);
        private PointF _rbPonit = new PointF(0,0);
        private PointF _startPoint = new PointF(0, 0);
        private PointF _mouseDownPoint = new PointF(0, 0);
        private bool _allawDraw;
        private bool _mIsClick;
        private bool _isMouseMove;
        private int _i;
        public float Hrate = 1;     //竖向缩放比
        public float Wrate = 1;    //横向缩放比
        private Color _rectColor = Color.Red;
        private bool _isFirstZoom = true;
        #endregion

        #region 属性 

        public bool IsFineTuring   // 是否是微调状态
            => _nodeSelected != PosSizableRect.None;

        public Rectangle ImageRect  //基于原图的框
        {
            get
            {
                int x = (int)Math.Round((_luPonit.X - _startPoint.X) / Wrate);
                int y = (int)Math.Round((_luPonit.Y - _startPoint.Y) / Hrate);
                int width = (int)Math.Round((_rbPonit.X - _luPonit.X) / Wrate);
                int height = (int)Math.Round((_rbPonit.Y - _luPonit.Y) / Wrate);
                Rectangle rect = new Rectangle(x, y, width, height);
                Rectangle imageRect = new Rectangle(0, 0, _image == null ? 0 : _image.Width, _image == null ? 0 : _image.Height);
                _imageRect = Rectangle.Intersect(rect, imageRect);
                if (_imageRect != rect)
                {
                    _luPonit.X = (int)(_imageRect.X * Wrate + _startPoint.X);
                    _luPonit.Y = (int)(_imageRect.Y * Hrate + _startPoint.Y);
                    _rbPonit.X = (int)(_imageRect.Width * Wrate + _luPonit.X);
                    _rbPonit.Y = (int)(_imageRect.Height * Hrate + _luPonit.Y);
                }
                return _imageRect;
            }
            set
            {
                if (_imageRect != value)
                {
                    _luPonit.X = (int) (value.X*Wrate + _startPoint.X);
                    _luPonit.Y = (int) (value.Y*Hrate + _startPoint.Y);
                    _rbPonit.X = (int) (value.Width*Wrate + _luPonit.X);
                    _rbPonit.Y = (int) (value.Height*Hrate + _luPonit.Y);
                }
                Invalidate();
            }
        }
        /// <summary>
        /// 是否保持图片比例不变
        /// </summary>
        public bool BIsStretch
        {
            get;
            set;
        }

        public bool IsFirstZoom   // 是否是微调状态
        {
            get { return _isFirstZoom; }
            set { _isFirstZoom = value; }
        }
        /// <summary>
        /// 允许画框
        /// </summary>
        public bool AllawDraw
        {
            get
            {
                return _allawDraw;
            }
            set
            {
                _treatmentType = _allawDraw ? TreatmentType.Draw : TreatmentType.Zoom;
                _allawDraw = value;
            }
        }

        public Color RectColor
        {
            get
            {
                return _rectColor;
            }
            set
            {
                _rectColor = value;
            }
        }
        #endregion

        #region 重写
        protected override void OnPaint(PaintEventArgs e)
        {
            try
            {
                if (_image != null)
                {
                    using (Bitmap imageToDraw = (Bitmap)_image.Clone())
                    {
                        Graphics g = e.Graphics;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.CompositingQuality = CompositingQuality.HighQuality;
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        int width = (int)Math.Round(_image.Width * Wrate);
                        int height = (int)Math.Round(_image.Height * Hrate);
                        g.DrawImage(imageToDraw, new Rectangle((int)Math.Round(_startPoint.X), (int)Math.Round(_startPoint.Y), width, height));
                        g.DrawRectangle(new Pen(_rectColor,1),_fineTuningRect.GetRectByF(new RectangleF(_luPonit.X,_luPonit.Y,(_rbPonit.X - _luPonit.X),(_rbPonit.Y - _luPonit.Y))));
                    }
                }
            }
            finally
            {
                base.OnPaint(e);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.ControlKey && _i == 0)
            {
                _lasttreatmentType = _treatmentType;
                SetZoom();
                _i++;
            }
            if (e.Control && e.KeyCode == Keys.R)
            {
                FitToScreen();
            }
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.ControlKey)
            {
                _i = 0;
                if (_lasttreatmentType != TreatmentType.None)
                    _treatmentType = _lasttreatmentType;
            }
            base.OnKeyUp(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _isMouseMove = false;
            
            _mouseDownPoint = new Point(e.X, e.Y);
            if (_image == null) return;//图片不为空
            if (e.X < _startPoint.X ||           // left
                e.X > _startPoint.X + Math.Round(_image.Width * Wrate) || // right
                e.Y < _startPoint.Y ||  // top
                e.Y > _startPoint.Y + Math.Round(_image.Height * Hrate))  // buttom
                return; //且鼠标在图片内
            _nodeSelected = _fineTuningRect.GetNodeSelectable(new Point(e.X, e.Y), _luPonit, _rbPonit);

            if (e.Button == MouseButtons.Left) 
                _mIsClick = true;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_treatmentType != TreatmentType.Zoom)
                if (AfterDrawEvent != null)
                    AfterDrawEvent(_isMouseMove, e.Button == MouseButtons.Right);
            _mIsClick = false;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            _isMouseMove = true;

            PosSizableRect r = _fineTuningRect.GetNodeSelectable(new Point(e.X, e.Y), _luPonit, _rbPonit);

            if (_image == null) return;
            if (!_mIsClick && _treatmentType!= TreatmentType.Zoom)
            {
                Cursor = _fineTuningRect.GetCursor(r);
                if (r != PosSizableRect.None)
                    SetFineTuring();
                else
                    SetDraw();
                return;
            }
            if(_mIsClick)
            {
                switch (_treatmentType)
                {
                    case TreatmentType.Zoom:
                        ZoomMouseMove(e);
                        break;
                    case TreatmentType.Draw:
                        DrawMouseMove(e);
                        break;
                    case TreatmentType.FineTuring:
                        FineTuringMouseMove(e);
                        break;
                }
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (_image == null) return;
            switch (_treatmentType)
            {
                case TreatmentType.Zoom:
                    ZoomMouseWheel(e);
                    break;
                case TreatmentType.Draw:
                    DrawMouseWheel(e);
                    break;
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            Focus();
        }
        #endregion

        #region 内部方法
        private void FitToScreen()
        {
            if (_image != null)
            {
                if (BIsStretch)
                {
                    Hrate = ((float)(Height)) / (_image.Height);
                    Wrate = ((float)(Width)) / (_image.Width);
                }
                else
                {
                    Hrate = Wrate = Math.Min(((float)(Width)) / (_image.Width), ((float)(Height)) / (_image.Height));
                }
                float x = (Width - (_image.Width * Wrate)) / 2;
                float y = (Height - (_image.Height * Hrate)) / 2;
                _startPoint = new PointF(x, y);
            }
            Invalidate();
        }

        private void DrawMouseMove(MouseEventArgs e)
        {
            _luPonit.X = Math.Min(_mouseDownPoint.X, e.X);
            _luPonit.Y = Math.Min(_mouseDownPoint.Y, e.Y);
            _rbPonit.X = Math.Max(_mouseDownPoint.X, e.X);
            _rbPonit.Y = Math.Max(_mouseDownPoint.Y, e.Y);

            Invalidate();
        }

        private void DrawMouseWheel(MouseEventArgs e)
        {
            MouseWheelDrawEvent?.Invoke(this, e);
        }

        private void ZoomMouseMove(MouseEventArgs e)
        {
            _startPoint.X += e.X - _mouseDownPoint.X;
            _startPoint.Y += e.Y - _mouseDownPoint.Y;
            _luPonit.X += e.X - _mouseDownPoint.X;
            _luPonit.Y += e.Y - _mouseDownPoint.Y;
            _rbPonit.X += e.X - _mouseDownPoint.X;
            _rbPonit.Y += e.Y - _mouseDownPoint.Y;
            _mouseDownPoint = new PointF(e.X, e.Y);
            Invalidate();
        }

        private void ZoomMouseWheel(MouseEventArgs e)
        {
            if (e.X >= _startPoint.X && e.X <= (_startPoint.X + _image.Width * Wrate) && e.Y >= _startPoint.Y && e.Y <= _startPoint.Y + _image.Height * Hrate)
            {
                float imageX = (e.X - _startPoint.X) / Wrate;
                float imageY = (e.Y - _startPoint.Y) / Hrate;

                PointF firstLu = new PointF((e.X - _luPonit.X) / Wrate, (e.Y - _luPonit.Y) / Hrate);
                PointF firstRb = new PointF((e.X - _rbPonit.X) / Wrate, (e.Y - _rbPonit.Y) / Hrate);
                float rate = 1;
                if (e.Delta > 0)
                {
                    if (Math.Max(Wrate, Hrate) <= 10)
                    {
                        rate = 1.15F;
                    }
                }
                else
                {
                    if (Math.Min(Wrate, Hrate) >= 0.1)
                    {
                        rate = 0.85F;
                    }
                }
                if (rate == 1) return;
                Hrate *= rate;
                Wrate *= rate;
                _luPonit.X = e.X - firstLu.X * Wrate;
                _luPonit.Y = e.Y - firstLu.Y * Hrate;
                _rbPonit.X = e.X - firstRb.X * Wrate;
                _rbPonit.Y = e.Y - firstRb.Y * Hrate;
                _startPoint = new PointF(e.X - imageX * Wrate, e.Y - imageY * Hrate);
                Invalidate();
            }
        }

        private void FineTuringMouseMove(MouseEventArgs e)
        {
            PointF firstLu = new PointF(_luPonit.X, _luPonit.Y);
            PointF firstRb = new PointF(_rbPonit.X, _rbPonit.Y);

            switch (_nodeSelected)
            {
                case PosSizableRect.LeftUp:
                    _luPonit.X += e.X - _mouseDownPoint.X;
                    _luPonit.Y += e.Y - _mouseDownPoint.Y;
                    break;
                case PosSizableRect.LeftMiddle:
                    _luPonit.X += e.X - _mouseDownPoint.X;
                    break;
                case PosSizableRect.LeftBottom:
                    _luPonit.X += e.X - _mouseDownPoint.X;
                    _rbPonit.Y += e.Y - _mouseDownPoint.Y;
                    break;
                case PosSizableRect.BottomMiddle:
                    _rbPonit.Y += e.Y - _mouseDownPoint.Y;
                    break;
                case PosSizableRect.RightBottom:
                    _rbPonit.X += e.X - _mouseDownPoint.X;
                    _rbPonit.Y += e.Y - _mouseDownPoint.Y;
                    break;
                case PosSizableRect.RightMiddle:
                    _rbPonit.X += e.X - _mouseDownPoint.X;
                    break;
                case PosSizableRect.RightUp:
                    _rbPonit.X += e.X - _mouseDownPoint.X;
                    _luPonit.Y += e.Y - _mouseDownPoint.Y;
                    break;
                case PosSizableRect.UpMiddle:
                    _luPonit.Y += e.Y - _mouseDownPoint.Y;
                    break;
                case PosSizableRect.TopIn:
                    _luPonit.X += e.X - _mouseDownPoint.X;
                    _luPonit.Y += e.Y - _mouseDownPoint.Y;
                    _rbPonit.X += e.X - _mouseDownPoint.X;
                    _rbPonit.Y += e.Y - _mouseDownPoint.Y;
                    break;
                case PosSizableRect.ButtonIn:
                    _luPonit.X += e.X - _mouseDownPoint.X;
                    _luPonit.Y += e.Y - _mouseDownPoint.Y;
                    _rbPonit.X += e.X - _mouseDownPoint.X;
                    _rbPonit.Y += e.Y - _mouseDownPoint.Y;
                    break;
            }
            _mouseDownPoint.X = e.X;
            _mouseDownPoint.Y = e.Y;

            if ((_rbPonit.X - _luPonit.X) < 5 || (_rbPonit.Y - _luPonit.Y) < 5)
            {
                 _luPonit = new PointF(firstLu.X, firstLu.Y);
                 _rbPonit = new PointF(firstRb.X, firstRb.Y);
            }

            Invalidate();
        }

        private void PicExControl_SizeChanged(object sender, EventArgs e)
        {
            if (_image != null)
            {
                if (BIsStretch)
                {
                    Hrate = ((float)(Height)) / (_image.Height);
                    Wrate = ((float)(Width)) / (_image.Width);
                }
                else
                {
                    Hrate = Wrate = Math.Min(((float)(Width)) / (_image.Width), ((float)(Height)) / (_image.Height));
                }
            }
            Invalidate();
        }

        private void SetDraw()
        {
            Cursor = Cursors.Default;
            _treatmentType = _allawDraw ? TreatmentType.Draw : TreatmentType.Zoom;
            Invalidate();
        }

        private void SetFineTuring()
        {
            _treatmentType = _allawDraw ? TreatmentType.FineTuring : TreatmentType.Zoom;
            Invalidate();
        }
        #endregion

        #region 公用方法
        public Image GetImage()
        {
            return _image;
        }

        public void SetImage(Image bitmap, bool isFirst, bool isDeleteRect = false, int zoom = 1)
        {
            if (_image != null)
            {
                _image.Dispose();
                _image = null;
            }
            if (bitmap == null)
            {
                return;
            }
            _image = (Image)bitmap.Clone();
            if (isFirst)
            {
                if (_isFirstZoom)
                    FitToScreen();
                else
                {
                    Hrate = zoom;     //竖向缩放比
                    Wrate = zoom;    //横向缩放比
                    _startPoint = new PointF((Width - _image.Width*Wrate) / 2,(Height - _image.Height * Hrate) / 2);
                   
                }
                SetDraw();
            }
            if (isDeleteRect)
                ImageRect = new Rectangle(0, 0, 0, 0);
            Refresh();
            bitmap.Dispose();
            Invalidate();
        }

        public Point GetClickPoint()
        {
            return new Point((int)((_mouseDownPoint.X - _startPoint.X) / Wrate), (int)((_mouseDownPoint.Y - _startPoint.Y) / Hrate));
        }
        // 进入缩放模式
        public void SetZoom()
        {
            _treatmentType = TreatmentType.Zoom;
            Invalidate();
        }
        #endregion

        private void recognize_ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (RecognizeEvent != null)
            {
                Rectangle rect = new Rectangle(0,0,_image.Width,_image.Height);
                if (_imageRect != Rectangle.Empty)
                    rect = _imageRect;
                Bitmap recognizeImage = new Bitmap(rect.Width, rect.Height);
                using (Graphics g = Graphics.FromImage(recognizeImage))
                {
                    g.DrawImage(_image, new Point(rect.X, rect.Y));
                }
                RecognizeEvent(recognizeImage);
            }
        }
    }

    public class FineTuningRect
    {
        public PicExControl FatherControl;
        private const int SizeNodeRect = 10;

        public Rectangle GetRectByF(RectangleF rectf)
        {
            return new Rectangle((int)rectf.X, (int)rectf.Y, (int)rectf.Width, (int)rectf.Height);
        }

        private RectangleF GetRect(PosSizableRect p, PointF luPointF, PointF rbPointF)
        {
            switch (p)
            {
                ////        点
                case PosSizableRect.LeftUp:
                    return new RectangleF(luPointF.X - SizeNodeRect / 2, luPointF.Y - SizeNodeRect / 2, SizeNodeRect, SizeNodeRect);
                case PosSizableRect.LeftBottom:
                    return new RectangleF(luPointF.X - SizeNodeRect / 2, rbPointF.Y - SizeNodeRect / 2, SizeNodeRect, SizeNodeRect);
                case PosSizableRect.RightUp:
                    return new RectangleF(rbPointF.X - SizeNodeRect / 2, luPointF.Y - SizeNodeRect / 2, SizeNodeRect, SizeNodeRect);
                case PosSizableRect.RightBottom:
                    return new RectangleF(rbPointF.X - SizeNodeRect / 2, rbPointF.Y - SizeNodeRect / 2, SizeNodeRect, SizeNodeRect);
                
                case PosSizableRect.TopIn:
                    return new RectangleF(luPointF.X + (rbPointF.X - luPointF.X) / 6 - SizeNodeRect / 2, luPointF.Y - SizeNodeRect / 2, (rbPointF.X - luPointF.X) / 3, SizeNodeRect);
                case PosSizableRect.ButtonIn:
                    return new RectangleF(luPointF.X + (rbPointF.X - luPointF.X) / 6 - SizeNodeRect / 2, rbPointF.Y - SizeNodeRect / 2, (rbPointF.X - luPointF.X) / 3, SizeNodeRect);
                ////       线
                case PosSizableRect.LeftMiddle:
                    return new RectangleF(luPointF.X, luPointF.Y, SizeNodeRect, rbPointF.Y - luPointF.Y);
                case PosSizableRect.UpMiddle:
                    return new RectangleF(luPointF.X, luPointF.Y, rbPointF.X - luPointF.X, SizeNodeRect);
                case PosSizableRect.RightMiddle:
                    return new RectangleF(rbPointF.X, luPointF.Y, SizeNodeRect, rbPointF.Y - luPointF.Y);
                case PosSizableRect.BottomMiddle:
                    return new RectangleF(luPointF.X, rbPointF.Y, rbPointF.X - luPointF.X, SizeNodeRect);
                default:
                    return new RectangleF();
            }
        }

        public PosSizableRect GetNodeSelectable(Point p, PointF luPointF, PointF rbPointF)
        {
            foreach (PosSizableRect r in Enum.GetValues(typeof(PosSizableRect)))
            {
                if (r == PosSizableRect.LeftMiddle || r == PosSizableRect.UpMiddle || r == PosSizableRect.RightMiddle ||
                    r == PosSizableRect.BottomMiddle)
                    continue;
                if (GetRect(r, luPointF, rbPointF).Contains(p))
                {
                    return r;
                }
            }
            foreach (PosSizableRect r in Enum.GetValues(typeof (PosSizableRect)))
            {
                if (GetRect(r, luPointF, rbPointF).Contains(p))
                {
                    return r;
                }
            }
            return PosSizableRect.None;
        }

        public Cursor GetCursor(PosSizableRect r)
        {
            switch (r)
            {
                case PosSizableRect.LeftUp:
                    return Cursors.SizeNWSE;

                case PosSizableRect.LeftMiddle:
                    return Cursors.SizeWE;

                case PosSizableRect.LeftBottom:
                    return Cursors.SizeNESW;

                case PosSizableRect.BottomMiddle:
                    return Cursors.SizeNS;

                case PosSizableRect.RightUp:
                    return Cursors.SizeNESW;

                case PosSizableRect.RightBottom:
                    return Cursors.SizeNWSE;

                case PosSizableRect.RightMiddle:
                    return Cursors.SizeWE;

                case PosSizableRect.UpMiddle:
                    return Cursors.SizeNS;

                case PosSizableRect.TopIn:
                    return Cursors.SizeAll;
                case PosSizableRect.ButtonIn:
                    return Cursors.SizeAll;

                default:
                    return Cursors.Default;
            }
        }
    }

    public enum TreatmentType
    {
        Zoom,
        Draw,
        FineTuring,
        None
    }

    public enum PosSizableRect
    {
        UpMiddle,
        LeftMiddle,
        LeftBottom,
        LeftUp,
        RightUp,
        RightMiddle,
        RightBottom,
        BottomMiddle,
        None,
        TopIn,
        ButtonIn
    };
}
