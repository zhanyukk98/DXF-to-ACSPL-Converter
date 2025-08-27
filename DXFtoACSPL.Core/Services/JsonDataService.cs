using DXFtoACSPL.Core.Interfaces;
using DXFtoACSPL.Core.Models;
using Newtonsoft.Json;

namespace DXFtoACSPL.Core.Services;

public class JsonDataService : IDataService
{
    private readonly JsonSerializerSettings _jsonSettings;

    public JsonDataService()
    {
        _jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Include
        };
    }

    public async Task<bool> SaveCirclesAsync(List<CircleEntity> circles, string filePath)
    {
        try
        {
            var json = JsonConvert.SerializeObject(circles, _jsonSettings);
            await File.WriteAllTextAsync(filePath, json);
            return true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"保存圆形实体数据失败: {ex.Message}", ex);
        }
    }

    public async Task<List<CircleEntity>> LoadCirclesAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"文件不存在: {filePath}");
            }

            var json = await File.ReadAllTextAsync(filePath);
            var circles = JsonConvert.DeserializeObject<List<CircleEntity>>(json, _jsonSettings);
            return circles ?? new List<CircleEntity>();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"加载圆形实体数据失败: {ex.Message}", ex);
        }
    }

    public async Task<bool> SavePathAsync(List<PathElement> path, string filePath)
    {
        try
        {
            var json = JsonConvert.SerializeObject(path, _jsonSettings);
            await File.WriteAllTextAsync(filePath, json);
            return true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"保存路径数据失败: {ex.Message}", ex);
        }
    }

    public async Task<List<PathElement>> LoadPathAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"文件不存在: {filePath}");
            }

            var json = await File.ReadAllTextAsync(filePath);
            var path = JsonConvert.DeserializeObject<List<PathElement>>(json, _jsonSettings);
            return path ?? new List<PathElement>();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"加载路径数据失败: {ex.Message}", ex);
        }
    }

    public async Task<bool> SaveConfigAsync(ProcessingConfig config, string filePath)
    {
        try
        {
            var json = JsonConvert.SerializeObject(config, _jsonSettings);
            await File.WriteAllTextAsync(filePath, json);
            return true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"保存配置失败: {ex.Message}", ex);
        }
    }

    public async Task<ProcessingConfig?> LoadConfigAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var config = JsonConvert.DeserializeObject<ProcessingConfig>(json, _jsonSettings);
            return config;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"加载配置失败: {ex.Message}", ex);
        }
    }
} 