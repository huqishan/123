using ControlLibrary.Models.EventsModels.Test;
using ControlLibrary.Models.MediatorModels.Communication;
using Module.Business.Models;
using Module.Business.Services.BusinessOperations;
using Module.Business.Features.Scheme.ViewModels.PresentationModels;
using Module.Business.Features.OperationEditing.ViewModels.PresentationModels;
using Module.Business.Features.WorkStep.Services;
using Module.Business.Features.WorkStep.ViewModels.PresentationModels;
using Shared.Infrastructure.Communication;
using Shared.Infrastructure.Events;
using Shared.Infrastructure.Extensions;
using Shared.Infrastructure.Lua;
using Shared.Infrastructure.Mediator;
using Shared.Models.Communication;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Module.Business.Services;

public static class SchemeExecutionService
{
    #region ����������״̬�ֶ�

    // �������������������ѯ���ڣ�ͳһ�����������ɢ��ħ��ֵ��
    private const int ControlPollingIntervalMilliseconds = 50;

    private static readonly Regex PlaceholderRegex =
        new(@"\{\{\s*(?<name>[^{}\r\n]+?)\s*\}\}", RegexOptions.Compiled);

    private static readonly ConcurrentDictionary<string, SchemeExecutionContext> ActiveExecutions =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, string> GlobalValues = new(StringComparer.OrdinalIgnoreCase);
    private static IEventAggregator? _eventAggregator;
    private static IMediator? _mediator;

    #endregion

    public static void ConfigureEventAggregator(IEventAggregator eventAggregator)
    {
        _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
    }

