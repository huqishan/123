using Module.Business.Models;
using Module.Business.Features.SchemeConfiguration;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Module.Business.Services;

/// <summary>
/// 方案配置持久化工具，负责方案配置的加载、保存与规范化。
/// </summary>
public static class SchemeConfigurationStore
{
    #region 配置目录与默认值

    /// <summary>
    /// 业务配置根目录。
    /// </summary>
    private static readonly string RootConfigDirectory =
        Path.Combine(AppContext.BaseDirectory, "Config");

    /// <summary>
    /// 方案配置文件目录。
    /// </summary>
    private static readonly string SchemeDirectory =
        Path.Combine(RootConfigDirectory, "Scheme");

    /// <summary>
    /// 方案配置文件搜索模式。
    /// </summary>
    private const string SchemeFileSearchPattern = "*.scheme.json";

    #endregion

    #region JSON 序列化选项

    /// <summary>
    /// 方案配置 JSON 序列化选项。
    /// </summary>
    private static readonly JsonSerializerOptions SchemeJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    #endregion

    #region 公开加载保存入口

    /// <summary>
    /// 加载完整方案配置目录。
    /// </summary>
    /// <returns>规范化后的方案配置目录。</returns>
    public static SchemeConfigurationCatalog LoadCatalog()
    {
        SchemeConfigurationCatalog catalog = new()
        {
            Schemes = LoadSchemes()
        };

        return NormalizeCatalog(catalog);
    }

    /// <summary>
    /// 保存完整方案配置目录。
    /// </summary>
    /// <param name="catalog">需要保存的方案配置目录。</param>
    public static void SaveCatalog(SchemeConfigurationCatalog catalog)
    {
        SchemeConfigurationCatalog normalized = NormalizeCatalog(catalog);
        SaveSchemes(normalized.Schemes);
    }

    #endregion

    #region 方案加载保存

    /// <summary>
    /// 从配置目录读取所有方案文件。
    /// </summary>
    /// <returns>读取到的方案集合。</returns>
    private static ObservableCollection<SchemeProfile> LoadSchemes()
    {
        ObservableCollection<SchemeProfile> schemes = new();
        foreach (string filePath in EnumerateConfigFiles(SchemeDirectory, SchemeFileSearchPattern))
        {
            SchemeProfile? scheme = ReadJson<SchemeProfile>(filePath, SchemeJsonOptions);
            if (scheme is not null)
            {
                schemes.Add(scheme);
            }
        }

        return schemes;
    }

    /// <summary>
    /// 将方案集合保存为独立配置文件。
    /// </summary>
    /// <param name="schemes">需要保存的方案集合。</param>
    private static void SaveSchemes(ObservableCollection<SchemeProfile> schemes)
    {
        Directory.CreateDirectory(SchemeDirectory);
        HashSet<string> currentFilePaths = new(StringComparer.OrdinalIgnoreCase);

        foreach (SchemeProfile scheme in schemes)
        {
            string filePath = BuildSchemeFilePath(scheme);
            WriteJson(filePath, scheme, SchemeJsonOptions);
            currentFilePaths.Add(filePath);
        }

        DeleteStaleFiles(SchemeDirectory, SchemeFileSearchPattern, currentFilePaths);
    }

    #endregion

    #region 目录级规范化

    /// <summary>
    /// 克隆并规范化方案配置目录。
    /// </summary>
    /// <param name="catalog">原始方案配置目录。</param>
    /// <returns>可安全使用的方案配置目录。</returns>
    private static SchemeConfigurationCatalog NormalizeCatalog(SchemeConfigurationCatalog? catalog)
    {
        SchemeConfigurationCatalog normalized = new()
        {
            Schemes = new ObservableCollection<SchemeProfile>(
                (catalog?.Schemes ?? new ObservableCollection<SchemeProfile>())
                    .Where(scheme => scheme is not null)
                    .Select(scheme => scheme.Clone()))
        };

        NormalizeSchemes(normalized.Schemes);
        return normalized;
    }

