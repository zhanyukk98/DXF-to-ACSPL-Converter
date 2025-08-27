using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
using DXFtoACSPL.Core.Models;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using AlphaMode = SharpDX.Direct2D1.AlphaMode;
using Factory = SharpDX.Direct2D1.Factory;

namespace DXFtoACSPL.WinForms.Controls
{
    /// <summary>
    /// 基于Direct2D的DXF预览控件
    /// 提供高质量的硬件加速渲染，解决SkiaSharp的重叠问题
    /// </summary>
    public class Direct2DDxfPreviewControl : UserControl
    {
        // Direct2D 对象
        private Factory? _d2dFactory;
        private RenderTarget? _renderTarget;
        private SolidColorBrush? _circleBrush;
        private SolidColorBrush? _gridBrush;
        private SolidColorBrush? _axisBrush;
        private SolidColorBrush? _backgroundBrush;
        private SolidColorBrush? _hoveredBrush;
        private SolidColorBrush? _selectedBrush;
        
        // 画刷缓存，避免频繁创建
        private Dictionary<string, SolidColorBrush> _brushCache = new Dictionary<string, SolidColorBrush>();
        
        // 静态内容标记（保留用于将来可能的优化）
        // private bool _staticContentNeedsUpdate = true; // 暂时移除，避免编译警告
        
        // 批量绘制优化
        private Dictionary<string, Geometry>? _geometryGroups;
        private bool _geometryNeedsUpdate = true;
        
        // 圆形数据
        private List<CircleEntity> _circles = new List<CircleEntity>();
        private List<CircleEntity> _overlayCircles = new List<CircleEntity>();
        
        // 变换参数
        private float _scale = 1.0f;
        private float _panX = 0.0f;
        private float _panY = 0.0f;
        private RawRectangleF _modelBounds = new RawRectangleF();
        
        // 鼠标交互状态
        private bool _isDragging = false;
        private Point _lastMousePosition;
        private int _hoveredCircleIndex = -1;
        private int _selectedCircleIndex = -1;
        private DateTime _lastWheelTime = DateTime.MinValue;
        
        // 事件
        public event Action<CircleEntity>? CircleSelected;
        private const float HOVER_DISTANCE = 10.0f;
        
        // 移除定时器，改为事件驱动渲染以提升性能
        
        // 显示选项
        private bool _showGrid = true;
        private bool _showAxes = true;
        private bool _isInitialized = false;
        
        // 颜色配置
        private readonly System.Drawing.Color[] _circleColors = new System.Drawing.Color[]
        {
            System.Drawing.Color.Blue,
            System.Drawing.Color.Red,
            System.Drawing.Color.Green,
            System.Drawing.Color.Orange,
            System.Drawing.Color.Purple,
            System.Drawing.Color.Brown,
            System.Drawing.Color.Pink,
            System.Drawing.Color.Gray
        };
        
        public Direct2DDxfPreviewControl()
        {
            // 移除 DoubleBuffer，因为 Direct2D 有自己的缓冲机制
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Opaque, true); // 避免背景重绘
            SetStyle(ControlStyles.SupportsTransparentBackColor, false); // 确保不透明背景
            
            this.BackColor = System.Drawing.Color.White;
            this.Cursor = Cursors.Cross;
            
            // 初始化Direct2D
            InitializeDirect2D();
            
            // 确保控件可见时立即重绘
            this.Load += (s, e) => {
                Invalidate();
                Update();
            };
            
            Console.WriteLine("Direct2DDxfPreviewControl 初始化完成");
        }
        
