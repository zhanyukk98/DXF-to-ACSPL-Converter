using System;
using System.Runtime.InteropServices;

namespace DxfFast.Interop.Native
{
    /// <summary>
    /// 原生DLL互操作声明
    /// </summary>
    public static class NativeInterop
    {
        private const string DllName = "dxf_fast_ffi";

        #region 错误码枚举
        
        /// <summary>
        /// DXF错误码
        /// </summary>
        public enum DxfErrorCode : int
        {
            Success = 0,
            IoError = 1,
            InvalidFormat = 2,
            ParseError = 3,
            UnsupportedVersion = 4,
            OutOfMemory = 5,
            UnsupportedEntity = 6,
            ConversionError = 7,
            CorruptedFile = 8,
            Timeout = 9,
            InvalidHandle = 10,
            NullPointer = 11,
        }

        #endregion

        #region 数据结构

        /// <summary>
        /// 3D点坐标
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct CPoint3D
        {
            public double X;
            public double Y;
            public double Z;

            public CPoint3D(double x, double y, double z)
            {
                X = x;
                Y = y;
                Z = z;
            }
        }

        /// <summary>
        /// 颜色结构
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct CColor
        {
            public uint R;
            public uint G;
            public uint B;
            public uint A;

            public CColor(uint r, uint g, uint b, uint a = 255)
            {
                R = r;
                G = g;
                B = b;
                A = a;
            }
        }

        /// <summary>
        /// 圆形类型
        /// </summary>
        public enum CCircleKind : int
        {
            Circle = 0,
            Ellipse = 1,
            Polyline = 2,
        }

        /// <summary>
        /// 标准化圆形
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct CNormalizedCircle
        {
            public CPoint3D Center;
            public double Radius;
            public CCircleKind Kind;
            public uint OriginalIndex;
        }

        /// <summary>
        /// 解析统计信息
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct CParseStats
        {
            public uint TotalEntities;
            public uint ParseTimeMs;
            public uint MemoryUsageBytes;
            public uint SkippedEntities;
        }

        /// <summary>
        /// 解析器配置
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct CParserConfig
        {
            public int ParallelParsing;
            public uint WorkerThreads;
            public uint MemoryLimitMb;
            public int SkipUnknownEntities;
            public int StrictMode;
            public uint ChunkSize;
            public int UseMemoryMapping;
            public int EnableCircleOptimization;
            public int EnableStringPool;
            public int EnableCache;
        }

        #endregion

        #region DLL导入声明

        /// <summary>
        /// 创建DXF解析器
        /// </summary>
        /// <param name="config">解析器配置</param>
        /// <returns>解析器句柄</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr dxf_parser_create(ref CParserConfig config);
        
        /// <summary>
        /// 创建DXF解析器（使用默认配置）
        /// </summary>
        /// <returns>解析器句柄</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr dxf_parser_create(IntPtr config = default);

        /// <summary>
        /// 销毁DXF解析器实例
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern DxfErrorCode dxf_parser_destroy(UIntPtr handle);

        /// <summary>
        /// 解析DXF文件
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern DxfErrorCode dxf_parse_file(
            UIntPtr handle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string filePath,
            out UIntPtr drawingHandle);

        /// <summary>
        /// 获取解析统计信息
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern DxfErrorCode dxf_get_parse_stats(
            UIntPtr drawingHandle,
            ref CParseStats stats);

        /// <summary>
        /// 获取实体数量
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern DxfErrorCode dxf_get_entity_count(
            UIntPtr drawingHandle,
            out uint count);

        /// <summary>
        /// 几何归一化
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern DxfErrorCode dxf_normalize_geometry(
            UIntPtr drawingHandle,
            out UIntPtr circlesHandle);

        /// <summary>
        /// 获取标准化圆形数量
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern DxfErrorCode dxf_get_circle_count(
            UIntPtr circlesHandle,
            out uint count);

        /// <summary>
        /// 获取标准化圆形数据
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern DxfErrorCode dxf_get_circles(
            UIntPtr circlesHandle,
            [Out] CNormalizedCircle[] buffer,
            uint bufferSize,
            out uint actualCount);

        /// <summary>
        /// 销毁图形实例
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern DxfErrorCode dxf_destroy_drawing(UIntPtr drawingHandle);

        /// <summary>
        /// 销毁圆形集合实例
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern DxfErrorCode dxf_destroy_circles(UIntPtr circlesHandle);

        /// <summary>
        /// 获取错误信息字符串
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr dxf_get_error_message(DxfErrorCode errorCode);

        /// <summary>
        /// 获取库版本信息
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr dxf_get_version();

        /// <summary>
        /// 清理所有资源
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern DxfErrorCode dxf_global_cleanup();

        #endregion

        #region 辅助方法

        /// <summary>
        /// 将IntPtr转换为字符串
        /// </summary>
        public static string? PtrToString(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                return null;
            
            return Marshal.PtrToStringAnsi(ptr);
        }

        /// <summary>
        /// 获取错误信息
        /// </summary>
        public static string GetErrorMessage(DxfErrorCode errorCode)
        {
            var ptr = dxf_get_error_message(errorCode);
            return PtrToString(ptr) ?? "Unknown error";
        }

        /// <summary>
        /// 获取版本信息
        /// </summary>
        public static string GetVersion()
        {
            var ptr = dxf_get_version();
            return PtrToString(ptr) ?? "Unknown version";
        }

        #endregion
    }
}