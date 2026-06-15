using ControlLibrary;
using Module.Business.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace Module.Business.Features.SchemeConfiguration;

public sealed class InlineParameterEditorViewModel : ViewModelProperties
{
    private readonly Func<WorkStepOperation, ObservableCollection<WorkStepOperationParameter>> _createReturnParameters;
    private readonly Func<WorkStepOperation, ObservableCollection<WorkStepOperationParameter>, bool> _hasModifiedParameters;
    private bool _isOpen;
    private WorkStepOperation? _targetOperation;
    private string _operationSummary = string.Empty;

    public InlineParameterEditorViewModel(
        Func<WorkStepOperation, ObservableCollection<WorkStepOperationParameter>> createReturnParameters,
        Func<WorkStepOperation, ObservableCollection<WorkStepOperationParameter>, bool> hasModifiedParameters)
    {
        _createReturnParameters = createReturnParameters;
        _hasModifiedParameters = hasModifiedParameters;
    }

    public bool IsOpen
    {
        get => _isOpen;
        private set => SetField(ref _isOpen, value);
    }

    public WorkStepOperation? TargetOperation
    {
        get => _targetOperation;
        private set => SetField(ref _targetOperation, value);
    }

    public string OperationSummary
    {
        get => _operationSummary;
        private set => SetField(ref _operationSummary, value);
    }

    public ObservableCollection<InlineInputParameterRow> InputParameterRows { get; } = new();

    public ObservableCollection<InlineReturnParameterRow> ReturnParameterRows { get; } = new();

    public IReadOnlyList<string> ParsedReturnKeys { get; private set; } = Array.Empty<string>();

    /// <summary>
    /// 打开对应的编辑界面或抽屉。
    /// </summary>
    public void Open(WorkStepOperation operation, IEnumerable<WorkStepOperation> currentOperations)
    {
        TargetOperation = operation ?? throw new ArgumentNullException(nameof(operation));
        OperationSummary = $"{operation.OperationObject}.{operation.InvokeMethod}";
        ReplaceInputRows(CreateInputParameterRows(operation.Parameters));
        ReplaceReturnRows(CreateReturnParameterRows(operation, out IReadOnlyList<string> parsedReturnKeys));
        ParsedReturnKeys = parsedReturnKeys;
        RefreshInputValueOptions(currentOperations);
        IsOpen = true;
    }

    /// <summary>
    /// 关闭对应的编辑界面或抽屉。
    /// </summary>
    public void Close()
    {
        IsOpen = false;
        TargetOperation = null;
        OperationSummary = string.Empty;
        ParsedReturnKeys = Array.Empty<string>();
        InputParameterRows.Clear();
        ReturnParameterRows.Clear();
    }

    /// <summary>
    /// 刷新对应的界面或业务状态。
    /// </summary>
    public void RefreshInputValueOptions(IEnumerable<WorkStepOperation> currentOperations)
    {
        if (TargetOperation is null)
        {
            return;
        }

        List<string> options = BuildInputReturnValueOptions(currentOperations, TargetOperation)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (InlineInputParameterRow row in InputParameterRows)
        {
            ReplaceStringOptions(row.ValueOptions, options);
        }
    }

    /// <summary>
    /// 应用当前编辑结果到目标对象。
    /// </summary>
    public bool Apply()
    {
        if (TargetOperation is null)
        {
            return false;
        }

        SanitizeReturnParameterTable();
        ObservableCollection<WorkStepOperationParameter> parameters = BuildInputParameters();
        TargetOperation.Parameters = parameters;
        ApplyReturnParameters(TargetOperation);
        TargetOperation.AreParametersModified = _hasModifiedParameters(TargetOperation, parameters);
        return true;
    }

