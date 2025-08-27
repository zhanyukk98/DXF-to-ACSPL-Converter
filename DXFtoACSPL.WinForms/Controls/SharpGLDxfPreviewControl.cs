using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DXFtoACSPL.Core.Models;
using SharpGL;
using SharpGL.WinForms;
using SharpGL.Enumerations;

namespace DXFtoACSPL.WinForms.Controls
{
    /// <summary>
    /// 基于 SharpGL 的 DXF 预览控件
    /// </summary>
    public partial class SharpGLDxfPreviewControl : UserControl
    {
        private OpenGLControl _openGLControl;
        private List<CircleEntity> _circles = new List<CircleEntity>();
        private List<CircleEntity> _overlayCircles = new List<CircleEntity>();
        private RectangleF _modelBounds = RectangleF.Empty;
        
        // 视图变换参数
        private float _scale = 1.0f;
        private float _panX = 0.0f;
        private float _panY = 0.0f;
        private bool _isPanning = false;
        private Point _lastMousePosition;
        
        // 渲染参数
        private bool _showGrid = true;
        private bool _showAxes = true;
        private Color _backgroundColor = Color.Black;  // 改为黑色背景
        private Color _circleColor = Color.Lime;       // 改为绿色圆形
        private Color _overlayCircleColor = Color.Red;
        private Color _gridColor = Color.Gray;
        private Color _axesColor = Color.Yellow;       // 改为黄色坐标轴
        
        // 事件
        public event Action<CircleEntity>? CircleSelected;
        public event Action? ViewChanged;
        
        public SharpGLDxfPreviewControl()
        {
            InitializeComponent();
            SetupOpenGL();
        }
        
        private void InitializeComponent()
        {
            this.SuspendLayout();
            
            // 设置控件属性
            this.BackColor = _backgroundColor;
            this.Size = new Size(800, 600);
            this.Name = "SharpGLDxfPreviewControl";
            
            this.ResumeLayout(false);
        }
        
        private void SetupOpenGL()
        {
            // 创建 OpenGL 控件
            _openGLControl = new OpenGLControl()
            {
                Dock = DockStyle.Fill,
                DrawFPS = false,
                FrameRate = 60,
                RenderTrigger = RenderTrigger.TimerBased
            };
            
            // 绑定事件
            _openGLControl.OpenGLInitialized += OnOpenGLInitialized;
            _openGLControl.OpenGLDraw += OnOpenGLDraw;
            _openGLControl.Resized += OnOpenGLResized;
            _openGLControl.MouseDown += OnMouseDown;
            _openGLControl.MouseMove += OnMouseMove;
            _openGLControl.MouseUp += OnMouseUp;
            _openGLControl.MouseWheel += OnMouseWheel;
            
            this.Controls.Add(_openGLControl);
        }
        
        private void OnOpenGLInitialized(object sender, EventArgs e)
        {
            try
            {
                var gl = _openGLControl.OpenGL;
                
                Console.WriteLine("🔧 开始初始化 SharpGL...");
                
                // 设置背景色
                gl.ClearColor(_backgroundColor.R / 255.0f, _backgroundColor.G / 255.0f, 
                             _backgroundColor.B / 255.0f, 1.0f);
                
                // 关闭深度测试（2D图形不需要）
                gl.Disable(OpenGL.GL_DEPTH_TEST);
                
                // 启用抗锯齿
                gl.Enable(OpenGL.GL_LINE_SMOOTH);
                gl.Enable(OpenGL.GL_BLEND);
                gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
                gl.Hint(OpenGL.GL_LINE_SMOOTH_HINT, OpenGL.GL_NICEST);
                
                // 设置线宽
                gl.LineWidth(1.0f);
                
                Console.WriteLine("✅ SharpGL 初始化完成，准备渲染 DXF 圆形数据");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ SharpGL 初始化失败: {ex.Message}");
                Console.WriteLine($"❌ 堆栈跟踪: {ex.StackTrace}");
            }
        }
        
