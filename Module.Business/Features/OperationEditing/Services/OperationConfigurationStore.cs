using Module.Business.Models;
using Shared.Infrastructure.Extensions;
using Shared.Infrastructure.PackMethod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Module.Business.Features.OperationEditing.Services;

/// <summary>
/// 读取步骤编辑器依赖的通信、协议和 Lua 模板配置。
/// </summary>
internal static class OperationConfigurationStore
{
    #region 配置目录与解析规则

    /// <summary>
    /// 通信设备配置目录，用于读取设备名称和设备支持的协议。
    /// </summary>
    private static readonly string CommunicationDirectory =
        Path.Combine(AppContext.BaseDirectory, "Config", "Communication");

    /// <summary>
    /// 协议配置目录，用于读取协议、指令、占位符和返回值定义。
    /// </summary>
    private static readonly string ProtocolDirectory =
        Path.Combine(AppContext.BaseDirectory, "Config", "Protocol");

    /// <summary>
    /// Lua 脚本模板目录。
    /// </summary>
    private static readonly string LuaScriptDirectory =
        Path.Combine(AppContext.BaseDirectory, "Config", "LuaScript");

    /// <summary>
    /// 匹配协议内容模板中的双花括号占位符，例如 {{Address}}。
    /// </summary>
    private static readonly Regex ProtocolPlaceholderRegex =
        new(@"\{\{\s*(?<name>[^{}\r\n]+?)\s*\}\}", RegexOptions.Compiled);

    #endregion

    #region 通信设备配置

    /// <summary>
    /// 加载全部通信设备名称。单个配置文件损坏时跳过该文件，避免影响步骤编辑器使用。
    /// </summary>
    public static IEnumerable<string> LoadDeviceNames()
    {
        if (!Directory.Exists(CommunicationDirectory))
        {
            return Enumerable.Empty<string>();
        }

        List<string> names = new();
        foreach (string filePath in Directory.EnumerateFiles(CommunicationDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(filePath, Encoding.UTF8));
                string name = GetJsonString(document.RootElement, "LocalName");
                if (!string.IsNullOrWhiteSpace(name))
                {
                    names.Add(name.Trim());
                }
            }
            catch
            {
                // 单个配置损坏不应阻断步骤编辑。
            }
        }

