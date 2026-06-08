using ControlLibrary.Controls.FlowchartEditor.Models;
using Module.Business.Models;
using Module.Business.Features.StationConfiguration;
using Module.Business.Features.SchemeConfiguration;
using Module.Business.Features.LuaScript;
using Module.Business.Services.BusinessOperations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Module.Business.Services;

/// <summary>
/// 业务配置持久化工具，负责方案、工位和流程图配置的加载、保存与规范化�?/// </summary>
public static class BusinessConfigurationStore
{
    #region 配置目录与默认�?    /// <summary>
    /// 业务配置根目录�?    /// </summary>
    private static readonly string RootConfigDirectory =
        Path.Combine(AppContext.BaseDirectory, "Config");

    /// <summary>
    /// 方案配置文件目录�?    /// </summary>
    private static readonly string SchemeDirectory =
        Path.Combine(RootConfigDirectory, "Scheme");

    /// <summary>
    /// 工位配置文件目录�?    /// </summary>
    private static readonly string StationDirectory =
        Path.Combine(RootConfigDirectory, "Station");

    /// <summary>
    /// 方案配置文件搜索模式�?    /// </summary>
    private const string SchemeFileSearchPattern = "*.scheme.json";

    /// <summary>
    /// 工位配置文件搜索模式�?    /// </summary>
    private const string StationFileSearchPattern = "*.station.json";

    /// <summary>
    /// 流程图节点默认宽度�?    /// </summary>
    private const double DefaultNodeWidth = 150;

    /// <summary>
    /// 流程图节点默认高度�?    /// </summary>
    private const double DefaultNodeHeight = 70;

    #endregion

    #region JSON 序列化选项

    /// <summary>
    /// 方案和工位配�?JSON 序列化选项�?    /// </summary>
    private static readonly JsonSerializerOptions SchemeJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    #endregion

    #region 公开加载保存入口

    /// <summary>
    /// 加载完整方案配置目录�?    /// </summary>
    /// <returns>规范化后的方案配置目录�?/returns>
    public static SchemeConfigurationCatalog LoadCatalog()
    {
        SchemeConfigurationCatalog catalog = new()
        {
            Schemes = LoadSchemes()
        };

        return NormalizeCatalog(catalog);
    }

    /// <summary>
    /// 保存完整方案配置目录�?    /// </summary>
    /// <param name="catalog">需要保存的方案配置目录�?/param>
    public static void SaveCatalog(SchemeConfigurationCatalog catalog)
    {
        SchemeConfigurationCatalog normalized = NormalizeCatalog(catalog);

        SaveSchemes(normalized.Schemes);
    }

    /// <summary>
    /// 加载完整工位配置目录�?    /// </summary>
    /// <returns>规范化后的工位配置目录�?/returns>
    public static StationConfigurationCatalog LoadStationCatalog()
    {
        StationConfigurationCatalog catalog = new()
        {
            Stations = LoadStations()
        };

        return NormalizeStationCatalog(catalog);
    }

    /// <summary>
    /// 保存完整工位配置目录�?    /// </summary>
    /// <param name="catalog">需要保存的工位配置目录�?/param>
    public static void SaveStationCatalog(StationConfigurationCatalog catalog)
    {
        StationConfigurationCatalog normalized = NormalizeStationCatalog(catalog);
        SaveStations(normalized.Stations);
    }

    #endregion

    #region 方案加载保存

    /// <summary>
    /// 从配置目录读取所有方案文件�?    /// </summary>
    /// <returns>读取到的方案集合�?/returns>
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
    /// 将方案集合保存为独立配置文件�?    /// </summary>
    /// <param name="schemes">需要保存的方案集合�?/param>
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

    #region 工位加载保存

    /// <summary>
    /// 从配置目录读取所有工位文件�?    /// </summary>
    /// <returns>读取到的工位集合�?/returns>
    private static ObservableCollection<StationProfile> LoadStations()
    {
        ObservableCollection<StationProfile> stations = new();
        foreach (string filePath in EnumerateConfigFiles(StationDirectory, StationFileSearchPattern))
        {
            StationProfile? station = ReadJson<StationProfile>(filePath, SchemeJsonOptions);
            if (station is not null)
            {
                stations.Add(station);
            }
        }

        return stations;
    }

    /// <summary>
    /// 将工位集合保存为独立配置文件�?    /// </summary>
    /// <param name="stations">需要保存的工位集合�?/param>
    private static void SaveStations(ObservableCollection<StationProfile> stations)
    {
        Directory.CreateDirectory(StationDirectory);
        HashSet<string> currentFilePaths = new(StringComparer.OrdinalIgnoreCase);

        foreach (StationProfile station in stations)
        {
            string filePath = BuildStationFilePath(station);
            WriteJson(filePath, station, SchemeJsonOptions);
            currentFilePaths.Add(filePath);
        }

        DeleteStaleFiles(StationDirectory, StationFileSearchPattern, currentFilePaths);
    }

