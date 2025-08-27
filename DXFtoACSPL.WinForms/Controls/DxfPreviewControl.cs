using DXFtoACSPL.Core.Models;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DXFtoACSPL.WinForms.Controls;

/// <summary>
/// DXF预览控件 - 基于原始项目的实现思路
/// 直接渲染DXF实体，支持缩放和拖拽
/// </summary>
public class DxfPreviewControl : UserControl
{
    private List<CircleEntity> _circles = new();
    private RectangleF _drawingBounds = RectangleF.Empty;
    
    // 视图参数
    private float _zoom = 1.0f;
    private PointF _viewOffset = PointF.Empty;
    private Point _lastMousePosition;
    private bool _isPanning = false;
    
    // 事件
    public event Action<CircleEntity>? CircleSelected;

    public DxfPreviewControl()
    {
        this.DoubleBuffered = true;
        this.SetStyle(ControlStyles.OptimizedDoubleBuffer | 
                     ControlStyles.AllPaintingInWmPaint | 
                     ControlStyles.UserPaint, true);
        this.ResizeRedraw = true;
        this.BackColor = Color.White;
        this.Cursor = Cursors.Cross;
        
        // 启用鼠标滚轮事件
        this.MouseWheel += OnMouseWheel;
    }

    /// <summary>
    /// 设置圆形实体数据
    /// </summary>
    public void SetCircles(List<CircleEntity> circles, RectangleF bounds)
    {
        _circles = circles ?? new List<CircleEntity>();
        _drawingBounds = bounds;
        
        Console.WriteLine($"DxfPreviewControl.SetCircles: 设置 {_circles.Count} 个圆形，边界: {bounds}");
        
        CalculateDrawingBounds();
        FitToView();
        Invalidate();
    }

    /// <summary>
    /// 重置视图
    /// </summary>
    public void ResetView()
    {
        _zoom = 1.0f;
        _viewOffset = PointF.Empty;
        FitToView();
        Invalidate();
    }

    /// <summary>
    /// 缩放到适合窗口
    /// </summary>
    public void FitToView()
    {
        if (_drawingBounds.IsEmpty || _circles.Count == 0)
        {
            _zoom = 1.0f;
            _viewOffset = PointF.Empty;
            return;
        }

        // 添加边距
        var margin = 50f;
        var displayBounds = new RectangleF(
            _drawingBounds.X - margin,
            _drawingBounds.Y - margin,
            _drawingBounds.Width + margin * 2,
            _drawingBounds.Height + margin * 2
        );

        // 计算缩放比例
        var scaleX = (Width - 20) / displayBounds.Width;
        var scaleY = (Height - 20) / displayBounds.Height;
        _zoom = Math.Min(scaleX, scaleY);

        // 计算偏移量，使图形居中
        var scaledWidth = displayBounds.Width * _zoom;
        var scaledHeight = displayBounds.Height * _zoom;
        _viewOffset = new PointF(
            (Width - scaledWidth) / 2 - displayBounds.X * _zoom,
            (Height - scaledHeight) / 2 - displayBounds.Y * _zoom
        );

        Console.WriteLine($"DxfPreviewControl.FitToView: 缩放={_zoom:F2}, 偏移=({_viewOffset.X:F2}, {_viewOffset.Y:F2})");
    }

    /// <summary>
    /// 计算图形边界
    /// </summary>
    private void CalculateDrawingBounds()
    {
        if (_circles == null || _circles.Count == 0)
        {
            _drawingBounds = RectangleF.Empty;
            return;
        }

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        foreach (var circle in _circles)
        {
            var center = circle.Center;
            var radius = circle.Radius;
            
            minX = Math.Min(minX, center.X - radius);
            minY = Math.Min(minY, center.Y - radius);
            maxX = Math.Max(maxX, center.X + radius);
            maxY = Math.Max(maxY, center.Y + radius);
        }

        _drawingBounds = new RectangleF(minX, minY, maxX - minX, maxY - minY);
        Console.WriteLine($"DxfPreviewControl.CalculateDrawingBounds: 边界={_drawingBounds}");
    }

    /// <summary>
    /// 坐标变换：屏幕坐标到世界坐标
    /// </summary>
    private PointF ScreenToWorld(Point screenPoint)
    {
        return new PointF(
            (screenPoint.X - _viewOffset.X) / _zoom,
            (screenPoint.Y - _viewOffset.Y) / _zoom
        );
    }

