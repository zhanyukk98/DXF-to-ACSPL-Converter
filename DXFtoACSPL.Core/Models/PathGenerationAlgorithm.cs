namespace DXFtoACSPL.Core.Models
{
    /// <summary>
    /// 路径生成算法类型
    /// </summary>
    public enum PathGenerationAlgorithm
    {
        /// <summary>
        /// 聚类算法（原始算法）
        /// </summary>
        Cluster,
        
        /// <summary>
        /// 螺旋填充算法
        /// </summary>
        SpiralFill,
        
        /// <summary>
        /// 蛇形路径算法
        /// </summary>
        SnakePath,
        
        /// <summary>
        /// 最近邻算法
        /// </summary>
        NearestNeighbor,
        
        /// <summary>
        /// 测试算法（自定义路径规划）
        /// </summary>
        TestAlgorithm,
        
        /// <summary>
        /// 聚类算法强化版（自适应网格 + 空间索引优化）
        /// </summary>
        EnhancedCluster
    }
} 