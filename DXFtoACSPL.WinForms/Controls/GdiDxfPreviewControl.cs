using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
using DXFtoACSPL.Core.Models;

namespace DXFtoACSPL.WinForms.Controls
{
    /// <summary>
    /// 基于GDI+的DXF预览控件 - 照抄PathVisualizationControl的渲染方式
    /// 专门用于显示DXF圆形数据，不使用OpenGL
    /// </summary>
    public class GdiDxfPreviewControl : UserControl
    {
        // 圆形数据
        private List<CircleEntity> _circles = new List<CircleEntity>();
        
        // 变换参数 - 完全照抄PathVisualizationControl
        private double _scale = 1.0;
        private double _offsetX = 0;
        private double _offsetY = 0;
        private Point _lastMouse;
        private bool _isPanning = false;
        private bool _isInitialTransformCalculated = false;
        
        // 鼠标交互
        private Point _currentMousePosition;
        private int _hoveredCircleIndex = -1;
        private int _clickedCircleIndex = -1;
        private const float HOVER_DISTANCE = 15f;
        
        // 缓存机制 - 完全照抄PathVisualizationControl
        private Bitmap? _cachedBitmap;
        private Graphics? _cachedGraphics;
        private bool _needsRedraw = true;
        private Rectangle _lastClientRect;
        private bool _isLowQuality = false;
        
        // 性能优化：拖拽时的简化绘制
        private bool _isDragging = false;
        private DateTime _lastDragTime = DateTime.MinValue;
        private const int DRAG_THROTTLE_MS = 50;
        
        // 显示选项
        private bool _showGrid = true;
        private bool _showAxes = true;
        
        // 颜色配置
        private Color _backgroundColor = Color.White;
        private Color _circleColor = Color.Blue;
        private Color _gridColor = Color.LightGray;
        private Color _axesColor = Color.Black;
        
        // 事件
        public event Action<CircleEntity>? CircleSelected;
        public event Action? ViewChanged;
        public event Action<string>? LogMessage;
        
        public GdiDxfPreviewControl()
        {
            InitializeComponent();
        }
        
        private void InitializeComponent()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | 
                         ControlStyles.UserPaint | 
                         ControlStyles.DoubleBuffer | 
                         ControlStyles.ResizeRedraw, true);
            
            this.BackColor = _backgroundColor;
            this.Size = new Size(800, 600);
            this.Name = "GdiDxfPreviewControl";
            
            // 鼠标事件
            this.MouseDown += OnMouseDown;
            this.MouseMove += OnMouseMove;
            this.MouseUp += OnMouseUp;
            this.MouseWheel += OnMouseWheel;
            
