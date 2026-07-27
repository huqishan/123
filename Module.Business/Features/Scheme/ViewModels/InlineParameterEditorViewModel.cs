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
    private bool _isOpen;
    private WorkStepOperation? _targetOperation;
    private string _operationSummary = string.Empty;
    private ObservableCollection<InputParameter> _originalParameters = new();

    public InlineParameterEditorViewModel()
    {
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

    public void Open(WorkStepOperation operation, IEnumerable<WorkStepOperation> currentOperations)
    {
        TargetOperation = operation ?? throw new ArgumentNullException(nameof(operation));
        OperationSummary = $"{operation.OperationObjectName}.{operation.PCommandName}";
        _originalParameters = new ObservableCollection<InputParameter>(
            operation.Parameters.Select(p => p.Clone()));
        ReplaceInputRows(CreateInputParameterRows(operation.Parameters));
        ReplaceReturnRows(CreateReturnParameterRows(operation));
        RefreshInputValueOptions(currentOperations);
        IsOpen = true;
    }

    public void Close()
    {
        IsOpen = false;
        TargetOperation = null;
        OperationSummary = string.Empty;
        _originalParameters = new ObservableCollection<InputParameter>();
        InputParameterRows.Clear();
        ReturnParameterRows.Clear();
    }

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

    public bool Apply()
    {
        if (TargetOperation is null)
        {
            return false;
        }

        SanitizeReturnParameterTable();
        ObservableCollection<InputParameter> parameters = BuildInputParameters();
        TargetOperation.Parameters = parameters;
        ApplyReturnParameters(TargetOperation, ReturnParameterRows);
        TargetOperation.IsEditParameter = HasParameterChanges(parameters);
        return true;
    }

    public void SanitizeReturnParameterTable()
    {
        SanitizeReturnParameterTable(ReturnParameterRows);
    }

    public static void SanitizeReturnParameterTable(
        ObservableCollection<InlineReturnParameterRow> returnParameterRows)
    {
        HashSet<string> seenKeys = new(StringComparer.OrdinalIgnoreCase);
        List<InlineReturnParameterRow> rowsToRemove = new();
        foreach (InlineReturnParameterRow row in returnParameterRows)
        {
            if (IsEmptyReturnParameterRow(row))
            {
                rowsToRemove.Add(row);
                continue;
            }

            if (!seenKeys.Add(row.Key))
            {
                rowsToRemove.Add(row);
            }
        }

        foreach (InlineReturnParameterRow row in rowsToRemove)
        {
            returnParameterRows.Remove(row);
        }
    }

    private bool HasParameterChanges(ObservableCollection<InputParameter> parameters)
    {
        if (parameters.Count != _originalParameters.Count)
        {
            return true;
        }

        List<InputParameter> original = _originalParameters
            .OrderBy(p => p.Num)
            .ToList();
        List<InputParameter> current = parameters
            .OrderBy(p => p.Num)
            .ToList();

        for (int i = 0; i < original.Count; i++)
        {
            if (!string.Equals(original[i].ParameterType, current[i].ParameterType, StringComparison.Ordinal) ||
                !string.Equals(original[i].ParameterName, current[i].ParameterName, StringComparison.Ordinal) ||
                !string.Equals(original[i].Value, current[i].Value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private ObservableCollection<InputParameter> BuildInputParameters()
    {
        List<InputParameter> parameters = new();
        foreach (InlineInputParameterRow row in InputParameterRows)
        {
            parameters.Add(new InputParameter
            {
                Id = row.Id,
                Num = Math.Max(1, row.Sequence),
                ParameterType = row.Type,
                ParameterName = row.ParameterName,
                Value = row.Value
            });
        }

        return new ObservableCollection<InputParameter>(
            parameters
                .OrderBy(parameter => parameter.Num)
                .Select((parameter, index) =>
                {
                    parameter.Num = index + 1;
                    return parameter;
                }));
    }

    public static void ApplyReturnParameters(
        WorkStepOperation targetOperation,
        IEnumerable<InlineReturnParameterRow> returnParameterRows)
    {
        List<InlineReturnParameterRow> rows = returnParameterRows
            .Where(item => !IsEmptyReturnParameterRow(item))
            .ToList();

        List<ReturnValue> returnValues = rows.Select(row => new ReturnValue
        {
            ReturnParameterName = row.Key,
            IsShowView = row.ShowDataToView,
            JudgeType = row.ViewJudgeType,
            JudgeSymbols = row.JudgeSymbols,
            JudgeValue = row.FirstJudgeConditionValue,
            OriginalUnit = string.Empty,
            ShowUnit = string.Empty,
            DecimalPlaces = 0
        }).ToList();

        targetOperation.ReturnValues = new ObservableCollection<ReturnValue>(returnValues);
    }

    private static IEnumerable<InlineInputParameterRow> CreateInputParameterRows(
        IEnumerable<InputParameter> parameters)
    {
        return parameters
            .OrderBy(parameter => parameter.Num)
            .Select(parameter => new InlineInputParameterRow
            {
                Id = parameter.Id,
                Sequence = parameter.Num,
                Type = parameter.ParameterType,
                ParameterName = parameter.ParameterName,
                Value = parameter.Value
            });
    }

    public IEnumerable<InlineReturnParameterRow> CreateReturnParameterRows(WorkStepOperation operation)
    {
        ReturnValue? returnValue = operation.ReturnValues.FirstOrDefault();
        if (returnValue is null)
        {
            return Enumerable.Empty<InlineReturnParameterRow>();
        }

        return new[]
        {
            new InlineReturnParameterRow
            {
                Key = returnValue.ReturnParameterName,
                ShowDataToView = returnValue.IsShowView,
                ViewJudgeType = returnValue.JudgeType,
                JudgeSymbols = returnValue.JudgeSymbols,
                FirstJudgeConditionValue = returnValue.JudgeValue,
                SecondJudgeConditionValue = string.Empty
            }
        };
    }

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
            .SelectMany(operation => operation.ReturnValues)
            .Select(returnValue => returnValue.ReturnParameterName);
    }

    private void ReplaceInputRows(IEnumerable<InlineInputParameterRow> rows)
    {
        InputParameterRows.Clear();
        foreach (InlineInputParameterRow row in rows)
        {
            InputParameterRows.Add(row);
        }
    }

    private void ReplaceReturnRows(IEnumerable<InlineReturnParameterRow> rows)
    {
        ReturnParameterRows.Clear();
        foreach (InlineReturnParameterRow row in rows)
        {
            ReturnParameterRows.Add(row);
        }
    }

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

    private static bool IsEmptyReturnParameterRow(InlineReturnParameterRow row)
    {
        return string.IsNullOrWhiteSpace(row.Key) &&
               !row.ShowDataToView &&
               string.IsNullOrWhiteSpace(row.ViewJudgeType) &&
               string.IsNullOrWhiteSpace(row.FirstJudgeConditionValue);
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

        public ObservableCollection<string> ValueOptions { get; } = new();
    }

    public sealed class InlineReturnParameterRow : ViewModelProperties
    {
        public sealed record JudgeTemplateOption(string DisplayText, string Value)
        {
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
        private string _viewJudgeType = string.Empty;
        private string _judgeSymbols = string.Empty;
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

                if (IsRangeJudgeTemplate != wasRangeTemplate)
                {
                    if (IsRangeJudgeTemplate)
                    {
                        JudgeSymbols = BuildRangeConditionValue();
                        ParseRangeConditionValue(_firstJudgeConditionValue);
                    }
                    else
                    {
                        _firstJudgeConditionValue = JudgeSymbols;
                        _secondJudgeConditionValue = string.Empty;
                        JudgeSymbols = string.Empty;
                    }
                }

                OnPropertyChanged(nameof(ViewJudgeCondition));
                OnPropertyChanged(nameof(IsRangeJudgeTemplate));
                OnPropertyChanged(nameof(FirstJudgeConditionValue));
                OnPropertyChanged(nameof(SecondJudgeConditionValue));
                OnPropertyChanged(nameof(JudgeSymbols));
            }
        }

        public string JudgeSymbols
        {
            get => _judgeSymbols;
            set
            {
                if (SetField(ref _judgeSymbols, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(ViewJudgeCondition));
                }
            }
        }

        public string ViewJudgeCondition
        {
            get => IsRangeJudgeTemplate
                ? BuildRangeConditionValue()
                : FirstJudgeConditionValue.Trim();
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
                _judgeSymbols = ViewJudgeType;
            }

            OnPropertyChanged(nameof(FirstJudgeConditionValue));
            OnPropertyChanged(nameof(SecondJudgeConditionValue));
            OnPropertyChanged(nameof(JudgeSymbols));
            OnPropertyChanged(nameof(ViewJudgeCondition));
        }

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

        private static bool IsRangeTemplate(string template)
        {
            return string.Equals(template, "<{0}<", StringComparison.Ordinal) ||
                   string.Equals(template, "<={0}<=", StringComparison.Ordinal);
        }

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

        private static string TrimRangeBoundary(string value)
        {
            return (value ?? string.Empty).Trim().Trim('<', '>', '=', ' ');
        }
    }
}