    #endregion

    #region 方案内容规范化

    /// <summary>
    /// 规范化方案集合，补齐方案、工步、步骤的标识与名称。
    /// </summary>
    /// <param name="schemes">需要规范化的方案集合。</param>
    private static void NormalizeSchemes(ObservableCollection<SchemeProfile> schemes)
    {
        HashSet<string> usedIds = new(StringComparer.Ordinal);
        HashSet<string> usedSchemeNames = new(StringComparer.OrdinalIgnoreCase);
        int index = 1;

        foreach (SchemeProfile scheme in schemes)
        {
            scheme.Id = EnsureUniqueId(scheme.Id, usedIds);
            scheme.SchemeName = BuildUniqueName(
                string.IsNullOrWhiteSpace(scheme.SchemeName) ? $"方案 {index}" : scheme.SchemeName.Trim(),
                usedSchemeNames);
            scheme.LastModifiedAt = scheme.LastModifiedAt == default ? DateTime.Now : scheme.LastModifiedAt;

            int stepIndex = 1;
            foreach (SchemeWorkStepItem step in scheme.Steps.Where(step => step is not null))
            {
                step.Id = string.IsNullOrWhiteSpace(step.Id) ? Guid.NewGuid().ToString("N") : step.Id.Trim();
                step.Num = stepIndex;
                if (string.IsNullOrWhiteSpace(step.StepName))
                {
                    step.StepName = $"工步 {stepIndex}";
                }

                int operationIndex = 1;
                foreach (WorkStepOperation operation in step.Operations.Where(op => op is not null))
                {
                    operation.Id = string.IsNullOrWhiteSpace(operation.Id) ? Guid.NewGuid().ToString("N") : operation.Id.Trim();
                    operation.Num = operationIndex;
                    operation.DelayMilliseconds = Math.Max(0, operation.DelayMilliseconds);

                    int paramIndex = 1;
                    foreach (InputParameter param in operation.Parameters.Where(p => p is not null))
                    {
                        param.Id = string.IsNullOrWhiteSpace(param.Id) ? Guid.NewGuid().ToString("N") : param.Id.Trim();
                        param.Num = paramIndex;
                        param.ParameterName = param.ParameterName?.Trim() ?? string.Empty;
                        param.ParameterType = string.IsNullOrWhiteSpace(param.ParameterType) ? "设置值" : param.ParameterType.Trim();
                        param.Value = param.Value?.Trim() ?? string.Empty;
                        paramIndex++;
                    }

                    int returnIndex = 1;
                    foreach (ReturnValue rv in operation.ReturnValues.Where(r => r is not null))
                    {
                        rv.Id = string.IsNullOrWhiteSpace(rv.Id) ? Guid.NewGuid().ToString("N") : rv.Id.Trim();
                        rv.Num = returnIndex;
                        rv.ReturnParameterName = rv.ReturnParameterName?.Trim() ?? string.Empty;
                        rv.JudgeType = rv.JudgeType?.Trim() ?? string.Empty;
                        rv.JudgeSymbols = rv.JudgeSymbols?.Trim() ?? string.Empty;
                        rv.JudgeValue = rv.JudgeValue?.Trim() ?? string.Empty;
                        rv.OriginalUnit = rv.OriginalUnit?.Trim() ?? string.Empty;
                        rv.ShowUnit = rv.ShowUnit?.Trim() ?? string.Empty;
                        rv.DecimalPlaces = Math.Max(0, rv.DecimalPlaces);
                        returnIndex++;
                    }

                    operationIndex++;
                }

                stepIndex++;
            }

            index++;
        }
    }

    #endregion

    #region 唯一性与名称工具

