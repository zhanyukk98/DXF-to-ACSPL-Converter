namespace DXFtoACSPL.Core.Models;

/// <summary>
/// DXF处理配置
/// </summary>
public class ProcessingConfig
{
    /// <summary>
    /// 圆形检测容差
    /// </summary>
    public float CircleDetectionTolerance { get; set; } = 1.5f;

    /// <summary>
    /// 中心点去重容差
    /// </summary>
    public float CenterPointTolerance { get; set; } = 0.1f;

    /// <summary>
    /// 最小半径
    /// </summary>
    public float MinRadius { get; set; } = 0.1f;

    /// <summary>
    /// 最大半径
    /// </summary>
    public float MaxRadius { get; set; } = 100f;

    /// <summary>
    /// 缩放比例
    /// </summary>
    public float Scale { get; set; } = 1.0f;

    /// <summary>
    /// 移动速度
    /// </summary>
    public float MoveVelocity { get; set; } = 50f;

    /// <summary>
    /// 加工速度
    /// </summary>
    public float ProcessVelocity { get; set; } = 50f;

    /// <summary>
    /// 额外脉冲数
    /// </summary>
    public int ExtraPulses { get; set; } = 0;

    /// <summary>
    /// 脉冲周期
    /// </summary>
    public float PulsePeriod { get; set; } = 0.10f;

    /// <summary>
    /// 路径规划容差1（用于蛇形路径行分组）
    /// </summary>
    public float PathTolerance1 { get; set; } = 10.0f;

    /// <summary>
    /// 路径规划容差2（用于空间聚类）
    /// </summary>
    public float PathTolerance2 { get; set; } = 1000.0f;

    /// <summary>
    /// 是否启用XY翻转
    /// </summary>
    public bool EnableXYFlip { get; set; } = false;

    /// <summary>
    /// 是否启用中心化
    /// </summary>
    public bool EnableCentering { get; set; } = false;

    /// <summary>
    /// 加工旋转角度（单位：度，正为逆时针，负为顺时针）
    /// </summary>
    public float RotationAngle { get; set; } = 0f;

    /// <summary>
    /// 路径生成算法类型
    /// </summary>
    public PathGenerationAlgorithm PathAlgorithm { get; set; } = PathGenerationAlgorithm.Cluster;

    /// <summary>
    /// 阿基米德螺旋半径增量（dr）
    /// </summary>
    public float SpiralRadiusIncrement { get; set; } = 1.0f;

    /// <summary>
    /// 阿基米德螺旋角度步长（dtheta，单位：弧度）
    /// </summary>
    public float SpiralAngleStep { get; set; } = 0.1f;

    /// <summary>
    /// 阿基米德螺旋起始半径
    /// </summary>
    public float SpiralStartRadius { get; set; } = 0.0f;

    /// <summary>
    /// 阿基米德螺旋中心点X坐标（如果为null则自动计算）
    /// </summary>
    public float? SpiralCenterX { get; set; } = null;

    /// <summary>
    /// 阿基米德螺旋中心点Y坐标（如果为null则自动计算）
    /// </summary>
    public float? SpiralCenterY { get; set; } = null;

    public ProcessingConfig()
    {
    }

    /// <summary>
    /// 验证配置参数
    /// </summary>
    /// <returns>验证结果</returns>
    public bool Validate()
    {
        return CircleDetectionTolerance > 0 &&
               CenterPointTolerance > 0 &&
               MinRadius >= 0 &&
               MaxRadius > MinRadius &&
               Scale > 0 &&
               MoveVelocity > 0 &&
               ProcessVelocity > 0 &&
               ExtraPulses >= 0 &&
               PulsePeriod > 0 &&
               PathTolerance1 > 0 &&
               PathTolerance2 > 0;
    }

    /// <summary>
    /// 获取验证错误信息
    /// </summary>
    /// <returns>错误信息列表</returns>
    public List<string> GetValidationErrors()
    {
        var errors = new List<string>();

        if (CircleDetectionTolerance <= 0)
            errors.Add("圆形检测容差必须大于0");

        if (CenterPointTolerance <= 0)
            errors.Add("中心点容差必须大于0");

        if (MinRadius < 0)
            errors.Add("最小半径不能为负数");

        if (MaxRadius <= MinRadius)
            errors.Add("最大半径必须大于最小半径");

        if (Scale <= 0)
            errors.Add("缩放比例必须大于0");

        if (MoveVelocity <= 0)
            errors.Add("移动速度必须大于0");

        if (ProcessVelocity <= 0)
            errors.Add("加工速度必须大于0");

        if (ExtraPulses < 0)
            errors.Add("额外脉冲数不能为负数");

        if (PulsePeriod <= 0)
            errors.Add("脉冲周期必须大于0");

        if (PathTolerance1 <= 0)
            errors.Add("路径规划容差1必须大于0");

        if (PathTolerance2 <= 0)
            errors.Add("路径规划容差2必须大于0");

        return errors;
    }
} 