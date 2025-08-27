using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using DXFtoACSPL.Core.Models;
using PathElement = DXFtoACSPL.Core.Services.PathGenerator.PathElement;
using AlphaMode = SharpDX.Direct2D1.AlphaMode;
using Bitmap = SharpDX.Direct2D1.Bitmap;
using Brush = SharpDX.Direct2D1.Brush;
using Color = System.Drawing.Color;
using Factory = SharpDX.Direct2D1.Factory;
using PixelFormat = SharpDX.Direct2D1.PixelFormat;
using RectangleF = System.Drawing.RectangleF;
using SolidColorBrush = SharpDX.Direct2D1.SolidColorBrush;

namespace DXFtoACSPL.WinForms.Controls
{
    /// <summary>
    /// 基于Direct2D的加工路径图示控件
    /// 保留原PathVisualizationControl的所有逻辑，使用Direct2D渲染提升性能
    /// </summary>
    public partial class Direct2DPathVisualizationControl : Control
    {
        #region Direct2D 资源
        private Factory _d2dFactory;
        private WindowRenderTarget _renderTarget;
        private SolidColorBrush _whiteBrush;
        private SolidColorBrush _redBrush;
        private SolidColorBrush _greenBrush;
        private SolidColorBrush _blueBrush;
        private SolidColorBrush _blackBrush;
        private SolidColorBrush _grayBrush;
        private SolidColorBrush _orangeBrush;
        private SolidColorBrush _purpleBrush;
        private SolidColorBrush _brownBrush;
        private SolidColorBrush _darkRedBrush;
        private Dictionary<string, SolidColorBrush> _brushCache;
        
        // DirectWrite 资源
        private SharpDX.DirectWrite.Factory _writeFactory;
        private TextFormat _textFormat;
        private TextFormat _boldTextFormat;
        private TextFormat _smallTextFormat;
        #endregion

        #region 路径数据 - 保留原有逻辑
        private List<PathElement> _pathElements = new List<PathElement>();
        private List<CircleEntity> _pathCircles = new List<CircleEntity>();
        #endregion

        #region 变换参数 - 保留原有逻辑
        private double _scale = 1.0;
        private double _offsetX = 0;
        private double _offsetY = 0;
        private bool _isInitialTransformCalculated = false;
        #endregion

        #region 鼠标交互状态 - 保留原有逻辑
        private bool _isPanning = false;
        private bool _isDragging = false;
        private Point _lastMouse;
        private Point _currentMousePosition;
        private int _hoveredPointIndex = -1;
        private int _clickedPointIndex = -1;
        private bool _isLowQuality = false;
        private DateTime _lastDragTime = DateTime.MinValue;
        private DateTime _lastWheelTime = DateTime.MinValue;
        #endregion

        #region 常量 - 保留原有逻辑
        private const int HOVER_DISTANCE = 15;
        private const int CLICK_RANGE = 5;
        private const int DRAG_THROTTLE_MS = 16; // 约60fps
        #endregion

        #region 事件 - 保留原有逻辑
        public event Action<string> LogMessage;
        #endregion

        #region 初始化状态
        private bool _isInitialized = false;
        #endregion

        public Direct2DPathVisualizationControl()
        {
            InitializeComponent();
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Opaque, true); // 避免背景重绘
            SetStyle(ControlStyles.SupportsTransparentBackColor, false); // 确保不透明背景
            
            this.BackColor = System.Drawing.Color.White;
            
            InitializeDirect2D();
            
            // 确保控件可见时立即重绘
            this.HandleCreated += (s, e) => {
                if (_isInitialized)
                {
                    Invalidate();
                    Update();
                }
            };
            
            // 注册事件
            this.MouseWheel += PathVisualizationControl_MouseWheel;
            this.MouseLeave += PathVisualizationControl_MouseLeave;
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // Direct2DPathVisualizationControl
            // 
            this.Name = "Direct2DPathVisualizationControl";
            this.Size = new System.Drawing.Size(800, 600);
            this.ResumeLayout(false);
        }

