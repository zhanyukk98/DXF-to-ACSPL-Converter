using DXFtoACSPL.Core.Models;

namespace DXFtoACSPL.Core.Interfaces;

/// <summary>
/// 数据服务接口
/// </summary>
public interface IDataService
{
    /// <summary>
    /// 保存圆形实体数据
    /// </summary>
    /// <param name="circles">圆形实体列表</param>
    /// <param name="filePath">文件路径</param>
    /// <returns>是否保存成功</returns>
    Task<bool> SaveCirclesAsync(List<CircleEntity> circles, string filePath);

    /// <summary>
    /// 加载圆形实体数据
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>圆形实体列表</returns>
    Task<List<CircleEntity>> LoadCirclesAsync(string filePath);

    /// <summary>
    /// 保存路径数据
    /// </summary>
    /// <param name="path">路径元素列表</param>
    /// <param name="filePath">文件路径</param>
    /// <returns>是否保存成功</returns>
    Task<bool> SavePathAsync(List<PathElement> path, string filePath);

    /// <summary>
    /// 加载路径数据
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>路径元素列表</returns>
    Task<List<PathElement>> LoadPathAsync(string filePath);

    /// <summary>
    /// 保存配置
    /// </summary>
    /// <param name="config">处理配置</param>
    /// <param name="filePath">文件路径</param>
    /// <returns>是否保存成功</returns>
    Task<bool> SaveConfigAsync(ProcessingConfig config, string filePath);

    /// <summary>
    /// 加载配置
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>处理配置</returns>
    Task<ProcessingConfig?> LoadConfigAsync(string filePath);
} 