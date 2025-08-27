using System;
using System.Drawing;
using DxfFast.Interop.Native;

namespace DxfFast.Interop.Types
{
    /// <summary>
    /// 3D点坐标
    /// </summary>
    public struct Point3D : IEquatable<Point3D>
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Point3D(double x, double y, double z = 0.0)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// 原点
        /// </summary>
        public static Point3D Origin => new(0, 0, 0);

        /// <summary>
        /// 转换为2D点
        /// </summary>
        public PointF ToPoint2D() => new((float)X, (float)Y);

        /// <summary>
        /// 计算到另一点的距离
        /// </summary>
        public double DistanceTo(Point3D other)
        {
            var dx = X - other.X;
            var dy = Y - other.Y;
            var dz = Z - other.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>
        /// 从原生结构转换
        /// </summary>
        internal static Point3D FromNative(NativeInterop.CPoint3D native)
        {
            return new Point3D(native.X, native.Y, native.Z);
        }

        /// <summary>
        /// 转换为原生结构
        /// </summary>
        internal NativeInterop.CPoint3D ToNative()
        {
            return new NativeInterop.CPoint3D(X, Y, Z);
        }

        public bool Equals(Point3D other)
        {
            const double tolerance = 1e-10;
            return Math.Abs(X - other.X) < tolerance &&
                   Math.Abs(Y - other.Y) < tolerance &&
                   Math.Abs(Z - other.Z) < tolerance;
        }

        public override bool Equals(object? obj)
        {
            return obj is Point3D other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z);
        }

