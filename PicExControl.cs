using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
            _fineTuningRect = new FineTuningRect { FatherControl = this };
            RectColor = Color.Red;
            AllawDraw = false;
            IsFirstZoom = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        #region 字段和事件
        public delegate void MouseWheelDraw(object sender, MouseEventArgs e);  //显示树
        public event MouseWheelDraw MouseWheelDrawEvent;
        public delegate void AfterDraw(bool isMove, bool isRight);
        public event AfterDraw AfterDrawEvent;
        public delegate void Recognize(Bitmap image);
        public static event Recognize RecognizeEvent;

        private FineTuningRect _fineTuningRect;
        private Image _image; //原图
        private PosSizableRect _nodeSelected = PosSizableRect.None;
        private TreatmentType _treatmentType = TreatmentType.Zoom;
        private TreatmentType _lasttreatmentType = TreatmentType.None;
        // zoom
        private PointF _startPoint = new PointF(0, 0);
        public float Hrate = 1;     //竖向缩放比
        public float Wrate = 1;    //横向缩放比

        private bool _showSupLine;
        public bool ShowSupLine
        {
            get => _showSupLine;
            set
            {
                _showSupLine = value;
                Invalidate();
            } 
        }

        public Color SupLineColor { get; set; } = Color.Red;
                                   // drawRect
        private Rectangle _imageRect;
        private PointF _luPonit = new PointF(0, 0);           // left , up
        private PointF _rbPonit = new PointF(0, 0);           // right, bottom 
        private PointF _mouseDownPoint = new PointF(0, 0);
        private PointF _mouseMovePoint = new PointF(0, 0);
        private bool _mIsClick;
        private bool _isMouseMove;
        private int _i;

        private List<PointF> _polygons = new List<PointF>();
        private PointF _drwaingPoint = new PointF(0, 0);
        private Color _polygonColor = Color.Pink;
        private int _circleRadius = 5;
        private int _selectedCircleIndex = -1;
        private int _onLineIndex1 = -1;
        #endregion

        #region 属性 

        public bool IsFineTuring   // 是否是微调状态
            => _nodeSelected != PosSizableRect.None;

        public bool IsDrawPolygon   // 是否是微调状态
        => _treatmentType == TreatmentType.DrawPolygon;

        public List<int[]> DrawPointList
        {
            get
            {
                List<int[]> points = new List<int[]>();
                foreach (var point in _polygons)
                {
                    points.Add(new int[] { (int)((point.X - _startPoint.X) / Wrate), (int)((point.Y - _startPoint.Y) / Hrate) });
                }
                return points;
            }
            set
            {
                if (value == null)
                { _polygons = new List<PointF>();
                    return;
                }
                foreach (var point in value)
                {
                    _polygons.Add(new PointF(point[0] * Wrate + _startPoint.X, point[1] * Hrate + _startPoint.Y));
                }
            }
        }

        public Rectangle ImageRect  //基于原图的框
        {
            get
            {
                int x = (int)Math.Round((_luPonit.X - _startPoint.X) / Wrate);
                int y = (int)Math.Round((_luPonit.Y - _startPoint.Y) / Hrate);
                int width = (int)Math.Round((_rbPonit.X - _luPonit.X) / Wrate);
                int height = (int)Math.Round((_rbPonit.Y - _luPonit.Y) / Wrate);
                Rectangle rect = new Rectangle(x, y, width, height);
                Rectangle imageRect = new Rectangle(0, 0, _image?.Width ?? 0, _image?.Height ?? 0);
                _imageRect = Rectangle.Intersect(rect, imageRect);
                if (_imageRect != rect)
                {
                    _luPonit.X = _imageRect.X * Wrate + _startPoint.X;
                    _luPonit.Y = _imageRect.Y * Hrate + _startPoint.Y;
                    _rbPonit.X = _imageRect.Width * Wrate + _luPonit.X;
                    _rbPonit.Y = _imageRect.Height * Hrate + _luPonit.Y;
                }
                return _imageRect;
            }
            set
            {
                if (_imageRect != value)
                {
                    _luPonit.X = value.X * Wrate + _startPoint.X;
                    _luPonit.Y = value.Y * Hrate + _startPoint.Y;
                    _rbPonit.X = value.Width * Wrate + _luPonit.X;
                    _rbPonit.Y = value.Height * Hrate + _luPonit.Y;
                    _imageRect = value;
                }
                Invalidate();
            }
        }
        /// <summary>
        /// 是否保持图片比例不变
        /// </summary>
        public bool BIsStretch { get; set; }

        [DefaultValue(typeof(bool),"true")]
        public bool IsFirstZoom { get; set; }   // 是否在开始的查看是缩放状态
        /// <summary>
        /// 允许画框
        /// </summary>
        private bool _allawDraw = true;
        public bool AllawDraw {
            get { return _allawDraw; }
            set
            {
                _allawDraw = value;
                _treatmentType = _allawDraw ? TreatmentType.DrawRect : TreatmentType.Zoom;
            }
        }

        [DefaultValue(typeof(Color), "Red")]
        public Color RectColor { get; set; }
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
                        g.DrawRectangle(new Pen(RectColor,1),_fineTuningRect.GetRectByF(new RectangleF(_luPonit.X,_luPonit.Y,(_rbPonit.X - _luPonit.X),(_rbPonit.Y - _luPonit.Y))));
                        g.DrawString(_treatmentType.ToString(), DefaultFont, Brushes.Black, 0, 0);
                        if (_polygons.Count > 0)
                        {
                            for (int j = 0; j < _polygons.Count; j++)
                            {
                                g.DrawEllipse(new Pen(_polygonColor, 1), _polygons[j].X - _circleRadius, _polygons[j].Y - _circleRadius, _circleRadius * 2, _circleRadius * 2);
                                if (j < _polygons.Count - 1)
                                    g.DrawLine(new Pen(_polygonColor, 2), _polygons[j], _polygons[j + 1]); //画线
                            }
                            g.DrawLine(new Pen(_polygonColor, 1), _polygons[0], _polygons[_polygons.Count - 1]);
                        }
                        if (ShowSupLine)
                        {
                            if (_treatmentType == TreatmentType.FineTuring && _mIsClick)
                            {
                                switch (_nodeSelected)
                                {
                                    case PosSizableRect.UpMiddle:
                                    case PosSizableRect.TopIn:
                                        DrawSupLine(new PointF(_mouseMovePoint.X, _luPonit.Y), g);
                                        break;
                                    case PosSizableRect.BottomMiddle:
                                    case PosSizableRect.ButtonIn:
                                        DrawSupLine(new PointF(_mouseMovePoint.X, _rbPonit.Y), g);
                                        break;
                                    case PosSizableRect.LeftMiddle:
                                        DrawSupLine(new PointF(_luPonit.X, _mouseMovePoint.Y), g);
                                        break;
                                    case PosSizableRect.LeftBottom:
                                        DrawSupLine(new PointF(_luPonit.X, _rbPonit.Y), g);
                                        break;
                                    case PosSizableRect.LeftUp:
                                        DrawSupLine(_luPonit, g);
                                        break;
                                    case PosSizableRect.RightUp:
                                        DrawSupLine(new PointF(_rbPonit.X, _luPonit.Y), g);
                                        break;
                                    case PosSizableRect.RightMiddle:
                                        DrawSupLine(new PointF(_rbPonit.X, _mouseMovePoint.Y), g);
                                        break;
                                    case PosSizableRect.RightBottom:
                                        DrawSupLine(_rbPonit, g);
                                        break;
                                    case PosSizableRect.None:
                                        DrawSupLine(_mouseMovePoint, g);
                                        break;
                                }

                            }
                            else
                            {
                                DrawSupLine(_mouseMovePoint, g);
                            }
                        }
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
            if (e.KeyValue == 17 && _i == 0)
            {
                _lasttreatmentType = _treatmentType;
                  _treatmentType = TreatmentType.Zoom;
                Invalidate();
                _i++;
            }
            if (e.KeyValue == 18 && _i == 0)
            {
                _lasttreatmentType = _treatmentType;
                _treatmentType = TreatmentType.DrawPolygon;
                Invalidate();
                _i++;
            }
            if (e.Alt && e.KeyCode == Keys.D)
            {
                _polygons.Clear();
                _selectedCircleIndex = -1;
                _onLineIndex1 = -1;
                _drwaingPoint = new PointF(0, 0);
                Invalidate();
            }
            if (e.Control && e.KeyCode == Keys.R)
            {
                FitToScreen();
            }
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.KeyValue == 17 || e.KeyValue == 18)
            {
                _i = 0;
                if (_lasttreatmentType != TreatmentType.None)
                {
                    _treatmentType = _lasttreatmentType;
                    Invalidate();
                }
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
            {
                _mIsClick = true;
                if (_treatmentType == TreatmentType.DrawPolygon)
                {
                    _selectedCircleIndex = GetSelectedCircleIndexFromPolygon(_mouseDownPoint);
                    if (_selectedCircleIndex == -1)
                    {
                        _onLineIndex1 = -1;
                        _drwaingPoint =  new PointF(e.X, e.Y);
                        if(_polygons.Count > 1)
                        for (int i = 0; i < _polygons.Count ; i++)
                        {
                            if (CheckPointInLine(new PointF(e.X, e.Y), _polygons[i], _polygons[i == _polygons.Count -1 ? 0 : i + 1]))
                            {
                                _onLineIndex1 = i;
                                _drwaingPoint = new PointF(0, 0);
                                break;
                            }
                        }
                    }
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_treatmentType == TreatmentType.FineTuring || _treatmentType == TreatmentType.DrawRect)
                AfterDrawEvent?.Invoke(_isMouseMove, e.Button == MouseButtons.Right);
            if (_treatmentType == TreatmentType.DrawPolygon)
            {
                if (_selectedCircleIndex != -1)
                {
                    PointF point = new PointF(e.X, e.Y);
                    if (GetDistance(_polygons[_selectedCircleIndex], new PointF(e.X, e.Y)) > _circleRadius)
                    {
                        if (CheckPointInLine(point, _polygons[_selectedCircleIndex == 0 ? _polygons.Count - 1 : _selectedCircleIndex - 1], _polygons[_selectedCircleIndex == _polygons.Count - 1 ? 0 : _selectedCircleIndex + 1]))
                            _polygons.RemoveAt(_selectedCircleIndex);
                        else
                            _polygons[_selectedCircleIndex] = point;
                    }
                }
                else
                {
                    if (_onLineIndex1 == -1)
                        _polygons.Add(_drwaingPoint);
                    else
                        _polygons.Insert(_onLineIndex1 + 1, new PointF(e.X, e.Y));
                }
                AfterDrawEvent?.Invoke(_isMouseMove, e.Button == MouseButtons.Right);
                Invalidate();
            }
            _mIsClick = false;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            _mouseMovePoint.X = e.X;
            _mouseMovePoint.Y = e.Y;
            PosSizableRect r = _fineTuningRect.GetNodeSelectable(new Point(e.X, e.Y), _luPonit, _rbPonit);
            if (_image == null) return;
            if (!_mIsClick && _treatmentType!= TreatmentType.Zoom)
            {
                if (_treatmentType != TreatmentType.DrawPolygon)
                {
                    Cursor = _fineTuningRect.GetCursor(r);
                    if (r != PosSizableRect.None)
                    {
                        if (_treatmentType != TreatmentType.FineTuring)
                        {
                            SetFineTuring();
                            return;
                        }
                    }
                    else if (_treatmentType != TreatmentType.DrawRect)
                    {
                        SetDraw();
                        return;
                    }
                }
            }
            if(_mIsClick)
            {
                _isMouseMove = true;
                switch (_treatmentType)
                {
                    case TreatmentType.Zoom:
                        ZoomMouseMove(e);
                        return;
                    case TreatmentType.DrawRect:
                        DrawMouseMove(e);
                        return;
                    case TreatmentType.FineTuring:
                        FineTuringMouseMove(e);
                        return;
                }
            }
            if(ShowSupLine)
                Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (_image == null) return;
            switch (_treatmentType)
            {
                case TreatmentType.Zoom:
                    ZoomMouseWheel(e);
                    break;
                case TreatmentType.DrawRect:
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
        private double GetDistance(PointF p1, PointF p2)
        {
            return Math.Sqrt(
               Math.Pow((p1.X - p2.X), 2)
               + Math.Pow((p1.Y - p2.Y), 2));
        }

        public bool CheckPointInLine(PointF pf, PointF p2, PointF p1, double range = 6)
        {
            double cross = (p2.X - p1.X) * (pf.X - p1.X) + (p2.Y - p1.Y) * (pf.Y - p1.Y);
            if (cross <= 0) return false;
            double d2 = (p2.X - p1.X) * (p2.X - p1.X) + (p2.Y - p1.Y) * (p2.Y - p1.Y);
            if (cross >= d2) return false;
            double r = cross / d2;
            double px = p1.X + (p2.X - p1.X) * r;
            double py = p1.Y + (p2.Y - p1.Y) * r;
            return Math.Sqrt((pf.X - px) * (pf.X - px) + (py - pf.Y) * (py - pf.Y)) <= range;
        }

        private int GetSelectedCircleIndexFromPolygon(PointF point)
        {
            if (_polygons.Count == 0)
                return -1;
            for (int i = 0; i < _polygons.Count; i++)
            {
                if (point.X >= _polygons[i].X - _circleRadius &&
                        point.X <= _polygons[i].X + _circleRadius &&
                        point.Y >= _polygons[i].Y - _circleRadius &&
                        point.Y <= _polygons[i].Y + _circleRadius)
                    return i;
            }
            return -1;
        }


        private void FitToScreen()
        {
            if (_image != null)
            {
                //矩形到图片的初始相对距离
                float _ImageRectLuX = (_luPonit.X - _startPoint.X) / Wrate;
                float _ImageRectLuY = (_luPonit.Y - _startPoint.Y) / Hrate;
                float _ImageRectRbX = (_rbPonit.X - _startPoint.X) / Wrate;
                float _ImageRectRbY = (_rbPonit.Y - _startPoint.Y) / Hrate;

                //多边形相对位置
                List<PointF> temppolygons = new List<PointF>();
                foreach (var point in _polygons)
                {
                    temppolygons.Add(new PointF((point.X - _startPoint.X) / Wrate, (point.Y - _startPoint.Y) / Hrate));
                }

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

                //还原矩形位置
                _luPonit.X = _startPoint.X + _ImageRectLuX * Wrate;
                _luPonit.Y = _startPoint.Y + _ImageRectLuY * Hrate;
                _rbPonit.X = _startPoint.X + _ImageRectRbX * Wrate;
                _rbPonit.Y = _startPoint.Y + _ImageRectRbY * Hrate;

                //还原多边形
                for (int i = 0; i < _polygons.Count; i++)
                {
                    _polygons[i] = new PointF(
                        _startPoint.X + temppolygons[i].X * Wrate,
                        _startPoint.Y + temppolygons[i].Y * Hrate);
                }
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
            for (int i = 0; i < _polygons.Count; i++)
            {
                _polygons[i] = new PointF(_polygons[i].X + e.X - _mouseDownPoint.X, _polygons[i].Y + e.Y - _mouseDownPoint.Y);
            }
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
                for (int i = 0; i < _polygons.Count; i++)
                {
                    _polygons[i] = new PointF((e.X - _polygons[i].X) / Wrate, (e.Y - _polygons[i].Y) / Hrate);
                }
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
                for (int i = 0; i < _polygons.Count; i++)
                {
                    _polygons[i] = new PointF(e.X - _polygons[i].X * Wrate, e.Y - _polygons[i].Y * Hrate);
                }
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
                //if (BIsStretch)
                //{
                //    Hrate = ((float)(Height)) / (_image.Height);
                //    Wrate = ((float)(Width)) / (_image.Width);
                //}
                //else
                //{
                //    Hrate = Wrate = Math.Min(((float)(Width)) / (_image.Width), ((float)(Height)) / (_image.Height));
                //}
                FitToScreen();
            }
            Invalidate();
        }

        private void SetDraw()
        {
            Cursor = Cursors.Default;
            _treatmentType = AllawDraw ? TreatmentType.DrawRect : TreatmentType.Zoom;
            Invalidate();
        }

        private void SetFineTuring()
        {
            _treatmentType = AllawDraw ? TreatmentType.FineTuring : TreatmentType.Zoom;
            Invalidate();
        }

        private void DrawSupLine(PointF crosspoint,Graphics g)
        {
            Pen pen = new Pen(SupLineColor, 1) {DashStyle = DashStyle.Dash,DashCap=DashCap.Round,DashPattern=new[]{10F,6F}};
            g.DrawLine(pen, crosspoint.X, 0, crosspoint.X, Height);
            g.DrawLine(pen, 0, crosspoint.Y, Width, crosspoint.Y);
        }
        #endregion

        #region 公用方法
        public Image GetImage()
        {
            return _image;
        }

        public void SetImage(Image bitmap, bool isFirst, bool isDeleteRect = false, float zoom = 1, PointF centerpoint = default(PointF))
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
                if (IsFirstZoom)
                    FitToScreen();
                else
                {
                    Hrate = zoom;     //竖向缩放比
                    Wrate = zoom;    //横向缩放比
                    _startPoint = new PointF((Width - _image.Width*Wrate) / 2,(Height - _image.Height * Hrate) / 2);
                   
                }
                SetDraw();
                _polygons.Clear();
            }
            if(centerpoint!= default(PointF))
            {
                _startPoint = new PointF(Width / 2 - centerpoint.X * zoom, Height / 2 - centerpoint.Y * zoom);
                Wrate = Hrate = zoom;
            }
            if (isDeleteRect)
            {
                _luPonit = new PointF();
                _rbPonit = new PointF();
                ImageRect = Rectangle.Empty;
            }
            Refresh();
            bitmap.Dispose();
            Invalidate();
        }

        public Point GetClickPoint()
        {
            return new Point((int)((_mouseDownPoint.X - _startPoint.X) / Wrate), (int)((_mouseDownPoint.Y - _startPoint.Y) / Hrate));
        }

       
        #endregion

        private void Recognize_ToolStripMenuItem_Click(object sender, EventArgs e)
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
        public Control FatherControl;
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
        DrawRect,
        FineTuring,
        DrawPolygon,
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