        private void OnOpenGLDraw(object sender, RenderEventArgs args)
        {
            try
            {
                var gl = _openGLControl.OpenGL;
                
                Console.WriteLine("正在绘制...");  // 确认绘制回调被触发
                
                // 清除颜色缓冲区（2D不需要深度缓冲）
                gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT);
                
                // 设置投影矩阵（根据模型边界自动调整）
                SetupProjection(gl);
                
                // 应用视图变换
                ApplyViewTransform(gl);
            
                // 测试绘制十字线（调试用）- 使用模型边界范围
                gl.Color(1.0f, 0.0f, 0.0f, 1.0f);  // 红色
                gl.Begin(OpenGL.GL_LINES);
                if (!_modelBounds.IsEmpty)
                {
                    float centerX = _modelBounds.Left + _modelBounds.Width / 2;
                    float centerY = _modelBounds.Bottom + _modelBounds.Height / 2;
                    float size = Math.Min(_modelBounds.Width, _modelBounds.Height) / 10;
                    
                    // 水平线
                    gl.Vertex(centerX - size, centerY, 0);
                    gl.Vertex(centerX + size, centerY, 0);
                    // 垂直线
                    gl.Vertex(centerX, centerY - size, 0);
                    gl.Vertex(centerX, centerY + size, 0);
                    
                    // Console.WriteLine($"🎨 绘制十字线: 中心=({centerX:F2}, {centerY:F2}), 尺寸={size:F2}");
                }
                else
                {
                    // 默认十字线
                    gl.Vertex(-1000, 0, 0);
                    gl.Vertex(1000, 0, 0);
                    gl.Vertex(0, -1000, 0);
                    gl.Vertex(0, 1000, 0);
                }
                gl.End();
                
                // 绘制网格
                if (_showGrid)
                    DrawGrid(gl);
                
                // 绘制坐标轴
                if (_showAxes)
                    DrawAxes(gl);
                
                // 绘制原始圆形
                DrawCircles(gl, _circles, _circleColor);
                
                // 绘制叠加圆形（处理后的圆形）
                DrawCircles(gl, _overlayCircles, _overlayCircleColor);
                
                // 绘制信息文本
                DrawInfoText(gl);
            }
            catch (Exception ex)
            {
                // Console.WriteLine($"❌ OpenGL绘制失败: {ex.Message}");
                // Console.WriteLine($"❌ 堆栈跟踪: {ex.StackTrace}");
            }
        }
        
        private void SetupProjection(OpenGL gl)
        {
            // 设置视口 - 这是关键！
            gl.Viewport(0, 0, _openGLControl.Width, _openGLControl.Height);
            
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.LoadIdentity();
            
            // 计算视口比例
            float aspect = (float)_openGLControl.Width / _openGLControl.Height;
            
            // Console.WriteLine($"🎨 SetupProjection: 视口尺寸={_openGLControl.Width}x{_openGLControl.Height}, 比例={aspect:F2}");
            
            if (!_modelBounds.IsEmpty)
            {
                // 基于模型边界设置正交投影
                float margin = Math.Max(_modelBounds.Width, _modelBounds.Height) * 0.1f;
                float left = _modelBounds.Left - margin;
                float right = _modelBounds.Right + margin;
                float bottom = _modelBounds.Bottom - margin;
                float top = _modelBounds.Top + margin;
                
                // 调整比例以保持纵横比
                float width = right - left;
                float height = top - bottom;
                
                if (width / height > aspect)
                {
                    float newHeight = width / aspect;
                    float diff = (newHeight - height) / 2;
                    bottom -= diff;
                    top += diff;
                }
                else
                {
                    float newWidth = height * aspect;
                    float diff = (newWidth - width) / 2;
                    left -= diff;
                    right += diff;
                }
                
                gl.Ortho(left, right, bottom, top, -1, 1);
                Console.WriteLine($"🎨 SetupProjection: 投影范围=({left:F2}, {right:F2}, {bottom:F2}, {top:F2})");
            }
            else
            {
                // 默认视图 - 强制更大的视口范围
                gl.Ortho(-50000, 50000, -50000, 50000, -1, 1);
                Console.WriteLine($"🎨 SetupProjection: 使用默认视图，范围=±50000 x ±50000");
            }
        }
        
        private void ApplyViewTransform(OpenGL gl)
        {
            gl.MatrixMode(OpenGL.GL_MODELVIEW);
            gl.LoadIdentity();
            
            // 应用平移和缩放
            gl.Translate(_panX, _panY, 0);
            gl.Scale(_scale, _scale, 1);
        }
        