    #endregion

    #region 目录级规范化
    /// <summary>
    /// 克隆并规范化方案配置目录�?    /// </summary>
    /// <param name="catalog">原始方案配置目录�?/param>
    /// <returns>可安全使用的方案配置目录�?/returns>
    private static SchemeConfigurationCatalog NormalizeCatalog(SchemeConfigurationCatalog? catalog)
    {
        SchemeConfigurationCatalog normalized = new()
        {
            WorkSteps = new ObservableCollection<WorkStepProfile>(
                (catalog?.WorkSteps ?? new ObservableCollection<WorkStepProfile>())
                    .Where(step => step is not null)
                    .Select(step => step.Clone())),
            Schemes = new ObservableCollection<SchemeProfile>(
                (catalog?.Schemes ?? new ObservableCollection<SchemeProfile>())
                    .Where(scheme => scheme is not null)
                    .Select(scheme => scheme.Clone()))
        };

        NormalizeWorkSteps(normalized.WorkSteps);
        NormalizeSchemes(normalized.Schemes, normalized.WorkSteps);
        return normalized;
    }

    /// <summary>
    /// 克隆并规范化工位配置目录�?    /// </summary>
    /// <param name="catalog">原始工位配置目录�?/param>
    /// <returns>可安全使用的工位配置目录�?/returns>
    private static StationConfigurationCatalog NormalizeStationCatalog(StationConfigurationCatalog? catalog)
    {
        StationConfigurationCatalog normalized = new()
        {
            Stations = new ObservableCollection<StationProfile>(
                (catalog?.Stations ?? new ObservableCollection<StationProfile>())
                    .Where(station => station is not null)
                    .Select(station => station.Clone()))
        };

        NormalizeStations(normalized.Stations);
        return normalized;
    }

    #endregion

    #region 工步与操作规范化

    /// <summary>
    /// 规范化工步集合，补齐标识、名称和操作列表�?    /// </summary>
    /// <param name="workSteps">需要规范化的工步集合�?/param>
    private static void NormalizeWorkSteps(ObservableCollection<WorkStepProfile> workSteps)
    {
        HashSet<string> usedIds = new(StringComparer.Ordinal);
        HashSet<string> usedStepNames = new(StringComparer.OrdinalIgnoreCase);
        int index = 1;

        foreach (WorkStepProfile workStep in workSteps)
        {
            workStep.Id = EnsureUniqueId(workStep.Id, usedIds);
            string fallbackStepName = $"工步 {index}";
            workStep.StepName = BuildUniqueName(
                string.IsNullOrWhiteSpace(workStep.StepName) ? fallbackStepName : workStep.StepName.Trim(),
                usedStepNames);
            workStep.Steps = new ObservableCollection<WorkStepOperation>(
                workStep.Steps
                    .Where(operation => operation is not null)
                    .Select(NormalizeOperation));
            index++;
        }
    }

    /// <summary>
    /// 规范化单个工步操作�?    /// </summary>
    /// <param name="operation">原始工步操作�?/param>
    /// <returns>规范化后的工步操作�?/returns>
    internal static WorkStepOperation NormalizeWorkStepOperation(WorkStepOperation? operation)
    {
        return NormalizeOperation(operation ?? new WorkStepOperation());
    }

