using ControlLibrary;
using Module.Business.Features.OperationEditing.ViewModels.PresentationModels;
using Module.Business.Features.Scheme.ViewModels.PresentationModels;
using Module.Business.Features.WorkStep.Services;
using Module.Business.Features.WorkStep.ViewModels.PresentationModels;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Module.Business.Features.Scheme.ViewModels;

/// <summary>
/// 方案判断条件编辑器，按工步连续展示并统一编辑方案工步的输入参数和返回参数。
/// 本编辑器只回写内存中的方案工步参数，不负责方案持久化。
/// </summary>
public sealed class SchemeConditionEditorViewModel : ViewModelProperties
{
    #region 私有字段

    private readonly List<SchemeConditionWorkStepGroup> _allGroups = new();
    private SchemeProfile? _editingScheme;
    private bool _isOpen;
    private string _searchText = string.Empty;
    private string _selectedParameterFilter = "全部参数";

    #endregion

    #region 构造与集合

    public SchemeConditionEditorViewModel()
    {
        ParameterFilters = new ObservableCollection<string> { "全部参数", "输入参数", "返回参数" };
        JudgeOperators = new ObservableCollection<string>
        {
            "NA", "=", "≠", ">", "≥", "<", "≤", "＜{0}＜", "≤{0}≤", "()", "!()", "黑名单", "白名单"
        };
        SaveCommand = new RelayCommand(_ => Save());
        CloseCommand = new RelayCommand(_ => Close());
    }

    /// <summary>当前筛选后显示的工步分组。</summary>
    public ObservableCollection<SchemeConditionWorkStepGroup> Groups { get; } = new();

    /// <summary>参数类型固定筛选集合。</summary>
    public ObservableCollection<string> ParameterFilters { get; }

    /// <summary>返回参数判断符号固定集合。</summary>
    public ObservableCollection<string> JudgeOperators { get; }

    #endregion

    #region 展示状态与命令

