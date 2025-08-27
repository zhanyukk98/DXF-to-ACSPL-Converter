using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using DXFtoACSPL.Core.Models;

namespace DXFtoACSPL.Core.Services
{
    /// <summary>
    /// 螺旋填充路径生成器
    /// 使用自定义算法实现螺旋填充，不依赖外部库
    /// </summary>
    public class SpiralFillPathGenerator
    {
        /// <summary>
        /// 螺旋填充配置
        /// </summary>
        public class SpiralFillConfig
        {
            /// <summary>
            /// 螺旋偏移距离（行距）
            /// </summary>
            public float OffsetDistance { get; set; } = 1.0f;

            /// <summary>
            /// 最大偏移次数
            /// </summary>
            public int MaxOffsetCount { get; set; } = 10;

            /// <summary>
            /// 螺旋精度（每圈的点数）- 降低默认值以提高性能
            /// </summary>
            public int SpiralPrecision { get; set; } = 16; // 从32降低到16

            /// <summary>
            /// 引入线长度
            /// </summary>
            public float LeadInLength { get; set; } = 2.0f;

            /// <summary>
            /// 引出线长度
            /// </summary>
            public float LeadOutLength { get; set; } = 2.0f;

            /// <summary>
            /// 是否使用圆弧拟合
            /// </summary>
            public bool UseArcFitting { get; set; } = true;

            /// <summary>
            /// 圆弧拟合容差
            /// </summary>
            public float ArcFittingTolerance { get; set; } = 0.1f;
        }

        /// <summary>
        /// 生成螺旋填充路径 - 对现有的圆形实体进行螺旋排序
        /// </summary>
        public List<PathGenerator.PathElement> GenerateSpiralFillPath(List<CircleEntity> circles, SpiralFillConfig config)
        {
            if (circles == null || circles.Count == 0)
                return new List<PathGenerator.PathElement>();

            Console.WriteLine($"开始螺旋填充路径生成，共 {circles.Count} 个圆形实体");

            var pathElements = new List<PathGenerator.PathElement>();
            
            // 对圆形实体进行螺旋排序
            var spiralOrderedCircles = OrderCirclesInSpiralPattern(circles, config);
            
            // 转换为路径元素
            foreach (var circle in spiralOrderedCircles)
            {
                pathElements.Add(new PathGenerator.PathElement 
                { 
                    Data = circle.Center, 
                    Type = "Point" 
                });
            }
            
            Console.WriteLine($"螺旋填充路径生成完成，共 {pathElements.Count} 个路径点");
            return pathElements;
        }
        
        /// <summary>
        /// 为单个圆生成真正的螺旋填充路径 - 连接现有的加工点
        /// </summary>
        private List<PathGenerator.PathElement> GenerateSpiralForCircle(CircleEntity circle, SpiralFillConfig config)
        {
            var center = circle.Center;
            var radius = circle.Radius;
            
            Console.WriteLine($"圆形 {circle.Index}: 半径={radius:F2}，生成螺旋连接路径");
            
            var spiralPath = new List<PathGenerator.PathElement>();
            
            // 生成螺旋连接路径 - 从外到内连接现有的加工点
            var spiralPoints = GenerateSpiralConnectionPath(center, radius, config.OffsetDistance, config.SpiralPrecision);
            
            // 转换为PathElement
            foreach (var point in spiralPoints)
            {
                spiralPath.Add(new PathGenerator.PathElement 
                { 
                    Data = point, 
                    Type = "Point" 
                });
            }
            
            return spiralPath;
        }
        