        private void DrawGrid(OpenGL gl)
        {
            if (_modelBounds.IsEmpty) return;
            
            gl.Color(_gridColor.R / 255.0f, _gridColor.G / 255.0f, _gridColor.B / 255.0f, 0.5f);
            gl.LineWidth(0.5f);
            
            // 计算网格间距
            float gridSize = Math.Max(_modelBounds.Width, _modelBounds.Height) / 20;
            
            // 绘制垂直线
            gl.Begin(OpenGL.GL_LINES);
            for (float x = _modelBounds.Left; x <= _modelBounds.Right; x += gridSize)
            {
                gl.Vertex(x, _modelBounds.Bottom, 0);
                gl.Vertex(x, _modelBounds.Top, 0);
            }
            
            // 绘制水平线
            for (float y = _modelBounds.Bottom; y <= _modelBounds.Top; y += gridSize)
            {
                gl.Vertex(_modelBounds.Left, y, 0);
                gl.Vertex(_modelBounds.Right, y, 0);
            }
            gl.End();
            
            gl.LineWidth(1.0f);
        }
        
        private void DrawAxes(OpenGL gl)
        {
            if (_modelBounds.IsEmpty) return;
            
            gl.Color(_axesColor.R / 255.0f, _axesColor.G / 255.0f, _axesColor.B / 255.0f, 1.0f);
            gl.LineWidth(2.0f);
            
            gl.Begin(OpenGL.GL_LINES);
            
            // X 轴
            gl.Vertex(_modelBounds.Left, 0, 0);
            gl.Vertex(_modelBounds.Right, 0, 0);
            
            // Y 轴
            gl.Vertex(0, _modelBounds.Bottom, 0);
            gl.Vertex(0, _modelBounds.Top, 0);
            
            gl.End();
            
            gl.LineWidth(1.0f);
        }
        
        private void DrawCircles(OpenGL gl, List<CircleEntity> circles, Color color)
        {
            if (circles == null || circles.Count == 0) 
            {
                Console.WriteLine($"🎨 DrawCircles: 跳过绘制，圆形列表为空或null");
                return;
            }
            
            Console.WriteLine($"🎨 DrawCircles: 开始绘制 {circles.Count} 个圆形，颜色={color.Name}");
            
            gl.Color(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, 1.0f);
            
            // 绘制前几个圆形用于调试
            int debugCount = Math.Min(3, circles.Count);
            for (int i = 0; i < debugCount; i++)
            {
                var circle = circles[i];
                Console.WriteLine($"🎨 绘制圆形 {i+1}: 中心=({circle.Center.X:F2}, {circle.Center.Y:F2}), 半径={circle.Radius:F2}");
                DrawCircle(gl, (float)circle.Center.X, (float)circle.Center.Y, (float)circle.Radius);
            }
            
            // 绘制剩余圆形
            for (int i = debugCount; i < circles.Count; i++)
            {
                var circle = circles[i];
                DrawCircle(gl, (float)circle.Center.X, (float)circle.Center.Y, (float)circle.Radius);
            }
            
            Console.WriteLine($"🎨 DrawCircles: 完成绘制 {circles.Count} 个圆形");
        }
        
        private void DrawCircle(OpenGL gl, float centerX, float centerY, float radius)
        {
            const int segments = 32;
            const float angleStep = (float)(2 * Math.PI / segments);
            
            gl.Begin(OpenGL.GL_LINE_LOOP);
            
            for (int i = 0; i < segments; i++)
            {
                float angle = i * angleStep;
                float x = centerX + radius * (float)Math.Cos(angle);
                float y = centerY + radius * (float)Math.Sin(angle);
                gl.Vertex(x, y, 0);  // 明确指定 z=0
            }
            
            gl.End();
        }
        
        private void DrawInfoText(OpenGL gl)
        {
            // 在 OpenGL 中绘制文本比较复杂，这里先跳过
            // 可以考虑使用叠加的 GDI+ 文本或者 OpenGL 字体库
        }
        
        private void OnOpenGLResized(object sender, EventArgs e)
        {
            var gl = _openGLControl.OpenGL;
            gl.Viewport(0, 0, _openGLControl.Width, _openGLControl.Height);
            
            // 重新设置投影矩阵以适应新的视口尺寸
            SetupProjection(gl);
        }
        
