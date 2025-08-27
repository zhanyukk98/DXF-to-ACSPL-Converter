using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using DXFtoACSPL.Core.Models;

namespace DXFtoACSPL.Core.Services;

/// <summary>
/// 路径分析器
/// </summary>
public class PathAnalyzer
{
    /// <summary>
    /// 分析路径生成过程
    /// </summary>
    public string AnalyzePathGeneration(
        List<CircleEntity> circles,
        List<PointF> finalPath,
        ProcessingConfig config)
    {
        var analysis = new List<string>();
        
        // 基本统计
        int originalCount = circles.Count;
        int finalCount = finalPath.Count;
        int uniqueCenters = circles
            .GroupBy(c => new { X = Math.Round(c.Center.X / config.CenterPointTolerance), Y = Math.Round(c.Center.Y / config.CenterPointTolerance) })
            .Count();
        
        analysis.Add($"原始实体数量: {originalCount}");
        analysis.Add($"检测到的孔位数量: {uniqueCenters}");
        analysis.Add($"最终路径点数量: {finalCount}");
        analysis.Add($"路径点增加数量: {finalCount - uniqueCenters}");
        
        // 分析增加原因
        if (finalCount > uniqueCenters)
        {
            analysis.Add("");
            analysis.Add("路径点数量增加的原因:");
            analysis.Add("1. 蛇形路径算法: 每行之间需要连接点");
            analysis.Add("2. 多区域连接: 不同分区之间需要连接点");
            analysis.Add("3. 路径优化: 可能添加额外的中间点");
            analysis.Add("4. 重复点: 算法可能产生重复的路径点");
        }
        
        return string.Join("\n", analysis);
    }
} 