    public static void ConfigureMediator(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    #region ִ�����������¼�

    /// <summary>
    /// ����ִ��ǰ�¼�����ͨ�� <see cref="SchemeExecutionEventArgs.Cancel"/> ȡ��ִ�С�
    /// </summary>
    public static event EventHandler<SchemeExecutionEventArgs>? BeforeSchemeExecuting;

    /// <summary>
    /// ����ִ�����¼����ڷ���������ִ��ѭ���󴥷���
    /// </summary>
    public static event EventHandler<SchemeExecutionEventArgs>? SchemeExecuting;

    /// <summary>
    /// ����ִ�к��¼�������ִ�п�ʼʱ�䡢����ʱ��ͺ�ʱ��?
    /// </summary>
    public static event EventHandler<SchemeExecutionEventArgs>? AfterSchemeExecuted;

    /// <summary>
    /// ����ִ��ǰ�¼�����ͨ�� <see cref="SchemeExecutionEventArgs.Cancel"/> ȡ��ִ�С�
    /// </summary>
    public static event EventHandler<SchemeExecutionEventArgs>? BeforeWorkStepExecuting;

    /// <summary>
    /// ����ִ�����¼����ڹ����ڲ��迪ʼִ��ǰ������
    /// </summary>
    public static event EventHandler<SchemeExecutionEventArgs>? WorkStepExecuting;

    /// <summary>
    /// ����ִ�к��¼�������ִ�п�ʼʱ�䡢����ʱ��ͺ�ʱ��?
    /// </summary>
    public static event EventHandler<SchemeExecutionEventArgs>? AfterWorkStepExecuted;

    /// <summary>
    /// ����ִ��ǰ�¼�����ͨ�� <see cref="SchemeExecutionEventArgs.Cancel"/> ȡ��ִ�С�
    /// </summary>
    public static event EventHandler<SchemeExecutionEventArgs>? BeforeStepExecuting;

    /// <summary>
    /// ����ִ�����¼����ڵ���������ʽ����ǰ������
    /// </summary>
    public static event EventHandler<SchemeExecutionEventArgs>? StepExecuting;

    /// <summary>
    /// ����ִ�к��¼�������ִ�п�ʼʱ�䡢����ʱ�䡢��ʱ��ִ�н����?
    /// </summary>
    public static event EventHandler<SchemeExecutionEventArgs>? AfterStepExecuted;

    #endregion

    #region ����ִ����������

    /// <summary>
    /// ���ݹ�λ�źͷ������ƶ�ȡ�����ļ���ִ�У�ͬһ��λͬһʱ��ֻ����һ������ִ��ʵ����
    /// </summary>
    public static async Task<SchemeExecutionResult> ExecuteAsync(string stationNo, string schemeName)
    {
        string normalizedStationNo = NormalizeRequiredText(stationNo);
        string normalizedSchemeName = NormalizeRequiredText(schemeName);
        DateTime startTime = DateTime.Now;
        if (string.IsNullOrWhiteSpace(normalizedStationNo))
        {
            return SchemeExecutionResult.CreateFailure("Station number is required.", startTime: startTime, endTime: DateTime.Now);
        }

        if (string.IsNullOrWhiteSpace(normalizedSchemeName))
        {
            return SchemeExecutionResult.CreateFailure("Scheme name is required.", startTime: startTime, endTime: DateTime.Now);
        }

        PublishTestStatus(
            normalizedStationNo,
            "׼������",
            string.Empty,
            normalizedSchemeName,
            string.Empty,
            $"׼��ִ�в��Է�����{normalizedSchemeName}");

        SchemeExecutionKey key = new(normalizedStationNo, normalizedSchemeName);
        SchemeExecutionContext context = new(key, startTime);
        if (!ActiveExecutions.TryAdd(normalizedStationNo, context))
        {
            string runningSchemeName = ActiveExecutions.TryGetValue(normalizedStationNo, out SchemeExecutionContext? runningContext)
                ? runningContext.Key.SchemeName
                : string.Empty;
            context.Dispose();
            string message = string.IsNullOrWhiteSpace(runningSchemeName)
                ? $"Station '{normalizedStationNo}' already has a running scheme."
                : $"Station '{normalizedStationNo}' is already running scheme '{runningSchemeName}'.";
            PublishTestStatus(
                normalizedStationNo,
                "����ʧ��",
                string.Empty,
                normalizedSchemeName,
                string.Empty,
                message,
                false);
            return SchemeExecutionResult.CreateFailure(
                message,
                startTime: startTime,
                endTime: DateTime.Now);
        }

        try
        {
            SchemeConfigurationCatalog catalog = SchemeConfigurationStore.LoadCatalog();
            SchemeProfile? scheme = catalog.Schemes.FirstOrDefault(item =>
                string.Equals(item.SchemeName?.Trim(), normalizedSchemeName, StringComparison.OrdinalIgnoreCase));
            if (scheme is null)
            {
                PublishTestStatus(
                    normalizedStationNo,
                    "����ʧ��",
                    string.Empty,
                    normalizedSchemeName,
                    string.Empty,
                    $"Scheme '{normalizedSchemeName}' was not found.",
                    false);
                return SchemeExecutionResult.CreateFailure(
                    $"Scheme '{normalizedSchemeName}' was not found.",
                    startTime: startTime,
                    endTime: DateTime.Now);
            }

            return await ExecuteSchemeAsync(context, scheme.Clone(), WorkStepConfigurationStore.Load())
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            PublishTestStatus(
                normalizedStationNo,
                "��ֹͣ",
                string.Empty,
                normalizedSchemeName,
                string.Empty,
                $"Scheme '{normalizedSchemeName}' on station '{normalizedStationNo}' was stopped.",
                false);
            return SchemeExecutionResult.CreateCanceled(
                $"Scheme '{normalizedSchemeName}' on station '{normalizedStationNo}' was stopped.",
                context.Logs,
                startTime,
                DateTime.Now);
        }
        catch (Exception ex)
        {
            PublishTestStatus(
                normalizedStationNo,
                "����ʧ��",
                string.Empty,
                normalizedSchemeName,
                string.Empty,
                $"Scheme '{normalizedSchemeName}' on station '{normalizedStationNo}' failed: {ex.Message}",
                false);
            return SchemeExecutionResult.CreateFailure(
                $"Scheme '{normalizedSchemeName}' on station '{normalizedStationNo}' failed: {ex.Message}",
                context.Logs,
                startTime,
                DateTime.Now);
        }
        finally
        {
            ActiveExecutions.TryRemove(normalizedStationNo, out _);
            context.Dispose();
        }
    }
    /// <summary>
    /// ��ָͣ����λ�������������еķ���ִ��ʵ����
    /// </summary>
    public static SchemeExecutionControlActionResult Pause(string stationNo)
    {
        if (!TryGetStationContexts(stationNo, out List<SchemeExecutionContext> contexts, out string message))
        {
            return SchemeExecutionControlActionResult.CreateFailure(message);
        }

        string normalizedStationNo = NormalizeRequiredText(stationNo);
        int pausedCount = contexts.Count(context => context.Pause());
        if (pausedCount > 0)
        {
            PublishExecutionControlStatus(normalizedStationNo, "����ͣ", contexts.First().Key.SchemeName, "��������ͣ");
        }

        return pausedCount > 0
            ? SchemeExecutionControlActionResult.CreateSuccess(
                $"Station '{normalizedStationNo}' paused {pausedCount}/{contexts.Count} execution(s).")
            : SchemeExecutionControlActionResult.CreateSuccess(
                $"Station '{normalizedStationNo}' executions are already paused.");
    }
    /// <summary>
    /// ����ָ����λ����������ͣ�ķ���ִ��ʵ����
    /// </summary>
    public static SchemeExecutionControlActionResult Continue(string stationNo)
    {
        if (!TryGetStationContexts(stationNo, out List<SchemeExecutionContext> contexts, out string message))
        {
            return SchemeExecutionControlActionResult.CreateFailure(message);
        }

        string normalizedStationNo = NormalizeRequiredText(stationNo);
        int resumedCount = contexts.Count(context => context.Resume());
        if (resumedCount > 0)
        {
            PublishExecutionControlStatus(normalizedStationNo, "������", contexts.First().Key.SchemeName, "�����Ѽ���");
        }

        return resumedCount > 0
            ? SchemeExecutionControlActionResult.CreateSuccess(
                $"Station '{normalizedStationNo}' resumed {resumedCount}/{contexts.Count} execution(s).")
            : SchemeExecutionControlActionResult.CreateSuccess(
                $"Station '{normalizedStationNo}' executions are not paused.");
    }
    /// <summary>
    /// ָֹͣ����λ�������������еķ���ִ��ʵ����
    /// </summary>
    public static SchemeExecutionControlActionResult Stop(string stationNo)
    {
        if (!TryGetStationContexts(stationNo, out List<SchemeExecutionContext> contexts, out string message))
        {
            return SchemeExecutionControlActionResult.CreateFailure(message);
        }

        foreach (SchemeExecutionContext context in contexts)
        {
            context.Stop();
        }

        string normalizedStationNo = NormalizeRequiredText(stationNo);
        PublishExecutionControlStatus(normalizedStationNo, "ֹͣ��", contexts.First().Key.SchemeName, "�ѷ���ֹͣ��������");
        return SchemeExecutionControlActionResult.CreateSuccess(
            $"Stop request sent to {contexts.Count} execution(s) on station '{normalizedStationNo}'.");
    }

    /// <summary>
    /// ��ȡ��ǰ�������еķ���ִ�п��ա�
    /// </summary>
    public static IReadOnlyList<SchemeExecutionSnapshot> GetActiveExecutions()
    {
        return ActiveExecutions.Values
            .Select(context => context.CreateSnapshot())
            .OrderBy(item => item.StationNo, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.SchemeName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    #endregion

    #region ����������������ִ�б���

    private static async Task<SchemeExecutionResult> ExecuteSchemeAsync(
        SchemeExecutionContext context,
        SchemeProfile scheme,
        IReadOnlyCollection<WorkStepProfile> configuredWorkSteps)
    {
        DateTime startTime = DateTime.Now;
        SchemeExecutionEventArgs beforeSchemeArgs = SchemeExecutionEventArgs.CreateScheme(
            context.Key.StationNo,
            scheme,
            startTime: startTime);
        Raise(BeforeSchemeExecuting, beforeSchemeArgs);
        if (beforeSchemeArgs.Cancel)
        {
            DateTime canceledAt = DateTime.Now;
            PublishSchemeStatus(context.Key.StationNo, "��ȡ��", scheme, "Scheme execution was canceled before start.", false);
            return SchemeExecutionResult.CreateCanceled(
                "Scheme execution was canceled before start.",
                context.Logs,
                startTime,
                canceledAt);
        }

        context.AddLog($"Start scheme '{scheme.SchemeName}' on station '{context.Key.StationNo}'.");
        Raise(SchemeExecuting, SchemeExecutionEventArgs.CreateScheme(
            context.Key.StationNo,
            scheme,
            message: "Scheme is executing.",
            startTime: startTime));
        PublishSchemeStatus(context.Key.StationNo, "������", scheme, $"���Է���ִ���У�{scheme.SchemeName}");

        for (int workStepIndex = 0; workStepIndex < scheme.Steps.Count; workStepIndex++)
        {
            await context.WaitIfPausedAsync().ConfigureAwait(false);
            context.ThrowIfCancellationRequested();

            SchemeWorkStepItem schemeStep = scheme.Steps[workStepIndex];
            if (!schemeStep.IsStartupEnabled)
            {
                context.AddLog($"Skip work step {workStepIndex + 1}: {schemeStep.StepName}.");
                continue;
            }

            WorkStepProfile? configuredWorkStep = configuredWorkSteps.FirstOrDefault(item => string.Equals(
                item.Name,
                schemeStep.StepType,
                StringComparison.OrdinalIgnoreCase));
            if (configuredWorkStep is null || configuredWorkStep.Operations.Count == 0)
            {
                string failureMessage = $"Work step '{schemeStep.StepName}' has no operations.";
                DateTime failedAt = DateTime.Now;
                Raise(AfterSchemeExecuted, SchemeExecutionEventArgs.CreateScheme(
                    context.Key.StationNo,
                    scheme,
                    false,
                    failureMessage,
                    startTime,
                    failedAt));
                PublishSchemeStatus(context.Key.StationNo, "����ʧ��", scheme, failureMessage, false);
                return SchemeExecutionResult.CreateFailure(failureMessage, context.Logs, startTime, failedAt);
            }

            SchemeExecutionResult workStepResult = await ExecuteWorkStepAsync(
                    context,
                    scheme,
                    schemeStep,
                    configuredWorkStep.Operations,
                    workStepIndex + 1)
                .ConfigureAwait(false);
            if (!workStepResult.IsSuccess)
            {
                DateTime failedAt = DateTime.Now;
                Raise(AfterSchemeExecuted, SchemeExecutionEventArgs.CreateScheme(
                    context.Key.StationNo,
                    scheme,
                    false,
                    workStepResult.Message,
                    startTime,
                    failedAt));
                PublishSchemeStatus(context.Key.StationNo, "����ʧ��", scheme, workStepResult.Message, false);
                return workStepResult;
            }
        }

        string message = $"Scheme '{scheme.SchemeName}' finished.";
        DateTime endTime = DateTime.Now;
        context.AddLog(message);
        Raise(AfterSchemeExecuted, SchemeExecutionEventArgs.CreateScheme(
            context.Key.StationNo,
            scheme,
            true,
            message,
            startTime,
            endTime));
        PublishSchemeStatus(context.Key.StationNo, "����ͨ��", scheme, message, true);
        return SchemeExecutionResult.CreateSuccess(message, context.Logs, startTime, endTime);
    }

    private static async Task<SchemeExecutionResult> ExecuteWorkStepAsync(
        SchemeExecutionContext context,
        SchemeProfile scheme,
        SchemeWorkStepItem schemeStep,
        IReadOnlyList<WorkStepOperation> operations,
        int workStepIndex)
    {
        DateTime startTime = DateTime.Now;
        SchemeExecutionEventArgs beforeWorkStepArgs = SchemeExecutionEventArgs.CreateWorkStep(
            context.Key.StationNo,
            scheme,
            schemeStep,
            workStepIndex,
            startTime: startTime);
        Raise(BeforeWorkStepExecuting, beforeWorkStepArgs);
        if (beforeWorkStepArgs.Cancel)
        {
            DateTime canceledAt = DateTime.Now;
            PublishSchemeStatus(context.Key.StationNo, "��ȡ��", scheme, "Work step execution was canceled before start.", false);
            return SchemeExecutionResult.CreateCanceled(
                "Work step execution was canceled before start.",
                context.Logs,
                startTime,
                canceledAt);
        }

        context.AddLog($"Start work step {workStepIndex}: {schemeStep.StepName}.");
        Raise(WorkStepExecuting, SchemeExecutionEventArgs.CreateWorkStep(
            context.Key.StationNo,
            scheme,
            schemeStep,
            workStepIndex,
            message: "Work step is executing.",
            startTime: startTime));
        PublishSchemeStatus(
            context.Key.StationNo,
            "������",
            scheme,
            $"����ִ�й��� {workStepIndex}��{schemeStep.StepName}");

        Dictionary<string, string> returnValues = new(StringComparer.OrdinalIgnoreCase);
        for (int stepIndex = 0; stepIndex < operations.Count; stepIndex++)
        {
            await context.WaitIfPausedAsync().ConfigureAwait(false);
            context.ThrowIfCancellationRequested();

            WorkStepOperation operation = operations[stepIndex];
            SchemeExecutionResult stepResult = await ExecuteStepAsync(
                    context,
                    scheme,
                    schemeStep,
                    operation,
                    returnValues,
                    workStepIndex,
                    stepIndex + 1)
                .ConfigureAwait(false);
            if (!stepResult.IsSuccess)
            {
                DateTime failedAt = DateTime.Now;
                Raise(AfterWorkStepExecuted, SchemeExecutionEventArgs.CreateWorkStep(
                    context.Key.StationNo,
                    scheme,
                    schemeStep,
                    workStepIndex,
                    false,
                    stepResult.Message,
                    startTime,
                    failedAt));
                return stepResult;
            }
        }

        string message = $"Work step {workStepIndex} finished: {schemeStep.StepName}.";
        DateTime endTime = DateTime.Now;
        context.AddLog(message);
        Raise(AfterWorkStepExecuted, SchemeExecutionEventArgs.CreateWorkStep(
            context.Key.StationNo,
            scheme,
            schemeStep,
            workStepIndex,
            true,
            message,
            startTime,
            endTime));
        return SchemeExecutionResult.CreateSuccess(message, context.Logs, startTime, endTime);
    }

    private static async Task<SchemeExecutionResult> ExecuteStepAsync(
        SchemeExecutionContext context,
        SchemeProfile scheme,
        SchemeWorkStepItem schemeStep,
        WorkStepOperation operation,
        Dictionary<string, string> returnValues,
        int workStepIndex,
        int stepIndex)
    {
        DateTime startTime = DateTime.Now;
        SchemeExecutionEventArgs beforeStepArgs = SchemeExecutionEventArgs.CreateStep(
            context.Key.StationNo,
            scheme,
            schemeStep,
            operation,
            workStepIndex,
            stepIndex,
            startTime: startTime);
        Raise(BeforeStepExecuting, beforeStepArgs);
        if (beforeStepArgs.Cancel)
        {
            DateTime canceledAt = DateTime.Now;
            PublishSchemeStatus(context.Key.StationNo, "��ȡ��", scheme, "Step execution was canceled before start.", false);
            return SchemeExecutionResult.CreateCanceled(
                "Step execution was canceled before start.",
                context.Logs,
                startTime,
                canceledAt);
        }

        context.AddLog($"Start step {workStepIndex}.{stepIndex}: {operation.Summary}.");
        Raise(StepExecuting, SchemeExecutionEventArgs.CreateStep(
            context.Key.StationNo,
            scheme,
            schemeStep,
            operation,
            workStepIndex,
            stepIndex,
            message: "Step is executing.",
            startTime: startTime));
        PublishSchemeStatus(
            context.Key.StationNo,
            "������",
            scheme,
            $"����ִ�в��� {workStepIndex}.{stepIndex}��{operation.Summary}");

        SchemeStepExecutionOutput output;
        try
        {
            output = await ExecuteOperationAsync(context, operation, schemeStep, returnValues)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            output = SchemeStepExecutionOutput.Failure(ex.Message);
        }

        string resultText = output.Result?.ToString() ?? string.Empty;
        foreach (var rv in operation.ReturnValues)
        {
            if (!string.IsNullOrWhiteSpace(operation.ReturnValue) &&
                !string.IsNullOrWhiteSpace(rv.ReturnParameterName))
            {
                // 返回值字典与参数下拉框使用同一命名规则，后续步骤可直接按“返回值_返回值键”引用。
                string returnValueName = $"{operation.ReturnValue.Trim()}_{rv.ReturnParameterName.Trim()}";
                returnValues[returnValueName] = resultText;
            }
        }

        foreach (var rv in operation.ReturnValues.Where(rv => rv.IsShowView))
        {
            Module.Business.Business.System.SendDataToView(
                rv.ViewDataName,
                resultText);
        }

        if (operation.DelayMilliseconds > 0)
        {
            await DelayWithControlAsync(context, operation.DelayMilliseconds).ConfigureAwait(false);
        }

        string message = output.IsSuccess
            ? $"Step {workStepIndex}.{stepIndex} finished: {operation.Summary}."
            : $"Step {workStepIndex}.{stepIndex} failed: {output.Message}";
        DateTime endTime = DateTime.Now;
        context.AddLog(message);
        Raise(AfterStepExecuted, SchemeExecutionEventArgs.CreateStep(
            context.Key.StationNo,
            scheme,
            schemeStep,
            operation,
            workStepIndex,
            stepIndex,
            output.IsSuccess,
            message,
            output.Result,
            startTime,
            endTime));
        if (!output.IsSuccess)
        {
            PublishSchemeStatus(context.Key.StationNo, "����ʧ��", scheme, message, false);
        }

        return output.IsSuccess
            ? SchemeExecutionResult.CreateSuccess(message, context.Logs, startTime, endTime)
            : SchemeExecutionResult.CreateFailure(message, context.Logs, startTime, endTime);
    }

    #endregion

    #region ���������ִ��?

    /// <summary>
    /// ������ͼ�ȶ���������ڸ��õ�������ִ���߼���?
    /// </summary>
    internal static async Task<SchemeStepExecutionOutput> ExecuteStandaloneStepAsync(
        IControlledExecutionContext context,
        WorkStepOperation operation,
        Dictionary<string, string> returnValues)
    {
        SchemeWorkStepItem standaloneStep = CreateStandaloneSchemeStep();
        SchemeStepExecutionOutput output = await ExecuteOperationAsync(context, operation, standaloneStep, returnValues)
            .ConfigureAwait(false);

        string resultText = output.Result?.ToString() ?? string.Empty;
        foreach (var rv in operation.ReturnValues)
        {
            if (!string.IsNullOrWhiteSpace(operation.ReturnValue) &&
                !string.IsNullOrWhiteSpace(rv.ReturnParameterName))
            {
                // 独立步骤执行与方案执行保持一致，统一使用“返回值_返回值键”作为字典键。
                string returnValueName = $"{operation.ReturnValue.Trim()}_{rv.ReturnParameterName.Trim()}";
                returnValues[returnValueName] = resultText;
            }
        }

        foreach (var rv in operation.ReturnValues.Where(rv => rv.IsShowView))
        {
            Module.Business.Business.System.SendDataToView(
                rv.ViewDataName,
                resultText);
        }

        if (operation.DelayMilliseconds > 0)
        {
            await DelayWithControlAsync(context, operation.DelayMilliseconds).ConfigureAwait(false);
        }

        return output;
    }

    private static async Task<SchemeStepExecutionOutput> ExecuteOperationAsync(
        IControlledExecutionContext context,
        WorkStepOperation operation,
        SchemeWorkStepItem schemeStep,
        IReadOnlyDictionary<string, string> returnValues)
    {
        if (IsLuaOperation(operation))
        {
            return ExecuteLua(operation);
        }

        if (IsJudgeOperation(operation))
        {
            return ExecuteJudge(operation, schemeStep, returnValues);
        }

        if (TryResolveBusinessOperation(operation, out string deviceId, out string operationId))
        {
            return await ExecuteCatalogBusinessOperationAsync(
                    context,
                    operation,
                    schemeStep,
                    returnValues,
                    deviceId,
                    operationId)
                .ConfigureAwait(false);
        }

        if (IsSystemOperation(operation))
        {
            return await ExecuteSystemMethodAsync(operation, schemeStep, returnValues)
                .ConfigureAwait(false);
        }

        return await ExecuteDeviceOperationAsync(context, operation, schemeStep, returnValues)
            .ConfigureAwait(false);
    }

    private static SchemeStepExecutionOutput ExecuteLua(WorkStepOperation operation)
    {
        object[] results = new LuaManage().DoString(operation.LuaScript ?? string.Empty);
        if (results.Length == 1 && results[0] is Exception ex)
        {
            return SchemeStepExecutionOutput.Failure(ex.Message, ex);
        }

        string resultText = string.Join(", ", results.Select(item => item?.ToString() ?? string.Empty));
        return SchemeStepExecutionOutput.Success(resultText);
    }

    private static bool TryResolveBusinessOperation(
        WorkStepOperation operation,
        out string deviceId,
        out string operationId)
    {
        deviceId = BusinessOperationBindingResolver.ResolveCatalogDeviceId(
            operation.OperationObjectName);
        operationId = operation.PCommandName?.Trim() ?? string.Empty;

        return BusinessOperationCatalog.Find(deviceId, operationId) is not null;
    }

    private static async Task<SchemeStepExecutionOutput> ExecuteCatalogBusinessOperationAsync(
        IControlledExecutionContext context,
        WorkStepOperation operation,
        SchemeWorkStepItem schemeStep,
        IReadOnlyDictionary<string, string> returnValues,
        string deviceId,
        string operationId)
    {
        Dictionary<string, string> parameterValues = operation.Parameters
            .OrderBy(parameter => parameter.Num)
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.ParameterName))
            .GroupBy(parameter => parameter.ParameterName.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => ResolveParameterValue(group.First(), schemeStep, returnValues),
                StringComparer.OrdinalIgnoreCase);
        string? operationObjectName = string.IsNullOrWhiteSpace(operation.OperationObjectName)
            ? null
            : operation.OperationObjectName.Trim();

        BusinessOperationInvocationResult result = await BusinessOperationInvoker
            .InvokeAsync(deviceId, operationId, parameterValues, operationObjectName, context.CancellationToken)
            .ConfigureAwait(false);
        context.ThrowIfCancellationRequested();

        return result.IsSuccess
            ? SchemeStepExecutionOutput.Success(result.Result)
            : SchemeStepExecutionOutput.Failure(result.Message, result.Result);
    }

    private static async Task<SchemeStepExecutionOutput> ExecuteSystemMethodAsync(
        WorkStepOperation operation,
        SchemeWorkStepItem schemeStep,
        IReadOnlyDictionary<string, string> returnValues)
    {
        string methodName = operation.PCommandName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(methodName) || string.Equals(methodName, "�ȴ�", StringComparison.OrdinalIgnoreCase))
        {
            return SchemeStepExecutionOutput.Success(string.Empty);
        }

        MethodInfo? method = typeof(Module.Business.Business.System)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(item => string.Equals(item.Name, methodName, StringComparison.OrdinalIgnoreCase));
        if (method is null)
        {
            return SchemeStepExecutionOutput.Failure($"System method '{methodName}' was not found.");
        }

        object?[] args = BuildMethodArguments(method, operation, schemeStep, returnValues);
        object? value = method.Invoke(null, args);
        if (value is Task task)
        {
            await task.ConfigureAwait(false);
            value = task.GetType().IsGenericType
                ? task.GetType().GetProperty("Result")?.GetValue(task)
                : null;
        }

        return SchemeStepExecutionOutput.Success(value);
    }

