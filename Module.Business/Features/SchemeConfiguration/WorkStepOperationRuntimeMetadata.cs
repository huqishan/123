using Module.Business.Services;
using Module.Business.Services.BusinessOperations;
using Shared.Infrastructure.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Module.Business.Features.SchemeConfiguration;

internal static class WorkStepOperationRuntimeMetadata
{
    public static bool IsSystemOperation(WorkStepOperation? operation)
    {
        return operation is not null &&
               SchemeStepEditorState.IsSystemOperationObject(operation.OperationObject);
    }

    public static bool IsLuaOperation(WorkStepOperation? operation)
    {
        return operation is not null &&
               SchemeStepEditorState.IsLuaOperationObject(operation.OperationObject);
    }

    public static bool IsJudgeOperation(WorkStepOperation? operation)
    {
        return operation is not null &&
               SchemeStepEditorState.IsJudgeOperationObject(operation.OperationObject);
    }

    public static IEnumerable<WorkStepOperationParameter> GetOrderedInputParameters(WorkStepOperation? operation)
    {
        return (operation?.InputParameters ?? Enumerable.Empty<WorkStepOperationParameter>())
            .Where(parameter => parameter is not null)
            .OrderBy(parameter => parameter.Sequence);
    }

    public static IEnumerable<WorkStepOperationParameter> GetOrderedReturnParameters(WorkStepOperation? operation)
    {
        return (operation?.ReturnParameters ?? Enumerable.Empty<WorkStepOperationParameter>())
            .Where(parameter => parameter is not null)
            .OrderBy(parameter => parameter.Sequence);
    }

    public static string GetReturnParameterKey(WorkStepOperationParameter? parameter)
    {
        if (parameter is null)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(parameter.ParameterName)
            ? parameter.Value?.Trim() ?? string.Empty
            : parameter.ParameterName.Trim();
    }

    public static WorkStepOperationParameter? FindReturnParameter(WorkStepOperation? operation, string? key)
    {
        string normalizedKey = key?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            return null;
        }

        return GetOrderedReturnParameters(operation).FirstOrDefault(parameter =>
            string.Equals(GetReturnParameterKey(parameter), normalizedKey, StringComparison.OrdinalIgnoreCase));
    }

    public static WorkStepOperationParameter? GetPrimaryReturnParameter(WorkStepOperation? operation)
    {
        List<WorkStepOperationParameter> parameters = GetOrderedReturnParameters(operation).ToList();
        return parameters.FirstOrDefault(parameter => parameter.ShowDataToView) ??
               parameters.FirstOrDefault();
    }

    public static bool TryResolveBusinessOperation(
        WorkStepOperation? operation,
        out string deviceId,
        out string operationId,
        out BusinessOperationDescriptor? descriptor)
    {
        deviceId = string.Empty;
        operationId = string.Empty;
        descriptor = null;
        if (operation is null ||
            IsSystemOperation(operation) ||
            IsJudgeOperation(operation) ||
            IsLuaOperation(operation))
        {
            return false;
        }

        deviceId = BusinessOperationBindingResolver.ResolveCatalogDeviceId(operation.OperationObject, null);
        operationId = operation.InvokeMethod?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(operationId))
        {
            return false;
        }

        descriptor = BusinessOperationCatalog.Find(deviceId, operationId);
        return descriptor is not null;
    }

    public static bool TryResolveProtocolCommand(
        WorkStepOperation? operation,
        out string protocolName,
        out string commandName)
    {
        protocolName = string.Empty;
        commandName = string.Empty;
        if (operation is null ||
            IsSystemOperation(operation) ||
            IsJudgeOperation(operation) ||
            IsLuaOperation(operation))
        {
            return false;
        }

        if (TryResolveBusinessOperation(operation, out _, out _, out _))
        {
            return false;
        }

        string operationObject = operation.OperationObject?.Trim() ?? string.Empty;
        commandName = operation.InvokeMethod?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(operationObject) || string.IsNullOrWhiteSpace(commandName))
        {
            return false;
        }

        foreach (string candidateProtocolName in LoadDeviceSupportedProtocolNames(operationObject))
        {
            if (ProtocolCommandExists(candidateProtocolName, commandName))
            {
                protocolName = candidateProtocolName;
                return true;
            }
        }

        commandName = string.Empty;
        return false;
    }

    public static ObservableCollection<WorkStepOperationParameter> CloneParameters(
        IEnumerable<WorkStepOperationParameter>? parameters)
    {
        return new ObservableCollection<WorkStepOperationParameter>(
            (parameters ?? Enumerable.Empty<WorkStepOperationParameter>()).Select(parameter => parameter.Clone()));
    }

    private static IEnumerable<string> LoadDeviceSupportedProtocolNames(string operationObject)
    {
        if (string.IsNullOrWhiteSpace(operationObject))
        {
            return Enumerable.Empty<string>();
        }

        string communicationConfigDirectory = Path.Combine(AppContext.BaseDirectory, "Config", "Communication");
        if (!Directory.Exists(communicationConfigDirectory))
        {
            return Enumerable.Empty<string>();
        }

        List<string> names = new();
        foreach (string filePath in Directory.EnumerateFiles(communicationConfigDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(filePath, Encoding.UTF8));
                if (!document.RootElement.TryGetProperty("LocalName", out JsonElement localNameElement) ||
                    !TextEquals(localNameElement.GetString(), operationObject))
                {
                    continue;
                }

                if (!document.RootElement.TryGetProperty("SupportedProtocols", out JsonElement supportedProtocolsElement) ||
                    supportedProtocolsElement.ValueKind != JsonValueKind.Array)
                {
                    return Enumerable.Empty<string>();
                }

                foreach (JsonElement protocolElement in supportedProtocolsElement.EnumerateArray())
                {
                    string name = GetJsonString(protocolElement, "ProtocolName");
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        names.Add(name.Trim());
                    }
                }

                break;
            }
            catch
            {
                // Ignore broken config files during runtime resolution.
            }
        }

        return names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);
    }

    private static bool ProtocolCommandExists(string protocolName, string commandName)
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "Config", "Protocol");
        if (string.IsNullOrWhiteSpace(protocolName) ||
            string.IsNullOrWhiteSpace(commandName) ||
            !Directory.Exists(directory))
        {
            return false;
        }

        foreach (string filePath in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(ReadPossiblyEncryptedText(filePath));
                JsonElement root = document.RootElement;
                if (!TextEquals(GetJsonString(root, "Name"), protocolName))
                {
                    continue;
                }

                if (root.TryGetProperty("Commands", out JsonElement commandsElement) &&
                    commandsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement commandElement in commandsElement.EnumerateArray())
                    {
                        if (TextEquals(GetJsonString(commandElement, "Name"), commandName))
                        {
                            return true;
                        }
                    }
                }

                if (TextEquals(GetJsonString(root, "CommandName"), commandName))
                {
                    return true;
                }
            }
            catch
            {
                // Ignore broken protocol files during runtime resolution.
            }
        }

        return false;
    }

    private static string ReadPossiblyEncryptedText(string filePath)
    {
        string text = File.ReadAllText(filePath, Encoding.UTF8);
        try
        {
            return text.DesDecrypt();
        }
        catch
        {
            return text;
        }
    }

    private static string GetJsonString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement propertyElement))
        {
            return string.Empty;
        }

        return propertyElement.ValueKind switch
        {
            JsonValueKind.String => propertyElement.GetString() ?? string.Empty,
            JsonValueKind.Number => propertyElement.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => string.Empty
        };
    }

    private static bool TextEquals(string? left, string? right)
    {
        return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
