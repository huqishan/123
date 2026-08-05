using ControlLibrary;
using Module.Business.Models;
using Module.Business.Features.OperationEditing.ViewModels;
using Module.Business.Features.OperationEditing.ViewModels.PresentationModels;
using Module.Business.Features.OperationEditing.Models;
using Module.Business.Features.OperationEditing.Services;
using Module.Business.Features.Scheme.ViewModels.PresentationModels;
using Module.Business.Services;
using Module.Business.Services.BusinessOperations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Module.Business.Features.Scheme.ViewModels;
public sealed class SchemeConfigurationViewModel : ViewModelProperties
{
    #region 状态颜色

    private static readonly Brush SuccessBrush =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A"));

    private static readonly Brush WarningBrush =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EA580C"));

    private static readonly Brush NeutralBrush =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));

    #endregion

    #region 私有字段

    private SchemeConfigurationCatalog _catalog = SchemeConfigurationStore.LoadCatalog();
    private SchemeWorkStepItem? _stepEditorHostWorkStep;
    private DateTime _lastCreateOrCopyCommandAt = DateTime.MinValue;
    private readonly List<WorkStepOperation> _copiedOperations = new();

    #endregion


    /// 独立的步骤操作编辑器视图模型，由方案配置页面组合使用。
    /// </summary>
    public OperationEditorViewModel OperationEditor { get; }

    #region 集合属性

    public ObservableCollection<SchemeProfile> Schemes => _catalog.Schemes;

    public ICollectionView SchemesView { get; private set; } = null!;


    /// 复用步骤编辑器能力。
    /// </summary>

    public ObservableCollection<WorkStepOperation>? StepCollection => SelectedWorkStep?.Operations;

    #region 当前工步

    private SchemeWorkStepItem? _selectedWorkStep;


    /// 当前工步。
    /// </summary>
    public SchemeWorkStepItem? SelectedWorkStep
    {
        get => _selectedWorkStep;
        set
        {
            if (ReferenceEquals(_selectedWorkStep, value))
            {
                return;
            }

            if (_selectedWorkStep is not null)
            {
                _selectedWorkStep.PropertyChanged -= SelectedWorkStep_PropertyChanged;
            }

            _selectedWorkStep = value;

            if (_selectedWorkStep is not null)
            {
                _selectedWorkStep.PropertyChanged += SelectedWorkStep_PropertyChanged;
            }

            SelectedOperation = _selectedWorkStep?.Operations.FirstOrDefault();
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedWorkStep));
            OnPropertyChanged(nameof(StepCollection));
            OnPropertyChanged(nameof(AreAllOperationsChecked));
            RaiseCommandStatesChanged();
        }
    }

    #endregion

    public WorkStepOperation? SelectedStep
    {
        get => SelectedOperation;
        set => SelectedOperation = value;
    }

    public bool AreAllStepsChecked
    {
        get => AreAllOperationsChecked;
        set => AreAllOperationsChecked = value;
    }

    public string CurrentSchemeStepName => SelectedSchemeStep?.StepName ?? string.Empty;

    #endregion

    #region 当前选择与搜索

    #region 搜索文本

    private string _searchText = string.Empty;


    /// 搜索文本。
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetField(ref _searchText, value ?? string.Empty))
            {
                return;
            }

            SchemesView.Refresh();
        }
    }

    #endregion

    #region 当前方案

    private SchemeProfile? _selectedScheme;

    /// <summary>
    /// 当前方案。
    /// </summary>
    public SchemeProfile? SelectedScheme
    {
        get => _selectedScheme;
        set
        {
            if (ReferenceEquals(_selectedScheme, value))
            {
                return;
            }

            if (_selectedScheme is not null)
            {
                _selectedScheme.PropertyChanged -= SelectedScheme_PropertyChanged;
            }

            _selectedScheme = value;

            if (_selectedScheme is not null)
            {
                _selectedScheme.PropertyChanged += SelectedScheme_PropertyChanged;
            }

            SelectedSchemeStep = _selectedScheme?.Steps.FirstOrDefault();
            OnPropertyChanged();
            RaisePageSummaryChanged();
            RaiseCommandStatesChanged();
        }
    }

    #endregion

    #region 当前方案工步

    private SchemeWorkStepItem? _selectedSchemeStep;

    /// <summary>
    /// 当前方案工步。
    /// </summary>
    public SchemeWorkStepItem? SelectedSchemeStep
    {
        get => _selectedSchemeStep;
        set
        {
            if (ReferenceEquals(_selectedSchemeStep, value))
            {
                return;
            }

            if (_selectedSchemeStep is not null)
            {
                _selectedSchemeStep.PropertyChanged -= SelectedSchemeStep_PropertyChanged;
            }

            _selectedSchemeStep = value;

            if (_selectedSchemeStep is not null)
            {
                _selectedSchemeStep.PropertyChanged += SelectedSchemeStep_PropertyChanged;
            }

            SynchronizeSelectedWorkStep();
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentSchemeStepName));
            OnPropertyChanged(nameof(AreAllSchemeStepsStartupEnabled));
            RaiseCommandStatesChanged();
        }
    }

    #endregion

    #endregion

    #region 页面展示属性

    #region 页面状态文本

    private string _pageStatusText = "等待编辑";

    /// <summary>
    /// 页面状态文本。
    /// </summary>
    public string PageStatusText
    {
        get => _pageStatusText;
        private set => SetField(ref _pageStatusText, value);
    }

    #endregion

    #region 页面状态颜色

    private Brush _pageStatusBrush = NeutralBrush;

    /// <summary>
    /// 页面状态颜色。
    /// </summary>
    public Brush PageStatusBrush
    {
        get => _pageStatusBrush;
        private set => SetField(ref _pageStatusBrush, value);
    }

    #endregion

    #region 方案工步启用列头的全选状态
    /// <summary>
    /// 方案工步启用列头的全选状态。
    /// </summary>
    public bool AreAllSchemeStepsStartupEnabled
    {
        get => SelectedScheme is not null &&
               SelectedScheme.Steps.Count > 0 &&
               SelectedScheme.Steps.All(step => step.IsStartupEnabled);
        set
        {
            if (SelectedScheme is null)
            {
                return;
            }

            foreach (SchemeWorkStepItem step in SelectedScheme.Steps
                         .Where(step => step.IsStartupEnabled != value)
                         .ToList())
            {
                step.IsStartupEnabled = value;
            }

            OnPropertyChanged();
            RaiseCommandStatesChanged();
        }
    }
    #endregion

    #endregion

    #region 命令属性
    /// <summary>
    /// 在当前方案工步中新增一个步骤。
    /// </summary>
    public ICommand AddStepCommand { get; private set; } = null!;

    /// <summary>
    /// 复制当前方案工步中选中的步骤。
    /// </summary>
    public ICommand CopyStepCommand { get; private set; } = null!;

    /// <summary>
    /// 将已复制的步骤粘贴到当前方案工步。
    /// </summary>
    public ICommand PasteStepCommand { get; private set; } = null!;

    /// <summary>
    /// 删除当前方案工步中选中的步骤。
    /// </summary>
    public ICommand DeleteStepCommand { get; private set; } = null!;

    /// <summary>
    /// 新增方案。
    /// </summary>
    public ICommand NewSchemeCommand { get; private set; } = null!;

    /// <summary>
    /// 复制当前选中的方案。
    /// </summary>
    public ICommand DuplicateSchemeCommand { get; private set; } = null!;

    /// <summary>
    /// 删除当前选中的方案。
    /// </summary>
    public ICommand DeleteSchemeCommand { get; private set; } = null!;

    /// <summary>
    /// 保存全部方案配置。
    /// </summary>
    public ICommand SaveSchemesCommand { get; private set; } = null!;

    /// <summary>
    /// 向当前方案新增一个工步。
    /// </summary>
    public ICommand AddWorkStepToSchemeCommand { get; private set; } = null!;

    /// <summary>
    /// 从当前方案移除选中的工步。
    /// </summary>
    public ICommand RemoveWorkStepFromSchemeCommand { get; private set; } = null!;

    #endregion

    #region 属性联动

    /// <summary>
    /// 方案自身属性变化时，刷新页面统计与筛选。
    /// </summary>
    private void SelectedScheme_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SchemeProfile.StepCount)
            or nameof(SchemeProfile.SchemeName)
            or nameof(SchemeProfile.LastModifiedAt)
            or nameof(SchemeProfile.LastModifiedText))
        {
            RaisePageSummaryChanged();
        }

        if (e.PropertyName is nameof(SchemeProfile.StepCount)
            or nameof(SchemeProfile.Steps))
        {
            SchemesView.Refresh();
        }

        if (e.PropertyName == nameof(SchemeProfile.Steps))
        {
            OnPropertyChanged(nameof(AreAllSchemeStepsStartupEnabled));
        }
    }

    /// <summary>
    /// 当前选中方案工步变化时，同步右侧步骤编辑器与统计信息。
    /// </summary>
    private void SelectedSchemeStep_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SchemeWorkStepItem.Operations))
        {
            SynchronizeSelectedWorkStep();
        }

        if (e.PropertyName is nameof(SchemeWorkStepItem.StepName))
        {
            if (_stepEditorHostWorkStep is not null)
            {
                _stepEditorHostWorkStep.StepName = SelectedSchemeStep?.StepName ?? string.Empty;
            }

            OnPropertyChanged(nameof(CurrentSchemeStepName));
        }

        if (e.PropertyName is nameof(SchemeWorkStepItem.IsStartupEnabled)
            or nameof(SchemeWorkStepItem.Operations))
        {
            OnPropertyChanged(nameof(AreAllSchemeStepsStartupEnabled));
        }
    }

    public static WorkStepOperation CreateDefaultOperation()
    {
        return new WorkStepOperation
        {
            OperationObjectName = "System",
            PCommandName = string.Empty,
            LuaScript = string.Empty,
            DelayMilliseconds = 0,
            IsEditParameter = false
        };
    }

    private void RaisePageSummaryChanged()
    {
        OnPropertyChanged(nameof(AreAllSchemeStepsStartupEnabled));
    }

    /// <summary>
    /// 让共享步骤编辑器重新绑定到当前方案工步。
    /// </summary>
    private void SynchronizeSelectedWorkStep()
    {
        //OperationEditor.Close();

        if (SelectedSchemeStep is null)
        {
            _stepEditorHostWorkStep = null;
            SelectedOperation = null;
            SelectedWorkStep = null;
            return;
        }

        _stepEditorHostWorkStep = new SchemeWorkStepItem
        {
            StepName = SelectedSchemeStep.StepName,
            Operations = SelectedSchemeStep.Operations
        };

        SelectedWorkStep = _stepEditorHostWorkStep;
        SelectedOperation = _stepEditorHostWorkStep.Operations.FirstOrDefault();
    }

    #endregion

    #region 构造与初始化

    public SchemeConfigurationViewModel()
    {
        OperationEditor = new OperationEditorViewModel();
        OperationEditor.OperationSaved += OperationEditor_OperationSaved;
        Schemes.CollectionChanged += Schemes_CollectionChanged;
        SchemesView = CollectionViewSource.GetDefaultView(Schemes);
        SchemesView.Filter = FilterSchemes;
        InitializeCommands();
        SelectedScheme = Schemes.FirstOrDefault();
        SetPageStatus(
            Schemes.Count == 0 ? "暂无方案配置，请点击新增。" : $"已加载 {Schemes.Count} 个方案。",
            NeutralBrush);
    }

    /// <summary>
    /// 初始化页面命令。
    /// </summary>
    private void InitializeCommands()
    {
        NewSchemeCommand = new RelayCommand(_ => NewScheme());
        DuplicateSchemeCommand = new RelayCommand(_ => DuplicateSelectedScheme(), _ => SelectedScheme is not null);
        DeleteSchemeCommand = new RelayCommand(_ => DeleteSelectedScheme(), _ => SelectedScheme is not null);
        SaveSchemesCommand = new RelayCommand(_ => SaveSchemes());
        AddWorkStepToSchemeCommand = new RelayCommand(_ => AddWorkStepToScheme(), _ => SelectedScheme is not null);
        RemoveWorkStepFromSchemeCommand = new RelayCommand(
            _ => RemoveSelectedSchemeStep(),
            _ => SelectedScheme is not null && SelectedSchemeStep is not null);
        AddStepCommand = new RelayCommand(_ => OpenOperationEditorForNew(), _ => SelectedWorkStep is not null);
        CopyStepCommand = new RelayCommand(
            _ => CopySelectedOperations(),
            _ => SelectedWorkStep is not null && GetOperationsForClipboard().Count > 0);
        PasteStepCommand = new RelayCommand(
            _ => PasteCopiedOperations(),
            _ => SelectedWorkStep is not null && _copiedOperations.Count > 0);
        DeleteStepCommand = new RelayCommand(
            _ => DeleteSelectedOperation(),
            _ => SelectedWorkStep is not null &&
                 (SelectedOperation is not null || SelectedWorkStep.Operations.Any(operation => operation.IsChecked)));
    }

    #endregion

    #region 方案配置命令

    /// <summary>
    /// 新增一个默认方案并立即选中。
    /// </summary>
    private void NewScheme()
    {
        if (!CanRunCreateOrCopyCommand())
        {
            return;
        }

        SchemeProfile scheme = new()
        {
            SchemeName = GenerateUniqueSchemeName("方案")
        };
        Schemes.Add(scheme);
        SelectCreatedScheme(scheme);
        SetPageStatus("已新增方案。", SuccessBrush);
    }

    /// <summary>
    /// 复制当前选中的方案及其工步。
    /// </summary>
    private void DuplicateSelectedScheme()
    {
        if (!CanRunCreateOrCopyCommand() || SelectedScheme is null)
        {
            return;
        }

        SchemeProfile scheme = CreateCopyScheme(SelectedScheme);
        Schemes.Add(scheme);
        SelectCreatedScheme(scheme);
        SetPageStatus($"已复制方案：{scheme.SchemeName}。", SuccessBrush);
    }

    /// <summary>
    /// 删除当前选中的方案。
    /// </summary>
    private void DeleteSelectedScheme()
    {
        if (SelectedScheme is null)
        {
            return;
        }

        int index = Schemes.IndexOf(SelectedScheme);

        Schemes.Remove(SelectedScheme);
        SelectedScheme = Schemes.Count == 0
            ? null
            : Schemes[Math.Clamp(index, 0, Schemes.Count - 1)];

        SetPageStatus("已删除方案，保存后生效。", WarningBrush);
    }

    /// <summary>
    /// 保存全部方案配置。
    /// </summary>
    private void SaveSchemes()
    {
        if (!ValidateSchemes(out string message))
        {
            SetPageStatus(message, WarningBrush);
            return;
        }

        List<(SchemeProfile Scheme, DateTime PreviousTime)> modifiedSchemes = Schemes
            .Where(scheme => scheme.IsModified)
            .Select(scheme => (scheme, scheme.LastModifiedAt))
            .ToList();
        DateTime savedAt = DateTime.Now;

        // 保存文件前写入时间，确保落盘数据与界面一致；保存失败时恢复原时间和未保存状态。
        foreach ((SchemeProfile scheme, _) in modifiedSchemes)
        {
            scheme.LastModifiedAt = savedAt;
        }

        try
        {
            SchemeConfigurationStore.SaveCatalog(_catalog);
            foreach ((SchemeProfile scheme, _) in modifiedSchemes)
            {
                scheme.AcceptChanges(savedAt);
            }
        }
        catch
        {
            foreach ((SchemeProfile scheme, DateTime previousTime) in modifiedSchemes)
            {
                scheme.LastModifiedAt = previousTime;
                scheme.MarkModified();
            }

            throw;
        }

        SetPageStatus($"已保存 {Schemes.Count} 个方案。", SuccessBrush);
    }

    #endregion

    #region 方案工步命令

    /// <summary>
    /// 在当前方案中新增工步。
    /// </summary>
    private void AddWorkStepToScheme()
    {
        if (SelectedScheme is null)
        {
            return;
        }

        SchemeWorkStepItem schemeStep = new()
        {
            StepName = GenerateUniqueSchemeStepName("工步"),
            IsStartupEnabled = true
        };
        int insertIndex = SelectedSchemeStep is null
            ? SelectedScheme.Steps.Count
            : Math.Clamp(SelectedScheme.Steps.IndexOf(SelectedSchemeStep) + 1, 0, SelectedScheme.Steps.Count);

        SelectedScheme.Steps.Insert(insertIndex, schemeStep);
        SelectedSchemeStep = schemeStep;
        SetPageStatus($"已新增方案工步：{schemeStep.StepName}。", SuccessBrush);
    }

    /// <summary>
    /// 删除当前选中的方案工步。
    /// </summary>
    private void RemoveSelectedSchemeStep()
    {
        if (SelectedScheme is null || SelectedSchemeStep is null)
        {
            return;
        }

        int index = SelectedScheme.Steps.IndexOf(SelectedSchemeStep);
        SelectedScheme.Steps.Remove(SelectedSchemeStep);
        SelectedSchemeStep = SelectedScheme.Steps.Count == 0
            ? null
            : SelectedScheme.Steps[Math.Clamp(index, 0, SelectedScheme.Steps.Count - 1)];

        SetPageStatus("已删除方案工步。", WarningBrush);
    }

    /// <summary>
    /// 调整方案工步顺序。
    /// </summary>
    public void MoveSchemeStep(SchemeWorkStepItem draggedSchemeStep, SchemeWorkStepItem targetSchemeStep, bool insertAfter)
    {
        if (SelectedScheme is null)
        {
            return;
        }

        ObservableCollection<SchemeWorkStepItem> steps = SelectedScheme.Steps;
        int oldIndex = steps.IndexOf(draggedSchemeStep);
        int targetIndex = steps.IndexOf(targetSchemeStep);
        if (oldIndex < 0 || targetIndex < 0 || oldIndex == targetIndex)
        {
            return;
        }

        int newIndex = targetIndex + (insertAfter ? 1 : 0);
        if (oldIndex < newIndex)
        {
            newIndex--;
        }

        newIndex = Math.Clamp(newIndex, 0, steps.Count - 1);
        if (oldIndex == newIndex)
        {
            return;
        }

        steps.Move(oldIndex, newIndex);
        SelectedSchemeStep = draggedSchemeStep;
        SetPageStatus("已调整工步顺序。", SuccessBrush);
        RaiseCommandStatesChanged();
    }

    /// <summary>
    /// 调整当前工步内的步骤顺序，并保持拖拽步骤为当前选中项。
    /// </summary>
    public void MoveOperationStep(WorkStepOperation draggedOperation, WorkStepOperation targetOperation, bool insertAfter)
    {
        if (SelectedWorkStep is null)
        {
            return;
        }

        ObservableCollection<WorkStepOperation> operations = SelectedWorkStep.Operations;
        int oldIndex = operations.IndexOf(draggedOperation);
        int targetIndex = operations.IndexOf(targetOperation);
        if (oldIndex < 0 || targetIndex < 0 || oldIndex == targetIndex)
        {
            return;
        }

        // 源项位于目标项之前时，移除源项会使目标索引前移一位，因此需要修正插入索引。
        int newIndex = targetIndex + (insertAfter ? 1 : 0);
        if (oldIndex < newIndex)
        {
            newIndex--;
        }

        newIndex = Math.Clamp(newIndex, 0, operations.Count - 1);
        if (oldIndex == newIndex)
        {
            return;
        }

        operations.Move(oldIndex, newIndex);
        SelectedOperation = draggedOperation;
        SetPageStatus("已调整步骤顺序。", SuccessBrush);
        RaiseCommandStatesChanged();
    }

    #endregion

    #region 校验与搜索

    /// <summary>
    /// 保存前校验方案数据。
    /// </summary>
    private bool ValidateSchemes(out string message)
    {
        if (Schemes.Count == 0)
        {
            message = "请至少保留一个方案。";
            return false;
        }

        HashSet<string> schemeNames = new(StringComparer.OrdinalIgnoreCase);

        foreach (SchemeProfile scheme in Schemes)
        {
            if (string.IsNullOrWhiteSpace(scheme.SchemeName))
            {
                message = "方案名称不能为空。";
                return false;
            }

            if (!schemeNames.Add(scheme.SchemeName.Trim()))
            {
                message = $"方案名称重复：{scheme.SchemeName}";
                return false;
            }

            foreach (SchemeWorkStepItem schemeStep in scheme.Steps)
            {
                if (string.IsNullOrWhiteSpace(schemeStep.StepName))
                {
                    message = $"方案“{scheme.SchemeName}”存在未命名工步。";
                    return false;
                }
            }
        }

        message = string.Empty;
        return true;
    }

    /// <summary>
    /// 按关键字过滤方案列表。
    /// </summary>
    private bool FilterSchemes(object item)
    {
        if (item is not SchemeProfile scheme)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        string keyword = SearchText.Trim();
        return Contains(scheme.SchemeName, keyword) ||
               scheme.Steps.Any(step =>
                   Contains(step.StepName, keyword) ||
                   step.Operations.Any(operation => Contains(operation.Summary, keyword)));
    }

    private static bool Contains(string? source, string keyword)
    {
        return source?.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    #endregion

    #region 文本比较辅助

    private static bool TextEquals(string? left, string? right)
    {
        return string.Equals(NormalizeText(left), NormalizeText(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeText(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    #endregion

    #region 工厂与命名

    /// <summary>
    /// 深拷贝指定方案及其方案工步。
    /// </summary>
    private SchemeProfile CreateCopyScheme(SchemeProfile source)
    {
        return new SchemeProfile
        {
            Id = Guid.NewGuid().ToString("N"),
            SchemeName = GenerateCopySchemeName(source.SchemeName),
            Steps = new ObservableCollection<SchemeWorkStepItem>(
                source.Steps.Select(step =>
                {
                    SchemeWorkStepItem clone = step.Clone();
                    clone.Id = Guid.NewGuid().ToString("N");
                    return clone;
                }))
        };
    }

    /// <summary>
    /// 选中刚创建的方案，并让列表视图定位到该方案。
    /// </summary>
    private void SelectCreatedScheme(SchemeProfile scheme)
    {
        SearchText = string.Empty;
        SchemesView.Refresh();
        SelectedScheme = scheme;
        SchemesView.MoveCurrentTo(scheme);
    }

    /// <summary>
    /// 限制新增和复制命令的触发频率，避免连续点击重复创建。
    /// </summary>
    private bool CanRunCreateOrCopyCommand()
    {
        DateTime now = DateTime.UtcNow;
        if (now - _lastCreateOrCopyCommandAt < TimeSpan.FromMilliseconds(300))
        {
            return false;
        }

        _lastCreateOrCopyCommandAt = now;
        return true;
    }

    /// <summary>
    /// 根据前缀生成当前方案列表内唯一的方案名称。
    /// </summary>
    private string GenerateUniqueSchemeName(string prefix)
    {
        HashSet<string> existingNames = new(Schemes.Select(scheme => scheme.SchemeName), StringComparer.OrdinalIgnoreCase);
        int index = existingNames.Count + 1;
        string candidate;

        do
        {
            candidate = $"{prefix} {index}";
            index++;
        }
        while (existingNames.Contains(candidate));

        return candidate;
    }

    /// <summary>
    /// 根据原方案名称生成当前列表内唯一的复制方案名称。
    /// </summary>
    private string GenerateCopySchemeName(string baseName)
    {
        HashSet<string> existingNames = new(Schemes.Select(scheme => scheme.SchemeName), StringComparer.OrdinalIgnoreCase);
        string normalizedName = string.IsNullOrWhiteSpace(baseName) ? "方案" : baseName.Trim();
        string copyName = $"{normalizedName} 副本";
        if (!existingNames.Contains(copyName))
        {
            return copyName;
        }

        for (int index = 2; ; index++)
        {
            string candidate = $"{copyName} {index}";
            if (!existingNames.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>
    /// 根据前缀生成目标方案内唯一的方案工步名称。
    /// </summary>
    private string GenerateUniqueSchemeStepName(string prefix, SchemeProfile? targetScheme = null)
    {
        SchemeProfile? scheme = targetScheme ?? SelectedScheme;
        HashSet<string> existingNames = new(
            scheme?.Steps.Select(step => step.StepName) ?? Enumerable.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        string baseName = string.IsNullOrWhiteSpace(prefix) ? "工步" : prefix.Trim();
        string candidate = baseName;
        int index = 1;

        while (existingNames.Contains(candidate))
        {
            index++;
            candidate = $"{baseName} {index}";
        }

        return candidate;
    }

    #endregion

    #region 页面状态与命令刷新

    private void Schemes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RaisePageSummaryChanged();
        SchemesView.Refresh();
        RaiseCommandStatesChanged();
    }

    private void SetPageStatus(string text, Brush brush)
    {
        PageStatusText = text;
        PageStatusBrush = brush;
    }

    private void RaiseCommandStatesChanged()
    {
        RaiseCommandState(DuplicateSchemeCommand);
        RaiseCommandState(DeleteSchemeCommand);
        RaiseCommandState(AddWorkStepToSchemeCommand);
        RaiseCommandState(RemoveWorkStepFromSchemeCommand);
        RaiseCommandState(AddStepCommand);
        RaiseCommandState(CopyStepCommand);
        RaiseCommandState(PasteStepCommand);
        RaiseCommandState(DeleteStepCommand);
    }

    private static void RaiseCommandState(ICommand? command)
    {
        if (command is RelayCommand relayCommand)
        {
            relayCommand.RaiseCanExecuteChanged();
        }
    }

    #endregion

    #region 当前步骤

    private WorkStepOperation? _selectedOperation;

    /// <summary>
    /// 当前步骤。
    /// </summary>
    public WorkStepOperation? SelectedOperation
    {
        get => _selectedOperation;
        set
        {
            if (ReferenceEquals(_selectedOperation, value))
            {
                return;
            }

            _selectedOperation = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedStep));
            RaiseCommandStatesChanged();

        }
    }

    #endregion

    #region 命令属性

    public bool AreAllOperationsChecked
    {
        get => SelectedWorkStep is not null &&
               SelectedWorkStep.Operations.Count > 0 &&
               SelectedWorkStep.Operations.All(operation => operation.IsChecked);
        set
        {
            if (SelectedWorkStep is null)
            {
                return;
            }

            foreach (WorkStepOperation operation in SelectedWorkStep.Operations
                         .Where(operation => operation.IsChecked != value)
                         .ToList())
            {
                operation.IsChecked = value;
            }

            OnPropertyChanged();
            RaiseCommandStatesChanged();
        }
    }

    #endregion

    #region 属性联动方法
    /// <summary>
    /// 监听当前工步属性变更，同步汇总信息、筛选结果和命令状态。
    /// </summary>
    private void SelectedWorkStep_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SchemeWorkStepItem.OperationCount)
            or nameof(SchemeWorkStepItem.StepName)
            or nameof(SchemeWorkStepItem.Operations))
        {
            OnPropertyChanged(nameof(AreAllOperationsChecked));
            RaiseCommandStatesChanged();
        }
    }

    #endregion

    #region 步骤命令方法

    /// <summary>
    /// 打开抽屉，新建当前工步的操作步骤。
    /// </summary>
    private void OpenOperationEditorForNew()
    {
        if (SelectedWorkStep is null)
        {
            return;
        }

        SelectedOperation = null;
        WorkStepOperation operation = new()
        {
            OperationObjectName = "System",
            PCommandName = string.Empty,
            LuaScript = string.Empty,
            DelayMilliseconds = 0,
            IsEditParameter = false
        };

        OperationEditor.Open(operation, isNewOperation: true, SelectedWorkStep.Operations);
        SetPageStatus("正在新建步骤。", NeutralBrush);
    }

    /// <summary>
    /// 打开抽屉，编辑当前工步下的已有步骤。
    /// </summary>
    public void OpenOperationEditorForEdit(WorkStepOperation operation)
    {
        if (SelectedWorkStep is null || !SelectedWorkStep.Operations.Contains(operation))
        {
            return;
        }

        SelectedOperation = operation;
        OperationEditor.Open(operation, isNewOperation: false, SelectedWorkStep.Operations);
        SetPageStatus("正在编辑步骤。", NeutralBrush);
    }

    /// <summary>
    /// 根据方法指令表当前行创建步骤操作对象。
    /// </summary>
    public WorkStepOperation? CreateOperationFromMethodItem(StationOperationMethodItem? item)
    {
        if (item is null)
        {
            return null;
        }

        return CreateOperationFromMethodDefinition(
            item.OperationType,
            item.OperationObject,
            item.ProtocolName,
            item.CommandName,
            item.InvokeMethod,
            item.Summary);
    }

    /// <summary>
    /// 按操作定义组装步骤操作，并填充默认返回值和参数。
    /// </summary>
    private WorkStepOperation? CreateOperationFromMethodDefinition(
        string operationType,
        string operationObject,
        string protocolName,
        string commandName,
        string invokeMethod,
        string summary)
    {
        if (string.IsNullOrWhiteSpace(operationObject) || string.IsNullOrWhiteSpace(invokeMethod))
        {
            return null;
        }

        WorkStepOperation operation = new()
        {
            OperationObjectName = operationObject,
            PCommandName = invokeMethod,
            LuaScript = string.Empty,
            Summary = summary,
            DelayMilliseconds = 0,
            IsEditParameter = false
        };
        operation.Parameters = OperationEditor.CreateDefaultOperationParameters(operation);

        return operation;
    }

    /// <summary>
    /// 接收操作编辑器生成的最终结果，并将新增或编辑结果提交到当前方案工步。
    /// </summary>
    private void OperationEditor_OperationSaved(object? sender, OperationEditorSavedEventArgs e)
    {
        if (SelectedWorkStep is null)
        {
            return;
        }

        if (e.IsNewOperation)
        {
            SelectedWorkStep.Operations.Add(e.Operation);
            SelectedOperation = null;
            return;
        }

        int operationIndex = SelectedOperation is null
            ? -1
            : SelectedWorkStep.Operations.IndexOf(SelectedOperation);
        if (operationIndex >= 0)
        {
            SelectedWorkStep.Operations[operationIndex] = e.Operation;
        }

        SelectedOperation = e.Operation;
    }

    /// <summary>
    /// 关闭步骤编辑抽屉，不提交当前编辑缓存。
    /// </summary>

    /// <summary>
    /// 删除当前选中的操作步骤。
    /// </summary>
    private void DeleteSelectedOperation()
    {
        if (SelectedWorkStep is null)
        {
            return;
        }

        ObservableCollection<WorkStepOperation> steps = SelectedWorkStep.Operations;
        List<WorkStepOperation> operationsToDelete = GetCheckedOperations(steps);
        if (operationsToDelete.Count == 0 && SelectedOperation is not null)
        {
            operationsToDelete.Add(SelectedOperation);
        }

        if (operationsToDelete.Count == 0)
        {
            return;
        }

        int targetIndex = operationsToDelete
            .Select(steps.IndexOf)
            .Where(index => index >= 0)
            .DefaultIfEmpty(-1)
            .Min();

        WorkStepOperation? operationToKeepSelected =
            SelectedOperation is not null && !operationsToDelete.Contains(SelectedOperation)
                ? SelectedOperation
                : null;

        foreach (WorkStepOperation operation in operationsToDelete
                     .Where(operation => steps.Contains(operation))
                     .OrderByDescending(operation => steps.IndexOf(operation))
                     .ToList())
        {
            steps.Remove(operation);
        }

        if (operationToKeepSelected is not null && steps.Contains(operationToKeepSelected))
        {
            SelectedOperation = operationToKeepSelected;
        }
        else
        {
            SelectedOperation = steps.Count == 0 || targetIndex < 0
                ? null
                : steps[Math.Clamp(targetIndex, 0, steps.Count - 1)];
        }

        SetPageStatus(operationsToDelete.Count == 1
            ? "已删除步骤。"
            : $"已删除 {operationsToDelete.Count} 个步骤。", WarningBrush);
    }

    /// <summary>
    /// 复制勾选或当前选中的步骤到内部剪贴板。
    /// </summary>
    private void CopySelectedOperations()
    {
        List<WorkStepOperation> operationsToCopy = GetOperationsForClipboard();
        if (operationsToCopy.Count == 0)
        {
            return;
        }

        _copiedOperations.Clear();
        _copiedOperations.AddRange(operationsToCopy.Select(CreateClipboardOperation));
        RaiseCommandStatesChanged();

        SetPageStatus(operationsToCopy.Count == 1
            ? "已复制 1 个步骤。"
            : $"已复制 {operationsToCopy.Count} 个步骤。", SuccessBrush);
    }

    /// <summary>
    /// 将内部剪贴板中的步骤插入到当前工步。
    /// </summary>
    private void PasteCopiedOperations()
    {
        if (SelectedWorkStep is null || _copiedOperations.Count == 0)
        {
            return;
        }

        ObservableCollection<WorkStepOperation> steps = SelectedWorkStep.Operations;
        int insertIndex = ResolvePasteInsertIndex(steps);
        ClearCheckedOperations(steps);

        List<WorkStepOperation> operationsToPaste = _copiedOperations
            .Select(CreateClipboardOperation)
            .ToList();

        foreach (WorkStepOperation operation in operationsToPaste)
        {
            steps.Insert(insertIndex, operation);
            insertIndex++;
        }

        SelectedOperation = operationsToPaste.FirstOrDefault();
        SetPageStatus(operationsToPaste.Count == 1
            ? "已粘贴 1 个步骤。"
            : $"已粘贴 {operationsToPaste.Count} 个步骤。", SuccessBrush);
    }

    /// <summary>
    /// 获取步骤集合中所有被勾选的项。
    /// </summary>
    private List<WorkStepOperation> GetCheckedOperations(ObservableCollection<WorkStepOperation> steps)
    {
        return steps
            .Where(operation => operation.IsChecked)
            .ToList();
    }

    /// <summary>
    /// 获取用于复制的步骤列表，优先使用勾选项。
    /// </summary>
    private List<WorkStepOperation> GetOperationsForClipboard()
    {
        if (SelectedWorkStep is null)
        {
            return new List<WorkStepOperation>();
        }

        List<WorkStepOperation> checkedOperations = GetCheckedOperations(SelectedWorkStep.Operations);
        if (checkedOperations.Count > 0)
        {
            return checkedOperations;
        }

        return SelectedOperation is null
            ? new List<WorkStepOperation>()
            : new List<WorkStepOperation> { SelectedOperation };
    }

    /// <summary>
    /// 计算粘贴步骤时的插入位置。
    /// </summary>
    private int ResolvePasteInsertIndex(ObservableCollection<WorkStepOperation> steps)
    {
        List<WorkStepOperation> checkedOperations = GetCheckedOperations(steps);
        if (checkedOperations.Count > 0)
        {
            int lastCheckedIndex = checkedOperations
                .Select(steps.IndexOf)
                .DefaultIfEmpty(-1)
                .Max();
            if (lastCheckedIndex >= 0)
            {
                return Math.Min(lastCheckedIndex + 1, steps.Count);
            }
        }

        if (SelectedOperation is not null)
        {
            int selectedIndex = steps.IndexOf(SelectedOperation);
            if (selectedIndex >= 0)
            {
                return Math.Min(selectedIndex + 1, steps.Count);
            }
        }

        return steps.Count;
    }

    /// <summary>
    /// 清除步骤集合中的勾选状态。
    /// </summary>
    private void ClearCheckedOperations(ObservableCollection<WorkStepOperation> steps)
    {
        foreach (WorkStepOperation operation in steps.Where(item => item.IsChecked).ToList())
        {
            operation.IsChecked = false;
        }
    }

    /// <summary>
    /// 克隆步骤及其参数，用于复制粘贴场景。
    /// </summary>
    private WorkStepOperation CreateClipboardOperation(WorkStepOperation source)
    {
        WorkStepOperation operation = source.Clone();
        operation.Id = Guid.NewGuid().ToString("N");
        operation.IsChecked = false;
        operation.Parameters = new ObservableCollection<InputParameter>(
            operation.Parameters.Select(parameter =>
            {
                parameter.Id = Guid.NewGuid().ToString("N");
                return parameter;
            }));

        return operation;
    }

    #endregion

}