        #region Direct2D 初始化和清理
        private void InitializeDirect2D()
        {
            try
            {
                _d2dFactory = new Factory();
                _writeFactory = new SharpDX.DirectWrite.Factory();
                
                CreateDeviceResources();
                CreateTextFormats();
                
                LogMessage?.Invoke("Direct2D PathVisualization 初始化成功");
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Direct2D PathVisualization 初始化失败: {ex.Message}");
            }
        }

        private void CreateDeviceResources()
        {
            if (_renderTarget != null) return;
            if (Width <= 0 || Height <= 0) return;

            try
            {
                var renderTargetProperties = new RenderTargetProperties
                {
                    Type = RenderTargetType.Default,
                    PixelFormat = new PixelFormat(Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied),
                    DpiX = 96,
                    DpiY = 96,
                    Usage = RenderTargetUsage.None,
                    MinLevel = FeatureLevel.Level_DEFAULT
                };

                var hwndRenderTargetProperties = new HwndRenderTargetProperties
                {
                    Hwnd = Handle,
                    PixelSize = new SharpDX.Size2(Width, Height),
                    PresentOptions = PresentOptions.None
                };

                _renderTarget = new WindowRenderTarget(_d2dFactory, renderTargetProperties, hwndRenderTargetProperties);
                
                CreateBrushes();
                
                _isInitialized = true;
                LogMessage?.Invoke("Direct2D PathVisualization 渲染目标创建成功");
                
                // 渲染目标创建成功后立即触发重绘
                if (this.Visible && this.Handle != IntPtr.Zero)
                {
                    LogMessage?.Invoke("渲染目标创建后触发重绘");
                    Invalidate();
                    Update();
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"创建渲染目标失败: {ex.Message}");
                _isInitialized = false;
            }
        }

        private void CreateBrushes()
        {
            if (_renderTarget == null) return;

            _brushCache = new Dictionary<string, SolidColorBrush>();
            
            _whiteBrush = new SolidColorBrush(_renderTarget, new RawColor4(1.0f, 1.0f, 1.0f, 1.0f));
            _redBrush = new SolidColorBrush(_renderTarget, new RawColor4(1.0f, 0.0f, 0.0f, 1.0f));
            _greenBrush = new SolidColorBrush(_renderTarget, new RawColor4(0.0f, 1.0f, 0.0f, 1.0f));
            _blueBrush = new SolidColorBrush(_renderTarget, new RawColor4(0.0f, 0.0f, 1.0f, 1.0f));
            _blackBrush = new SolidColorBrush(_renderTarget, new RawColor4(0.0f, 0.0f, 0.0f, 1.0f));
            _grayBrush = new SolidColorBrush(_renderTarget, new RawColor4(0.5f, 0.5f, 0.5f, 1.0f));
            _orangeBrush = new SolidColorBrush(_renderTarget, new RawColor4(1.0f, 0.647f, 0.0f, 1.0f));
            _purpleBrush = new SolidColorBrush(_renderTarget, new RawColor4(0.5f, 0.0f, 0.5f, 1.0f));
            _brownBrush = new SolidColorBrush(_renderTarget, new RawColor4(0.647f, 0.165f, 0.165f, 1.0f));
            _darkRedBrush = new SolidColorBrush(_renderTarget, new RawColor4(0.545f, 0.0f, 0.0f, 1.0f));
        }

        private void CreateTextFormats()
        {
            if (_writeFactory == null) return;

            _textFormat = new TextFormat(_writeFactory, "Arial", 9.0f)
            {
                TextAlignment = TextAlignment.Center,
                ParagraphAlignment = ParagraphAlignment.Center
            };

            _boldTextFormat = new TextFormat(_writeFactory, "Arial", FontWeight.Bold, SharpDX.DirectWrite.FontStyle.Normal, 10.0f)
            {
                TextAlignment = TextAlignment.Center,
                ParagraphAlignment = ParagraphAlignment.Center
            };

            _smallTextFormat = new TextFormat(_writeFactory, "Arial", 8.0f)
            {
                TextAlignment = TextAlignment.Center,
                ParagraphAlignment = ParagraphAlignment.Center
            };
        }