    public bool IsOpen { get => _isOpen; private set => SetField(ref _isOpen, value); }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value ?? string.Empty)) RefreshGroups();
        }
    }

    public string SelectedParameterFilter
    {
        get => _selectedParameterFilter;
        set
        {
            if (SetField(ref _selectedParameterFilter, value ?? "全部参数")) RefreshGroups();
        }
    }

    public string Title => _editingScheme is null ? "判断条件配置" : $"判断条件配置 · {_editingScheme.SchemeName}";

    public int WorkStepCount => _allGroups.Count;

    public int InputParameterCount => _allGroups.Sum(group => group.Items.Count(item => item.IsInputParameter));

    public int ReturnParameterCount => _allGroups.Sum(group => group.Items.Count(item => item.IsReturnParameter));

    public ICommand SaveCommand { get; }

    public ICommand CloseCommand { get; }

    /// <summary>
    /// 判断条件编辑结果已经回写方案工步参数时触发，供方案配置页面刷新当前参数副本。
    /// </summary>
    public event Action<SchemeProfile>? ParametersSaved;

    #endregion

    #region 打开、保存与关闭

    /// <summary>
    /// 打开编辑器并从方案工步实例生成连续分组参数行。
    /// </summary>
    public void Open(SchemeProfile scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        _editingScheme = scheme;
        _allGroups.Clear();
        ObservableCollection<WorkStepProfile> configuredWorkSteps = WorkStepConfigurationStore.Load();

        foreach (SchemeWorkStepItem workStep in scheme.Steps.OrderBy(item => item.Num))
        {
            WorkStepProfile? configuredWorkStep = configuredWorkSteps.FirstOrDefault(item => string.Equals(
                item.Name,
                workStep.StepType,
                StringComparison.OrdinalIgnoreCase));
            IReadOnlyList<WorkStepOperation> operations = configuredWorkStep is null
                ? Array.Empty<WorkStepOperation>()
                : configuredWorkStep.Operations;
            List<InputParameter> configuredInputs = operations
                .OrderBy(operation => operation.Num)
                .SelectMany(operation => operation.Parameters.OrderBy(parameter => parameter.Num))
                .Where(parameter => string.Equals(parameter.ParameterType?.Trim(), "工步值", StringComparison.Ordinal))
                .DistinctBy(parameter => parameter.Value?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();
            List<SchemeConditionEditorItem> rows = new();
            foreach (InputParameter configuredInput in configuredInputs)
            {
                string parameterName = configuredInput.Value?.Trim() ?? string.Empty;
                SchemeWorkStepParameterItem? savedParameter = workStep.InputParameters.FirstOrDefault(parameter =>
                    string.Equals(parameter.Name?.Trim(), parameterName, StringComparison.OrdinalIgnoreCase));
                if (savedParameter?.IsUsed == true)
                {
                    rows.Add(new SchemeConditionEditorItem(savedParameter.Clone(), true));
                }
            }

            List<string> configuredReturns = operations
                .OrderBy(operation => operation.Num)
                .SelectMany(operation => operation.ReturnValues
                    .OrderBy(returnValue => returnValue.Num)
                    .Select(returnValue =>
                        string.IsNullOrWhiteSpace(operation.ReturnValue)
                            ? returnValue.ReturnParameterName
                            : $"{operation.ReturnValue}_{returnValue.ReturnParameterName}"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (string fullName in configuredReturns)
            {
                SchemeWorkStepParameterItem? savedParameter = workStep.ReturnParameters.FirstOrDefault(parameter =>
                    string.Equals(parameter.Value?.Trim(), fullName.Trim(), StringComparison.OrdinalIgnoreCase));
                if (savedParameter?.IsUsed == true)
                {
                    rows.Add(new SchemeConditionEditorItem(savedParameter.Clone(), false));
                }
            }

            _allGroups.Add(new SchemeConditionWorkStepGroup(workStep, rows));
        }

        _searchText = string.Empty;
        _selectedParameterFilter = "全部参数";
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(SelectedParameterFilter));
        RefreshGroups();
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(WorkStepCount));
        OnPropertyChanged(nameof(InputParameterCount));
        OnPropertyChanged(nameof(ReturnParameterCount));
        IsOpen = true;
    }

    /// <summary>
    /// 将编辑行统一回写到对应方案工步参数集合，不执行方案持久化。
    /// </summary>
    private void Save()
    {
        if (_editingScheme is null) return;

        foreach (SchemeConditionWorkStepGroup group in _allGroups)
        {
            foreach (SchemeConditionEditorItem item in group.Items)
            {
                SchemeWorkStepParameterItem? target = item.IsInputParameter
                    ? group.Source.InputParameters.FirstOrDefault(parameter => string.Equals(
                        parameter.Name?.Trim(),
                        item.ParameterName.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                    : group.Source.ReturnParameters.FirstOrDefault(parameter => string.Equals(
                        parameter.Value?.Trim(),
                        item.ParameterName.Trim(),
                        StringComparison.OrdinalIgnoreCase));
                if (target is null)
                {
                    continue;
                }

                if (item.IsInputParameter)
                {
                    target.Value = item.EditableValue.Trim();
                }
                else
                {
                    target.Unit = item.Unit.Trim();
                    target.Name = item.EditableValue.Trim();
                    target.Operator = item.Operator;
                    target.JudgeValue = item.JudgeValue.Trim();
                }
            }

        }

        ParametersSaved?.Invoke(_editingScheme);
        Close();
    }

    public void Close()
    {
        IsOpen = false;
        _editingScheme = null;
        OnPropertyChanged(nameof(Title));
    }

    #endregion

    #region 筛选与显示状态

    /// <summary>
    /// 按搜索文字、参数类型和判断条件筛选重建显示分组。
    /// </summary>
    private void RefreshGroups()
    {
        Groups.Clear();
        string keyword = SearchText.Trim();
        foreach (SchemeConditionWorkStepGroup group in _allGroups)
        {
            List<SchemeConditionEditorItem> visibleRows = group.Items.Where(item =>
                    (SelectedParameterFilter == "全部参数" || item.ParameterType == SelectedParameterFilter) &&
                    (string.IsNullOrWhiteSpace(keyword) ||
                     group.WorkStepName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                     item.ParameterName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                     item.EditableValue.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (visibleRows.Count > 0) Groups.Add(new SchemeConditionWorkStepGroup(group.Source, visibleRows));
        }
    }

    #endregion
}
