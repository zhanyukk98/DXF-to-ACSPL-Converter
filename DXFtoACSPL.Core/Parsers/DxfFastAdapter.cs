using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DXFtoACSPL.Core.Interfaces;
using DXFtoACSPL.Core.Models;
using DxfFast.Interop;
using DxfFast.Interop.Types;

namespace DXFtoACSPL.Core.Parsers
{
    /// <summary>
    /// 基于 DxfFast.Interop 的适配器，实现 IDxfParser 接口
    /// </summary>
    public sealed class DxfFastAdapter : IDxfParser
    {
        private DxfFast.Interop.DxfParser? _native;
        private readonly List<object> _allEntities = new();
        private readonly object _lock = new();
        private string _filePath = string.Empty;
        private IReadOnlyList<NormalizedCircle> _normalizedCircles = Array.Empty<NormalizedCircle>();
        private RectangleF _modelBounds = RectangleF.Empty;
        private DxfFileInfo _fileInfo = new();

        public async Task<bool> LoadFileAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("文件路径不能为空", nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"DXF文件不存在: {filePath}", filePath);

            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    try
                    {
                        _native?.Dispose();
                        _native = new DxfFast.Interop.DxfParser(ParserConfiguration.HighPerformance);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ DxfParser 初始化失败: {ex.Message}");
                        throw new InvalidOperationException($"DxfParser 初始化失败: {ex.Message}", ex);
                    }

                    Console.WriteLine($"🔍 DxfFast 开始解析文件: {Path.GetFileName(filePath)}");
                    var start = DateTime.Now;
                    _native.ParseFile(filePath, normalize: true);

                    _filePath = filePath;
                    _normalizedCircles = _native.NormalizedCircles ?? Array.Empty<NormalizedCircle>();
                    _modelBounds = ComputeBounds(_normalizedCircles);

                    // 详细的调试输出
                    Console.WriteLine($"🔍 DxfFast 解析结果: 总实体={_native.EntityCount}, 归一化圆形={_normalizedCircles.Count}");
                    Console.WriteLine($"📊 归一化圆形数量: {_normalizedCircles.Count}");
                    
                    if (_normalizedCircles.Count > 0)
                    {
                        var minRadius = _normalizedCircles.Min(c => c.Radius);
                        var maxRadius = _normalizedCircles.Max(c => c.Radius);
                        var avgRadius = _normalizedCircles.Average(c => c.Radius);
                        Console.WriteLine($"📏 半径范围: 最小={minRadius:F2}, 最大={maxRadius:F2}, 平均={avgRadius:F2}");
                        
                        Console.WriteLine($"🎯 前5个圆形详情:");
                        for (int i = 0; i < Math.Min(5, _normalizedCircles.Count); i++)
                        {
                            var circle = _normalizedCircles[i];
                            Console.WriteLine($"  圆形{i+1}: 中心=({circle.Center.X:F2}, {circle.Center.Y:F2}), 半径={circle.Radius:F2}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ 警告: 没有解析到任何圆形实体!");
                    }

                    _fileInfo = new DxfFileInfo
                    {
                        FilePath = filePath,
                        FileSize = new FileInfo(filePath).Length,
                        TotalEntities = (int)_native.EntityCount,
                        CircleEntities = _normalizedCircles.Count,
                        ArcEntities = 0,
                        PolylineEntities = 0,
                        BlockReferences = 0,
                        LoadTime = DateTime.Now - start
                    };
                    
                    Console.WriteLine($"✅ DxfFast 解析完成，耗时: {_fileInfo.LoadTime.TotalMilliseconds:F0}ms");

                    _allEntities.Clear();
                    // 若未来扩展更多实体，可在此加入实体集合

                    return true;
                }
            });
        }

        public async Task<List<CircleEntity>> ParseCirclesAsync(ProcessingConfig config)
        {
            if (_native == null)
                throw new InvalidOperationException("请先加载DXF文件");

            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    Console.WriteLine("🚀 DxfFastAdapter: 开始使用 DxfFast.dll 解析圆形实体...");
                    var result = new List<CircleEntity>();
                    var uniqueCenters = new List<PointF>();
                    int index = 0;

                    Console.WriteLine($"🔍 开始处理 {_normalizedCircles.Count} 个归一化圆形，容差={config.CenterPointTolerance}");
                    
                    foreach (var nc in _normalizedCircles)
                    {
                        var center2d = new PointF((float)nc.Center.X, (float)nc.Center.Y);
                        var radius = (float)nc.Radius;

                        Console.WriteLine($"  处理圆形: 中心=({center2d.X:F3}, {center2d.Y:F3}), 半径={radius:F3}");

                        if (radius < config.MinRadius || radius > config.MaxRadius)
                        {
                            Console.WriteLine($"    ❌ 半径超出范围 [{config.MinRadius}, {config.MaxRadius}]，跳过");
                            continue;
                        }

                        bool isNewCenter = IsNewCenter(uniqueCenters, center2d, config.CenterPointTolerance);
                        if (!isNewCenter)
                        {
                            Console.WriteLine($"    ❌ 中心点重复（容差={config.CenterPointTolerance}），跳过");
                            continue;
                        }

                        uniqueCenters.Add(center2d);
                        Console.WriteLine($"    ✅ 添加圆形，当前总数={uniqueCenters.Count}");

                        var entityType = nc.Kind switch
                        {
                            CircleKind.Circle => "CIRCLE",
                            CircleKind.Ellipse => "ELLIPSE",
                            CircleKind.Polyline => "POLYLINE",
                            _ => "CIRCLE"
                        };

                        result.Add(new CircleEntity
                        {
                            Index = ++index,
                            EntityType = entityType,
                            Center = center2d,
                            Radius = radius,
                            BlockName = string.Empty,
                            InsertName = string.Empty
                        });
                    }

                    Console.WriteLine($"✅ DxfFastAdapter: 解析完成，共找到 {result.Count} 个圆形实体 (使用 DxfFast.dll)");
                    return result;
                }
            });
        }

        public DxfFileInfo GetFileInfo() => _fileInfo;

        public List<object> GetAllEntities() => _allEntities;

        public RectangleF GetModelBounds() => _modelBounds;

        public void Dispose()
        {
            lock (_lock)
            {
                _native?.Dispose();
                _native = null;
                _normalizedCircles = Array.Empty<NormalizedCircle>();
                _allEntities.Clear();
                _modelBounds = RectangleF.Empty;
            }
        }

        private static bool IsNewCenter(List<PointF> existingCenters, PointF newCenter, float tolerance)
        {
            for (int i = 0; i < existingCenters.Count; i++)
            {
                var c = existingCenters[i];
                if (Math.Abs(c.X - newCenter.X) < tolerance && Math.Abs(c.Y - newCenter.Y) < tolerance)
                    return false;
            }
            return true;
        }

        private static RectangleF ComputeBounds(IReadOnlyList<NormalizedCircle> circles)
        {
            if (circles == null || circles.Count == 0)
                return RectangleF.Empty;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (var c in circles)
            {
                var cx = (float)c.Center.X;
                var cy = (float)c.Center.Y;
                var r = (float)c.Radius;

                minX = Math.Min(minX, cx - r);
                minY = Math.Min(minY, cy - r);
                maxX = Math.Max(maxX, cx + r);
                maxY = Math.Max(maxY, cy + r);
            }

            return RectangleF.FromLTRB(minX, minY, maxX, maxY);
        }
    }
}