        private void ReleaseDeviceResources()
        {
            _whiteBrush?.Dispose();
            _redBrush?.Dispose();
            _greenBrush?.Dispose();
            _blueBrush?.Dispose();
            _blackBrush?.Dispose();
            _grayBrush?.Dispose();
            _orangeBrush?.Dispose();
            _purpleBrush?.Dispose();
            _brownBrush?.Dispose();
            _darkRedBrush?.Dispose();
            
            if (_brushCache != null)
            {
                foreach (var brush in _brushCache.Values)
                {
                    brush?.Dispose();
                }
                _brushCache.Clear();
            }
            
            _renderTarget?.Dispose();
            _renderTarget = null;
            
            _isInitialized = false;
        }
        #endregion

        #region 公共接口 - 保留原有逻辑
        /// <summary>
        /// 设置路径数据
        /// </summary>
        public void SetPathData(List<PathElement> pathElements, List<CircleEntity> pathCircles)
        {
            _pathElements = pathElements ?? new List<PathElement>();
            _pathCircles = pathCircles ?? new List<CircleEntity>();
            
            _isInitialTransformCalculated = false;
            _clickedPointIndex = -1;
            _hoveredPointIndex = -1;
            
            LogMessage?.Invoke($"设置路径数据: 路径点数={_pathElements.Count}, 圆形数={_pathCircles.Count}");
            
            Invalidate();
        }

        /// <summary>
        /// 清除路径数据
        /// </summary>
        public void Clear()
        {
            _pathElements.Clear();
            _pathCircles.Clear();
            _isInitialTransformCalculated = false;
            _clickedPointIndex = -1;
            _hoveredPointIndex = -1;
            
            LogMessage?.Invoke("清除路径数据");
            
            Invalidate();
        }

        /// <summary>
        /// 清除路径数据 - 兼容原有接口
        /// </summary>
        public void ClearPathData()
        {
            Clear();
        }
        #endregion

        #region 变换计算 - 保留原有逻辑
        /// <summary>
        /// 计算路径变换参数
        /// </summary>
        private void CalculatePathTransform()
        {
            // 获取所有路径点的坐标
            var points = _pathElements.Where(e => e.Type == "Point" && e.Data is PointF)
                                    .Select(e => (PointF)e.Data)
                                    .ToList();

            if (points.Count == 0) return;

            // 计算路径的边界
            double minX = points.Min(p => p.X);
            double maxX = points.Max(p => p.X);
            double minY = points.Min(p => p.Y);
            double maxY = points.Max(p => p.Y);

            double w = maxX - minX;
            double h = maxY - minY;
            if (w <= 0 || h <= 0) return;

            // 计算缩放比例，留出边距
            double scaleX = (this.Width - 40) / w;
            double scaleY = (this.Height - 40) / h;
            _scale = Math.Min(scaleX, scaleY);
            _offsetX = (this.Width - w * _scale) / 2 - minX * _scale;
            // 翻转Y轴：CAD坐标系Y向上为正，屏幕坐标系Y向下为正
            _offsetY = (this.Height + h * _scale) / 2 + maxY * _scale;
            
            LogMessage?.Invoke($"CalculatePathTransform: 边界({minX:F2}, {minY:F2}) - ({maxX:F2}, {maxY:F2}), 缩放={_scale:F2}, 偏移=({_offsetX:F2}, {_offsetY:F2})");
        }
        #endregion

