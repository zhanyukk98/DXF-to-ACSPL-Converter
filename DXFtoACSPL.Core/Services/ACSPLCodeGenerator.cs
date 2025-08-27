using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DXFtoACSPL.Core.Models;
using System.Linq; // Added for .Any() and .Sum()
using PathElement = DXFtoACSPL.Core.Services.PathGenerator.PathElement; // 使用PathGenerator中的PathElement类型

namespace DXFtoACSPL.Core.Services
{
    /// <summary>
    /// ACSPL代码生成器
    /// </summary>
    public class ACSPLCodeGenerator
    {
        /// <summary>
        /// 生成ACSPL代码
        /// </summary>
        /// <param name="pathElements">路径元素列表</param>
        /// <param name="config">处理配置</param>
        /// <returns>ACSPL代码字符串</returns>
        public string GenerateACSPLCode(List<PathElement> pathElements, ProcessingConfig config)
        {
            var commands = new StringBuilder();
            var commands1 = new StringBuilder(); // 初始化部分
            var commands2 = new StringBuilder(); // 坐标数组部分
            var commands3 = new StringBuilder(); // 运动指令部分

            try
            {
                // 按路径顺序提取圆心坐标和Marker
                var orderedPoints = new List<PointF>();
                foreach (var element in pathElements)
                {
                    if (element.Type == "Point" && element.Data is PointF point)
                    {
                        // 验证坐标有效性
                        if (IsValidCoordinate(point))
                        {
                            orderedPoints.Add(point);
                        }
                        else
                        {
                            Console.WriteLine($"警告：跳过无效坐标点 ({point.X}, {point.Y})");
                        }
                    }
                    else if (element.Type == "Marker")
                    {
                        // 插入特殊分组点
                        orderedPoints.Add(new PointF(-999999, -999999));
                    }
                }

                if (orderedPoints.Count == 0)
                {
                    commands.AppendLine("// 没有找到有效的加工点");
                    return commands.ToString();
                }

                // 检查是否包含Marker（特殊值：X=-999999, Y=-999999）
                bool hasMarkers = orderedPoints.Any(p => Math.Abs(p.X - (-999999)) < 0.001 && Math.Abs(p.Y - (-999999)) < 0.001);

                if (hasMarkers)
                {
                    // 按Marker分组处理
                    return GenerateGroupedACSPLCode(orderedPoints, config);
                }
                else
                {
                    // 单组处理（原来的逻辑）
                    return GenerateSingleGroupACSPLCode(orderedPoints, config);
                }
            }
            catch (Exception ex)
            {
                commands.AppendLine($"// 错误: {ex.Message}");
            }

            return commands.ToString();
        }

