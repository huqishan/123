using ControlLibrary;
using Microsoft.Win32;
using Module.Business.Models;
using Module.Business.Services;
using Module.Business.Services.BusinessOperations;
using Shared.Infrastructure.Extensions;
using Shared.Infrastructure.PackMethod;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;

namespace Module.Business.Features.SchemeConfiguration;

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

    #region 静态常量

    public const string SystemOperationObjectName = "System";
    public const string LuaOperationObjectName = "Lua";
    public const string JudgeOperationObjectName = "判断";

    #endregion

    #region 私有字段

    private SchemeConfigurationCatalog _catalog = SchemeConfigurationStore.LoadCatalog();
    private SchemeWorkStepItem? _stepEditorHostWorkStep;
    private WorkStepOperation? _drawerOperation;
    private DateTime _lastCreateOrCopyCommandAt = DateTime.MinValue;
    private bool _isSynchronizingOperationSelection;
    private bool _isSortingInvokeParameters;
    private bool _isInitializingOperationDrawer;
    private bool _isSyncingSystemInvokeMethodSelection;
    private readonly HashSet<InputParameter> _trackedEditingInvokeParameters = new();
    private readonly List<WorkStepOperation> _copiedOperations = new();
    private readonly HashSet<string> _checkedOperationIds = new();

    #endregion

    #region 集合属性

    public ObservableCollection<SchemeProfile> Schemes => _catalog.Schemes;

    public ICollectionView SchemesView { get; private set; } = null!;

    public ObservableCollection<string> OperationObjectOptions { get; } = new();

    public string? SelectedOperationObjectOption =>
        OperationObjectOptions.FirstOrDefault(option =>
            string.Equals(option, EditingOperationObject, StringComparison.OrdinalIgnoreCase));

    public ObservableCollection<string> LuaScriptTemplateOptions { get; } = new();

    public ObservableCollection<StationOperationMethodItem> StationOperationMethodCollection =>
        OperationMethods;

    public ObservableCollection<InputParameter> EditingInvokeParameters { get; } = new();

    public ObservableCollection<string> ParameterTypeCollection => ParameterTypeOptions;

    public ObservableCollection<string> ParameterTypeOptions { get; } = new()
    {
        "设置值",
        "返回值",
        "系统值"
    };

    public ObservableCollection<string> ReturnValueOptions { get; } = new();

    public ObservableCollection<InlineParameterEditorViewModel.InlineReturnParameterRow> StepEditorReturnParameterRows { get; } = new();

    public ObservableCollection<string> InlineOperationObjectOptions { get; } = new();

    public ObservableCollection<string> InlineInvokeMethodOptions { get; } = new();

    public InlineParameterEditorViewModel InlineParameterEditor { get; } = new();

    public StationOperationMethodItem? SelectedStationOperationMethod
    {
        get => SelectedOperationMethod;
        set => SelectedOperationMethod = value;
    }

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

    public string StepEditorTitle => OperationDrawerTitle;

    public string StepEditorHostStepName => SelectedWorkStep?.StepName ?? string.Empty;

    public bool IsStepEditorOpen => IsOperationDrawerOpen;

    public bool IsInitializingStepEditor => _isInitializingOperationDrawer;

    public ObservableCollection<WorkStepOperation>? StepCollection => SelectedWorkStep?.Operations;

    #endregion

    #region 当前工步

    private SchemeWorkStepItem? _selectedWorkStep;

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
            OnPropertyChanged(nameof(StepEditorHostStepName));
            OnPropertyChanged(nameof(AreAllOperationsChecked));
            RefreshEditingOptions();
            RefreshParameterValueOptions();
            RaiseCommandStatesChanged();
        }
    }

    #endregion

    #region 编辑操作对象

    private string _editingOperationObject = string.Empty;

    public string EditingOperationObject
    {
        get => _editingOperationObject;
        set
        {
            if (SetField(ref _editingOperationObject, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(SelectedOperationObjectOption));
                OnPropertyChanged(nameof(IsSystemOperationSelected));
                OnPropertyChanged(nameof(IsJudgeOperationSelected));
                OnPropertyChanged(nameof(IsSystemOrJudgeOperationSelected));
                OnPropertyChanged(nameof(IsLuaOperationSelected));
                OnPropertyChanged(nameof(IsProtocolCommandSelectionVisible));
                OnPropertyChanged(nameof(IsModifyInvokeParametersVisible));
                OnPropertyChanged(nameof(IsInvokeParameterEditorVisible));
                RefreshProtocolOptions(updateStatus: false);
                RefreshCommandOptions(updateStatus: false);
                RefreshOperationMethodTable();
                RefreshReturnValueOptions();
                RaiseCommandStatesChanged();
            }
        }
    }

    #endregion

    #region 编辑协议名称

    private string _editingProtocolName = string.Empty;

    public string EditingProtocolName
    {
        get => _editingProtocolName;
        set
        {
            if (SetField(ref _editingProtocolName, value ?? string.Empty))
            {
                RefreshCommandOptions(updateStatus: false);
                RefreshReturnValueOptions();
            }
        }
    }

    #endregion

    #region 编辑指令名称

    private string _editingCommandName = string.Empty;

    public string EditingCommandName
    {
        get => _editingCommandName;
        set
        {
            if (SetField(ref _editingCommandName, value ?? string.Empty) &&
                !IsSystemOrJudgeOperationSelected &&
                !IsLuaOperationSelected)
            {
                EditingInvokeMethod = _editingCommandName;
                RefreshInvokeParametersFromSelectedCommand();
                RefreshReturnValueOptions();
            }
        }
    }

    #endregion

    #region 编辑调用方法

    private string _editingInvokeMethod = string.Empty;

    public string EditingInvokeMethod
    {
        get => _editingInvokeMethod;
        set
        {
            if (!SetField(ref _editingInvokeMethod, value ?? string.Empty))
            {
                return;
            }

            if (IsSystemOperationSelected &&
                !_isInitializingOperationDrawer &&
                !_isSyncingSystemInvokeMethodSelection)
            {
                SyncSystemInvokeMethodRemarkFromMethod();
                RefreshInvokeParametersFromSelectedSystemMethod(clearWhenNoMetadata: true);
            }
            else if (IsJudgeOperationSelected &&
                     !_isInitializingOperationDrawer &&
                     !_isSyncingSystemInvokeMethodSelection)
            {
                SyncJudgeInvokeMethodRemarkFromMethod();
                RefreshInvokeParametersFromSelectedJudgeMethod(clearWhenNoMetadata: true);
            }
        }
    }

    #endregion

    #region 编辑修改输入参数

    private bool _editingModifyInvokeParameters;

    public bool EditingModifyInvokeParameters
    {
        get => _editingModifyInvokeParameters;
        set
        {
            if (SetField(ref _editingModifyInvokeParameters, value))
            {
                OnPropertyChanged(nameof(IsInvokeParameterEditorVisible));
                RaiseCommandStatesChanged();
            }
        }
    }

    #endregion

    #region 编辑 Lua 脚本

    private string _editingLuaScript = string.Empty;

    public string EditingLuaScript
    {
        get => _editingLuaScript;
        set => SetField(ref _editingLuaScript, value ?? string.Empty);
    }

    #endregion

    #region 编辑返回值（单个，用于兼容旧视图）

    private string _editingReturnValue = string.Empty;

    public string EditingReturnValue
    {
        get => _editingReturnValue;
        set
        {
            if (SetField(ref _editingReturnValue, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(EditingShowDataToView));
                OnPropertyChanged(nameof(EditingViewDataName));
                OnPropertyChanged(nameof(EditingViewJudgeType));
                OnPropertyChanged(nameof(EditingViewJudgeCondition));
            }
        }
    }

    public bool EditingShowDataToView
    {
        get
        {
            ReturnValue? rv = GetEditingSelectedReturnValue();
            return rv?.IsShowView ?? false;
        }
        set
        {
            ReturnValue? rv = GetEditingSelectedReturnValue();
            if (rv is not null)
            {
                rv.IsShowView = value;
            }
        }
    }

    public string EditingViewDataName
    {
        get
        {
            ReturnValue? rv = GetEditingSelectedReturnValue();
            return rv?.ReturnParameterName ?? string.Empty;
        }
        set
        {
            ReturnValue? rv = GetEditingSelectedReturnValue();
            if (rv is not null)
            {
                rv.ReturnParameterName = value ?? string.Empty;
            }
        }
    }

    public string EditingViewJudgeType
    {
        get
        {
            ReturnValue? rv = GetEditingSelectedReturnValue();
            return rv?.JudgeType ?? string.Empty;
        }
        set
        {
            ReturnValue? rv = GetEditingSelectedReturnValue();
            if (rv is not null)
            {
                rv.JudgeType = value ?? string.Empty;
            }
        }
    }

    public string EditingViewJudgeCondition
    {
        get
        {
            ReturnValue? rv = GetEditingSelectedReturnValue();
            return rv?.JudgeSymbols ?? string.Empty;
        }
        set
        {
            ReturnValue? rv = GetEditingSelectedReturnValue();
            if (rv is not null)
            {
                rv.JudgeSymbols = value ?? string.Empty;
            }
        }
    }

    private ReturnValue? GetEditingSelectedReturnValue()
    {
        if (_drawerOperation is null)
        {
            return null;
        }

        return _drawerOperation.ReturnValues.FirstOrDefault();
    }

    #endregion

    public void RefreshLuaScriptTemplateOptions()
    {
        ReplaceStringOptions(LuaScriptTemplateOptions, LoadLuaScriptTemplateNames());
    }

    public void ApplyLuaScriptTemplate(string? templateName)
    {
        ApplySelectedLuaScriptTemplate(templateName);
    }

    #region 编辑延时毫秒文本

    private string _editingDelayMillisecondsText = "0";

    public string EditingDelayMillisecondsText
    {
        get => _editingDelayMillisecondsText;
        set => SetField(ref _editingDelayMillisecondsText, value ?? string.Empty);
    }

    #endregion

    #region 编辑备注

    private string _editingRemark = string.Empty;

    public string EditingRemark
    {
        get => _editingRemark;
        set => SetField(ref _editingRemark, value ?? string.Empty);
    }

    #endregion

    #region 当前编辑输入参数

    private InputParameter? _selectedEditingInvokeParameter;

    public InputParameter? SelectedEditingInvokeParameter
    {
        get => _selectedEditingInvokeParameter;
        set
        {
            if (SetField(ref _selectedEditingInvokeParameter, value))
            {
                RaiseCommandStatesChanged();
            }
        }
    }

    #endregion

    #region 操作对象类型判断属性

    public bool IsSystemOperationSelected => IsSystemOperationObject(EditingOperationObject);

    public bool IsJudgeOperationSelected => IsJudgeOperationObject(EditingOperationObject);

    public bool IsSystemOrJudgeOperationSelected => IsSystemOperationSelected || IsJudgeOperationSelected;

    public bool IsLuaOperationSelected => IsLuaOperationObject(EditingOperationObject);

    public bool IsProtocolCommandSelectionVisible => !IsSystemOrJudgeOperationSelected && !IsLuaOperationSelected;

    public bool IsModifyInvokeParametersVisible => !IsLuaOperationSelected;

    public bool IsInvokeParameterEditorVisible => !IsLuaOperationSelected && EditingModifyInvokeParameters;

    #endregion

    #region 当前选择与搜索

    #region 搜索文本

    private string _searchText = string.Empty;

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

            BindSchemeStepEditor();
            OnPropertyChanged();
            OnPropertyChanged(nameof(AreAllSchemeStepsStartupEnabled));
            RaiseCommandStatesChanged();
        }
    }

    #endregion

    #endregion

    #region 页面展示属性

    #region 页面状态文本

    private string _pageStatusText = "等待编辑";

    public string PageStatusText
    {
        get => _pageStatusText;
        private set => SetField(ref _pageStatusText, value);
    }

    #endregion

    #region 页面状态颜色

    private Brush _pageStatusBrush = NeutralBrush;

    public Brush PageStatusBrush
    {
        get => _pageStatusBrush;
        private set => SetField(ref _pageStatusBrush, value);
    }

    #endregion

    #region 方案工步启用列头的全选状态

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
        }
    }

    #endregion

    #endregion

    #region 命令属性

    public ICommand AddStepCommand { get; private set; } = null!;
    public ICommand CopyStepCommand { get; private set; } = null!;
    public ICommand PasteStepCommand { get; private set; } = null!;
    public ICommand DeleteStepCommand { get; private set; } = null!;
    public ICommand SaveStepEditorCommand { get; private set; } = null!;
    public ICommand CloseStepEditorCommand { get; private set; } = null!;
    public ICommand NewSchemeCommand { get; private set; } = null!;
    public ICommand DuplicateSchemeCommand { get; private set; } = null!;
    public ICommand DeleteSchemeCommand { get; private set; } = null!;
    public ICommand SaveSchemesCommand { get; private set; } = null!;
    public ICommand ImportSchemeCommand { get; private set; } = null!;
    public ICommand ExportSchemeCommand { get; private set; } = null!;
    public ICommand AddWorkStepToSchemeCommand { get; private set; } = null!;
    public ICommand RemoveWorkStepFromSchemeCommand { get; private set; } = null!;

    #endregion

    #region 属性联动

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

    private void SelectedSchemeStep_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SchemeWorkStepItem.Operations))
        {
            BindSchemeStepEditor();
        }

        if (e.PropertyName is nameof(SchemeWorkStepItem.StepName))
        {
            if (_stepEditorHostWorkStep is not null)
            {
                _stepEditorHostWorkStep.StepName = SelectedSchemeStep?.StepName ?? string.Empty;
            }
        }

        if (e.PropertyName is nameof(SchemeWorkStepItem.IsStartupEnabled)
            or nameof(SchemeWorkStepItem.Operations)
            or nameof(SchemeWorkStepItem.LastModifiedAt)
            or nameof(SchemeWorkStepItem.LastModifiedText))
        {
            OnPropertyChanged(nameof(AreAllSchemeStepsStartupEnabled));
        }
    }

    private void RefreshEditingOptions()
    {
        IEnumerable<WorkStepOperation> currentOperations =
            SelectedWorkStep?.Operations ?? Enumerable.Empty<WorkStepOperation>();

        ReplaceStringOptions(
            OperationObjectOptions,
            new[]
            {
                SystemOperationObjectName,
                LuaOperationObjectName
            }
            .Concat(LoadDeviceOperationObjectNames())
            .Concat(currentOperations.Select(operation => operation.OperationObjectName))
            .Where(option => !IsJudgeOperationObject(option)));

        string selectedOperationObject = SelectedOperation is null
            ? string.Empty
            : ResolveOperationObjectForEditing(SelectedOperation);
        List<string> invokeMethodOptions = LoadInvokeMethodOptionsForOperationObject(selectedOperationObject)
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Select(option => option.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        ReplaceStringOptions(InvokeMethodOptions, invokeMethodOptions);
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

    public WorkStepOperation? CreateOperationFromMethodItem(StationOperationMethodItem? item)
    {
        return CreateOperationFromMethodItemCore(item);
    }

    public void OpenOperationDrawerForEdit(WorkStepOperation operation)
    {
        if (SelectedWorkStep is null || !SelectedWorkStep.Operations.Contains(operation))
        {
            return;
        }

        SelectedOperation = operation;
        if (!IsOperationDrawerOpen ||
            !ReferenceEquals(_drawerOperation, operation))
        {
            BeginOperationDrawer(operation, isNewOperation: false);
        }
        SetPageStatus("正在编辑步骤。", NeutralBrush);
    }

    public void CloseStepEditor()
    {
        if (CloseStepEditorCommand.CanExecute(null))
        {
            CloseStepEditorCommand.Execute(null);
        }
    }

    public void SetExternalReturnValueOptions(IEnumerable<string> returnValues)
    {
        ReplaceStringOptions(ExternalReturnValueOptions, returnValues);
        RefreshParameterValueOptions();
        RefreshReturnValueOptions();
    }

    public bool TrySaveStepEditor()
    {
        return IsOperationDrawerOpen && SaveOperationDrawer();
    }

    public WorkStepOperation? CreateSelectedOperationSnapshot()
    {
        return SelectedWorkStep?.Operations.FirstOrDefault()?.Clone() ?? SelectedOperation?.Clone();
    }

    public static WorkStepOperation CreateDefaultOperation()
    {
        return new WorkStepOperation
        {
            OperationObjectName = SystemOperationObjectName,
            PCommandName = string.Empty,
            LuaScript = string.Empty,
            DelayMilliseconds = 0
        };
    }

    public void RefreshOperationParameterModifiedStates(IEnumerable<WorkStepOperation> operations)
    {
        foreach (WorkStepOperation operation in operations.Where(operation => operation is not null))
        {
            operation.IsEditParameter = HasModifiedOperationParameters(operation);
        }
    }

    private void RaisePageSummaryChanged()
    {
        OnPropertyChanged(nameof(AreAllSchemeStepsStartupEnabled));
    }

    private void BindSchemeStepEditor()
    {
        if (CloseStepEditorCommand.CanExecute(null))
        {
            CloseStepEditorCommand.Execute(null);
        }

        if (SelectedSchemeStep is null)
        {
            _stepEditorHostWorkStep = null;
            SelectedOperation = null;
            SelectedWorkStep = null;
            RefreshEditingOptions();
            return;
        }

        _stepEditorHostWorkStep = SelectedSchemeStep;

        SelectedWorkStep = _stepEditorHostWorkStep;
        SelectedOperation = _stepEditorHostWorkStep.Operations.FirstOrDefault();
        RefreshEditingOptions();
    }

    #endregion

    #region 序列化配置

    private static readonly JsonSerializerOptions SchemePackageJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    #endregion

    #region 构造与初始化

    public SchemeConfigurationViewModel()
    {
        Schemes.CollectionChanged += Schemes_CollectionChanged;
        SchemesView = CollectionViewSource.GetDefaultView(Schemes);
        SchemesView.Filter = FilterSchemes;
        InitializeStepEditorState();
        InitializeCommands();
        SelectedScheme = Schemes.FirstOrDefault();
        SetPageStatus(
            Schemes.Count == 0 ? "暂无方案配置，请点击新增。" : $"已加载 {Schemes.Count} 个方案。",
            NeutralBrush);
    }

    private void InitializeStepEditorState()
    {
        EditingInvokeParameters.CollectionChanged += EditingInvokeParameters_CollectionChanged;
        RefreshLuaScriptTemplateOptions();
        RefreshOperationMethodTable();
        RefreshReturnValueOptions();
    }

    private void InitializeCommands()
    {
        NewSchemeCommand = new RelayCommand(_ => NewScheme());
        DuplicateSchemeCommand = new RelayCommand(_ => DuplicateSelectedScheme(), _ => SelectedScheme is not null);
        DeleteSchemeCommand = new RelayCommand(_ => DeleteSelectedScheme(), _ => SelectedScheme is not null);
        SaveSchemesCommand = new RelayCommand(_ => SaveSchemes());
        ImportSchemeCommand = new RelayCommand(_ => ImportScheme());
        ExportSchemeCommand = new RelayCommand(_ => ExportSelectedScheme(), _ => SelectedScheme is not null);
        AddWorkStepToSchemeCommand = new RelayCommand(_ => AddWorkStepToScheme(), _ => SelectedScheme is not null);
        RemoveWorkStepFromSchemeCommand = new RelayCommand(
            _ => RemoveSelectedSchemeStep(),
            _ => SelectedScheme is not null && SelectedSchemeStep is not null);
        AddStepCommand = new RelayCommand(_ => OpenOperationDrawerForNew(), _ => SelectedWorkStep is not null);
        CopyStepCommand = new RelayCommand(_ => CopySelectedOperations(), _ => CanCopyOperations());
        PasteStepCommand = new RelayCommand(_ => PasteCopiedOperations(), _ => CanPasteOperations());
        DeleteStepCommand = new RelayCommand(_ => DeleteSelectedOperation(), _ => CanDeleteOperations());
        SaveStepEditorCommand = new RelayCommand(_ => SaveOperationDrawer());
        CloseStepEditorCommand = new RelayCommand(_ => CloseOperationDrawer());
    }

    #endregion

    #region 方案配置命令

    private void NewScheme()
    {
        if (!CanRunCreateOrCopyCommand())
        {
            return;
        }

        SchemeProfile scheme = CreateScheme(GenerateUniqueSchemeName("方案"));
        Schemes.Add(scheme);
        SelectCreatedScheme(scheme);
        SetPageStatus("已新增方案。", SuccessBrush);
    }

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

    private void SaveSchemes()
    {
        if (!ValidateSchemes(out string message))
        {
            SetPageStatus(message, WarningBrush);
            return;
        }

        SchemeConfigurationStore.SaveCatalog(_catalog);
        SetPageStatus($"已保存 {Schemes.Count} 个方案。", SuccessBrush);
    }

    private void ImportScheme()
    {
        OpenFileDialog dialog = new()
        {
            Filter = "方案文件 (*.scheme.json)|*.scheme.json|JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
            DefaultExt = ".scheme.json"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(dialog.FileName);
            SchemeConfigurationPackage? package =
                JsonSerializer.Deserialize<SchemeConfigurationPackage>(json, SchemePackageJsonOptions);

            if (package?.Scheme is null)
            {
                SetPageStatus("导入失败：方案文件为空或格式无效。", WarningBrush);
                return;
            }

            ImportSchemePackage(package);
        }
        catch (Exception ex)
        {
            SetPageStatus($"导入方案失败：{ex.Message}", WarningBrush);
        }
    }

    private void ExportSelectedScheme()
    {
        if (SelectedScheme is null)
        {
            SetPageStatus("请先选择要导出的方案。", WarningBrush);
            return;
        }

        SaveFileDialog dialog = new()
        {
            Filter = "方案文件 (*.scheme.json)|*.scheme.json|JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
            DefaultExt = ".scheme.json",
            FileName = $"{SanitizeFileName(SelectedScheme.SchemeName)}.scheme.json"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            SchemeConfigurationPackage package = CreateSchemePackage(SelectedScheme);
            string json = JsonSerializer.Serialize(package, SchemePackageJsonOptions);
            File.WriteAllText(dialog.FileName, json);
            SetPageStatus($"已导出方案：{dialog.FileName}", SuccessBrush);
        }
        catch (Exception ex)
        {
            SetPageStatus($"导出方案失败：{ex.Message}", WarningBrush);
        }
    }

    #endregion

    #region 方案工步命令

    private void AddWorkStepToScheme()
    {
        if (SelectedScheme is null)
        {
            return;
        }

        SchemeWorkStepItem schemeStep = CreateEmptySchemeStep(GenerateUniqueSchemeStepName("工步"));
        int insertIndex = SelectedSchemeStep is null
            ? SelectedScheme.Steps.Count
            : Math.Clamp(SelectedScheme.Steps.IndexOf(SelectedSchemeStep) + 1, 0, SelectedScheme.Steps.Count);

        SelectedScheme.Steps.Insert(insertIndex, schemeStep);
        SelectedSchemeStep = schemeStep;
        SetPageStatus($"已新增方案工步：{schemeStep.StepName}。", SuccessBrush);
    }

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

    #endregion

    #region 校验与搜索

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
                    message = $"方案\"{scheme.SchemeName}\"存在未命名工步。";
                    return false;
                }
            }
        }

        message = string.Empty;
        return true;
    }

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
                   step.Operations.Any(operation => Contains(operation.DisplayText, keyword)));
    }

    private static bool Contains(string? source, string keyword)
    {
        return source?.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    #endregion

    #region 导入导出辅助

    private SchemeConfigurationPackage CreateSchemePackage(SchemeProfile scheme)
    {
        return new SchemeConfigurationPackage
        {
            Scheme = scheme.Clone()
        };
    }

    private void ImportSchemePackage(SchemeConfigurationPackage package)
    {
        SchemeProfile scheme = package.Scheme!.Clone();
        scheme.Id = Guid.NewGuid().ToString("N");
        scheme.SchemeName = GenerateUniqueImportedSchemeName(scheme.SchemeName);

        foreach (SchemeWorkStepItem schemeStep in scheme.Steps)
        {
            schemeStep.Id = Guid.NewGuid().ToString("N");

            if (schemeStep.Operations.Count == 0)
            {
                SetPageStatus($"导入失败：工步\"{schemeStep.StepName}\"缺少步骤内容。", WarningBrush);
                return;
            }

            if (string.IsNullOrWhiteSpace(schemeStep.StepName))
            {
                schemeStep.StepName = GenerateUniqueSchemeStepName("工步", scheme);
            }
        }

        Schemes.Add(scheme);
        SelectCreatedScheme(scheme);
        SetPageStatus($"已导入方案：{scheme.SchemeName}。", SuccessBrush);
    }

    private static string SanitizeFileName(string fileName)
    {
        string safeName = string.IsNullOrWhiteSpace(fileName) ? "方案" : fileName.Trim();
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            safeName = safeName.Replace(invalidChar, '_');
        }

        return safeName;
    }

    #endregion

    #region 工厂与命名

    private SchemeProfile CreateScheme(string schemeName)
    {
        return new SchemeProfile
        {
            SchemeName = schemeName
        };
    }

    private SchemeWorkStepItem CreateEmptySchemeStep(string stepName)
    {
        return new SchemeWorkStepItem
        {
            StepName = stepName,
            IsStartupEnabled = true,
            LastModifiedAt = DateTime.Now
        };
    }

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

    private void SelectCreatedScheme(SchemeProfile scheme)
    {
        SearchText = string.Empty;
        SchemesView.Refresh();
        SelectedScheme = scheme;
        SchemesView.MoveCurrentTo(scheme);
    }

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

    private string GenerateUniqueImportedSchemeName(string schemeName)
    {
        HashSet<string> existingNames = new(Schemes.Select(scheme => scheme.SchemeName), StringComparer.OrdinalIgnoreCase);
        string baseName = string.IsNullOrWhiteSpace(schemeName) ? "方案" : schemeName.Trim();
        string candidate = baseName;
        int index = 2;

        while (existingNames.Contains(candidate))
        {
            candidate = $"{baseName} {index}";
            index++;
        }

        return candidate;
    }

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
        RaiseCommandState(ExportSchemeCommand);
        RaiseCommandState(AddWorkStepToSchemeCommand);
        RaiseCommandState(RemoveWorkStepFromSchemeCommand);
        RaiseCommandState(AddStepCommand);
        RaiseCommandState(CopyStepCommand);
        RaiseCommandState(PasteStepCommand);
        RaiseCommandState(DeleteStepCommand);
        RaiseCommandState(SaveStepEditorCommand);
        RaiseCommandState(CloseStepEditorCommand);
    }

    private static void RaiseCommandState(ICommand? command)
    {
        if (command is RelayCommand relayCommand)
        {
            relayCommand.RaiseCanExecuteChanged();
        }
    }

    #endregion

    #region 集合属性

    public ObservableCollection<string> ProtocolOptions { get; } = new();

    public ObservableCollection<string> CommandOptions { get; } = new();

    public ObservableCollection<string> InvokeMethodOptions { get; } = new();

    public ObservableCollection<string> InvokeMethodRemarkOptions { get; } = new();

    public ObservableCollection<StationOperationMethodItem> OperationMethods { get; } = new();

    #region 当前操作方法

    private StationOperationMethodItem? _selectedOperationMethod;

    public StationOperationMethodItem? SelectedOperationMethod
    {
        get => _selectedOperationMethod;
        set
        {
            if (SetField(ref _selectedOperationMethod, value))
            {
                OnPropertyChanged(nameof(SelectedStationOperationMethod));
            }
        }
    }

    #endregion

    private ObservableCollection<string> ExternalReturnValueOptions { get; } = new();

    private bool RestrictOperationObjectOptionsToDecision { get; set; }

    #endregion

    #region 搜索与当前编辑属性

    #region 当前步骤

    private WorkStepOperation? _selectedOperation;

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
            RefreshEditingOptions();
            OnPropertyChanged(nameof(SelectedStep));
            OnPropertyChanged(nameof(AreAllOperationsChecked));
            RaiseCommandStatesChanged();

            if (IsOperationDrawerOpen &&
                !_isInitializingOperationDrawer &&
                value is not null &&
                SelectedWorkStep?.Operations.Contains(value) == true &&
                !ReferenceEquals(_drawerOperation, value))
            {
                BeginOperationDrawer(value, isNewOperation: false);
                SetPageStatus("正在编辑步骤。", NeutralBrush);
            }
        }
    }

    #endregion

    #region 步骤编辑抽屉打开状态

    private bool _isOperationDrawerOpen;

    public bool IsOperationDrawerOpen
    {
        get => _isOperationDrawerOpen;
        private set
        {
            if (SetField(ref _isOperationDrawerOpen, value))
            {
                OnPropertyChanged(nameof(OperationDrawerTitle));
                OnPropertyChanged(nameof(IsStepEditorOpen));
                OnPropertyChanged(nameof(StepEditorTitle));
                RaiseCommandStatesChanged();
            }
        }
    }

    #endregion

    public string OperationDrawerTitle => _drawerOperation is not null && _checkedOperationIds.Contains(_drawerOperation.Id) ? "新建步骤" : "编辑步骤";

    #region 编辑调用方法备注

    private string _editingInvokeMethodRemark = string.Empty;

    public string EditingInvokeMethodRemark
    {
        get => _editingInvokeMethodRemark;
        set
        {
            if (!SetField(ref _editingInvokeMethodRemark, value ?? string.Empty))
            {
                return;
            }

            if (IsSystemOperationSelected &&
                !_isInitializingOperationDrawer &&
                !_isSyncingSystemInvokeMethodSelection)
            {
                SyncSystemInvokeMethodFromRemark();
                RefreshInvokeParametersFromSelectedSystemMethod(clearWhenNoMetadata: true);
            }
            else if (IsJudgeOperationSelected &&
                     !_isInitializingOperationDrawer &&
                     !_isSyncingSystemInvokeMethodSelection)
            {
                SyncJudgeInvokeMethodFromRemark();
                RefreshInvokeParametersFromSelectedJudgeMethod(clearWhenNoMetadata: true);
            }
        }
    }

    #endregion

    #endregion

    #region 命令属性

    public bool AreAllOperationsChecked
    {
        get => SelectedWorkStep is not null &&
               SelectedWorkStep.Operations.Count > 0 &&
               SelectedWorkStep.Operations.All(operation => _checkedOperationIds.Contains(operation.Id));
        set
        {
            if (SelectedWorkStep is null)
            {
                return;
            }

            if (value)
            {
                foreach (WorkStepOperation operation in SelectedWorkStep.Operations)
                {
                    _checkedOperationIds.Add(operation.Id);
                }
            }
            else
            {
                _checkedOperationIds.Clear();
            }

            OnPropertyChanged();
            RaiseCommandStatesChanged();
        }
    }

    #endregion

    #region 属性联动方法

    private void SelectedWorkStep_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SchemeWorkStepItem.StepName)
            or nameof(SchemeWorkStepItem.Operations)
            or nameof(SchemeWorkStepItem.LastModifiedAt)
            or nameof(SchemeWorkStepItem.LastModifiedText))
        {
            OnPropertyChanged(nameof(AreAllOperationsChecked));
            OnPropertyChanged(nameof(StepEditorHostStepName));
            RaiseCommandStatesChanged();
        }
    }

    #endregion

    #region 正则与路径

    private static readonly Regex ProtocolPlaceholderRegex =
        new Regex(@"\{\{\s*(?<name>[^{}\r\n]+?)\s*\}\}", RegexOptions.Compiled);

    private static readonly Regex SystemMethodSignatureRegex =
        new Regex(
            @"^\s*public\s+static\s+(?:async\s+)?(?<return>[A-Za-z_][\w\.<>,\[\]\?]*)\s+(?<name>[A-Za-z_]\w*(?:<[^>]+>)?)\s*\((?<parameters>.*)\)",
            RegexOptions.Compiled);

    private static readonly string LuaScriptConfigDirectory =
        Path.Combine(AppContext.BaseDirectory, "Config", "LuaScript");

    #endregion

    #region 步骤命令方法

    private void OpenOperationDrawerForNew()
    {
        if (SelectedWorkStep is null)
        {
            return;
        }

        SelectedOperation = null;
        BeginOperationDrawer(CreateDefaultOperation(), isNewOperation: true);
        SetPageStatus("正在新建步骤。", NeutralBrush);
    }

    private WorkStepOperation? CreateOperationFromMethodItemCore(StationOperationMethodItem? item)
    {
        if (item is null)
        {
            return null;
        }

        return CreateOperationFromMethodDefinition(
            item.OperationObject,
            item.ProtocolName,
            item.CommandName,
            item.InvokeMethod);
    }

    private WorkStepOperation? CreateOperationFromMethodDefinition(
        string operationObject,
        string protocolName,
        string commandName,
        string invokeMethod)
    {
        if (string.IsNullOrWhiteSpace(operationObject) || string.IsNullOrWhiteSpace(invokeMethod))
        {
            return null;
        }

        WorkStepOperation operation = new()
        {
            OperationObjectName = operationObject,
            PCommandName = invokeMethod,
            DelayMilliseconds = 0
        };

        operation.Parameters = CreateOperationParametersFromMethodTableRow(
            operationObject, protocolName, commandName, invokeMethod);

        return operation;
    }

    private bool SaveOperationDrawer()
    {
        if (SelectedWorkStep is null || _drawerOperation is null)
        {
            SetPageStatus("没有可保存的步骤。", WarningBrush);
            return false;
        }

        bool isLuaOperation = IsLuaOperationSelected;

        if (string.IsNullOrWhiteSpace(EditingOperationObject))
        {
            SetPageStatus("操作对象不能为空。", WarningBrush);
            return false;
        }

        string pCommandName = isLuaOperation
            ? LuaOperationObjectName
            : EditingInvokeMethod.Trim();

        if (string.IsNullOrWhiteSpace(pCommandName))
        {
            SetPageStatus("调用方法不能为空。", WarningBrush);
            return false;
        }

        if (!int.TryParse(EditingDelayMillisecondsText, out int delayMilliseconds) || delayMilliseconds < 0)
        {
            SetPageStatus("延时(ms)必须是大于等于 0 的整数。", WarningBrush);
            return false;
        }

        _drawerOperation.OperationObjectName = isLuaOperation
            ? LuaOperationObjectName
            : EditingOperationObject.Trim();
        _drawerOperation.PCommandName = pCommandName;
        _drawerOperation.LuaScript = isLuaOperation ? EditingLuaScript : string.Empty;
        _drawerOperation.DelayMilliseconds = delayMilliseconds;

        if (isLuaOperation)
        {
            _drawerOperation.Parameters = new ObservableCollection<InputParameter>();
            _drawerOperation.ReturnValues = new ObservableCollection<ReturnValue>();
        }
        else if (EditingModifyInvokeParameters)
        {
            NormalizeInvokeParameterSequences();
            SortInvokeParametersBySequence();
            _drawerOperation.Parameters = new ObservableCollection<InputParameter>(
                EditingInvokeParameters
                    .OrderBy(parameter => parameter.Num)
                    .Select(parameter => parameter.Clone()));
        }
        else
        {
            WorkStepOperation? defaultOperation = CreateOperationFromMethodItem(SelectedOperationMethod);
            _drawerOperation.Parameters = defaultOperation?.Parameters != null
                ? new ObservableCollection<InputParameter>(
                    defaultOperation.Parameters.Select(parameter => parameter.Clone()))
                : new ObservableCollection<InputParameter>();
        }

        _drawerOperation.IsEditParameter = EditingModifyInvokeParameters;

        if (!isLuaOperation)
        {
            InlineParameterEditorViewModel.ApplyReturnParameters(
                _drawerOperation,
                StepEditorReturnParameterRows);
        }

        bool savedNewOperation = _checkedOperationIds.Contains(_drawerOperation.Id);
        if (savedNewOperation)
        {
            WorkStepOperation savedOperation = CloneOperationWithNewIdentity(_drawerOperation);
            SelectedWorkStep.Operations.Add(savedOperation);
            _checkedOperationIds.Remove(_drawerOperation.Id);
            SelectedOperation = null;
            SetPageStatus("已新增步骤。", SuccessBrush);
            OnPropertyChanged(nameof(OperationDrawerTitle));
            OnPropertyChanged(nameof(StepEditorTitle));
            return true;
        }

        SelectedOperation = _drawerOperation;
        SetPageStatus(savedNewOperation ? "已新增步骤。" : "已更新步骤。", SuccessBrush);

        return true;
    }

    private void CloseOperationDrawer()
    {
        IsOperationDrawerOpen = false;
        _drawerOperation = null;
        _checkedOperationIds.Clear();
        EditingInvokeParameters.Clear();
        EditingInvokeMethodRemark = string.Empty;
        EditingModifyInvokeParameters = false;
        EditingLuaScript = string.Empty;
        EditingRemark = string.Empty;
        EditingReturnValue = string.Empty;
        SelectedEditingInvokeParameter = null;
        OnPropertyChanged(nameof(OperationDrawerTitle));
        OnPropertyChanged(nameof(StepEditorTitle));
    }

    private void DeleteSelectedOperation()
    {
        if (SelectedWorkStep is null)
        {
            return;
        }

        ObservableCollection<WorkStepOperation> operations = SelectedWorkStep.Operations;
        List<WorkStepOperation> operationsToDelete = GetCheckedOperations(operations);
        if (operationsToDelete.Count == 0 && SelectedOperation is not null)
        {
            operationsToDelete.Add(SelectedOperation);
        }

        if (operationsToDelete.Count == 0)
        {
            return;
        }

        int targetIndex = operationsToDelete
            .Select(operations.IndexOf)
            .Where(index => index >= 0)
            .DefaultIfEmpty(-1)
            .Min();

        WorkStepOperation? operationToKeepSelected =
            SelectedOperation is not null && !operationsToDelete.Contains(SelectedOperation)
                ? SelectedOperation
                : null;

        if (_drawerOperation is not null &&
            operationsToDelete.Any(operation => ReferenceEquals(operation, _drawerOperation)))
        {
            CloseOperationDrawer();
        }

        foreach (WorkStepOperation operation in operationsToDelete
                     .Where(operation => operations.Contains(operation))
                     .OrderByDescending(operation => operations.IndexOf(operation))
                     .ToList())
        {
            _checkedOperationIds.Remove(operation.Id);
            operations.Remove(operation);
        }

        if (operationToKeepSelected is not null && operations.Contains(operationToKeepSelected))
        {
            SelectedOperation = operationToKeepSelected;
        }
        else
        {
            SelectedOperation = operations.Count == 0 || targetIndex < 0
                ? null
                : operations[Math.Clamp(targetIndex, 0, operations.Count - 1)];
        }

        SetPageStatus(operationsToDelete.Count == 1
            ? "已删除步骤。"
            : $"已删除 {operationsToDelete.Count} 个步骤。", WarningBrush);
    }

    private bool CanCopyOperations()
    {
        return SelectedWorkStep is not null && GetOperationsForClipboard().Count > 0;
    }

    private bool CanPasteOperations()
    {
        return SelectedWorkStep is not null && _copiedOperations.Count > 0;
    }

    private bool CanDeleteOperations()
    {
        return SelectedWorkStep is not null &&
               (SelectedOperation is not null || SelectedWorkStep.Operations.Any(operation => _checkedOperationIds.Contains(operation.Id)));
    }

    private void CopySelectedOperations()
    {
        List<WorkStepOperation> operationsToCopy = GetOperationsForClipboard();
        if (operationsToCopy.Count == 0)
        {
            return;
        }

        _copiedOperations.Clear();
        _copiedOperations.AddRange(operationsToCopy.Select(CloneOperationWithNewIdentity));
        RaiseCommandStatesChanged();

        SetPageStatus(operationsToCopy.Count == 1
            ? "已复制 1 个步骤。"
            : $"已复制 {operationsToCopy.Count} 个步骤。", SuccessBrush);
    }

    private void PasteCopiedOperations()
    {
        if (SelectedWorkStep is null || _copiedOperations.Count == 0)
        {
            return;
        }

        ObservableCollection<WorkStepOperation> operations = SelectedWorkStep.Operations;
        int insertIndex = ResolvePasteInsertIndex(operations);
        ClearCheckedOperations(operations);

        List<WorkStepOperation> operationsToPaste = _copiedOperations
            .Select(CloneOperationWithNewIdentity)
            .ToList();

        foreach (WorkStepOperation operation in operationsToPaste)
        {
            operations.Insert(insertIndex, operation);
            insertIndex++;
        }

        SelectedOperation = operationsToPaste.FirstOrDefault();
        SetPageStatus(operationsToPaste.Count == 1
            ? "已粘贴 1 个步骤。"
            : $"已粘贴 {operationsToPaste.Count} 个步骤。", SuccessBrush);
    }

    private List<WorkStepOperation> GetCheckedOperations(ObservableCollection<WorkStepOperation> operations)
    {
        return operations
            .Where(operation => _checkedOperationIds.Contains(operation.Id))
            .ToList();
    }

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

    private int ResolvePasteInsertIndex(ObservableCollection<WorkStepOperation> operations)
    {
        List<WorkStepOperation> checkedOperations = GetCheckedOperations(operations);
        if (checkedOperations.Count > 0)
        {
            int lastCheckedIndex = checkedOperations
                .Select(operations.IndexOf)
                .DefaultIfEmpty(-1)
                .Max();
            if (lastCheckedIndex >= 0)
            {
                return Math.Min(lastCheckedIndex + 1, operations.Count);
            }
        }

        if (SelectedOperation is not null)
        {
            int selectedIndex = operations.IndexOf(SelectedOperation);
            if (selectedIndex >= 0)
            {
                return Math.Min(selectedIndex + 1, operations.Count);
            }
        }

        return operations.Count;
    }

    private void ClearCheckedOperations(ObservableCollection<WorkStepOperation> operations)
    {
        foreach (WorkStepOperation operation in operations.Where(item => _checkedOperationIds.Contains(item.Id)).ToList())
        {
            _checkedOperationIds.Remove(operation.Id);
        }
    }

    private WorkStepOperation CloneOperationWithNewIdentity(WorkStepOperation source)
    {
        WorkStepOperation operation = source.Clone();
        operation.Id = Guid.NewGuid().ToString("N");
        _checkedOperationIds.Remove(operation.Id);

        foreach (InputParameter parameter in operation.Parameters)
        {
            parameter.Id = Guid.NewGuid().ToString("N");
        }

        foreach (ReturnValue returnValue in operation.ReturnValues)
        {
            returnValue.Id = Guid.NewGuid().ToString("N");
        }

        return operation;
    }

    private void BeginOperationDrawer(WorkStepOperation operation, bool isNewOperation)
    {
        RefreshLuaScriptTemplateOptions();
        SelectedOperationMethod = null;
        _drawerOperation = operation;
        if (isNewOperation)
        {
            _checkedOperationIds.Add(operation.Id);
        }
        _isInitializingOperationDrawer = true;
        try
        {
            string operationObject = ResolveOperationObjectForEditing(operation);
            if (RestrictOperationObjectOptionsToDecision &&
                !IsJudgeOperationObject(operationObject) &&
                string.IsNullOrWhiteSpace(operation.OperationObjectName))
            {
                operationObject = JudgeOperationObjectName;
            }

            string pCommandName = operation.PCommandName;
            string invokeMethod = pCommandName;

            string resolvedProtocolName = string.Empty;
            string resolvedCommandName = string.Empty;

            if (!IsSystemOperationObject(operationObject) &&
                !IsJudgeOperationObject(operationObject) &&
                !IsLuaOperationObject(operationObject) &&
                TryFindDeviceCommand(operationObject, invokeMethod, out resolvedProtocolName, out resolvedCommandName))
            {
                if (string.IsNullOrWhiteSpace(invokeMethod))
                {
                    invokeMethod = resolvedCommandName;
                }
            }

            RefreshOperationObjectOptions(updateStatus: false);
            EditingOperationObject = operationObject;
            EditingProtocolName = resolvedProtocolName;
            EnsureProtocolOption(EditingProtocolName);
            EditingCommandName = resolvedCommandName;
            EnsureCommandOption(EditingCommandName);
            EditingInvokeMethod = invokeMethod;
            RefreshProtocolOptions(updateStatus: false);
            RefreshCommandOptions(updateStatus: false);
            EditingLuaScript = operation.LuaScript;
            EditingDelayMillisecondsText = operation.DelayMilliseconds.ToString();
            EditingRemark = string.Empty;
            EditingModifyInvokeParameters = operation.IsEditParameter;
            EditingInvokeParameters.Clear();
            foreach (InputParameter parameter in operation.Parameters.Select(parameter => parameter.Clone()))
            {
                EditingInvokeParameters.Add(parameter);
            }

            NormalizeInvokeParameterSequences();
            SortInvokeParametersBySequence();

            if (IsLuaOperationSelected)
            {
                EditingProtocolName = string.Empty;
                EditingCommandName = string.Empty;
                EditingInvokeMethod = LuaOperationObjectName;
                EditingInvokeMethodRemark = string.Empty;
                EditingInvokeParameters.Clear();
            }
            else if (IsJudgeOperationSelected)
            {
                EditingProtocolName = string.Empty;
                EditingCommandName = string.Empty;
                SyncJudgeInvokeMethodRemarkFromMethod();
                if (EditingInvokeParameters.Count == 0)
                {
                    RefreshInvokeParametersFromSelectedJudgeMethod(clearWhenNoMetadata: false);
                }
            }
            else if (IsSystemOperationSelected)
            {
                SyncSystemInvokeMethodRemarkFromMethod();
                if (EditingInvokeParameters.Count == 0)
                {
                    RefreshInvokeParametersFromSelectedSystemMethod(clearWhenNoMetadata: false);
                }
            }
            else if (EditingInvokeParameters.Count == 0)
            {
                RefreshInvokeParametersFromSelectedCommand();
            }

            if (!isNewOperation)
            {
                SelectedOperationMethod = FindOperationMethodForEditingState();
            }

            SelectedEditingInvokeParameter = EditingInvokeParameters.FirstOrDefault();
            OnPropertyChanged(nameof(OperationDrawerTitle));
            OnPropertyChanged(nameof(StepEditorTitle));
            IsOperationDrawerOpen = true;
        }
        finally
        {
            _isInitializingOperationDrawer = false;
        }
    }

    #endregion

    #region 工具方法

    private void NormalizeInvokeParameterSequences()
    {
        bool wasSorting = _isSortingInvokeParameters;
        _isSortingInvokeParameters = true;
        try
        {
            HashSet<int> usedNums = new();
            int nextNum = 1;
            foreach (InputParameter parameter in EditingInvokeParameters)
            {
                if (parameter.Num <= 0 || !usedNums.Add(parameter.Num))
                {
                    while (usedNums.Contains(nextNum))
                    {
                        nextNum++;
                    }

                    parameter.Num = nextNum;
                    usedNums.Add(parameter.Num);
                }

                nextNum = Math.Max(nextNum, parameter.Num + 1);
            }
        }
        finally
        {
            _isSortingInvokeParameters = wasSorting;
        }
    }

    private void SortInvokeParametersBySequence()
    {
        if (_isSortingInvokeParameters || EditingInvokeParameters.Count < 2)
        {
            return;
        }

        _isSortingInvokeParameters = true;
        try
        {
            List<InputParameter> orderedParameters = EditingInvokeParameters
                .Select((parameter, index) => new { Parameter = parameter, Index = index })
                .OrderBy(item => item.Parameter.Num)
                .ThenBy(item => item.Index)
                .Select(item => item.Parameter)
                .ToList();

            for (int targetIndex = 0; targetIndex < orderedParameters.Count; targetIndex++)
            {
                InputParameter parameter = orderedParameters[targetIndex];
                int currentIndex = EditingInvokeParameters.IndexOf(parameter);
                if (currentIndex >= 0 && currentIndex != targetIndex)
                {
                    EditingInvokeParameters.Move(currentIndex, targetIndex);
                }
            }
        }
        finally
        {
            _isSortingInvokeParameters = false;
        }
    }

    private void RefreshParameterValueOptions()
    {
        foreach (InputParameter parameter in EditingInvokeParameters)
        {
            UpdateParameterValueOptions(parameter);
        }
    }

    private void UpdateParameterValueOptions(InputParameter parameter)
    {
    }

    private IEnumerable<string> BuildParameterValueOptions(string parameterType)
    {
        string normalizedType = parameterType?.Trim() ?? string.Empty;
        return normalizedType switch
        {
            "返回值" => BuildParameterReturnValueOptions(),
            _ => Enumerable.Empty<string>()
        };
    }

    private IEnumerable<string> BuildParameterReturnValueOptions()
    {
        if (SelectedWorkStep is null)
        {
            return Enumerable.Empty<string>();
        }

        List<WorkStepOperation> operations = SelectedWorkStep.Operations
            .Where(operation => operation is not null)
            .ToList();

        if (operations.Count == 0)
        {
            return Enumerable.Empty<string>();
        }

        WorkStepOperation? editingOperation = _drawerOperation ?? SelectedOperation;
        int targetIndex = editingOperation is null
            ? -1
            : operations.FindIndex(operation =>
                ReferenceEquals(operation, editingOperation) ||
                string.Equals(operation.Id, editingOperation.Id, StringComparison.Ordinal));

        if (targetIndex < 0)
        {
            targetIndex = operations.Count;
        }

        return operations
            .Take(targetIndex)
            .SelectMany(operation => operation.ReturnValues)
            .Select(returnValue => returnValue.ReturnParameterName)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase);
    }

    private IEnumerable<string> BuildReturnValueOptions()
    {
        IEnumerable<string> savedReturnValues = SelectedWorkStep?.Operations
            .SelectMany(operation => operation.ReturnValues.Select(rv => rv.ReturnParameterName))
            .Where(value => !string.IsNullOrWhiteSpace(value)) ?? Enumerable.Empty<string>();

        return savedReturnValues
            .Concat(ExternalReturnValueOptions)
            .Concat(LoadProtocolCommandReturnValueKeys(EditingProtocolName, EditingCommandName))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase);
    }

    private void RefreshReturnValueOptions()
    {
        ReplaceStringOptions(ReturnValueOptions, BuildReturnValueOptions());
    }

    private void ApplyDefaultReturnValueKey()
    {
        if (IsSystemOrJudgeOperationSelected ||
            IsLuaOperationSelected)
        {
            return;
        }

        IReadOnlyList<string> keys = LoadProtocolCommandReturnValueKeys(EditingProtocolName, EditingCommandName);
        if (keys.Count == 1)
        {
            EditingCommandName = keys[0];
        }
    }

    public IEnumerable<string> LoadDeviceOperationObjectNames()
    {
        return LoadDeviceOperationObjectOptions();
    }

    public IEnumerable<string> LoadInvokeMethodOptionsForOperationObject(string? operationObject)
    {
        string normalizedOperationObject = operationObject?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedOperationObject))
        {
            return Enumerable.Empty<string>();
        }

        if (IsLuaOperationObject(normalizedOperationObject))
        {
            return new[] { LuaOperationObjectName };
        }

        if (IsSystemOperationObject(normalizedOperationObject))
        {
            return LoadSystemMethodSelectionItems()
                .Select(method => method.Name);
        }

        return LoadDeviceInvokeMethodOptions(normalizedOperationObject);
    }

    private static IEnumerable<string> LoadDeviceInvokeMethodOptions(string operationObject)
    {
        IEnumerable<string> businessOperations = BusinessOperationBindingResolver
            .GetOperationsForOperationObject(operationObject)
            .Select(operation => operation.OperationId);
        HashSet<string> allowedProtocols = new(LoadDeviceSupportedProtocolNames(operationObject), StringComparer.OrdinalIgnoreCase);
        if (allowedProtocols.Count == 0)
        {
            return businessOperations;
        }

        return businessOperations.Concat(LoadProtocolSelectionItems()
            .Where(protocol => allowedProtocols.Contains(protocol.Name))
            .SelectMany(protocol => protocol.Commands.Select(command => command.Name)));
    }

    public void SynchronizeOperationMetadata(
        WorkStepOperation operation,
        IReadOnlyList<string> invokeMethodOptions)
    {
        if (operation is null)
        {
            return;
        }

        string operationObject = ResolveOperationObjectForEditing(operation);

        if (IsLuaOperationObject(operationObject))
        {
            operation.OperationObjectName = LuaOperationObjectName;
            operation.PCommandName = LuaOperationObjectName;
            return;
        }

        string invokeMethod = operation.PCommandName?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(invokeMethod) &&
            !invokeMethodOptions.Any(option => TextEquals(option, invokeMethod)))
        {
            invokeMethod = string.Empty;
            operation.PCommandName = invokeMethod;
        }

        if (IsSystemOperationObject(operationObject))
        {
            operation.OperationObjectName = SystemOperationObjectName;
            operation.PCommandName = invokeMethod;
            return;
        }

        operation.OperationObjectName = operationObject;
        BusinessOperationDescriptor? businessOperation = BusinessOperationBindingResolver.FindOperationForOperationObject(
            operationObject,
            null,
            invokeMethod);
        if (businessOperation is not null)
        {
            operation.PCommandName = businessOperation.OperationId;
            return;
        }

        if (TryFindDeviceCommand(operationObject, invokeMethod, out _, out string commandName))
        {
            operation.PCommandName = commandName;
        }
        else
        {
            operation.PCommandName = invokeMethod;
        }
    }

    private static bool TryFindDeviceCommand(
        string operationObject,
        string invokeMethod,
        out string protocolName,
        out string commandName)
    {
        protocolName = string.Empty;
        commandName = string.Empty;

        if (string.IsNullOrWhiteSpace(operationObject) || string.IsNullOrWhiteSpace(invokeMethod))
        {
            return false;
        }

        HashSet<string> allowedProtocols = new(LoadDeviceSupportedProtocolNames(operationObject), StringComparer.OrdinalIgnoreCase);
        if (allowedProtocols.Count > 0 &&
            !TryFindProtocolCommand(allowedProtocols, invokeMethod, out protocolName, out commandName))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 在允许的协议集合中查找匹配的指令。
    /// </summary>
    private static bool TryFindProtocolCommand(
        HashSet<string> allowedProtocols,
        string invokeMethod,
        out string protocolName,
        out string commandName)
    {
        protocolName = string.Empty;
        commandName = string.Empty;

        foreach (ProtocolSelectionItem protocol in LoadProtocolSelectionItems())
        {
            if (!allowedProtocols.Contains(protocol.Name))
            {
                continue;
            }

            ProtocolCommandItem? matchedCommand = protocol.Commands
                .FirstOrDefault(command => TextEquals(command.Name, invokeMethod));
            if (matchedCommand is not null)
            {
                protocolName = protocol.Name;
                commandName = matchedCommand.Name;
                return true;
            }
        }

        return false;
    }

    #endregion

    #region 基础判断方法

    /// <summary>
    /// 判断操作对象是否为系统操作。
    /// </summary>
    public static bool IsSystemOperationObject(string? operationObject)
    {
        return string.Equals(operationObject?.Trim(), SystemOperationObjectName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断操作对象是否为判断操作。
    /// </summary>
    public static bool IsJudgeOperationObject(string? operationObject)
    {
        return string.Equals(operationObject?.Trim(), JudgeOperationObjectName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断操作对象是否为 Lua 操作。
    /// </summary>
    public static bool IsLuaOperationObject(string? operationObject)
    {
        return string.Equals(operationObject?.Trim(), LuaOperationObjectName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 忽略大小写和首尾空白比较两个字符串。
    /// </summary>
    private static bool TextEquals(string? left, string? right)
    {
        return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 配置加载方法

    /// <summary>
    /// 加载设备操作对象选项（通信配置中的设备名称列表）。
    /// </summary>
    public static IEnumerable<string> LoadDeviceOperationObjectOptions()
    {
        string communicationConfigDirectory =
            Path.Combine(AppContext.BaseDirectory, "Config", "Communication");
        if (!Directory.Exists(communicationConfigDirectory))
        {
            return Enumerable.Empty<string>();
        }

        return Directory
            .EnumerateFiles(communicationConfigDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(filePath =>
            {
                try
                {
                    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(filePath, Encoding.UTF8));
                    return document.RootElement.TryGetProperty("LocalName", out JsonElement nameElement)
                        ? nameElement.GetString()?.Trim() ?? string.Empty
                        : string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            })
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 加载指定操作对象（设备）支持的协议名称列表。
    /// </summary>
    public static IEnumerable<string> LoadDeviceSupportedProtocolNames(string operationObject)
    {
        if (string.IsNullOrWhiteSpace(operationObject))
        {
            return Enumerable.Empty<string>();
        }

        string communicationConfigDirectory =
            Path.Combine(AppContext.BaseDirectory, "Config", "Communication");
        if (!Directory.Exists(communicationConfigDirectory))
        {
            return Enumerable.Empty<string>();
        }

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

                if (!document.RootElement.TryGetProperty("SupportedProtocols", out JsonElement supportedProtocols) ||
                    supportedProtocols.ValueKind != JsonValueKind.Array)
                {
                    return Enumerable.Empty<string>();
                }

                return supportedProtocols
                    .EnumerateArray()
                    .Select(element =>
                        element.TryGetProperty("ProtocolName", out JsonElement protocolNameElement)
                            ? protocolNameElement.GetString()?.Trim() ?? string.Empty
                            : string.Empty)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
            }
        }

        return Enumerable.Empty<string>();
    }

    /// <summary>
    /// 加载协议选择项，包含协议名称及其指令列表。
    /// </summary>
    public static IEnumerable<ProtocolSelectionItem> LoadProtocolSelectionItems()
    {
        string protocolConfigDirectory =
            Path.Combine(AppContext.BaseDirectory, "Config", "Protocol");
        if (!Directory.Exists(protocolConfigDirectory))
        {
            return Enumerable.Empty<ProtocolSelectionItem>();
        }

        List<ProtocolSelectionItem> items = new();
        foreach (string filePath in Directory.EnumerateFiles(protocolConfigDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(ReadPossiblyEncryptedConfigText(filePath));
                JsonElement root = document.RootElement;
                string protocolName = root.TryGetProperty("Name", out JsonElement nameElement)
                    ? nameElement.GetString()?.Trim() ?? string.Empty
                    : string.Empty;

                if (string.IsNullOrWhiteSpace(protocolName))
                {
                    continue;
                }

                List<ProtocolCommandItem> commands = new();
                if (root.TryGetProperty("Commands", out JsonElement commandsElement) &&
                    commandsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement commandElement in commandsElement.EnumerateArray())
                    {
                        string commandName = commandElement.TryGetProperty("Name", out JsonElement cmdNameElement)
                            ? cmdNameElement.GetString()?.Trim() ?? string.Empty
                            : string.Empty;

                        if (!string.IsNullOrWhiteSpace(commandName))
                        {
                            commands.Add(new ProtocolCommandItem(commandName));
                        }
                    }
                }

                items.Add(new ProtocolSelectionItem(protocolName, commands));
            }
            catch
            {
            }
        }

        return items.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 加载系统方法选择项（通过反射获取 System 操作对象可用的方法）。
    /// </summary>
    public static IEnumerable<SystemMethodSelectionItem> LoadSystemMethodSelectionItems()
    {
        // 从业务操作目录中获取系统操作对象的方法列表
        IReadOnlyList<BusinessOperationDescriptor> systemOperations =
            BusinessOperationCatalog.GetOperations(SystemOperationObjectName);

        List<SystemMethodSelectionItem> items = new();

        // 添加业务目录中注册的系统方法
        foreach (BusinessOperationDescriptor operation in systemOperations)
        {
            items.Add(new SystemMethodSelectionItem(
                operation.OperationId,
                operation.DisplayName,
                operation.Description,
                operation.Parameters.Select(p => new SystemMethodParameterItem(
                    p.Name,
                    p.DisplayName,
                    p.TypeName,
                    p.DefaultValue,
                    p.IsOptional,
                    p.Sequence)).ToList()));
        }

        // 通过反射查找 System 类的静态公共方法
        try
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in assemblies)
            {
                try
                {
                    Type[] types = assembly.GetTypes();
                    foreach (Type type in types.Where(t =>
                                 t.Name == "System" || t.Name == "SystemHelper" || t.Name == "SystemMethods"))
                    {
                        MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
                        foreach (MethodInfo method in methods)
                        {
                            if (method.DeclaringType == typeof(object))
                            {
                                continue;
                            }

                            if (items.Any(item => TextEquals(item.Name, method.Name)))
                            {
                                continue;
                            }

                            string displayName = method.Name;
                            IEnumerable<SystemMethodParameterItem> parameters =
                                method.GetParameters()
                                    .Select(p => new SystemMethodParameterItem(
                                        p.Name ?? $"参数{string.Empty}",
                                        p.Name ?? string.Empty,
                                        p.ParameterType.Name,
                                        p.DefaultValue?.ToString() ?? string.Empty,
                                        !p.HasDefaultValue,
                                        p.Position + 1));

                            items.Add(new SystemMethodSelectionItem(
                                method.Name,
                                displayName,
                                string.Empty,
                                parameters.ToList()));
                        }
                    }
                }
                catch
                {
                }
            }
        }
        catch
        {
        }

        return items.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 加载 Lua 脚本模板名称列表（Config/LuaScript 目录下的 JSON 文件名）。
    /// </summary>
    public static IEnumerable<string> LoadLuaScriptTemplateNames()
    {
        string luaScriptConfigDirectory =
            Path.Combine(AppContext.BaseDirectory, "Config", "LuaScript");
        if (!Directory.Exists(luaScriptConfigDirectory))
        {
            return Enumerable.Empty<string>();
        }

        return Directory
            .EnumerateFiles(luaScriptConfigDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(filePath =>
            {
                try
                {
                    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(filePath, Encoding.UTF8));
                    return document.RootElement.TryGetProperty("Name", out JsonElement nameElement)
                        ? nameElement.GetString()?.Trim() ?? string.Empty
                        : string.Empty;
                }
                catch
                {
                    return Path.GetFileNameWithoutExtension(filePath);
                }
            })
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 读取可能经过加密保存的配置文本。
    /// </summary>
    private static string ReadPossiblyEncryptedConfigText(string filePath)
    {
        string storageText = File.ReadAllText(filePath, Encoding.UTF8);
        try
        {
            return storageText.DesDecrypt();
        }
        catch
        {
            return storageText;
        }
    }

    #endregion

    #region 参数刷新方法

    /// <summary>
    /// 根据当前选中的协议指令刷新调用参数。
    /// </summary>
    private void RefreshInvokeParametersFromSelectedCommand()
    {
        if (IsSystemOrJudgeOperationSelected || IsLuaOperationSelected)
        {
            return;
        }

        ProtocolCommandReturnMetadata metadata = ProtocolCommandMetadataStore.GetReturnMetadata(
            EditingProtocolName, EditingCommandName);

        if (metadata.IsSendOnly)
        {
            EditingInvokeParameters.Clear();
            return;
        }

        if (EditingInvokeParameters.Count == 0 && metadata.ReturnValueKeys.Count > 0)
        {
            return;
        }

        BusinessOperationDescriptor? operationDescriptor =
            BusinessOperationBindingResolver.FindOperationForOperationObject(
                EditingOperationObject,
                null,
                EditingInvokeMethod);

        if (operationDescriptor is not null && operationDescriptor.Parameters.Count > 0)
        {
            EditingInvokeParameters.Clear();
            foreach (BusinessParameterDescriptor parameter in operationDescriptor.Parameters
                         .OrderBy(p => p.Sequence))
            {
                EditingInvokeParameters.Add(new InputParameter
                {
                    ParameterName = parameter.Name,
                    ParameterType = "设置值",
                    Value = parameter.DefaultValue
                });
            }

            NormalizeInvokeParameterSequences();
            SortInvokeParametersBySequence();
        }
    }

    /// <summary>
    /// 根据当前选中的系统方法刷新调用参数。
    /// </summary>
    private void RefreshInvokeParametersFromSelectedSystemMethod(bool clearWhenNoMetadata)
    {
        SystemMethodSelectionItem? methodItem = FindSystemMethod(EditingInvokeMethod);
        if (methodItem is null)
        {
            if (clearWhenNoMetadata)
            {
                EditingInvokeParameters.Clear();
            }

            return;
        }

        EditingInvokeParameters.Clear();
        foreach (SystemMethodParameterItem parameter in methodItem.Parameters.OrderBy(p => p.Sequence))
        {
            EditingInvokeParameters.Add(new InputParameter
            {
                ParameterName = parameter.Name,
                ParameterType = "设置值",
                Value = parameter.DefaultValue
            });
        }

        NormalizeInvokeParameterSequences();
        SortInvokeParametersBySequence();
    }

    /// <summary>
    /// 根据当前选中的判断方法刷新调用参数。
    /// </summary>
    private void RefreshInvokeParametersFromSelectedJudgeMethod(bool clearWhenNoMetadata)
    {
        if (clearWhenNoMetadata && EditingInvokeParameters.Count == 0)
        {
            EditingInvokeParameters.Add(new InputParameter
            {
                ParameterName = "Input",
                ParameterType = "设置值",
                Value = string.Empty
            });
            EditingInvokeParameters.Add(new InputParameter
            {
                ParameterName = "CompareValue",
                ParameterType = "设置值",
                Value = string.Empty
            });

            NormalizeInvokeParameterSequences();
            SortInvokeParametersBySequence();
        }
    }

    /// <summary>
    /// 从编辑的调用方法备注中同步系统方法。
    /// </summary>
    private void SyncSystemInvokeMethodFromRemark()
    {
        if (_isSyncingSystemInvokeMethodSelection)
        {
            return;
        }

        SystemMethodSelectionItem? matchedMethod = FindSystemMethod(EditingInvokeMethodRemark);
        if (matchedMethod is not null)
        {
            _isSyncingSystemInvokeMethodSelection = true;
            try
            {
                EditingInvokeMethod = matchedMethod.Name;
            }
            finally
            {
                _isSyncingSystemInvokeMethodSelection = false;
            }
        }
    }

    /// <summary>
    /// 从编辑的调用方法备注中同步判断方法。
    /// </summary>
    private void SyncJudgeInvokeMethodFromRemark()
    {
    }

    /// <summary>
    /// 从选中的系统方法同步调用方法备注。
    /// </summary>
    private void SyncSystemInvokeMethodRemarkFromMethod()
    {
        SystemMethodSelectionItem? methodItem = FindSystemMethod(EditingInvokeMethod);
        if (methodItem is not null)
        {
            EditingInvokeMethodRemark = methodItem.DisplayName;
        }
    }

    /// <summary>
    /// 从选中的判断方法同步调用方法备注。
    /// </summary>
    private void SyncJudgeInvokeMethodRemarkFromMethod()
    {
        EditingInvokeMethodRemark = EditingInvokeMethod;
    }

    /// <summary>
    /// 在系统方法列表中查找指定方法。
    /// </summary>
    private SystemMethodSelectionItem? FindSystemMethod(string? methodName)
    {
        if (string.IsNullOrWhiteSpace(methodName))
        {
            return null;
        }

        return LoadSystemMethodSelectionItems()
            .FirstOrDefault(item => TextEquals(item.Name, methodName));
    }

    #endregion

    #region UI 刷新方法

    /// <summary>
    /// 刷新操作对象选项列表。
    /// </summary>
    private void RefreshOperationObjectOptions(bool updateStatus = true)
    {
        IEnumerable<string> options = new[]
        {
            SystemOperationObjectName,
            LuaOperationObjectName
        }
        .Concat(LoadDeviceOperationObjectNames())
        .Where(option => !IsJudgeOperationObject(option));

        if (updateStatus)
        {
            RefreshEditingOptions();
        }
    }

    /// <summary>
    /// 刷新操作方法表格。
    /// </summary>
    private void RefreshOperationMethodTable()
    {
        string operationObject = IsLuaOperationSelected
            ? LuaOperationObjectName
            : EditingOperationObject;

        List<StationOperationMethodItem> methodItems = new();

        // 从业务操作目录获取
        IEnumerable<BusinessOperationDescriptor> businessOperations =
            BusinessOperationBindingResolver.GetOperationsForOperationObject(operationObject);
        foreach (BusinessOperationDescriptor operation in businessOperations)
        {
            methodItems.Add(new StationOperationMethodItem
            {
                Kind = "业务",
                OperationType = "业务方法",
                OperationObject = operationObject,
                ProtocolName = string.Empty,
                CommandName = string.Empty,
                InvokeMethod = operation.OperationId,
                Summary = operation.DisplayName,
                ParameterCount = operation.Parameters.Count
            });
        }

        // 从协议指令获取
        foreach (ProtocolSelectionItem protocol in LoadProtocolSelectionItems())
        {
            foreach (ProtocolCommandItem command in protocol.Commands)
            {
                methodItems.Add(new StationOperationMethodItem
                {
                    Kind = "协议",
                    OperationType = "协议指令",
                    OperationObject = operationObject,
                    ProtocolName = protocol.Name,
                    CommandName = command.Name,
                    InvokeMethod = command.Name,
                    Summary = $"{protocol.Name}.{command.Name}",
                    ParameterCount = 0
                });
            }
        }

        // 从系统方法获取
        foreach (SystemMethodSelectionItem method in LoadSystemMethodSelectionItems())
        {
            methodItems.Add(new StationOperationMethodItem
            {
                Kind = "系统",
                OperationType = "系统方法",
                OperationObject = SystemOperationObjectName,
                ProtocolName = string.Empty,
                CommandName = string.Empty,
                InvokeMethod = method.Name,
                Summary = method.DisplayName,
                ParameterCount = method.Parameters.Count
            });
        }

        ReplaceStationOperationMethodOptions(OperationMethods, methodItems);
    }

    /// <summary>
    /// 替换操作方法集合。
    /// </summary>
    private static void ReplaceStationOperationMethodOptions(
        ObservableCollection<StationOperationMethodItem> target,
        IEnumerable<StationOperationMethodItem> source)
    {
        List<StationOperationMethodItem> items = source.ToList();
        target.Clear();
        foreach (StationOperationMethodItem item in items)
        {
            target.Add(item);
        }
    }

    /// <summary>
    /// 刷新协议选项列表。
    /// </summary>
    private void RefreshProtocolOptions(bool updateStatus = true)
    {
        if (IsSystemOrJudgeOperationSelected || IsLuaOperationSelected)
        {
            ReplaceStringOptions(ProtocolOptions, Enumerable.Empty<string>());
            return;
        }

        HashSet<string> allowedProtocols = new(
            LoadDeviceSupportedProtocolNames(EditingOperationObject),
            StringComparer.OrdinalIgnoreCase);

        IEnumerable<string> protocolNames = LoadProtocolSelectionItems()
            .Where(p => allowedProtocols.Count == 0 || allowedProtocols.Contains(p.Name))
            .Select(p => p.Name);

        ReplaceStringOptions(ProtocolOptions, protocolNames);
    }

    /// <summary>
    /// 刷新指令选项列表。
    /// </summary>
    private void RefreshCommandOptions(bool updateStatus = true)
    {
        if (IsSystemOrJudgeOperationSelected || IsLuaOperationSelected)
        {
            ReplaceStringOptions(CommandOptions, Enumerable.Empty<string>());
            return;
        }

        IEnumerable<string> commandNames = LoadProtocolSelectionItems()
            .Where(p => TextEquals(p.Name, EditingProtocolName) || string.IsNullOrWhiteSpace(EditingProtocolName))
            .SelectMany(p => p.Commands)
            .Select(c => c.Name);

        ReplaceStringOptions(CommandOptions, commandNames);
    }

    /// <summary>
    /// 确保协议选项存在于集合中。
    /// </summary>
    private void EnsureProtocolOption(string? protocolName)
    {
        if (string.IsNullOrWhiteSpace(protocolName))
        {
            return;
        }

        if (!ProtocolOptions.Contains(protocolName, StringComparer.OrdinalIgnoreCase))
        {
            ProtocolOptions.Add(protocolName);
        }
    }

    /// <summary>
    /// 确保指令选项存在于集合中。
    /// </summary>
    private void EnsureCommandOption(string? commandName)
    {
        if (string.IsNullOrWhiteSpace(commandName))
        {
            return;
        }

        if (!CommandOptions.Contains(commandName, StringComparer.OrdinalIgnoreCase))
        {
            CommandOptions.Add(commandName);
        }
    }

    #endregion

    #region 工具方法

    /// <summary>
    /// 解析操作的当前编辑对象。
    /// </summary>
    private static string ResolveOperationObjectForEditing(WorkStepOperation operation)
    {
        return operation.OperationObjectName ?? string.Empty;
    }

    /// <summary>
    /// 判断操作参数是否被修改过。
    /// </summary>
    public bool HasModifiedOperationParameters(WorkStepOperation operation)
    {
        if (operation is null)
        {
            return false;
        }

        if (operation.Parameters.Count > 0 || operation.ReturnValues.Count > 0)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 根据操作方法表中的行创建操作参数。
    /// </summary>
    private ObservableCollection<InputParameter> CreateOperationParametersFromMethodTableRow(
        string operationObject,
        string protocolName,
        string commandName,
        string invokeMethod)
    {
        ObservableCollection<InputParameter> parameters = new();

        BusinessOperationDescriptor? operationDescriptor =
            BusinessOperationBindingResolver.FindOperationForOperationObject(
                operationObject,
                null,
                invokeMethod);

        if (operationDescriptor is not null)
        {
            foreach (BusinessParameterDescriptor parameter in operationDescriptor.Parameters
                         .OrderBy(p => p.Sequence))
            {
                parameters.Add(new InputParameter
                {
                    ParameterName = parameter.Name,
                    ParameterType = "设置值",
                    Value = parameter.DefaultValue
                });
            }

            return parameters;
        }

        ProtocolCommandReturnMetadata metadata = ProtocolCommandMetadataStore.GetReturnMetadata(
            protocolName, commandName);

        if (!metadata.IsSendOnly && metadata.ReturnValueKeys.Count > 0)
        {
            foreach (string key in metadata.ReturnValueKeys)
            {
                parameters.Add(new InputParameter
                {
                    ParameterName = key,
                    ParameterType = "返回值",
                    Value = string.Empty
                });
            }
        }

        return parameters;
    }

    /// <summary>
    /// 根据当前编辑状态查找操作方法项。
    /// </summary>
    private StationOperationMethodItem? FindOperationMethodForEditingState()
    {
        return OperationMethods.FirstOrDefault(method =>
            TextEquals(method.InvokeMethod, EditingInvokeMethod) &&
            (string.IsNullOrWhiteSpace(EditingOperationObject) ||
             TextEquals(method.OperationObject, EditingOperationObject)));
    }

    /// <summary>
    /// 加载协议指令的返回值键列表。
    /// </summary>
    public IReadOnlyList<string> LoadProtocolCommandReturnValueKeys(string? protocolName, string? commandName)
    {
        ProtocolCommandReturnMetadata metadata = ProtocolCommandMetadataStore.GetReturnMetadata(
            protocolName, commandName);
        return metadata.ReturnValueKeys;
    }

    /// <summary>
    /// 应用选中的 Lua 脚本模板。
    /// </summary>
    public void ApplySelectedLuaScriptTemplate(string? templateName)
    {
        if (string.IsNullOrWhiteSpace(templateName))
        {
            return;
        }

        string luaScriptConfigDirectory =
            Path.Combine(AppContext.BaseDirectory, "Config", "LuaScript");
        if (!Directory.Exists(luaScriptConfigDirectory))
        {
            return;
        }

        string filePath = Path.Combine(luaScriptConfigDirectory, $"{templateName}.json");
        if (!File.Exists(filePath))
        {
            return;
        }

        try
        {
            string storageText = File.ReadAllText(filePath, Encoding.UTF8);
            string json = storageText;
            try
            {
                json = storageText.DesDecrypt();
            }
            catch
            {
            }

            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("Content", out JsonElement contentElement))
            {
                EditingLuaScript = contentElement.GetString() ?? string.Empty;
            }
        }
        catch
        {
        }
    }

    #endregion

    #region 输入参数集合变更处理

    private void EditingInvokeParameters_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (InputParameter parameter in e.NewItems.OfType<InputParameter>())
            {
                if (!_trackedEditingInvokeParameters.Contains(parameter))
                {
                    _trackedEditingInvokeParameters.Add(parameter);
                    parameter.PropertyChanged += EditingInvokeParameter_PropertyChanged;
                }
            }
        }

        if (e.OldItems is not null)
        {
            foreach (InputParameter parameter in e.OldItems.OfType<InputParameter>())
            {
                _trackedEditingInvokeParameters.Remove(parameter);
                parameter.PropertyChanged -= EditingInvokeParameter_PropertyChanged;
            }
        }

        if (!_isSortingInvokeParameters)
        {
            NormalizeInvokeParameterSequences();
        }

        RefreshParameterValueOptions();
    }

    private void EditingInvokeParameter_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(InputParameter.ParameterType)
            or nameof(InputParameter.Value))
        {
            RefreshParameterValueOptions();
        }
    }

    #endregion

    #region 辅助类

    /// <summary>
    /// 协议选择项，包含协议名称及其指令列表。
    /// </summary>
    public sealed class ProtocolSelectionItem
    {
        /// <summary>
        /// 协议名称。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 协议指令列表。
        /// </summary>
        public IReadOnlyList<ProtocolCommandItem> Commands { get; }

        /// <summary>
        /// 创建协议选择项。
        /// </summary>
        public ProtocolSelectionItem(string name, IEnumerable<ProtocolCommandItem> commands)
        {
            Name = name;
            Commands = commands.ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// 协议指令项。
    /// </summary>
    public sealed class ProtocolCommandItem
    {
        /// <summary>
        /// 指令名称。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 创建协议指令项。
        /// </summary>
        public ProtocolCommandItem(string name)
        {
            Name = name;
        }
    }

    /// <summary>
    /// 系统方法选择项，包含方法名称、显示名称、说明及参数列表。
    /// </summary>
    public sealed class SystemMethodSelectionItem
    {
        /// <summary>
        /// 方法名称（代码名）。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 显示名称。
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// 方法说明。
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// 参数列表。
        /// </summary>
        public IReadOnlyList<SystemMethodParameterItem> Parameters { get; }

        /// <summary>
        /// 创建系统方法选择项。
        /// </summary>
        public SystemMethodSelectionItem(
            string name,
            string displayName,
            string description,
            IEnumerable<SystemMethodParameterItem> parameters)
        {
            Name = name;
            DisplayName = displayName;
            Description = description;
            Parameters = parameters.ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// 系统方法参数项。
    /// </summary>
    public sealed class SystemMethodParameterItem
    {
        /// <summary>
        /// 参数名称。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 参数显示名称。
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// 参数类型名称。
        /// </summary>
        public string TypeName { get; }

        /// <summary>
        /// 默认值。
        /// </summary>
        public string DefaultValue { get; }

        /// <summary>
        /// 是否为必填参数。
        /// </summary>
        public bool IsRequired { get; }

        /// <summary>
        /// 参数顺序。
        /// </summary>
        public int Sequence { get; }

        /// <summary>
        /// 创建系统方法参数项。
        /// </summary>
        public SystemMethodParameterItem(
            string name,
            string displayName,
            string typeName,
            string defaultValue,
            bool isRequired,
            int sequence)
        {
            Name = name;
            DisplayName = displayName;
            TypeName = typeName;
            DefaultValue = defaultValue;
            IsRequired = isRequired;
            Sequence = sequence;
        }
    }

    #endregion
}