        /// <summary>
        /// 初始化Direct2D资源
        /// </summary>
        private void InitializeDirect2D()
        {
            try
            {
                // 创建Direct2D工厂
                _d2dFactory = new Factory();
                
                Console.WriteLine("Direct2D工厂创建成功");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"初始化Direct2D失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 创建渲染目标和画刷
        /// </summary>
        private void CreateRenderTarget()
        {
            if (_d2dFactory == null || this.Handle == IntPtr.Zero)
                return;
                
            try
            {
                // 释放旧的渲染目标
                DisposeRenderTarget();
                
                // 创建HWND渲染目标
                var renderTargetProperties = new RenderTargetProperties()
                {
                    Type = RenderTargetType.Default,
                    PixelFormat = new PixelFormat(Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied),
                    DpiX = 96.0f,
                    DpiY = 96.0f,
                    Usage = RenderTargetUsage.None,
                    MinLevel = FeatureLevel.Level_DEFAULT
                };
                
                var hwndRenderTargetProperties = new HwndRenderTargetProperties()
                {
                    Hwnd = this.Handle,
                    PixelSize = new SharpDX.Size2(this.ClientSize.Width, this.ClientSize.Height),
                    PresentOptions = PresentOptions.None
                };
                
                _renderTarget = new WindowRenderTarget(_d2dFactory, renderTargetProperties, hwndRenderTargetProperties);
                
                // 创建画刷
                CreateBrushes();
                
                _isInitialized = true;
                Console.WriteLine("Direct2D渲染目标创建成功");
                
                // 渲染目标创建成功后立即触发重绘
                if (this.Visible && this.Handle != IntPtr.Zero)
                {
                    Console.WriteLine("渲染目标创建后触发重绘");
                    Invalidate();
                    Update();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"创建渲染目标失败: {ex.Message}");
                _isInitialized = false;
            }
        }
        
        /// <summary>
        /// 创建画刷
        /// </summary>
        private void CreateBrushes()
        {
            if (_renderTarget == null)
                return;
                
            try
            {
                _backgroundBrush = new SolidColorBrush(_renderTarget, new RawColor4(1.0f, 1.0f, 1.0f, 1.0f)); // 白色
                _circleBrush = new SolidColorBrush(_renderTarget, new RawColor4(0.0f, 0.0f, 1.0f, 1.0f)); // 蓝色
                _gridBrush = new SolidColorBrush(_renderTarget, new RawColor4(0.9f, 0.9f, 0.9f, 1.0f)); // 浅灰色
                _axisBrush = new SolidColorBrush(_renderTarget, new RawColor4(0.5f, 0.5f, 0.5f, 1.0f)); // 灰色
                _hoveredBrush = new SolidColorBrush(_renderTarget, new RawColor4(1.0f, 0.5f, 0.0f, 1.0f)); // 橙色
                _selectedBrush = new SolidColorBrush(_renderTarget, new RawColor4(1.0f, 0.0f, 0.0f, 1.0f)); // 红色
                
                Console.WriteLine("Direct2D画刷创建成功");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"创建画刷失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 释放渲染目标和相关资源
        /// </summary>
        private void DisposeRenderTarget()
        {
            _backgroundBrush?.Dispose();
            _circleBrush?.Dispose();
            _gridBrush?.Dispose();
            _axisBrush?.Dispose();
            _hoveredBrush?.Dispose();
            _selectedBrush?.Dispose();
            
            // 清理画刷缓存
            foreach (var brush in _brushCache.Values)
            {
                brush?.Dispose();
            }
            _brushCache.Clear();
            
            // 重置静态内容标记
            // _staticContentNeedsUpdate = true; // 已移除
            
            // 清理几何组
            if (_geometryGroups != null)
            {
                foreach (var group in _geometryGroups.Values)
                {
                    group?.Dispose();
                }
                _geometryGroups.Clear();
                _geometryGroups = null;
            }
            _geometryNeedsUpdate = true;
            
            _renderTarget?.Dispose();
            
            _backgroundBrush = null;
            _circleBrush = null;
            _gridBrush = null;
            _axisBrush = null;
            _hoveredBrush = null;
            _selectedBrush = null;
            _renderTarget = null;
            
            _isInitialized = false;
        }
        
        /// <summary>
        /// 加载圆形数据
        /// </summary>
        public void LoadCircles(List<CircleEntity> circles)
        {
            _circles = circles ?? new List<CircleEntity>();
            
            if (_circles.Any())
            {
                CalculateModelBounds();
                ResetView();
            }
            
            _geometryNeedsUpdate = true; // 数据变化时需要更新几何组
            // 强制重绘
            Invalidate();
            Update(); // 立即处理重绘消息
            Console.WriteLine($"加载了 {_circles.Count} 个圆形");
        }

        /// <summary>
        /// 清除所有圆形数据
        /// </summary>
        public void Clear()
        {
            _circles.Clear();
            _overlayCircles.Clear();
            _modelBounds = new RawRectangleF();
            
            _geometryNeedsUpdate = true; // 数据清除时需要更新几何组
            ResetView();
            Invalidate();
        }

        /// <summary>
        /// 设置圆形数据（与其他预览控件保持接口一致）
        /// </summary>
        public void SetCircles(List<CircleEntity> circles, RectangleF bounds)
        {
            LoadCircles(circles);
        }
        
        /// <summary>
        /// 设置叠加圆形（处理后的圆形）
        /// </summary>
        public void SetOverlayCircles(List<CircleEntity> overlayCircles)
        {
            _overlayCircles = overlayCircles?.ToList() ?? new List<CircleEntity>();
            _geometryNeedsUpdate = true; // 叠加数据变化时需要更新几何组
            Invalidate();
        }
        
        /// <summary>
        /// 计算模型边界
        /// </summary>
        private void CalculateModelBounds()
        {
            if (!_circles.Any())
            {
                _modelBounds = new RawRectangleF(0, 0, 100, 100);
                return;
            }
            
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            
            foreach (var circle in _circles)
            {
                float left = circle.Center.X - circle.Radius;
                float right = circle.Center.X + circle.Radius;
                float top = circle.Center.Y - circle.Radius;
                float bottom = circle.Center.Y + circle.Radius;
                
                minX = Math.Min(minX, left);
                maxX = Math.Max(maxX, right);
                minY = Math.Min(minY, top);
                maxY = Math.Max(maxY, bottom);
            }
            
            _modelBounds = new RawRectangleF(minX, minY, maxX, maxY);
            Console.WriteLine($"模型边界: ({minX:F2}, {minY:F2}) - ({maxX:F2}, {maxY:F2})");
        }
        
        /// <summary>
        /// 重置视图到适合窗口的缩放和位置
        /// </summary>
        public void ResetView()
        {
            if (this.ClientSize.Width <= 0 || this.ClientSize.Height <= 0)
                return;
                
            float modelWidth = _modelBounds.Right - _modelBounds.Left;
            float modelHeight = _modelBounds.Bottom - _modelBounds.Top;
            
            if (modelWidth <= 0 || modelHeight <= 0)
            {
                _scale = 1.0f;
                _panX = 0.0f;
                _panY = 0.0f;
                return;
            }
            
            // 计算缩放比例，预留边距
            float margin = 40.0f;
            float scaleX = (this.ClientSize.Width - 2 * margin) / modelWidth;
            float scaleY = (this.ClientSize.Height - 2 * margin) / modelHeight;
            _scale = Math.Min(scaleX, scaleY);
            
            // 限制最小缩放比例
            _scale = Math.Max(_scale, 0.01f);
            
            // 计算居中偏移
            float modelCenterX = (_modelBounds.Left + _modelBounds.Right) / 2.0f;
            float modelCenterY = (_modelBounds.Top + _modelBounds.Bottom) / 2.0f;
            
            _panX = this.ClientSize.Width / 2.0f - modelCenterX * _scale;
            _panY = this.ClientSize.Height / 2.0f - modelCenterY * _scale;
            
            Console.WriteLine($"重置视图: 缩放={_scale:F4}, 平移=({_panX:F2}, {_panY:F2})");
            Invalidate();
        }
        
        /// <summary>
        /// 世界坐标转屏幕坐标
        /// </summary>
        private RawVector2 WorldToScreen(float worldX, float worldY)
        {
            return new RawVector2(
                worldX * _scale + _panX,
                this.ClientSize.Height - (worldY * _scale + _panY)  // 翻转 Y 轴
            );
        }
        
        /// <summary>
        /// 屏幕坐标转世界坐标
        /// </summary>
        private RawVector2 ScreenToWorld(float screenX, float screenY)
        {
            return new RawVector2(
                (screenX - _panX) / _scale,
                (this.ClientSize.Height - screenY - _panY) / _scale  // 翻转 Y 轴
            );
        }
        
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            CreateRenderTarget();
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
                Console.WriteLine("控件变为可见，触发重绘");
                Invalidate();
                Update();
            }
        }
        
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            
            if (_renderTarget is WindowRenderTarget hwndTarget)
            {
                try
                {
                    hwndTarget.Resize(new SharpDX.Size2(this.ClientSize.Width, this.ClientSize.Height));
                    // _staticContentNeedsUpdate = true; // 窗口大小改变时需要更新静态内容 - 已移除
                    Invalidate();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"调整渲染目标大小失败: {ex.Message}");
                    // 重新创建渲染目标
                    CreateRenderTarget();
                }
            }
        }
        