    /// <summary>
    /// 坐标变换：世界坐标到屏幕坐标
    /// </summary>
    private PointF WorldToScreen(PointF worldPoint)
    {
        return new PointF(
            worldPoint.X * _zoom + _viewOffset.X,
            worldPoint.Y * _zoom + _viewOffset.Y
        );
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        Console.WriteLine($"DxfPreviewControl.OnPaint: 绘制 {_circles.Count} 个圆形");

        if (_circles == null || _circles.Count == 0)
        {
            DrawPlaceholder(e.Graphics);
            return;
        }

        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // 绘制背景
        g.Clear(Color.White);

        // 绘制网格
        DrawGrid(g);

        // 绘制所有圆形实体
        DrawAllCircles(g);

        // 绘制边界框
        DrawBounds(g);

        // 绘制视图信息
        DrawViewInfo(g);
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
    /// 绘制网格
    /// </summary>
    private void DrawGrid(Graphics g)
    {
        if (_drawingBounds.IsEmpty) return;

        using var pen = new Pen(Color.LightGray, 1);
        pen.DashStyle = DashStyle.Dash;

        var gridSize = Math.Max(10f, 50f * _zoom);
        var startX = _viewOffset.X - (_viewOffset.X % gridSize);
        var startY = _viewOffset.Y - (_viewOffset.Y % gridSize);

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
    /// 绘制所有圆形实体
    /// </summary>
    private void DrawAllCircles(Graphics g)
    {
        foreach (var circle in _circles)
        {
            DrawCircle(g, circle);
        }
    }

    /// <summary>
    /// 绘制单个圆形实体
    /// </summary>
    private void DrawCircle(Graphics g, CircleEntity circle)
    {
        var center = WorldToScreen(circle.Center);
        var radius = circle.Radius * _zoom;

        // 绘制圆形
        using var pen = new Pen(GetCircleColor(circle), Math.Max(1, 2 * _zoom));
        g.DrawEllipse(pen, center.X - radius, center.Y - radius, radius * 2, radius * 2);

        // 绘制圆心
        using var centerBrush = new SolidBrush(Color.Red);
        var centerSize = Math.Max(2, 4 * _zoom);
        g.FillEllipse(centerBrush, center.X - centerSize, center.Y - centerSize, centerSize * 2, centerSize * 2);

        // 绘制索引（只在缩放级别足够时显示）
        if (_zoom > 0.5f)
        {
            using var textBrush = new SolidBrush(Color.Black);
            using var font = new Font("Arial", Math.Max(6, 8 * _zoom));
            var text = circle.Index.ToString();
            var textSize = g.MeasureString(text, font);
            g.DrawString(text, font, textBrush, center.X - textSize.Width / 2, center.Y - textSize.Height / 2);
        }
    }

    /// <summary>
    /// 绘制边界框
    /// </summary>
    private void DrawBounds(Graphics g)
    {
        if (_drawingBounds.IsEmpty) return;

        var screenBounds = new RectangleF(
            WorldToScreen(new PointF(_drawingBounds.X, _drawingBounds.Y)),
            new SizeF(_drawingBounds.Width * _zoom, _drawingBounds.Height * _zoom)
        );

        using var pen = new Pen(Color.Blue, Math.Max(1, 1 * _zoom));
        pen.DashStyle = DashStyle.Dash;
        g.DrawRectangle(pen, screenBounds.X, screenBounds.Y, screenBounds.Width, screenBounds.Height);
    }

    /// <summary>
    /// 绘制视图信息
    /// </summary>
    private void DrawViewInfo(Graphics g)
    {
        using var brush = new SolidBrush(Color.DarkGray);
        using var font = new Font("Arial", 8);
        var info = $"缩放: {_zoom:F2}x | 实体: {_circles.Count} | 按鼠标中键拖拽 | 滚轮缩放";
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
        FitToView();
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
            var worldPoint = ScreenToWorld(e.Location);
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
            _viewOffset.X += delta.X;
            _viewOffset.Y += delta.Y;
            _lastMousePosition = e.Location;
            Invalidate();
        }
    }

    private void OnMouseWheel(object? sender, MouseEventArgs e)
    {
        // 滚轮缩放
        var zoomFactor = e.Delta > 0 ? 1.1f : 0.9f;
        var oldZoom = _zoom;
        _zoom *= zoomFactor;
        
        // 限制缩放范围
        _zoom = Math.Max(0.1f, Math.Min(10.0f, _zoom));
        
        // 以鼠标位置为中心进行缩放
        var mousePos = e.Location;
        var worldPos = ScreenToWorld(mousePos);
        
        _viewOffset.X = mousePos.X - worldPos.X * _zoom;
        _viewOffset.Y = mousePos.Y - worldPos.Y * _zoom;
        
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
} 