        /// <summary>
        /// 生成分组ACSPL代码（模仿原来C#程序的逻辑）
        /// </summary>
        private string GenerateGroupedACSPLCode(List<PointF> allPoints, ProcessingConfig config)
        {
            var commands = new StringBuilder();
            var commands1 = new StringBuilder(); // 初始化部分
            var commands2 = new StringBuilder(); // 坐标数组部分
            var commands3 = new StringBuilder(); // 运动指令部分

            try
            {
                // 分割数据为组
                List<List<PointF>> groups = new List<List<PointF>>();
                List<PointF> currentGroup = new List<PointF>();

                foreach (var point in allPoints)
                {
                    // 检查是否是Marker
                    if (Math.Abs(point.X - (-999999)) < 0.001 && Math.Abs(point.Y - (-999999)) < 0.001)
                    {
                        // 遇到Marker，结束当前组
                        if (currentGroup.Count > 0)
                        {
                            groups.Add(currentGroup);
                            currentGroup = new List<PointF>();
                        }
                    }
                    else
                    {
                        // 添加点到当前组
                        currentGroup.Add(point);
                    }
                }

                // 添加最后一组
                if (currentGroup.Count > 0)
                {
                    groups.Add(currentGroup);
                }

                // 1. 初始化部分
                commands1.AppendLine("int AxX = 0");
                commands1.AppendLine("int AxY = 1");
                commands1.AppendLine("real PulseWidth1");
                commands1.AppendLine("PulseWidth1 = 0.001");
                commands1.AppendLine("lc.Init()");
                commands1.AppendLine("lc.SetSafetyMasks(1,1)");
                commands1.AppendLine("enable(AxX)");
                commands1.AppendLine("enable(AxY)");
                commands1.AppendLine($"lc.ExtraPulsesQty = {config.ExtraPulses}");
                commands1.AppendLine($"lc.ExtraPulsesPeriod = {config.PulsePeriod}");
                commands1.AppendLine("SET FPOS(AxX)= 0");
                commands1.AppendLine("SET FPOS(AxY)= 0");

                // 2. 计算总点数并生成坐标数组声明
                int totalPoints = groups.Sum(g => g.Count);
                commands1.AppendLine($"global real XCoord10({totalPoints}), YCoord10({totalPoints})");

                // 3. 填充坐标数组
                int k = 0;
                foreach (var group in groups)
                {
                    if (group.Count == 0) continue;

                    for (int i = 0; i < group.Count; i++)
                    {
                        commands2.AppendLine($"XCoord10({k}) = {group[i].X:F4}; YCoord10({k}) = {group[i].Y:F4};");
                        k = k + 1;
                    }
                }

                // 4. 生成运动指令
                commands3.AppendLine($"lc.CoordinateArrPulse({totalPoints}, PulseWidth1, XCoord10, YCoord10)");

                // 5. 为每个组生成运动控制命令
                int globalIndex = 0;
                foreach (var group in groups)
                {
                    if (group.Count == 0) continue;

                    // 生成运动控制命令
                    double firstX = group[0].X - 0.01;
                    double firstY = group[0].Y;

                    commands3.AppendLine($"PTP/ve (AxX,AxY), {firstX:F4}, {firstY:F4}, {config.MoveVelocity}");
                    commands3.AppendLine($"VEL(AxX) = {config.ProcessVelocity}");
                    commands3.AppendLine($"VEL(AxY) = {config.ProcessVelocity}");
                    commands3.AppendLine("lc.LaserEnable()");

                    // 优化线段绘制
                    int lastIndex = globalIndex;
                    for (int i = 0; i < group.Count; i++)
                    {
                        // 检查方向是否改变
                        if (i > 0 && IsDirectionChanged(group, i - 1, i))
                        {
                            // 方向改变，生成新的PTP命令
                            commands3.AppendLine($"PTP/e (AxX,AxY), {group[i].X:F4}, {group[i].Y:F4}");
                        }
                        globalIndex++;
                    }

                    // 结束当前组，关闭激光
                    commands3.AppendLine("lc.LaserDisable()");
                }

                // 组合所有命令
                commands.Append(commands1).Append(commands2).Append(commands3);
                commands.AppendLine("STOP");
            }
            catch (Exception ex)
            {
                commands.AppendLine($"// 错误: {ex.Message}");
            }

            return commands.ToString();
        }

        /// <summary>
        /// 生成单组ACSPL代码（原来的逻辑）
        /// </summary>
        private string GenerateSingleGroupACSPLCode(List<PointF> orderedPoints, ProcessingConfig config)
        {
            var commands = new StringBuilder();
            var commands1 = new StringBuilder(); // 初始化部分
            var commands2 = new StringBuilder(); // 坐标数组部分
            var commands3 = new StringBuilder(); // 运动指令部分

            try
            {
                // 1. 初始化部分
                commands1.AppendLine("int AxX = 0");
                commands1.AppendLine("int AxY = 1");
                commands1.AppendLine("real PulseWidth1");
                commands1.AppendLine("PulseWidth1 = 0.001");
                commands1.AppendLine("lc.Init()");
                commands1.AppendLine("lc.SetSafetyMasks(1,1)");
                commands1.AppendLine("enable(AxX)");
                commands1.AppendLine("enable(AxY)");
                commands1.AppendLine($"lc.ExtraPulsesQty = {config.ExtraPulses}");
                commands1.AppendLine($"lc.ExtraPulsesPeriod = {config.PulsePeriod}");
                commands1.AppendLine("SET FPOS(AxX)= 0");
                commands1.AppendLine("SET FPOS(AxY)= 0");

                // 2. 坐标数组部分
                commands1.AppendLine($"global real XCoord10({orderedPoints.Count}), YCoord10({orderedPoints.Count})");

                for (int i = 0; i < orderedPoints.Count; i++)
                {
                    var point = orderedPoints[i];
                    commands2.AppendLine($"XCoord10({i}) = {point.X:F4}; YCoord10({i}) = {point.Y:F4};");
                }

                // 3. 运动指令部分
                commands3.AppendLine($"lc.CoordinateArrPulse({orderedPoints.Count}, PulseWidth1, XCoord10, YCoord10)");

                // 生成运动控制命令
                if (orderedPoints.Count > 0)
                {
                    var firstPoint = orderedPoints[0];
                    double firstX = firstPoint.X - 0.01; // 偏移0.01作为起始点
                    double firstY = firstPoint.Y;

                    commands3.AppendLine($"PTP/ve (AxX,AxY), {firstX:F4}, {firstY:F4}, {config.MoveVelocity}");
                    commands3.AppendLine($"VEL(AxX) = {config.ProcessVelocity}");
                    commands3.AppendLine($"VEL(AxY) = {config.ProcessVelocity}");
                    commands3.AppendLine("lc.LaserEnable()");

                    // 优化线段绘制
                    int lastIndex = 0;
                    for (int i = 0; i < orderedPoints.Count; i++)
                    {
                        // 检查方向是否改变
                        bool directionChanged = IsDirectionChanged(orderedPoints, lastIndex, i);
                        bool isLastPoint = (i == orderedPoints.Count - 1);

                        if (directionChanged || isLastPoint)
                        {
                            var point = orderedPoints[i];
                            commands3.AppendLine($"PTP/e (AxX,AxY), {point.X:F4}, {point.Y:F4}");
                            lastIndex = i;
                        }
                    }

                    commands3.AppendLine("lc.LaserDisable()");
                }

                // 组合所有命令
                commands.Append(commands1).Append(commands2).Append(commands3);
                commands.AppendLine("STOP");
            }
            catch (Exception ex)
            {
                commands.AppendLine($"// 错误: {ex.Message}");
            }

            return commands.ToString();
        }