    /// <summary>
    /// 清理返回参数表格中的无效显示项。
    /// </summary>
    public void SanitizeReturnParameterTable()
    {
        HashSet<string> seenKeys = new(StringComparer.OrdinalIgnoreCase);
        List<InlineReturnParameterRow> rowsToRemove = new();
        foreach (InlineReturnParameterRow row in ReturnParameterRows)
        {
            if (IsEmptyReturnParameterRow(row))
            {
                rowsToRemove.Add(row);
                continue;
            }

            string returnValue = row.Key;
            if (ParsedReturnKeys.Count > 0 &&
                !ParsedReturnKeys.Any(key => string.Equals(key, returnValue, StringComparison.OrdinalIgnoreCase)))
            {
                rowsToRemove.Add(row);
                continue;
            }

            if (!seenKeys.Add(returnValue))
            {
                rowsToRemove.Add(row);
            }
        }

        foreach (InlineReturnParameterRow row in rowsToRemove)
        {
            ReturnParameterRows.Remove(row);
        }
    }

    /// <summary>
    /// 构建并返回对应的业务数据。
    /// </summary>
    private ObservableCollection<WorkStepOperationParameter> BuildInputParameters()
    {
        List<WorkStepOperationParameter> parameters = new();
        foreach (InlineInputParameterRow row in InputParameterRows)
        {
            parameters.Add(new WorkStepOperationParameter
            {
                Id = row.Id,
                Sequence = Math.Max(1, row.Sequence),
                Name = row.Type,
                ParameterName = row.ParameterName,
                Value = row.Value,
                Remark = row.Description
            });
        }

        return new ObservableCollection<WorkStepOperationParameter>(
            parameters
                .OrderBy(parameter => parameter.Sequence)
                .Select((parameter, index) =>
                {
                    parameter.Sequence = index + 1;
                    return parameter;
                }));
    }