    private static WorkStepOperation NormalizeOperation(WorkStepOperation operation)
    {
        string operationObject = ResolveOperationObject(operation);
        bool isLuaOperation = IsLuaOperationObject(operationObject);
        bool isJudgeOperation = !isLuaOperation && IsJudgeOperationObject(operationObject);
        bool isSystemOperation = !isLuaOperation && !isJudgeOperation && IsNormalizedSystemOperationObject(operationObject);
        string invokeMethod = isLuaOperation
            ? "Lua"
            : ResolveInvokeMethod(operation, isJudgeOperation, isSystemOperation);
        ObservableCollection<WorkStepOperationParameter> inputParameters = isLuaOperation
            ? new ObservableCollection<WorkStepOperationParameter>()
            : new ObservableCollection<WorkStepOperationParameter>(
                ResolveInputParameters(operation)
                    .Where(parameter => parameter is not null)
                    .Select((parameter, index) => NormalizeOperationParameter(parameter, index))
                    .OrderBy(parameter => parameter.Sequence));
        ObservableCollection<WorkStepOperationParameter> returnParameters = isLuaOperation
            ? new ObservableCollection<WorkStepOperationParameter>()
            : new ObservableCollection<WorkStepOperationParameter>(
                ResolveReturnParameters(operation, operationObject, invokeMethod)
                    .Where(parameter => parameter is not null)
                    .Select((parameter, index) => NormalizeOperationParameter(parameter, index))
                    .OrderBy(parameter => parameter.Sequence));

        WorkStepOperation normalized = new()
        {
            Id = string.IsNullOrWhiteSpace(operation.Id) ? Guid.NewGuid().ToString("N") : operation.Id.Trim(),
            OperationObject = operationObject,
            InvokeMethod = invokeMethod,
            LuaScript = isLuaOperation ? operation.LuaScript ?? string.Empty : string.Empty,
            DelayMilliseconds = Math.Max(0, operation.DelayMilliseconds),
            Remark = operation.Remark?.Trim() ?? string.Empty,
            InputParameters = inputParameters,
            ReturnParameters = returnParameters
        };
        normalized.AreParametersModified = operation.AreParametersModified;
        normalized.DisplayOrder = operation.DisplayOrder;

        string legacyDeviceId = GetLegacyString(operation, "DeviceId");
        normalized.DeviceId = string.IsNullOrWhiteSpace(legacyDeviceId)
            ? operation.DeviceId
            : legacyDeviceId;
        normalized.OperationId = string.IsNullOrWhiteSpace(operation.OperationId)
            ? invokeMethod
            : operation.OperationId.Trim();
        if (!isSystemOperation && !isJudgeOperation && !isLuaOperation &&
            TryResolveProtocolHint(operation, operationObject, invokeMethod, out string protocolName, out string commandName))
        {
            normalized.ProtocolName = protocolName;
            normalized.CommandName = commandName;
        }

        return normalized;
    }

    private static string ResolveOperationObject(WorkStepOperation operation)
    {
        string operationType = string.IsNullOrWhiteSpace(operation.OperationType)
            ? GetLegacyString(operation, "OperationType")
            : operation.OperationType;
        string operationObject = string.IsNullOrWhiteSpace(operation.OperationObject)
            ? GetLegacyString(operation, "OperationObject")
            : operation.OperationObject;
        string deviceId = string.IsNullOrWhiteSpace(operation.DeviceId)
            ? GetLegacyString(operation, "DeviceId")
            : operation.DeviceId;

        if (IsLuaOperationObject(operationType) ||
            IsLuaOperationObject(operationObject))
        {
            return "Lua";
        }

        if (IsJudgeOperationObject(operationType) ||
            IsJudgeOperationObject(operationObject))
        {
            return "�ж�";
        }

        if (IsNormalizedSystemOperationType(operationType) ||
            IsNormalizedSystemOperationObject(operationObject))
        {
            return "System";
        }

        if (!string.IsNullOrWhiteSpace(operationObject))
        {
            return operationObject.Trim();
        }

        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            return deviceId.Trim();
        }