        /// <summary>
        /// 生成螺旋连接路径 - 从外到内螺旋连接现有的加工点
        /// </summary>
        private List<PointF> GenerateSpiralConnectionPath(PointF center, float radius, float offsetDistance, int precision)
        {
            var spiralPoints = new List<PointF>();
            
            // 计算螺旋圈数（基于半径和偏移距离）
            int spiralTurns = Math.Max(1, (int)(radius / offsetDistance));
            
            // 生成螺旋路径点 - 这些是连接点，不是填充点
            for (int turn = 0; turn <= spiralTurns; turn++)
            {
                // 当前圈的半径
                float currentRadius = radius - (turn * offsetDistance);
                
                // 如果半径太小，停止
                if (currentRadius <= offsetDistance / 2)
                    break;
                
                // 在当前圈上生成连接点
                int pointsInTurn = Math.Max(4, precision / 4); // 每圈的点数
                for (int i = 0; i < pointsInTurn; i++)
                {
                    float angle = (2 * (float)Math.PI * i) / pointsInTurn;
                    
                    // 计算螺旋点坐标
                    float x = center.X + currentRadius * (float)Math.Cos(angle);
                    float y = center.Y + currentRadius * (float)Math.Sin(angle);
                    
                    spiralPoints.Add(new PointF(x, y));
                }
            }
            
            Console.WriteLine($"生成螺旋连接路径: {spiralPoints.Count} 个连接点，半径从 {radius:F2} 到 {offsetDistance:F2}");
            return spiralPoints;
        }

        /// <summary>
        /// 生成圆形路径点
        /// </summary>
        private List<PointF> GenerateCirclePoints(PointF center, float radius, int segments)
        {
            var points = new List<PointF>();
            var angleStep = 2 * Math.PI / segments;
            
            for (int i = 0; i <= segments; i++)
            {
                var angle = i * angleStep;
                var x = center.X + radius * (float)Math.Cos(angle);
                var y = center.Y + radius * (float)Math.Sin(angle);
                points.Add(new PointF(x, y));
            }
            
            return points;
        }

        /// <summary>
        /// 添加引入引出线
        /// </summary>
        private void AddLeadInLeadOut(List<PathGenerator.PathElement> pathElements, SpiralFillConfig config)
        {
            if (pathElements.Count == 0)
                return;
            
            var pathPoints = pathElements.Where(p => p.Type == "Point").ToList();
            if (pathPoints.Count == 0)
                return;
            
            var firstPoint = (PointF)pathPoints.First().Data;
            var lastPoint = (PointF)pathPoints.Last().Data;
            
            // 添加引入线
            var leadInStart = new PointF(
                firstPoint.X - config.LeadInLength,
                firstPoint.Y
            );
            
            var leadInElements = new List<PathGenerator.PathElement>
            {
                new PathGenerator.PathElement { Type = "Point", Data = leadInStart },
                new PathGenerator.PathElement { Type = "Point", Data = firstPoint }
            };
            
            // 添加引出线
            var leadOutEnd = new PointF(
                lastPoint.X + config.LeadOutLength,
                lastPoint.Y
            );
            
            var leadOutElements = new List<PathGenerator.PathElement>
            {
                new PathGenerator.PathElement { Type = "Point", Data = lastPoint },
                new PathGenerator.PathElement { Type = "Point", Data = leadOutEnd }
            };
            
            // 插入引入线和引出线
            pathElements.InsertRange(0, leadInElements);
            pathElements.AddRange(leadOutElements);
        }

        /// <summary>
        /// 回退路径生成（如果螺旋填充失败）
        /// </summary>
        private List<PathGenerator.PathElement> GenerateFallbackPath(List<CircleEntity> circles)
        {
            var pathElements = new List<PathGenerator.PathElement>();
            
            foreach (var circle in circles)
            {
                // 简单的圆形路径（逆时针）
                var segments = 32;
                var angleStep = 2 * Math.PI / segments;
                
                for (int i = 0; i <= segments; i++)
                {
                    var angle = i * angleStep;
                    var x = circle.Center.X + circle.Radius * (float)Math.Cos(angle);
                    var y = circle.Center.Y + circle.Radius * (float)Math.Sin(angle);
                    
                    pathElements.Add(new PathGenerator.PathElement 
                    { 
                        Type = "Point", 
                        Data = new PointF(x, y) 
                    });
                }
                
                // 添加路径结束标记
                pathElements.Add(new PathGenerator.PathElement { Type = "Marker", Data = new PointF(-999999, -999999) });
            }
            
            return pathElements;
        }