        protected override void OnPaint(PaintEventArgs e)
        {
            // 确保Direct2D已初始化
            if (!_isInitialized || _renderTarget == null)
            {
                CreateRenderTarget();
                if (!_isInitialized || _renderTarget == null)
                {
                    // 如果Direct2D仍未初始化，使用GDI+绘制简单内容
                    base.OnPaint(e);
                    e.Graphics.Clear(System.Drawing.Color.White);
                    e.Graphics.DrawString("Direct2D 初始化中...", this.Font, System.Drawing.Brushes.Black, 10, 10);
                    return;
                }
            }
            
            // 调用基类的 OnPaint 以确保正确的消息处理
            // base.OnPaint(e); // 注释掉，因为我们使用 Direct2D 完全接管绘制
            
            try
            {
                _renderTarget.BeginDraw();
                
                // 清除背景
                _renderTarget.Clear(new RawColor4(1.0f, 1.0f, 1.0f, 1.0f));
                
                // 直接绘制静态内容（网格和坐标轴）
                if (_showGrid)
                    DrawGridToTarget();
                if (_showAxes)
                    DrawAxesToTarget();
                    
                // 绘制圆形
                DrawCircles();
                
                try
                {
                    _renderTarget.EndDraw();
                }
                catch (SharpDX.SharpDXException ex) when (ex.ResultCode == SharpDX.DXGI.ResultCode.DeviceRemoved || 
                                                          ex.ResultCode == SharpDX.DXGI.ResultCode.DeviceReset)
                {
                    Console.WriteLine("Direct2D设备丢失，重新创建渲染目标");
                    CreateRenderTarget();
                    Invalidate(); // 强制重绘
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Direct2D绘制失败: {ex.Message}");
                
                // 如果Direct2D绘制失败，重新创建渲染目标
                CreateRenderTarget();
                
                // 使用GDI+作为后备
                e.Graphics.Clear(System.Drawing.Color.White);
                e.Graphics.DrawString($"Direct2D 错误: {ex.Message}", this.Font, System.Drawing.Brushes.Red, 10, 10);
                
                // 延迟重绘
                this.BeginInvoke(new Action(() => Invalidate()));
            }
        }
        

        
        /// <summary>
        /// 绘制网格到当前渲染目标
        /// </summary>
        private void DrawGridToTarget()
        {
            if (_gridBrush == null || _renderTarget == null)
                return;
                
            try
            {
                // 计算网格间距
                float gridSpacing = CalculateGridSpacing();
                
                // 计算可见区域
                var topLeft = ScreenToWorld(0, 0);
                var bottomRight = ScreenToWorld(this.ClientSize.Width, this.ClientSize.Height);
                
                // 绘制垂直线
                float startX = (float)(Math.Floor(topLeft.X / gridSpacing) * gridSpacing);
                for (float x = startX; x <= bottomRight.X; x += gridSpacing)
                {
                    var start = WorldToScreen(x, topLeft.Y);
                    var end = WorldToScreen(x, bottomRight.Y);
                    _renderTarget.DrawLine(start, end, _gridBrush, 0.5f);
                }
                
                // 绘制水平线
                float startY = (float)(Math.Floor(topLeft.Y / gridSpacing) * gridSpacing);
                for (float y = startY; y <= bottomRight.Y; y += gridSpacing)
                {
                    var start = WorldToScreen(topLeft.X, y);
                    var end = WorldToScreen(bottomRight.X, y);
                    _renderTarget.DrawLine(start, end, _gridBrush, 0.5f);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"绘制网格失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 绘制网格（兼容旧接口）
        /// </summary>
        private void DrawGrid()
        {
            // 直接绘制网格
            DrawGridToTarget();
        }
        
        /// <summary>
        /// 绘制坐标轴到当前渲染目标 - 参考PathVisualizationControl实现图像中心化和清晰坐标轴
        /// </summary>
        private void DrawAxesToTarget()
        {
            if (_renderTarget == null || _circles.Count == 0)
                return;
                
            try
            {
                // 计算图形内容的实际中心点（参考PathVisualizationControl）
                float centerX = _circles.Average(c => c.Center.X);
                float centerY = _circles.Average(c => c.Center.Y);
                
                // 转换到屏幕坐标
                var screenCenter = WorldToScreen(centerX, centerY);
                
                // 创建清晰明亮的画刷
                 using (var xAxisBrush = new SolidColorBrush(_renderTarget, new RawColor4(1.0f, 0.0f, 0.0f, 1.0f))) // 鲜红色
                 using (var yAxisBrush = new SolidColorBrush(_renderTarget, new RawColor4(0.0f, 0.8f, 0.2f, 1.0f))) // 明亮的绿色（降低饱和度）
                 using (var originBrush = new SolidColorBrush(_renderTarget, new RawColor4(0.0f, 0.0f, 1.0f, 1.0f))) // 蓝色
                {
                    // X轴 - 通过图形中心的水平线（鲜红色，加粗）
                    var xStart = new RawVector2(0, screenCenter.Y);
                    var xEnd = new RawVector2(this.ClientSize.Width, screenCenter.Y);
                    _renderTarget.DrawLine(xStart, xEnd, xAxisBrush, 2.0f);
                    
                    // Y轴 - 通过图形中心的垂直线（鲜绿色，加粗）
                    var yStart = new RawVector2(screenCenter.X, 0);
                    var yEnd = new RawVector2(screenCenter.X, this.ClientSize.Height);
                    _renderTarget.DrawLine(yStart, yEnd, yAxisBrush, 2.0f);
                    
                    // 绘制原点标记（蓝色圆点）
                    var originEllipse = new Ellipse(new RawVector2(screenCenter.X, screenCenter.Y), 5.0f, 5.0f);
                    _renderTarget.DrawEllipse(originEllipse, originBrush, 2.0f);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"绘制坐标轴失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 更新几何组（批量绘制优化）
        /// </summary>
        private void UpdateGeometryGroups()
        {
            if (!_geometryNeedsUpdate || _renderTarget == null || _d2dFactory == null)
                return;
                
            try
            {
                // 清理旧的几何组
                if (_geometryGroups != null)
                {
                    foreach (var group in _geometryGroups.Values)
                    {
                        group?.Dispose();
                    }
                    _geometryGroups.Clear();
                }
                else
                {
                    _geometryGroups = new Dictionary<string, Geometry>();
                }
                
                // 按实体类型分组圆形
                var circlesByType = _circles.GroupBy(c => c.EntityType).ToList();
                
                foreach (var group in circlesByType)
                {
                    var entityType = group.Key;
                    var circles = group.ToList();
                    
                    if (circles.Count == 0)
                        continue;
                        
                    // 创建路径几何
                    var pathGeometry = new PathGeometry(_d2dFactory);
                    var sink = pathGeometry.Open();
                    
                    foreach (var circle in circles)
                    {
                        // 转换到屏幕坐标
                        var center = WorldToScreen(circle.Center.X, circle.Center.Y);
                        float radius = circle.Radius * _scale;
                        
                        // 使用两个半圆弧来绘制完整的圆
                        var startPoint = new RawVector2(center.X - radius, center.Y);
                        var endPoint = new RawVector2(center.X + radius, center.Y);
                        
                        sink.BeginFigure(startPoint, FigureBegin.Filled);
                        
                        // 上半圆
                        sink.AddArc(new ArcSegment
                        {
                            Point = endPoint,
                            Size = new Size2F(radius, radius),
                            RotationAngle = 0,
                            SweepDirection = SweepDirection.Clockwise,
                            ArcSize = ArcSize.Small
                        });
                        
                        // 下半圆
                        sink.AddArc(new ArcSegment
                        {
                            Point = startPoint,
                            Size = new Size2F(radius, radius),
                            RotationAngle = 0,
                            SweepDirection = SweepDirection.Clockwise,
                            ArcSize = ArcSize.Small
                        });
                        
                        sink.EndFigure(FigureEnd.Closed);
                    }
                    
                    sink.Close();
                    sink.Dispose();
                    _geometryGroups[entityType] = pathGeometry;
                }
                
                _geometryNeedsUpdate = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新几何组失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 计算合适的网格间距
        /// </summary>
        private float CalculateGridSpacing()
        {
            float modelWidth = _modelBounds.Right - _modelBounds.Left;
            float modelHeight = _modelBounds.Bottom - _modelBounds.Top;
            float maxDimension = Math.Max(modelWidth, modelHeight);
            
            // 基于模型大小计算网格间距
            float baseSpacing = maxDimension / 20.0f;
            
            // 调整到合适的数值
            float[] spacings = { 0.1f, 0.2f, 0.5f, 1.0f, 2.0f, 5.0f, 10.0f, 20.0f, 50.0f, 100.0f, 200.0f, 500.0f };
            
            foreach (float spacing in spacings)
            {
                if (spacing >= baseSpacing)
                    return spacing;
            }
            
            return spacings[spacings.Length - 1];
        }
        
        /// <summary>
        /// 绘制坐标轴（兼容旧接口）
        /// </summary>
        private void DrawAxes()
        {
            // 坐标轴已经包含在静态内容的预渲染中，这里不需要额外绘制
            // 如果需要单独绘制坐标轴，可以调用 DrawAxesToTarget()
        }
        
        /// <summary>
        /// 绘制圆形
        /// </summary>
        private void DrawCircles()
        {
            if (_renderTarget == null)
                return;
                
            try
            {
                // 更新几何组（如果需要）
                UpdateGeometryGroups();
                
                // 计算线条粗细 - 更细更精致
                float strokeWidth = Math.Max(0.3f, 0.8f / _scale);
                strokeWidth = Math.Min(strokeWidth, 2.0f);
                
                // 使用几何组批量绘制（排除选中和悬停的圆形）
                if (_geometryGroups != null)
                {
                    foreach (var kvp in _geometryGroups)
                    {
                        var entityType = kvp.Key;
                        var geometryGroup = kvp.Value;
                        var brush = GetCircleBrush(entityType);
                        
                        if (brush != null && geometryGroup != null)
                        {
                            _renderTarget.DrawGeometry(geometryGroup, brush, strokeWidth);
                        }
                    }
                }
                
                // 单独绘制选中和悬停的圆形（覆盖批量绘制的效果）
                if (_selectedCircleIndex >= 0 && _selectedCircleIndex < _circles.Count)
                {
                    DrawSingleCircle(_selectedCircleIndex, _selectedBrush, strokeWidth * 1.5f);
                }
                
                if (_hoveredCircleIndex >= 0 && _hoveredCircleIndex < _circles.Count && _hoveredCircleIndex != _selectedCircleIndex)
                {
                    DrawSingleCircle(_hoveredCircleIndex, _hoveredBrush, strokeWidth * 1.2f);
                }
                
                Console.WriteLine($"批量绘制了 {_circles.Count} 个圆形，缩放={_scale:F4}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"绘制圆形失败: {ex.Message}");
                // 回退到逐个绘制
                DrawCirclesIndividually();
            }
        }
        
        /// <summary>
        /// 绘制单个圆形（用于选中和悬停状态）
        /// </summary>
        private void DrawSingleCircle(int index, SolidColorBrush? brush, float strokeWidth)
        {
            if (brush == null || index < 0 || index >= _circles.Count)
                return;
                
            var circle = _circles[index];
            var center = WorldToScreen(circle.Center.X, circle.Center.Y);
            float radius = circle.Radius * _scale;
            var ellipse = new Ellipse(center, radius, radius);
            
            _renderTarget.DrawEllipse(ellipse, brush, strokeWidth);
        }
        
        /// <summary>
        /// 回退方法：逐个绘制圆形
        /// </summary>
        private void DrawCirclesIndividually()
        {
            try
            {
                for (int i = 0; i < _circles.Count; i++)
                {
                    var circle = _circles[i];
                    
                    // 选择画刷颜色
                    SolidColorBrush? brush = null;
                    if (i == _selectedCircleIndex)
                        brush = _selectedBrush;
                    else if (i == _hoveredCircleIndex)
                        brush = _hoveredBrush;
                    else
                        brush = GetCircleBrush(circle.EntityType);
                        
                    if (brush == null)
                        continue;
                        
                    // 转换到屏幕坐标
                    var center = WorldToScreen(circle.Center.X, circle.Center.Y);
                    float radius = circle.Radius * _scale;
                    
                    // 创建椭圆几何
                    var ellipse = new Ellipse(center, radius, radius);
                    
                    // 计算线条粗细 - 更细更精致
                    float strokeWidth = Math.Max(0.3f, 0.8f / _scale);
                    strokeWidth = Math.Min(strokeWidth, 2.0f);
                    
                    // 绘制圆形
                    _renderTarget.DrawEllipse(ellipse, brush, strokeWidth);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"逐个绘制圆形失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 根据实体类型获取圆形画刷（使用缓存避免重复创建）
        /// </summary>
        private SolidColorBrush? GetCircleBrush(string entityType)
        {
            if (_renderTarget == null)
                return null;
                
            // 检查缓存中是否已有该类型的画刷
            if (_brushCache.TryGetValue(entityType, out var cachedBrush))
            {
                return cachedBrush;
            }
                
            // 根据实体类型选择颜色
            var color = entityType switch
            {
                "CIRCLE" => new RawColor4(0.0f, 0.0f, 1.0f, 1.0f), // 蓝色
                "ARC" => new RawColor4(1.0f, 0.0f, 0.0f, 1.0f),    // 红色
                "POLYLINE" => new RawColor4(0.8f, 0.4f, 0.0f, 1.0f), // 橙色
                _ => new RawColor4(0.5f, 0.5f, 0.5f, 1.0f)          // 灰色
            };
            
            try
            {
                var brush = new SolidColorBrush(_renderTarget, color);
                _brushCache[entityType] = brush; // 缓存画刷
                return brush;
            }
            catch
            {
                return _circleBrush;
            }
        }
        
        /// <summary>
        /// 鼠标移动事件
        /// </summary>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            
            if (_isDragging)
            {
                // 平移 - Y轴需要反向以匹配翻转的坐标系
                _panX += e.X - _lastMousePosition.X;
                _panY -= e.Y - _lastMousePosition.Y;  // 反向Y轴拖拽
                _lastMousePosition = e.Location;
                // _staticContentNeedsUpdate = true; // 平移时需要更新静态内容 - 已移除
                _geometryNeedsUpdate = true; // 平移时需要更新几何组
                Invalidate();
            }
            else
            {
                // 检测悬停的圆形
                UpdateHoveredCircle(e.Location);
            }
        }
        
        /// <summary>
        /// 更新悬停的圆形
        /// </summary>
        private void UpdateHoveredCircle(Point mousePos)
        {
            int newHoveredIndex = -1;
            var worldPos = ScreenToWorld(mousePos.X, mousePos.Y);
            
            for (int i = 0; i < _circles.Count; i++)
            {
                var circle = _circles[i];
                float dx = worldPos.X - circle.Center.X;
                float dy = worldPos.Y - circle.Center.Y;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                
                if (distance <= circle.Radius + HOVER_DISTANCE / _scale)
                {
                    newHoveredIndex = i;
                    break;
                }
            }
            
            if (newHoveredIndex != _hoveredCircleIndex)
            {
                _hoveredCircleIndex = newHoveredIndex;
                Invalidate();
                
                // 更新光标
                this.Cursor = _hoveredCircleIndex >= 0 ? Cursors.Hand : Cursors.Cross;
            }
        }
        
        /// <summary>
        /// 鼠标按下事件
        /// </summary>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            
            if (e.Button == MouseButtons.Left)
            {
                // 左键始终用于拖动，无论是否在圆形内部
                _isDragging = true;
                _lastMousePosition = e.Location;
                this.Cursor = Cursors.SizeAll;
            }
            else if (e.Button == MouseButtons.Right && _hoveredCircleIndex >= 0)
            {
                // 右键用于选择圆形
                _selectedCircleIndex = _hoveredCircleIndex;
                
                // 触发圆形选择事件
                if (_selectedCircleIndex < _circles.Count)
                {
                    CircleSelected?.Invoke(_circles[_selectedCircleIndex]);
                }
                
                Invalidate();
            }
        }
        
        /// <summary>
        /// 鼠标释放事件
        /// </summary>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            
            if (e.Button == MouseButtons.Left && _isDragging)
            {
                _isDragging = false;
                this.Cursor = _hoveredCircleIndex >= 0 ? Cursors.Hand : Cursors.Cross;
            }
        }
        
        /// <summary>
        /// 鼠标滚轮事件
        /// </summary>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            
            // 记录滚轮操作时间
            _lastWheelTime = DateTime.Now;
            
            // 缩放
            float scaleFactor = e.Delta > 0 ? 1.1f : 0.9f;
            
            // 以鼠标位置为中心缩放
            var worldPos = ScreenToWorld(e.X, e.Y);
            
            _scale *= scaleFactor;
            _scale = Math.Max(_scale, 0.001f);
            _scale = Math.Min(_scale, 1000.0f);
            
            // 调整平移以保持鼠标位置不变
            var newScreenPos = WorldToScreen(worldPos.X, worldPos.Y);
            _panX += e.X - newScreenPos.X;
            _panY -= e.Y - newScreenPos.Y;  // Y轴需要反向调整以匹配翻转的坐标系
            
            // _staticContentNeedsUpdate = true; // 缩放时需要更新静态内容 - 已移除
            _geometryNeedsUpdate = true; // 缩放时需要更新几何组
            Invalidate();
        }
        
        /// <summary>
        /// 双击重置视图
        /// </summary>
        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            // 只有在没有进行拖拽操作且距离上次滚轮操作超过500毫秒时才重置视图
            if (!_isDragging && (DateTime.Now - _lastWheelTime).TotalMilliseconds > 500)
            {
                ResetView();
            }
        }
        
        /// <summary>
        /// 释放资源
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeRenderTarget();
                _d2dFactory?.Dispose();
                _d2dFactory = null;
            }
            
            base.Dispose(disposing);
        }
        
        /// <summary>
        /// 显示/隐藏网格
        /// </summary>
        public void ToggleGrid()
        {
            _showGrid = !_showGrid;
            Invalidate();
        }
        
        /// <summary>
        /// 获取或设置是否显示坐标轴
        /// </summary>
        public bool ShowAxes
        {
            get => _showAxes;
            set
            {
                if (_showAxes != value)
                {
                    _showAxes = value;
                    Invalidate();
                }
            }
        }
        
        /// <summary>
        /// 显示/隐藏坐标轴
        /// </summary>
        public void ToggleAxes()
        {
            _showAxes = !_showAxes;
            Invalidate();
        }
        
        /// <summary>
        /// 获取当前选中的圆形
        /// </summary>
        public CircleEntity? GetSelectedCircle()
        {
            if (_selectedCircleIndex >= 0 && _selectedCircleIndex < _circles.Count)
                return _circles[_selectedCircleIndex];
            return null;
        }
        
        /// <summary>
        /// 清除选择
        /// </summary>
        public void ClearSelection()
        {
            _selectedCircleIndex = -1;
            _hoveredCircleIndex = -1;
            Invalidate();
        }
    }
}