        #region Direct2D 绘制
        protected override void OnPaint(PaintEventArgs e)
        {
            if (_renderTarget == null)
            {
                CreateDeviceResources();
                if (_renderTarget == null) return;
            }

            try
            {
                _renderTarget.BeginDraw();
                _renderTarget.Clear(new RawColor4(1.0f, 1.0f, 1.0f, 1.0f));

                // 只在第一次计算变换参数，避免覆盖用户的拖拽缩放操作
                if (!_isInitialTransformCalculated)
                {
                    CalculatePathTransform();
                    _isInitialTransformCalculated = true;
                }

                DrawPath();
                DrawCoordinateAxes();

                _renderTarget.EndDraw();
            }
            catch (SharpDXException ex) when (ex.ResultCode == SharpDX.DXGI.ResultCode.DeviceRemoved || ex.ResultCode == SharpDX.DXGI.ResultCode.DeviceReset)
            {
                ReleaseDeviceResources();
                CreateDeviceResources();
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"绘制错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 绘制路径 - 基于路径坐标数据生成加工路径图示
        /// </summary>
        private void DrawPath()
        {
            if (_pathElements == null || _pathElements.Count == 0)
                return;

            // 缓存所有路径点的屏幕坐标（用于路径线条）
            var pathScreenPoints = new List<RawVector2>();
            
            // 使用所有路径点，不进行采样
            foreach (var element in _pathElements)
            {
                if (element.Type == "Point" && element.Data is PointF point)
                {
                    // 只做缩放和平移，不做任何旋转
                    float x = (float)(point.X * _scale + _offsetX);
                    float y = (float)(-point.Y * _scale + _offsetY);
                    pathScreenPoints.Add(new RawVector2(x, y));
                }
            }

            // 绘制路径线条 - 红色系（连接所有点）
            if (pathScreenPoints.Count > 1)
            {
                var pathBrush = GetOrCreateBrush(new RawColor4(1.0f, 0.392f, 0.392f, 0.706f));
                for (int i = 0; i < pathScreenPoints.Count - 1; i++)
                {
                    _renderTarget.DrawLine(pathScreenPoints[i], pathScreenPoints[i + 1], pathBrush, 1.5f);
                }
            }

            // 绘制所有圆形 - 使用原始的所有路径点，不进行采样
            bool isDragging = _isPanning;
            bool shouldDrawSimplified = isDragging && _pathElements.Count > 5000; // 提高简化绘制的阈值

            if (shouldDrawSimplified || _isLowQuality)
            {
                // 简化绘制：只绘制部分圆，提高性能
                int step = Math.Max(1, _pathElements.Count / 1000); // 减少采样间隔，显示更多圆
                for (int i = 0; i < _pathElements.Count; i += step)
                {
                    var element = _pathElements[i];
                    if (element.Type == "Point" && element.Data is PointF point && i < _pathCircles.Count)
                    {
                        float x = (float)(point.X * _scale + _offsetX);
                        float y = (float)(-point.Y * _scale + _offsetY);
                        float radius = _pathCircles[i].Radius * (float)_scale;
                        
                        var circleBrush = GetCircleBrush(_pathCircles[i].EntityType);
                        var ellipse = new Ellipse(new RawVector2(x, y), radius, radius);
                        _renderTarget.DrawEllipse(ellipse, circleBrush, 1.0f);
                    }
                }
            }
            else
            {
                // 正常绘制：绘制所有圆形
                for (int i = 0; i < _pathElements.Count; i++)
                {
                    var element = _pathElements[i];
                    if (element.Type == "Point" && element.Data is PointF point && i < _pathCircles.Count)
                    {
                        float x = (float)(point.X * _scale + _offsetX);
                        float y = (float)(-point.Y * _scale + _offsetY);
                        float radius = _pathCircles[i].Radius * (float)_scale;
                        
                        // 使用与DXF预览完全一致的颜色方案
                        var circleBrush = GetCircleBrush(_pathCircles[i].EntityType);
                        
                        // 只绘制圆环，不绘制中心点
                        var ellipse = new Ellipse(new RawVector2(x, y), radius, radius);
                        _renderTarget.DrawEllipse(ellipse, circleBrush, 1.2f);
                    }
                }
            }
            
            // 只在非拖拽状态下绘制交互元素
            if (!isDragging)
            {
                // 计算所有路径点的屏幕坐标（用于交互）
                var allScreenPoints = new List<RawVector2>();
                for (int i = 0; i < _pathElements.Count; i++)
                {
                    var element = _pathElements[i];
                    if (element.Type == "Point" && element.Data is PointF point)
                    {
                        float x = (float)(point.X * _scale + _offsetX);
                        float y = (float)(-point.Y * _scale + _offsetY);
                        allScreenPoints.Add(new RawVector2(x, y));
                    }
                }

                // 绘制点击点的序号范围（优先显示）
                if (_clickedPointIndex >= 0 && _clickedPointIndex < allScreenPoints.Count)
                {
                    DrawClickedPointNumbers(allScreenPoints, _clickedPointIndex);
                }
                // 如果没有点击，则显示悬停点的序号
                else if (_hoveredPointIndex >= 0 && _hoveredPointIndex < allScreenPoints.Count)
                {
                    DrawHoveredPointNumber(allScreenPoints[_hoveredPointIndex], _hoveredPointIndex + 1);
                }

                // 绘制起点和终点标记
                if (allScreenPoints.Count > 0)
                {
                    // 起点（绿色圆环）
                    var startBrush = GetOrCreateBrush(new RawColor4(0.196f, 0.784f, 0.196f, 0.784f));
                var startFillBrush = GetOrCreateBrush(new RawColor4(0.196f, 0.784f, 0.196f, 0.588f));
                    float startRadius = Math.Min(6f, allScreenPoints.Count > 0 && 0 < _pathCircles.Count ? 
                        _pathCircles[0].Radius * (float)_scale * 0.3f : 6f);
                    var startEllipse = new Ellipse(allScreenPoints[0], startRadius, startRadius);
                    _renderTarget.FillEllipse(startEllipse, startFillBrush);
                    _renderTarget.DrawEllipse(startEllipse, startBrush, 2.0f);
                    
                    // 终点（红色圆环）
                    if (allScreenPoints.Count > 1)
                    {
                        var endBrush = GetOrCreateBrush(new RawColor4(0.784f, 0.196f, 0.196f, 0.784f));
                var endFillBrush = GetOrCreateBrush(new RawColor4(0.784f, 0.196f, 0.196f, 0.588f));
                        float endRadius = Math.Min(6f, allScreenPoints.Count > 1 && allScreenPoints.Count - 1 < _pathCircles.Count ? 
                            _pathCircles[allScreenPoints.Count - 1].Radius * (float)_scale * 0.3f : 6f);
                        var endEllipse = new Ellipse(allScreenPoints[^1], endRadius, endRadius);
                        _renderTarget.FillEllipse(endEllipse, endFillBrush);
                        _renderTarget.DrawEllipse(endEllipse, endBrush, 2.0f);
                    }
                }
            }
        }

        /// <summary>
        /// 获取圆形画刷 - 根据路径坐标列表中的实体类型
        /// </summary>
        private SolidColorBrush GetCircleBrush(string entityType)
        {
            if (string.IsNullOrEmpty(entityType))
                return _grayBrush;
                
            switch (entityType.ToUpper())
            {
                case "CIRCLE":
                    return _blueBrush;
                case "FITTED_CIRCLE":
                case "FITTEDCIRCLE":
                    return _orangeBrush;
                case "ELLIPSE":
                    return _greenBrush;
                case "ARC":
                    return _purpleBrush;
                case "FITTED_POLYLINE":
                case "POLYLINE":
                    return _brownBrush;
                case "LINE":
                    return _darkRedBrush;
                case "POINT":
                    return _blackBrush;
                default:
                    // 对于未知类型，使用不同颜色以便区分
                    return GetOrCreateBrush(new RawColor4(0.314f, 0.471f, 0.784f, 0.706f));
            }
        }

        /// <summary>
        /// 获取或创建画刷
        /// </summary>
        private SolidColorBrush GetOrCreateBrush(RawColor4 color)
        {
            string key = $"{color.R}_{color.G}_{color.B}_{color.A}";
            if (!_brushCache.TryGetValue(key, out var brush))
            {
                brush = new SolidColorBrush(_renderTarget, color);
                _brushCache[key] = brush;
            }
            return brush;
        }

        /// <summary>
        /// 绘制坐标轴
        /// </summary>
        private void DrawCoordinateAxes()
        {
            try
            {
                // 计算中心位置：使用路径点计算中心
                double centerX = 0, centerY = 0;
                
                var validPoints = _pathElements.Where(e => e.Type == "Point" && e.Data is PointF).Select(e => (PointF)e.Data).ToList();
                if (validPoints.Count > 0)
                {
                    // 使用路径点计算中心
                    centerX = validPoints.Average(p => p.X);
                    centerY = validPoints.Average(p => p.Y);
                }
                else
                {
                    // 默认使用控件中心
                    centerX = Width / 2.0 / _scale;
                    centerY = Height / 2.0 / _scale;
                }
                
                var screenCenterX = (float)(centerX * _scale + _offsetX);
                var screenCenterY = (float)(-centerY * _scale + _offsetY);
                
                // X轴（红色虚线）
                var xAxisBrush = _redBrush;
                var strokeStyle = new StrokeStyle(_d2dFactory, new StrokeStyleProperties
                {
                    DashStyle = DashStyle.Dash
                });
                _renderTarget.DrawLine(new RawVector2(0, screenCenterY), new RawVector2(Width, screenCenterY), xAxisBrush, 2.0f, strokeStyle);
                
                // Y轴（绿色虚线）
                var yAxisBrush = _greenBrush;
                _renderTarget.DrawLine(new RawVector2(screenCenterX, 0), new RawVector2(screenCenterX, Height), yAxisBrush, 2.0f, strokeStyle);
                
                // 原点（蓝色圆）
                var originBrush = _blueBrush;
                var originEllipse = new Ellipse(new RawVector2(screenCenterX, screenCenterY), 5, 5);
                _renderTarget.DrawEllipse(originEllipse, originBrush, 3.0f);
                
                // 坐标轴标签
                var textBrush = _redBrush;
                var xRect = new RawRectangleF(Width - 30, screenCenterY - 20, Width - 10, screenCenterY);
                _renderTarget.DrawText("X", _textFormat, xRect, textBrush);
                
                textBrush = _greenBrush;
                var yRect = new RawRectangleF(screenCenterX + 10, 10, screenCenterX + 30, 30);
                _renderTarget.DrawText("Y", _textFormat, yRect, textBrush);
                
                strokeStyle?.Dispose();
            }
            catch { }
        }
         #endregion

        #region 鼠标交互 - 保留原有逻辑
        /// <summary>
        /// 鼠标滚轮缩放
        /// </summary>
        private void PathVisualizationControl_MouseWheel(object sender, MouseEventArgs e)
        {
            if (_pathElements.Count == 0) return;

            _lastWheelTime = DateTime.Now;

            double zoomFactor = e.Delta > 0 ? 1.1 : 0.9;
            double oldScale = _scale;
            _scale *= zoomFactor;
            
            // 限制缩放范围 - 扩大范围以适应大量孔位
            _scale = Math.Max(0.001, Math.Min(100.0, _scale));
            
            // 计算鼠标位置在缩放前的世界坐标
            double worldX = (e.X - _offsetX) / oldScale;
            double worldY = (e.Y - _offsetY) / oldScale;
            
            // 计算鼠标位置在缩放后的屏幕坐标
            var newScreenPos = new RawVector2(
                (float)(worldX * _scale + _offsetX),
                (float)(worldY * _scale + _offsetY)
            );
            
            // 调整平移量以保持鼠标位置不变
            _offsetX += e.X - newScreenPos.X;
            _offsetY -= e.Y - newScreenPos.Y; // Y轴翻转
            
            Invalidate();
        }

        /// <summary>
        /// 鼠标拖拽平移
        /// </summary>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                // 检查是否点击了路径点
                int clickedIndex = GetHoveredPointIndex(e.Location);
                if (clickedIndex >= 0)
                {
                    _clickedPointIndex = clickedIndex;
                    _hoveredPointIndex = -1;
                    Invalidate();
                    return;
                }
                
                // 没有点击路径点，开始拖拽
                _isPanning = true;
                _isDragging = true;
                _lastMouse = e.Location;
                this.Cursor = Cursors.Hand;
                _isLowQuality = true;
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left)
            {
                _isPanning = false;
                _isDragging = false;
                this.Cursor = Cursors.Default;
                _isLowQuality = false;
                Invalidate();
            }
            else if (e.Button == MouseButtons.Right)
            {
                if (_clickedPointIndex != -1)
                {
                    _clickedPointIndex = -1;
                    Invalidate();
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _currentMousePosition = e.Location;
            
            if (_isPanning)
            {
                int dx = e.X - _lastMouse.X;
                int dy = e.Y - _lastMouse.Y;
                _offsetX += dx;
                _offsetY += dy;
                _lastMouse = e.Location;
                
                // 性能优化：拖拽时使用时间节流
                var now = DateTime.Now;
                if ((now - _lastDragTime).TotalMilliseconds >= DRAG_THROTTLE_MS)
                {
                    _lastDragTime = now;
                    
                    // 根据路径点数量调整重绘策略
                    if (_pathElements.Count > 100)
                    {
                        // 大量路径点：只重绘路径线条，不重绘圆形
                        _isLowQuality = true;
                        Invalidate();
                    }
                    else if (_pathElements.Count > 50)
                    {
                        // 中等数量：减少重绘频率
                        if (Math.Abs(dx) > 8 || Math.Abs(dy) > 8)
                        {
                            Invalidate();
                        }
                    }
                    else
                    {
                        // 少量路径点：正常重绘
                        Invalidate();
                    }
                }
            }
            else
            {
                // 更新悬停状态
                int newHoveredIndex = GetHoveredPointIndex(e.Location);
                if (newHoveredIndex != _hoveredPointIndex)
                {
                    _hoveredPointIndex = newHoveredIndex;
                    Invalidate();
                }
            }
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            
            // 防止拖拽后意外触发双击，以及滚轮操作后立即触发的双击
            if (!_isDragging && (DateTime.Now - _lastWheelTime).TotalMilliseconds > 500)
            {
                ResetView();
            }
        }

        private void PathVisualizationControl_MouseLeave(object sender, EventArgs e)
        {
            if (_hoveredPointIndex != -1)
            {
                _hoveredPointIndex = -1;
                Invalidate();
            }
        }

        /// <summary>
        /// 获取悬停的点索引
        /// </summary>
        private int GetHoveredPointIndex(Point mouseLocation)
        {
            if (_pathElements.Count == 0) return -1;

            var points = _pathElements.Where(e => e.Type == "Point" && e.Data is PointF)
                                    .Select(e => (PointF)e.Data)
                                    .ToList();

            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];
                float x = (float)(point.X * _scale + _offsetX);
                float y = (float)(-point.Y * _scale + _offsetY);
                
                float distance = (float)Math.Sqrt(
                    Math.Pow(mouseLocation.X - x, 2) + 
                    Math.Pow(mouseLocation.Y - y, 2)
                );
                
                if (distance <= HOVER_DISTANCE)
                {
                    return i;
                }
            }
            
            return -1;
        }
        #endregion

