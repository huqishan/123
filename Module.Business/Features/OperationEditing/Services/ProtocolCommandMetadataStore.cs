using Shared.Infrastructure.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Module.Business.Features.OperationEditing.Services;

/// <summary>
/// 协议指令返回值元数据。
/// </summary>
public sealed record ProtocolCommandReturnMetadata(
    bool IsSendOnly,
    IReadOnlyList<string> ReturnValueKeys);

/// <summary>
/// 从协议配置文件读取指令返回值元数据。
/// </summary>
public static class ProtocolCommandMetadataStore
{
    #region 配置路径

    /// <summary>
    /// 协议配置文件所在目录。
    /// </summary>
    private static readonly string ProtocolConfigDirectory =
        Path.Combine(AppContext.BaseDirectory, "Config", "Protocol");

    #endregion

    #region 元数据读取

    /// <summary>
    /// 获取指定协议指令的返回值元数据。
    /// </summary>
    /// <param name="protocolName">协议名称。</param>
    /// <param name="commandName">指令名称。</param>
    /// <returns>指令返回值元数据。</returns>
    public static ProtocolCommandReturnMetadata GetReturnMetadata(
        string? protocolName,
        string? commandName)
    {
        string normalizedProtocolName = protocolName?.Trim() ?? string.Empty;
        string normalizedCommandName = commandName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedProtocolName) ||
            string.IsNullOrWhiteSpace(normalizedCommandName) ||
            !Directory.Exists(ProtocolConfigDirectory))
        {
            return EmptyMetadata();
        }

        foreach (string filePath in Directory.EnumerateFiles(ProtocolConfigDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(ReadPossiblyEncryptedText(filePath));
                JsonElement root = document.RootElement;
                if (!TextEquals(GetJsonString(root, "Name"), normalizedProtocolName))
                {
                    continue;
                }

                if (!root.TryGetProperty("Commands", out JsonElement commandsElement) ||
                    commandsElement.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (JsonElement commandElement in commandsElement.EnumerateArray())
                {
                    if (TextEquals(GetJsonString(commandElement, "Name"), normalizedCommandName))
                    {
                        return CreateMetadata(commandElement);
                    }
                }
            }
            catch
            {
                // 忽略损坏的协议文件，避免配置界面无法打开。
            }
        }

        return EmptyMetadata();
    }

    /// <summary>
    /// 从协议指令 JSON 节点构建返回值元数据。
    /// </summary>
    /// <param name="command">协议指令 JSON 节点。</param>
    /// <returns>指令返回值元数据。</returns>
    private static ProtocolCommandReturnMetadata CreateMetadata(JsonElement command)
    {
        bool waitForResponse = GetJsonBool(command, "WaitForResponse", defaultValue: true);
        bool isParseOnly = GetJsonBool(command, "IsParseOnly", defaultValue: false);
        bool isSendOnly = !waitForResponse && !isParseOnly;
        return new ProtocolCommandReturnMetadata(
            isSendOnly,
            isSendOnly ? Array.Empty<string>() : GetJsonStringArray(command, "ParsedResultKeys"));
    }

    /// <summary>
    /// 创建空返回值元数据。
    /// </summary>
    /// <returns>空返回值元数据。</returns>
    private static ProtocolCommandReturnMetadata EmptyMetadata()
    {
        return new ProtocolCommandReturnMetadata(false, Array.Empty<string>());
    }

    #endregion

    #region 文件与 JSON 工具

    /// <summary>
    /// 读取可能经过加密保存的协议配置文本。
    /// </summary>
    /// <param name="filePath">协议配置文件路径。</param>
    /// <returns>解密后或原始的配置文本。</returns>
    private static string ReadPossiblyEncryptedText(string filePath)
    {
        string storageText = File.ReadAllText(filePath, Encoding.UTF8);
        try
        {
            return storageText.DesDecrypt();
        }
        catch
        {
            return storageText;
        }
    }

    /// <summary>
    /// 从 JSON 节点读取字符串属性。
    /// </summary>
    /// <param name="element">JSON 节点。</param>
    /// <param name="propertyName">属性名称。</param>
    /// <returns>字符串属性值。</returns>
    private static string GetJsonString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement propertyElement)
            ? propertyElement.GetString() ?? string.Empty
            : string.Empty;
    }

    /// <summary>
    /// 从 JSON 节点读取字符串数组属性。
    /// </summary>
    /// <param name="element">JSON 节点。</param>
    /// <param name="propertyName">属性名称。</param>
    /// <returns>去重并排序后的字符串数组。</returns>
    private static IReadOnlyList<string> GetJsonStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement propertyElement) ||
            propertyElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return propertyElement
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// 从 JSON 节点读取布尔属性。
    /// </summary>
    /// <param name="element">JSON 节点。</param>
    /// <param name="propertyName">属性名称。</param>
    /// <param name="defaultValue">属性不存在或无法解析时的默认值。</param>
    /// <returns>布尔属性值。</returns>
    private static bool GetJsonBool(JsonElement element, string propertyName, bool defaultValue)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement propertyElement))
        {
            return defaultValue;
        }

        return propertyElement.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(propertyElement.GetString(), out bool value) => value,
            _ => defaultValue
        };
    }

    /// <summary>
    /// 按忽略大小写和首尾空白的规则比较文本。
    /// </summary>
    /// <param name="left">左侧文本。</param>
    /// <param name="right">右侧文本。</param>
    /// <returns>文本相等时返回 true。</returns>
    private static bool TextEquals(string? left, string? right)
    {
        return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