        /// <summary>
        /// 检查方向是否改变
        /// </summary>
        private bool IsDirectionChanged(List<PointF> points, int startIndex, int currentIndex)
        {
            if (currentIndex <= startIndex || currentIndex >= points.Count - 1) return false;

            const float tolerance = 0.001f;

            // 计算当前线段的方向向量
            float dx1 = points[currentIndex].X - points[startIndex].X;
            float dy1 = points[currentIndex].Y - points[startIndex].Y;

            // 计算下一线段的方向向量
            float dx2 = points[currentIndex + 1].X - points[currentIndex].X;
            float dy2 = points[currentIndex + 1].Y - points[currentIndex].Y;

            // 如果任一向量长度太小，认为没有方向改变
            if (Math.Abs(dx1) < tolerance && Math.Abs(dy1) < tolerance) return false;
            if (Math.Abs(dx2) < tolerance && Math.Abs(dy2) < tolerance) return false;

            // 计算方向向量的角度（简化处理）
            // 如果两个向量的点积为负，说明方向改变超过90度
            float dotProduct = dx1 * dx2 + dy1 * dy2;
            float length1 = (float)Math.Sqrt(dx1 * dx1 + dy1 * dy1);
            float length2 = (float)Math.Sqrt(dx2 * dx2 + dy2 * dy2);

            if (length1 < tolerance || length2 < tolerance) return false;

            // 计算夹角余弦值
            float cosAngle = dotProduct / (length1 * length2);
            
            // 如果夹角大于45度（cos(45°) ≈ 0.707），认为方向改变
            return cosAngle < 0.707f;
        }

        /// <summary>
        /// 验证坐标是否有效
        /// </summary>
        private bool IsValidCoordinate(PointF point)
        {
            // 检查X坐标是否在有效范围内
            if (double.IsInfinity(point.X) || double.IsNaN(point.X) || point.X < -1000000 || point.X > 1000000)
            {
                return false;
            }

            // 检查Y坐标是否在有效范围内
            if (double.IsInfinity(point.Y) || double.IsNaN(point.Y) || point.Y < -1000000 || point.Y > 1000000)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 保存代码到文件
        /// </summary>
        /// <param name="code">代码内容</param>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否保存成功</returns>
        public async Task<bool> SaveCodeToFileAsync(string code, string filePath)
        {
            try
            {
                await File.WriteAllTextAsync(filePath, code, Encoding.UTF8);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 生成代码文件
        /// </summary>
        /// <param name="pathElements">路径元素列表</param>
        /// <param name="config">处理配置</param>
        /// <param name="filePath">输出文件路径</param>
        /// <returns>是否生成成功</returns>
        public async Task<bool> GenerateCodeFileAsync(List<PathElement> pathElements, ProcessingConfig config, string filePath)
        {
            try
            {
                var code = GenerateACSPLCode(pathElements, config);
                return await SaveCodeToFileAsync(code, filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"生成代码文件失败: {ex.Message}");
                return false;
            }
        }
    }
}