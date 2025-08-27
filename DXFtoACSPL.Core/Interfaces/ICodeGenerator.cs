using DXFtoACSPL.Core.Models;

namespace DXFtoACSPL.Core.Interfaces;

/// <summary>
/// 代码生成器接口
/// </summary>
public interface ICodeGenerator
{
    /// <summary>
    /// 生成ACSPL代码
    /// </summary>
    /// <param name="path">路径元素列表</param>
    /// <param name="config">处理配置</param>
    /// <returns>ACSPL代码字符串</returns>
    Task<string> GenerateACSPLCodeAsync(List<PathElement> path, ProcessingConfig config);

    /// <summary>
    /// 保存代码到文件
    /// </summary>
    /// <param name="code">代码内容</param>
    /// <param name="filePath">文件路径</param>
    /// <returns>是否保存成功</returns>
    Task<bool> SaveCodeToFileAsync(string code, string filePath);

    /// <summary>
    /// 生成代码文件
    /// </summary>
    /// <param name="path">路径元素列表</param>
    /// <param name="config">处理配置</param>
    /// <param name="filePath">输出文件路径</param>
    /// <returns>是否生成成功</returns>
    Task<bool> GenerateCodeFileAsync(List<PathElement> path, ProcessingConfig config, string filePath);
} 