        return "System";
    }

    private static WorkStepOperationParameter NormalizeOperationParameter(WorkStepOperationParameter parameter, int index)
    {
        return new WorkStepOperationParameter
        {
            Id = string.IsNullOrWhiteSpace(parameter.Id) ? Guid.NewGuid().ToString("N") : parameter.Id.Trim(),
            Sequence = parameter.Sequence <= 0 ? index + 1 : parameter.Sequence,
            Name = string.IsNullOrWhiteSpace(parameter.Name) ? "����ֵ" : parameter.Name.Trim(),
            ParameterName = string.IsNullOrWhiteSpace(parameter.ParameterName)
                ? parameter.Description?.Trim() ?? string.Empty
                : parameter.ParameterName.Trim(),
            ValueType = parameter.ValueType?.Trim() ?? string.Empty,
            Value = parameter.Value?.Trim() ?? string.Empty,
            Remark = parameter.Remark?.Trim() ?? string.Empty,
            ShowDataToView = parameter.ShowDataToView,
            ViewDataName = parameter.ViewDataName?.Trim() ?? string.Empty,
            ViewJudgeType = parameter.ViewJudgeType?.Trim() ?? string.Empty,
            ViewJudgeCondition = parameter.ViewJudgeCondition?.Trim() ?? string.Empty
        };
    }

    private static string ResolveInvokeMethod(WorkStepOperation operation, bool isJudgeOperation, bool isSystemOperation)
    {
        string invokeMethod = operation.InvokeMethod?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(invokeMethod))
        {
            return invokeMethod;
        }

        string operationId = string.IsNullOrWhiteSpace(operation.OperationId)
            ? GetLegacyString(operation, "OperationId")
            : operation.OperationId;
        if (!string.IsNullOrWhiteSpace(operationId))
        {
            return operationId.Trim();
        }

        string commandName = string.IsNullOrWhiteSpace(operation.CommandName)
            ? GetLegacyString(operation, "CommandName")
            : operation.CommandName;
        if (!string.IsNullOrWhiteSpace(commandName))
        {
            return commandName.Trim();
        }

        return isJudgeOperation
            ? string.Empty
            : isSystemOperation
                ? "�ȴ�"
                : "ָ��";
    }

    private static IEnumerable<WorkStepOperationParameter> ResolveInputParameters(WorkStepOperation operation)
    {
        if (operation.InputParameters.Count > 0)
        {
            return operation.InputParameters;
        }

        return ReadLegacyParameters(operation, "Parameters");
    }

    private static IEnumerable<WorkStepOperationParameter> ResolveReturnParameters(
        WorkStepOperation operation,
        string operationObject,
        string invokeMethod)
    {
        if (operation.ReturnParameters.Count > 0)
        {
            return operation.ReturnParameters;
        }

        List<WorkStepOperationParameter> inferredParameters = CreateInferredReturnParameters(operation, operationObject, invokeMethod);
        string legacyReturnValue = GetLegacyString(operation, "ReturnValue");
        bool legacyShowDataToView = GetLegacyBool(operation, "ShowDataToView");
        string legacyViewDataName = GetLegacyString(operation, "ViewDataName");
        string legacyViewJudgeType = GetLegacyString(operation, "ViewJudgeType");
        string legacyViewJudgeCondition = GetLegacyString(operation, "ViewJudgeCondition");
        bool hasLegacyDisplaySettings = legacyShowDataToView ||
                                        !string.IsNullOrWhiteSpace(legacyViewDataName) ||
                                        !string.IsNullOrWhiteSpace(legacyViewJudgeType) ||
                                        !string.IsNullOrWhiteSpace(legacyViewJudgeCondition);

        if (inferredParameters.Count == 0 && string.IsNullOrWhiteSpace(legacyReturnValue) && !hasLegacyDisplaySettings)
        {
            return Array.Empty<WorkStepOperationParameter>();
        }

        if (inferredParameters.Count == 0)
        {
            string fallbackKey = !string.IsNullOrWhiteSpace(legacyReturnValue)
                ? legacyReturnValue.Trim()
                : !string.IsNullOrWhiteSpace(legacyViewDataName)
                    ? legacyViewDataName.Trim()
                    : invokeMethod;
            inferredParameters.Add(new WorkStepOperationParameter
            {
                Sequence = 1,
                Name = "����ֵ",
                ParameterName = fallbackKey,
                Value = fallbackKey
            });
        }

        if (!string.IsNullOrWhiteSpace(legacyReturnValue))
        {
            WorkStepOperationParameter? matched = inferredParameters.FirstOrDefault(parameter =>
                string.Equals(parameter.ParameterName, legacyReturnValue.Trim(), StringComparison.OrdinalIgnoreCase));
            if (matched is null)
            {
                inferredParameters.Insert(0, new WorkStepOperationParameter
                {
                    Sequence = 1,
                    Name = "����ֵ",
                    ParameterName = legacyReturnValue.Trim(),
                    Value = legacyReturnValue.Trim()
                });
                matched = inferredParameters[0];
            }

            matched.ShowDataToView = legacyShowDataToView;
            matched.ViewDataName = legacyViewDataName;
            matched.ViewJudgeType = legacyViewJudgeType;
            matched.ViewJudgeCondition = legacyViewJudgeCondition;
        }
        else if (hasLegacyDisplaySettings)
        {
            WorkStepOperationParameter matched = inferredParameters[0];
            matched.ShowDataToView = legacyShowDataToView;
            matched.ViewDataName = legacyViewDataName;
            matched.ViewJudgeType = legacyViewJudgeType;
            matched.ViewJudgeCondition = legacyViewJudgeCondition;
        }

        for (int index = 0; index < inferredParameters.Count; index++)
        {
            inferredParameters[index].Sequence = index + 1;
        }

        return inferredParameters;
    }

    private static List<WorkStepOperationParameter> CreateInferredReturnParameters(
        WorkStepOperation operation,
        string operationObject,
        string invokeMethod)
    {
        WorkStepOperation probeOperation = new()
        {
            OperationObject = operationObject,
            InvokeMethod = invokeMethod,
            DeviceId = string.IsNullOrWhiteSpace(operation.DeviceId)
                ? GetLegacyString(operation, "DeviceId")
                : operation.DeviceId
        };
        if (WorkStepOperationRuntimeMetadata.TryResolveBusinessOperation(
                probeOperation,
                out _,
                out _,
                out BusinessOperationDescriptor? descriptor) &&
            descriptor is not null &&
            !string.Equals(descriptor.ReturnTypeName, "void", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new WorkStepOperationParameter
                {
                    Sequence = 1,
                    Name = "����ֵ",
                    ParameterName = descriptor.OperationId,
                    ValueType = descriptor.ReturnTypeName,
                    Value = descriptor.OperationId,
                    Remark = string.IsNullOrWhiteSpace(descriptor.Description)
                        ? descriptor.DisplayName
                        : descriptor.Description
                }
            ];
        }

        if (TryResolveProtocolHint(operation, operationObject, invokeMethod, out string protocolName, out string commandName))
        {
            ProtocolCommandReturnMetadata metadata = ProtocolCommandMetadataStore.GetReturnMetadata(protocolName, commandName);
            return metadata.ReturnValueKeys
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Select((key, index) => new WorkStepOperationParameter
                {
                    Sequence = index + 1,
                    Name = "����ֵ",
                    ParameterName = key.Trim(),
                    Value = key.Trim(),
                    Remark = "���ز���"
                })
                .ToList();
        }

        return new List<WorkStepOperationParameter>();
    }

    private static bool TryResolveProtocolHint(
        WorkStepOperation operation,
        string operationObject,
        string invokeMethod,
        out string protocolName,
        out string commandName)
    {
        protocolName = string.IsNullOrWhiteSpace(operation.ProtocolName)
            ? GetLegacyString(operation, "ProtocolName")
            : operation.ProtocolName;
        commandName = string.IsNullOrWhiteSpace(operation.CommandName)
            ? GetLegacyString(operation, "CommandName")
            : operation.CommandName;
        if (!string.IsNullOrWhiteSpace(protocolName) && !string.IsNullOrWhiteSpace(commandName))
        {
            protocolName = protocolName.Trim();
            commandName = commandName.Trim();
            return true;
        }

        return WorkStepOperationRuntimeMetadata.TryResolveProtocolCommand(
            new WorkStepOperation
            {
                OperationObject = operationObject,
                InvokeMethod = invokeMethod,
                ProtocolName = protocolName,
                CommandName = commandName
            },
            out protocolName,
            out commandName);
    }

    private static IEnumerable<WorkStepOperationParameter> ReadLegacyParameters(WorkStepOperation operation, string propertyName)
    {
        if (operation.LegacyData is null ||
            !operation.LegacyData.TryGetValue(propertyName, out JsonElement element) ||
            element.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<WorkStepOperationParameter>();
        }

        List<WorkStepOperationParameter> parameters = new();
        int index = 1;
        foreach (JsonElement item in element.EnumerateArray())
        {
            parameters.Add(new WorkStepOperationParameter
            {
                Id = GetJsonString(item, "Id"),
                Sequence = GetJsonInt(item, "Sequence", index),
                Name = GetJsonString(item, "Name"),
                ParameterName = GetJsonString(item, "ParameterName"),
                ValueType = GetJsonString(item, "ValueType"),
                Value = GetJsonString(item, "Value"),
                Remark = GetJsonString(item, "Remark")
            });
            index++;
        }

        return parameters;
    }

    private static string GetLegacyString(WorkStepOperation operation, string propertyName)
    {
        if (operation.LegacyData is null ||
            !operation.LegacyData.TryGetValue(propertyName, out JsonElement element))
        {
            return string.Empty;
        }

        return GetJsonString(element);
    }

    private static bool GetLegacyBool(WorkStepOperation operation, string propertyName)
    {
        if (operation.LegacyData is null ||
            !operation.LegacyData.TryGetValue(propertyName, out JsonElement element))
        {
            return false;
        }

        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(element.GetString(), out bool parsedValue) => parsedValue,
            _ => false
        };
    }

    private static int GetJsonInt(JsonElement element, string propertyName, int defaultValue)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement propertyElement))
        {
            return defaultValue;
        }

        if (propertyElement.ValueKind == JsonValueKind.Number &&
            propertyElement.TryGetInt32(out int parsedValue))
        {
            return parsedValue;
        }

        if (propertyElement.ValueKind == JsonValueKind.String &&
            int.TryParse(propertyElement.GetString(), out parsedValue))
        {
            return parsedValue;
        }

        return defaultValue;
    }

    private static string GetJsonString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement propertyElement)
            ? GetJsonString(propertyElement)
            : string.Empty;
    }

    private static string GetJsonString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString()?.Trim() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => string.Empty
        };
    }

    #endregion

    #region 方案内容规范�?    /// <summary>
    /// 规范化方案集合，并重建方案工步参数快照�?    /// </summary>
    /// <param name="schemes">需要规范化的方案集合�?/param>
    /// <param name="workSteps">可引用的工步集合�?/param>
    private static void NormalizeSchemes(
        ObservableCollection<SchemeProfile> schemes,
        ObservableCollection<WorkStepProfile> workSteps)
    {
        HashSet<string> usedIds = new(StringComparer.Ordinal);
        HashSet<string> usedSchemeNames = new(StringComparer.OrdinalIgnoreCase);
        int index = 1;

        foreach (SchemeProfile scheme in schemes)
        {
            DateTime normalizedLastModifiedAt = scheme.LastModifiedAt == default ? DateTime.Now : scheme.LastModifiedAt;
            scheme.Id = EnsureUniqueId(scheme.Id, usedIds);
            scheme.SchemeName = BuildUniqueName(
                string.IsNullOrWhiteSpace(scheme.SchemeName) ? $"方案 {index}" : scheme.SchemeName.Trim(),
                usedSchemeNames);

            ObservableCollection<WorkStepProfile> normalizedSteps = new();
            foreach (WorkStepProfile step in scheme.Steps.Where(step => step is not null))
            {
                WorkStepProfile normalizedStep = step.Clone();
                normalizedStep.Id = string.IsNullOrWhiteSpace(step.Id) ? Guid.NewGuid().ToString("N") : step.Id.Trim();

                if (string.IsNullOrWhiteSpace(normalizedStep.StepName))
                {
                    normalizedStep.StepName = $"工步 {normalizedSteps.Count + 1}";
                }

                normalizedStep.Parameters = WorkStepProfile.CreateParametersFromOperations(
                    normalizedStep.Operations,
                    step.Parameters);
                normalizedSteps.Add(normalizedStep);
            }

            scheme.Steps = normalizedSteps;
            scheme.LastModifiedAt = normalizedLastModifiedAt;
            index++;
        }
    }

    #endregion

    #region 工位与流程图规范�?    /// <summary>
    /// 规范化流程图配置集合�?    /// </summary>
    /// <param name="flowcharts">需要规范化的流程图集合�?/param>
    private static void NormalizeFlowcharts(ObservableCollection<FlowchartProfile> flowcharts)
    {
        HashSet<string> usedIds = new(StringComparer.Ordinal);
        HashSet<string> usedNames = new(StringComparer.OrdinalIgnoreCase);
        int index = 1;

        foreach (FlowchartProfile flowchart in flowcharts)
        {
            flowchart.Id = EnsureUniqueId(flowchart.Id, usedIds);
            flowchart.Name = BuildUniqueName(
                string.IsNullOrWhiteSpace(flowchart.Name) ? $"����ͼ {index}" : flowchart.Name.Trim(),
                usedNames);
            flowchart.Document = NormalizeFlowchartDocument(flowchart.Document);
            index++;
        }
    }

    /// <summary>
    /// 规范化工位集合，补齐工位名称、编码和流程图文档�?    /// </summary>
    /// <param name="stations">需要规范化的工位集合�?/param>
    private static void NormalizeStations(ObservableCollection<StationProfile> stations)
    {
        HashSet<string> usedIds = new(StringComparer.Ordinal);
        HashSet<string> usedNames = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> usedCodes = new(StringComparer.OrdinalIgnoreCase);
        int index = 1;

        foreach (StationProfile station in stations)
        {
            station.Id = EnsureUniqueId(station.Id, usedIds);
            station.StationName = BuildUniqueName(
                string.IsNullOrWhiteSpace(station.StationName) ? $"工位 {index}" : station.StationName.Trim(),
                usedNames);
            station.StationCode = BuildUniqueStationCode(station.StationCode, usedCodes, index);
            station.LastModifiedAt = station.LastModifiedAt == default ? DateTime.Now : station.LastModifiedAt;
            station.FlowchartDocument = NormalizeFlowchartDocument(station.FlowchartDocument);
            index++;
        }
    }

    /// <summary>
    /// 规范化流程图文档，过滤无效节点和连线�?    /// </summary>
    /// <param name="document">原始流程图文档�?/param>
    /// <returns>规范化后的流程图文档�?/returns>
    private static FlowchartDocument NormalizeFlowchartDocument(FlowchartDocument? document)
    {
        if (document is null)
        {
            return new FlowchartDocument();
        }

        HashSet<Guid> usedNodeIds = new();
        Dictionary<Guid, Guid> nodeIdMap = new();
        List<FlowchartNodeDocument> nodes = new();

        foreach (FlowchartNodeDocument node in document.Nodes ?? new List<FlowchartNodeDocument>())
        {
            Guid originalId = node.Id;
            Guid nodeId = EnsureUniqueGuid(originalId, usedNodeIds);

            if (originalId != Guid.Empty)
            {
                nodeIdMap[originalId] = nodeId;
            }

            nodes.Add(new FlowchartNodeDocument
            {
                Id = nodeId,
                Text = string.IsNullOrWhiteSpace(node.Text) ? "处理" : node.Text.Trim(),
                MetadataJson = NormalizeNodeMetadataJson(node.MetadataJson),
                Kind = Enum.IsDefined(typeof(FlowchartNodeKind), node.Kind) ? node.Kind : FlowchartNodeKind.Process,
                X = NormalizeCoordinate(node.X),
                Y = NormalizeCoordinate(node.Y),
                Width = NormalizeSize(node.Width, DefaultNodeWidth),
                Height = NormalizeSize(node.Height, DefaultNodeHeight)
            });
        }

        HashSet<Guid> usedConnectionIds = new();
        List<FlowchartConnectionDocument> connections = new();
        foreach (FlowchartConnectionDocument connection in document.Connections ?? new List<FlowchartConnectionDocument>())
        {
            if (!nodeIdMap.TryGetValue(connection.SourceNodeId, out Guid sourceNodeId) ||
                !nodeIdMap.TryGetValue(connection.TargetNodeId, out Guid targetNodeId) ||
                sourceNodeId == targetNodeId)
            {
                continue;
            }

            if (!Enum.IsDefined(typeof(FlowchartAnchor), connection.SourceAnchor) ||
                !Enum.IsDefined(typeof(FlowchartAnchor), connection.TargetAnchor))
            {
                continue;
            }

            connections.Add(new FlowchartConnectionDocument
            {
                Id = EnsureUniqueGuid(connection.Id, usedConnectionIds),
                SourceNodeId = sourceNodeId,
                SourceAnchor = connection.SourceAnchor,
                TargetNodeId = targetNodeId,
                TargetAnchor = connection.TargetAnchor
            });
        }

        return new FlowchartDocument
        {
            Version = document.Version <= 0 ? 1 : document.Version,
            Nodes = nodes,
            Connections = connections
        };
    }


    private static string NormalizeNodeMetadataJson(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return string.Empty;
        }

        try
        {
            WorkStepOperation? operation = JsonSerializer.Deserialize<WorkStepOperation>(metadataJson);
            if (operation is null)
            {
                return metadataJson ?? string.Empty;
            }

            WorkStepOperation normalized = NormalizeWorkStepOperation(operation);
            return JsonSerializer.Serialize(normalized);
        }
        catch
        {
            return metadataJson ?? string.Empty;
        }
    }
    #endregion

    #region 操作类型判断

    /// <summary>
    /// 判断是否为旧版系统操作类型�?    /// </summary>
    /// <param name="operationType">操作类型文本�?/param>
    /// <returns>是旧版系统操作时返回 true�?/returns>
    private static bool IsLegacySystemOperationType(string? operationType)
    {
        return string.Equals(operationType?.Trim(), "系统", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断操作对象是否表示系统操作�?    /// </summary>
    /// <param name="operationObject">操作对象文本�?/param>
    /// <returns>是系统操作对象时返回 true�?/returns>
    private static bool IsSystemOperationObject(string? operationObject)
    {
        return string.Equals(operationObject?.Trim(), "System", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(operationObject?.Trim(), "系统", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断操作类型是否表示已规范化的系统操作�?    /// </summary>
    /// <param name="operationType">操作类型文本�?/param>
    /// <returns>是系统操作类型时返回 true�?/returns>
    private static bool IsNormalizedSystemOperationType(string? operationType)
    {
        return string.Equals(operationType?.Trim(), "System", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(operationType?.Trim(), "系统", StringComparison.OrdinalIgnoreCase) ||
               IsLegacySystemOperationType(operationType);
    }

    /// <summary>
    /// 判断操作对象是否表示已规范化的系统操作�?    /// </summary>
    /// <param name="operationObject">操作对象文本�?/param>
    /// <returns>是系统操作对象时返回 true�?/returns>
    private static bool IsNormalizedSystemOperationObject(string? operationObject)
    {
        return string.Equals(operationObject?.Trim(), "System", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(operationObject?.Trim(), "系统", StringComparison.OrdinalIgnoreCase) ||
               IsSystemOperationObject(operationObject);
    }

    /// <summary>
    /// 判断操作对象是否为判断操作�?    /// </summary>
    /// <param name="operationObject">操作对象文本�?/param>
    /// <returns>是判断操作时返回 true�?/returns>
    private static bool IsJudgeOperationObject(string? operationObject)
    {
        return string.Equals(operationObject?.Trim(), "判断", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断操作对象是否�?Lua 操作�?    /// </summary>
    /// <param name="operationObject">操作对象文本�?/param>
    /// <returns>�?Lua 操作时返�?true�?/returns>
    private static bool IsLuaOperationObject(string? operationObject)
    {
        return string.Equals(operationObject?.Trim(), "Lua", StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 唯一性与名称工具

    /// <summary>
    /// 确保字符串标识在当前集合中唯一�?    /// </summary>
    /// <param name="id">候选标识�?/param>
    /// <param name="usedIds">已使用标识集合�?/param>
    /// <returns>唯一字符串标识�?/returns>
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
    /// 确保 Guid 标识在当前集合中唯一�?    /// </summary>
    /// <param name="id">候�?Guid�?/param>
    /// <param name="usedIds">已使�?Guid 集合�?/param>
    /// <returns>唯一 Guid�?/returns>
    private static Guid EnsureUniqueGuid(Guid id, HashSet<Guid> usedIds)
    {
        Guid candidate = id == Guid.Empty ? Guid.NewGuid() : id;
        while (!usedIds.Add(candidate))
        {
            candidate = Guid.NewGuid();
        }

        return candidate;
    }

    /// <summary>
    /// 基于名称生成不重复的显示名称�?    /// </summary>
    /// <param name="name">候选名称�?/param>
    /// <param name="usedNames">已使用名称集合�?/param>
    /// <returns>唯一显示名称�?/returns>
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

    /// <summary>
    /// 基于工位编码生成不重复的编码�?    /// </summary>
    /// <param name="code">候选工位编码�?/param>
    /// <param name="usedCodes">已使用编码集合�?/param>
    /// <param name="index">默认编码序号�?/param>
    /// <returns>唯一工位编码�?/returns>
    private static string BuildUniqueStationCode(string? code, HashSet<string> usedCodes, int index)
    {
        string baseCode = string.IsNullOrWhiteSpace(code) ? $"ST-{index:00}" : code.Trim().ToUpperInvariant();
        string candidate = baseCode;
        int suffix = 2;

        while (!usedCodes.Add(candidate))
        {
            candidate = $"{baseCode}-{suffix:00}";
            suffix++;
        }

        return candidate;
    }

    #endregion

    #region 文件与数值工�?    /// <summary>
    /// 枚举指定目录下的配置文件�?    /// </summary>
    /// <param name="directory">配置目录�?/param>
    /// <param name="searchPattern">文件搜索模式�?/param>
    /// <returns>按文件名排序后的配置文件路径�?/returns>
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
    /// 规范化流程图坐标值�?    /// </summary>
    /// <param name="value">原始坐标值�?/param>
    /// <returns>可用坐标值�?/returns>
    private static double NormalizeCoordinate(double value)
    {
        return double.IsNaN(value) || double.IsInfinity(value) ? 0 : value;
    }

    /// <summary>
    /// 规范化流程图节点尺寸�?    /// </summary>
    /// <param name="value">原始尺寸值�?/param>
    /// <param name="fallback">无效时使用的默认尺寸�?/param>
    /// <returns>可用尺寸值�?/returns>
    private static double NormalizeSize(double value, double fallback)
    {
        return double.IsNaN(value) || double.IsInfinity(value) || value <= 0 ? fallback : value;
    }

    /// <summary>
    /// 从指定文件反序列�?JSON�?    /// </summary>
    /// <typeparam name="T">目标对象类型�?/typeparam>
    /// <param name="filePath">JSON 文件路径�?/param>
    /// <param name="options">JSON 序列化选项�?/param>
    /// <returns>读取到的对象，失败时返回默认值�?/returns>
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
    /// 将对象序列化并写�?JSON 文件�?    /// </summary>
    /// <typeparam name="T">待写入对象类型�?/typeparam>
    /// <param name="filePath">JSON 文件路径�?/param>
    /// <param name="value">待写入对象�?/param>
    /// <param name="options">JSON 序列化选项�?/param>
    private static void WriteJson<T>(string filePath, T value, JsonSerializerOptions options)
    {
        string json = JsonSerializer.Serialize(value, options);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// 删除本次保存未生成的历史配置文件�?    /// </summary>
    /// <param name="directory">配置目录�?/param>
    /// <param name="searchPattern">文件搜索模式�?/param>
    /// <param name="currentFilePaths">本次保存生成的文件路径集合�?/param>
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
    /// 构建方案配置文件路径�?    /// </summary>
    /// <param name="scheme">方案配置�?/param>
    /// <returns>方案配置文件路径�?/returns>
    private static string BuildSchemeFilePath(SchemeProfile scheme)
    {
        return Path.Combine(SchemeDirectory, $"{SanitizeFileName(scheme.SchemeName)}_{SanitizeFileName(scheme.Id)}.scheme.json");
    }

    /// <summary>
    /// 构建工位配置文件路径�?    /// </summary>
    /// <param name="station">工位配置�?/param>
    /// <returns>工位配置文件路径�?/returns>
    private static string BuildStationFilePath(StationProfile station)
    {
        return Path.Combine(StationDirectory, $"{SanitizeFileName(station.StationName)}_{SanitizeFileName(station.Id)}.station.json");
    }

    /// <summary>
    /// 清理文件名中的非法字符�?    /// </summary>
    /// <param name="fileName">原始文件名�?/param>
    /// <returns>可用于文件系统的安全文件名�?/returns>
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




