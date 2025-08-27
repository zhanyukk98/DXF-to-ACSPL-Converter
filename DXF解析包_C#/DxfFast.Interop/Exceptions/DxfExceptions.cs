using System;
using DxfFast.Interop.Native;

namespace DxfFast.Interop.Exceptions
{
    /// <summary>
    /// DXF解析异常基类
    /// </summary>
    public abstract class DxfException : Exception
    {
        /// <summary>
        /// 错误码
        /// </summary>
        public NativeInterop.DxfErrorCode ErrorCode { get; }

        protected DxfException(NativeInterop.DxfErrorCode errorCode, string message) 
            : base(message)
        {
            ErrorCode = errorCode;
        }

        protected DxfException(NativeInterop.DxfErrorCode errorCode, string message, Exception innerException) 
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }

        /// <summary>
        /// 从错误码创建相应的异常
        /// </summary>
        public static DxfException FromErrorCode(NativeInterop.DxfErrorCode errorCode, string? customMessage = null)
        {
            var message = customMessage ?? NativeInterop.GetErrorMessage(errorCode);
            
            return errorCode switch
            {
                NativeInterop.DxfErrorCode.Success => throw new ArgumentException("Cannot create exception for success code"),
                NativeInterop.DxfErrorCode.IoError => new DxfIoException(message),
                NativeInterop.DxfErrorCode.InvalidFormat => new DxfInvalidFormatException(message),
                NativeInterop.DxfErrorCode.ParseError => new DxfParseException(message),
                NativeInterop.DxfErrorCode.UnsupportedVersion => new DxfUnsupportedVersionException(message),
                NativeInterop.DxfErrorCode.OutOfMemory => new DxfOutOfMemoryException(message),
                NativeInterop.DxfErrorCode.UnsupportedEntity => new DxfUnsupportedEntityException(message),
                NativeInterop.DxfErrorCode.ConversionError => new DxfConversionException(message),
                NativeInterop.DxfErrorCode.CorruptedFile => new DxfCorruptedFileException(message),
                NativeInterop.DxfErrorCode.Timeout => new DxfTimeoutException(message),
                NativeInterop.DxfErrorCode.InvalidHandle => new DxfInvalidHandleException(message),
                NativeInterop.DxfErrorCode.NullPointer => new DxfNullPointerException(message),
                _ => new DxfUnknownException(errorCode, message)
            };
        }
    }

    /// <summary>
    /// IO错误异常
    /// </summary>
    public class DxfIoException : DxfException
    {
        public DxfIoException(string message) 
            : base(NativeInterop.DxfErrorCode.IoError, message) { }
        
        public DxfIoException(string message, Exception innerException) 
            : base(NativeInterop.DxfErrorCode.IoError, message, innerException) { }
    }

    /// <summary>
    /// 无效格式异常
    /// </summary>
    public class DxfInvalidFormatException : DxfException
    {
        public DxfInvalidFormatException(string message) 
            : base(NativeInterop.DxfErrorCode.InvalidFormat, message) { }
        
        public DxfInvalidFormatException(string message, Exception innerException) 
            : base(NativeInterop.DxfErrorCode.InvalidFormat, message, innerException) { }
    }

    /// <summary>
    /// 解析错误异常
    /// </summary>
    public class DxfParseException : DxfException
    {
        public DxfParseException(string message) 
            : base(NativeInterop.DxfErrorCode.ParseError, message) { }
        
        public DxfParseException(string message, Exception innerException) 
            : base(NativeInterop.DxfErrorCode.ParseError, message, innerException) { }
    }

    /// <summary>
    /// 不支持的版本异常
    /// </summary>
    public class DxfUnsupportedVersionException : DxfException
    {
        public DxfUnsupportedVersionException(string message) 
            : base(NativeInterop.DxfErrorCode.UnsupportedVersion, message) { }
        
        public DxfUnsupportedVersionException(string message, Exception innerException) 
            : base(NativeInterop.DxfErrorCode.UnsupportedVersion, message, innerException) { }
    }

    /// <summary>
    /// 内存不足异常
    /// </summary>
    public class DxfOutOfMemoryException : DxfException
    {
        public DxfOutOfMemoryException(string message) 
            : base(NativeInterop.DxfErrorCode.OutOfMemory, message) { }
        
        public DxfOutOfMemoryException(string message, Exception innerException) 
            : base(NativeInterop.DxfErrorCode.OutOfMemory, message, innerException) { }
    }

    /// <summary>
    /// 不支持的实体异常
    /// </summary>
    public class DxfUnsupportedEntityException : DxfException
    {
        public DxfUnsupportedEntityException(string message) 
            : base(NativeInterop.DxfErrorCode.UnsupportedEntity, message) { }
        
        public DxfUnsupportedEntityException(string message, Exception innerException) 
            : base(NativeInterop.DxfErrorCode.UnsupportedEntity, message, innerException) { }
    }

    /// <summary>
    /// 转换错误异常
    /// </summary>
    public class DxfConversionException : DxfException
    {
        public DxfConversionException(string message) 
            : base(NativeInterop.DxfErrorCode.ConversionError, message) { }
        
        public DxfConversionException(string message, Exception innerException) 
            : base(NativeInterop.DxfErrorCode.ConversionError, message, innerException) { }
    }

    /// <summary>
    /// 文件损坏异常
    /// </summary>
    public class DxfCorruptedFileException : DxfException
    {
        public DxfCorruptedFileException(string message) 
            : base(NativeInterop.DxfErrorCode.CorruptedFile, message) { }
        
        public DxfCorruptedFileException(string message, Exception innerException) 
            : base(NativeInterop.DxfErrorCode.CorruptedFile, message, innerException) { }
    }

    /// <summary>
    /// 超时异常
    /// </summary>
    public class DxfTimeoutException : DxfException
    {
        public DxfTimeoutException(string message) 
            : base(NativeInterop.DxfErrorCode.Timeout, message) { }
        
        public DxfTimeoutException(string message, Exception innerException) 
            : base(NativeInterop.DxfErrorCode.Timeout, message, innerException) { }
    }

    /// <summary>
    /// 无效句柄异常
    /// </summary>
    public class DxfInvalidHandleException : DxfException
    {
        public DxfInvalidHandleException(string message) 
            : base(NativeInterop.DxfErrorCode.InvalidHandle, message) { }
        
        public DxfInvalidHandleException(string message, Exception innerException) 
            : base(NativeInterop.DxfErrorCode.InvalidHandle, message, innerException) { }
    }

    /// <summary>
    /// 空指针异常
    /// </summary>
    public class DxfNullPointerException : DxfException
    {
        public DxfNullPointerException(string message) 
            : base(NativeInterop.DxfErrorCode.NullPointer, message) { }
        
        public DxfNullPointerException(string message, Exception innerException) 
            : base(NativeInterop.DxfErrorCode.NullPointer, message, innerException) { }
    }

    /// <summary>
    /// 未知错误异常
    /// </summary>
    public class DxfUnknownException : DxfException
    {
        public DxfUnknownException(NativeInterop.DxfErrorCode errorCode, string message) 
            : base(errorCode, message) { }
        
        public DxfUnknownException(NativeInterop.DxfErrorCode errorCode, string message, Exception innerException) 
            : base(errorCode, message, innerException) { }
    }
}