        #region 鼠标交互
        
        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle || e.Button == MouseButtons.Left)
            {
                _isPanning = true;
                _lastMousePosition = e.Location;
                _openGLControl.Cursor = Cursors.Hand;
            }
        }
        
        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                // 计算鼠标移动距离
                int deltaX = e.X - _lastMousePosition.X;
                int deltaY = e.Y - _lastMousePosition.Y;
                
                // 转换为世界坐标的移动量
                float worldDeltaX = deltaX / _scale * 0.1f;
                float worldDeltaY = -deltaY / _scale * 0.1f; // Y轴翻转
                
                _panX += worldDeltaX;
                _panY += worldDeltaY;
                
                _lastMousePosition = e.Location;
                
                _openGLControl.DoRender();
                _openGLControl.DoRender();
            ViewChanged?.Invoke();
            }
        }
        
        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                _openGLControl.Cursor = Cursors.Default;
            }
        }
        
        private void OnMouseWheel(object sender, MouseEventArgs e)
        {
            // 缩放
            float scaleFactor = e.Delta > 0 ? 1.1f : 0.9f;
            _scale *= scaleFactor;
            
            // 限制缩放范围
            _scale = Math.Max(0.01f, Math.Min(100.0f, _scale));
            
            ViewChanged?.Invoke();
        }
        
        #endregion
        
        #region 公共方法
        
        /// <summary>
        /// 加载圆形数据
        /// </summary>
        public void LoadCircles(List<CircleEntity> circles)
        {
            _circles = circles?.ToList() ?? new List<CircleEntity>();
            
            // 计算模型边界
            CalculateModelBounds();
            
            // 重置视图
            ResetView();
            
            // 手动触发重绘
            _openGLControl.DoRender();
            _openGLControl.Invalidate();  // 额外的刷新调用
            
            Console.WriteLine($"✅ SharpGL 预览控件加载了 {_circles.Count} 个圆形");
            Console.WriteLine($"✅ 模型边界: {_modelBounds}");
            
            if (_circles.Count > 0)
            {
                var firstCircle = _circles[0];
                Console.WriteLine($"✅ 第一个圆形: 中心=({firstCircle.Center.X:F2}, {firstCircle.Center.Y:F2}), 半径={firstCircle.Radius:F2}");
            }
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
            _openGLControl.DoRender();
        }
        
        /// <summary>
        /// 重置视图到适合所有内容
        /// </summary>
        public void ResetView()
        {
            _scale = 1.0f;
            _panX = 0.0f;
            _panY = 0.0f;
            
            // 重新设置投影矩阵以适应模型边界
            var gl = _openGLControl.OpenGL;
            SetupProjection(gl);
            
            _openGLControl.DoRender();  // 触发重绘
            ViewChanged?.Invoke();
        }
        
        /// <summary>
        /// 清除所有内容
        /// </summary>
        public void Clear()
        {
            _circles.Clear();
            _overlayCircles.Clear();
            _modelBounds = RectangleF.Empty;
            ResetView();
            _openGLControl.DoRender();
        }
        
        private void CalculateModelBounds()
        {
            if (_circles.Count == 0)
            {
                _modelBounds = RectangleF.Empty;
                Console.WriteLine("🎨 CalculateModelBounds: 没有圆形，边界为空");
                return;
            }
            
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            
            foreach (var circle in _circles)
            {
                float x = (float)circle.Center.X;
                float y = (float)circle.Center.Y;
                float r = (float)circle.Radius;
                
                minX = Math.Min(minX, x - r);
                minY = Math.Min(minY, y - r);
                maxX = Math.Max(maxX, x + r);
                maxY = Math.Max(maxY, y + r);
                
                // Console.WriteLine($"🎨 圆形边界: 中心=({x:F2}, {y:F2}), 半径={r:F2}, 范围=[{x-r:F2}, {y-r:F2}] 到 [{x+r:F2}, {y+r:F2}]");
            }
            
            _modelBounds = new RectangleF(minX, minY, maxX - minX, maxY - minY);
            Console.WriteLine($"🎨 CalculateModelBounds: 最终边界=[{minX:F2}, {minY:F2}] 到 [{maxX:F2}, {maxY:F2}], 尺寸={maxX-minX:F2}x{maxY-minY:F2}");
        }
        
        #endregion
        
        #region 属性
        
        public bool ShowGrid
        {
            get => _showGrid;
            set
            {
                _showGrid = value;
                ViewChanged?.Invoke();
            }
        }
        
        public bool ShowAxes
        {
            get => _showAxes;
            set
            {
                _showAxes = value;
                ViewChanged?.Invoke();
            }
        }
        
        public Color CircleColor
        {
            get => _circleColor;
            set
            {
                _circleColor = value;
                ViewChanged?.Invoke();
            }
        }
        
        public Color OverlayCircleColor
        {
            get => _overlayCircleColor;
            set
            {
                _overlayCircleColor = value;
                ViewChanged?.Invoke();
            }
        }
        
        public int CircleCount => _circles.Count;
        public int OverlayCircleCount => _overlayCircles.Count;
        public RectangleF ModelBounds => _modelBounds;
        
        #endregion
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _openGLControl?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}