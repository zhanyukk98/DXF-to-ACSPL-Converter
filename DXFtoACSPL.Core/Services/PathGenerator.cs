using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using DXFtoACSPL.Core.Models;

namespace DXFtoACSPL.Core.Services;

/// <summary>
/// 路径生成器 - 完全移植自原始C#程序
/// </summary>
public class PathGenerator
{
    public class PathElement
    {
        public object Data { get; set; }
        public string Type { get; set; } // "Point" 或 "Marker"
        public int ClusterCount { get; set; } = 0; // 聚类数量（用于调试）
    }

    // 严格分组模式：每组只包含原始点，不插入任何中间点，分组之间插入Marker
    private List<PathElement> GenerateStrictMultiRegionPathWithMarkers(List<List<PointData>> orderedClusters, float rotationAngle)
    {
        List<PathElement> fullPath = new List<PathElement>();
        Console.WriteLine($"分组数量: {orderedClusters.Count}");
        // 日志输出（如果有LogMessage事件）
        try { System.Diagnostics.Debug.WriteLine($"分组数量: {orderedClusters.Count}"); } catch {}
        for (int i = 0; i < orderedClusters.Count; i++)
        {
            var cluster = orderedClusters[i];
            foreach (var pt in cluster)
            {
                // 应用旋转到路径点
                var rotatedPoint = RotatePoint(pt.ToPointF(), rotationAngle);
                fullPath.Add(new PathElement { Data = rotatedPoint, Type = "Point" });
            }
            if (i < orderedClusters.Count - 1)
            {
                fullPath.Add(new PathElement { Data = new PointF(-999999, -999999), Type = "Marker" });
            }
        }
        return fullPath;
    }

    /// <summary>
    /// 生成聚类路径 - 完全移植自原始C#程序
    /// </summary>
    public List<PointF> GeneratePathForCluster(
        List<PointData> points,
        float pathTolerance,
        float clusterTolerance,
        bool optimizeTravel = true)
    {
        // 过滤掉空点
        var validPoints = points.ToList();
        // 如果未指定聚类容差或点集较小，则使用单区域路径
        if (clusterTolerance <= 0 || validPoints.Count < 10)
        {
            return GenerateSingleRegionPath(validPoints, pathTolerance);
        }
        
        // 1. 空间聚类分区
        var clusters = ClusterPoints(validPoints, clusterTolerance);

        // 2. 分区排序（根据优化开关选择方法）
        List<List<PointData>> orderedClusters;
        if (optimizeTravel && clusters.Count > 1)
        {
            orderedClusters = OrderClustersByDistance(clusters);
        }
        else
        {
            orderedClusters = OrderClusters(clusters);
        }

        // 3. 生成完整路径
        return GenerateMultiRegionPath(orderedClusters, pathTolerance);
    }

    #region 空间聚类算法 - 完全移植自原始C#程序
    private List<List<PointData>> ClusterPoints(List<PointData> points, float tolerance)
    {
        var clusters = new List<List<PointData>>();
        var visited = new HashSet<PointData>();

        foreach (var point in points)
        {
            if (visited.Contains(point)) continue;

            var cluster = new List<PointData>();
            ExpandCluster(point, points, cluster, visited, tolerance);

            if (cluster.Count > 0)
                clusters.Add(cluster);
        }

        return clusters;
    }