    /// <summary>
    /// 应用当前编辑结果到目标对象。
    /// </summary>
    private void ApplyReturnParameters(WorkStepOperation targetOperation)
    {
        List<InlineReturnParameterRow> rows = ReturnParameterRows
            .Where(item => !IsEmptyReturnParameterRow(item))
            .Where(IsAllowedReturnParameterRow)
            .ToList();

        InlineReturnParameterRow? row = rows.FirstOrDefault(item =>
            !string.IsNullOrWhiteSpace(targetOperation.ReturnValue) &&
            string.Equals(
                item.Key,
                targetOperation.ReturnValue.Trim(),
                StringComparison.OrdinalIgnoreCase)) ??
            rows.FirstOrDefault(item => item.ShowDataToView) ??
            (rows.Count == 1 ? rows[0] : null);

        if (row is null)
        {
            targetOperation.ReturnValue = string.Empty;
            targetOperation.ShowDataToView = false;
            targetOperation.ViewDataName = string.Empty;
            targetOperation.ViewJudgeType = string.Empty;
            targetOperation.ViewJudgeCondition = string.Empty;
            return;
        }

        targetOperation.ReturnValue = row.Key;
        targetOperation.ShowDataToView = row.ShowDataToView;
        targetOperation.ViewDataName = row.ViewDataName?.Trim() ?? string.Empty;
        targetOperation.ViewJudgeType = row.ViewJudgeType?.Trim() ?? string.Empty;
        targetOperation.ViewJudgeCondition = row.ViewJudgeCondition?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// 根据操作参数创建输入参数行集合。
    /// </summary>
    private static IEnumerable<InlineInputParameterRow> CreateInputParameterRows(
        IEnumerable<WorkStepOperationParameter> parameters)
    {
        return parameters
            .OrderBy(parameter => parameter.Sequence)
            .Select(parameter => new InlineInputParameterRow
            {
                Id = parameter.Id,
                Sequence = parameter.Sequence,
                Type = parameter.Type,
                ParameterName = parameter.ParameterName,
                Value = parameter.Value,
                Description = parameter.Description
            });
    }

    /// <summary>
    /// 根据操作返回值配置创建返回参数行集合。
    /// </summary>
    private IEnumerable<InlineReturnParameterRow> CreateReturnParameterRows(
        WorkStepOperation operation,
        out IReadOnlyList<string> parsedReturnKeys)
    {
        ProtocolCommandReturnMetadata metadata = ProtocolCommandMetadataStore.GetReturnMetadata(
            operation.ProtocolName,
            operation.CommandName);
        parsedReturnKeys = metadata.ReturnValueKeys;
        if (metadata.IsSendOnly)
        {
            return Enumerable.Empty<InlineReturnParameterRow>();
        }

        if (metadata.ReturnValueKeys.Count > 0)
        {
            return metadata.ReturnValueKeys.Select(parsedKey =>
            {
                bool isCurrentReturnValue = string.Equals(parsedKey, operation.ReturnValue, StringComparison.OrdinalIgnoreCase);
                return new InlineReturnParameterRow
                {
                    Key = parsedKey,
                    ShowDataToView = isCurrentReturnValue && operation.ShowDataToView,
                    ViewDataName = isCurrentReturnValue ? operation.ViewDataName : string.Empty,
                    ViewJudgeType = isCurrentReturnValue ? operation.ViewJudgeType : string.Empty,
                    ViewJudgeCondition = isCurrentReturnValue ? operation.ViewJudgeCondition : string.Empty
                };
            });
        }

        ObservableCollection<WorkStepOperationParameter> inferredReturnParameters = _createReturnParameters(operation);
        if (inferredReturnParameters.Count > 0)
        {
            return inferredReturnParameters
                .Where(parameter => !string.IsNullOrWhiteSpace(parameter.ParameterName))
                .Select(parameter =>
                {
                    bool isCurrentReturnValue = string.Equals(parameter.ParameterName, operation.ReturnValue, StringComparison.OrdinalIgnoreCase);
                    return new InlineReturnParameterRow
                    {
                        Key = parameter.ParameterName,
                        ShowDataToView = isCurrentReturnValue && operation.ShowDataToView,
                        ViewDataName = isCurrentReturnValue ? operation.ViewDataName : string.Empty,
                        ViewJudgeType = isCurrentReturnValue ? operation.ViewJudgeType : string.Empty,
                        ViewJudgeCondition = isCurrentReturnValue ? operation.ViewJudgeCondition : string.Empty
                    };
                });
        }

        if (!HasReturnParameter(operation))
        {
            return Enumerable.Empty<InlineReturnParameterRow>();
        }

        return new[]
        {
            new InlineReturnParameterRow
            {
                Key = operation.ReturnValue,
                ShowDataToView = operation.ShowDataToView,
                ViewDataName = operation.ViewDataName,
                ViewJudgeType = operation.ViewJudgeType,
                ViewJudgeCondition = operation.ViewJudgeCondition
            }
        };
    }

    /// <summary>
    /// 判断是否满足指定业务条件。
    /// </summary>
    private bool IsAllowedReturnParameterRow(InlineReturnParameterRow row)
    {
        if (ParsedReturnKeys.Count == 0)
        {
            return true;
        }

        string returnValue = row.Key;
        return ParsedReturnKeys.Any(key => string.Equals(key, returnValue, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 构建并返回对应的业务数据。
    /// </summary>
    private IEnumerable<string> BuildInputReturnValueOptions(
        IEnumerable<WorkStepOperation> currentOperations,
        WorkStepOperation targetOperation)
    {
        List<WorkStepOperation> operations = currentOperations
            .Where(operation => operation is not null)
            .ToList();

        int targetIndex = operations.FindIndex(operation =>
            ReferenceEquals(operation, targetOperation) ||
            string.Equals(operation.Id, targetOperation.Id, StringComparison.Ordinal));

        if (targetIndex <= 0)
        {
            return Enumerable.Empty<string>();
        }

        return operations
            .Take(targetIndex)
            .SelectMany(operation => _createReturnParameters(operation)
                .Select(parameter => parameter.ParameterName));
    }

    /// <summary>
    /// 用新输入参数行替换当前集合。
    /// </summary>
    private void ReplaceInputRows(IEnumerable<InlineInputParameterRow> rows)
    {
        InputParameterRows.Clear();
        foreach (InlineInputParameterRow row in rows)
        {
            InputParameterRows.Add(row);
        }
    }

    /// <summary>
    /// 用新返回参数行替换当前集合。
    /// </summary>
    private void ReplaceReturnRows(IEnumerable<InlineReturnParameterRow> rows)
    {
        ReturnParameterRows.Clear();
        foreach (InlineReturnParameterRow row in rows)
        {
            ReturnParameterRows.Add(row);
        }
    }

    /// <summary>
    /// 用候选项集合替换字符串选项集合。
    /// </summary>
    private static void ReplaceStringOptions(ObservableCollection<string> target, IEnumerable<string> source)
    {
        List<string> options = source
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Select(option => option.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(option => option, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (target.SequenceEqual(options, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        target.Clear();
        foreach (string option in options)
        {
            target.Add(option);
        }
    }

    /// <summary>
    /// 判断操作是否配置了返回参数。
    /// </summary>
    private static bool HasReturnParameter(WorkStepOperation operation)
    {
        return !string.IsNullOrWhiteSpace(operation.ReturnValue) ||
               operation.ShowDataToView ||
               !string.IsNullOrWhiteSpace(operation.ViewDataName) ||
               !string.IsNullOrWhiteSpace(operation.ViewJudgeType) ||
               !string.IsNullOrWhiteSpace(operation.ViewJudgeCondition);
    }

    /// <summary>
    /// 判断是否满足指定业务条件。
    /// </summary>
    private static bool IsEmptyReturnParameterRow(InlineReturnParameterRow row)
    {
        return string.IsNullOrWhiteSpace(row.Key) &&
               !row.ShowDataToView &&
               string.IsNullOrWhiteSpace(row.ViewDataName) &&
               string.IsNullOrWhiteSpace(row.ViewJudgeType) &&
               string.IsNullOrWhiteSpace(row.ViewJudgeCondition);
    }

    public sealed class InlineInputParameterRow : ViewModelProperties
    {
        private string _type = string.Empty;
        private string _value = string.Empty;

        public string Id { get; set; } = string.Empty;

        public int Sequence { get; set; }

        public string Type
        {
            get => _type;
            set => SetField(ref _type, value?.Trim() ?? string.Empty);
        }

        public string ParameterName { get; set; } = string.Empty;

        public string Value
        {
            get => _value;
            set => SetField(ref _value, value ?? string.Empty);
        }

        public string Description { get; set; } = string.Empty;

        public ObservableCollection<string> ValueOptions { get; } = new();
    }

    public sealed class InlineReturnParameterRow : ViewModelProperties
    {
        /// <summary>
        /// 判断条件模板显示项。
        /// </summary>
        public sealed record JudgeTemplateOption(string DisplayText, string Value)
        {
            /// <summary>
            /// 返回模板显示文本。
            /// </summary>
            public override string ToString() => DisplayText;
        }

        private static readonly IReadOnlyList<JudgeTemplateOption> DefaultJudgeTemplateOptions =
            Array.AsReadOnly(new[]
            {
                new JudgeTemplateOption(">", ">"),
                new JudgeTemplateOption(">=", ">="),
                new JudgeTemplateOption("<", "<"),
                new JudgeTemplateOption("<=", "<="),
                new JudgeTemplateOption("==", "=="),
                new JudgeTemplateOption("!=", "!="),
                new JudgeTemplateOption("<{0}<", "<{0}<"),
                new JudgeTemplateOption("<={0}<=", "<={0}<="),
                new JudgeTemplateOption("()", "()"),
                new JudgeTemplateOption("!()", "!()"),
                new JudgeTemplateOption("黑名单", "黑名单"),
                new JudgeTemplateOption("白名单", "白名单"),
                new JudgeTemplateOption("NA", "NA")
            });

        private string _key = string.Empty;
        private bool _showDataToView;
        private string _viewDataName = string.Empty;
        private string _viewJudgeType = string.Empty;
        private string _firstJudgeConditionValue = string.Empty;
        private string _secondJudgeConditionValue = string.Empty;

        public string Key
        {
            get => _key;
            set => SetField(ref _key, value?.Trim() ?? string.Empty);
        }

        public bool ShowDataToView
        {
            get => _showDataToView;
            set => SetField(ref _showDataToView, value);
        }

        public string ViewDataName
        {
            get => _viewDataName;
            set => SetField(ref _viewDataName, value ?? string.Empty);
        }

        public IReadOnlyList<JudgeTemplateOption> JudgeTemplateOptions => DefaultJudgeTemplateOptions;

        public string ViewJudgeType
        {
            get => _viewJudgeType;
            set
            {
                string normalizedValue = value?.Trim() ?? string.Empty;
                bool wasRangeTemplate = IsRangeJudgeTemplate;
                if (!SetField(ref _viewJudgeType, normalizedValue))
                {
                    return;
                }

                if (!IsRangeJudgeTemplate && wasRangeTemplate)
                {
                    _firstJudgeConditionValue = BuildRangeConditionValue();
                    _secondJudgeConditionValue = string.Empty;
                }
                else if (IsRangeJudgeTemplate && !wasRangeTemplate)
                {
                    ParseRangeConditionValue(_firstJudgeConditionValue);
                }

                OnPropertyChanged(nameof(ViewJudgeCondition));
                OnPropertyChanged(nameof(IsRangeJudgeTemplate));
                OnPropertyChanged(nameof(FirstJudgeConditionValue));
                OnPropertyChanged(nameof(SecondJudgeConditionValue));
            }
        }

        public string ViewJudgeCondition
        {
            get => IsRangeJudgeTemplate
                ? BuildRangeConditionValue()
                : _firstJudgeConditionValue.Trim();
            set => ApplyJudgeCondition(value);
        }

        public string FirstJudgeConditionValue
        {
            get => _firstJudgeConditionValue;
            set
            {
                if (SetField(ref _firstJudgeConditionValue, value?.Trim() ?? string.Empty))
                {
                    OnPropertyChanged(nameof(ViewJudgeCondition));
                }
            }
        }

        public string SecondJudgeConditionValue
        {
            get => _secondJudgeConditionValue;
            set
            {
                if (SetField(ref _secondJudgeConditionValue, value?.Trim() ?? string.Empty))
                {
                    OnPropertyChanged(nameof(ViewJudgeCondition));
                }
            }
        }

        public bool IsRangeJudgeTemplate =>
            string.Equals(ViewJudgeType, "<{0}<", StringComparison.Ordinal) ||
            string.Equals(ViewJudgeType, "<={0}<=", StringComparison.Ordinal);

        /// <summary>
        /// 应用当前编辑结果到目标对象。
        /// </summary>
        private void ApplyJudgeCondition(string? value)
        {
            string normalizedValue = value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(ViewJudgeType))
            {
                string inferredTemplate = InferJudgeTemplate(normalizedValue);
                if (!string.IsNullOrWhiteSpace(inferredTemplate))
                {
                    _viewJudgeType = inferredTemplate;
                    OnPropertyChanged(nameof(ViewJudgeType));
                    OnPropertyChanged(nameof(IsRangeJudgeTemplate));
                }
            }

            if (IsRangeJudgeTemplate)
            {
                ParseRangeConditionValue(normalizedValue);
            }
            else
            {
                _firstJudgeConditionValue = StripSimpleTemplate(normalizedValue, ViewJudgeType);
                _secondJudgeConditionValue = string.Empty;
            }

            OnPropertyChanged(nameof(FirstJudgeConditionValue));
            OnPropertyChanged(nameof(SecondJudgeConditionValue));
            OnPropertyChanged(nameof(ViewJudgeCondition));
        }

        /// <summary>
        /// 构建并返回对应的业务数据。
        /// </summary>
        private string BuildRangeConditionValue()
        {
            string firstValue = _firstJudgeConditionValue.Trim();
            string secondValue = _secondJudgeConditionValue.Trim();
            if (string.IsNullOrWhiteSpace(firstValue) && string.IsNullOrWhiteSpace(secondValue))
            {
                return string.Empty;
            }

            return $"{firstValue}|{secondValue}";
        }

        /// <summary>
        /// 解析范围判断条件中的边界值。
        /// </summary>
        private void ParseRangeConditionValue(string value)
        {
            string normalizedValue = value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedValue))
            {
                _firstJudgeConditionValue = string.Empty;
                _secondJudgeConditionValue = string.Empty;
                return;
            }

            string[] placeholderParts = normalizedValue.Split(
                new[] { "{0}" },
                StringSplitOptions.None);
            if (placeholderParts.Length >= 2)
            {
                _firstJudgeConditionValue = TrimRangeBoundary(placeholderParts[0]);
                _secondJudgeConditionValue = TrimRangeBoundary(placeholderParts[1]);
                return;
            }

            string[] delimiterParts = normalizedValue.Split(
                new[] { '|', ',', ';', '，', '；' },
                2,
                StringSplitOptions.TrimEntries);
            _firstJudgeConditionValue = delimiterParts.ElementAtOrDefault(0)?.Trim() ?? string.Empty;
            _secondJudgeConditionValue = delimiterParts.ElementAtOrDefault(1)?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// 根据判断条件推断模板类型。
        /// </summary>
        private static string InferJudgeTemplate(string condition)
        {
            string normalizedCondition = condition?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedCondition))
            {
                return string.Empty;
            }

            if (normalizedCondition.Contains("{0}", StringComparison.Ordinal))
            {
                return normalizedCondition.Contains("<={0}<=", StringComparison.Ordinal)
                    ? "<={0}<="
                    : "<{0}<";
            }

            foreach (JudgeTemplateOption template in DefaultJudgeTemplateOptions
                         .Where(template => !IsRangeTemplate(template.Value))
                         .OrderByDescending(template => template.Value.Length))
            {
                if (normalizedCondition.StartsWith(template.Value, StringComparison.OrdinalIgnoreCase))
                {
                    return template.Value;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// 判断是否满足指定业务条件。
        /// </summary>
        private static bool IsRangeTemplate(string template)
        {
            return string.Equals(template, "<{0}<", StringComparison.Ordinal) ||
                   string.Equals(template, "<={0}<=", StringComparison.Ordinal);
        }

        /// <summary>
        /// 从判断条件中移除简单模板前缀。
        /// </summary>
        private static string StripSimpleTemplate(string condition, string template)
        {
            string normalizedCondition = condition?.Trim() ?? string.Empty;
            string normalizedTemplate = template?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedCondition) ||
                string.IsNullOrWhiteSpace(normalizedTemplate))
            {
                return normalizedCondition;
            }

            if (normalizedCondition.StartsWith("{0}", StringComparison.Ordinal))
            {
                normalizedCondition = normalizedCondition[3..].Trim();
            }

            if (normalizedCondition.StartsWith(normalizedTemplate, StringComparison.OrdinalIgnoreCase))
            {
                normalizedCondition = normalizedCondition[normalizedTemplate.Length..].Trim();
            }

            return normalizedCondition;
        }

        /// <summary>
        /// 清理范围边界值的空白和括号。
        /// </summary>
        private static string TrimRangeBoundary(string value)
        {
            return (value ?? string.Empty).Trim().Trim('<', '>', '=', ' ');
        }
    }
}
