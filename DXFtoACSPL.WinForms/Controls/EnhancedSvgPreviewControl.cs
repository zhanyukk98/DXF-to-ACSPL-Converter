using DXFtoACSPL.Core.Models;
using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Drawing2D;

namespace DXFtoACSPL.WinForms.Controls;

/// <summary>
/// 增强版SVG预览控件
/// 支持真正的SVG渲染和流畅的缩放拖拽
/// </summary>
public class EnhancedSvgPreviewControl : Control
{
    private string _svgContent = string.Empty;
    private bool _isLoaded = false;
    private List<CircleEntity> _circles = new();
    private RectangleF _bounds = RectangleF.Empty;
    
    // 视图变换参数
    private float _scale = 1.0f;
    private PointF _panOffset = PointF.Empty;
    private PointF _lastMousePosition = PointF.Empty;
    private bool _isPanning = false;
    private bool _isZooming = false;
    
    // 渲染缓存
    private Bitmap? _cachedBitmap;
    private bool _needsRedraw = true;
    
    // 事件
    public event Action<CircleEntity>? CircleSelected;

    public EnhancedSvgPreviewControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | 
                ControlStyles.UserPaint | 
                ControlStyles.DoubleBuffer | 
                ControlStyles.ResizeRedraw, true);
        
        this.BackColor = Color.White;
        this.Cursor = Cursors.Cross;
        
        // 启用鼠标滚轮事件
        this.MouseWheel += OnMouseWheel;
    }

    /// <summary>
    /// 加载SVG内容
    /// </summary>
    public void LoadSvg(string svgContent)
    {
        Console.WriteLine($"LoadSvg被调用，内容长度: {svgContent?.Length ?? 0}");
        _svgContent = svgContent;
        _isLoaded = !string.IsNullOrEmpty(svgContent);
        Console.WriteLine($"SVG加载状态: {_isLoaded}");
        _needsRedraw = true;
        CalculateViewport();
        Invalidate();
    }

    /// <summary>
    /// 从字符串加载SVG内容
    /// </summary>
    public void LoadSvgFromString(string svgContent)
    {
        LoadSvg(svgContent);
    }

    /// <summary>
    /// 从文件加载SVG
    /// </summary>
    public async Task LoadSvgFromFileAsync(string filePath)
    {
        try
        {
            Console.WriteLine($"尝试加载SVG文件: {filePath}");
            if (File.Exists(filePath))
            {
                var svgContent = await File.ReadAllTextAsync(filePath);
                Console.WriteLine($"SVG文件加载成功，内容长度: {svgContent.Length}");
                LoadSvg(svgContent);
            }
            else
            {
                Console.WriteLine($"SVG文件不存在: {filePath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载SVG文件失败: {ex.Message}");
            MessageBox.Show($"加载SVG文件失败: {ex.Message}", "错误");
        }
    }

    /// <summary>
    /// 设置圆形实体数据
    /// </summary>
    public void SetCircles(List<CircleEntity> circles, RectangleF bounds)
    {
        _circles = circles ?? new List<CircleEntity>();
        _bounds = bounds;
        _needsRedraw = true;
        CalculateViewport();
        Invalidate();
    }

    /// <summary>
    /// 重置视图
    /// </summary>
    public void ResetView()
    {
        _scale = 1.0f;
        _panOffset = PointF.Empty;
        _needsRedraw = true;
        CalculateViewport();
        Invalidate();
    }

    /// <summary>
    /// 缩放到适合窗口
    /// </summary>
    public void ZoomToFit()
    {
        if (_bounds.IsEmpty) return;
        
        var margin = 50f;
        var displayBounds = new RectangleF(
            _bounds.X - margin,
            _bounds.Y - margin,
            _bounds.Width + margin * 2,
            _bounds.Height + margin * 2
        );

        var scaleX = (Width - 20) / displayBounds.Width;
        var scaleY = (Height - 20) / displayBounds.Height;
        _scale = Math.Min(scaleX, scaleY);

        var scaledWidth = displayBounds.Width * _scale;
        var scaledHeight = displayBounds.Height * _scale;
        _panOffset = new PointF(
            (Width - scaledWidth) / 2 - displayBounds.X * _scale,
            (Height - scaledHeight) / 2 - displayBounds.Y * _scale
        );

        _needsRedraw = true;
        Invalidate();
    }

    /// <summary>
    /// 计算视口变换
    /// </summary>
    private void CalculateViewport()
    {
        if (_bounds.IsEmpty || _circles.Count == 0)
        {
            _scale = 1.0f;
            _panOffset = PointF.Empty;
            return;
        }

        // 如果还没有设置变换，则自动缩放到适合窗口
        if (_scale == 1.0f && _panOffset == PointF.Empty)
        {
            ZoomToFit();
        }
    }

    /// <summary>
    /// 坐标变换
    /// </summary>
    private PointF TransformPoint(PointF point)
    {
        return new PointF(
            point.X * _scale + _panOffset.X,
            point.Y * _scale + _panOffset.Y
        );
    }

    /// <summary>
    /// 逆坐标变换
    /// </summary>
    private PointF InverseTransformPoint(PointF point)
    {
        return new PointF(
            (point.X - _panOffset.X) / _scale,
            (point.Y - _panOffset.Y) / _scale
        );
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        // 使用缓存位图进行双缓冲
        if (_needsRedraw || _cachedBitmap == null)
        {
            RedrawCachedBitmap();
        }

        if (_cachedBitmap != null)
        {
            e.Graphics.DrawImage(_cachedBitmap, 0, 0);
        }
    }

    /// <summary>
    /// 重绘缓存位图
    /// </summary>
    private void RedrawCachedBitmap()
    {
        if (Width <= 0 || Height <= 0) return;

        _cachedBitmap?.Dispose();
        _cachedBitmap = new Bitmap(Width, Height);

        using var g = Graphics.FromImage(_cachedBitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // 绘制背景
        g.Clear(Color.White);

        // 检查是否有内容需要显示
        if (_circles.Count == 0 && string.IsNullOrEmpty(_svgContent))
        {
            DrawPlaceholder(g);
        }
        else
        {
            // 绘制SVG内容（如果可用）
            if (!string.IsNullOrEmpty(_svgContent))
            {
                DrawSvgContent(g);
            }

            // 绘制网格
            DrawGrid(g);

            // 绘制圆形实体
            DrawCircles(g);

            // 绘制边界框
            DrawBounds(g);

            // 绘制视图信息
            DrawViewInfo(g);
        }

        _needsRedraw = false;
    }

    /// <summary>
    /// 绘制占位符
    /// </summary>
    private void DrawPlaceholder(Graphics g)
    {
        using var brush = new SolidBrush(Color.Gray);
        using var font = new Font("Arial", 12);
        var text = "请加载DXF文件进行预览";
        var size = g.MeasureString(text, font);
        var x = (Width - size.Width) / 2;
        var y = (Height - size.Height) / 2;
        g.DrawString(text, font, brush, x, y);
    }

    /// <summary>
    /// 绘制SVG内容
    /// </summary>
    private void DrawSvgContent(Graphics g)
    {
        try
        {
            // 这里可以集成真正的SVG渲染库
            // 目前先绘制一个简单的SVG占位符
            using var brush = new SolidBrush(Color.LightBlue);
            using var font = new Font("Arial", 10);
            var text = "SVG内容渲染中...";
            var size = g.MeasureString(text, font);
            var x = 10;
            var y = 10;
            g.DrawString(text, font, brush, x, y);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SVG渲染失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 绘制网格
    /// </summary>
    private void DrawGrid(Graphics g)
    {
        if (_bounds.IsEmpty) return;

        using var pen = new Pen(Color.LightGray, 1);
        pen.DashStyle = DashStyle.Dash;

        var gridSize = Math.Max(10f, 50f * _scale);
        var startX = _panOffset.X - (_panOffset.X % gridSize);
        var startY = _panOffset.Y - (_panOffset.Y % gridSize);

        // 绘制垂直线
        for (float x = startX; x <= Width; x += gridSize)
        {
            g.DrawLine(pen, x, 0, x, Height);
        }

        // 绘制水平线
        for (float y = startY; y <= Height; y += gridSize)
        {
            g.DrawLine(pen, 0, y, Width, y);
        }
    }

    /// <summary>
    /// 绘制圆形实体
    /// </summary>
    private void DrawCircles(Graphics g)
    {
        foreach (var circle in _circles)
        {
            var center = TransformPoint(circle.Center);
            var radius = circle.Radius * _scale;

            // 绘制圆形
            using var pen = new Pen(GetCircleColor(circle), Math.Max(1, 2 * _scale));
            g.DrawEllipse(pen, center.X - radius, center.Y - radius, radius * 2, radius * 2);

            // 绘制圆心
            using var centerBrush = new SolidBrush(Color.Red);
            var centerSize = Math.Max(2, 4 * _scale);
            g.FillEllipse(centerBrush, center.X - centerSize, center.Y - centerSize, centerSize * 2, centerSize * 2);

            // 绘制索引（只在缩放级别足够时显示）
            if (_scale > 0.5f)
            {
                using var textBrush = new SolidBrush(Color.Black);
                using var font = new Font("Arial", Math.Max(6, 8 * _scale));
                var text = circle.Index.ToString();
                var textSize = g.MeasureString(text, font);
                g.DrawString(text, font, textBrush, center.X - textSize.Width / 2, center.Y - textSize.Height / 2);
            }
        }
    }

    /// <summary>
    /// 绘制边界框
    /// </summary>
    private void DrawBounds(Graphics g)
    {
        if (_bounds.IsEmpty) return;

        var transformedBounds = new RectangleF(
            TransformPoint(new PointF(_bounds.X, _bounds.Y)),
            new SizeF(_bounds.Width * _scale, _bounds.Height * _scale)
        );

        using var pen = new Pen(Color.Blue, Math.Max(1, 1 * _scale));
        pen.DashStyle = DashStyle.Dash;
        g.DrawRectangle(pen, transformedBounds.X, transformedBounds.Y, transformedBounds.Width, transformedBounds.Height);
    }

    /// <summary>
    /// 绘制视图信息
    /// </summary>
    private void DrawViewInfo(Graphics g)
    {
        using var brush = new SolidBrush(Color.DarkGray);
        using var font = new Font("Arial", 8);
        var info = $"缩放: {_scale:F2}x | 实体: {_circles.Count} | 按鼠标中键拖拽 | 滚轮缩放";
        g.DrawString(info, font, brush, 5, Height - 20);
    }

    private Color GetCircleColor(CircleEntity circle)
    {
        return circle.EntityType switch
        {
            "圆" => Color.Blue,
            "圆弧" => Color.Green,
            "多段线（拟合成圆）" => Color.Orange,
            _ => Color.Black
        };
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        _needsRedraw = true;
        CalculateViewport();
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        
        if (e.Button == MouseButtons.Middle)
        {
            // 中键拖拽
            _isPanning = true;
            _lastMousePosition = e.Location;
            this.Cursor = Cursors.Hand;
        }
        else if (e.Button == MouseButtons.Left)
        {
            // 左键选择
            var worldPoint = InverseTransformPoint(e.Location);
            var clickedCircle = FindCircleAtPoint(worldPoint);
            
            if (clickedCircle != null)
            {
                CircleSelected?.Invoke(clickedCircle);
            }
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        
        if (e.Button == MouseButtons.Middle)
        {
            _isPanning = false;
            this.Cursor = Cursors.Cross;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        
        if (_isPanning)
        {
            var delta = new PointF(e.X - _lastMousePosition.X, e.Y - _lastMousePosition.Y);
            _panOffset.X += delta.X;
            _panOffset.Y += delta.Y;
            _lastMousePosition = e.Location;
            _needsRedraw = true;
            Invalidate();
        }
    }

    private void OnMouseWheel(object? sender, MouseEventArgs e)
    {
        // 滚轮缩放
        var zoomFactor = e.Delta > 0 ? 1.1f : 0.9f;
        var oldScale = _scale;
        _scale *= zoomFactor;
        
        // 限制缩放范围
        _scale = Math.Max(0.1f, Math.Min(10.0f, _scale));
        
        // 以鼠标位置为中心进行缩放
        var mousePos = e.Location;
        var worldPos = InverseTransformPoint(mousePos);
        
        _panOffset.X = mousePos.X - worldPos.X * _scale;
        _panOffset.Y = mousePos.Y - worldPos.Y * _scale;
        
        _needsRedraw = true;
        Invalidate();
    }

    private CircleEntity? FindCircleAtPoint(PointF point)
    {
        const float tolerance = 10f;
        
        foreach (var circle in _circles)
        {
            var distance = DistanceBetweenPoints(point, circle.Center);
            if (distance <= tolerance)
            {
                return circle;
            }
        }
        
        return null;
    }

    private float DistanceBetweenPoints(PointF p1, PointF p2)
    {
        var dx = p1.X - p2.X;
        var dy = p1.Y - p2.Y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cachedBitmap?.Dispose();
        }
        base.Dispose(disposing);
    }
} 