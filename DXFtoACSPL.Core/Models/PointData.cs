using System.Drawing;

namespace DXFtoACSPL.Core.Models;

/// <summary>
/// 点数据模型，用于路径生成算法
/// </summary>
public class PointData
{
    public float X { get; set; }
    public float Y { get; set; }
    public bool IsEmpty { get; set; }

    public PointData()
    {
        IsEmpty = true;
    }

    public PointData(float x, float y)
    {
        X = x;
        Y = y;
        IsEmpty = false;
    }

    public PointData(PointF point)
    {
        X = point.X;
        Y = point.Y;
        IsEmpty = false;
    }

    public PointF ToPointF()
    {
        return new PointF(X, Y);
    }

    public override string ToString()
    {
        return $"({X:F4}, {Y:F4})";
    }
} 