            LogMessage?.Invoke("✅ GDI+ DXF预览控件初始化完成");
        }
        
        /// <summary>
        /// 加载圆形数据
        /// </summary>
        public void LoadCircles(List<CircleEntity> circles)
        {
            _circles = circles?.ToList() ?? new List<CircleEntity>();
            _isInitialTransformCalculated = false;
            _needsRedraw = true;
            
            LogMessage?.Invoke($"📊 加载了 {_circles.Count} 个圆形");
            
            if (_circles.Count > 0)
            {
                var firstCircle = _circles[0];
                LogMessage?.Invoke($"📊 第一个圆形: 中心=({firstCircle.Center.X:F2}, {firstCircle.Center.Y:F2}), 半径={firstCircle.Radius:F2}");
            }
            
            this.Invalidate();
        }
        
        /// <summary>
        /// 计算圆形变换参数 - 完全照抄PathVisualizationControl的CalculatePathTransform
        /// </summary>
        private void CalculateCircleTransform()
        {
            if (_circles.Count == 0) return;
            
            // 计算圆形的边界（包括半径）
            double minX = _circles.Min(c => c.Center.X - c.Radius);
            double maxX = _circles.Max(c => c.Center.X + c.Radius);
            double minY = _circles.Min(c => c.Center.Y - c.Radius);
            double maxY = _circles.Max(c => c.Center.Y + c.Radius);
            
            double w = maxX - minX;
            double h = maxY - minY;
            if (w <= 0 || h <= 0) return;
            
            // 计算缩放比例，留出边距 - 完全照抄PathVisualizationControl
            double scaleX = (this.Width - 40) / w;
            double scaleY = (this.Height - 40) / h;
            _scale = Math.Min(scaleX, scaleY);
            _offsetX = (this.Width - w * _scale) / 2 - minX * _scale;
            // 翻转Y轴：CAD坐标系Y向上为正，屏幕坐标系Y向下为正
            _offsetY = (this.Height + h * _scale) / 2 + maxY * _scale;
            
            LogMessage?.Invoke($"🎨 CalculateCircleTransform: 边界({minX:F2}, {minY:F2}) - ({maxX:F2}, {maxY:F2}), 缩放={_scale:F2}, 偏移=({_offsetX:F2}, {_offsetY:F2})");
        }
        
        /// <summary>
        /// 确保缓存位图 - 完全照抄PathVisualizationControl
        /// </summary>
        private void EnsureCachedBitmap()
        {
            if (_cachedBitmap == null || _cachedBitmap.Width != Width || _cachedBitmap.Height != Height)
            {
                _cachedBitmap?.Dispose();
                _cachedGraphics?.Dispose();
                if (Width > 0 && Height > 0)
                {
                    _cachedBitmap = new Bitmap(Width, Height);
                    _cachedGraphics = Graphics.FromImage(_cachedBitmap);
                    _needsRedraw = true;
                    LogMessage?.Invoke($"🎨 缓存位图创建成功: {Width}x{Height}");
                }
            }
        }
        
        /// <summary>
        /// 绘制方法 - 完全照抄PathVisualizationControl的OnPaint逻辑
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            
            try
            {
                // 使用缓存位图来提高性能
                EnsureCachedBitmap();
                if (_cachedBitmap != null && _cachedGraphics != null)
                {
                    if (_needsRedraw || _lastClientRect != ClientRectangle)
                    {
                        _cachedGraphics.Clear(_backgroundColor);
                        
                        // 只在第一次计算变换参数，避免覆盖用户的拖拽缩放操作
                        if (!_isInitialTransformCalculated)
                        {
                            CalculateCircleTransform();
                            _isInitialTransformCalculated = true;
                        }
                        
                        // 绘制网格和坐标轴
                        if (_showGrid)
                            DrawGrid(_cachedGraphics);
                        if (_showAxes)
                            DrawAxes(_cachedGraphics);
                        
                        // 绘制圆形
                        DrawCircles(_cachedGraphics);
                        
                        _needsRedraw = false;
                        _lastClientRect = ClientRectangle;
                    }
                    e.Graphics.DrawImage(_cachedBitmap, 0, 0);
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"❌ 绘制失败: {ex.Message}");
                // 绘制错误信息
                using (var brush = new SolidBrush(Color.Red))
                {
                    e.Graphics.DrawString($"绘制错误: {ex.Message}", this.Font, brush, 10, 10);
                }
            }
        }
        
        /// <summary>
        /// 绘制圆形 - 基于PathVisualizationControl的DrawPath方法
        /// </summary>
        private void DrawCircles(Graphics g)
        {
            if (_circles == null || _circles.Count == 0)
            {
                LogMessage?.Invoke("🎨 DrawCircles: 跳过绘制，圆形列表为空");
                return;
            }
            
            LogMessage?.Invoke($"🎨 DrawCircles: 开始绘制 {_circles.Count} 个圆形");
            
            // 性能优化：拖拽时简化绘制
            bool isDragging = _isPanning;
            bool shouldDrawSimplified = isDragging && _circles.Count > 1000;
            
            if (shouldDrawSimplified || _isLowQuality)
            {
                // 简化绘制：只绘制部分圆
                int step = Math.Max(1, _circles.Count / 500);
                for (int i = 0; i < _circles.Count; i += step)
                {
                    DrawSingleCircle(g, _circles[i], i);
                }
                LogMessage?.Invoke($"🎨 简化绘制完成，绘制了 {_circles.Count / step} 个圆形");
            }
            else
            {
                // 正常绘制：绘制所有圆形
                for (int i = 0; i < _circles.Count; i++)
                {
                    DrawSingleCircle(g, _circles[i], i);
                }
                LogMessage?.Invoke($"🎨 正常绘制完成，绘制了 {_circles.Count} 个圆形");
            }
        }
        
        /// <summary>
        /// 绘制单个圆形
        /// </summary>
        private void DrawSingleCircle(Graphics g, CircleEntity circle, int index)
        {
            // 转换为屏幕坐标
            float x = (float)(circle.Center.X * _scale + _offsetX);
            float y = (float)(-circle.Center.Y * _scale + _offsetY); // Y轴翻转
            float radius = (float)(circle.Radius * _scale);
            
            // 选择颜色
            Color color = _circleColor;
            if (index == _hoveredCircleIndex)
                color = Color.Orange;
            else if (index == _clickedCircleIndex)
                color = Color.Red;
            
            // 绘制圆形
            using (var pen = new Pen(color, 1.2f))
            {
                g.DrawEllipse(pen, x - radius, y - radius, radius * 2, radius * 2);
            }
        }
        
        /// <summary>
        /// 绘制网格
        /// </summary>
        private void DrawGrid(Graphics g)
        {
            if (_circles.Count == 0) return;
            
            // 计算网格范围
            double minX = _circles.Min(c => c.Center.X - c.Radius);
            double maxX = _circles.Max(c => c.Center.X + c.Radius);
            double minY = _circles.Min(c => c.Center.Y - c.Radius);
            double maxY = _circles.Max(c => c.Center.Y + c.Radius);
            
            // 计算网格间距
            double gridSize = Math.Max(maxX - minX, maxY - minY) / 20;
            
            using (var pen = new Pen(_gridColor, 0.5f))
            {
                // 绘制垂直线
                for (double x = minX; x <= maxX; x += gridSize)
                {
                    float screenX = (float)(x * _scale + _offsetX);
                    float screenY1 = (float)(-minY * _scale + _offsetY);
                    float screenY2 = (float)(-maxY * _scale + _offsetY);
                    g.DrawLine(pen, screenX, screenY1, screenX, screenY2);
                }
                
                // 绘制水平线
                for (double y = minY; y <= maxY; y += gridSize)
                {
                    float screenY = (float)(-y * _scale + _offsetY);
                    float screenX1 = (float)(minX * _scale + _offsetX);
                    float screenX2 = (float)(maxX * _scale + _offsetX);
                    g.DrawLine(pen, screenX1, screenY, screenX2, screenY);
                }
            }
        }
        
        /// <summary>
        /// 绘制坐标轴
        /// </summary>
        private void DrawAxes(Graphics g)
        {
            if (_circles.Count == 0) return;
            
            using (var pen = new Pen(_axesColor, 2.0f))
            {
                // X轴
                float originX = (float)_offsetX;
                float originY = (float)_offsetY;
                g.DrawLine(pen, 0, originY, Width, originY);
                
                // Y轴
                g.DrawLine(pen, originX, 0, originX, Height);
            }
        }
        
        #region 鼠标交互 - 完全照抄PathVisualizationControl
        
        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle || e.Button == MouseButtons.Left)
            {
                _isPanning = true;
                _lastMouse = e.Location;
                this.Cursor = Cursors.Hand;
                _isDragging = true;
            }
        }
        
        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            _currentMousePosition = e.Location;
            
            if (_isPanning)
            {
                // 平移
                int deltaX = e.X - _lastMouse.X;
                int deltaY = e.Y - _lastMouse.Y;
                
                _offsetX += deltaX;
                _offsetY += deltaY;
                
                _lastMouse = e.Location;
                _needsRedraw = true;
                
                // 限制重绘频率
                if ((DateTime.Now - _lastDragTime).TotalMilliseconds > DRAG_THROTTLE_MS)
                {
                    this.Invalidate();
                    _lastDragTime = DateTime.Now;
                }
                
                ViewChanged?.Invoke();
            }
            else
            {
                // 检测悬停的圆形
                UpdateHoveredCircle(e.Location);
            }
        }
        
        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                this.Cursor = Cursors.Default;
                _isDragging = false;
                _needsRedraw = true;
                this.Invalidate();
            }
        }
        
        private void OnMouseWheel(object sender, MouseEventArgs e)
        {
            // 缩放
            double scaleFactor = e.Delta > 0 ? 1.1 : 0.9;
            
            // 以鼠标位置为中心缩放
            double mouseX = (e.X - _offsetX) / _scale;
            double mouseY = (e.Y - _offsetY) / _scale;
            
            _scale *= scaleFactor;
            
            // 限制缩放范围
            _scale = Math.Max(0.01, Math.Min(100.0, _scale));
            
            _offsetX = e.X - mouseX * _scale;
            _offsetY = e.Y - mouseY * _scale;
            
            _needsRedraw = true;
            this.Invalidate();
            ViewChanged?.Invoke();
        }
        
        /// <summary>
        /// 更新悬停的圆形
        /// </summary>
        private void UpdateHoveredCircle(Point mousePos)
        {
            int newHoveredIndex = -1;
            
            for (int i = 0; i < _circles.Count; i++)
            {
                var circle = _circles[i];
                float screenX = (float)(circle.Center.X * _scale + _offsetX);
                float screenY = (float)(-circle.Center.Y * _scale + _offsetY);
                float screenRadius = (float)(circle.Radius * _scale);
                
                // 检查鼠标是否在圆形边界附近
                float distance = (float)Math.Sqrt(Math.Pow(mousePos.X - screenX, 2) + Math.Pow(mousePos.Y - screenY, 2));
                if (Math.Abs(distance - screenRadius) < HOVER_DISTANCE)
                {
                    newHoveredIndex = i;
                    break;
                }
            }
            
            if (newHoveredIndex != _hoveredCircleIndex)
            {
                _hoveredCircleIndex = newHoveredIndex;
                _needsRedraw = true;
                this.Invalidate();
                
                if (_hoveredCircleIndex >= 0)
                {
                    CircleSelected?.Invoke(_circles[_hoveredCircleIndex]);
                }
            }
        }
        
        #endregion
        
        /// <summary>
        /// 清除所有圆形数据
        /// </summary>
        public void Clear()
        {
            _circles.Clear();
            _hoveredCircleIndex = -1;
            _clickedCircleIndex = -1;
            _isInitialTransformCalculated = false;
            _needsRedraw = true;
            this.Invalidate();
            LogMessage?.Invoke("🧹 预览控件已清除");
        }
        
        /// <summary>
        /// 重置视图
        /// </summary>
        public void ResetView()
        {
            _isInitialTransformCalculated = false;
            _needsRedraw = true;
            this.Invalidate();
        }
        
        /// <summary>
        /// 清理资源
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cachedBitmap?.Dispose();
                _cachedGraphics?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}