    private void ExpandCluster(
        PointData seed,
        List<PointData> allPoints,
        List<PointData> cluster,
        HashSet<PointData> visited,
        float tolerance)
    {
        var queue = new Queue<PointData>();
        queue.Enqueue(seed);
        visited.Add(seed);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            cluster.Add(current);

            var neighbors = FindNeighbors(current, allPoints, tolerance, visited);
            foreach (var neighbor in neighbors)
            {
                visited.Add(neighbor);
                queue.Enqueue(neighbor);
            }
        }
    }

    private List<PointData> FindNeighbors(
        PointData point,
        List<PointData> allPoints,
        float tolerance,
        HashSet<PointData> visited)
    {
        return allPoints
            .Where(p => !visited.Contains(p))
            .Where(p => EuclideanDistance(p, point) <= tolerance)
            .ToList();
    }
    #endregion

    #region 分区排序 - 原始版本（完全移植自原始C#程序）
    private List<List<PointData>> OrderClusters(List<List<PointData>> clusters)
    {
        // 计算每个分区的边界框中心
        var clusterCenters = clusters.Select(cluster => {
            float minX = cluster.Min(p => p.X);
            float maxX = cluster.Max(p => p.X);
            float minY = cluster.Min(p => p.Y);
            float maxY = cluster.Max(p => p.Y);
            return new PointF((minX + maxX) / 2, (minY + maxY) / 2);
        }).ToList();

        // 按Y坐标主排序，X坐标次排序
        return clusters
            .Zip(clusterCenters, (c, center) => new { Cluster = c, Center = center })
            .OrderBy(x => x.Center.Y)
            .ThenBy(x => x.Center.X)
            .Select(x => x.Cluster)
            .ToList();
    }
    #endregion

    #region 分区排序 - 优化版本（完全移植自原始C#程序）
    private List<List<PointData>> OrderClustersByDistance(List<List<PointData>> clusters)
    {
        if (clusters.Count <= 1) return clusters;

        // 1. 计算每个分区的边界框中心
        var centroids = clusters.Select(cluster => {
            float minX = cluster.Min(p => p.X);
            float maxX = cluster.Max(p => p.X);
            float minY = cluster.Min(p => p.Y);
            float maxY = cluster.Max(p => p.Y);
            return new PointF((minX + maxX) / 2, (minY + maxY) / 2);
        }).ToList();

        // 2. 使用最近邻算法构建分区连接路径
        List<List<PointData>> orderedClusters = new List<List<PointData>>();
        List<PointF> remainingCentroids = new List<PointF>(centroids);
        List<List<PointData>> remainingClusters = new List<List<PointData>>(clusters);

        // 2.1 选择起始点（最左下角分区）
        int startIndex = FindLeftmostBottomCluster(centroids);
        orderedClusters.Add(clusters[startIndex]);
        remainingCentroids.RemoveAt(startIndex);
        remainingClusters.RemoveAt(startIndex);

        // 2.2 贪心算法连接最近邻分区
        PointF current = centroids[startIndex];
        while (remainingCentroids.Count > 0)
        {
            // 查找最近的未连接分区
            int nearestIndex = FindNearestClusterIndex(current, remainingCentroids);

            orderedClusters.Add(remainingClusters[nearestIndex]);
            current = remainingCentroids[nearestIndex];

            remainingCentroids.RemoveAt(nearestIndex);
            remainingClusters.RemoveAt(nearestIndex);
        }

        return orderedClusters;
    }

            // 查找最左下角的分区（完全移植自原始C#程序）
        private int FindLeftmostBottomCluster(List<PointF> centroids)
    {
            int index = 0;
            for (int i = 1; i < centroids.Count; i++)
            {
                if (centroids[i].Y < centroids[index].Y ||
                   (Math.Abs(centroids[i].Y - centroids[index].Y) < 0.001f &&
                    centroids[i].X < centroids[index].X))
                {
                    index = i;
                }
            }
            return index;
        }

        // 查找最近的未连接分区（完全移植自原始C#程序）
    private int FindNearestClusterIndex(PointF current, List<PointF> candidates)
    {
        int nearestIndex = 0;
        float minDistance = EuclideanDistance(current, candidates[0]);

        for (int i = 1; i < candidates.Count; i++)
        {
            float dist = EuclideanDistance(current, candidates[i]);
            if (dist < minDistance)
            {
                minDistance = dist;
                nearestIndex = i;
            }
        }
        return nearestIndex;
    }
    #endregion

    public List<PathElement> GenerateMarkedPath(
        List<PointData> points,
        float pathTolerance,
        float clusterTolerance,
            bool optimizeTravel = true)
    {
        var validPoints = points.Where(p => !p.IsEmpty).ToList();

        if (clusterTolerance <= 0 || validPoints.Count < 10)
        {
            return validPoints
                .Select(p => new PathElement { Data = p.ToPointF(), Type = "Point" })
                .ToList();
        }

        var clusters = ClusterPoints(validPoints, clusterTolerance);
        List<List<PointData>> orderedClusters = optimizeTravel && clusters.Count > 1
                ? OrderClustersByDistance(clusters)
            : OrderClusters(clusters);

            return GenerateMultiRegionPathWithMarkers(orderedClusters, pathTolerance);
    }

    /// <summary>
    /// 生成带标记的多区域路径（用于ACSPL代码生成）
    /// 返回List<PathElement>，组内点Type=Point，组间Type=Marker
    /// </summary>
    private List<PathElement> GenerateMultiRegionPathWithMarkers(
        List<List<PointData>> orderedClusters,
        float pathTolerance)
    {
        List<PathElement> fullPath = new List<PathElement>();
        PointF? lastPoint = null;

        for (int i = 0; i < orderedClusters.Count; i++)
        {
            var cluster = orderedClusters[i];
            var clusterPath = GenerateSingleRegionPath(cluster, pathTolerance);
            if (clusterPath.Count == 0) continue;

            if (lastPoint.HasValue)
            {
                PointF entryPoint = FindNearestEntryPoint(lastPoint.Value, clusterPath);
                if (!PointEquals(clusterPath[0], entryPoint))
                {
                    clusterPath = OptimizePathDirection(clusterPath, entryPoint);
                }
            }

            foreach (var point in clusterPath)
            {
                fullPath.Add(new PathElement { Data = point, Type = "Point" });
            }

            if (i < orderedClusters.Count - 1)
            {
                fullPath.Add(new PathElement { Data = "MARKER", Type = "Marker" });
            }

            lastPoint = clusterPath.Last();
        }
        return fullPath;
    }

    #region 路径生成核心
    private List<PointF> GenerateMultiRegionPath(
        List<List<PointData>> orderedClusters,
        float pathTolerance)
    {
        List<PointF> fullPath = new List<PointF>();
        PointF? lastPoint = null;

        for (int i = 0; i < orderedClusters.Count; i++)
        {
            var cluster = orderedClusters[i];
            var clusterPath = GenerateSingleRegionPath(cluster, pathTolerance);
            if (clusterPath.Count == 0) continue;

            // 智能选择连接点（仅当不是第一个分区）
            if (lastPoint.HasValue)
            {
                // 1. 计算当前分区最近的入口点
                PointF entryPoint = FindNearestEntryPoint(lastPoint.Value, clusterPath);

                // 2. 调整分区路径方向（使入口点为起点）
                if (!PointEquals(clusterPath[0], entryPoint))
                {
                    clusterPath = OptimizePathDirection(clusterPath, entryPoint);
                }

                // 3. 移除直接添加entryPoint的代码（避免重复）
                // --- 删除此行 --- fullPath.Add(entryPoint);
            }

            fullPath.AddRange(clusterPath); // 入口点已作为clusterPath起点包含
            lastPoint = clusterPath.Last();
        }
        return fullPath;
    }

    // 查找距离上一点最近的分区入口点（完全移植自原始C#程序）
    private PointF FindNearestEntryPoint(PointF fromPoint, List<PointF> clusterPath)
    {
        // 考虑分区路径的首尾点
        float distToStart = EuclideanDistance(fromPoint, clusterPath[0]);
        float distToEnd = EuclideanDistance(fromPoint, clusterPath[^1]);

        return distToStart <= distToEnd ? clusterPath[0] : clusterPath[^1];
    }

    // 优化路径方向（使指定点作为起点）（完全移植自原始C#程序）
    private List<PointF> OptimizePathDirection(List<PointF> path, PointF startPoint)
    {
        if (path.Count == 0) return path;

        // 如果起点是当前终点，则反转整个路径
        if (PointEquals(path[^1], startPoint))
        {
            path.Reverse();
            return path;
        }

        // 如果起点在路径中间（理论上不应发生）
        if (!PointEquals(path[0], startPoint))
        {
            // 查找起点在路径中的位置
            int index = path.FindIndex(p => PointEquals(p, startPoint));
            if (index > 0)
            {
                // 重新排序：startPoint → 终点
                var newPath = path.GetRange(index, path.Count - index);
                newPath.AddRange(path.GetRange(0, index).AsEnumerable().Reverse());
                return newPath;
            }
        }

        return path;
    }

    // 浮点数比较辅助方法（完全移植自原始C#程序）
    private bool PointEquals(PointF a, PointF b, float tolerance = 0.001f)
    {
        return Math.Abs(a.X - b.X) < tolerance && Math.Abs(a.Y - b.Y) < tolerance;
    }

    /// <summary>
    /// 验证坐标点的有效性
    /// </summary>
    private bool IsValidCoordinate(PointF point)
    {
        return !float.IsNaN(point.X) && !float.IsNaN(point.Y) && 
               !float.IsInfinity(point.X) && !float.IsInfinity(point.Y);
    }

    /// <summary>
    /// 验证并修复坐标点
    /// </summary>
    private PointF ValidateAndFixCoordinate(PointF point, PointF fallbackPoint)
    {
        if (IsValidCoordinate(point))
        {
            return point;
        }
        
        Console.WriteLine($"警告：坐标点无效 ({point.X}, {point.Y})，使用备选坐标 ({fallbackPoint.X}, {fallbackPoint.Y})");
        return fallbackPoint;
    }
    #endregion

    #region 单区域路径生成
    private List<PointF> GenerateSingleRegionPath(List<PointData> points, float tolerance)
    {
        var pointFs = points.Select(p => p.ToPointF()).ToList();
        return GenerateSnakePath(pointFs, tolerance);
    }

    /// <summary>
    /// 生成严格模式路径 - 只包含原始圆孔点，不添加任何经过点
    /// </summary>
    private List<PathElement> GenerateStrictPath(List<PointData> points, float tolerance, float rotationAngle)
    {
        var pointFs = points.Select(p => p.ToPointF()).ToList();
        var strictPath = GenerateStrictSnakePath(pointFs, tolerance);
        
        // 应用旋转到所有路径点
        var rotatedPath = strictPath.Select(p => RotatePoint(p, rotationAngle)).ToList();
        
        return rotatedPath
            .Select(p => new PathElement { Data = p, Type = "Point" })
            .ToList();
    }

    /// <summary>
    /// 严格蛇形路径 - 只包含原始点，不添加连接点
    /// </summary>
    private List<PointF> GenerateStrictSnakePath(List<PointF> cluster, float tolerance)
    {
        if (cluster.Count == 0)
            return new List<PointF>();

        // 按Y容差分组行
        var rows = cluster
            .GroupBy(p => (int)Math.Round(p.Y / tolerance))
            .OrderBy(g => g.Key)
            .ToList();

        List<PointF> path = new List<PointF>();
        bool reverse = false;

        for (int i = 0; i < rows.Count; i++)
        {
            var rowPoints = rows[i].ToList();

            // 蛇形转向逻辑
            if (reverse)
            {
                rowPoints = rowPoints.OrderByDescending(p => p.X).ToList();
            }
            else
            {
                rowPoints = rowPoints.OrderBy(p => p.X).ToList();
            }

            // 只添加当前行的原始点，不添加任何连接点
            path.AddRange(rowPoints);
            reverse = !reverse; // 切换方向
        }

        return path;
    }

    private List<PointF> GenerateSnakePath(List<PointF> cluster, float tolerance)
    {
        if (cluster.Count == 0)
            return new List<PointF>();

        // 按Y容差分组行
        var rows = cluster
            .GroupBy(p => (int)Math.Round(p.Y / tolerance))
            .OrderBy(g => g.Key)
            .ToList();

        List<PointF> path = new List<PointF>();
        bool reverse = false;

        for (int i = 0; i < rows.Count; i++)
        {
            var rowPoints = rows[i].ToList();

            // 蛇形转向逻辑
            if (reverse)
            {
                rowPoints = rowPoints.OrderByDescending(p => p.X).ToList();
            }
            else
            {
                rowPoints = rowPoints.OrderBy(p => p.X).ToList();
            }

            // 添加当前行点
            path.AddRange(rowPoints);
            reverse = !reverse; // 切换方向

            // 添加行间连接点（最后一行除外）
            if (i < rows.Count - 1)
            {
                // 获取下一行的第一个点（根据蛇形方向）
                PointF nextFirstPoint = reverse ?
                    rows[i + 1].OrderByDescending(p => p.X).First() :
                    rows[i + 1].OrderBy(p => p.X).First();

                // 直接从当前行最后一个点移动到下一行第一个点（斜线移动）
                path.Add(nextFirstPoint);
            }
        }

        return path;
    }
    #endregion

    #region 几何计算
    private float EuclideanDistance(PointData a, PointData b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    private float EuclideanDistance(PointF a, PointF b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// 计算平均点间距离
    /// </summary>
    private float CalculateAverageDistance(List<PointF> points)
    {
        if (points.Count < 2) return 1.0f;
        
        float totalDistance = 0;
        int sampleSize = Math.Min(500, points.Count);
        int comparisons = 0;
        
        for (int i = 0; i < sampleSize; i++)
        {
            var point = points[i];
            float minDist = float.MaxValue;
            
            // 只与附近的点比较，避免O(n²)
            for (int j = 0; j < Math.Min(20, points.Count); j++)
            {
                if (i == j) continue;
                var dist = EuclideanDistance(point, points[j]);
                if (dist < minDist) minDist = dist;
            }
            
            totalDistance += minDist;
            comparisons++;
        }
        
        return comparisons > 0 ? totalDistance / comparisons : 1.0f;
    }
    #endregion

    // 工具方法：应用旋转
    private static PointF RotatePoint(PointF pt, float angleDeg)
    {
        double angleRad = angleDeg * Math.PI / 180.0;
        float x = (float)(pt.X * Math.Cos(angleRad) - pt.Y * Math.Sin(angleRad));
        float y = (float)(pt.X * Math.Sin(angleRad) + pt.Y * Math.Cos(angleRad));
        return new PointF(x, y);
    }

    /// <summary>
    /// 根据配置生成路径（支持多种算法）
    /// </summary>
    /// <param name="points">输入点数据</param>
    /// <param name="config">处理配置</param>
    /// <returns>路径元素列表</returns>
    public List<PathElement> GeneratePathWithAlgorithm(List<PointData> points, ProcessingConfig config)
    {
        switch (config.PathAlgorithm)
        {
            case PathGenerationAlgorithm.SpiralFill:
                return GenerateSpiralFillPath(points, config);
            case PathGenerationAlgorithm.SnakePath:
                return GenerateSnakePath(points, config);
            case PathGenerationAlgorithm.NearestNeighbor:
                return GenerateNearestNeighborPath(points, config);
            case PathGenerationAlgorithm.TestAlgorithm:
                return GenerateTestAlgorithmPath(points, config);
            case PathGenerationAlgorithm.EnhancedCluster:
                return GenerateEnhancedClusterPath(points, config);
            case PathGenerationAlgorithm.Cluster:
            default:
                return GenerateClusterPath(points, config);
        }
    }

    /// <summary>
    /// 生成路径并返回调试信息
    /// </summary>
    public (List<PathElement> path, string debugInfo) GeneratePathWithAlgorithmAndDebug(List<PointData> points, ProcessingConfig config)
    {
        var debugLog = new List<string>();
        debugLog.Add($"算法 = {config.PathAlgorithm}");
        debugLog.Add($"输入点数 = {points.Count}");
        
        List<PathElement> result;
        
        switch (config.PathAlgorithm)
        {
            case PathGenerationAlgorithm.TestAlgorithm:
                result = GenerateTestAlgorithmPath(points, config, debugLog);
                break;
            case PathGenerationAlgorithm.SpiralFill:
                result = GenerateSpiralFillPath(points, config);
                debugLog.Add($"螺旋填充算法完成，生成 {result.Count} 个路径点");
                break;
            case PathGenerationAlgorithm.SnakePath:
                result = GenerateSnakePath(points, config);
                debugLog.Add($"蛇形路径算法完成，生成 {result.Count} 个路径点");
                break;
            case PathGenerationAlgorithm.NearestNeighbor:
                result = GenerateNearestNeighborPath(points, config);
                debugLog.Add($"最近邻算法完成，生成 {result.Count} 个路径点");
                break;
            case PathGenerationAlgorithm.EnhancedCluster:
                result = GenerateEnhancedClusterPath(points, config, debugLog);
                break;
            case PathGenerationAlgorithm.Cluster:
            default:
                result = GenerateClusterPath(points, config);
                debugLog.Add($"聚类算法完成，生成 {result.Count} 个路径点");
                break;
        }
        
        debugLog.Add($"路径生成完成，总路径点数 = {result.Count}");
        
        return (result, string.Join("\n", debugLog));
    }

    /// <summary>
    /// 生成阿基米德螺旋填充路径
    /// </summary>
    private List<PathElement> GenerateSpiralFillPath(List<PointData> points, ProcessingConfig config)
    {
        try
        {
            Console.WriteLine($"开始生成阿基米德螺旋路径，共 {points.Count} 个点");

            // 1. 计算螺旋中心点
            PointF spiralCenter = CalculateSpiralCenter(points, config);
            Console.WriteLine($"螺旋中心点: ({spiralCenter.X:F2}, {spiralCenter.Y:F2})");

            // 2. 将所有点转换为极坐标
            var polarPoints = ConvertToPolarCoordinates(points, spiralCenter);
            Console.WriteLine($"转换为极坐标完成，共 {polarPoints.Count} 个点");

            // 3. 生成阿基米德螺旋路径
            var spiralPath = GenerateArchimedesSpiral(polarPoints, config);
            Console.WriteLine($"阿基米德螺旋路径生成完成: {spiralPath.Count} 个路径点");

            // 4. 应用旋转
            if (Math.Abs(config.RotationAngle) > 0.001f)
            {
                for (int i = 0; i < spiralPath.Count; i++)
                {
                    if (spiralPath[i].Type == "Point" && spiralPath[i].Data is PointF point)
                    {
                        spiralPath[i].Data = RotatePoint(point, config.RotationAngle);
                    }
                }
            }

            return spiralPath;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"阿基米德螺旋路径生成失败，回退到聚类算法: {ex.Message}");
            return GenerateClusterPath(points, config);
        }
    }

    /// <summary>
    /// 计算螺旋中心点
    /// </summary>
    private PointF CalculateSpiralCenter(List<PointData> points, ProcessingConfig config)
    {
        // 如果配置中指定了中心点，使用配置的中心点
        if (config.SpiralCenterX.HasValue && config.SpiralCenterY.HasValue)
        {
            return new PointF(config.SpiralCenterX.Value, config.SpiralCenterY.Value);
        }

        // 否则自动计算所有点的中心
        float centerX = points.Average(p => p.X);
        float centerY = points.Average(p => p.Y);
        return new PointF(centerX, centerY);
    }

    /// <summary>
    /// 将笛卡尔坐标转换为极坐标
    /// </summary>
    private List<(PointData originalPoint, float radius, float angle)> ConvertToPolarCoordinates(List<PointData> points, PointF center)
    {
        var polarPoints = new List<(PointData, float, float)>();

        foreach (var point in points)
        {
            // 计算相对于中心的偏移
            float dx = point.X - center.X;
            float dy = point.Y - center.Y;

            // 计算半径
            float radius = (float)Math.Sqrt(dx * dx + dy * dy);

            // 计算角度（弧度）
            float angle = (float)Math.Atan2(dy, dx);

            // 确保角度在 [0, 2π) 范围内
            if (angle < 0)
                angle += 2 * (float)Math.PI;

            polarPoints.Add((point, radius, angle));
        }

        return polarPoints;
    }

    /// <summary>
    /// 生成阿基米德螺旋路径 - 优化版本
    /// </summary>
    private List<PathElement> GenerateArchimedesSpiral(List<(PointData originalPoint, float radius, float angle)> polarPoints, ProcessingConfig config)
    {
        var spiralPath = new List<PathElement>();

        // 按角度排序，确保螺旋路径的连续性
        var sortedPoints = polarPoints.OrderBy(p => p.angle).ToList();

        // 阿基米德螺旋参数
        float dr = config.SpiralRadiusIncrement;           // 半径增量
        float dtheta = config.SpiralAngleStep;            // 角度步长
        float r0 = config.SpiralStartRadius;              // 起始半径

        // 计算最大半径，避免无限循环
        float maxRadius = sortedPoints.Max(p => p.radius);
        float maxSpiralRadius = maxRadius + dr;

        // 预计算螺旋路径点，避免重复计算
        var spiralPoints = new List<(float radius, float angle)>();
        float currentAngle = 0;
        float currentRadius = r0;

        // 生成螺旋路径点（限制最大角度避免无限循环）
        const float MAX_ANGLE = 100 * 2 * (float)Math.PI; // 最多100圈
        while (currentAngle < MAX_ANGLE && currentRadius <= maxSpiralRadius)
        {
            // 阿基米德螺旋方程：r = r0 + dr * (θ / 2π)
            float spiralRadius = r0 + dr * (currentAngle / (2 * (float)Math.PI));
            
            spiralPoints.Add((spiralRadius, currentAngle));
            
            // 更新角度和半径
            currentAngle += dtheta;
            currentRadius = spiralRadius;
        }

        Console.WriteLine($"生成螺旋路径点: {spiralPoints.Count} 个点");

        // 为每个螺旋点找到最近的原始点
        var usedPoints = new HashSet<PointData>();
        foreach (var (spiralRadius, spiralAngle) in spiralPoints)
        {
            // 找到最近的未使用的原始点
            var nearestPoint = FindNearestUnusedPoint(sortedPoints, spiralRadius, spiralAngle, usedPoints);
            if (nearestPoint != null)
            {
                spiralPath.Add(new PathElement 
                { 
                    Data = nearestPoint.ToPointF(), 
                    Type = "Point" 
                });
                usedPoints.Add(nearestPoint);
            }
        }

        Console.WriteLine($"阿基米德螺旋路径生成完成: {spiralPath.Count} 个路径点");
        return spiralPath;
    }

    /// <summary>
    /// 找到最近的未使用的原始点
    /// </summary>
    private PointData? FindNearestUnusedPoint(List<(PointData originalPoint, float radius, float angle)> polarPoints, float spiralRadius, float spiralAngle, HashSet<PointData> usedPoints)
    {
        PointData? nearestPoint = null;
        float minDistance = float.MaxValue;

        foreach (var (originalPoint, radius, angle) in polarPoints)
        {
            // 跳过已使用的点
            if (usedPoints.Contains(originalPoint))
                continue;

            // 计算螺旋路径上的点与原始点的距离
            float distance = CalculateSpiralDistance(spiralRadius, spiralAngle, radius, angle);
            
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestPoint = originalPoint;
            }
        }

        return nearestPoint;
    }

    // 删除旧的FindNearestPointInSpiral方法，已被FindNearestUnusedPoint替代

    /// <summary>
    /// 计算螺旋路径上两点之间的距离
    /// </summary>
    private float CalculateSpiralDistance(float r1, float theta1, float r2, float theta2)
    {
        // 使用极坐标距离公式
        float dr = r2 - r1;
        float dtheta = theta2 - theta1;
        
        // 确保角度差在合理范围内
        while (dtheta > Math.PI) dtheta -= 2 * (float)Math.PI;
        while (dtheta < -Math.PI) dtheta += 2 * (float)Math.PI;

        return (float)Math.Sqrt(dr * dr + r1 * r1 * dtheta * dtheta);
    }

    /// <summary>
    /// 生成蛇形路径
    /// </summary>
    private List<PathElement> GenerateSnakePath(List<PointData> points, ProcessingConfig config)
    {
        var pathElements = new List<PathElement>();
        
        // 将PointData转换为PointF
        var pointList = points.Select(p => p.ToPointF()).ToList();
        
        // 应用旋转
        if (Math.Abs(config.RotationAngle) > 0.001f)
        {
            pointList = pointList.Select(p => RotatePoint(p, config.RotationAngle)).ToList();
        }
        
        // 生成蛇形路径
        var snakePath = GenerateSnakePath(pointList, config.PathTolerance1);
        
        // 转换为PathElement
        foreach (var point in snakePath)
        {
            pathElements.Add(new PathElement { Type = "Point", Data = point });
        }
        
        Console.WriteLine($"蛇形路径生成完成: {pathElements.Count} 个路径点");
        return pathElements;
    }

    /// <summary>
    /// 生成最近邻路径
    /// </summary>
    private List<PathElement> GenerateNearestNeighborPath(List<PointData> points, ProcessingConfig config)
    {
        var pathElements = new List<PathElement>();
        
        // 将PointData转换为PointF
        var pointList = points.Select(p => p.ToPointF()).ToList();
        
        // 应用旋转
        if (Math.Abs(config.RotationAngle) > 0.001f)
        {
            pointList = pointList.Select(p => RotatePoint(p, config.RotationAngle)).ToList();
        }
        
        // 最近邻算法
        var visited = new HashSet<PointF>();
        var current = pointList[0];
        visited.Add(current);
        pathElements.Add(new PathElement { Type = "Point", Data = current });
        
        while (visited.Count < pointList.Count)
        {
            PointF nearest = current;
            float minDistance = float.MaxValue;
            
            foreach (var point in pointList)
            {
                if (!visited.Contains(point))
                {
                    float distance = EuclideanDistance(current, point);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearest = point;
                    }
                }
            }
            
            current = nearest;
            visited.Add(current);
            pathElements.Add(new PathElement { Type = "Point", Data = current });
        }
        
        Console.WriteLine($"最近邻路径生成完成: {pathElements.Count} 个路径点");
        return pathElements;
    }

    /// <summary>
    /// 生成聚类路径（原始算法）
    /// </summary>
    private List<PathElement> GenerateClusterPath(List<PointData> points, ProcessingConfig config)
    {
        // 1. 先应用旋转到输入点
        var rotatedPoints = points.Select(p => {
            var rotatedPointF = RotatePoint(p.ToPointF(), config.RotationAngle);
            return new PointData(rotatedPointF);
        }).ToList();
        
        // 2. 应用与原始C#程序相同的坐标预处理（平移到坐标系中心）
        var preprocessedPoints = PreprocessCoordinates(rotatedPoints, config.Scale);
        
        // 3. 调整路径容差以适应坐标预处理
        // 由于坐标被缩放了scale倍，路径容差也需要相应调整
        float adjustedPathTolerance = config.PathTolerance1 * config.Scale;
        float adjustedClusterTolerance = config.PathTolerance2 * config.Scale;
        
        // 4. 使用原始C#程序的聚类算法
        var result = GeneratePathForCluster(
            preprocessedPoints,
            adjustedPathTolerance,  // 调整后的路径容差
            adjustedClusterTolerance,  // 调整后的聚类容差
            true // optimizeTravel
        );
        
        // 5. 转换为PathElement格式，并添加聚类信息
        var pathElements = result.Select(p => new PathElement { Data = p, Type = "Point" }).ToList();
        
        // 6. 添加聚类信息到第一个元素（用于调试）
        if (pathElements.Count > 0)
        {
            // 计算实际的聚类数量
            int actualClusterCount = CalculateActualClusterCount(preprocessedPoints, adjustedClusterTolerance);
            pathElements[0].ClusterCount = actualClusterCount; // 假设PathElement有ClusterCount属性
        }
        
        return pathElements;
    }

    /// <summary>
    /// 坐标预处理：平移到坐标系中心（与原始C#程序的CenterCirclePoints方法相同）
    /// </summary>
    private List<PointData> PreprocessCoordinates(List<PointData> points, float scale)
    {
        if (points.Count == 0) return points;

        // 1. 计算当前点集的质心
        float sumX = 0, sumY = 0;
        foreach (var point in points)
        {
            sumX += point.X;
            sumY += point.Y;
        }
        float centroidX = sumX / points.Count;
        float centroidY = sumY / points.Count;

        // 2. 计算平移偏移量（将质心移动到原点）
        float offsetX = -centroidX;
        float offsetY = -centroidY;

        // 3. 应用平移和缩放
        var preprocessedPoints = new List<PointData>();
        foreach (var point in points)
        {
            var preprocessedPoint = new PointData(
                (point.X + offsetX) * scale,
                (point.Y + offsetY) * scale
            );
            preprocessedPoints.Add(preprocessedPoint);
        }

        return preprocessedPoints;
    }
    
    /// <summary>
    /// 计算实际的聚类数量（用于调试）
    /// </summary>
    private int CalculateActualClusterCount(List<PointData> points, float clusterTolerance)
    {
        if (clusterTolerance <= 0 || points.Count < 10)
        {
            return 1; // 单区域路径
        }
        
        // 使用相同的聚类算法计算聚类数量
        var clusters = ClusterPoints(points, clusterTolerance);
        return clusters.Count;
    }

    /// <summary>
    /// 测试算法 - 自定义路径规划算法
    /// 结合距离优化、角度惩罚、动态权重调整和回溯机制
    /// </summary>
    private List<PathElement> GenerateTestAlgorithmPath(List<PointData> points, ProcessingConfig config)
    {
        return GenerateTestAlgorithmPath(points, config, new List<string>());
    }

    private List<PathElement> GenerateTestAlgorithmPath(List<PointData> points, ProcessingConfig config, List<string> debugLog)
    {
        try
        {
            debugLog.Add($"开始生成测试算法路径，共 {points.Count} 个点");

            // 将PointData转换为PointF
            var pointList = points.Select(p => p.ToPointF()).ToList();
            
            // 应用旋转
            if (Math.Abs(config.RotationAngle) > 0.001f)
            {
                pointList = pointList.Select(p => RotatePoint(p, config.RotationAngle)).ToList();
            }

            // 执行测试算法
            var testPath = ExecuteTestAlgorithm(pointList, config, debugLog);
            
            // 转换为PathElement
            var pathElements = new List<PathElement>();
            foreach (var point in testPath)
            {
                pathElements.Add(new PathElement { Type = "Point", Data = point });
            }

            debugLog.Add($"测试算法路径生成完成: {pathElements.Count} 个路径点");
            
            // 将调试信息输出到控制台
            foreach (var log in debugLog)
            {
                Console.WriteLine(log);
            }
            
            return pathElements;
        }
        catch (Exception ex)
        {
            debugLog.Add($"测试算法路径生成失败，回退到最近邻算法: {ex.Message}");
            Console.WriteLine($"测试算法路径生成失败，回退到最近邻算法: {ex.Message}");
            return GenerateNearestNeighborPath(points, config);
        }
    }

    /// <summary>
    /// 执行测试算法的核心逻辑（优化版 - 使用混合索引）
    /// </summary>
    private List<PointF> ExecuteTestAlgorithm(List<PointF> points, ProcessingConfig config, List<string> debugLog)
    {
        if (points.Count == 0) return new List<PointF>();
        if (points.Count == 1) return new List<PointF> { points[0] };

        var path = new List<PointF>();
        var unvisited = new HashSet<PointF>(points);
        
        // 构建混合索引结构 - 关键优化
        debugLog.Add("构建混合索引结构...");
        var spatialIndex = new HybridIndex(points, Math.Min(100, (int)Math.Sqrt(points.Count)));
        var stats = spatialIndex.GetStatistics();
        debugLog.Add($"索引统计: 总单元{stats.totalCells}, 非空单元{stats.nonEmptyCells}, 细分单元{stats.subdivisions}, 平均密度{stats.avgDensity:F2}");
        
        // 预计算平均距离用于候选点筛选
        var avgDistance = CalculateAverageDistance(points.Take(Math.Min(1000, points.Count)).ToList());
        debugLog.Add($"平均点间距离: {avgDistance:F2}");
        
        // 算法参数
        float k0 = 0.3f; // 基础角度惩罚系数
        float currentK = k0;
        float lambda = 1.0f; // 方向一致性惩罚系数
        int consecutiveTurns = 0; // 连续同向转向次数
        bool lastTurnDirection = false; // false=左转, true=右转
        int consecutiveNonBacktrack = 0; // 连续非回头选择次数
        
        // 趋势保持算法变量
        PointF? globalDirection = null; // 全局加工趋势方向
        float historyWeight = 0.7f; // 历史趋势权重
        float trendLambda = 1.0f; // 趋势惩罚系数
        int consecutiveSameDirection = 0; // 连续同向步数
        int trendPersistenceSteps = 0; // 趋势持续步数
        
        debugLog.Add($"测试算法开始，总点数: {points.Count}");
        
        // 1. 起点：最左下角
        var startPoint = FindLeftmostBottomPoint(points);
        path.Add(startPoint);
        unvisited.Remove(startPoint);
        debugLog.Add($"起点: ({startPoint.X:F2}, {startPoint.Y:F2})");
        
        // 2. 第二点：离起点最近的点
        if (unvisited.Count > 0)
        {
            var secondPoint = FindNearestPoint(startPoint, unvisited);
            path.Add(secondPoint);
            unvisited.Remove(secondPoint);
            debugLog.Add($"第二点: ({secondPoint.X:F2}, {secondPoint.Y:F2})");
        }

        // 3. 后续点：使用代价函数选择
        var recentCosts = new List<float>(); // 最近3个点的Cost值
                  var backtrackCount = 0; // 回溯次数
          var iterationCount = 0; // 迭代次数
          var lastBacktrackIteration = -10; // 上次回溯的迭代次数
          
          debugLog.Add($"开始主循环，未访问点数: {unvisited.Count}");
        
        while (unvisited.Count > 0 && backtrackCount < 50 && iterationCount < points.Count * 2)
        {
            iterationCount++;
            var currentPoint = path[path.Count - 1];
            var previousPoint = path.Count > 1 ? path[path.Count - 2] : currentPoint;
            
            // 只在关键点输出调试信息
            if (iterationCount <= 5 || iterationCount % 100 == 0)
            {
                debugLog.Add($"迭代 {iterationCount}: 当前点({currentPoint.X:F1}, {currentPoint.Y:F1}), 剩余{unvisited.Count}个点");
            }
            
            // 每1000次迭代输出进度
            if (iterationCount % 1000 == 0)
            {
                debugLog.Add($"=== 进度报告 ===");
                debugLog.Add($"迭代次数: {iterationCount}/{points.Count * 2}");
                debugLog.Add($"已处理点数: {path.Count}/{points.Count}");
                debugLog.Add($"剩余点数: {unvisited.Count}");
                debugLog.Add($"回溯次数: {backtrackCount}/50");
                debugLog.Add($"当前趋势惩罚: {trendLambda:F3}");
            }
            
            // 计算当前方向
            var currentDirection = CalculateDirection(previousPoint, currentPoint);
            
            // 特别关注6、7、8点的详细调试
            if (iterationCount >= 6 && iterationCount <= 8)
            {
                debugLog.Add($"=== 第{iterationCount}次迭代详细调试（重点关注180度问题） ===");
                debugLog.Add($"当前点: ({currentPoint.X:F2}, {currentPoint.Y:F2})");
                debugLog.Add($"前一点: ({previousPoint.X:F2}, {previousPoint.Y:F2})");
                debugLog.Add($"当前方向向量: ({currentDirection.X:F3}, {currentDirection.Y:F3})");
                debugLog.Add($"当前k值: {currentK:F3}, 当前lambda值: {lambda:F3}");
                debugLog.Add($"连续转向次数: {consecutiveTurns}, 连续非回头次数: {consecutiveNonBacktrack}");
                debugLog.Add($"未访问点数: {unvisited.Count}");
            }
            
            // 找到最佳候选点 - 动态调整候选点数量
            // 根据剩余点数和算法阶段动态调整候选点数量
            int baseCandidateCount = 50;
            if (unvisited.Count > 10000) baseCandidateCount = 30;        // 大规模：减少候选点
            else if (unvisited.Count > 1000) baseCandidateCount = 40;    // 中规模：适中候选点
            else if (unvisited.Count < 100) baseCandidateCount = Math.Min(20, unvisited.Count); // 小规模：更精确
            
            var candidateCount = Math.Min(baseCandidateCount, unvisited.Count);
            var nearestCandidates = spatialIndex.FindNearestPoints(currentPoint, candidateCount, unvisited);
            var candidateSet = new HashSet<PointF>(nearestCandidates.Where(p => unvisited.Contains(p)));
            
            // 调试信息：候选点策略
            if (iterationCount >= 6 && iterationCount <= 8)
            {
                debugLog.Add($"候选点策略: 剩余{unvisited.Count}点, 基础候选数{baseCandidateCount}, 实际候选数{candidateSet.Count}");
            }
            
            var bestCandidate = FindBestCandidateWithTrend(currentPoint, candidateSet, currentDirection, currentK, lambda, globalDirection, trendLambda, iterationCount);
            
            if (bestCandidate.HasValue)
            {
                var candidate = bestCandidate.Value;
                var cost = CalculateCostWithTrend(currentPoint, candidate, currentDirection, currentK, lambda, globalDirection, trendLambda, iterationCount);
                
                // 为6、7、8次迭代添加详细调试
                if (iterationCount >= 6 && iterationCount <= 8)
                {
                    debugLog.Add($"选择候选点: ({candidate.X:F2}, {candidate.Y:F2})");
                    debugLog.Add($"候选点Cost: {cost:F2}");
                    
                    // 计算选择点与当前方向的夹角
                    var selectedDirection = CalculateDirection(currentPoint, candidate);
                    var selectedAngleOffset = CalculateAngleOffset(currentDirection, selectedDirection);
                    debugLog.Add($"选择点的角度偏移: {selectedAngleOffset * 180 / Math.PI:F1}°");
                    
                    // 判断是否为180度回头
                    var selectedDotProduct = currentDirection.X * selectedDirection.X + currentDirection.Y * selectedDirection.Y;
                    bool is180DegreeTurn = selectedDotProduct < -0.9f; // cos(180°) = -1
                    debugLog.Add($"是否为180度回头: {is180DegreeTurn}");
                }
                
                // 检查是否需要回溯
                if (ShouldBacktrack(recentCosts, candidate, currentDirection, currentPoint, iterationCount, lastBacktrackIteration))
                {
                    // 执行回溯
                    var backtrackResult = PerformBacktrack(path, unvisited, k0);
                    path = backtrackResult.path;
                    unvisited = backtrackResult.unvisited;
                    currentK = 2 * k0; // 临时提高k值
                    lambda = 0; // 暂时禁用方向约束
                    consecutiveNonBacktrack = 0; // 重置非回头计数
                    recentCosts.Clear();
                    backtrackCount++;
                    lastBacktrackIteration = iterationCount; // 记录回溯的迭代次数
                    debugLog.Add($"执行回溯，回溯次数: {backtrackCount}，下次回溯冷却期到迭代{iterationCount + 5}");
                    continue;
                }
                
                // 添加点到路径
                path.Add(candidate);
                unvisited.Remove(candidate);
                
                // 更新连续转向计数和检查回头路
                var newDirection = CalculateDirection(currentPoint, candidate);
                var turnDirection = IsRightTurn(currentDirection, newDirection);
                if (path.Count > 2 && turnDirection == lastTurnDirection)
                {
                    consecutiveTurns++;
                }
                else
                {
                    consecutiveTurns = 0;
                }
                lastTurnDirection = turnDirection;
                
                // 趋势保持算法：更新全局方向
                var currentSegment = newDirection;
                if (globalDirection == null)
                {
                    globalDirection = currentSegment; // 初始化全局方向
                    debugLog.Add($"初始化全局趋势方向: ({globalDirection.Value.X:F3}, {globalDirection.Value.Y:F3})");
                }
                else
                {
                    // 更新全局方向：加权平均
                    var prevGlobal = globalDirection.Value;
                    globalDirection = new PointF(
                        historyWeight * prevGlobal.X + (1 - historyWeight) * currentSegment.X,
                        historyWeight * prevGlobal.Y + (1 - historyWeight) * currentSegment.Y
                    );
                    
                    // 归一化全局方向
                    var length = (float)Math.Sqrt(globalDirection.Value.X * globalDirection.Value.X + globalDirection.Value.Y * globalDirection.Value.Y);
                    if (length > 0.001f)
                    {
                        globalDirection = new PointF(globalDirection.Value.X / length, globalDirection.Value.Y / length);
                    }
                    
                    // 检查方向一致性
                    var trendDotProduct = prevGlobal.X * currentSegment.X + prevGlobal.Y * currentSegment.Y;
                    if (trendDotProduct > 0.8f) // 强一致性
                    {
                        consecutiveSameDirection++;
                        trendPersistenceSteps++;
                    }
                    else if (trendDotProduct < -0.7f) // 更严格的反向移动检测
                    {
                        consecutiveSameDirection = 0;
                        trendPersistenceSteps = 0;
                        trendLambda *= 1.2f; // 轻微增加趋势惩罚
                        debugLog.Add($"检测到反向移动，趋势惩罚系数调整为: {trendLambda:F3}");
                    }
                    else
                    {
                        consecutiveSameDirection = 0;
                    }
                    
                    // 动态调整趋势惩罚系数
                    if (consecutiveSameDirection >= 5)
                    {
                        trendLambda *= 0.9f; // 连续同向时减少惩罚
                        consecutiveSameDirection = 0; // 重置计数
                        debugLog.Add($"连续同向5步，趋势惩罚系数调整为: {trendLambda:F3}");
                    }
                    
                    // 限制趋势惩罚系数范围，避免过度惩罚
                    trendLambda = Math.Max(0.5f, Math.Min(2.0f, trendLambda));
                    
                    if (iterationCount >= 6 && iterationCount <= 8)
                    {
                        debugLog.Add($"趋势更新: 全局方向({globalDirection.Value.X:F3}, {globalDirection.Value.Y:F3}), 一致性={trendDotProduct:F3}, 趋势惩罚={trendLambda:F3}");
                    }
                }
                
                // 检查是否为回头路
                float dotProduct = currentDirection.X * newDirection.X + currentDirection.Y * newDirection.Y;
                bool isBacktrack = dotProduct < 0; // 夹角 > 90°
                
                if (!isBacktrack)
                {
                    consecutiveNonBacktrack++;
                    // 连续3次非回头选择后，降低方向惩罚
                    if (consecutiveNonBacktrack >= 3)
                    {
                        lambda = 0.5f;
                    }
                }
                else
                {
                    consecutiveNonBacktrack = 0;
                    // 检测到回头路，增强惩罚
                    lambda = 2.0f;
                }
                
                // 动态调整k值
                currentK = AdjustKValue(k0, consecutiveTurns, cost);
                
                // 记录Cost值
                recentCosts.Add(cost);
                if (recentCosts.Count > 3) recentCosts.RemoveAt(0);
            }
            else
            {
                // 没有找到候选点，强制选择一个点
                if (unvisited.Count > 0)
                {
                    var forcedCandidate = FindNearestPoint(currentPoint, unvisited);
                    path.Add(forcedCandidate);
                    unvisited.Remove(forcedCandidate);
                    
                    // 强制选择后，恢复方向约束但提高k值
                    lambda = 1.0f;
                    currentK = 1.5f * k0; // 提高50%
                }
                else
                {
                    break;
                }
            }
        }

        // 详细的完成信息
        string terminationReason = "";
        if (unvisited.Count == 0)
        {
            terminationReason = "所有点已处理完成";
        }
        else if (backtrackCount >= 50)
        {
            terminationReason = $"达到最大回溯次数限制({backtrackCount})";
        }
        else if (iterationCount >= points.Count * 2)
        {
            terminationReason = $"达到最大迭代次数限制({iterationCount})";
        }
        else
        {
            terminationReason = "未知原因提前终止";
        }
        
        debugLog.Add($"测试算法完成，路径长度: {path.Count}, 回溯次数: {backtrackCount}, 迭代次数: {iterationCount}");
        debugLog.Add($"剩余未处理点数: {unvisited.Count}, 终止原因: {terminationReason}");
        
        if (unvisited.Count > 0)
        {
            debugLog.Add($"警告：还有 {unvisited.Count} 个点未处理！");
            
            // 如果还有未处理的点，尝试简单的最近邻算法来处理剩余点
            debugLog.Add("尝试使用最近邻算法处理剩余点...");
            while (unvisited.Count > 0 && path.Count < points.Count)
            {
                var currentPoint = path[path.Count - 1];
                var nearestPoint = FindNearestPoint(currentPoint, unvisited);
                path.Add(nearestPoint);
                unvisited.Remove(nearestPoint);
                
                if (path.Count % 1000 == 0)
                {
                    debugLog.Add($"最近邻处理进度: {path.Count}/{points.Count}");
                }
            }
            debugLog.Add($"最近邻处理完成，最终路径长度: {path.Count}");
        }
        
        return path;
    }

    /// <summary>
    /// 找到最左下角的点
    /// </summary>
    private PointF FindLeftmostBottomPoint(List<PointF> points)
    {
        return points.OrderBy(p => p.X).ThenBy(p => p.Y).First();
    }

    /// <summary>
    /// 找到最近的点
    /// </summary>
    private PointF FindNearestPoint(PointF current, HashSet<PointF> candidates)
    {
        return candidates.OrderBy(p => EuclideanDistance(current, p)).First();
    }

    /// <summary>
    /// 找到最佳候选点（支持二级前瞻）
    /// </summary>
    private PointF? FindBestCandidate(PointF current, HashSet<PointF> candidates, PointF direction, float k, float lambda = 1.0f, int iterationCount = 0)
    {
        if (candidates.Count == 0) return null;
        
        // 优先使用二级前瞻决策
        var lookaheadResult = FindBestCandidateWithLookahead(current, candidates, direction, k, lambda, iterationCount);
        if (lookaheadResult != null) return lookaheadResult;
        
        // 降级到单级决策
        return FindBestCandidateSingleLevel(current, candidates, direction, k, lambda, iterationCount);
    }

    /// <summary>
    /// 找到最佳候选点（支持趋势保持）
    /// </summary>
    private PointF? FindBestCandidateWithTrend(PointF current, HashSet<PointF> candidates, PointF direction, float k, float lambda, PointF? globalDirection, float trendLambda, int iterationCount = 0)
    {
        if (candidates.Count == 0) return null;
        
        PointF? bestCandidate = null;
        float minCost = float.MaxValue;
        var debugInfo = new List<string>();

        // 首先尝试找到Cost最小的点
        foreach (var candidate in candidates)
        {
            var cost = CalculateCostWithTrend(current, candidate, direction, k, lambda, globalDirection, trendLambda, iterationCount);
            var distance = EuclideanDistance(current, candidate);
            var candidateDirection = CalculateDirection(current, candidate);
            var dotProduct = direction.X * candidateDirection.X + direction.Y * candidateDirection.Y;
            
            // 避免选择180度回头的点，除非没有其他选择
            if (dotProduct < -0.9f && candidates.Count > 1) // 180度回头且有其他选择
            {
                continue; // 跳过这个候选点
            }
            
            var angleOffset = CalculateAngleOffset(direction, candidateDirection);
            var directionPenalty = CalculateDirectionConsistencyPenalty(direction, candidateDirection, lambda);
            
            debugInfo.Add($"候选点({candidate.X:F2},{candidate.Y:F2}): 距离={distance:F2}, 角度={angleOffset * 180 / Math.PI:F1}°, 方向惩罚={directionPenalty:F2}, Cost={cost:F2}");
            
            if (cost < minCost)
            {
                minCost = cost;
                bestCandidate = candidate;
            }
        }

        // 为6、7、8次迭代添加详细调试
        if (iterationCount >= 6 && iterationCount <= 8)
        {
            Console.WriteLine($"=== FindBestCandidateWithTrend 第{iterationCount}次迭代详细分析 ===");
            Console.WriteLine($"当前点: ({current.X:F2}, {current.Y:F2})");
            Console.WriteLine($"当前方向: ({direction.X:F3}, {direction.Y:F3})");
            Console.WriteLine($"全局趋势: ({(globalDirection?.X ?? 0):F3}, {(globalDirection?.Y ?? 0):F3})");
            Console.WriteLine($"候选点数量: {candidates.Count}");
            Console.WriteLine($"k值: {k:F3}, lambda值: {lambda:F3}, trendLambda值: {trendLambda:F3}");
            
            // 显示前5个最佳候选点的详细信息
            var topCandidates = debugInfo.OrderBy(x => {
                var parts = x.Split(',');
                var costPart = parts.Last().Split('=')[1];
                return float.Parse(costPart);
            }).Take(5);
            
            Console.WriteLine("前5个最佳候选点:");
            foreach (var info in topCandidates)
            {
                Console.WriteLine($"  {info}");
            }
            
            if (bestCandidate.HasValue)
            {
                Console.WriteLine($"最终选择: ({bestCandidate.Value.X:F2}, {bestCandidate.Value.Y:F2}), Cost: {minCost:F2}");
            }
        }

        // 如果趋势算法找不到合适的候选点，降级到基础算法
        if (bestCandidate == null && candidates.Count > 0)
        {
            debugInfo.Add("趋势算法未找到候选点，降级到基础算法");
            
            // 使用更宽松的条件：仅考虑距离，忽略趋势惩罚
            float minDistance = float.MaxValue;
            foreach (var candidate in candidates)
            {
                var distance = EuclideanDistance(current, candidate);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    bestCandidate = candidate;
                }
            }
            
            if (iterationCount >= 6 && iterationCount <= 8)
            {
                Console.WriteLine($"降级选择最近点: ({bestCandidate?.X:F2}, {bestCandidate?.Y:F2}), 距离: {minDistance:F2}");
            }
        }
        
        // 最终保证：如果还没有候选点，强制选择第一个
        if (bestCandidate == null && candidates.Count > 0)
        {
            bestCandidate = candidates.First();
            if (iterationCount >= 6 && iterationCount <= 8)
            {
                Console.WriteLine($"强制选择: ({bestCandidate?.X:F2}, {bestCandidate?.Y:F2})");
            }
        }

        return bestCandidate;
    }
    
    /// <summary>
    /// 二级前瞻决策机制
    /// </summary>
    private PointF? FindBestCandidateWithLookahead(PointF current, HashSet<PointF> candidates, PointF direction, float k, float lambda, int iterationCount)
    {
        if (candidates.Count < 2) return null; // 候选点太少，无法前瞻
        
        PointF? bestCandidate = null;
        float minTotalCost = float.MaxValue;
        
        foreach (var candidate1 in candidates)
        {
            // 计算到第一个候选点的代价
            var cost1 = CalculateCost(current, candidate1, direction, k, lambda, iterationCount);
            var direction1 = CalculateDirection(current, candidate1);
            
            // 寻找第二级最佳候选点
            var remainingCandidates = new HashSet<PointF>(candidates);
            remainingCandidates.Remove(candidate1);
            
            if (remainingCandidates.Count == 0) continue;
            
            PointF? bestCandidate2 = null;
            float minCost2 = float.MaxValue;
            
            foreach (var candidate2 in remainingCandidates)
            {
                var cost2 = CalculateCost(candidate1, candidate2, direction1, k, lambda);
                if (cost2 < minCost2)
                {
                    minCost2 = cost2;
                    bestCandidate2 = candidate2;
                }
            }
            
            if (bestCandidate2 == null) continue;
            
            // 计算二级总代价：TotalCost = Cost1 + 0.7×Cost2
            var totalCost = cost1 + 0.7f * minCost2;
            
            // 方向连续性检测
            var direction2 = CalculateDirection(candidate1, bestCandidate2.Value);
            var isConsistent = IsDirectionConsistent(direction, direction1, direction2);
            
            // 方向一致性奖励
            if (isConsistent)
            {
                totalCost *= 0.9f; // 10%奖励
            }
            
            // 检测是否避免了180度回头
            var avoids180 = !Is180DegreeTurn(direction, direction1) && !Is180DegreeTurn(direction1, direction2);
            if (avoids180)
            {
                totalCost *= 0.8f; // 20%奖励
            }
            
            if (totalCost < minTotalCost)
            {
                minTotalCost = totalCost;
                bestCandidate = candidate1;
            }
            
            // 调试信息
            if (iterationCount >= 6 && iterationCount <= 8)
            {
                Console.WriteLine($"二级前瞻: {candidate1.X:F1},{candidate1.Y:F1} → {bestCandidate2.Value.X:F1},{bestCandidate2.Value.Y:F1}");
                Console.WriteLine($"  Cost1={cost1:F2}, Cost2={minCost2:F2}, 总代价={totalCost:F2}");
                Console.WriteLine($"  方向一致={isConsistent}, 避免180度={avoids180}");
            }
        }
        
        return bestCandidate;
    }

    
    /// <summary>
    /// 单级决策（降级方案）
    /// </summary>
    private PointF? FindBestCandidateSingleLevel(PointF current, HashSet<PointF> candidates, PointF direction, float k, float lambda = 1.0f, int iterationCount = 0)
    {
        if (candidates.Count == 0) return null;
        
        PointF? bestCandidate = null;
        float minCost = float.MaxValue;
        var debugInfo = new List<string>();

        // 首先尝试找到Cost最小的点
        foreach (var candidate in candidates)
        {
            var cost = CalculateCost(current, candidate, direction, k, lambda, iterationCount);
            var distance = EuclideanDistance(current, candidate);
            var candidateDirection = CalculateDirection(current, candidate);
            var dotProduct = direction.X * candidateDirection.X + direction.Y * candidateDirection.Y;
            
            // 避免选择180度回头的点，除非没有其他选择
            if (dotProduct < -0.9f && candidates.Count > 1) // 180度回头且有其他选择
            {
                continue; // 跳过这个候选点
            }
            
            var angleOffset = CalculateAngleOffset(direction, candidateDirection);
            var directionPenalty = CalculateDirectionConsistencyPenalty(direction, candidateDirection, lambda);
            
            debugInfo.Add($"候选点({candidate.X:F2},{candidate.Y:F2}): 距离={distance:F2}, 角度={angleOffset * 180 / Math.PI:F1}°, 方向惩罚={directionPenalty:F2}, Cost={cost:F2}");
            
            if (cost < minCost)
            {
                minCost = cost;
                bestCandidate = candidate;
            }
        }

        // 为6、7、8次迭代添加详细调试
        if (iterationCount >= 6 && iterationCount <= 8)
        {
            Console.WriteLine($"=== FindBestCandidate 第{iterationCount}次迭代详细分析 ===");
            Console.WriteLine($"当前点: ({current.X:F2}, {current.Y:F2})");
            Console.WriteLine($"当前方向: ({direction.X:F3}, {direction.Y:F3})");
            Console.WriteLine($"候选点数量: {candidates.Count}");
            Console.WriteLine($"k值: {k:F3}, lambda值: {lambda:F3}");
            
            // 显示前5个最佳候选点的详细信息
            var topCandidates = debugInfo.OrderBy(x => {
                var parts = x.Split(',');
                var costPart = parts.Last().Split('=')[1];
                return float.Parse(costPart);
            }).Take(5).ToList();
            
            Console.WriteLine("前5个最佳候选点:");
            foreach (var info in topCandidates)
            {
                Console.WriteLine($"  {info}");
            }
            
            if (bestCandidate.HasValue)
            {
                Console.WriteLine($"选择的最佳候选点: ({bestCandidate.Value.X:F2}, {bestCandidate.Value.Y:F2})");
                Console.WriteLine($"最小Cost: {minCost:F2}");
                
                // 计算选择点与当前方向的夹角
                var selectedDirection = CalculateDirection(current, bestCandidate.Value);
                var selectedAngleOffset = CalculateAngleOffset(direction, selectedDirection);
                Console.WriteLine($"选择点的角度偏移: {selectedAngleOffset * 180 / Math.PI:F1}°");
                
                // 判断是否为180度回头
                var dotProduct = direction.X * selectedDirection.X + direction.Y * selectedDirection.Y;
                bool is180DegreeTurn = dotProduct < -0.9f; // cos(180°) = -1
                Console.WriteLine($"是否为180度回头: {is180DegreeTurn}");
            }
        }

        // 如果Cost都太高，选择距离最近的点（忽略角度惩罚）
        if (bestCandidate == null || minCost > 10000) // 如果Cost超过10000，认为太高
        {
            float minDistance = float.MaxValue;
            foreach (var candidate in candidates)
            {
                var distance = EuclideanDistance(current, candidate);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    bestCandidate = candidate;
                }
            }
        }

        // 最后保证：如果还有候选点，一定要选择一个
        if (bestCandidate == null && candidates.Count > 0)
        {
            bestCandidate = candidates.First();
        }

        return bestCandidate;
    }

    /// <summary>
    /// 计算代价函数：Cost = 距离 + k × |角度偏移| + λ × 方向一致性惩罚
    /// </summary>
    private float CalculateCost(PointF current, PointF candidate, PointF direction, float k, float lambda = 1.0f, int iterationCount = 0)
    {
        // 距离
        float distance = EuclideanDistance(current, candidate);
        
        // 角度偏移
        var candidateDirection = CalculateDirection(current, candidate);
        float angleOffset = CalculateAngleOffset(direction, candidateDirection);
        
        // 方向一致性惩罚
        float directionPenalty = CalculateDirectionConsistencyPenalty(direction, candidateDirection, lambda);
        
        float totalCost = distance + k * Math.Abs(angleOffset) + directionPenalty;
        
        // 为6、7、8次迭代添加详细调试
        if (iterationCount >= 6 && iterationCount <= 8)
        {
            Console.WriteLine($"  CalculateCost详情 - 候选点({candidate.X:F2},{candidate.Y:F2}):");
            Console.WriteLine($"    距离: {distance:F2}");
            Console.WriteLine($"    角度偏移: {angleOffset * 180 / Math.PI:F1}°");
            Console.WriteLine($"    角度惩罚: {k * Math.Abs(angleOffset):F2}");
            Console.WriteLine($"    方向一致性惩罚: {directionPenalty:F2}");
            Console.WriteLine($"    总Cost: {totalCost:F2}");
        }
        
        return totalCost;
    }

    /// <summary>
    /// 计算移动到候选点的代价（包含趋势保持）
    /// </summary>
    private float CalculateCostWithTrend(PointF current, PointF candidate, PointF direction, float k, float lambda, PointF? globalDirection, float trendLambda, int iterationCount = 0)
    {
        // 基础代价计算
        float distance = EuclideanDistance(current, candidate);
        var candidateDirection = CalculateDirection(current, candidate);
        float angleOffset = CalculateAngleOffset(direction, candidateDirection);
        float directionPenalty = CalculateDirectionConsistencyPenalty(direction, candidateDirection, lambda);
        
        // 趋势一致性代价
        float trendPenalty = 0;
        if (globalDirection.HasValue)
        {
            // 计算候选移动方向与全局趋势的夹角
            float trendDotProduct = globalDirection.Value.X * candidateDirection.X + globalDirection.Value.Y * candidateDirection.Y;
            float trendAngleCos = Math.Max(-1f, Math.Min(1f, trendDotProduct)); // 限制在[-1,1]范围
            
            // 使用更温和的趋势惩罚：仅对明显反向的移动加重惩罚
            if (trendAngleCos < -0.3f) // 夹角 > 107度时才施加趋势惩罚
            {
                // 趋势惩罚 = trendLambda × |1 - cosθ|，但限制最大值
                trendPenalty = Math.Min(distance * 0.5f, trendLambda * Math.Abs(1f - trendAngleCos));
            }
            else
            {
                // 对于不太偏离趋势的移动，只施加轻微惩罚
                trendPenalty = trendLambda * 0.1f * Math.Abs(1f - trendAngleCos);
            }
        }
        
        float totalCost = distance + k * Math.Abs(angleOffset) + directionPenalty + trendPenalty;
        
        // 为6、7、8次迭代添加详细调试
        if (iterationCount >= 6 && iterationCount <= 8)
        {
            Console.WriteLine($"  CalculateCostWithTrend详情 - 候选点({candidate.X:F2},{candidate.Y:F2}):");
            Console.WriteLine($"    距离: {distance:F2}");
            Console.WriteLine($"    角度偏移: {angleOffset * 180 / Math.PI:F1}°");
            Console.WriteLine($"    角度惩罚: {k * Math.Abs(angleOffset):F2}");
            Console.WriteLine($"    方向一致性惩罚: {directionPenalty:F2}");
            Console.WriteLine($"    趋势惩罚: {trendPenalty:F2}");
            Console.WriteLine($"    总Cost: {totalCost:F2}");
        }
        
        return totalCost;
    }

    /// <summary>
    /// 计算方向向量
    /// </summary>
    private PointF CalculateDirection(PointF from, PointF to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var length = (float)Math.Sqrt(dx * dx + dy * dy);
        
        if (length < 0.001f) return new PointF(0, 1); // 避免除零
        
        return new PointF(dx / length, dy / length);
    }

    /// <summary>
    /// 计算角度偏移（弧度）
    /// </summary>
    private float CalculateAngleOffset(PointF dir1, PointF dir2)
    {
        // 使用点积计算夹角
        float dotProduct = dir1.X * dir2.X + dir1.Y * dir2.Y;
        dotProduct = Math.Max(-1, Math.Min(1, dotProduct)); // 限制在[-1,1]范围内
        return (float)Math.Acos(dotProduct);
    }

    /// <summary>
    /// 计算方向一致性惩罚
    /// 当候选方向与当前方向夹角 > 90° 时施加惩罚
    /// </summary>
    private float CalculateDirectionConsistencyPenalty(PointF currentDirection, PointF candidateDirection, float lambda)
    {
        // 计算点积
        float dotProduct = currentDirection.X * candidateDirection.X + currentDirection.Y * candidateDirection.Y;
        
        // 如果点积 < 0，说明夹角 > 90°（回头路）
        if (dotProduct < 0)
        {
            // 惩罚值 = M × (1 + cosθ)，其中 M = 10 × k₀
            float M = 10.0f * 0.3f; // k₀ = 0.3
            float cosTheta = dotProduct; // 点积就是cosθ
            
            // 特别增强180度回头的惩罚
            if (dotProduct < -0.9f) // 接近180度回头
            {
                M = 100.0f * 0.3f; // 大幅增加惩罚系数
            }
            else if (dotProduct < -0.7f) // 接近135度回头
            {
                M = 50.0f * 0.3f; // 中等增加惩罚系数
            }
            
            return lambda * M * (1 + cosTheta);
        }
        
        return 0; // 非回头路，无惩罚
    }

    /// <summary>
    /// 判断是否为右转
    /// </summary>
    private bool IsRightTurn(PointF dir1, PointF dir2)
    {
        // 使用叉积判断转向
        float crossProduct = dir1.X * dir2.Y - dir1.Y * dir2.X;
        return crossProduct < 0; // 负值表示右转
    }

    /// <summary>
    /// 动态调整k值
    /// </summary>
    private float AdjustKValue(float k0, int consecutiveTurns, float currentCost)
    {
        // 连续同向转向时增加惩罚
        if (consecutiveTurns > 0)
        {
            return k0 * (1 + 0.1f * consecutiveTurns);
        }
        
        // 路径趋于直线时逐渐恢复
        return k0;
    }

    /// <summary>
    /// 判断是否需要回溯
    /// </summary>
    private bool ShouldBacktrack(List<float> recentCosts, PointF candidate, PointF currentDirection, PointF currentPoint, int currentIteration, int lastBacktrackIteration)
    {
        // 回溯冷却期：防止连续回溯
        if (currentIteration - lastBacktrackIteration < 5) // 5次迭代的冷却期
        {
            return false; // 冷却期内不允许回溯
        }
        
        // 检查是否为180度回头 - 这是最重要的回溯条件
        var candidateDirection = CalculateDirection(currentPoint, candidate);
        var dotProduct = currentDirection.X * candidateDirection.X + currentDirection.Y * candidateDirection.Y;
        
        if (dotProduct < -0.95f) // 更严格：几乎完全的180度回头
        {
            Console.WriteLine($"*** 检测到180度回头，触发回溯 ***");
            Console.WriteLine($"当前点: ({currentPoint.X:F2}, {currentPoint.Y:F2})");
            Console.WriteLine($"候选点: ({candidate.X:F2}, {candidate.Y:F2})");
            Console.WriteLine($"当前方向: ({currentDirection.X:F3}, {currentDirection.Y:F3})");
            Console.WriteLine($"候选方向: ({candidateDirection.X:F3}, {candidateDirection.Y:F3})");
            Console.WriteLine($"点积: {dotProduct:F3}");
            return true; // 立即触发回溯
        }
        
        // 暂时禁用Cost上升回溯，避免过度回溯
        // if (recentCosts.Count >= 3)
        // {
        //     bool increasing = recentCosts[0] < recentCosts[1] && recentCosts[1] < recentCosts[2];
        //     float costIncrease = recentCosts[2] - recentCosts[0];
        //     if (increasing && costIncrease > 3000)
        //     {
        //         return true;
        //     }
        // }
        
        return false;
    }

    /// <summary>
    /// 执行回溯
    /// </summary>
    private (List<PointF> path, HashSet<PointF> unvisited) PerformBacktrack(List<PointF> path, HashSet<PointF> unvisited, float k0)
    {
        // 回退2-3个点
        int backtrackSteps = Math.Min(3, path.Count - 1);
        var newPath = path.Take(path.Count - backtrackSteps).ToList();
        
        // 将回退的点重新加入未访问集合
        for (int i = path.Count - backtrackSteps; i < path.Count; i++)
        {
            unvisited.Add(path[i]);
        }
        
        return (newPath, unvisited);
    }

    /// <summary>
    /// 根据角度偏移调整加工速度
    /// </summary>
    private float GetSpeedAdjustment(float angleOffset)
    {
        float angleDegrees = angleOffset * 180 / (float)Math.PI;
        
        if (angleDegrees < 20)
            return 1.0f; // 100%
        else if (angleDegrees < 45)
            return 0.9f; // 90%
        else
            return 0.8f; // 80%
    }

    /// <summary>
    /// 方向连续性检测
    /// </summary>
    private bool IsDirectionConsistent(PointF dir1, PointF dir2, PointF dir3)
    {
        // 检查连续方向的点积是否都为正（同向）
        var dot1 = dir1.X * dir2.X + dir1.Y * dir2.Y;
        var dot2 = dir2.X * dir3.X + dir2.Y * dir3.Y;
        return dot1 > 0 && dot2 > 0;
    }
    
    /// <summary>
    /// 检测是否为180度回头
    /// </summary>
    private bool Is180DegreeTurn(PointF dir1, PointF dir2)
    {
        var dotProduct = dir1.X * dir2.X + dir1.Y * dir2.Y;
        return dotProduct < -0.95f; // 接近180度
    }

    #region 聚类算法强化版 - 自适应网格 + 空间索引优化

    /// <summary>
    /// 生成聚类算法强化版路径
    /// </summary>
    public List<PathElement> GenerateEnhancedClusterPath(List<PointData> points, ProcessingConfig config)
    {
        var debugLog = new List<string>();
        return GenerateEnhancedClusterPath(points, config, debugLog);
    }

    /// <summary>
    /// 生成聚类算法强化版路径（带调试信息）
    /// </summary>
    public List<PathElement> GenerateEnhancedClusterPath(List<PointData> points, ProcessingConfig config, List<string> debugLog)
    {
        try
        {
            debugLog.Add("=== 聚类算法强化版开始 ===");
            debugLog.Add($"输入点数: {points.Count}");
            
            if (points.Count == 0) return new List<PathElement>();
            if (points.Count == 1) return new List<PathElement> { new PathElement { Type = "Point", Data = points[0].ToPointF() } };

            // 1. 预处理：构建混合索引结构
            debugLog.Add("构建混合索引结构...");
            var pointList = points.Select(p => p.ToPointF()).ToList();
            var spatialIndex = new HybridIndex(pointList, Math.Min(100, (int)Math.Sqrt(pointList.Count)));
            var stats = spatialIndex.GetStatistics();
            debugLog.Add($"索引统计: 总单元{stats.totalCells}, 非空单元{stats.nonEmptyCells}, 细分单元{stats.subdivisions}, 平均密度{stats.avgDensity:F2}");

            // 2. 自适应分组：使用空间索引替代手动容差
            debugLog.Add("执行自适应空间分组...");
            var clusters = AutoClusterWithSpatialIndex(points, spatialIndex, debugLog);
            debugLog.Add($"自适应分组完成，共 {clusters.Count} 个群组");

            // 3. 群组排序：保持原聚类算法的排序逻辑
            debugLog.Add("执行群组排序...");
            var orderedClusters = OrderClustersByDistance(clusters);
            
            // 4. 群组内路径生成：使用空间索引优化的蛇形算法
            debugLog.Add("为每个群组生成优化路径...");
            var allPathElements = new List<PathElement>();
            
            for (int i = 0; i < orderedClusters.Count; i++)
            {
                var cluster = orderedClusters[i];
                debugLog.Add($"处理群组 {i + 1}/{orderedClusters.Count}: {cluster.Count} 个点");
                
                // 使用空间索引优化的蛇形算法
                var clusterPath = GenerateOptimizedSnakePathForCluster(cluster, spatialIndex, config, debugLog);
                allPathElements.AddRange(clusterPath);
                
                debugLog.Add($"群组 {i + 1} 路径生成完成: {clusterPath.Count} 个路径点");
            }

            debugLog.Add($"=== 聚类算法强化版完成 ===");
            debugLog.Add($"总计生成 {allPathElements.Count} 个路径点");
            
            // 设置第一个路径元素的群组数量信息
            if (allPathElements.Count > 0)
            {
                allPathElements[0].ClusterCount = clusters.Count;
            }
            
            return allPathElements;
        }
        catch (Exception ex)
        {
            debugLog.Add($"聚类算法强化版失败，回退到原始聚类算法: {ex.Message}");
            return GenerateClusterPath(points, config);
        }
    }

    /// <summary>
    /// 使用空间索引进行自适应聚类
    /// </summary>
    private List<List<PointData>> AutoClusterWithSpatialIndex(List<PointData> points, HybridIndex spatialIndex, List<string> debugLog)
    {
        var clusters = new List<List<PointData>>();
        var visited = new HashSet<PointData>();
        var pointList = points.Select(p => p.ToPointF()).ToList();

        // 计算动态聚类参数
        var avgDistance = CalculateAverageDistance(pointList);
        debugLog.Add($"平均点间距离: {avgDistance:F2}");

        foreach (var point in points)
        {
            if (visited.Contains(point)) continue;

            var cluster = new List<PointData>();
            
            // 使用自适应delta进行聚类
            var autoDelta = CalculateAutoDelta(point.ToPointF(), spatialIndex, avgDistance);
            
            ExpandClusterWithSpatialIndex(point, points, cluster, visited, autoDelta, spatialIndex);

            if (cluster.Count > 0)
            {
                clusters.Add(cluster);
                debugLog.Add($"群组 {clusters.Count}: {cluster.Count} 个点, delta={autoDelta:F2}");
            }
        }

        // 后处理：合并过小的群组
        debugLog.Add($"初始分组完成，共 {clusters.Count} 个群组，开始优化合并...");
        var optimizedClusters = MergeSmallClusters(clusters, avgDistance, debugLog);
        debugLog.Add($"优化后分组：{optimizedClusters.Count} 个群组");

        return optimizedClusters;
    }

    /// <summary>
    /// 计算自适应delta值（参数调优版）
    /// </summary>
    private float CalculateAutoDelta(PointF point, HybridIndex spatialIndex, float avgDistance)
    {
        // 基于空间索引的局部密度计算
        var nearbyPoints = spatialIndex.FindNearestPoints(point, 20, null);
        
        // 计算中位最近邻距离作为基础尺寸
        var distances = new List<float>();
        foreach (var nearbyPoint in nearbyPoints)
        {
            if (nearbyPoint != point)
            {
                var dist = EuclideanDistance(point, nearbyPoint);
                if (dist > 0.001f) distances.Add(dist);
            }
        }
        
        float medianDistance;
        if (distances.Count == 0)
        {
            medianDistance = avgDistance * 0.1f; // 孤立点使用较小基础值
        }
        else
        {
            distances.Sort();
            medianDistance = distances[distances.Count / 2]; // 中位数
        }
        
        // grid_size = median_nearest_distance * 2.5  基础尺寸
        float gridSize = medianDistance * 2.5f;
        
        // 动态参数计算
        var baseSize = gridSize;
        var minDelta = gridSize * 0.3f;
        var maxDelta = gridSize * 1.8f;
        var singlePoint = gridSize * 1.2f; // 孤立点特殊处理
        
        // 根据邻居数量选择策略
        if (nearbyPoints.Count <= 3)
        {
            // 孤立点特殊处理
            return Math.Max(singlePoint, 200.0f); // 提高最小值
        }
        
        // 密度调整因子
        float densityFactor = Math.Min(2.0f, 20.0f / nearbyPoints.Count); // 密度越高因子越小
        float calculatedDelta = baseSize * densityFactor;
        
        // 应用动态范围限制
        calculatedDelta = Math.Max(minDelta, calculatedDelta);
        calculatedDelta = Math.Min(maxDelta, calculatedDelta);
        
        // 全局范围保护（避免极端值）
        calculatedDelta = Math.Max(200.0f, calculatedDelta); // 提高全局最小值
        calculatedDelta = Math.Min(avgDistance * 0.8f, calculatedDelta);
        
        return calculatedDelta;
    }

    /// <summary>
    /// 使用空间索引的群组扩展
    /// </summary>
    private void ExpandClusterWithSpatialIndex(
        PointData seedPoint,
        List<PointData> allPoints,
        List<PointData> cluster,
        HashSet<PointData> visited,
        float tolerance,
        HybridIndex spatialIndex)
    {
        var queue = new Queue<PointData>();
        visited.Add(seedPoint);
        queue.Enqueue(seedPoint);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            cluster.Add(current);

            // 使用空间索引快速找到邻居
            var neighbors = FindNeighborsWithSpatialIndex(current, allPoints, tolerance, visited, spatialIndex);
            foreach (var neighbor in neighbors)
            {
                visited.Add(neighbor);
                queue.Enqueue(neighbor);
            }
        }
    }

    /// <summary>
    /// 使用空间索引查找邻居
    /// </summary>
    private List<PointData> FindNeighborsWithSpatialIndex(
        PointData point,
        List<PointData> allPoints,
        float tolerance,
        HashSet<PointData> visited,
        HybridIndex spatialIndex)
    {
        // 使用空间索引快速获取候选邻居
        var candidateCount = Math.Min(50, allPoints.Count);
        var nearestCandidates = spatialIndex.FindNearestPoints(point.ToPointF(), candidateCount, null);
        
        var result = new List<PointData>();
        
        foreach (var candidatePoint in nearestCandidates)
        {
            // 在原始点列表中找到对应的PointData
            var pointData = allPoints.FirstOrDefault(p => 
                Math.Abs(p.X - candidatePoint.X) < 0.001f && 
                Math.Abs(p.Y - candidatePoint.Y) < 0.001f);
                
            if (pointData != null && 
                !visited.Contains(pointData) && 
                EuclideanDistance(pointData, point) <= tolerance)
            {
                result.Add(pointData);
            }
        }
        
        return result;
    }

    /// <summary>
    /// 为单个群组生成优化的蛇形路径
    /// </summary>
    private List<PathElement> GenerateOptimizedSnakePathForCluster(
        List<PointData> cluster, 
        HybridIndex spatialIndex, 
        ProcessingConfig config,
        List<string> debugLog)
    {
        if (cluster.Count == 0) return new List<PathElement>();
        if (cluster.Count == 1) return new List<PathElement> { new PathElement { Type = "Point", Data = cluster[0].ToPointF() } };

        debugLog.Add($"为群组生成优化蛇形路径: {cluster.Count} 个点");
        
        // 使用空间索引优化的蛇形方向判断
        var optimizedPath = GenerateSnakePathWithSpatialOptimization(cluster, spatialIndex, config.PathTolerance1);
        
        return optimizedPath.Select(p => new PathElement { Type = "Point", Data = p }).ToList();
    }

    /// <summary>
    /// 生成带空间索引优化的蛇形路径（完整优化版）
    /// </summary>
    private List<PointF> GenerateSnakePathWithSpatialOptimization(List<PointData> cluster, HybridIndex spatialIndex, float tolerance)
    {
        if (cluster.Count == 0) return new List<PointF>();
        
        var pointFs = cluster.Select(p => p.ToPointF()).ToList();
        
        // 使用空间索引优化的行分组
        var rows = GroupPointsIntoRowsWithSpatialIndex(pointFs, spatialIndex, tolerance);
        
        var path = new List<PointF>();
        bool reverse = false;
        PointF? lastDirection = null; // 方向权重：0.3

        for (int i = 0; i < rows.Count; i++)
        {
            var rowPoints = rows[i];

            // 空间索引增强的蛇形方向优化（带方向权重）
            if (reverse)
            {
                rowPoints = OptimizeRowDirectionWithWeight(rowPoints, spatialIndex, reverse: true, lastDirection, 0.3f);
            }
            else
            {
                rowPoints = OptimizeRowDirectionWithWeight(rowPoints, spatialIndex, reverse: false, lastDirection, 0.3f);
            }

            // 去重机制：合并相近点
            var deduplicatedPoints = DeduplicatePoints(rowPoints, tolerance * 0.1f); // 精度阈值
            
            path.AddRange(deduplicatedPoints);
            
            // 更新方向向量
            if (deduplicatedPoints.Count >= 2)
            {
                var first = deduplicatedPoints[0];
                var last = deduplicatedPoints[deduplicatedPoints.Count - 1];
                lastDirection = new PointF(last.X - first.X, last.Y - first.Y);
                
                // 标准化方向向量
                float length = (float)Math.Sqrt(lastDirection.Value.X * lastDirection.Value.X + lastDirection.Value.Y * lastDirection.Value.Y);
                if (length > 0.001f)
                {
                    lastDirection = new PointF(lastDirection.Value.X / length, lastDirection.Value.Y / length);
                }
            }
            
            reverse = !reverse;
        }

        return path;
    }

    /// <summary>
    /// 使用空间索引将点分组到行中
    /// </summary>
    private List<List<PointF>> GroupPointsIntoRowsWithSpatialIndex(List<PointF> points, HybridIndex spatialIndex, float tolerance)
    {
        // 基于Y坐标的容差分组，但使用空间索引优化
        var rows = new List<List<PointF>>();
        var processed = new HashSet<PointF>();

        // 按Y坐标排序
        var sortedPoints = points.OrderBy(p => p.Y).ToList();

        foreach (var point in sortedPoints)
        {
            if (processed.Contains(point)) continue;

            var row = new List<PointF>();
            CollectRowPointsWithSpatialIndex(point, points, row, processed, tolerance, spatialIndex);
            
            if (row.Count > 0)
            {
                rows.Add(row);
            }
        }

        return rows;
    }

    /// <summary>
    /// 使用空间索引收集同一行的点
    /// </summary>
    private void CollectRowPointsWithSpatialIndex(
        PointF seedPoint, 
        List<PointF> allPoints, 
        List<PointF> row, 
        HashSet<PointF> processed, 
        float tolerance,
        HybridIndex spatialIndex)
    {
        // 使用空间索引找到Y坐标相近的点
        var candidates = spatialIndex.FindNearestPoints(seedPoint, Math.Min(50, allPoints.Count), processed);
        
        foreach (var candidate in candidates)
        {
            if (!processed.Contains(candidate) && Math.Abs(candidate.Y - seedPoint.Y) <= tolerance)
            {
                row.Add(candidate);
                processed.Add(candidate);
            }
        }
        
        // 如果空间索引没找到足够的点，回退到传统方法
        if (row.Count == 0)
        {
            foreach (var point in allPoints)
            {
                if (!processed.Contains(point) && Math.Abs(point.Y - seedPoint.Y) <= tolerance)
                {
                    row.Add(point);
                    processed.Add(point);
                }
            }
        }
    }

    /// <summary>
    /// 优化行内点的排列方向
    /// </summary>
    private List<PointF> OptimizeRowDirection(List<PointF> rowPoints, HybridIndex spatialIndex, bool reverse)
    {
        if (rowPoints.Count <= 1) return rowPoints;
        
        // 基础排序
        var sortedPoints = reverse ? 
            rowPoints.OrderByDescending(p => p.X).ToList() : 
            rowPoints.OrderBy(p => p.X).ToList();
        
        // 使用空间索引进行局部优化
        return OptimizeRowWithSpatialIndex(sortedPoints, spatialIndex);
    }

    /// <summary>
    /// 使用空间索引优化行内路径
    /// </summary>
    private List<PointF> OptimizeRowWithSpatialIndex(List<PointF> sortedPoints, HybridIndex spatialIndex)
    {
        if (sortedPoints.Count <= 2) return sortedPoints;
        
        var optimized = new List<PointF> { sortedPoints[0] };
        var remaining = new HashSet<PointF>(sortedPoints.Skip(1));
        
        var current = sortedPoints[0];
        
        while (remaining.Count > 0)
        {
            // 使用空间索引找到最近的未访问点
            var nearestCandidates = spatialIndex.FindNearestPoints(current, Math.Min(5, remaining.Count), null);
            
            PointF? nextPoint = null;
            float minDistance = float.MaxValue;
            
            foreach (var candidate in nearestCandidates)
            {
                if (remaining.Contains(candidate))
                {
                    var distance = EuclideanDistance(current, candidate);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nextPoint = candidate;
                    }
                }
            }
            
            // 如果空间索引没找到，使用最近的剩余点
            if (nextPoint == null)
            {
                nextPoint = remaining.OrderBy(p => EuclideanDistance(current, p)).First();
            }
            
            optimized.Add(nextPoint.Value);
            remaining.Remove(nextPoint.Value);
            current = nextPoint.Value;
        }
        
        return optimized;
    }

    /// <summary>
    /// 合并过小的群组
    /// </summary>
    private List<List<PointData>> MergeSmallClusters(List<List<PointData>> clusters, float avgDistance, List<string> debugLog)
    {
        // 设定小群组的阈值（按你的建议调整）
        int smallClusterThreshold = 5; // 少于5个点的群组被认为是小群组
        int mainPathThreshold = 20; // 大于20个点的群组被认为是主要路径
        float maxMergeDistance = avgDistance * 0.5f; // 合并距离阈值
        
        var largeClusters = new List<List<PointData>>();
        var smallClusters = new List<List<PointData>>();
        
        // 分离大群组和小群组
        foreach (var cluster in clusters)
        {
            if (cluster.Count >= smallClusterThreshold)
            {
                largeClusters.Add(cluster);
            }
            else
            {
                smallClusters.Add(cluster);
            }
        }
        
        debugLog.Add($"大群组: {largeClusters.Count} 个, 小群组: {smallClusters.Count} 个");
        
        // 尝试将小群组合并到最近的大群组
        foreach (var smallCluster in smallClusters)
        {
            if (smallCluster.Count == 0) continue;
            
            var smallCenter = CalculateClusterCenter(smallCluster);
            float minDistance = float.MaxValue;
            List<PointData>? targetCluster = null;
            
            // 找到最近的大群组
            foreach (var largeCluster in largeClusters)
            {
                var largeCenter = CalculateClusterCenter(largeCluster);
                var distance = EuclideanDistance(smallCenter, largeCenter);
                
                if (distance < minDistance && distance <= maxMergeDistance)
                {
                    minDistance = distance;
                    targetCluster = largeCluster;
                }
            }
            
            if (targetCluster != null)
            {
                // 合并到最近的大群组
                targetCluster.AddRange(smallCluster);
                debugLog.Add($"小群组({smallCluster.Count}点)合并到大群组(距离:{minDistance:F1})");
            }
            else
            {
                // 如果没有合适的大群组，尝试与其他小群组合并
                List<PointData>? nearestSmallCluster = null;
                minDistance = float.MaxValue;
                
                foreach (var otherSmallCluster in smallClusters)
                {
                    if (otherSmallCluster == smallCluster) continue;
                    
                    var otherCenter = CalculateClusterCenter(otherSmallCluster);
                    var distance = EuclideanDistance(smallCenter, otherCenter);
                    
                    if (distance < minDistance && distance <= maxMergeDistance)
                    {
                        minDistance = distance;
                        nearestSmallCluster = otherSmallCluster;
                    }
                }
                
                if (nearestSmallCluster != null)
                {
                    nearestSmallCluster.AddRange(smallCluster);
                    smallCluster.Clear(); // 标记为已合并
                    debugLog.Add($"小群组({smallCluster.Count}点)与另一小群组合并");
                }
                else
                {
                    // 无法合并，保持为独立群组
                    largeClusters.Add(smallCluster);
                }
            }
        }
        
        // 第二阶段：确保主要路径连续性
        var finalClusters = largeClusters.Where(c => c.Count > 0).ToList();
        var mainPaths = finalClusters.Where(g => g.Count >= mainPathThreshold).ToList();
        var remainingSmall = finalClusters.Where(g => g.Count < mainPathThreshold).ToList();
        
        debugLog.Add($"第二阶段：{mainPaths.Count} 个主要路径，{remainingSmall.Count} 个小群组");
        
        // 主要路径吸收附近小群组
        foreach (var mainPath in mainPaths)
        {
            var mainCenter = CalculateClusterCenter(mainPath);
            var absorptionRadius = maxMergeDistance * 1.5f; // delta*1.5的吸收半径
            
            var toAbsorb = new List<List<PointData>>();
            foreach (var smallCluster in remainingSmall)
            {
                var smallCenter = CalculateClusterCenter(smallCluster);
                var distance = EuclideanDistance(mainCenter, smallCenter);
                
                if (distance <= absorptionRadius)
                {
                    toAbsorb.Add(smallCluster);
                }
            }
            
            // 执行吸收
            foreach (var cluster in toAbsorb)
            {
                mainPath.AddRange(cluster);
                remainingSmall.Remove(cluster);
                debugLog.Add($"主要路径吸收{cluster.Count}个点的小群组");
            }
        }
        
        // 合并主要路径和剩余的小群组
        var result = new List<List<PointData>>();
        result.AddRange(mainPaths);
        result.AddRange(remainingSmall);
        
        return result;
    }

    /// <summary>
    /// 计算群组中心点
    /// </summary>
    private PointF CalculateClusterCenter(List<PointData> cluster)
    {
        if (cluster.Count == 0) return new PointF(0, 0);
        
        float sumX = 0, sumY = 0;
        foreach (var point in cluster)
        {
            sumX += point.X;
            sumY += point.Y;
        }
        
        return new PointF(sumX / cluster.Count, sumY / cluster.Count);
    }

    /// <summary>
    /// 带方向权重的行优化
    /// </summary>
    private List<PointF> OptimizeRowDirectionWithWeight(List<PointF> rowPoints, HybridIndex spatialIndex, bool reverse, PointF? lastDirection, float directionWeight)
    {
        if (rowPoints.Count <= 1) return rowPoints;
        
        // 基础排序
        var sortedPoints = reverse ? 
            rowPoints.OrderByDescending(p => p.X).ToList() : 
            rowPoints.OrderBy(p => p.X).ToList();
        
        // 如果没有历史方向，使用基础排序
        if (!lastDirection.HasValue || directionWeight <= 0)
        {
            return OptimizeRowWithSpatialIndex(sortedPoints, spatialIndex);
        }
        
        // 使用方向权重进行优化
        return OptimizeRowWithDirectionWeight(sortedPoints, spatialIndex, lastDirection.Value, directionWeight);
    }

    /// <summary>
    /// 带方向权重的行内路径优化
    /// </summary>
    private List<PointF> OptimizeRowWithDirectionWeight(List<PointF> sortedPoints, HybridIndex spatialIndex, PointF preferredDirection, float weight)
    {
        if (sortedPoints.Count <= 2) return sortedPoints;
        
        var optimized = new List<PointF> { sortedPoints[0] };
        var remaining = new HashSet<PointF>(sortedPoints.Skip(1));
        
        var current = sortedPoints[0];
        
        while (remaining.Count > 0)
        {
            // 使用空间索引找到候选点
            var nearestCandidates = spatialIndex.FindNearestPoints(current, Math.Min(5, remaining.Count), null);
            
            PointF? nextPoint = null;
            float bestScore = float.MaxValue;
            
            foreach (var candidate in nearestCandidates)
            {
                if (remaining.Contains(candidate))
                {
                    var distance = EuclideanDistance(current, candidate);
                    
                    // 计算方向一致性
                    var candidateDirection = new PointF(candidate.X - current.X, candidate.Y - current.Y);
                    float dirLength = (float)Math.Sqrt(candidateDirection.X * candidateDirection.X + candidateDirection.Y * candidateDirection.Y);
                    
                    float directionPenalty = 0;
                    if (dirLength > 0.001f)
                    {
                        candidateDirection = new PointF(candidateDirection.X / dirLength, candidateDirection.Y / dirLength);
                        
                        // 计算与期望方向的夹角
                        float dotProduct = candidateDirection.X * preferredDirection.X + candidateDirection.Y * preferredDirection.Y;
                        directionPenalty = (1 - dotProduct) * weight * distance; // 方向权重：0.3
                    }
                    
                    float totalScore = distance + directionPenalty;
                    
                    if (totalScore < bestScore)
                    {
                        bestScore = totalScore;
                        nextPoint = candidate;
                    }
                }
            }
            
            // 如果空间索引没找到，使用最近的剩余点
            if (nextPoint == null)
            {
                nextPoint = remaining.OrderBy(p => EuclideanDistance(current, p)).First();
            }
            
            optimized.Add(nextPoint.Value);
            remaining.Remove(nextPoint.Value);
            current = nextPoint.Value;
        }
        
        return optimized;
    }

    /// <summary>
    /// 去重机制：合并相近点
    /// </summary>
    private List<PointF> DeduplicatePoints(List<PointF> points, float threshold)
    {
        if (points.Count <= 1) return points;
        
        var deduplicated = new List<PointF>();
        
        foreach (var point in points)
        {
            bool shouldMerge = false;
            
            // 检查是否与现有点过于接近
            for (int i = 0; i < deduplicated.Count; i++)
            {
                if (EuclideanDistance(point, deduplicated[i]) < threshold)
                {
                    // 合并相近点（取中点）
                    var merged = new PointF(
                        (point.X + deduplicated[i].X) / 2,
                        (point.Y + deduplicated[i].Y) / 2
                    );
                    deduplicated[i] = merged;
                    shouldMerge = true;
                    break;
                }
            }
            
            if (!shouldMerge)
            {
                deduplicated.Add(point);
            }
        }
        
        return deduplicated;
    }

    #endregion
} 