        return names
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// 加载指定通信设备声明的协议名称。
    /// </summary>
    /// <param name="operationObject">通信配置中的设备本地名称。</param>
    public static IEnumerable<string> LoadDeviceSupportedProtocolNames(string operationObject)
    {
        if (string.IsNullOrWhiteSpace(operationObject) || !Directory.Exists(CommunicationDirectory))
        {
            return Enumerable.Empty<string>();
        }

        string normalizedOperationObject = operationObject.Trim();
        foreach (string filePath in Directory.EnumerateFiles(CommunicationDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(filePath, Encoding.UTF8));
                if (!string.Equals(
                        GetJsonString(document.RootElement, "LocalName").Trim(),
                        normalizedOperationObject,
                        StringComparison.OrdinalIgnoreCase) ||
                    !document.RootElement.TryGetProperty("SupportedProtocols", out JsonElement protocolsElement) ||
                    protocolsElement.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                return protocolsElement
                    .EnumerateArray()
                    .Select(element => GetJsonString(element, "ProtocolName").Trim())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch
            {
                // 单个配置损坏不应阻断步骤编辑。
            }
        }

        return Enumerable.Empty<string>();
    }

    #endregion

    #region Lua 脚本模板

    /// <summary>
    /// 加载全部 Lua 脚本模板名称，并按名称稳定排序。
    /// </summary>
    public static IEnumerable<string> LoadLuaScriptTemplateNames()
    {
        return Directory.Exists(LuaScriptDirectory)
            ? Directory.EnumerateFiles(LuaScriptDirectory, "*.json", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : Enumerable.Empty<string>();
    }

    /// <summary>
    /// 按名称加载 Lua 脚本模板，兼容明文和 DES 加密两种存储格式。
    /// </summary>
    /// <param name="templateName">不包含扩展名的模板名称。</param>
    public static LuaScriptProfileDocument? LoadLuaScriptTemplate(string templateName)
    {
        if (string.IsNullOrWhiteSpace(templateName) || !Directory.Exists(LuaScriptDirectory))
        {
            return null;
        }

        string? filePath = Directory
            .EnumerateFiles(LuaScriptDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(path => string.Equals(
                Path.GetFileNameWithoutExtension(path),
                templateName.Trim(),
                StringComparison.OrdinalIgnoreCase));
        if (filePath is null)
        {
            return null;
        }

        string storageText = File.ReadAllText(filePath, Encoding.UTF8);
        try
        {
            return JsonHelper.DeserializeObject<LuaScriptProfileDocument>(storageText);
        }
        catch
        {
            return JsonHelper.DeserializeObject<LuaScriptProfileDocument>(storageText.DesDecrypt());
        }
    }

    #endregion

    #region 协议查询

    /// <summary>
    /// 加载全部协议名称。
    /// </summary>
    public static IEnumerable<string> LoadProtocolNames()
    {
        return LoadProtocolSelectionItems()
            .Select(protocol => protocol.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 加载指定协议下的全部指令名称。
    /// </summary>
    public static IEnumerable<string> LoadProtocolCommandNames(string protocolName)
    {
        return LoadProtocolSelectionItems()
            .Where(protocol => string.Equals(protocol.Name, protocolName?.Trim(), StringComparison.OrdinalIgnoreCase))
            .SelectMany(protocol => protocol.Commands.Select(command => command.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 加载指定协议指令中的占位符及其默认值。
    /// </summary>
    public static IReadOnlyList<ProtocolPlaceholderDefinition> LoadProtocolCommandPlaceholders(
        string protocolName,
        string commandName)
    {
        return FindProtocolCommand(protocolName, commandName)?.Placeholders ??
               Array.Empty<ProtocolPlaceholderDefinition>();
    }

    /// <summary>
    /// 加载指定协议指令能够解析出的返回值键。
    /// </summary>
    public static IReadOnlyList<string> LoadProtocolCommandReturnValueKeys(
        string protocolName,
        string commandName)
    {
        return FindProtocolCommand(protocolName, commandName)?.ReturnValueKeys ?? Array.Empty<string>();
    }

    /// <summary>
    /// 在完整协议快照中查找指定指令，供占位符和返回值查询共同使用。
    /// </summary>
    private static ProtocolCommandSelectionItem? FindProtocolCommand(string protocolName, string commandName)
    {
        if (string.IsNullOrWhiteSpace(protocolName) || string.IsNullOrWhiteSpace(commandName))
        {
            return null;
        }

        return LoadProtocolSelectionItems()
            .Where(protocol => string.Equals(protocol.Name, protocolName.Trim(), StringComparison.OrdinalIgnoreCase))
            .SelectMany(protocol => protocol.Commands)
            .FirstOrDefault(command => string.Equals(command.Name, commandName.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region 协议文件加载与解析

    /// <summary>
    /// 从协议目录生成当前协议配置快照。每次调用都重新读取文件，以便配置刷新后立即生效。
    /// </summary>
    internal static IReadOnlyList<ProtocolSelectionItem> LoadProtocolSelectionItems()
    {
        if (!Directory.Exists(ProtocolDirectory))
        {
            return Array.Empty<ProtocolSelectionItem>();
        }

        List<ProtocolSelectionItem> protocols = new();
        foreach (string filePath in Directory.EnumerateFiles(ProtocolDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                string storageText = File.ReadAllText(filePath, Encoding.UTF8);
                string json;
                try
                {
                    json = storageText.DesDecrypt();
                }
                catch
                {
                    json = storageText;
                }

                using JsonDocument document = JsonDocument.Parse(json);
                string protocolName = GetJsonString(document.RootElement, "Name").Trim();
                if (string.IsNullOrWhiteSpace(protocolName))
                {
                    continue;
                }

                List<ProtocolCommandSelectionItem> commands = new();
                if (document.RootElement.TryGetProperty("Commands", out JsonElement commandsElement) &&
                    commandsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement commandElement in commandsElement.EnumerateArray())
                    {
                        string commandName = GetJsonString(commandElement, "Name").Trim();
                        if (!string.IsNullOrWhiteSpace(commandName))
                        {
                            commands.Add(BuildCommand(commandName, commandElement));
                        }
                    }
                }

                if (commands.Count == 0)
                {
                    commands.Add(BuildCommand("指令 1", document.RootElement));
                }

                protocols.Add(new ProtocolSelectionItem(protocolName, commands));
            }
            catch
            {
                // 单个配置损坏或无法解密时跳过。
            }
        }

        return protocols;
    }

    /// <summary>
    /// 将一个协议指令 JSON 节点转换为编辑器使用的指令定义。
    /// </summary>
    private static ProtocolCommandSelectionItem BuildCommand(string name, JsonElement element)
    {
        Dictionary<string, string> values = ParsePlaceholderValues(GetJsonString(element, "PlaceholderValuesText"));
        List<ProtocolPlaceholderDefinition> placeholders = new();
        HashSet<string> seenNames = new(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in ProtocolPlaceholderRegex.Matches(GetJsonString(element, "ContentTemplate")))
        {
            string placeholderName = match.Groups["name"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(placeholderName) && seenNames.Add(placeholderName))
            {
                values.TryGetValue(placeholderName, out string? value);
                placeholders.Add(new ProtocolPlaceholderDefinition(placeholderName, value ?? string.Empty));
            }
        }

        return new ProtocolCommandSelectionItem(name, placeholders, GetJsonStringArray(element, "ParsedResultKeys"));
    }

    /// <summary>
    /// 解析“名称=默认值”格式的占位符默认值文本，忽略空行和注释行。
    /// </summary>
    private static Dictionary<string, string> ParsePlaceholderValues(string text)
    {
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
        foreach (string rawLine in (text ?? string.Empty).Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
        {
            string line = rawLine.Trim();
            int equalsIndex = line.IndexOf('=');
            if (!string.IsNullOrWhiteSpace(line) &&
                !line.StartsWith("#", StringComparison.Ordinal) &&
                !line.StartsWith("//", StringComparison.Ordinal) &&
                equalsIndex > 0)
            {
                values[line[..equalsIndex].Trim()] = line[(equalsIndex + 1)..].Trim();
            }
        }

        return values;
    }

    /// <summary>
    /// 从 JSON 节点安全读取字符串属性，缺失或类型不匹配时返回空字符串。
    /// </summary>
    private static string GetJsonString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement propertyElement) &&
               propertyElement.ValueKind == JsonValueKind.String
            ? propertyElement.GetString() ?? string.Empty
            : string.Empty;
    }

    /// <summary>
    /// 从 JSON 节点安全读取字符串数组，并完成清理、去重和稳定排序。
    /// </summary>
    private static IReadOnlyList<string> GetJsonStringArray(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement propertyElement) &&
               propertyElement.ValueKind == JsonValueKind.Array
            ? propertyElement.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : Array.Empty<string>();
    }

    #endregion
}

#region 配置快照模型

/// <summary>
/// 协议指令占位符定义。
/// </summary>
internal sealed record ProtocolPlaceholderDefinition(string Name, string Value);

/// <summary>
/// 协议及其指令的只读快照。
/// </summary>
internal sealed record ProtocolSelectionItem(
    string Name,
    IReadOnlyList<ProtocolCommandSelectionItem> Commands);

/// <summary>
/// 协议指令、占位符和返回值键的只读快照。
/// </summary>
internal sealed record ProtocolCommandSelectionItem(
    string Name,
    IReadOnlyList<ProtocolPlaceholderDefinition> Placeholders,
    IReadOnlyList<string> ReturnValueKeys);

#endregion
