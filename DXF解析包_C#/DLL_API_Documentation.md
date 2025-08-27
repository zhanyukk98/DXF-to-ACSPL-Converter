# DxfFast DLL API 完整文档

## 概述

DxfFast是一个高性能的DXF文件解析和几何归一化库，提供了Rust核心引擎和C# .NET互操作接口。本文档详细介绍了DLL的所有API、使用方法和最佳实践。

## 性能特点

- **高性能解析**: 最高可达18,377实体/秒的解析速度
- **大文件支持**: 成功处理4.95GB的DXF文件集合
- **多配置模式**: 支持默认、高性能、低内存、严格模式等配置
- **几何归一化**: 自动将复杂几何图形转换为标准化圆形
- **内存安全**: 完善的内存管理和错误处理机制
- **跨平台支持**: 支持Windows、Linux、macOS

## 测试结果

基于8个DXF文件的综合测试结果：

| 配置模式 | 成功率 | 解析速度 | 吞吐量 | 总耗时 |
|---------|--------|----------|--------|--------|
| 默认配置 | 100% | 13,206 实体/秒 | 121.90 MB/秒 | 41.5秒 |
| 高性能配置 | 100% | 18,377 实体/秒 | 169.63 MB/秒 | 29.9秒 |
| 低内存配置 | 100% | 11,654 实体/秒 | 107.58 MB/秒 | 47.1秒 |
| 严格模式 | 100% | 12,523 实体/秒 | 115.60 MB/秒 | 43.8秒 |

**推荐配置**: 高性能配置，提供最佳的解析速度。

## API 参考

### 核心数据结构

#### DxfErrorCode 枚举

```csharp
public enum DxfErrorCode : int
{
    Success = 0,              // 成功
    IoError = 1,              // IO错误
    InvalidFormat = 2,        // 无效格式
    ParseError = 3,           // 解析错误
    UnsupportedVersion = 4,   // 不支持的版本
    OutOfMemory = 5,          // 内存不足
    UnsupportedEntity = 6,    // 不支持的实体
    ConversionError = 7,      // 转换错误
    CorruptedFile = 8,        // 文件损坏
    Timeout = 9,              // 超时
    InvalidHandle = 10,       // 无效句柄
    NullPointer = 11,         // 空指针
}
```

#### ParserConfiguration 类

```csharp
public class ParserConfiguration
{
    public bool ParallelParsing { get; set; } = true;           // 并行解析
    public uint WorkerThreads { get; set; } = 0;               // 工作线程数(0=自动)
    public uint MemoryLimitMb { get; set; } = 1024;            // 内存限制(MB)
    public bool SkipUnknownEntities { get; set; } = false;     // 跳过未知实体
    public bool StrictMode { get; set; } = false;              // 严格模式
    public uint ChunkSize { get; set; } = 8192;                // 块大小
    public bool UseMemoryMapping { get; set; } = true;         // 内存映射
    public bool EnableCircleOptimization { get; set; } = true; // 圆形优化
    public bool EnableStringPool { get; set; } = true;         // 字符串池
    public bool EnableCache { get; set; } = true;              // 缓存

    // 预定义配置
    public static ParserConfiguration Default { get; }         // 默认配置
    public static ParserConfiguration HighPerformance { get; } // 高性能配置
    public static ParserConfiguration LowMemory { get; }       // 低内存配置
    public static ParserConfiguration StrictMode { get; }      // 严格模式配置
}
```

#### ParseStatistics 类

```csharp
public class ParseStatistics
{
    public uint ParseTimeMs { get; }        // 解析时间(毫秒)
    public uint EntityCount { get; }        // 实体数量
    public uint MemoryUsedBytes { get; }    // 内存使用(字节)
    public uint FileSize { get; }           // 文件大小
}
```

#### NormalizedCircle 结构

```csharp
public struct NormalizedCircle
{
    public Point3D Center { get; }          // 圆心
    public double Radius { get; }           // 半径
    public CircleKind Kind { get; }         // 圆形类型
}

public struct Point3D
{
    public double X { get; }
    public double Y { get; }
    public double Z { get; }
}

public enum CircleKind
{
    Perfect = 0,    // 完美圆形
    Fitted = 1,     // 拟合圆形
    Approximate = 2 // 近似圆形
}
```

### 主要API类

#### DxfParser 类

```csharp
public class DxfParser : IDisposable
{
    // 构造函数
    public DxfParser(ParserConfiguration config = null)
    
    // 属性
    public uint EntityCount { get; }                    // 实体数量
    public ParseStatistics Statistics { get; }          // 解析统计
    public NormalizedCircle[] NormalizedCircles { get; } // 标准化圆形
    
    // 方法
    public bool ParseFile(string filePath, bool normalize = false)
    public void NormalizeGeometry()
    public void Dispose()
}
```

### 异常类型

```csharp
// 基础异常类
public abstract class DxfException : Exception
{
    public DxfErrorCode ErrorCode { get; }
}

// 具体异常类型
public class DxfIoException : DxfException           // IO错误
public class DxfInvalidFormatException : DxfException // 无效格式
public class DxfParseException : DxfException         // 解析错误
public class DxfUnsupportedVersionException : DxfException // 不支持版本
public class DxfOutOfMemoryException : DxfException  // 内存不足
public class DxfUnsupportedEntityException : DxfException // 不支持实体
public class DxfConversionException : DxfException   // 转换错误
public class DxfCorruptedFileException : DxfException // 文件损坏
public class DxfTimeoutException : DxfException      // 超时
public class DxfInvalidHandleException : DxfException // 无效句柄
public class DxfNullPointerException : DxfException  // 空指针
```