        #region 文本绘制 - 保留原有逻辑
        /// <summary>
        /// 绘制悬停点序号
        /// </summary>
        private void DrawHoveredPointNumber(RawVector2 point, int number)
        {
            string numberText = number.ToString();
            
            var textBrush = _blackBrush;
            var bgBrush = GetOrCreateBrush(new RawColor4(1.0f, 1.0f, 1.0f, 0.941f));
                     var borderBrush = GetOrCreateBrush(new RawColor4(0.392f, 0.588f, 1.0f, 1.0f));
            
            var textRect = new RawRectangleF(point.X - 15, point.Y - 25, point.X + 15, point.Y - 5);
            
            // 绘制背景
            _renderTarget.FillRectangle(textRect, bgBrush);
            _renderTarget.DrawRectangle(textRect, borderBrush, 1.0f);
            
            // 绘制文本
            _renderTarget.DrawText(numberText, _textFormat, textRect, textBrush);
        }

        /// <summary>
        /// 绘制点击点的序号范围
        /// </summary>
        private void DrawClickedPointNumbers(List<RawVector2> screenPoints, int clickedIndex)
        {
            int startIndex = Math.Max(0, clickedIndex - CLICK_RANGE);
            int endIndex = Math.Min(screenPoints.Count - 1, clickedIndex + CLICK_RANGE);
            
            for (int i = startIndex; i <= endIndex; i++)
            {
                var point = screenPoints[i];
                string numberText = (i + 1).ToString();
                
                bool isClickedPoint = (i == clickedIndex);
                
                if (isClickedPoint)
                {
                    var textBrush = _redBrush;
                    var bgBrush = GetOrCreateBrush(new RawColor4(1.0f, 1.0f, 1.0f, 1.0f));
                    var borderBrush = _redBrush;
                    
                    var textRect = new RawRectangleF(point.X - 15, point.Y - 25, point.X + 15, point.Y - 5);
                    
                    // 绘制背景
                    _renderTarget.FillRectangle(textRect, bgBrush);
                    _renderTarget.DrawRectangle(textRect, borderBrush, 2.0f);
                    
                    // 绘制文本
                    _renderTarget.DrawText(numberText, _boldTextFormat, textRect, textBrush);
                }
                else
                {
                    var textBrush = GetOrCreateBrush(new RawColor4(0.196f, 0.196f, 0.196f, 0.784f));
                    var bgBrush = GetOrCreateBrush(new RawColor4(1.0f, 1.0f, 1.0f, 0.941f));
                    var borderBrush = GetOrCreateBrush(new RawColor4(0.392f, 0.588f, 1.0f, 0.784f));
                    
                    var textRect = new RawRectangleF(point.X - 12, point.Y - 20, point.X + 12, point.Y - 4);
                    
                    // 绘制背景
                    _renderTarget.FillRectangle(textRect, bgBrush);
                    _renderTarget.DrawRectangle(textRect, borderBrush, 1.0f);
                    
                    // 绘制文本
                    _renderTarget.DrawText(numberText, _smallTextFormat, textRect, textBrush);
                }
            }
        }
        #endregion

