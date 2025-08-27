using DXFtoACSPL.Core.Models;

namespace DXFtoACSPL.Core.Interfaces;

/// <summary>
/// 路径生成器接口
/// </summary>
public interface IPathGenerator
{
    /// <summary>
    /// 生成加工路径
    /// </summary>
    /// <param name="circles">圆形实体列表</param>
    /// <param name="config">处理配置</param>
    /// <returns>路径元素列表</returns>
    Task<List<PathElement>> GeneratePathAsync(List<CircleEntity> circles, ProcessingConfig config);

    /// <summary>
    /// 生成标记路径
    /// </summary>
    /// <param name="circles">圆形实体列表</param>
    /// <param name="config">处理配置</param>
    /// <returns>标记路径元素列表</returns>
    Task<List<PathElement>> GenerateMarkedPathAsync(List<CircleEntity> circles, ProcessingConfig config);

    /// <summary>
    /// 优化路径
    /// </summary>
    /// <param name="path">原始路径</param>
    /// <param name="config">处理配置</param>
    /// <returns>优化后的路径</returns>
    Task<List<PathElement>> OptimizePathAsync(List<PathElement> path, ProcessingConfig config);
} 