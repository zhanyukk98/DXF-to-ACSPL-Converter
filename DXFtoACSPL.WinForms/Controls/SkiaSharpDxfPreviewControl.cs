using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using DXFtoACSPL.Core.Models;

namespace DXFtoACSPL.WinForms.Controls;

/// <summary>
/// 基于 SkiaSharp 的 DXF 预览控件
/// 直接渲染 DxfFast 解析的圆形数据，完全独立于 CADLib
/// </summary>
public partial class SkiaSharpDxfPreviewControl : UserControl
{
    #region 字段和属性
    
    private SKControl _skiaControl;
    private List<CircleEntity> _circles = new List<CircleEntity>();
    private List<CircleEntity> _overlayCircles = new List<CircleEntity>();
    
    // 视图变换参数
    private float _scale = 1.0f;
    private float _panX = 0.0f;
    private float _panY = 0.0f;
    
    // 模型边界
    private RectangleF _modelBounds = RectangleF.Empty;
    
    // 鼠标交互
    private bool _mouseDown = false;
    private Point _lastMousePos;
    
    // 渲染参数
    private readonly SKPaint _circlePaint;
    private readonly SKPaint _overlayPaint;
    private readonly SKPaint _gridPaint;
    private readonly SKPaint _axisPaint;
    private readonly SKPaint _textPaint;
    
    #endregion
    
    #region 事件
    
    /// <summary>
    /// 圆形被选中事件
    /// </summary>
    public event Action<CircleEntity>? CircleSelected;
    
    #endregion
    
    #region 构造函数
    
    public SkiaSharpDxfPreviewControl()
    {
        InitializeComponent();
        
        // 初始化 SkiaSharp 控件
        _skiaControl = new SKControl
        {
            Dock = DockStyle.Fill
        };
        _skiaControl.PaintSurface += OnPaintSurface;
        _skiaControl.MouseDown += OnMouseDown;
        _skiaControl.MouseMove += OnMouseMove;
        _skiaControl.MouseUp += OnMouseUp;
        _skiaControl.MouseWheel += OnMouseWheel;
        _skiaControl.SizeChanged += OnSizeChanged;
        Controls.Add(_skiaControl);
        
        // 初始化画笔
        _circlePaint = new SKPaint
        {
            Color = SKColors.Red,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 5.0f,
            IsAntialias = true
        };
        
        _overlayPaint = new SKPaint
        {
            Color = SKColors.Orange,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3.0f,
            IsAntialias = true
        };
        
        _gridPaint = new SKPaint
        {
            Color = SKColors.LightGray,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.0f,
            IsAntialias = true
        };
        
        _axisPaint = new SKPaint
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2.0f,
            IsAntialias = true
        };
        