        public static bool operator ==(Point3D left, Point3D right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Point3D left, Point3D right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"({X:F3}, {Y:F3}, {Z:F3})";
        }
    }

    /// <summary>
    /// 圆形类型枚举
    /// </summary>
    public enum CircleKind
    {
        /// <summary>
        /// 标准圆形
        /// </summary>
        Circle,
        
        /// <summary>
        /// 椭圆转换的等效圆
        /// </summary>
        Ellipse,
        
        /// <summary>
        /// 多段线拟合的圆
        /// </summary>
        Polyline
    }

    /// <summary>
    /// 标准化圆形
    /// </summary>
    public class NormalizedCircle
    {
        /// <summary>
        /// 圆心坐标
        /// </summary>
        public Point3D Center { get; set; }

        /// <summary>
        /// 半径
        /// </summary>
        public double Radius { get; set; }

        /// <summary>
        /// 圆形类型
        /// </summary>
        public CircleKind Kind { get; set; }

        /// <summary>
        /// 原始实体索引
        /// </summary>
        public uint OriginalIndex { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public NormalizedCircle(Point3D center, double radius, CircleKind kind, uint originalIndex = 0)
        {
            Center = center;
            Radius = radius;
            Kind = kind;
            OriginalIndex = originalIndex;
        }

        /// <summary>
        /// 计算圆的面积
        /// </summary>
        public double Area => Math.PI * Radius * Radius;

        /// <summary>
        /// 计算圆的周长
        /// </summary>
        public double Circumference => 2 * Math.PI * Radius;

        /// <summary>
        /// 检查点是否在圆内
        /// </summary>
        public bool ContainsPoint(Point3D point, double tolerance = 1e-6)
        {
            var distance = Center.DistanceTo(point);
            return distance <= Radius + tolerance;
        }

        /// <summary>
        /// 检查点是否在圆周上
        /// </summary>
        public bool IsPointOnCircumference(Point3D point, double tolerance = 1e-6)
        {
            var distance = Center.DistanceTo(point);
            return Math.Abs(distance - Radius) <= tolerance;
        }

        /// <summary>
        /// 从原生结构转换
        /// </summary>
        internal static NormalizedCircle FromNative(NativeInterop.CNormalizedCircle native)
        {
            var kind = native.Kind switch
            {
                NativeInterop.CCircleKind.Circle => CircleKind.Circle,
                NativeInterop.CCircleKind.Ellipse => CircleKind.Ellipse,
                NativeInterop.CCircleKind.Polyline => CircleKind.Polyline,
                _ => CircleKind.Circle
            };

            return new NormalizedCircle(
                Point3D.FromNative(native.Center),
                native.Radius,
                kind,
                native.OriginalIndex
            );
        }

        public override string ToString()
        {
            return $"{Kind} Circle: Center={Center}, Radius={Radius:F3}, Index={OriginalIndex}";
        }
    }

    /// <summary>
    /// 解析统计信息
    /// </summary>
    public class ParseStatistics
    {
        /// <summary>
        /// 总实体数量
        /// </summary>
        public uint TotalEntities { get; set; }

        /// <summary>
        /// 解析时间（毫秒）
        /// </summary>
        public uint ParseTimeMs { get; set; }

        /// <summary>
        /// 内存使用量（字节）
        /// </summary>
        public uint MemoryUsageBytes { get; set; }

        /// <summary>
        /// 跳过的实体数量
        /// </summary>
        public uint SkippedEntities { get; set; }

        /// <summary>
        /// 解析时间（秒）
        /// </summary>
        public double ParseTimeSeconds => ParseTimeMs / 1000.0;

        /// <summary>
        /// 内存使用量（MB）
        /// </summary>
        public double MemoryUsageMB => MemoryUsageBytes / (1024.0 * 1024.0);

        /// <summary>
        /// 从原生结构转换
        /// </summary>
        internal static ParseStatistics FromNative(NativeInterop.CParseStats native)
        {
            return new ParseStatistics
            {
                TotalEntities = native.TotalEntities,
                ParseTimeMs = native.ParseTimeMs,
                MemoryUsageBytes = native.MemoryUsageBytes,
                SkippedEntities = native.SkippedEntities
            };
        }

        public override string ToString()
        {
            return $"Entities: {TotalEntities}, Time: {ParseTimeSeconds:F2}s, Memory: {MemoryUsageMB:F2}MB, Skipped: {SkippedEntities}";
        }
    }

    /// <summary>
    /// 解析器配置
    /// </summary>
    public class ParserConfiguration
    {
        /// <summary>
        /// 是否启用并行解析
        /// </summary>
        public bool ParallelParsing { get; set; } = true;

        /// <summary>
        /// 工作线程数量（0表示自动检测）
        /// </summary>
        public uint WorkerThreads { get; set; } = 0;

        /// <summary>
        /// 内存限制（MB，0表示无限制）
        /// </summary>
        public uint MemoryLimitMb { get; set; } = 0;

        /// <summary>
        /// 是否跳过未知实体
        /// </summary>
        public bool SkipUnknownEntities { get; set; } = true;

        /// <summary>
        /// 严格模式
        /// </summary>
        public bool StrictMode { get; set; } = false;

        /// <summary>
        /// 块大小
        /// </summary>
        public uint ChunkSize { get; set; } = 8192;

        /// <summary>
        /// 是否使用内存映射
        /// </summary>
        public bool UseMemoryMapping { get; set; } = true;

        /// <summary>
        /// 是否启用圆形优化
        /// </summary>
        public bool EnableCircleOptimization { get; set; } = true;

        /// <summary>
        /// 是否启用字符串池
        /// </summary>
        public bool EnableStringPool { get; set; } = true;

        /// <summary>
        /// 是否启用缓存
        /// </summary>
        public bool EnableCache { get; set; } = true;

        /// <summary>
        /// 创建默认配置
        /// </summary>
        public static ParserConfiguration Default => new();

        /// <summary>
        /// 创建快速模式配置
        /// </summary>
        public static ParserConfiguration FastMode => new()
        {
            ParallelParsing = true,
            WorkerThreads = 0, // 自动检测
            SkipUnknownEntities = true,
            StrictMode = false,
            UseMemoryMapping = true,
            EnableCircleOptimization = true,
            EnableStringPool = true,
            EnableCache = true
        };

        /// <summary>
        /// 创建高性能配置
        /// </summary>
        public static ParserConfiguration HighPerformance => new()
        {
            ParallelParsing = true,
            WorkerThreads = 0, // 自动检测CPU核心数
            MemoryLimitMb = 0, // 无内存限制
            SkipUnknownEntities = true,
            StrictMode = false,
            ChunkSize = 16384, // 更大的块大小
            UseMemoryMapping = true,
            EnableCircleOptimization = true,
            EnableStringPool = true,
            EnableCache = true
        };

        /// <summary>
        /// 创建低内存配置
        /// </summary>
        public static ParserConfiguration LowMemory => new()
        {
            ParallelParsing = false, // 单线程减少内存开销
            WorkerThreads = 1,
            MemoryLimitMb = 256, // 限制内存使用
            SkipUnknownEntities = true,
            StrictMode = false,
            ChunkSize = 2048, // 较小的块大小
            UseMemoryMapping = false, // 不使用内存映射
            EnableCircleOptimization = false,
            EnableStringPool = false,
            EnableCache = false
        };

        /// <summary>
        /// 创建严格模式配置
        /// </summary>
        public static ParserConfiguration Strict => new()
        {
            ParallelParsing = false,
            WorkerThreads = 1,
            MemoryLimitMb = 0,
            SkipUnknownEntities = false, // 不跳过任何实体
            StrictMode = true, // 严格解析模式
            ChunkSize = 4096,
            UseMemoryMapping = false,
            EnableCircleOptimization = false,
            EnableStringPool = false,
            EnableCache = false
        };

        /// <summary>
        /// 转换为原生结构
        /// </summary>
        internal NativeInterop.CParserConfig ToNative()
        {
            return new NativeInterop.CParserConfig
            {
                ParallelParsing = ParallelParsing ? 1 : 0,
                WorkerThreads = WorkerThreads,
                MemoryLimitMb = MemoryLimitMb,
                SkipUnknownEntities = SkipUnknownEntities ? 1 : 0,
                StrictMode = StrictMode ? 1 : 0,
                ChunkSize = ChunkSize,
                UseMemoryMapping = UseMemoryMapping ? 1 : 0,
                EnableCircleOptimization = EnableCircleOptimization ? 1 : 0,
                EnableStringPool = EnableStringPool ? 1 : 0,
                EnableCache = EnableCache ? 1 : 0
            };
        }
    }
}