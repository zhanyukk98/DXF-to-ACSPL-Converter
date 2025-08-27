using DXFtoACSPL.Core.Models;
using System.Drawing;

namespace DXFtoACSPL.Core.Interfaces;

/// <summary>
/// DXF文件解析器接口
/// </summary>
public interface IDxfParser
{
    /// <summary>
    /// 加载DXF文件
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>是否成功加载</returns>
    Task<bool> LoadFileAsync(string filePath);

    /// <summary>
    /// 解析圆形实体
    /// </summary>
    /// <param name="config">处理配置</param>
    /// <returns>圆形实体列表</returns>
    Task<List<CircleEntity>> ParseCirclesAsync(ProcessingConfig config);

    /// <summary>
    /// 获取文件信息
    /// </summary>
    /// <returns>文件信息</returns>
    DxfFileInfo GetFileInfo();

    /// <summary>
    /// 获取所有实体
    /// </summary>
    /// <returns>实体列表</returns>
    List<object> GetAllEntities();

    /// <summary>
    /// 获取模型边界
    /// </summary>
    /// <returns>边界矩形</returns>
    RectangleF GetModelBounds();

    /// <summary>
    /// 释放资源
    /// </summary>
    void Dispose();
}

/// <summary>
/// DXF文件信息
/// </summary>
public class DxfFileInfo
{
    /// <summary>
    /// 文件路径
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 文件大小
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// 实体总数
    /// </summary>
    public int TotalEntities { get; set; }

    /// <summary>
    /// 圆形实体数量
    /// </summary>
    public int CircleEntities { get; set; }

    /// <summary>
    /// 圆弧实体数量
    /// </summary>
    public int ArcEntities { get; set; }

    /// <summary>
    /// 多段线实体数量
    /// </summary>
    public int PolylineEntities { get; set; }

    /// <summary>
    /// 块引用数量
    /// </summary>
    public int BlockReferences { get; set; }

    /// <summary>
    /// 加载时间
    /// </summary>
    public TimeSpan LoadTime { get; set; }
} 