        #region 控件事件和公共方法 - 保留原有逻辑
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            CreateDeviceResources();
            // 确保句柄创建后立即重绘
            if (_isInitialized)
            {
                Invalidate();
                Update();
            }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (this.Visible && _isInitialized)
            {
                LogMessage?.Invoke("控件变为可见，触发重绘");
                Invalidate();
                Update();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            
            if (_renderTarget != null)
            {
                _renderTarget.Resize(new SharpDX.Size2(Width, Height));
            }
            
            Invalidate();
        }

        public void ResetView()
        {
            _isInitialTransformCalculated = false;
            Invalidate();
        }

        /// <summary>
        /// 获取路径调试信息 - 兼容原有接口
        /// </summary>
        public string GetPathDebugInfo()
        {
            return $"Direct2DPathVisualizationControl: 路径点数={_pathElements.Count}, 圆形数={_pathCircles.Count}, 缩放={_scale:F2}, 偏移=({_offsetX:F2}, {_offsetY:F2})";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ReleaseDeviceResources();
                
                _textFormat?.Dispose();
                _boldTextFormat?.Dispose();
                _smallTextFormat?.Dispose();
                _writeFactory?.Dispose();
                _d2dFactory?.Dispose();
            }
            base.Dispose(disposing);
        }
        #endregion
    }
}