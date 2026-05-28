using Module.Communication.Models;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Module.Business.Services.BusinessOperations;

/// <summary>
/// 业务绑定解析器。
/// 用于在“步骤编辑只保存设备名称”的前提下，把当前操作对象解析成业务目录可识别的绑定键。
/// 解析顺序如下：
/// 1. 优先使用已保存的 <c>DeviceId</c>；
/// 2. 其次尝试直接把操作对象当作业务绑定键；
/// 3. 若操作对象是通信配置中的设备名称，则继续反查其通信类型并用通信类型作为绑定键。
/// </summary>
internal static class BusinessOperationBindingResolver
{
    #region 字段

    /// <summary>
    /// 通信配置目录；用于根据设备名称读取通信类型。
    /// </summary>
    private static readonly string CommunicationConfigDirectory =
        Path.Combine(AppContext.BaseDirectory, "Config", "Communication");

    #endregion

    #region 对外入口

    /// <summary>
    /// 根据步骤里的操作对象，获取该对象当前可用的业务方法列表。
    /// </summary>
    /// <param name="operationObject">步骤里显示和保存的操作对象，通常是设备名称。</param>
    /// <param name="storedDeviceId">步骤里历史保存的业务绑定键，可能是设备类型或旧值。</param>
    /// <returns>解析后的业务方法描述列表。</returns>
    public static IReadOnlyList<BusinessOperationDescriptor> GetOperationsForOperationObject(
        string? operationObject,
        string? storedDeviceId = null)
    {
        string deviceId = ResolveCatalogDeviceId(operationObject, storedDeviceId);
        return string.IsNullOrWhiteSpace(deviceId)
            ? Array.Empty<BusinessOperationDescriptor>()
            : BusinessOperationCatalog.GetOperations(deviceId);
    }

    /// <summary>
    /// 根据步骤里的操作对象和方法名，查找匹配的业务方法。
    /// </summary>
    /// <param name="operationObject">步骤里显示和保存的操作对象，通常是设备名称。</param>
    /// <param name="storedDeviceId">步骤里历史保存的业务绑定键。</param>
    /// <param name="operationId">业务方法唯一标识。</param>
    /// <returns>匹配到的业务方法；未找到时返回 <c>null</c>。</returns>
    public static BusinessOperationDescriptor? FindOperationForOperationObject(
        string? operationObject,
        string? storedDeviceId,
        string? operationId)
    {
        string deviceId = ResolveCatalogDeviceId(operationObject, storedDeviceId);
        string normalizedOperationId = Normalize(operationId);
        return string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(normalizedOperationId)
            ? null
            : BusinessOperationCatalog.Find(deviceId, normalizedOperationId);
    }

    /// <summary>
    /// 将步骤里的操作对象解析成业务目录使用的绑定键。
    /// 该返回值通常是业务显式注册的 <c>DeviceId</c>，或某个通信类型名。
    /// </summary>
    /// <param name="operationObject">步骤中选中的操作对象，通常是设备名称。</param>
    /// <param name="storedDeviceId">步骤中已保存的业务绑定键。</param>
    /// <returns>最终用于业务目录查询的绑定键。</returns>
    public static string ResolveCatalogDeviceId(string? operationObject, string? storedDeviceId = null)
    {
        string normalizedStoredDeviceId = Normalize(storedDeviceId);
        if (!string.IsNullOrWhiteSpace(normalizedStoredDeviceId) &&
            BusinessOperationCatalog.GetOperations(normalizedStoredDeviceId).Count > 0)
        {
            return normalizedStoredDeviceId;
        }

        string normalizedOperationObject = Normalize(operationObject);
        if (string.IsNullOrWhiteSpace(normalizedOperationObject))
        {
            return normalizedStoredDeviceId;
        }

        if (BusinessOperationCatalog.GetOperations(normalizedOperationObject).Count > 0)
        {
            return normalizedOperationObject;
        }

        if (TryResolveCommunicationDeviceId(normalizedOperationObject, out string communicationDeviceId))
        {
            return communicationDeviceId;
        }

        return !string.IsNullOrWhiteSpace(normalizedStoredDeviceId)
            ? normalizedStoredDeviceId
            : normalizedOperationObject;
    }

    #endregion

    #region 通信类型解析

    /// <summary>
    /// 根据设备名称在通信配置目录中查找其通信类型。
    /// </summary>
    /// <param name="operationObject">步骤中选中的设备名称。</param>
    /// <param name="communicationType">找到的通信类型。</param>
    /// <returns>找到并成功解析时返回 <c>true</c>。</returns>
    private static bool TryResolveCommunicationDeviceId(string operationObject, out string deviceId)
    {
        deviceId = string.Empty;
        if (string.IsNullOrWhiteSpace(operationObject) || !Directory.Exists(CommunicationConfigDirectory))
        {
            return false;
        }

        foreach (string filePath in Directory.EnumerateFiles(CommunicationConfigDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(filePath, Encoding.UTF8));
                if (!document.RootElement.TryGetProperty("LocalName", out JsonElement localNameElement) ||
                    !TextEquals(localNameElement.GetString(), operationObject))
                {
                    continue;
                }

                if (!TryReadCommunicationTypeId(document.RootElement, out string typeId))
                {
                    return false;
                }

                if (BusinessOperationCatalog.GetOperations(typeId).Count > 0)
                {
                    deviceId = typeId;
                    return true;
                }

                DeviceCommunicationConfigRegistry registry = DeviceCommunicationConfigRegistry.Default;
                if (!registry.Contains(typeId))
                {
                    return false;
                }

                string runtimeType = registry
                    .Get(typeId)
                    .RuntimeType
                    .ToString();
                if (BusinessOperationCatalog.GetOperations(runtimeType).Count > 0)
                {
                    deviceId = runtimeType;
                    return true;
                }

                return false;
            }
            catch
            {
                // Ignore broken config files and keep fallback behavior.
            }
        }

        return false;
    }

    /// <summary>
    /// 从单个通信配置 JSON 节点中读取通信类型。
    /// 同时兼容数字枚举值和字符串枚举名两种存储方式。
    /// </summary>
    /// <param name="rootElement">通信配置文件根节点。</param>
    /// <param name="communicationType">解析出的通信类型。</param>
    /// <returns>解析成功时返回 <c>true</c>。</returns>
    private static bool TryReadCommunicationTypeId(JsonElement rootElement, out string typeId)
    {
        typeId = string.Empty;
        if (!rootElement.TryGetProperty("TypeId", out JsonElement typeElement) ||
            typeElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        typeId = typeElement.GetString()?.Trim() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(typeId);
    }

    #endregion

    #region 基础工具

    /// <summary>
    /// 规范化文本，统一处理空值和首尾空白。
    /// </summary>
    private static string Normalize(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// 忽略大小写和首尾空白比较文本。
    /// </summary>
    private static bool TextEquals(string? left, string? right)
    {
        return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