    private static SchemeStepExecutionOutput ExecuteJudge(
        WorkStepOperation operation,
        SchemeWorkStepItem schemeStep,
        IReadOnlyDictionary<string, string> returnValues)
    {
        List<string> values = operation.Parameters
            .OrderBy(parameter => parameter.Num)
            .Select(parameter => ResolveParameterValue(parameter, schemeStep, returnValues))
            .ToList();

        string methodName = operation.PCommandName?.Trim() ?? string.Empty;
        bool result = methodName switch
        {
            "=" => TextEquals(GetValue(values, 0), GetValue(values, 1)),
            "≠" => !TextEquals(GetValue(values, 0), GetValue(values, 1)),
            ">" => CompareNumbers(GetValue(values, 0), GetValue(values, 1)) > 0,
            "≥" => CompareNumbers(GetValue(values, 0), GetValue(values, 1)) >= 0,
            "<" => CompareNumbers(GetValue(values, 0), GetValue(values, 1)) < 0,
            "≤" => CompareNumbers(GetValue(values, 0), GetValue(values, 1)) <= 0,
            "∈" => CompareNumbers(GetValue(values, 0), GetValue(values, 1)) >= 0 && CompareNumbers(GetValue(values, 0), GetValue(values, 2)) <= 0,
            "∉" => CompareNumbers(GetValue(values, 0), GetValue(values, 1)) < 0 || CompareNumbers(GetValue(values, 0), GetValue(values, 2)) > 0,
            "∋" => GetValue(values, 0).IndexOf(GetValue(values, 1), StringComparison.OrdinalIgnoreCase) >= 0,
            "∌" => GetValue(values, 0).IndexOf(GetValue(values, 1), StringComparison.OrdinalIgnoreCase) < 0,
            "= ∅" => string.IsNullOrWhiteSpace(GetValue(values, 0)),
            "≠ ∅" => !string.IsNullOrWhiteSpace(GetValue(values, 0)),
            _ => false
        };

        return result
            ? SchemeStepExecutionOutput.Success(true)
            : SchemeStepExecutionOutput.Failure($"Judge method '{methodName}' returned false.", false);
    }