    /// <summary>
    /// 确保字符串标识在当前集合中唯一。
    /// </summary>
    /// <param name="id">候选标识。</param>
    /// <param name="usedIds">已使用标识集合。</param>
    /// <returns>唯一字符串标识。</returns>
    private static string EnsureUniqueId(string id, HashSet<string> usedIds)
    {
        string candidate = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id.Trim();
        while (!usedIds.Add(candidate))
        {
            candidate = Guid.NewGuid().ToString("N");
        }

        return candidate;
    }

    /// <summary>
    /// 基于名称生成不重复的显示名称。
    /// </summary>
    /// <param name="name">候选名称。</param>
    /// <param name="usedNames">已使用名称集合。</param>
    /// <returns>唯一显示名称。</returns>
    private static string BuildUniqueName(string name, HashSet<string> usedNames)
    {
        string baseName = string.IsNullOrWhiteSpace(name) ? "名称" : name.Trim();
        string candidate = baseName;
        int index = 2;

        while (!usedNames.Add(candidate))
        {
            candidate = $"{baseName} {index}";
            index++;
        }

        return candidate;
    }

    #endregion

    #region 文件与数值工具

    /// <summary>
    /// 枚举指定目录下的配置文件。
    /// </summary>
    /// <param name="directory">配置目录。</param>
    /// <param name="searchPattern">文件搜索模式。</param>
    /// <returns>按文件名排序后的配置文件路径。</returns>
    private static IEnumerable<string> EnumerateConfigFiles(string directory, string searchPattern)
    {
        if (!Directory.Exists(directory))
        {
            return Enumerable.Empty<string>();
        }

        return Directory
            .EnumerateFiles(directory, searchPattern, SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 从指定文件反序列化 JSON。
    /// </summary>
    /// <typeparam name="T">目标对象类型。</typeparam>
    /// <param name="filePath">JSON 文件路径。</param>
    /// <param name="options">JSON 序列化选项。</param>
    /// <returns>读取到的对象，失败时返回默认值。</returns>
    private static T? ReadJson<T>(string filePath, JsonSerializerOptions options)
    {
        try
        {
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<T>(json, options);
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// 将对象序列化并写入 JSON 文件。
    /// </summary>
    /// <typeparam name="T">待写入对象类型。</typeparam>
    /// <param name="filePath">JSON 文件路径。</param>
    /// <param name="value">待写入对象。</param>
    /// <param name="options">JSON 序列化选项。</param>
    private static void WriteJson<T>(string filePath, T value, JsonSerializerOptions options)
    {
        string json = JsonSerializer.Serialize(value, options);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// 删除本次保存未生成的历史配置文件。
    /// </summary>
    /// <param name="directory">配置目录。</param>
    /// <param name="searchPattern">文件搜索模式。</param>
    /// <param name="currentFilePaths">本次保存生成的文件路径集合。</param>
    private static void DeleteStaleFiles(string directory, string searchPattern, HashSet<string> currentFilePaths)
    {
        foreach (string filePath in Directory.EnumerateFiles(directory, searchPattern, SearchOption.TopDirectoryOnly))
        {
            if (!currentFilePaths.Contains(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    /// <summary>
    /// 构建方案配置文件路径。
    /// </summary>
    /// <param name="scheme">方案配置。</param>
    /// <returns>方案配置文件路径。</returns>
    private static string BuildSchemeFilePath(SchemeProfile scheme)
    {
        return Path.Combine(SchemeDirectory, $"{SanitizeFileName(scheme.SchemeName)}_{SanitizeFileName(scheme.Id)}.scheme.json");
    }

    /// <summary>
    /// 清理文件名中的非法字符。
    /// </summary>
    /// <param name="fileName">原始文件名。</param>
    /// <returns>可用于文件系统的安全文件名。</returns>
    private static string SanitizeFileName(string fileName)
    {
        string safeName = string.IsNullOrWhiteSpace(fileName) ? "config" : fileName.Trim();
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            safeName = safeName.Replace(invalidChar, '_');
        }

        return safeName;
    }

    #endregion
}