## 使用示例

### 基本使用

```csharp
using DxfFast.Interop;
using DxfFast.Interop.Types;

// 基本解析
try
{
    using var parser = new DxfParser();
    bool success = parser.ParseFile(@"C:\path\to\file.dxf");
    
    if (success)
    {
        Console.WriteLine($"解析成功！实体数量: {parser.EntityCount}");
        Console.WriteLine($"解析时间: {parser.Statistics?.ParseTimeMs}ms");
    }
}
catch (DxfException ex)
{
    Console.WriteLine($"解析失败: {ex.Message}");
}
```

### 高性能配置

```csharp
// 使用高性能配置
using var parser = new DxfParser(ParserConfiguration.HighPerformance);
bool success = parser.ParseFile(@"C:\path\to\large_file.dxf", normalize: true);

if (success)
{
    Console.WriteLine($"实体数量: {parser.EntityCount}");
    Console.WriteLine($"圆形数量: {parser.NormalizedCircles?.Length ?? 0}");
    
    // 访问标准化圆形
    foreach (var circle in parser.NormalizedCircles ?? Array.Empty<NormalizedCircle>())
    {
        Console.WriteLine($"圆心: ({circle.Center.X:F2}, {circle.Center.Y:F2}), 半径: {circle.Radius:F2}");
    }
}
```

### 自定义配置

```csharp
// 自定义配置
var config = new ParserConfiguration
{
    ParallelParsing = true,
    WorkerThreads = 8,
    MemoryLimitMb = 2048,
    StrictMode = false,
    EnableCircleOptimization = true
};

using var parser = new DxfParser(config);
bool success = parser.ParseFile(@"C:\path\to\file.dxf");
```

### 批量处理

```csharp
// 批量处理多个文件
string[] files = Directory.GetFiles(@"C:\dxf_files", "*.dxf");
var results = new List<(string file, bool success, uint entities)>();

foreach (string file in files)
{
    try
    {
        using var parser = new DxfParser(ParserConfiguration.HighPerformance);
        bool success = parser.ParseFile(file);
        results.Add((Path.GetFileName(file), success, parser.EntityCount));
    }
    catch (DxfException ex)
    {
        Console.WriteLine($"文件 {Path.GetFileName(file)} 解析失败: {ex.Message}");
        results.Add((Path.GetFileName(file), false, 0));
    }
}

// 输出结果
foreach (var (file, success, entities) in results)
{
    string status = success ? "✓" : "✗";
    Console.WriteLine($"{status} {file}: {entities} 个实体");
}
```

### 错误处理最佳实践

```csharp
public static bool SafeParseFile(string filePath, out uint entityCount, out string errorMessage)
{
    entityCount = 0;
    errorMessage = null;
    
    try
    {
        using var parser = new DxfParser();
        bool success = parser.ParseFile(filePath);
        
        if (success)
        {
            entityCount = parser.EntityCount;
            return true;
        }
        
        errorMessage = "解析失败，未知原因";
        return false;
    }
    catch (DxfIoException ex)
    {
        errorMessage = $"文件IO错误: {ex.Message}";
    }
    catch (DxfInvalidFormatException ex)
    {
        errorMessage = $"文件格式无效: {ex.Message}";
    }
    catch (DxfOutOfMemoryException ex)
    {
        errorMessage = $"内存不足: {ex.Message}";
    }
    catch (DxfException ex)
    {
        errorMessage = $"DXF错误: {ex.Message}";
    }
    catch (Exception ex)
    {
        errorMessage = $"未知错误: {ex.Message}";
    }
    
    return false;
}
```

## 最佳实践

### 1. 配置选择

- **小文件(<10MB)**: 使用默认配置
- **大文件(>100MB)**: 使用高性能配置
- **内存受限环境**: 使用低内存配置
- **严格验证需求**: 使用严格模式配置

### 2. 内存管理

```csharp
// 正确的资源管理
using var parser = new DxfParser(); // 自动释放资源

// 或者手动管理
var parser = new DxfParser();
try
{
    // 使用parser
}
finally
{
    parser.Dispose();
}
```

### 3. 性能优化

- 对于大文件，启用并行解析
- 合理设置工作线程数（通常为CPU核心数）
- 启用内存映射以提高IO性能
- 对于需要几何分析的场景，启用圆形优化

### 4. 错误处理

- 始终使用try-catch处理DxfException
- 根据具体的异常类型提供相应的用户反馈
- 记录详细的错误信息用于调试

### 5. 文件路径处理

- 支持包含中文字符的文件路径
- 使用绝对路径以避免路径解析问题
- 确保文件存在且可读

## 故障排除

### 常见问题

1. **文件路径包含中文字符**
   - 解决方案：库已支持UTF-8编码，可正常处理中文路径

2. **内存不足错误**
   - 解决方案：使用低内存配置或增加MemoryLimitMb设置

3. **解析速度慢**
   - 解决方案：使用高性能配置，启用并行解析

4. **不支持的DXF版本**
   - 解决方案：检查DXF文件版本，确保为支持的格式

### 调试信息

库会在解析过程中生成调试日志文件`ffi_debug.log`，包含：
- 函数调用信息
- 文件路径处理状态
- 配置参数详情
- 解析结果和错误信息

## 版本信息

- **当前版本**: 1.0.0
- **支持的.NET版本**: .NET 6.0+
- **支持的DXF版本**: AutoCAD R12-R2021
- **支持的平台**: Windows x64, Linux x64, macOS x64

## 许可证

MIT License - 详见项目根目录的LICENSE文件。

---

*本文档最后更新时间: 2024年1月*