using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace Module.Business.Features.SchemeConfiguration;

public sealed partial class WorkStepProfile
{
    private const string DisplayedViewDataSourceId = "__display_to_view__";

    private ObservableCollection<WorkStepOperation>? _attachedOperations;
    private string _workStepId = string.Empty;
    private string _schemeStepName = string.Empty;
    private ObservableCollection<SchemeWorkStepParameter> _parameters = new();

    private void InitializeSchemeState()
    {
        PropertyChanged += WorkStepProfile_PropertyChanged;
        _attachedOperations = Steps;
        AttachOperations(_attachedOperations);
        AttachParameters(_parameters);
    }

    public string WorkStepId
    {
        get => _workStepId;
        set => SetField(ref _workStepId, (value ?? string.Empty).Trim());
    }

    public string SchemeStepName
    {
        get => string.IsNullOrWhiteSpace(_schemeStepName) ? StepName : _schemeStepName;
        set
        {
            string normalizedValue = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            string storedValue = string.Equals(normalizedValue, StepName, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : normalizedValue;

            SetField(ref _schemeStepName, storedValue);
        }
    }

    public ObservableCollection<WorkStepOperation> Operations
    {
        get => Steps;
        set => Steps = value ?? new ObservableCollection<WorkStepOperation>();
    }

    public ObservableCollection<SchemeWorkStepParameter> Parameters
    {
        get => _parameters;
        set
        {
            if (ReferenceEquals(_parameters, value))
            {
                return;
            }

            DetachParameters(_parameters);
            _parameters = value ?? new ObservableCollection<SchemeWorkStepParameter>();
            AttachParameters(_parameters);
            OnPropertyChanged();
            LastModifiedAt = DateTime.Now;
        }
    }

    private void WorkStepProfile_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Steps))
        {
            if (!ReferenceEquals(_attachedOperations, Steps))
            {
                if (_attachedOperations is not null)
                {
                    DetachOperations(_attachedOperations);
                }

                _attachedOperations = Steps;
                AttachOperations(_attachedOperations);
            }

            OnPropertyChanged(nameof(Operations));
            RefreshOperationSnapshot();
            return;
        }

        if (e.PropertyName == nameof(StepName))
        {
            OnPropertyChanged(nameof(SchemeStepName));
        }
    }

    private void AttachOperations(ObservableCollection<WorkStepOperation> operations)
    {
        operations.CollectionChanged += Operations_CollectionChanged;
        foreach (WorkStepOperation operation in operations)
        {
            operation.PropertyChanged += Operation_PropertyChanged;
        }

        RefreshOperationDisplayOrders(operations);
        RefreshOperationSnapshot(updateLastModified: false);
    }

    private void DetachOperations(ObservableCollection<WorkStepOperation> operations)
    {
        operations.CollectionChanged -= Operations_CollectionChanged;
        foreach (WorkStepOperation operation in operations)
        {
            operation.PropertyChanged -= Operation_PropertyChanged;
        }
    }

    private void Operations_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Move)
        {
            if (sender is ObservableCollection<WorkStepOperation> movedOperations)
            {
                RefreshOperationDisplayOrders(movedOperations);
            }

            RefreshOperationSnapshot();
            return;
        }

        if (e.NewItems is not null)
        {
            foreach (WorkStepOperation operation in e.NewItems.OfType<WorkStepOperation>())
            {
                operation.PropertyChanged += Operation_PropertyChanged;
            }
        }

        if (e.OldItems is not null)
        {
            foreach (WorkStepOperation operation in e.OldItems.OfType<WorkStepOperation>())
            {
                operation.PropertyChanged -= Operation_PropertyChanged;
            }
        }

        if (sender is ObservableCollection<WorkStepOperation> changedOperations)
        {
            RefreshOperationDisplayOrders(changedOperations);
        }

        RefreshOperationSnapshot();
    }

    private void Operation_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorkStepOperation.OperationObject)
            or nameof(WorkStepOperation.InvokeMethod)
            or nameof(WorkStepOperation.LuaScript)
            or nameof(WorkStepOperation.DelayMilliseconds)
            or nameof(WorkStepOperation.Remark)
            or nameof(WorkStepOperation.ParameterCount)
            or nameof(WorkStepOperation.ReturnParameterCount)
            or nameof(WorkStepOperation.DisplayText)
            or nameof(WorkStepOperation.InputParameters)
            or nameof(WorkStepOperation.ReturnParameters))
        {
            RefreshOperationSnapshot();
        }
    }

    private void AttachParameters(ObservableCollection<SchemeWorkStepParameter> parameters)
    {
        parameters.CollectionChanged += Parameters_CollectionChanged;
        foreach (SchemeWorkStepParameter parameter in parameters)
        {
            parameter.PropertyChanged += Parameter_PropertyChanged;
        }
    }

    private void DetachParameters(ObservableCollection<SchemeWorkStepParameter> parameters)
    {
        parameters.CollectionChanged -= Parameters_CollectionChanged;
        foreach (SchemeWorkStepParameter parameter in parameters)
        {
            parameter.PropertyChanged -= Parameter_PropertyChanged;
        }
    }

    private void Parameters_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (SchemeWorkStepParameter parameter in e.NewItems.OfType<SchemeWorkStepParameter>())
            {
                parameter.PropertyChanged += Parameter_PropertyChanged;
            }
        }

        if (e.OldItems is not null)
        {
            foreach (SchemeWorkStepParameter parameter in e.OldItems.OfType<SchemeWorkStepParameter>())
            {
                parameter.PropertyChanged -= Parameter_PropertyChanged;
            }
        }

        LastModifiedAt = DateTime.Now;
    }

    private void Parameter_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SchemeWorkStepParameter.ParameterName)
            or nameof(SchemeWorkStepParameter.ParameterType)
            or nameof(SchemeWorkStepParameter.JudgeType)
            or nameof(SchemeWorkStepParameter.JudgeCondition))
        {
            LastModifiedAt = DateTime.Now;
        }
    }

    private static void RefreshOperationDisplayOrders(ObservableCollection<WorkStepOperation> operations)
    {
        for (int index = 0; index < operations.Count; index++)
        {
            operations[index].DisplayOrder = index + 1;
        }
    }

    private void RefreshOperationSnapshot(bool updateLastModified = true)
    {
        ObservableCollection<SchemeWorkStepParameter> updatedParameters =
            CreateParametersFromOperations(Operations, Parameters);
        if (!HasSameSchemeStepParameters(_parameters, updatedParameters))
        {
            DetachParameters(_parameters);
            _parameters = updatedParameters;
            AttachParameters(_parameters);
            OnPropertyChanged(nameof(Parameters));
        }

        if (updateLastModified)
        {
            LastModifiedAt = DateTime.Now;
        }
    }

    public static WorkStepProfile FromWorkStep(WorkStepProfile workStep)
    {
        return new WorkStepProfile
        {
            IsStartupEnabled = true,
            WorkStepId = workStep.Id,
            StepName = workStep.StepName,
            Operations = new ObservableCollection<WorkStepOperation>(workStep.Steps.Select(operation => operation.Clone())),
            Parameters = CreateParametersFromWorkStep(workStep)
        };
    }

    public WorkStepProfile ToWorkStepProfile()
    {
        return new WorkStepProfile
        {
            Id = string.IsNullOrWhiteSpace(WorkStepId) ? Guid.NewGuid().ToString("N") : WorkStepId,
            StepName = SchemeStepName,
            Steps = new ObservableCollection<WorkStepOperation>(Operations.Select(operation => operation.Clone()))
        };
    }

    public static ObservableCollection<SchemeWorkStepParameter> CreateParametersFromWorkStep(
        WorkStepProfile workStep,
        IEnumerable<SchemeWorkStepParameter>? existingParameters = null)
    {
        return CreateParametersFromOperations(workStep.Steps, existingParameters);
    }

    public static ObservableCollection<SchemeWorkStepParameter> CreateParametersFromOperations(
        IEnumerable<WorkStepOperation> operations,
        IEnumerable<SchemeWorkStepParameter>? existingParameters = null)
    {
        List<WorkStepOperation> operationList = operations
            .Where(operation => operation is not null)
            .ToList();

        List<string> displayJudgeTypeOptions = operationList
            .SelectMany(GetDisplayedReturnParameters)
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.ViewJudgeType))
            .Select(parameter => parameter.ViewJudgeType.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Dictionary<string, SchemeWorkStepParameter> existingBySource = (existingParameters ?? Enumerable.Empty<SchemeWorkStepParameter>())
            .Where(parameter => parameter is not null)
            .GroupBy(parameter => BuildParameterSourceKey(parameter.SourceOperationId, parameter.SourceParameterId), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        Dictionary<string, SchemeWorkStepParameter> existingByName = (existingParameters ?? Enumerable.Empty<SchemeWorkStepParameter>())
            .Where(parameter => parameter is not null && !string.IsNullOrWhiteSpace(parameter.ParameterName))
            .GroupBy(parameter => parameter.ParameterName.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        ObservableCollection<SchemeWorkStepParameter> parameters = new();
        int parameterIndex = 1;
        HashSet<string> addedDisplayedTypeKeys = new(StringComparer.OrdinalIgnoreCase);

        foreach (WorkStepOperation operation in operationList)
        {
            foreach (WorkStepOperationParameter parameter in WorkStepOperationRuntimeMetadata.GetOrderedInputParameters(operation))
            {
                if (!IsSchemeVisibleParameter(parameter))
                {
                    continue;
                }

                string parameterName = ResolveParameterName(parameter, parameterIndex);
                string sourceKey = BuildParameterSourceKey(operation.Id, parameter.Id);

                if (!existingBySource.TryGetValue(sourceKey, out SchemeWorkStepParameter? existingParameter) &&
                    !string.IsNullOrWhiteSpace(parameterName))
                {
                    existingByName.TryGetValue(parameterName, out existingParameter);
                }

                SchemeWorkStepParameter schemeParameter = existingParameter?.Clone() ?? new SchemeWorkStepParameter();
                schemeParameter.SourceOperationId = operation.Id;
                schemeParameter.SourceParameterId = parameter.Id;
                schemeParameter.ParameterName = parameterName;
                schemeParameter.ReplaceJudgeTypeOptions(Array.Empty<string>());
                parameters.Add(schemeParameter);
                parameterIndex++;
            }

            foreach (WorkStepOperationParameter returnParameter in GetDisplayedReturnParameters(operation))
            {
                string displayedParameterName = ResolveDisplayedViewDataName(returnParameter, parameterIndex);
                string displayedTypeKey = ResolveDisplayedViewDataKey(returnParameter, displayedParameterName, parameterIndex);
                if (!addedDisplayedTypeKeys.Add(displayedTypeKey))
                {
                    continue;
                }

                string displayedSourceKey = BuildParameterSourceKey(DisplayedViewDataSourceId, displayedTypeKey);

                if (!existingBySource.TryGetValue(displayedSourceKey, out SchemeWorkStepParameter? displayedExistingParameter) &&
                    !string.IsNullOrWhiteSpace(displayedParameterName))
                {
                    existingByName.TryGetValue(displayedParameterName, out displayedExistingParameter);
                }

                bool isNewDisplayedParameter = displayedExistingParameter is null;
                SchemeWorkStepParameter displayedSchemeParameter = displayedExistingParameter?.Clone() ?? new SchemeWorkStepParameter();
                displayedSchemeParameter.SourceOperationId = DisplayedViewDataSourceId;
                displayedSchemeParameter.SourceParameterId = displayedTypeKey;
                displayedSchemeParameter.ParameterName = displayedParameterName;
                displayedSchemeParameter.ParameterType = isNewDisplayedParameter ? "判断值" : displayedSchemeParameter.ParameterType;
                displayedSchemeParameter.JudgeType = returnParameter.ViewJudgeType;
                displayedSchemeParameter.JudgeCondition = returnParameter.ViewJudgeCondition;
                displayedSchemeParameter.ReplaceJudgeTypeOptions(displayJudgeTypeOptions);
                parameters.Add(displayedSchemeParameter);
                parameterIndex++;
            }
        }

        return parameters;
    }

    private static IEnumerable<WorkStepOperationParameter> GetDisplayedReturnParameters(WorkStepOperation operation)
    {
        return WorkStepOperationRuntimeMetadata.GetOrderedReturnParameters(operation)
            .Where(parameter => parameter.ShowDataToView);
    }
    private static bool IsSchemeVisibleParameter(WorkStepOperationParameter parameter)
    {
        return string.Equals(parameter.Type, "工步值", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveParameterName(WorkStepOperationParameter parameter, int index)
    {
        if (!string.IsNullOrWhiteSpace(parameter.Value))
        {
            return parameter.Value.Trim();
        }

        if (!string.IsNullOrWhiteSpace(parameter.ParameterName))
        {
            return parameter.ParameterName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(parameter.Description))
        {
            return parameter.Description.Trim();
        }

        return $"鍙傛暟 {index}";
    }

    private static string ResolveDisplayedViewDataName(WorkStepOperationParameter parameter, int index)
    {
        if (!string.IsNullOrWhiteSpace(parameter.ViewJudgeType))
        {
            return parameter.ViewJudgeType.Trim();
        }

        if (!string.IsNullOrWhiteSpace(parameter.ViewDataName))
        {
            return parameter.ViewDataName.Trim();
        }

        string returnKey = WorkStepOperationRuntimeMetadata.GetReturnParameterKey(parameter);
        if (!string.IsNullOrWhiteSpace(returnKey))
        {
            return returnKey;
        }

        return $"鏄剧ず鏁版嵁 {index}";
    }

    private static string ResolveDisplayedViewDataKey(WorkStepOperationParameter parameter, string displayedParameterName, int index)
    {
        if (!string.IsNullOrWhiteSpace(parameter.ViewJudgeType))
        {
            return parameter.ViewJudgeType.Trim();
        }

        if (!string.IsNullOrWhiteSpace(displayedParameterName))
        {
            return displayedParameterName.Trim();
        }

        return $"鏄剧ず鏁版嵁_{index}";
    }

    private static string BuildParameterSourceKey(string? operationId, string? parameterId)
    {
        return $"{operationId?.Trim() ?? string.Empty}::{parameterId?.Trim() ?? string.Empty}";
    }

    private static bool HasSameSchemeStepParameters(
        ObservableCollection<SchemeWorkStepParameter> left,
        ObservableCollection<SchemeWorkStepParameter> right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left.Count != right.Count)
        {
            return false;
        }

        for (int index = 0; index < left.Count; index++)
        {
            SchemeWorkStepParameter leftParameter = left[index];
            SchemeWorkStepParameter rightParameter = right[index];
            if (!string.Equals(leftParameter.SourceOperationId, rightParameter.SourceOperationId, StringComparison.Ordinal) ||
                !string.Equals(leftParameter.SourceParameterId, rightParameter.SourceParameterId, StringComparison.Ordinal) ||
                !string.Equals(leftParameter.ParameterName, rightParameter.ParameterName, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(leftParameter.ParameterType, rightParameter.ParameterType, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(leftParameter.JudgeType, rightParameter.JudgeType, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(leftParameter.JudgeCondition, rightParameter.JudgeCondition, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}


