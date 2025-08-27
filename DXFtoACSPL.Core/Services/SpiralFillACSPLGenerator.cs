using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DXFtoACSPL.Core.Models;
using System.Linq;
using PathElement = DXFtoACSPL.Core.Services.PathGenerator.PathElement;

namespace DXFtoACSPL.Core.Services
{
    /// <summary>
    /// 螺旋填充ACSPL代码生成器
    /// 实现高级功能：BSPLINE、引入引出线、激光同步、功率渐变等
    /// </summary>
    public class SpiralFillACSPLGenerator
    {
        /// <summary>
        /// 螺旋填充ACSPL配置
        /// </summary>
        public class SpiralFillACSPLConfig
        {
            /// <summary>
            /// 引入线长度(mm)
            /// </summary>
            public float LeadInLength { get; set; } = 2.0f;
            
            /// <summary>
            /// 引出线长度(mm)
            /// </summary>
            public float LeadOutLength { get; set; } = 2.0f;
            
            /// <summary>
            /// 引入线速度(mm/s)
            /// </summary>
            public float LeadInSpeed { get; set; } = 50.0f;
            
            /// <summary>
            /// 引出线速度(mm/s)
            /// </summary>
            public float LeadOutSpeed { get; set; } = 50.0f;
            
            /// <summary>
            /// 主加工速度(mm/s)
            /// </summary>
            public float MainSpeed { get; set; } = 100.0f;
            
            /// <summary>
            /// 定位速度(mm/s)
            /// </summary>
            public float PositioningSpeed { get; set; } = 200.0f;
            
            /// <summary>
            /// 激光起始功率(%)
            /// </summary>
            public float StartPower { get; set; } = 50.0f;
            
            /// <summary>
            /// 激光主功率(%)
            /// </summary>
            public float MainPower { get; set; } = 100.0f;
            
            /// <summary>
            /// 激光结束功率(%)
            /// </summary>
            public float EndPower { get; set; } = 50.0f;
            
            /// <summary>
            /// 激光提前开启时间(ms)
            /// </summary>
            public int LaserLeadInTime { get; set; } = 5;
            
            /// <summary>
            /// 激光延迟关闭时间(ms)
            /// </summary>
            public int LaserLagOutTime { get; set; } = 5;
            
            /// <summary>
            /// BSPLINE容差(mm)
            /// </summary>
            public float BsplineTolerance { get; set; } = 0.01f;
            
            /// <summary>
            /// 前瞻缓冲区大小
            /// </summary>
            public int LookAheadBufferSize { get; set; } = 1000;
            
            /// <summary>
            /// 加速度(mm/s²)
            /// </summary>
            public float Acceleration { get; set; } = 500.0f;
            
            /// <summary>
            /// 减速度(mm/s²)
            /// </summary>
            public float Deceleration { get; set; } = 500.0f;
            
            /// <summary>
            /// 安全Z高度(mm)
            /// </summary>
            public float SafeZHeight { get; set; } = 10.0f;
            
            /// <summary>
            /// 加工Z高度(mm)
            /// </summary>
            public float WorkZHeight { get; set; } = 0.0f;
        }

        /// <summary>
        /// 生成螺旋填充ACSPL代码
        /// </summary>
        /// <param name="pathElements">路径元素列表</param>
        /// <param name="config">处理配置</param>
        /// <param name="spiralConfig">螺旋填充ACSPL配置</param>
        /// <returns>ACSPL代码字符串</returns>
        public string GenerateSpiralFillACSPLCode(List<PathElement> pathElements, ProcessingConfig config, SpiralFillACSPLConfig spiralConfig)
        {
            var commands = new StringBuilder();
            
            try
            {
                // 提取路径点（排除Marker）
                var pathPoints = pathElements
                    .Where(p => p.Type == "Point" && p.Data is PointF)
                    .Select(p => (PointF)p.Data)
                    .ToList();

                if (pathPoints.Count == 0)
                {
                    commands.AppendLine("// 没有找到有效的螺旋填充路径点");
                    return commands.ToString();
                }

                // 生成程序头
                commands.Append(GenerateProgramHeader(config, spiralConfig));
                
                // 生成螺旋填充路径
                commands.Append(GenerateSpiralPath(pathPoints, spiralConfig));
                
                // 生成程序尾
                commands.Append(GenerateProgramFooter());
            }
            catch (Exception ex)
            {
                commands.AppendLine($"// 错误: {ex.Message}");
            }

            return commands.ToString();
        }

