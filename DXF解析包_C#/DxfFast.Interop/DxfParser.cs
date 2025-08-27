using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using DxfFast.Interop.Native;
using DxfFast.Interop.Types;
using DxfFast.Interop.Exceptions;

namespace DxfFast.Interop
{
    /// <summary>
    /// DXF解析器 - 高性能DXF文件解析和几何归一化
    /// </summary>
    public class DxfParser : IDisposable
    {
        private UIntPtr _parserHandle = UIntPtr.Zero;
        private UIntPtr _drawingHandle = UIntPtr.Zero;
        private UIntPtr _circlesHandle = UIntPtr.Zero;
        private bool _disposed = false;

        /// <summary>
        /// 解析统计信息
        /// </summary>
        public ParseStatistics? Statistics { get; private set; }

        /// <summary>
        /// 归一化后的圆形数据
        /// </summary>
        public IReadOnlyList<NormalizedCircle>? NormalizedCircles { get; private set; }

        /// <summary>
        /// 实体总数
        /// </summary>
        public uint EntityCount { get; private set; }

        /// <summary>
        /// 创建DXF解析器实例
        /// </summary>
        /// <param name="config">解析器配置</param>
        public DxfParser(ParserConfiguration? config = null)
        {
            var nativeConfig = config?.ToNative() ?? ParserConfiguration.Default.ToNative();
            
            _parserHandle = NativeInterop.dxf_parser_create(ref nativeConfig);
            
            if (_parserHandle == UIntPtr.Zero)
            {
                throw new DxfOutOfMemoryException("Failed to create DXF parser");
            }
        }

        /// <summary>
        /// 解析DXF文件
        /// </summary>
        /// <param name="filePath">DXF文件路径</param>
        /// <param name="normalize">是否进行几何归一化</param>
        /// <returns>解析是否成功</returns>
        public bool ParseFile(string filePath, bool normalize = false)
        {
            ThrowIfDisposed();
            
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            // 清理之前的数据
            CleanupPreviousData();

            var errorCode = NativeInterop.dxf_parse_file(_parserHandle, filePath, out _drawingHandle);
            
            if (errorCode != NativeInterop.DxfErrorCode.Success)
            {
                var errorMessage = NativeInterop.GetErrorMessage(errorCode);
                throw DxfException.FromErrorCode(errorCode, $"Failed to parse file '{filePath}': {errorMessage}");
            }

            // 获取解析统计信息
            LoadStatistics();
            
            // 获取实体数量
            var result = NativeInterop.dxf_get_entity_count(_drawingHandle, out uint count);
            if (result == NativeInterop.DxfErrorCode.Success)
            {
                EntityCount = count;
            }

            // 如果需要归一化
            if (normalize)
            {
                NormalizeGeometry();
            }

            return true;
        }

        /// <summary>
        /// 进行几何归一化（将多边形和椭圆转换为圆形）
        /// </summary>
        public void NormalizeGeometry()
        {
            ThrowIfDisposed();
            
            if (_drawingHandle == UIntPtr.Zero)
                throw new InvalidOperationException("No drawing data available. Call ParseFile first.");

            // 清理之前的圆形数据
            if (_circlesHandle != UIntPtr.Zero)
            {
                NativeInterop.dxf_destroy_circles(_circlesHandle);
                _circlesHandle = UIntPtr.Zero;
            }

            var errorCode = NativeInterop.dxf_normalize_geometry(_drawingHandle, out _circlesHandle);
            
            if (errorCode != NativeInterop.DxfErrorCode.Success)
            {
                var errorMessage = NativeInterop.GetErrorMessage(errorCode);
                throw DxfException.FromErrorCode(errorCode, $"Failed to normalize geometry: {errorMessage}");
            }

            // 加载归一化后的圆形数据
            LoadNormalizedCircles();
        }

        /// <summary>
        /// 获取库版本信息
        /// </summary>
        /// <returns>版本字符串</returns>
        public static string GetVersion()
        {
            return NativeInterop.GetVersion();
        }

        /// <summary>
        /// 全局清理（释放所有全局资源）
        /// </summary>
        public static void GlobalCleanup()
        {
            NativeInterop.dxf_global_cleanup();
        }

        private void LoadStatistics()
        {
            if (_parserHandle == UIntPtr.Zero) return;

            var stats = new NativeInterop.CParseStats();
            var result = NativeInterop.dxf_get_parse_stats(_drawingHandle, ref stats);
            if (result == NativeInterop.DxfErrorCode.Success)
            {
                Statistics = ParseStatistics.FromNative(stats);
            }
        }

        private void LoadNormalizedCircles()
        {
            if (_circlesHandle == UIntPtr.Zero)
            {
                NormalizedCircles = new List<NormalizedCircle>();
                return;
            }

            var result = NativeInterop.dxf_get_circle_count(_circlesHandle, out uint count);
            if (result != NativeInterop.DxfErrorCode.Success) 
            {
                NormalizedCircles = new List<NormalizedCircle>();
                return;
            }
            var circles = new List<NormalizedCircle>((int)count);

            var buffer = new NativeInterop.CNormalizedCircle[count];
            var getResult = NativeInterop.dxf_get_circles(_circlesHandle, buffer, count, out uint actualCount);
            if (getResult == NativeInterop.DxfErrorCode.Success)
            {
                for (int i = 0; i < actualCount; i++)
                {
                    circles.Add(NormalizedCircle.FromNative(buffer[i]));
                }
            }

            NormalizedCircles = circles;
        }

        private void CleanupPreviousData()
        {
            if (_drawingHandle != UIntPtr.Zero)
            {
                NativeInterop.dxf_destroy_drawing(_drawingHandle);
                _drawingHandle = UIntPtr.Zero;
            }

            if (_circlesHandle != UIntPtr.Zero)
            {
                NativeInterop.dxf_destroy_circles(_circlesHandle);
                _circlesHandle = UIntPtr.Zero;
            }

            Statistics = null;
            NormalizedCircles = null;
            EntityCount = 0;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DxfParser));
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (_circlesHandle != UIntPtr.Zero)
                {
                    NativeInterop.dxf_destroy_circles(_circlesHandle);
                    _circlesHandle = UIntPtr.Zero;
                }

                if (_drawingHandle != UIntPtr.Zero)
                {
                    NativeInterop.dxf_destroy_drawing(_drawingHandle);
                    _drawingHandle = UIntPtr.Zero;
                }

                if (_parserHandle != UIntPtr.Zero)
                {
                    NativeInterop.dxf_parser_destroy(_parserHandle);
                    _parserHandle = UIntPtr.Zero;
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~DxfParser()
        {
            Dispose(false);
        }
    }
}