using System.Drawing;

namespace DXFtoACSPL.Core.Models;

/// <summary>
/// 路径元素数据模型
/// </summary>
public class PathElement
{
    /// <summary>
    /// 位置坐标
    /// </summary>
    public PointF Position { get; set; }

    /// <summary>
    /// 元素类型
    /// </summary>
    public PathElementType Type { get; set; }

    /// <summary>
    /// 速度参数
    /// </summary>
    public float Velocity { get; set; }

    /// <summary>
    /// 额外脉冲数
    /// </summary>
    public int ExtraPulses { get; set; }

    /// <summary>
    /// 脉冲周期
    /// </summary>
    public float PulsePeriod { get; set; }

    /// <summary>
    /// 注释信息
    /// </summary>
    public string? Comment { get; set; }

    public PathElement()
    {
    }

    public PathElement(PointF position, PathElementType type = PathElementType.Move)
    {
        Position = position;
        Type = type;
    }

    public override string ToString()
    {
        return $"{Type} - ({Position.X:F4}, {Position.Y:F4})";
    }
}

/// <summary>
/// 路径元素类型
/// </summary>
public enum PathElementType
{
    /// <summary>
    /// 移动
    /// </summary>
    Move,

    /// <summary>
    /// 加工
    /// </summary>
    Process,

    /// <summary>
    /// 快速移动
    /// </summary>
    RapidMove
} 