        /// <summary>
        /// 对圆形实体进行螺旋排序 - 从外到内螺旋排列
        /// </summary>
        private List<CircleEntity> OrderCirclesInSpiralPattern(List<CircleEntity> circles, SpiralFillConfig config)
        {
            if (circles.Count <= 1)
                return circles;
            
            // 计算所有圆的中心点
            PointF center = CalculateCenterPoint(circles);
            
            // 按距离中心点的距离分组（螺旋圈）
            var spiralGroups = GroupCirclesBySpiralRings(circles, center, config.OffsetDistance);
            
            // 对每个螺旋圈内的圆进行排序
            var orderedCircles = new List<CircleEntity>();
            foreach (var group in spiralGroups)
            {
                var orderedGroup = OrderCirclesInRing(group, center);
                orderedCircles.AddRange(orderedGroup);
            }
            
            return orderedCircles;
        }
        
        /// <summary>
        /// 计算所有圆的中心点
        /// </summary>
        private PointF CalculateCenterPoint(List<CircleEntity> circles)
        {
            float sumX = 0, sumY = 0;
            foreach (var circle in circles)
            {
                sumX += circle.Center.X;
                sumY += circle.Center.Y;
            }
            
            return new PointF(sumX / circles.Count, sumY / circles.Count);
        }
        
        /// <summary>
        /// 按螺旋圈分组圆形实体
        /// </summary>
        private List<List<CircleEntity>> GroupCirclesBySpiralRings(List<CircleEntity> circles, PointF center, float offsetDistance)
        {
            var groups = new List<List<CircleEntity>>();
            var remainingCircles = new List<CircleEntity>(circles);
            
            while (remainingCircles.Count > 0)
            {
                // 找到当前圈（距离中心最远的圆）
                var currentRing = new List<CircleEntity>();
                float maxDistance = 0;
                
                foreach (var circle in remainingCircles)
                {
                    float distance = CalculateDistance(center, circle.Center);
                    if (distance > maxDistance)
                        maxDistance = distance;
                }
                
                // 收集当前圈的所有圆（在最大距离附近的圆）
                var circlesToRemove = new List<CircleEntity>();
                foreach (var circle in remainingCircles)
                {
                    float distance = CalculateDistance(center, circle.Center);
                    if (Math.Abs(distance - maxDistance) <= offsetDistance)
                    {
                        currentRing.Add(circle);
                        circlesToRemove.Add(circle);
                    }
                }
                
                // 从剩余列表中移除已处理的圆
                foreach (var circle in circlesToRemove)
                {
                    remainingCircles.Remove(circle);
                }
                
                if (currentRing.Count > 0)
                    groups.Add(currentRing);
            }
            
            return groups;
        }
        
        /// <summary>
        /// 对单个螺旋圈内的圆进行排序
        /// </summary>
        private List<CircleEntity> OrderCirclesInRing(List<CircleEntity> circles, PointF center)
        {
            if (circles.Count <= 1)
                return circles;
            
            // 按角度排序（从0度开始，逆时针）
            return circles.OrderBy(circle => 
            {
                float angle = (float)Math.Atan2(circle.Center.Y - center.Y, circle.Center.X - center.X);
                // 转换为0-2π范围
                if (angle < 0) angle += 2 * (float)Math.PI;
                return angle;
            }).ToList();
        }
        
        /// <summary>
        /// 计算两点间距离
        /// </summary>
        private float CalculateDistance(PointF p1, PointF p2)
        {
            float dx = p1.X - p2.X;
            float dy = p1.Y - p2.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
} 