    private static async Task<SchemeStepExecutionOutput> ExecuteDeviceOperationAsync(
        IControlledExecutionContext context,
        WorkStepOperation operation,
        SchemeWorkStepItem schemeStep,
        IReadOnlyDictionary<string, string> returnValues)
    {
        string communicationName = operation.OperationObjectName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(communicationName))
        {
            return SchemeStepExecutionOutput.Failure("Communication name is required.");
        }

        if (_mediator is null)
        {
            return SchemeStepExecutionOutput.Failure("Device communication mediator is not configured.");
        }

        string message = BuildDeviceMessage(operation, schemeStep, returnValues);
        SendReceiveModel send = new(message);
        DeviceExecutionActionResult result = await _mediator
            .Send(new SendDeviceDataRequest(communicationName, send), context.CancellationToken)
            .ConfigureAwait(false);
        context.ThrowIfCancellationRequested();

        return result.IsSuccess
            ? SchemeStepExecutionOutput.Success(result.Result ?? string.Empty)
            : SchemeStepExecutionOutput.Failure(
                string.IsNullOrWhiteSpace(result.Message)
                    ? $"Communication '{communicationName}' write failed."
                    : result.Message,
                result.Result);
    }

    private static string BuildDeviceMessage(
        WorkStepOperation operation,
        SchemeWorkStepItem schemeStep,
        IReadOnlyDictionary<string, string> returnValues)
    {
        Dictionary<string, string> parameterValues = operation.Parameters
            .OrderBy(parameter => parameter.Num)
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.ParameterName))
            .GroupBy(parameter => parameter.ParameterName.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => ResolveParameterValue(group.First(), schemeStep, returnValues),
                StringComparer.OrdinalIgnoreCase);

        return PlaceholderRegex.Replace(operation.PCommandName ?? string.Empty, match =>
        {
            string name = match.Groups["name"].Value.Trim();
            return parameterValues.TryGetValue(name, out string? value) ? value : match.Value;
        });
    }

    #endregion

    #region Э�����ö�ȡ�뱨������

    private static bool TryResolveProtocolCommand(
        string protocolName,
        string commandName,
        IReadOnlyDictionary<string, string> parameterValues,
        out string message)
    {
        message = string.Empty;
        if (string.IsNullOrWhiteSpace(protocolName) || string.IsNullOrWhiteSpace(commandName))
        {
            return false;
        }

        JsonElement? command = FindProtocolCommand(protocolName.Trim(), commandName.Trim());
        if (command is null)
        {
            return false;
        }

        string template = GetJsonString(command.Value, "ContentTemplate");
        if (string.IsNullOrWhiteSpace(template))
        {
            return false;
        }

        Dictionary<string, string> values = ParseKeyValueLines(GetJsonString(command.Value, "PlaceholderValuesText"));
        foreach (KeyValuePair<string, string> parameter in parameterValues)
        {
            values[parameter.Key] = parameter.Value;
        }

        string rendered = PlaceholderRegex.Replace(template, match =>
        {
            string name = match.Groups["name"].Value.Trim();
            return values.TryGetValue(name, out string? value) ? value : match.Value;
        });

        string requestFormat = GetJsonString(command.Value, "RequestFormat");
        string crcMode = GetJsonString(command.Value, "CrcMode");
        if (IsHexRequestFormat(requestFormat))
        {
            string normalizedHex = NormalizeHexString(rendered);
            byte[] payloadBytes = normalizedHex.HexStringToByteArray();
            byte[] checksum = BuildChecksum(payloadBytes, crcMode);
            message = "0x" + payloadBytes.Concat(checksum).ToArray().ByteArrayToHexString();
            return true;
        }

        message = rendered;
        return true;
    }

    private static JsonElement? FindProtocolCommand(string protocolName, string commandName)
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "Config", "Protocol");
        if (!Directory.Exists(directory))
        {
            return null;
        }

        foreach (string filePath in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(ReadPossiblyEncryptedText(filePath));
                JsonElement root = document.RootElement;
                if (!string.Equals(GetJsonString(root, "Name"), protocolName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (root.TryGetProperty("Commands", out JsonElement commandsElement) &&
                    commandsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement commandElement in commandsElement.EnumerateArray())
                    {
                        if (string.Equals(GetJsonString(commandElement, "Name"), commandName, StringComparison.OrdinalIgnoreCase))
                        {
                            return commandElement.Clone();
                        }
                    }
                }

                if (string.Equals(GetJsonString(root, "CommandName"), commandName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(commandName, "ָ�� 1", StringComparison.OrdinalIgnoreCase))
                {
                    return root.Clone();
                }
            }
            catch
            {
                // Ignore broken protocol files during runtime lookup.
            }
        }

        return null;
    }

    #endregion

    #region ϵͳ��������������ֵ����

    private static object?[] BuildMethodArguments(
        MethodInfo method,
        WorkStepOperation operation,
        SchemeWorkStepItem schemeStep,
        IReadOnlyDictionary<string, string> returnValues)
    {
        Dictionary<string, InputParameter> configuredParameters = operation.Parameters
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.ParameterName))
            .GroupBy(parameter => parameter.ParameterName.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        List<InputParameter> orderedParameters = operation.Parameters
            .OrderBy(parameter => parameter.Num)
            .ToList();

        ParameterInfo[] methodParameters = method.GetParameters();
        object?[] args = new object?[methodParameters.Length];
        for (int index = 0; index < methodParameters.Length; index++)
        {
            ParameterInfo parameterInfo = methodParameters[index];
            InputParameter? configuredParameter =
                configuredParameters.TryGetValue(parameterInfo.Name ?? string.Empty, out InputParameter? matched)
                    ? matched
                    : orderedParameters.ElementAtOrDefault(index);
            string value = configuredParameter is null
                ? string.Empty
                : ResolveParameterValue(configuredParameter, schemeStep, returnValues);
            args[index] = ConvertToParameterType(value, parameterInfo.ParameterType);
        }

        return args;
    }

    private static string ResolveParameterValue(
        InputParameter parameter,
        SchemeWorkStepItem schemeStep,
        IReadOnlyDictionary<string, string> returnValues)
    {
        string type = parameter.ParameterType?.Trim() ?? string.Empty;
        string value = parameter.Value?.Trim() ?? string.Empty;

        return type switch
        {
            "设置值" or "工步值" => ResolveSchemeStepParameterValue(parameter, schemeStep),
            "返回值" => returnValues.TryGetValue(value, out string? returnValue) ? returnValue : string.Empty,
            "系统值" or "全局值" => GlobalValues.TryGetValue(value, out string? globalValue) ? globalValue : string.Empty,
            _ => value
        };
    }

    private static string ResolveSchemeStepParameterValue(
        InputParameter parameter,
        SchemeWorkStepItem schemeStep)
    {
        string parameterName = parameter.Value?.Trim() ?? string.Empty;
        return schemeStep.InputParameters.FirstOrDefault(item => string.Equals(
                   item.Name?.Trim(),
                   parameterName,
                   StringComparison.OrdinalIgnoreCase))?.Value?.Trim()
               ?? parameter.ParameterName?.Trim()
               ?? string.Empty;
    }

    #endregion

    #region ��������������ִ�п��ƹ���

    private static async Task DelayWithControlAsync(IControlledExecutionContext context, int delayMilliseconds)
    {
        int remaining = Math.Max(0, delayMilliseconds);
        while (remaining > 0)
        {
            await context.WaitIfPausedAsync().ConfigureAwait(false);
            context.ThrowIfCancellationRequested();

            int delay = Math.Min(remaining, ControlPollingIntervalMilliseconds);
            await Task.Delay(delay, context.CancellationToken).ConfigureAwait(false);
            remaining -= delay;
        }
    }

    private static SchemeWorkStepItem CreateStandaloneSchemeStep()
    {
        return new SchemeWorkStepItem
        {
            StepName = "临时步骤"
        };
    }

    private static bool TryGetStationContexts(
        string stationNo,
        out List<SchemeExecutionContext> contexts,
        out string message)
    {
        string normalizedStationNo = NormalizeRequiredText(stationNo);
        if (string.IsNullOrWhiteSpace(normalizedStationNo))
        {
            contexts = new List<SchemeExecutionContext>();
            message = "Station number is required.";
            return false;
        }

        if (!ActiveExecutions.TryGetValue(normalizedStationNo, out SchemeExecutionContext? context))
        {
            contexts = new List<SchemeExecutionContext>();
            message = $"No schemes are running on station '{normalizedStationNo}'.";
            return false;
        }

        contexts = new List<SchemeExecutionContext> { context };
        message = string.Empty;
        return true;
    }

    #endregion

    #region �������͡��¼���ͨ�ù���

    private static bool IsSystemOperation(WorkStepOperation operation)
    {
        return string.Equals(operation.OperationObjectName?.Trim(), "System", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(operation.OperationObjectName?.Trim(), "System", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLuaOperation(WorkStepOperation operation)
    {
        return string.Equals(operation.OperationObjectName?.Trim(), "Lua", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsJudgeOperation(WorkStepOperation operation)
    {
        return string.Equals(operation.OperationObjectName?.Trim(), "判断", StringComparison.OrdinalIgnoreCase);
    }

    private static void Raise(EventHandler<SchemeExecutionEventArgs>? handler, SchemeExecutionEventArgs args)
    {
        handler?.Invoke(null, args);
    }
    private static void PublishSchemeStatus(
        string stationNo,
        string testStatus,
        SchemeProfile scheme,
        string message,
        bool? isSuccess = null)
    {
        PublishTestStatus(
            stationNo,
            testStatus,
            string.Empty,
            scheme.SchemeName,
            string.Empty,
            message,
            isSuccess);
    }
    private static void PublishTestStatus(
        string stationNo,
        string testStatus,
        string productBarcode,
        string schemeName,
        string productName,
        string message,
        bool? isSuccess = null)
    {
        try
        {
            _eventAggregator?
                .GetEvent<TestExecutionStatusChangedEvent>()
                .Publish(new TestExecutionStatusMessage(
                    stationNo,
                    testStatus,
                    productBarcode,
                    schemeName,
                    productName,
                    message,
                    isSuccess));
        }
        catch
        {
            // Status publication must not interrupt the actual test execution.
        }
    }

    private static void PublishExecutionControlStatus(
        string stationNo,
        string testStatus,
        string schemeName,
        string message)
    {
        PublishTestStatus(
            stationNo,
            testStatus,
            string.Empty,
            schemeName,
            string.Empty,
            message);
    }

    private static object? ConvertToParameterType(string value, Type targetType)
    {
        Type type = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (type == typeof(string))
        {
            return value;
        }

        if (type == typeof(int))
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) ? result : 0;
        }

        if (type == typeof(double))
        {
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result) ? result : 0d;
        }

        if (type == typeof(decimal))
        {
            return decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal result) ? result : 0m;
        }

        if (type == typeof(bool))
        {
            return bool.TryParse(value, out bool result) && result;
        }

        if (type.IsEnum)
        {
            return Enum.TryParse(type, value, true, out object? enumValue)
                ? enumValue
                : Activator.CreateInstance(type);
        }

        return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
    }

    private static Dictionary<string, string> ParseKeyValueLines(string text)
    {
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
        foreach (string rawLine in (text ?? string.Empty).Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
        {
            string line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) ||
                line.StartsWith("#", StringComparison.Ordinal) ||
                line.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            int equalsIndex = line.IndexOf('=');
            if (equalsIndex <= 0)
            {
                continue;
            }

            values[line[..equalsIndex].Trim()] = line[(equalsIndex + 1)..].Trim();
        }

        return values;
    }

    private static byte[] BuildChecksum(byte[] payloadBytes, string crcMode)
    {
        return crcMode switch
        {
            "ModbusCrc16" or "1" => ComputeReflectedCrc16(payloadBytes, 0xFFFF),
            "Crc16Ibm" or "2" => ComputeReflectedCrc16(payloadBytes, 0x0000),
            "Crc16CcittFalse" or "3" => ComputeCrc16CcittFalse(payloadBytes),
            "Crc32" or "4" => ComputeCrc32LittleEndian(payloadBytes),
            _ => Array.Empty<byte>()
        };
    }

    private static byte[] ComputeReflectedCrc16(byte[] data, ushort seed)
    {
        ushort crc = seed;
        foreach (byte value in data)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 0x0001) != 0
                    ? (ushort)((crc >> 1) ^ 0xA001)
                    : (ushort)(crc >> 1);
            }
        }

        return new[] { (byte)(crc & 0xFF), (byte)((crc >> 8) & 0xFF) };
    }

    private static byte[] ComputeCrc16CcittFalse(byte[] data)
    {
        ushort crc = 0xFFFF;
        foreach (byte value in data)
        {
            crc ^= (ushort)(value << 8);
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 0x8000) != 0
                    ? (ushort)((crc << 1) ^ 0x1021)
                    : (ushort)(crc << 1);
            }
        }

        return new[] { (byte)((crc >> 8) & 0xFF), (byte)(crc & 0xFF) };
    }

    private static byte[] ComputeCrc32LittleEndian(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte value in data)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 0x00000001) != 0
                    ? (crc >> 1) ^ 0xEDB88320
                    : crc >> 1;
            }
        }

        crc ^= 0xFFFFFFFF;
        byte[] bytes = BitConverter.GetBytes(crc);
        return BitConverter.IsLittleEndian ? bytes : bytes.Reverse().ToArray();
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

    private static bool IsHexRequestFormat(string requestFormat)
    {
        return string.Equals(requestFormat, "Hex", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(requestFormat, "0", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeHexString(string value)
    {
        string normalized = value.Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase);
        foreach (string separator in new[] { " ", "-", ",", "_", "\r", "\n", "\t" })
        {
            normalized = normalized.Replace(separator, string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return normalized.Trim();
    }

    private static int CompareNumbers(string left, string right)
    {
        decimal leftValue = decimal.TryParse(left, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal parsedLeft)
            ? parsedLeft
            : 0m;
        decimal rightValue = decimal.TryParse(right, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal parsedRight)
            ? parsedRight
            : 0m;
        return leftValue.CompareTo(rightValue);
    }

    private static string GetValue(IReadOnlyList<string> values, int index)
    {
        return index >= 0 && index < values.Count ? values[index] : string.Empty;
    }

    private static bool TextEquals(string? left, string? right)
    {
        return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveParameterDisplayName(InputParameter parameter)
    {
        if (!string.IsNullOrWhiteSpace(parameter.Value))
        {
            return parameter.Value.Trim();
        }

        if (!string.IsNullOrWhiteSpace(parameter.ParameterName))
        {
            return parameter.ParameterName.Trim();
        }

        return string.Empty;
    }

    private static string NormalizeRequiredText(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    #endregion

    #region �ڲ�ִ��������

    private sealed class SchemeExecutionKey : IEquatable<SchemeExecutionKey>
    {
        public SchemeExecutionKey(string stationNo, string schemeName)
        {
            StationNo = stationNo;
            SchemeName = schemeName;
        }

        public string StationNo { get; }

        public string SchemeName { get; }

        public bool Equals(SchemeExecutionKey? other)
        {
            return other is not null &&
                   string.Equals(StationNo, other.StationNo, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(SchemeName, other.SchemeName, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as SchemeExecutionKey);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(StationNo),
                StringComparer.OrdinalIgnoreCase.GetHashCode(SchemeName));
        }
    }

    private sealed class SchemeExecutionContext : IControlledExecutionContext, IDisposable
    {
        private readonly object _pauseLock = new();
        private TaskCompletionSource<bool>? _resumeSignal;
        private bool _isPaused;

        public SchemeExecutionContext(SchemeExecutionKey key, DateTime startTime)
        {
            Key = key;
            StartTime = startTime;
        }

        public SchemeExecutionKey Key { get; }

        public DateTime StartTime { get; }

        public CancellationTokenSource CancellationTokenSource { get; } = new();

        public CancellationToken CancellationToken => CancellationTokenSource.Token;

        public List<string> Logs { get; } = new();

        public bool IsPaused
        {
            get
            {
                lock (_pauseLock)
                {
                    return _isPaused;
                }
            }
        }

        public bool Pause()
        {
            lock (_pauseLock)
            {
                if (_isPaused)
                {
                    return false;
                }

                _isPaused = true;
                _resumeSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                return true;
            }
        }

        public bool Resume()
        {
            TaskCompletionSource<bool>? resumeSignal;
            lock (_pauseLock)
            {
                if (!_isPaused)
                {
                    return false;
                }

                _isPaused = false;
                resumeSignal = _resumeSignal;
                _resumeSignal = null;
            }

            resumeSignal?.TrySetResult(true);
            return true;
        }

        public void Stop()
        {
            CancellationTokenSource.Cancel();
            Resume();
        }

        public async Task WaitIfPausedAsync()
        {
            while (true)
            {
                TaskCompletionSource<bool>? resumeSignal;
                lock (_pauseLock)
                {
                    if (!_isPaused)
                    {
                        return;
                    }

                    resumeSignal = _resumeSignal;
                }

                if (resumeSignal is null)
                {
                    return;
                }

                await resumeSignal.Task.WaitAsync(CancellationToken).ConfigureAwait(false);
            }
        }

        public void ThrowIfCancellationRequested()
        {
            CancellationToken.ThrowIfCancellationRequested();
        }

        public void AddLog(string message)
        {
            lock (Logs)
            {
                Logs.Add(message);
            }
        }

        public SchemeExecutionSnapshot CreateSnapshot()
        {
            DateTime snapshotTime = DateTime.Now;
            return new SchemeExecutionSnapshot(
                Key.StationNo,
                Key.SchemeName,
                IsPaused,
                StartTime,
                null,
                snapshotTime - StartTime,
                Logs.ToList());
        }

        public void Dispose()
        {
            CancellationTokenSource.Dispose();
        }
    }

    #endregion
}

#region ִ�н�����¼�ģ��?

public sealed class SchemeExecutionEventArgs : EventArgs
{
    private SchemeExecutionEventArgs(
        string stationNo,
        string schemeName,
        string? workStepName,
        string? stepName,
        int workStepIndex,
        int stepIndex,
        bool? isSuccess,
        string message,
        object? result,
        DateTime? startTime,
        DateTime? endTime)
    {
        StationNo = stationNo;
        SchemeName = schemeName;
        WorkStepName = workStepName ?? string.Empty;
        StepName = stepName ?? string.Empty;
        WorkStepIndex = workStepIndex;
        StepIndex = stepIndex;
        IsSuccess = isSuccess;
        Message = message;
        Result = result;
        StartTime = startTime;
        EndTime = endTime;
        ExecutionTime = startTime.HasValue && endTime.HasValue
            ? endTime.Value - startTime.Value
            : null;
    }

    public string StationNo { get; }

    public string SchemeName { get; }

    public string WorkStepName { get; }

    public string StepName { get; }

    public int WorkStepIndex { get; }

    public int StepIndex { get; }

    public bool? IsSuccess { get; }

    public string Message { get; }

    public object? Result { get; }

    public DateTime? StartTime { get; }

    public DateTime? EndTime { get; }

    public TimeSpan? ExecutionTime { get; }

    public bool Cancel { get; set; }

    internal static SchemeExecutionEventArgs CreateScheme(
        string stationNo,
        SchemeProfile scheme,
        bool? isSuccess = null,
        string message = "",
        DateTime? startTime = null,
        DateTime? endTime = null)
    {
        return new SchemeExecutionEventArgs(
            stationNo,
            scheme.SchemeName,
            null,
            null,
            0,
            0,
            isSuccess,
            message,
            null,
            startTime,
            endTime);
    }

    internal static SchemeExecutionEventArgs CreateWorkStep(
        string stationNo,
        SchemeProfile scheme,
        SchemeWorkStepItem schemeStep,
        int workStepIndex,
        bool? isSuccess = null,
        string message = "",
        DateTime? startTime = null,
        DateTime? endTime = null)
    {
        return new SchemeExecutionEventArgs(
            stationNo,
            scheme.SchemeName,
            schemeStep.StepName,
            null,
            workStepIndex,
            0,
            isSuccess,
            message,
            null,
            startTime,
            endTime);
    }

    internal static SchemeExecutionEventArgs CreateStep(
        string stationNo,
        SchemeProfile scheme,
        SchemeWorkStepItem schemeStep,
        WorkStepOperation operation,
        int workStepIndex,
        int stepIndex,
        bool? isSuccess = null,
        string message = "",
        object? result = null,
        DateTime? startTime = null,
        DateTime? endTime = null)
    {
        return new SchemeExecutionEventArgs(
            stationNo,
            scheme.SchemeName,
            schemeStep.StepName,
            operation.Summary,
            workStepIndex,
            stepIndex,
            isSuccess,
            message,
            result,
            startTime,
            endTime);
    }
}

public sealed class SchemeExecutionResult
{
    public SchemeExecutionResult(
        bool isSuccess,
        bool isCanceled,
        string message,
        IReadOnlyList<string>? steps = null,
        DateTime? startTime = null,
        DateTime? endTime = null)
    {
        IsSuccess = isSuccess;
        IsCanceled = isCanceled;
        Message = message ?? string.Empty;
        Steps = steps ?? Array.Empty<string>();
        StartTime = startTime;
        EndTime = endTime;
        ExecutionTime = startTime.HasValue && endTime.HasValue
            ? endTime.Value - startTime.Value
            : null;
    }

    public bool IsSuccess { get; }

    public bool IsCanceled { get; }

    public string Message { get; }

    public IReadOnlyList<string> Steps { get; }

    public DateTime? StartTime { get; }

    public DateTime? EndTime { get; }

    public TimeSpan? ExecutionTime { get; }

    public static SchemeExecutionResult CreateSuccess(
        string message,
        IReadOnlyList<string>? steps = null,
        DateTime? startTime = null,
        DateTime? endTime = null)
    {
        return new SchemeExecutionResult(true, false, message, steps, startTime, endTime);
    }

    public static SchemeExecutionResult CreateFailure(
        string message,
        IReadOnlyList<string>? steps = null,
        DateTime? startTime = null,
        DateTime? endTime = null)
    {
        return new SchemeExecutionResult(false, false, message, steps, startTime, endTime);
    }

    public static SchemeExecutionResult CreateCanceled(
        string message,
        IReadOnlyList<string>? steps = null,
        DateTime? startTime = null,
        DateTime? endTime = null)
    {
        return new SchemeExecutionResult(false, true, message, steps, startTime, endTime);
    }
}

public sealed class SchemeExecutionControlActionResult
{
    public SchemeExecutionControlActionResult(bool isSuccess, string message)
    {
        IsSuccess = isSuccess;
        Message = message ?? string.Empty;
    }

    public bool IsSuccess { get; }

    public string Message { get; }

    public static SchemeExecutionControlActionResult CreateSuccess(string message)
    {
        return new SchemeExecutionControlActionResult(true, message);
    }

    public static SchemeExecutionControlActionResult CreateFailure(string message)
    {
        return new SchemeExecutionControlActionResult(false, message);
    }
}

public sealed record SchemeExecutionSnapshot(
    string StationNo,
    string SchemeName,
    bool IsPaused,
    DateTime StartTime,
    DateTime? EndTime,
    TimeSpan ExecutionTime,
    IReadOnlyList<string> Steps);

internal interface IControlledExecutionContext
{
    CancellationToken CancellationToken { get; }

    Task WaitIfPausedAsync();

    void ThrowIfCancellationRequested();
}

internal sealed class SchemeStepExecutionOutput
{
    private SchemeStepExecutionOutput(bool isSuccess, string message, object? result)
    {
        IsSuccess = isSuccess;
        Message = message ?? string.Empty;
        Result = result;
    }

    public bool IsSuccess { get; }

    public string Message { get; }

    public object? Result { get; }

    public static SchemeStepExecutionOutput Success(object? result)
    {
        return new SchemeStepExecutionOutput(true, string.Empty, result);
    }

    public static SchemeStepExecutionOutput Failure(string message, object? result = null)
    {
        return new SchemeStepExecutionOutput(false, message, result);
    }
}

#endregion
