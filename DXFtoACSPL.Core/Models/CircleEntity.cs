using System.Drawing;

namespace DXFtoACSPL.Core.Models;

/// <summary>
/// 圆形实体数据模型
/// </summary>
public class CircleEntity
{
    /// <summary>
    /// 实体索引
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// 实体类型
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// 圆形中心点
    /// </summary>
    public PointF Center { get; set; }

    /// <summary>
    /// 半径
    /// </summary>
    public float Radius { get; set; }

    /// <summary>
    /// 所属块名称
    /// </summary>
    public string? BlockName { get; set; }

    /// <summary>
    /// 插入名称
    /// </summary>
    public string? InsertName { get; set; }

    /// <summary>
    /// 显示参数信息
    /// </summary>
    public string Parameters => $"圆心:({Center.X:F4},{Center.Y:F4}), 半径: {Radius:F4}";

    /// <summary>
    /// 是否为有效圆形
    /// </summary>
    public bool IsValid => Radius > 0;

    public CircleEntity()
    {
    }

    public CircleEntity(PointF center, float radius, string entityType = "CIRCLE")
    {
        Center = center;
        Radius = radius;
        EntityType = entityType;
    }

    public override string ToString()
    {
        return $"{EntityType} - {Parameters}";
    }
} 