        /// <summary>
        /// 生成程序头
        /// </summary>
        private string GenerateProgramHeader(ProcessingConfig config, SpiralFillACSPLConfig spiralConfig)
        {
            var header = new StringBuilder();
            
            header.AppendLine("; =========================================");
            header.AppendLine("; 螺旋填充ACSPL程序");
            header.AppendLine("; 生成时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            header.AppendLine("; =========================================");
            header.AppendLine();
            
            // 基本设置
            header.AppendLine("G90 ; 绝对坐标模式");
            header.AppendLine("UNITS MILLIMETERS ; 单位设置");
            header.AppendLine($"GLOBAL.TOLERANCE = {spiralConfig.BsplineTolerance:F4} ; 插补容差");
            header.AppendLine($"LOOKAHEAD.BUFFERSIZE = {spiralConfig.LookAheadBufferSize} ; 前瞻缓冲区");
            header.AppendLine($"ACCEL = {spiralConfig.Acceleration:F2} ; 加速度");
            header.AppendLine($"DECEL = {spiralConfig.Deceleration:F2} ; 减速度");
            header.AppendLine();
            
            // 轴定义
            header.AppendLine("int AxX = 0 ; X轴");
            header.AppendLine("int AxY = 1 ; Y轴");
            header.AppendLine("int AxZ = 2 ; Z轴");
            header.AppendLine();
            
            // 激光器初始化
            header.AppendLine("; 激光器初始化");
            header.AppendLine("lc.Init()");
            header.AppendLine("lc.SetSafetyMasks(1,1)");
            header.AppendLine($"lc.ExtraPulsesQty = {config.ExtraPulses}");
            header.AppendLine($"lc.ExtraPulsesPeriod = {config.PulsePeriod}");
            header.AppendLine($"LASER.LAGIN = {spiralConfig.LaserLeadInTime} ; 激光提前开启时间(ms)");
            header.AppendLine($"LASER.LAGOUT = {spiralConfig.LaserLagOutTime} ; 激光延迟关闭时间(ms)");
            header.AppendLine();
            
            // 轴使能
            header.AppendLine("; 轴使能");
            header.AppendLine("enable(AxX)");
            header.AppendLine("enable(AxY)");
            header.AppendLine("enable(AxZ)");
            header.AppendLine();
            
            // 坐标系设置
            header.AppendLine("; 坐标系设置");
            header.AppendLine("SET FPOS(AxX) = 0");
            header.AppendLine("SET FPOS(AxY) = 0");
            header.AppendLine("SET FPOS(AxZ) = 0");
            header.AppendLine();
            
            return header.ToString();
        }

        /// <summary>
        /// 生成螺旋路径
        /// </summary>
        private string GenerateSpiralPath(List<PointF> pathPoints, SpiralFillACSPLConfig spiralConfig)
        {
            var path = new StringBuilder();
            
            if (pathPoints.Count == 0) return path.ToString();
            
            // 移动到安全高度
            path.AppendLine("; 移动到安全高度");
            path.AppendLine($"LZ {spiralConfig.SafeZHeight:F3}");
            path.AppendLine();
            
            // 移动到起始点附近
            var startPoint = pathPoints[0];
            path.AppendLine("; 快速移动到起始点附近");
            path.AppendLine($"PTP/ve (AxX,AxY), {startPoint.X - spiralConfig.LeadInLength:F4}, {startPoint.Y:F4}, {spiralConfig.PositioningSpeed:F2}");
            path.AppendLine();
            
            // 下降到加工高度
            path.AppendLine("; 下降到加工高度");
            path.AppendLine($"LZ {spiralConfig.WorkZHeight:F3}");
            path.AppendLine();
            
            // 生成BSPLINE路径
            path.Append(GenerateBsplinePath(pathPoints, spiralConfig));
            
            // 上升到安全高度
            path.AppendLine("; 上升到安全高度");
            path.AppendLine($"LZ {spiralConfig.SafeZHeight:F3}");
            path.AppendLine();
            
            return path.ToString();
        }

        /// <summary>
        /// 生成BSPLINE路径
        /// </summary>
        private string GenerateBsplinePath(List<PointF> pathPoints, SpiralFillACSPLConfig spiralConfig)
        {
            var bspline = new StringBuilder();
            
            if (pathPoints.Count < 2) return bspline.ToString();
            
            var startPoint = pathPoints[0];
            var endPoint = pathPoints[^1];
            
            // 引入线
            bspline.AppendLine("; =========================================");
            bspline.AppendLine("; 引入线");
            bspline.AppendLine("; =========================================");
            bspline.AppendLine($"LINEAR ; 切换到直线插补");
            bspline.AppendLine($"MOVE LIN [{startPoint.X - spiralConfig.LeadInLength:F4}, {startPoint.Y:F4}] F {spiralConfig.PositioningSpeed:F2} ; 快速定位到引入线起点");
            bspline.AppendLine("lc.LaserEnable() ; 开启激光");
            bspline.AppendLine($"LPOWER {spiralConfig.StartPower:F1} ; 起始功率");
            bspline.AppendLine($"MOVE LIN [{startPoint.X:F4}, {startPoint.Y:F4}] F {spiralConfig.LeadInSpeed:F2} ; 走引入线到实际起点");
            bspline.AppendLine();
            
            // 主螺旋路径（BSPLINE）
            bspline.AppendLine("; =========================================");
            bspline.AppendLine("; 主螺旋路径 (BSPLINE)");
            bspline.AppendLine("; =========================================");
            bspline.AppendLine("BSPLINE START ; 开始BSPLINE");
            bspline.AppendLine($"LPOWER {spiralConfig.MainPower:F1} ; 主功率");
            
            // 添加所有路径点
            for (int i = 0; i < pathPoints.Count; i++)
            {
                var point = pathPoints[i];
                bspline.AppendLine($"BSPLINE POINT [{point.X:F4}, {point.Y:F4}] ; 路径点 {i + 1}");
            }
            
            bspline.AppendLine($"BSPLINE END F {spiralConfig.MainSpeed:F2} ; 结束BSPLINE并设置进给速度");
            bspline.AppendLine();
            
            // 引出线
            bspline.AppendLine("; =========================================");
            bspline.AppendLine("; 引出线");
            bspline.AppendLine("; =========================================");
            bspline.AppendLine("LINEAR ; 切回直线插补");
            bspline.AppendLine($"LPOWER {spiralConfig.EndPower:F1} ; 结束功率");
            bspline.AppendLine($"MOVE LIN [{endPoint.X + spiralConfig.LeadOutLength:F4}, {endPoint.Y:F4}] F {spiralConfig.LeadOutSpeed:F2} ; 走引出线");
            bspline.AppendLine("LPOWER 0 ; 功率渐降到0");
            bspline.AppendLine("lc.LaserDisable() ; 关闭激光");
            bspline.AppendLine();
            
            return bspline.ToString();
        }

        /// <summary>
        /// 生成程序尾
        /// </summary>
        private string GenerateProgramFooter()
        {
            var footer = new StringBuilder();
            
            footer.AppendLine("; =========================================");
            footer.AppendLine("; 程序结束");
            footer.AppendLine("; =========================================");
            footer.AppendLine("STOP ; 程序停止");
            footer.AppendLine();
            
            return footer.ToString();
        }

        /// <summary>
        /// 生成优化的螺旋填充ACSPL代码（包含岛屿连接）
        /// </summary>
        /// <param name="pathElements">路径元素列表</param>
        /// <param name="config">处理配置</param>
        /// <param name="spiralConfig">螺旋填充ACSPL配置</param>
        /// <returns>ACSPL代码字符串</returns>
        public string GenerateOptimizedSpiralFillACSPLCode(List<PathElement> pathElements, ProcessingConfig config, SpiralFillACSPLConfig spiralConfig)
        {
            var commands = new StringBuilder();
            
            try
            {
                // 按Marker分组处理
                var groups = SplitPathIntoGroups(pathElements);
                
                // 生成程序头
                commands.Append(GenerateProgramHeader(config, spiralConfig));
                
                // 处理每个组
                for (int i = 0; i < groups.Count; i++)
                {
                    var group = groups[i];
                    if (group.Count == 0) continue;
                    
                    commands.AppendLine($"; =========================================");
                    commands.AppendLine($"; 螺旋填充组 {i + 1} (共 {groups.Count} 组)");
                    commands.AppendLine($"; =========================================");
                    commands.Append(GenerateSpiralPath(group, spiralConfig));
                    
                    // 如果不是最后一组，添加岛屿连接
                    if (i < groups.Count - 1 && groups[i + 1].Count > 0)
                    {
                        commands.Append(GenerateIslandConnection(group[^1], groups[i + 1][0], spiralConfig));
                    }
                }
                
                // 生成程序尾
                commands.Append(GenerateProgramFooter());
            }
            catch (Exception ex)
            {
                commands.AppendLine($"// 错误: {ex.Message}");
            }

            return commands.ToString();
        }

        /// <summary>
        /// 将路径按Marker分组
        /// </summary>
        private List<List<PointF>> SplitPathIntoGroups(List<PathElement> pathElements)
        {
            var groups = new List<List<PointF>>();
            var currentGroup = new List<PointF>();
            
            foreach (var element in pathElements)
            {
                if (element.Type == "Point" && element.Data is PointF point)
                {
                    currentGroup.Add(point);
                }
                else if (element.Type == "Marker")
                {
                    if (currentGroup.Count > 0)
                    {
                        groups.Add(currentGroup);
                        currentGroup = new List<PointF>();
                    }
                }
            }
            
            // 添加最后一组
            if (currentGroup.Count > 0)
            {
                groups.Add(currentGroup);
            }
            
            return groups;
        }

        /// <summary>
        /// 生成岛屿连接代码
        /// </summary>
        private string GenerateIslandConnection(PointF fromPoint, PointF toPoint, SpiralFillACSPLConfig spiralConfig)
        {
            var connection = new StringBuilder();
            
            connection.AppendLine("; =========================================");
            connection.AppendLine("; 岛屿连接");
            connection.AppendLine("; =========================================");
            connection.AppendLine("lc.LaserDisable() ; 关闭激光");
            connection.AppendLine($"LZ {spiralConfig.SafeZHeight:F3} ; 抬刀到安全高度");
            connection.AppendLine($"PTP/ve (AxX,AxY), {toPoint.X - spiralConfig.LeadInLength:F6}, {toPoint.Y:F6}, {spiralConfig.PositioningSpeed:F2} ; 快速移动到下一个岛屿");
            connection.AppendLine($"LZ {spiralConfig.WorkZHeight:F3} ; 落刀到加工高度");
            connection.AppendLine();
            
            return connection.ToString();
        }

        /// <summary>
        /// 保存ACSPL代码到文件
        /// </summary>
        public async Task<bool> SaveCodeToFileAsync(string code, string filePath)
        {
            try
            {
                await File.WriteAllTextAsync(filePath, code, Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存ACSPL代码失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 生成并保存ACSPL代码文件
        /// </summary>
        public async Task<bool> GenerateCodeFileAsync(List<PathElement> pathElements, ProcessingConfig config, SpiralFillACSPLConfig spiralConfig, string filePath)
        {
            try
            {
                var code = GenerateOptimizedSpiralFillACSPLCode(pathElements, config, spiralConfig);
                return await SaveCodeToFileAsync(code, filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"生成ACSPL代码文件失败: {ex.Message}");
                return false;
            }
        }
    }
}