        _textPaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 12.0f,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Microsoft YaHei", SKFontStyle.Normal) ?? SKTypeface.FromFamilyName("SimSun", SKFontStyle.Normal) ?? SKTypeface.Default
        };
        
        Console.WriteLine("✅ SkiaSharp 预览控件初始化完成");
    }
    
    #endregion
    
    #region 渲染方法
    
    /// <summary>
    /// SkiaSharp 绘制事件处理
    /// </summary>
    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;
        
        Console.WriteLine($"🎨 SkiaSharp 开始渲染，圆形数量={_circles.Count}, 缩放={_scale:F2}, 平移=({_panX:F2}, {_panY:F2})");
        
        // 清除画布
        canvas.Clear(SKColors.White);
        
        // 保存画布状态
        canvas.Save();
        
        // 设置视图变换
        SetupTransform(canvas, info.Width, info.Height);
        
        // 绘制网格
        DrawGrid(canvas, info.Width, info.Height);
        
        // 绘制坐标轴
        DrawAxes(canvas);
        
        // 绘制原始圆形
        DrawCircles(canvas, _circles, _circlePaint);
        
        // 绘制叠加圆形
        DrawCircles(canvas, _overlayCircles, _overlayPaint);
        
        // 恢复画布状态
        canvas.Restore();
        
        // 绘制信息文本（不受变换影响）
        DrawInfoText(canvas, info.Width, info.Height);
    }
    
    /// <summary>
    /// 设置视图变换
    /// </summary>
    private void SetupTransform(SKCanvas canvas, int width, int height)
    {
        Console.WriteLine($"🔧 SetupTransform: 控件尺寸=({width}, {height}), 缩放={_scale:F2}, 平移=({_panX:F2}, {_panY:F2})");
        
        // 移动到画布中心
        canvas.Translate(width / 2.0f, height / 2.0f);
        
        // 翻转Y轴（CAD坐标系Y向上为正，屏幕坐标系Y向下为正）
        canvas.Scale(1.0f, -1.0f);
        
        // 应用缩放
        canvas.Scale(_scale, _scale);
        
        // 应用平移
        canvas.Translate(_panX, _panY);
        
        Console.WriteLine($"🔧 变换后的第一个圆形应该在: ({(_circles.Count > 0 ? (_circles[0].Center.X + _panX) * _scale : 0):F2}, {(_circles.Count > 0 ? (_circles[0].Center.Y + _panY) * _scale : 0):F2})");
    }
    
    /// <summary>
    /// 绘制网格
    /// </summary>
    private void DrawGrid(SKCanvas canvas, int width, int height)
    {
        if (_modelBounds.IsEmpty) return;
        
        float gridSize = 50.0f / _scale; // 根据缩放调整网格大小
        
        // 计算网格范围
        float left = _modelBounds.Left - 100;
        float right = _modelBounds.Right + 100;
        float top = _modelBounds.Top - 100;
        float bottom = _modelBounds.Bottom + 100;
        
        // 绘制垂直线
        for (float x = left; x <= right; x += gridSize)
        {
            canvas.DrawLine(x, top, x, bottom, _gridPaint);
        }
        
        // 绘制水平线
        for (float y = top; y <= bottom; y += gridSize)
        {
            canvas.DrawLine(left, y, right, y, _gridPaint);
        }
    }
    
    /// <summary>
    /// 绘制坐标轴
    /// </summary>
    private void DrawAxes(SKCanvas canvas)
    {
        if (_modelBounds.IsEmpty) return;
        
        // 计算图形内容的中心点
        float centerX = (_modelBounds.Left + _modelBounds.Right) / 2;
        float centerY = (_modelBounds.Top + _modelBounds.Bottom) / 2;
        
        // 固定轴线延伸长度，不依赖于模型边界大小
        // 使用视图空间的固定长度，根据缩放调整
        float extend = 1000.0f / _scale; // 在当前缩放级别下的固定长度
        
        // X轴（鲜红色）- 通过图形中心的水平线
        var xAxisPaint = new SKPaint
        {
            Color = new SKColor(255, 0, 0, 255), // 鲜红色，完全不透明
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2.0f / _scale, // 根据缩放调整线条粗细，保持视觉一致性
            IsAntialias = true
        };
        canvas.DrawLine(centerX - extend, centerY, centerX + extend, centerY, xAxisPaint);
        
        // Y轴（鲜绿色）- 通过图形中心的垂直线
        var yAxisPaint = new SKPaint
        {
            Color = new SKColor(0, 255, 0, 255), // 鲜绿色，完全不透明
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2.0f / _scale, // 根据缩放调整线条粗细，保持视觉一致性
            IsAntialias = true
        };
        canvas.DrawLine(centerX, centerY - extend, centerX, centerY + extend, yAxisPaint);
        
        xAxisPaint.Dispose();
        yAxisPaint.Dispose();
    }
    
    /// <summary>
    /// 获取圆形颜色 - 根据实体类型
    /// </summary>
    private SKColor GetCircleColor(string entityType)
    {
        if (string.IsNullOrEmpty(entityType))
            return SKColors.Gray;
            
        switch (entityType.ToUpper())
        {
            case "CIRCLE":
                return SKColors.Blue;
            case "FITTED_CIRCLE":
            case "FITTEDCIRCLE":
                return SKColors.Orange;
            case "ELLIPSE":
                return SKColors.Green;
            case "ARC":
                return SKColors.Purple;
            case "FITTED_POLYLINE":
            case "POLYLINE":
                return SKColors.Brown;
            case "LINE":
                return SKColors.DarkRed;
            case "POINT":
                return SKColors.Black;
            default:
                // 对于未知类型，使用不同颜色以便区分
                return new SKColor(180, 80, 120, 200);
        }
    }
    
    /// <summary>
    /// 绘制圆形集合 - 修复重叠问题：像素对齐 + 抗锯齿优化
    /// </summary>
    private void DrawCircles(SKCanvas canvas, List<CircleEntity> circles, SKPaint paint)
    {
        if (circles == null || circles.Count == 0)
        {
            Console.WriteLine($"🎨 SkiaSharp 跳过绘制，圆形列表为空或null");
            return;
        }
        
        Console.WriteLine($"🎨 SkiaSharp 绘制 {circles.Count} 个圆形 (像素对齐模式)");
        
        // 根据缩放级别调整线条粗细，使线条更细
        float adaptiveStrokeWidth = Math.Max(0.5f, 1.0f / _scale);
        
        Console.WriteLine($"🎨 自适应线条粗细: {adaptiveStrokeWidth:F2} (缩放={_scale:F2})");
        
        for (int i = 0; i < circles.Count; i++)
        {
            var circle = circles[i];
            
            // 像素对齐：将坐标四舍五入到像素中心，避免浮点累积误差
            float x = (float)Math.Round(circle.Center.X);
            float y = (float)Math.Round(circle.Center.Y);
            float radius = (float)circle.Radius; // 半径保持原值，不做像素对齐
            
            if (i < 3) // 只打印前3个圆形的详细信息
            {
                Console.WriteLine($"🎨 绘制圆形[{i}]: 原始=({circle.Center.X:F2}, {circle.Center.Y:F2}), 像素对齐=({x:F2}, {y:F2}), 半径={radius:F2}");
            }
            
            // 根据实体类型获取颜色
            var color = GetCircleColor(circle.EntityType);
            
            using (var entityPaint = new SKPaint
            {
                Color = color,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = adaptiveStrokeWidth,
                IsAntialias = true // 保持抗锯齿，但配合像素对齐减少重叠视觉效果
            })
            {
                canvas.DrawCircle(x, y, radius, entityPaint);
            }
        }
    }
    
    /// <summary>
    /// 绘制信息文本
    /// </summary>
    private void DrawInfoText(SKCanvas canvas, int width, int height)
    {
        string info = $"圆形数量: {_circles.Count} | 缩放: {_scale:F2}x | 平移: ({_panX:F1}, {_panY:F1})";
        
        // 绘制背景
        var textBounds = new SKRect();
        _textPaint.MeasureText(info, ref textBounds);
        
        var bgRect = new SKRect(5, 5, textBounds.Width + 15, textBounds.Height + 15);
        var bgPaint = new SKPaint { Color = SKColors.White.WithAlpha(200) };
        canvas.DrawRect(bgRect, bgPaint);
        
        // 绘制文本
        canvas.DrawText(info, 10, 20, _textPaint);
    }
    
    #endregion
    
    #region 鼠标交互
    
    private void OnMouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _mouseDown = true;
            _lastMousePos = e.Location;
            Console.WriteLine($"🖱️ 鼠标按下: {e.Location}");
        }
    }
    
    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_mouseDown)
        {
            float dx = (e.X - _lastMousePos.X) / _scale;
            float dy = -(e.Y - _lastMousePos.Y) / _scale; // 翻转Y轴
            
            _panX += dx;
            _panY += dy;
            
            _lastMousePos = e.Location;
            _skiaControl.Invalidate();
            
            Console.WriteLine($"🖱️ 平移: ({dx:F2}, {dy:F2}), 总平移: ({_panX:F2}, {_panY:F2})");
        }
    }
    
    private void OnMouseUp(object sender, MouseEventArgs e)
    {
        _mouseDown = false;
        Console.WriteLine($"🖱️ 鼠标释放: {e.Location}");
    }
    
    private void OnMouseWheel(object sender, MouseEventArgs e)
    {
        float scaleFactor = e.Delta > 0 ? 1.1f : 0.9f;
        _scale *= scaleFactor;
        
        // 限制缩放范围
        _scale = Math.Max(0.01f, Math.Min(10.0f, _scale));
        
        _skiaControl.Invalidate();
        
        Console.WriteLine($"🖱️ 缩放: {scaleFactor:F2}, 当前缩放: {_scale:F2}");
    }
    
    private void OnSizeChanged(object sender, EventArgs e)
    {
        Console.WriteLine($"📏 SkiaSharp 控件尺寸变化: ({_skiaControl.Width}, {_skiaControl.Height})");
        
        // 如果有圆形数据且控件尺寸有效，重新计算视图
        if (_circles.Count > 0 && _skiaControl.Width > 0 && _skiaControl.Height > 0)
        {
            ResetView();
        }
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
        
        Console.WriteLine($"✅ SkiaSharp 预览控件加载了 {_circles.Count} 个圆形");
        Console.WriteLine($"✅ 模型边界: {_modelBounds}");
        
        if (_circles.Count > 0)
        {
            var firstCircle = _circles[0];
            Console.WriteLine($"✅ 第一个圆形: 中心=({firstCircle.Center.X:F2}, {firstCircle.Center.Y:F2}), 半径={firstCircle.Radius:F2}");
        }
        
        _skiaControl.Invalidate();
    }
    
    /// <summary>
    /// 设置叠加圆形（处理后的圆形）
    /// </summary>
    public void SetOverlayCircles(List<CircleEntity> overlayCircles)
    {
        _overlayCircles = overlayCircles?.ToList() ?? new List<CircleEntity>();
        _skiaControl.Invalidate();
    }
    
    /// <summary>
    /// 重置视图到适合所有内容 - 修复缩放计算，与GDI+保持一致
    /// </summary>
    public void ResetView()
    {
        _scale = 1.0f;
        _panX = 0.0f;
        _panY = 0.0f;
        
        Console.WriteLine($"🔍 ResetView: 控件尺寸=({_skiaControl.Width}, {_skiaControl.Height}), 模型边界={_modelBounds}");
        
        if (!_modelBounds.IsEmpty && _skiaControl.Width > 0 && _skiaControl.Height > 0)
        {
            // 使用与GDI+相同的缩放计算方式，留出边距
            float scaleX = (_skiaControl.Width - 40) / _modelBounds.Width;
            float scaleY = (_skiaControl.Height - 40) / _modelBounds.Height;
            _scale = Math.Min(scaleX, scaleY);
            
            // 确保最小缩放比例，避免过度缩小
            if (_scale < 0.1f)
            {
                _scale = 0.1f;
                Console.WriteLine($"🔍 ResetView: 缩放比例过小，限制为0.1");
            }
            
            // 居中显示 - 移动模型中心到原点
            _panX = -(_modelBounds.Left + _modelBounds.Width / 2);
            _panY = -(_modelBounds.Top + _modelBounds.Height / 2);
            
            Console.WriteLine($"🔍 ResetView: 修复后缩放={_scale:F2}, 平移=({_panX:F2}, {_panY:F2})");
            Console.WriteLine($"🔍 模型尺寸: {_modelBounds.Width:F2} x {_modelBounds.Height:F2}");
        }
        else
        {
            Console.WriteLine($"🔍 ResetView: 跳过视图重置 - 模型边界空={_modelBounds.IsEmpty}, 控件尺寸=({_skiaControl.Width}, {_skiaControl.Height})");
        }
        
        _skiaControl.Invalidate();
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
        _skiaControl.Invalidate();
    }
    
    #endregion
    
    #region 私有方法
    
    /// <summary>
    /// 计算模型边界
    /// </summary>
    private void CalculateModelBounds()
    {
        if (_circles.Count == 0)
        {
            _modelBounds = RectangleF.Empty;
            return;
        }
        
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;
        
        foreach (var circle in _circles)
        {
            float left = (float)(circle.Center.X - circle.Radius);
            float right = (float)(circle.Center.X + circle.Radius);
            float top = (float)(circle.Center.Y - circle.Radius);
            float bottom = (float)(circle.Center.Y + circle.Radius);
            
            minX = Math.Min(minX, left);
            maxX = Math.Max(maxX, right);
            minY = Math.Min(minY, top);
            maxY = Math.Max(maxY, bottom);
        }
        
        _modelBounds = new RectangleF(minX, minY, maxX - minX, maxY - minY);
    }
    
    #endregion
    
    #region 资源释放
    
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _circlePaint?.Dispose();
            _overlayPaint?.Dispose();
            _gridPaint?.Dispose();
            _axisPaint?.Dispose();
            _textPaint?.Dispose();
        }
        base.Dispose(disposing);
    }
    
    #endregion
}