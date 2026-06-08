using ControlLibrary;
using Module.Business.Models;
using Module.Business.Services;
using Module.Business.Services.BusinessOperations;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows.Media;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Shared.Infrastructure.Extensions;

namespace Module.Business.Features.SchemeConfiguration;

internal sealed class SchemeStepEditorState : ViewModelProperties
{
    #region 状态颜色

    private static readonly Brush SuccessBrush =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A"));

    private static readonly Brush WarningBrush =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EA580C"));

    private static readonly Brush NeutralBrush =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));

    #endregion

    #region 私有状态

    private SchemeConfigurationCatalog _catalog = BusinessConfigurationStore.LoadCatalog();
    private WorkStepProfile? _selectedWorkStep;
    private WorkStepOperation? _selectedOperation;
    private StationOperationMethodItem? _selectedOperationMethod;
    private WorkStepOperation? _drawerOperation;
    private WorkStepOperationParameter? _selectedEditingInvokeParameter;
    private string _searchText = string.Empty;
    private string _pageStatusText = "等待编辑";
    private string _editingOperationObject = string.Empty;
    private string _editingProtocolName = string.Empty;
    private string _editingCommandName = string.Empty;
    private string _editingInvokeMethod = string.Empty;
    private string _editingInvokeMethodRemark = string.Empty;
    private string _editingReturnValue = string.Empty;
    private bool _editingShowDataToView;
    private string _editingViewDataName = string.Empty;
    private string _editingViewJudgeType = string.Empty;
    private string _editingViewJudgeCondition = string.Empty;
    private string _editingLuaScript = string.Empty;
    private string _editingDelayMillisecondsText = "0";
    private string _editingRemark = string.Empty;
    private Brush _pageStatusBrush = NeutralBrush;
    private DateTime _lastCreateOrCopyCommandAt = DateTime.MinValue;
    private bool _isOperationDrawerOpen;
    private bool _isNewOperationInDrawer;
    private bool _isSortingInvokeParameters;
    private bool _isInitializingOperationDrawer;
    private bool _isSyncingSystemInvokeMethodSelection;
    private bool _isDecisionOperationMode;
    private bool _isStandaloneOperationEditMode;
    private bool _editingModifyInvokeParameters;
    private readonly HashSet<WorkStepOperationParameter> _trackedEditingInvokeParameters = new();
    private readonly List<WorkStepOperation> _copiedOperations = new();

    #endregion

    #region 集合属性

    public ObservableCollection<WorkStepProfile> WorkSteps => _catalog.WorkSteps;

    public ICollectionView WorkStepsView { get; private set; } = null!;

    public ObservableCollection<string> OperationObjectOptions { get; } = new();

    public ObservableCollection<string> ProtocolOptions { get; } = new();

    public ObservableCollection<string> CommandOptions { get; } = new();

    public ObservableCollection<string> ReturnValueOptions { get; } = new();

    public ObservableCollection<string> InvokeMethodOptions { get; } = new();

    public ObservableCollection<string> InvokeMethodRemarkOptions { get; } = new();

    public ObservableCollection<string> ParameterTypeOptions { get; } = new()
    {
        "设置值",
        "返回值",
        "系统值"
    };

    public ObservableCollection<WorkStepOperationParameter> EditingInvokeParameters { get; } = new();

    public ObservableCollection<StationOperationMethodItem> OperationMethods { get; } = new();

    public StationOperationMethodItem? SelectedOperationMethod
    {
        get => _selectedOperationMethod;
        set => SetField(ref _selectedOperationMethod, value);
    }

    private ObservableCollection<string> ExternalReturnValueOptions { get; } = new();

    #endregion

    #region 搜索与当前编辑属性
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetField(ref _searchText, value ?? string.Empty))
            {
                return;
            }

            WorkStepsView.Refresh();
            SelectFirstVisibleWorkStep();
            RefreshParameterValueOptions();
        }
    }

    public WorkStepProfile? SelectedWorkStep
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

            SelectedOperation = _selectedWorkStep?.Steps.FirstOrDefault();
            OnPropertyChanged();
            OnPropertyChanged(nameof(OperationCountText));
            OnPropertyChanged(nameof(AreAllOperationsChecked));
            RefreshParameterValueOptions();
            RefreshReturnValueOptions();
            RaiseCommandStatesChanged();
        }
    }

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
            RaiseCommandStatesChanged();
        }
    }

    public bool IsOperationDrawerOpen
    {
        get => _isOperationDrawerOpen;
        private set
        {
            if (SetField(ref _isOperationDrawerOpen, value))
            {
                OnPropertyChanged(nameof(OperationDrawerTitle));
                RaiseCommandStatesChanged();
            }
        }
    }

    public string OperationDrawerTitle => _isNewOperationInDrawer ? "新建步骤" : "编辑步骤";

    public string EditingOperationObject
    {
        get => _editingOperationObject;
        set
        {
            if (SetField(ref _editingOperationObject, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(IsSystemOperationSelected));
                OnPropertyChanged(nameof(IsJudgeOperationSelected));
                OnPropertyChanged(nameof(IsSystemOrJudgeOperationSelected));
                OnPropertyChanged(nameof(IsLuaOperationSelected));
                OnPropertyChanged(nameof(IsProtocolCommandSelectionVisible));
                OnPropertyChanged(nameof(IsModifyInvokeParametersVisible));
                OnPropertyChanged(nameof(IsInvokeParameterEditorVisible));
                OnPropertyChanged(nameof(IsReturnValueVisible));
                RefreshProtocolOptions(updateStatus: false);
                RefreshInvokeMethodOptions(updateStatus: false);
                RefreshOperationMethodTable();
                RefreshReturnValueOptions();
                RaiseCommandStatesChanged();
            }
        }
    }

    public bool IsSystemOperationSelected => IsSystemOperationObject(EditingOperationObject);

    public bool IsJudgeOperationSelected => IsJudgeOperationObject(EditingOperationObject);

    public bool IsSystemOrJudgeOperationSelected => IsSystemOperationSelected || IsJudgeOperationSelected;

    public bool IsLuaOperationSelected => IsLuaOperationObject(EditingOperationObject);

    public bool IsProtocolCommandSelectionVisible => !IsSystemOrJudgeOperationSelected && !IsLuaOperationSelected;

    public bool IsModifyInvokeParametersVisible => !IsLuaOperationSelected;

    public bool IsInvokeParameterEditorVisible => !IsLuaOperationSelected && EditingModifyInvokeParameters;

    public bool IsReturnValueVisible => !IsLuaOperationSelected;

    public string EditingProtocolName
    {
        get => _editingProtocolName;
        set
        {
            if (SetField(ref _editingProtocolName, value ?? string.Empty))
            {
                RefreshCommandOptions(updateStatus: false);
                RefreshOperationMethodTable();
                RefreshReturnValueOptions();
            }
        }
    }

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

    public string EditingReturnValue
    {
        get => _editingReturnValue;
        set
        {
            if (SetField(ref _editingReturnValue, value ?? string.Empty))
            {
                RefreshReturnValueOptions();
                RefreshParameterValueOptions();
            }
        }
    }

    public bool EditingShowDataToView
    {
        get => _editingShowDataToView;
        set => SetField(ref _editingShowDataToView, value);
    }

    public string EditingViewDataName
    {
        get => _editingViewDataName;
        set => SetField(ref _editingViewDataName, value ?? string.Empty);
    }

    public string EditingViewJudgeType
    {
        get => _editingViewJudgeType;
        set => SetField(ref _editingViewJudgeType, value ?? string.Empty);
    }

    public string EditingViewJudgeCondition
    {
        get => _editingViewJudgeCondition;
        set => SetField(ref _editingViewJudgeCondition, value ?? string.Empty);
    }

    public string EditingLuaScript
    {
        get => _editingLuaScript;
        set => SetField(ref _editingLuaScript, value ?? string.Empty);
    }

    public string EditingDelayMillisecondsText
    {
        get => _editingDelayMillisecondsText;
        set => SetField(ref _editingDelayMillisecondsText, value ?? string.Empty);
    }

    public string EditingRemark
    {
        get => _editingRemark;
        set => SetField(ref _editingRemark, value ?? string.Empty);
    }

    public WorkStepOperationParameter? SelectedEditingInvokeParameter
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

    #region 页面状态属性

    public string PageStatusText
    {
        get => _pageStatusText;
        private set => SetField(ref _pageStatusText, value);
    }

    public Brush PageStatusBrush
    {
        get => _pageStatusBrush;
        private set => SetField(ref _pageStatusBrush, value);
    }

    public string WorkStepCountText => $"{WorkSteps.Count} 个工步";
    public string OperationCountText => SelectedWorkStep is null
        ? "未选择工步"
        : $"{SelectedWorkStep.Steps.Count} 个步骤";

    #endregion

    #region 命令属性

    public bool AreAllOperationsChecked
    {
        get => SelectedWorkStep is not null &&
               SelectedWorkStep.Steps.Count > 0 &&
               SelectedWorkStep.Steps.All(operation => operation.IsChecked);
        set
        {
            if (SelectedWorkStep is null)
            {
                return;
            }

            foreach (WorkStepOperation operation in SelectedWorkStep.Steps
                         .Where(operation => operation.IsChecked != value)
                         .ToList())
            {
                operation.IsChecked = value;
            }

            OnPropertyChanged();
            RaiseCommandStatesChanged();
        }
    }

    public ICommand NewWorkStepCommand { get; private set; } = null!;

    public ICommand DuplicateWorkStepCommand { get; private set; } = null!;

    public ICommand DeleteWorkStepCommand { get; private set; } = null!;

    public ICommand SaveWorkStepsCommand { get; private set; } = null!;
    public ICommand AddOperationCommand { get; private set; } = null!;

    public ICommand CopyOperationCommand { get; private set; } = null!;

    public ICommand PasteOperationCommand { get; private set; } = null!;

    public ICommand DeleteOperationCommand { get; private set; } = null!;

    public ICommand SaveOperationDrawerCommand { get; private set; } = null!;

    public ICommand CloseOperationDrawerCommand { get; private set; } = null!;

    public ICommand RefreshOperationObjectsCommand { get; private set; } = null!;

    public ICommand AddInvokeParameterCommand { get; private set; } = null!;

    public ICommand DeleteInvokeParameterCommand { get; private set; } = null!;

    #endregion

    #region 属性联动方法
    /// <summary>
    /// 监听当前工步属性变更，同步汇总信息、筛选结果和命令状态。
    /// </summary>
    private void SelectedWorkStep_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorkStepProfile.StepName)
            or nameof(WorkStepProfile.LastModifiedAt)
            or nameof(WorkStepProfile.LastModifiedText)
            or nameof(WorkStepProfile.Steps))
        {
            OnPropertyChanged(nameof(OperationCountText));
            OnPropertyChanged(nameof(WorkStepCountText));
            OnPropertyChanged(nameof(AreAllOperationsChecked));
            WorkStepsView.Refresh();
            RaiseCommandStatesChanged();
        }
    }

    /// <summary>
    /// 刷新页面顶部的工步数和步骤数汇总展示。
    /// </summary>
    private void RaisePageSummaryChanged()
    {
        OnPropertyChanged(nameof(WorkStepCountText));
        OnPropertyChanged(nameof(OperationCountText));
    }

    #endregion


private static readonly Regex ProtocolPlaceholderRegex =
        new Regex(@"\{\{\s*(?<name>[^{}\r\n]+?)\s*\}\}", RegexOptions.Compiled);

    private static readonly Regex SystemMethodSignatureRegex =
        new Regex(
            @"^\s*public\s+static\s+(?:async\s+)?(?<return>[A-Za-z_][\w\.<>,\[\]\?]*)\s+(?<name>[A-Za-z_]\w*(?:<[^>]+>)?)\s*\((?<parameters>.*)\)",
            RegexOptions.Compiled);

    #region 构造与初始化

    /// <summary>
    /// 初始化工步编辑状态、集合视图和页面命令。
    /// </summary>
    public SchemeStepEditorState()
    {
        WorkSteps.CollectionChanged += WorkSteps_CollectionChanged;
        EditingInvokeParameters.CollectionChanged += EditingInvokeParameters_CollectionChanged;
        WorkStepsView = CollectionViewSource.GetDefaultView(WorkSteps);
        WorkStepsView.Filter = FilterWorkSteps;
        InitializeCommands();
        SelectFirstVisibleWorkStep();
        RefreshOperationMethodTable();
        RefreshReturnValueOptions();
        SetPageStatus("等待编辑步骤。", NeutralBrush);
    }

    /// <summary>
    /// 初始化页面命令，XAML 不绑定后可用 Click 事件。
    /// </summary>
    private void InitializeCommands()
    {
        NewWorkStepCommand = new RelayCommand(_ => NewWorkStep());
        DuplicateWorkStepCommand = new RelayCommand(_ => DuplicateSelectedWorkStep(), _ => SelectedWorkStep is not null);
        DeleteWorkStepCommand = new RelayCommand(_ => DeleteSelectedWorkStep(), _ => SelectedWorkStep is not null);
        SaveWorkStepsCommand = new RelayCommand(_ => SaveWorkSteps());
        AddOperationCommand = new RelayCommand(_ => OpenOperationDrawerForNew(), _ => SelectedWorkStep is not null);
        CopyOperationCommand = new RelayCommand(_ => CopySelectedOperations(), _ => CanCopyOperations());
        PasteOperationCommand = new RelayCommand(_ => PasteCopiedOperations(), _ => CanPasteOperations());
        DeleteOperationCommand = new RelayCommand(_ => DeleteSelectedOperation(), _ => CanDeleteOperations());
        SaveOperationDrawerCommand = new RelayCommand(_ => SaveOperationDrawer(), _ => IsOperationDrawerOpen);
        CloseOperationDrawerCommand = new RelayCommand(_ => CloseOperationDrawer());
        RefreshOperationObjectsCommand = new RelayCommand(_ => RefreshOperationObjectOptions(updateStatus: true), _ => IsOperationDrawerOpen);
        AddInvokeParameterCommand = new RelayCommand(_ => AddInvokeParameter(), _ => IsOperationDrawerOpen && IsInvokeParameterEditorVisible);
        DeleteInvokeParameterCommand = new RelayCommand(_ => DeleteSelectedInvokeParameter(), _ => IsOperationDrawerOpen && IsInvokeParameterEditorVisible && SelectedEditingInvokeParameter is not null);
    }

    #endregion
    #region 工步命令方法

    /// <summary>
    /// 新建工步，并在创建后立即打开首条步骤的编辑抽屉。
    /// </summary>
    private void NewWorkStep()
    {
        if (!CanRunCreateOrCopyCommand())
        {
            return;
        }

        WorkStepProfile workStep = CreateWorkStep(GenerateUniqueStepName("工步"));
        WorkSteps.Add(workStep);
        SelectCreatedWorkStep(workStep);
        if (workStep.Steps.FirstOrDefault() is WorkStepOperation operation)
        {
            OpenOperationDrawerForEdit(operation);
        }

        SetPageStatus("已新增工步，编辑后点击保存。", SuccessBrush);
    }

    /// <summary>
    /// 复制当前选中工步及其步骤列表。
    /// </summary>
    private void DuplicateSelectedWorkStep()
    {
        if (!CanRunCreateOrCopyCommand())
        {
            return;
        }

        if (SelectedWorkStep is null)
        {
            return;
        }

        WorkStepProfile workStep = CreateCopyWorkStep(SelectedWorkStep);
        WorkSteps.Add(workStep);
        SelectCreatedWorkStep(workStep);
        SetPageStatus($"已复制工步：{workStep.StepName}。", SuccessBrush);
    }

    /// <summary>
    /// 删除当前选中工步。
    /// </summary>
    private void DeleteSelectedWorkStep()
    {
        if (SelectedWorkStep is null)
        {
            return;
        }

        WorkSteps.Remove(SelectedWorkStep);
        SelectFirstVisibleWorkStep();

        SetPageStatus("已删除工步，点击保存后同步到方案配置。", WarningBrush);
    }

    /// <summary>
    /// 校验并保存所有工步模板。
    /// </summary>
    private void SaveWorkSteps()
    {
        if (!ValidateWorkSteps(out string message))
        {
            SetPageStatus(message, WarningBrush);
            return;
        }

        BusinessConfigurationStore.SaveCatalog(_catalog);
        SetPageStatus($"已保存 {WorkSteps.Count} 个工步。", SuccessBrush);
    }

    #endregion

    #region 步骤命令方法

    /// <summary>
    /// 打开抽屉，新建当前工步的操作步骤。
    /// </summary>
    private void OpenOperationDrawerForNew()
    {
        if (SelectedWorkStep is null)
        {
            return;
        }

        WorkStepOperation operation = new()
        {
            OperationObject = SystemOperationObjectName,
            DeviceId = SystemOperationObjectName,
            InvokeMethod = string.Empty,
            OperationId = string.Empty,
            ReturnValue = string.Empty,
            ShowDataToView = false,
            ViewDataName = string.Empty,
            ViewJudgeType = string.Empty,
            ViewJudgeCondition = string.Empty,
            DelayMilliseconds = 0,
            Remark = string.Empty
        };

        BeginOperationDrawer(operation, isNewOperation: true);
        SetPageStatus("正在新建步骤。", NeutralBrush);
    }

    /// <summary>
    /// 打开抽屉，编辑当前工步下的已有步骤。
    /// </summary>
    public void OpenOperationDrawerForEdit(WorkStepOperation operation)
    {
        if (SelectedWorkStep is null || !SelectedWorkStep.Steps.Contains(operation))
        {
            return;
        }

        SelectedOperation = operation;
        BeginOperationDrawer(operation, isNewOperation: false);
        SetPageStatus("正在编辑步骤。", NeutralBrush);
    }

    /// <summary>
    /// 将拖拽的步骤移动到目标步骤前后。
    /// </summary>
    public void MoveOperation(WorkStepOperation draggedOperation, WorkStepOperation targetOperation, bool insertAfter)
    {
        if (SelectedWorkStep is null)
        {
            return;
        }

        ObservableCollection<WorkStepOperation> steps = SelectedWorkStep.Steps;
        int oldIndex = steps.IndexOf(draggedOperation);
        int targetIndex = steps.IndexOf(targetOperation);
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
        SelectedOperation = draggedOperation;
        SetPageStatus("已调整步骤顺序。", SuccessBrush);
    }

    /// <summary>
    /// 将方法指令表拖拽出来的新步骤插入到当前步骤列表。
    /// </summary>
    public void InsertOperation(WorkStepOperation operation, WorkStepOperation? targetOperation, bool insertAfter)
    {
        if (SelectedWorkStep is null)
        {
            return;
        }

        WorkStepOperation operationToInsert = operation.Clone();
        operationToInsert.Id = Guid.NewGuid().ToString("N");
        operationToInsert.IsChecked = false;
        operationToInsert.Parameters = new ObservableCollection<WorkStepOperationParameter>(
            operationToInsert.Parameters.Select(parameter =>
            {
                WorkStepOperationParameter clonedParameter = parameter.Clone();
                clonedParameter.Id = Guid.NewGuid().ToString("N");
                return clonedParameter;
            }));

        ObservableCollection<WorkStepOperation> steps = SelectedWorkStep.Steps;
        int insertIndex = steps.Count;
        if (targetOperation is not null)
        {
            int targetIndex = steps.IndexOf(targetOperation);
            if (targetIndex >= 0)
            {
                insertIndex = targetIndex + (insertAfter ? 1 : 0);
            }
        }

        steps.Insert(Math.Clamp(insertIndex, 0, steps.Count), operationToInsert);
        RefreshOperationParameterModifiedState(operationToInsert);
        SelectedOperation = operationToInsert;
        SetPageStatus("已从方法表新增步骤。", SuccessBrush);
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
            item.InvokeMethod);
    }

    /// <summary>
    /// 按操作定义组装步骤操作，并填充默认返回值和参数。
    /// </summary>
    private WorkStepOperation? CreateOperationFromMethodDefinition(
        string operationType,
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
            OperationType = string.IsNullOrWhiteSpace(operationType) ? "设备" : operationType,
            OperationObject = operationObject,
            DeviceId = string.Equals(operationType?.Trim(), "涓氬姟", StringComparison.OrdinalIgnoreCase)
                ? BusinessOperationBindingResolver.ResolveCatalogDeviceId(operationObject, operationObject)
                : operationObject,
            ProtocolName = protocolName,
            CommandName = commandName,
            InvokeMethod = invokeMethod,
            OperationId = invokeMethod,
            ReturnValue = ResolveDefaultProtocolCommandReturnValueKey(protocolName, commandName),
            ShowDataToView = false,
            DelayMilliseconds = 0,
            Remark = string.Empty,
            Parameters = CreateOperationParametersFromMethodTableRow(operationObject, protocolName, commandName, invokeMethod)
        };
        RefreshOperationParameterModifiedState(operation);

        return operation;
    }

    /// <summary>
    /// 保存步骤编辑抽屉中的当前内容，并同步回目标步骤对象。
    /// </summary>
    private void SaveOperationDrawer()
    {
        if (SelectedWorkStep is null || _drawerOperation is null)
        {
            CloseOperationDrawer();
            return;
        }

        bool isLuaOperation = IsLuaOperationSelected;
        WorkStepOperation? selectedMethodOperation = isLuaOperation
            ? null
            : CreateOperationFromMethodItem(SelectedOperationMethod);

        if (string.IsNullOrWhiteSpace(EditingOperationObject) && selectedMethodOperation is null)
        {
            SetPageStatus("操作对象不能为空。", WarningBrush);
            return;
        }

        string invokeMethod = isLuaOperation
            ? LuaOperationObjectName
            : selectedMethodOperation?.InvokeMethod ??
              (IsSystemOrJudgeOperationSelected
                  ? EditingInvokeMethod
                  : string.IsNullOrWhiteSpace(EditingCommandName)
                      ? EditingInvokeMethod
                      : EditingCommandName);
        if (string.IsNullOrWhiteSpace(invokeMethod))
        {
            SetPageStatus("调用方法不能为空。", WarningBrush);
            return;
        }

        if (!isLuaOperation &&
            EditingShowDataToView &&
            string.IsNullOrWhiteSpace(EditingViewDataName))
        {
            SetPageStatus("勾选显示到界面时，数据名称不能为空。", WarningBrush);
            return;
        }

        if (!int.TryParse(EditingDelayMillisecondsText, out int delayMilliseconds) || delayMilliseconds < 0)
        {
            SetPageStatus("延时(ms)必须是大于等于 0 的整数。", WarningBrush);
            return;
        }

        _drawerOperation.OperationType = isLuaOperation
            ? LuaOperationObjectName
            : selectedMethodOperation?.OperationType ??
              (IsJudgeOperationSelected
                  ? JudgeOperationObjectName
                  : IsSystemOperationSelected
                      ? "系统"
                      : "设备");
        _drawerOperation.OperationObject = isLuaOperation
            ? LuaOperationObjectName
            : selectedMethodOperation?.OperationObject ?? EditingOperationObject.Trim();
        _drawerOperation.DeviceId = isLuaOperation
            ? LuaOperationObjectName
            : selectedMethodOperation?.DeviceId ??
              BusinessOperationBindingResolver.ResolveCatalogDeviceId(
                  _drawerOperation.OperationObject,
                  _drawerOperation.DeviceId);
        _drawerOperation.ProtocolName = isLuaOperation
            ? string.Empty
            : selectedMethodOperation?.ProtocolName ??
              (IsProtocolCommandSelectionVisible ? EditingProtocolName.Trim() : string.Empty);
        _drawerOperation.CommandName = isLuaOperation
            ? string.Empty
            : selectedMethodOperation?.CommandName ??
              (IsProtocolCommandSelectionVisible ? invokeMethod.Trim() : string.Empty);
        _drawerOperation.InvokeMethod = invokeMethod.Trim();
        _drawerOperation.OperationId = invokeMethod.Trim();
        _drawerOperation.ReturnValue = isLuaOperation ? string.Empty : EditingReturnValue.Trim();
        _drawerOperation.ShowDataToView = !isLuaOperation && EditingShowDataToView;
        _drawerOperation.ViewDataName = isLuaOperation ? string.Empty : EditingViewDataName.Trim();
        _drawerOperation.ViewJudgeType = isLuaOperation ? string.Empty : EditingViewJudgeType.Trim();
        _drawerOperation.ViewJudgeCondition = isLuaOperation ? string.Empty : EditingViewJudgeCondition.Trim();
        _drawerOperation.LuaScript = isLuaOperation ? EditingLuaScript : string.Empty;
        _drawerOperation.DelayMilliseconds = delayMilliseconds;
        _drawerOperation.Remark = EditingRemark.Trim();
        if (isLuaOperation)
        {
            _drawerOperation.Parameters = new ObservableCollection<WorkStepOperationParameter>();
        }
        else if (EditingModifyInvokeParameters)
        {
            NormalizeInvokeParameterSequences();
            SortInvokeParametersBySequence();
            _drawerOperation.Parameters = new ObservableCollection<WorkStepOperationParameter>(
                EditingInvokeParameters
                    .OrderBy(parameter => parameter.Sequence)
                    .Select(parameter => parameter.Clone()));
        }
        else if (selectedMethodOperation is not null)
        {
            _drawerOperation.Parameters = new ObservableCollection<WorkStepOperationParameter>(
                selectedMethodOperation.Parameters.Select(parameter => parameter.Clone()));
        }
        RefreshOperationParameterModifiedState(_drawerOperation);

        if (_isNewOperationInDrawer)
        {
            SelectedWorkStep.Steps.Add(_drawerOperation);
        }

        SelectedOperation = _drawerOperation;
        bool savedNewOperation = _isNewOperationInDrawer;
        CloseOperationDrawer();
        SetPageStatus(savedNewOperation ? "已新增步骤。" : "已更新步骤。", SuccessBrush);
    }

    /// <summary>
    /// 关闭步骤编辑抽屉，不提交当前编辑缓存。
    /// </summary>
    private void CloseOperationDrawer()
    {
        IsOperationDrawerOpen = false;
        _drawerOperation = null;
        _isNewOperationInDrawer = false;
        EditingInvokeParameters.Clear();
        EditingInvokeMethodRemark = string.Empty;
        EditingModifyInvokeParameters = false;
        EditingShowDataToView = false;
        EditingViewDataName = string.Empty;
        EditingViewJudgeType = string.Empty;
        EditingViewJudgeCondition = string.Empty;
        SelectedEditingInvokeParameter = null;
        OnPropertyChanged(nameof(OperationDrawerTitle));
    }

    /// <summary>
    /// 删除当前选中的操作步骤。
    /// </summary>
    private void DeleteSelectedOperation()
    {
        if (SelectedWorkStep is null)
        {
            return;
        }

        ObservableCollection<WorkStepOperation> steps = SelectedWorkStep.Steps;
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

        if (_drawerOperation is not null &&
            operationsToDelete.Any(operation => ReferenceEquals(operation, _drawerOperation)))
        {
            CloseOperationDrawer();
        }

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
    /// 判断当前是否允许复制步骤。
    /// </summary>
    private bool CanCopyOperations()
    {
        return SelectedWorkStep is not null && GetOperationsForClipboard().Count > 0;
    }

    /// <summary>
    /// 判断当前是否允许粘贴已复制的步骤。
    /// </summary>
    private bool CanPasteOperations()
    {
        return SelectedWorkStep is not null && _copiedOperations.Count > 0;
    }

    /// <summary>
    /// 判断当前是否存在可删除的步骤。
    /// </summary>
    private bool CanDeleteOperations()
    {
        return SelectedWorkStep is not null &&
               (SelectedOperation is not null || SelectedWorkStep.Steps.Any(operation => operation.IsChecked));
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

        ObservableCollection<WorkStepOperation> steps = SelectedWorkStep.Steps;
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

        List<WorkStepOperation> checkedOperations = GetCheckedOperations(SelectedWorkStep.Steps);
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
        operation.Parameters = new ObservableCollection<WorkStepOperationParameter>(
            operation.Parameters.Select(parameter =>
            {
                parameter.Id = Guid.NewGuid().ToString("N");
                return parameter;
            }));

        return operation;
    }

    /// <summary>
    /// 初始化步骤编辑抽屉中的各项编辑状态。
    /// </summary>
    private void BeginOperationDrawer(WorkStepOperation operation, bool isNewOperation)
    {
        _drawerOperation = operation;
        _isNewOperationInDrawer = isNewOperation;
        _isInitializingOperationDrawer = true;
        try
        {
            string operationObject = ResolveOperationObjectForEditing(operation);
            if (_isDecisionOperationMode &&
                !IsJudgeOperationObject(operationObject) &&
                string.IsNullOrWhiteSpace(operation.OperationObject))
            {
                operationObject = JudgeOperationObjectName;
            }

            EnsureOperationObjectOption(operationObject);
            EditingOperationObject = operationObject;
            EditingProtocolName = operation.ProtocolName;
            EnsureProtocolOption(EditingProtocolName);
            EditingCommandName = string.IsNullOrWhiteSpace(operation.CommandName)
                ? operation.InvokeMethod
                : operation.CommandName;
            EnsureCommandOption(EditingCommandName);
            EditingInvokeMethod = IsSystemOrJudgeOperationSelected ? operation.InvokeMethod : EditingCommandName;
            RefreshProtocolOptions(updateStatus: false);
            RefreshInvokeMethodOptions(updateStatus: false);
            EditingReturnValue = operation.ReturnValue;
            EditingShowDataToView = operation.ShowDataToView;
            EditingViewDataName = operation.ViewDataName;
            EditingViewJudgeType = operation.ViewJudgeType;
            EditingViewJudgeCondition = operation.ViewJudgeCondition;
            EditingLuaScript = operation.LuaScript;
            EditingDelayMillisecondsText = operation.DelayMilliseconds.ToString();
            EditingRemark = operation.Remark;
            EditingModifyInvokeParameters = false;
            EditingInvokeParameters.Clear();
            foreach (WorkStepOperationParameter parameter in IsLuaOperationSelected
                         ? Enumerable.Empty<WorkStepOperationParameter>()
                         : operation.Parameters.Select(parameter => parameter.Clone()))
            {
                EditingInvokeParameters.Add(parameter);
            }
        }
        finally
        {
            _isInitializingOperationDrawer = false;
        }

        NormalizeInvokeParameterSequences();
        SortInvokeParametersBySequence();

        if (IsLuaOperationSelected)
        {
            EditingProtocolName = string.Empty;
            EditingCommandName = string.Empty;
            EditingInvokeMethod = LuaOperationObjectName;
            EditingInvokeMethodRemark = string.Empty;
            EditingReturnValue = string.Empty;
            EditingShowDataToView = false;
            EditingViewDataName = string.Empty;
            EditingViewJudgeType = string.Empty;
            EditingViewJudgeCondition = string.Empty;
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

        SelectedEditingInvokeParameter = EditingInvokeParameters.FirstOrDefault();
        OnPropertyChanged(nameof(OperationDrawerTitle));
        IsOperationDrawerOpen = true;
    }

    /// <summary>
    /// 在当前编辑集合中新增一个调用参数。
    /// </summary>
    private void AddInvokeParameter()
    {
        WorkStepOperationParameter parameter = new()
        {
            Sequence = GetNextInvokeParameterSequence(),
            Name = ParameterTypeOptions.FirstOrDefault() ?? "设置值",
            Value = string.Empty,
            Remark = string.Empty
        };

        EditingInvokeParameters.Add(parameter);
        SortInvokeParametersBySequence();
        SelectedEditingInvokeParameter = parameter;
        SetPageStatus("已新增调用方法参数。", SuccessBrush);
    }

    /// <summary>
    /// 删除当前选中的调用参数。
    /// </summary>
    private void DeleteSelectedInvokeParameter()
    {
        if (SelectedEditingInvokeParameter is null)
        {
            return;
        }

        int index = EditingInvokeParameters.IndexOf(SelectedEditingInvokeParameter);
        EditingInvokeParameters.Remove(SelectedEditingInvokeParameter);
        SelectedEditingInvokeParameter = EditingInvokeParameters.Count == 0
            ? null
            : EditingInvokeParameters[Math.Clamp(index, 0, EditingInvokeParameters.Count - 1)];
        SetPageStatus("已删除调用方法参数。", WarningBrush);
    }

    /// <summary>
    /// 监听调用参数集合变化，维护事件订阅和顺序状态。
    /// </summary>
    private void EditingInvokeParameters_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Move)
        {
            return;
        }

        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (WorkStepOperationParameter parameter in _trackedEditingInvokeParameters.ToList())
            {
                parameter.PropertyChanged -= EditingInvokeParameter_PropertyChanged;
            }

            _trackedEditingInvokeParameters.Clear();
        }

        if (e.NewItems is not null)
        {
            foreach (WorkStepOperationParameter parameter in e.NewItems.OfType<WorkStepOperationParameter>())
            {
                if (_trackedEditingInvokeParameters.Add(parameter))
                {
                    parameter.PropertyChanged += EditingInvokeParameter_PropertyChanged;
                }

                UpdateParameterValueOptions(parameter);
            }
        }

        if (e.OldItems is not null)
        {
            foreach (WorkStepOperationParameter parameter in e.OldItems.OfType<WorkStepOperationParameter>())
            {
                if (_trackedEditingInvokeParameters.Remove(parameter))
                {
                    parameter.PropertyChanged -= EditingInvokeParameter_PropertyChanged;
                }
            }
        }
    }

    /// <summary>
    /// 监听单个调用参数变化，刷新修改标记和可选值。
    /// </summary>
    private void EditingInvokeParameter_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not WorkStepOperationParameter parameter)
        {
            return;
        }

        if (e.PropertyName is nameof(WorkStepOperationParameter.Name) or nameof(WorkStepOperationParameter.Type))
        {
            UpdateParameterValueOptions(parameter);
        }

        if (e.PropertyName == nameof(WorkStepOperationParameter.Sequence))
        {
            SortInvokeParametersBySequence();
        }
    }

    #endregion

    #region 工具方法

    /// <summary>
    /// 规范当前调用参数的序号，确保连续递增。
    /// </summary>
    private void NormalizeInvokeParameterSequences()
    {
        bool wasSorting = _isSortingInvokeParameters;
        _isSortingInvokeParameters = true;
        try
        {
            HashSet<int> usedSequences = new();
            int nextSequence = 1;
            foreach (WorkStepOperationParameter parameter in EditingInvokeParameters)
            {
                if (parameter.Sequence <= 0 || !usedSequences.Add(parameter.Sequence))
                {
                    while (usedSequences.Contains(nextSequence))
                    {
                        nextSequence++;
                    }

                    parameter.Sequence = nextSequence;
                    usedSequences.Add(parameter.Sequence);
                }

                nextSequence = Math.Max(nextSequence, parameter.Sequence + 1);
            }
        }
        finally
        {
            _isSortingInvokeParameters = wasSorting;
        }
    }

    /// <summary>
    /// 获取新增调用参数时使用的下一个序号。
    /// </summary>
    private int GetNextInvokeParameterSequence()
    {
        return EditingInvokeParameters.Count == 0
            ? 1
            : EditingInvokeParameters.Max(parameter => parameter.Sequence) + 1;
    }

    /// <summary>
    /// 按参数序号重新排序当前编辑集合。
    /// </summary>
    private void SortInvokeParametersBySequence()
    {
        if (_isSortingInvokeParameters || EditingInvokeParameters.Count < 2)
        {
            return;
        }

        _isSortingInvokeParameters = true;
        try
        {
            List<WorkStepOperationParameter> orderedParameters = EditingInvokeParameters
                .Select((parameter, index) => new { Parameter = parameter, Index = index })
                .OrderBy(item => item.Parameter.Sequence)
                .ThenBy(item => item.Index)
                .Select(item => item.Parameter)
                .ToList();

            for (int targetIndex = 0; targetIndex < orderedParameters.Count; targetIndex++)
            {
                WorkStepOperationParameter parameter = orderedParameters[targetIndex];
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

    /// <summary>
    /// 刷新所有调用参数的可选值列表。
    /// </summary>
    private void RefreshParameterValueOptions()
    {
        foreach (WorkStepOperationParameter parameter in EditingInvokeParameters)
        {
            UpdateParameterValueOptions(parameter);
        }
    }

    /// <summary>
    /// 更新单个调用参数的可选值来源。
    /// </summary>
    private void UpdateParameterValueOptions(WorkStepOperationParameter parameter)
    {
        ReplaceStringOptions(parameter.ValueOptions, BuildParameterValueOptions(parameter.Type));
    }

    /// <summary>
    /// 按参数值类型构建候选值列表。
    /// </summary>
    private IEnumerable<string> BuildParameterValueOptions(string parameterType)
    {
        string normalizedType = parameterType?.Trim() ?? string.Empty;
        return normalizedType switch
        {
            "返回值" => BuildParameterReturnValueOptions(),
            _ => Enumerable.Empty<string>()
        };
    }

    /// <summary>
    /// 构建可供参数引用的返回值列表。
    /// </summary>
    private IEnumerable<string> BuildParameterReturnValueOptions()
    {
        if (_isStandaloneOperationEditMode)
        {
            return ExternalReturnValueOptions
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase);
        }

        if (SelectedWorkStep is null)
        {
            return Enumerable.Empty<string>();
        }

        List<WorkStepOperation> operations = SelectedWorkStep.Steps
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
            .SelectMany(operation => CreateReturnParametersFromOperation(operation))
            .Select(parameter => parameter.ParameterName)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 构建当前步骤可写入的返回值候选项。
    /// </summary>
    private IEnumerable<string> BuildReturnValueOptions()
    {
        if (_isStandaloneOperationEditMode)
        {
            return ExternalReturnValueOptions
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase);
        }

        IEnumerable<string> savedReturnValues = SelectedWorkStep?.Steps
            .Select(step => step.ReturnValue)
            .Where(value => !string.IsNullOrWhiteSpace(value)) ?? Enumerable.Empty<string>();

        IEnumerable<string> editingReturnValues = string.IsNullOrWhiteSpace(EditingReturnValue)
            ? Enumerable.Empty<string>()
            : new[] { EditingReturnValue };

        return savedReturnValues
            .Concat(ExternalReturnValueOptions)
            .Concat(LoadProtocolCommandReturnValueKeys(EditingProtocolName, EditingCommandName))
            .Concat(editingReturnValues)
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 刷新返回值下拉项，并修正默认值。
    /// </summary>
    private void RefreshReturnValueOptions()
    {
        ReplaceStringOptions(ReturnValueOptions, BuildReturnValueOptions());
    }

    /// <summary>
    /// 在存在唯一默认返回值时自动回填。
    /// </summary>
    private void ApplyDefaultReturnValueKey()
    {
        if (IsSystemOrJudgeOperationSelected ||
            IsLuaOperationSelected ||
            !string.IsNullOrWhiteSpace(EditingReturnValue))
        {
            return;
        }

        IReadOnlyList<string> keys = LoadProtocolCommandReturnValueKeys(EditingProtocolName, EditingCommandName);
        if (keys.Count == 1)
        {
            EditingReturnValue = keys[0];
        }
    }

    /// <summary>
    /// 使用新数据替换字符串选项集合内容。
    /// </summary>
    private static void ReplaceStringOptions(ObservableCollection<string> target, IEnumerable<string> source)
    {
        List<string> options = source
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Select(option => option.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
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
    /// 创建包含默认步骤的新工步。
    /// </summary>
    private WorkStepProfile CreateWorkStep(string stepName)
    {
        WorkStepProfile workStep = new()
        {
            StepName = stepName,
            LastModifiedAt = DateTime.Now
        };

        workStep.Steps.Add(new WorkStepOperation
        {
            OperationObject = SystemOperationObjectName,
            InvokeMethod = "等待",
            ReturnValue = string.Empty,
            ShowDataToView = false,
            ViewDataName = string.Empty,
            ViewJudgeType = string.Empty,
            ViewJudgeCondition = string.Empty,
            DelayMilliseconds = 0,
            Remark = string.Empty
        });

        return workStep;
    }

    /// <summary>
    /// 深拷贝工步及其全部步骤。
    /// </summary>
    private WorkStepProfile CreateCopyWorkStep(WorkStepProfile source)
    {
        return new WorkStepProfile
        {
            Id = Guid.NewGuid().ToString("N"),
            StepName = GenerateCopyStepName(source.StepName),
            LastModifiedAt = DateTime.Now,
            Steps = new ObservableCollection<WorkStepOperation>(
                source.Steps.Select(operation => new WorkStepOperation
                {
                    Id = Guid.NewGuid().ToString("N"),
                    OperationType = operation.OperationType,
                    OperationObject = operation.OperationObject,
                    DeviceId = operation.DeviceId,
                    ProtocolName = operation.ProtocolName,
                    CommandName = operation.CommandName,
                    InvokeMethod = operation.InvokeMethod,
                    OperationId = operation.OperationId,
                    ReturnValue = operation.ReturnValue,
                    ShowDataToView = operation.ShowDataToView,
                    ViewDataName = operation.ViewDataName,
                    ViewJudgeType = operation.ViewJudgeType,
                    ViewJudgeCondition = operation.ViewJudgeCondition,
                    LuaScript = operation.LuaScript,
                    DelayMilliseconds = operation.DelayMilliseconds,
                    Remark = operation.Remark,
                    Parameters = new ObservableCollection<WorkStepOperationParameter>(
                        operation.Parameters.Select(parameter => new WorkStepOperationParameter
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            Sequence = parameter.Sequence,
                            Name = parameter.Name,
                            ParameterName = parameter.ParameterName,
                            ValueType = parameter.ValueType,
                            Value = parameter.Value,
                            Remark = parameter.Remark
                        }))
                }))
        };
    }

    /// <summary>
    /// 用最新工步集合替换当前目录中的工步数据。
    /// </summary>
    private void ReloadWorkSteps(ObservableCollection<WorkStepProfile> latestWorkSteps)
    {
        WorkSteps.CollectionChanged -= WorkSteps_CollectionChanged;
        try
        {
            WorkSteps.Clear();
            foreach (WorkStepProfile workStep in latestWorkSteps)
            {
                WorkSteps.Add(workStep);
            }
        }
        finally
        {
            WorkSteps.CollectionChanged += WorkSteps_CollectionChanged;
        }
    }

    /// <summary>
    /// 选中新建或复制后的工步并滚动到可见范围。
    /// </summary>
    private void SelectCreatedWorkStep(WorkStepProfile workStep)
    {
        SearchText = string.Empty;
        WorkStepsView.Refresh();
        SelectedWorkStep = workStep;
        WorkStepsView.MoveCurrentTo(workStep);
    }

    /// <summary>
    /// 控制新建和复制工步命令的触发节流。
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
    /// 生成当前目录下不重复的工步名称。
    /// </summary>
    private string GenerateUniqueStepName(string prefix)
    {
        HashSet<string> existingNames = new(WorkSteps.Select(step => step.StepName), StringComparer.OrdinalIgnoreCase);

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
    /// 为复制得到的工步生成不重复名称。
    /// </summary>
    private string GenerateCopyStepName(string baseName)
    {
        HashSet<string> existingNames = new(WorkSteps.Select(step => step.StepName), StringComparer.OrdinalIgnoreCase);

        string copyName = $"{baseName.Trim()} 鍓湰";
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
    /// 根据搜索关键字筛选工步。
    /// </summary>
    private bool FilterWorkSteps(object item)
    {
        if (item is not WorkStepProfile workStep)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        string keyword = SearchText.Trim();
        return Contains(workStep.StepName, keyword) ||
               Contains(BuildWorkStepOperationSummary(workStep), keyword) ||
               Contains(workStep.LastModifiedText, keyword);
    }

    private static string BuildWorkStepOperationSummary(WorkStepProfile workStep)
    {
        if (workStep is null)
        {
            return string.Empty;
        }

        List<string> items = workStep.Steps
            .Where(operation => operation is not null)
            .Select(operation => operation.DisplayText)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => text.Trim())
            .ToList();

        return items.Count == 0 ? string.Empty : string.Join(" / ", items);
    }

    /// <summary>
    /// 以忽略大小写的方式判断文本是否包含关键字。
    /// </summary>
    private static bool Contains(string? source, string keyword)
    {
        return source?.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// 校验全部工步和步骤配置是否可保存。
    /// </summary>
    private bool ValidateWorkSteps(out string message)
    {
        HashSet<string> stepNames = new(StringComparer.OrdinalIgnoreCase);
        foreach (WorkStepProfile workStep in WorkSteps)
        {
            if (string.IsNullOrWhiteSpace(workStep.StepName))
            {
                message = "工步名称不能为空。";
                return false;
            }

            if (!stepNames.Add(workStep.StepName.Trim()))
            {
                message = $"工步名称不能重复：{workStep.StepName}";
                return false;
            }

            foreach (WorkStepOperation operation in workStep.Steps)
            {
                if (string.IsNullOrWhiteSpace(operation.OperationObject))
                {
                    message = $"工步“{workStep.StepName}”的操作对象不能为空。";
                    return false;
                }

                if (!IsLuaOperationObject(operation.OperationObject) &&
                    string.IsNullOrWhiteSpace(operation.InvokeMethod))
                {
                    message = $"工步“{workStep.StepName}”的调用方法不能为空。";
                    return false;
                }

                if (operation.DelayMilliseconds < 0)
                {
                    message = $"工步“{workStep.StepName}”的步骤延时不能小于 0。";
                    return false;
                }
            }
        }

        message = string.Empty;
        return true;
    }

    /// <summary>
    /// 监听工步集合变化，刷新页面汇总和选中状态。
    /// </summary>
    private void WorkSteps_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RaisePageSummaryChanged();
        WorkStepsView.Refresh();
        SelectFirstVisibleWorkStep();
        RaiseCommandStatesChanged();
    }

    /// <summary>
    /// 在当前筛选结果中优先保持原选中项，否则选中第一条可见工步。
    /// </summary>
    private void SelectFirstVisibleWorkStep()
    {
        SelectVisibleWorkStep(SelectedWorkStep?.Id);
    }

    /// <summary>
    /// 按给定工步 Id 恢复选择；若目标不可见，则回退到当前可见列表中的第一项。
    /// </summary>
    private void SelectVisibleWorkStep(string? preferredWorkStepId)
    {
        WorkStepProfile? preferredWorkStep = WorkStepsView
            .OfType<WorkStepProfile>()
            .FirstOrDefault(workStep =>
                !string.IsNullOrWhiteSpace(preferredWorkStepId) &&
                string.Equals(workStep.Id, preferredWorkStepId, StringComparison.Ordinal));

        if (preferredWorkStep is not null)
        {
            SelectedWorkStep = preferredWorkStep;
            WorkStepsView.MoveCurrentTo(preferredWorkStep);
            return;
        }

        SelectedWorkStep = WorkStepsView.OfType<WorkStepProfile>().FirstOrDefault();
        if (SelectedWorkStep is not null)
        {
            WorkStepsView.MoveCurrentTo(SelectedWorkStep);
        }
    }

    /// <summary>
    /// 更新页面底部状态文案和颜色。
    /// </summary>
    private void SetPageStatus(string text, Brush brush)
    {
        PageStatusText = text;
        PageStatusBrush = brush;
    }

    /// <summary>
    /// 加载可供选择的设备操作对象名称。
    /// </summary>
    public IEnumerable<string> LoadDeviceOperationObjectNames()
    {
        return LoadDeviceOperationObjectOptions();
    }

    /// <summary>
    /// 根据当前操作对象加载可调用的方法或指令列表。
    /// </summary>
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

    /// <summary>
    /// 为普通设备对象加载方法列表：先取业务方法，再拼接该设备支持协议下的指令。
    /// </summary>
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

    /// <summary>
    /// 当操作对象或方法发生变化时，重新规范化步骤元数据。
    /// 包括操作类型、业务绑定键、协议名和指令名。
    /// </summary>
    public void SynchronizeOperationMetadata(
        WorkStepOperation operation,
        IReadOnlyList<string> invokeMethodOptions)
    {
        if (operation is null)
        {
            return;
        }

        string operationObject = operation.OperationObject?.Trim() ?? string.Empty;

        if (IsLuaOperationObject(operationObject))
        {
            operation.OperationType = LuaOperationObjectName;
            operation.OperationObject = LuaOperationObjectName;
            operation.ProtocolName = string.Empty;
            operation.CommandName = string.Empty;
            operation.InvokeMethod = LuaOperationObjectName;
            return;
        }

        string invokeMethod = operation.InvokeMethod?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(invokeMethod) &&
            !invokeMethodOptions.Any(option => TextEquals(option, invokeMethod)))
        {
            invokeMethod = string.Empty;
            operation.InvokeMethod = invokeMethod;
        }

        if (IsSystemOperationObject(operationObject))
        {
            operation.OperationType = "系统";
            operation.OperationObject = SystemOperationObjectName;
            operation.DeviceId = SystemOperationObjectName;
            operation.ProtocolName = string.Empty;
            operation.CommandName = string.Empty;
            return;
        }

        operation.OperationType = "设备";
        BusinessOperationDescriptor? businessOperation = BusinessOperationBindingResolver.FindOperationForOperationObject(
            operationObject,
            operation.DeviceId,
            invokeMethod);
        if (businessOperation is not null)
        {
            operation.DeviceId = businessOperation.DeviceId;
            operation.ProtocolName = string.Empty;
            operation.CommandName = string.Empty;
            return;
        }

        operation.DeviceId = operationObject;
        if (TryFindDeviceCommand(operationObject, invokeMethod, out string protocolName, out string commandName))
        {
            operation.ProtocolName = protocolName;
            operation.CommandName = commandName;
        }
        else
        {
            operation.ProtocolName = string.Empty;
            operation.CommandName = string.Empty;
        }
    }

    /// <summary>
    /// 在设备支持的协议指令中查找与方法名匹配的协议和命令定义。
    /// </summary>
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
        if (allowedProtocols.Count == 0)
        {
            return false;
        }

        foreach (ProtocolSelectionItem protocol in LoadProtocolSelectionItems().Where(protocol => allowedProtocols.Contains(protocol.Name)))
        {
            ProtocolCommandSelectionItem? command = protocol.Commands
                .FirstOrDefault(command => TextEquals(command.Name, invokeMethod));
            if (command is null)
            {
                continue;
            }

            protocolName = protocol.Name;
            commandName = command.Name;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 按当前编辑状态刷新方法表，统一展示系统方法、业务方法和协议指令。
    /// </summary>
    private void RefreshOperationMethodTable()
    {
        SelectedOperationMethod = null;
        OperationMethods.Clear();
        if (IsLuaOperationSelected)
        {
            OnPropertyChanged(nameof(OperationMethods));
            return;
        }

        if (IsJudgeOperationSelected)
        {
            foreach (SystemMethodSelectionItem method in LoadJudgeMethodSelectionItems())
            {
                AddOperationMethod(
                    "方法",
                    JudgeOperationObjectName,
                    JudgeOperationObjectName,
                    string.Empty,
                    string.Empty,
                    method.Name,
                    method.Summary,
                    method.Parameters.Count);
            }

            OnPropertyChanged(nameof(OperationMethods));
            return;
        }

        if (IsSystemOperationSelected)
        {
            IReadOnlyList<SystemMethodSelectionItem> methods = LoadSystemMethodSelectionItems();
            foreach (SystemMethodSelectionItem method in methods)
            {
                AddOperationMethod(
                    "方法",
                    "系统",
                    SystemOperationObjectName,
                    string.Empty,
                    string.Empty,
                    method.Name,
                    method.Summary,
                    method.Parameters.Count);
            }

            OnPropertyChanged(nameof(OperationMethods));
            return;
        }

        string operationObject = EditingOperationObject.Trim();
        foreach (BusinessOperationDescriptor operation in BusinessOperationBindingResolver
                     .GetOperationsForOperationObject(operationObject)
                     .OrderBy(operation => operation.DisplayName, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(operation => operation.OperationId, StringComparer.OrdinalIgnoreCase))
        {
            AddOperationMethod(
                "业务",
                "业务",
                operationObject,
                string.Empty,
                string.Empty,
                operation.OperationId,
                string.IsNullOrWhiteSpace(operation.Description) ? operation.DisplayName : operation.Description,
                operation.Parameters.Count);
        }

        HashSet<string> allowedProtocols = new(LoadDeviceSupportedProtocolNames(operationObject), StringComparer.OrdinalIgnoreCase);
        if (allowedProtocols.Count == 0)
        {
            OnPropertyChanged(nameof(OperationMethods));
            return;
        }

        IReadOnlyList<ProtocolSelectionItem> protocols = LoadProtocolSelectionItems().ToList();
        IEnumerable<ProtocolSelectionItem> visibleProtocols = protocols.Where(protocol => allowedProtocols.Contains(protocol.Name));

        foreach (ProtocolSelectionItem protocol in visibleProtocols.OrderBy(protocol => protocol.Name, StringComparer.OrdinalIgnoreCase))
        {
            foreach (ProtocolCommandSelectionItem command in protocol.Commands.OrderBy(command => command.Name, StringComparer.OrdinalIgnoreCase))
            {
                AddOperationMethod(
                    "指令",
                    "设备",
                    operationObject,
                    protocol.Name,
                    command.Name,
                    command.Name,
                    protocol.Name,
                    command.Placeholders.Count);
            }
        }

        OnPropertyChanged(nameof(OperationMethods));
    }

    /// <summary>
    /// 向方法指令表追加一行可选操作定义。
    /// </summary>
    private void AddOperationMethod(
        string kind,
        string operationType,
        string operationObject,
        string protocolName,
        string commandName,
        string invokeMethod,
        string summary,
        int parameterCount)
    {
        OperationMethods.Add(new StationOperationMethodItem
        {
            Kind = kind,
            OperationType = operationType,
            OperationObject = operationObject,
            ProtocolName = protocolName,
            CommandName = commandName,
            InvokeMethod = invokeMethod,
            Summary = summary,
            ParameterCount = parameterCount
        });
    }

    /// <summary>
    /// 根据方法表中的选项构建默认输入参数。
    /// 系统方法、业务方法和协议指令分别走各自的参数生成逻辑。
    /// </summary>
    private ObservableCollection<WorkStepOperationParameter> CreateOperationParametersFromMethodTableRow(
        string operationObject,
        string protocolName,
        string commandName,
        string invokeMethod)
    {
        if (IsJudgeOperationObject(operationObject))
        {
            SystemMethodSelectionItem? method = FindJudgeMethodByName(invokeMethod);
            return CreateOperationParametersFromSystemMethod(method, useTypeAsDefaultValue: false);
        }

        if (IsSystemOperationObject(operationObject))
        {
            SystemMethodSelectionItem? method = FindSystemMethodByName(invokeMethod);
            return CreateOperationParametersFromSystemMethod(method, useTypeAsDefaultValue: true);
        }

        BusinessOperationDescriptor? businessOperation = BusinessOperationBindingResolver.FindOperationForOperationObject(
            operationObject,
            null,
            invokeMethod);
        if (businessOperation is not null)
        {
            return CreateOperationParametersFromBusinessOperation(businessOperation);
        }

        ObservableCollection<WorkStepOperationParameter> parameters = new();
        int sequence = 1;
        foreach (ProtocolPlaceholderSelectionItem placeholder in LoadProtocolCommandPlaceholders(protocolName, commandName))
        {
            parameters.Add(new WorkStepOperationParameter
            {
                Sequence = sequence,
                Name = ParameterTypeOptions.FirstOrDefault() ?? "设置值",
                ParameterName = placeholder.Name,
                Value = placeholder.Value,
                Remark = placeholder.Name
            });
            sequence++;
        }

        return parameters;
    }

    /// <summary>
    /// 为步骤重新生成默认输入参数，用于回填和重置。
    /// </summary>
    public ObservableCollection<WorkStepOperationParameter> CreateDefaultOperationParameters(WorkStepOperation operation)
    {
        if (operation is null ||
            IsLuaOperationObject(operation.OperationObject) ||
            IsLuaOperationObject(operation.OperationType))
        {
            return new ObservableCollection<WorkStepOperationParameter>();
        }

        string operationObject = operation.OperationObject?.Trim() ?? string.Empty;
        string protocolName = operation.ProtocolName?.Trim() ?? string.Empty;
        string commandName = string.IsNullOrWhiteSpace(operation.CommandName)
            ? operation.InvokeMethod?.Trim() ?? string.Empty
            : operation.CommandName.Trim();
        string invokeMethod = operation.InvokeMethod?.Trim() ?? string.Empty;
        BusinessOperationDescriptor? businessOperation = BusinessOperationBindingResolver.FindOperationForOperationObject(
            operationObject,
            operation.DeviceId,
            invokeMethod);
        if (businessOperation is not null)
        {
            return CreateOperationParametersFromBusinessOperation(businessOperation);
        }

        if (!IsSystemOperationObject(operationObject) &&
            !IsJudgeOperationObject(operationObject) &&
            (string.IsNullOrWhiteSpace(protocolName) || string.IsNullOrWhiteSpace(commandName)) &&
            TryFindDeviceCommand(operationObject, invokeMethod, out string resolvedProtocolName, out string resolvedCommandName))
        {
            protocolName = resolvedProtocolName;
            commandName = resolvedCommandName;
        }

        return CreateOperationParametersFromMethodTableRow(operationObject, protocolName, commandName, invokeMethod);
    }

    /// <summary>
    /// 为当前步骤推导返回值参数定义，供工步参数映射和界面显示使用。
    /// </summary>
    public ObservableCollection<WorkStepOperationParameter> CreateReturnParametersFromOperation(WorkStepOperation? operation)
    {
        if (operation is null ||
            IsLuaOperationObject(operation.OperationObject) ||
            IsLuaOperationObject(operation.OperationType))
        {
            return new ObservableCollection<WorkStepOperationParameter>();
        }

        string operationObject = operation.OperationObject?.Trim() ?? string.Empty;
        string protocolName = operation.ProtocolName?.Trim() ?? string.Empty;
        string commandName = string.IsNullOrWhiteSpace(operation.CommandName)
            ? operation.InvokeMethod?.Trim() ?? string.Empty
            : operation.CommandName.Trim();
        string invokeMethod = operation.OperationId?.Trim() ?? operation.InvokeMethod?.Trim() ?? string.Empty;

        BusinessOperationDescriptor? businessOperation = BusinessOperationBindingResolver.FindOperationForOperationObject(
            operationObject,
            operation.DeviceId,
            invokeMethod);
        if (businessOperation is not null &&
            !string.Equals(businessOperation.ReturnTypeName, "void", StringComparison.OrdinalIgnoreCase))
        {
            string key = string.IsNullOrWhiteSpace(operation.ReturnValue)
                ? businessOperation.OperationId
                : operation.ReturnValue.Trim();
            return new ObservableCollection<WorkStepOperationParameter>
            {
                new()
                {
                    Sequence = 1,
                    Name = "ReturnValue",
                    ParameterName = key,
                    ValueType = businessOperation.ReturnTypeName,
                    Value = key,
                    Remark = string.IsNullOrWhiteSpace(businessOperation.Description)
                        ? businessOperation.DisplayName
                        : businessOperation.Description
                }
            };
        }

        if (!IsSystemOperationObject(operationObject) &&
            !IsJudgeOperationObject(operationObject) &&
            (string.IsNullOrWhiteSpace(protocolName) || string.IsNullOrWhiteSpace(commandName)) &&
            TryFindDeviceCommand(
                operationObject,
                operation.InvokeMethod?.Trim() ?? string.Empty,
                out string resolvedProtocolName,
                out string resolvedCommandName))
        {
            protocolName = resolvedProtocolName;
            commandName = resolvedCommandName;
        }

        List<string> returnKeys = LoadProtocolCommandReturnValueKeys(protocolName, commandName)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (returnKeys.Count == 0 && !string.IsNullOrWhiteSpace(operation.ReturnValue))
        {
            returnKeys.Add(operation.ReturnValue.Trim());
        }

        string description = IsSystemOperationObject(operationObject)
            ? "方法返回值"
            : IsJudgeOperationObject(operationObject)
                ? "判断结果"
                : "返回参数";

        return new ObservableCollection<WorkStepOperationParameter>(
            returnKeys.Select((key, index) => new WorkStepOperationParameter
            {
                Sequence = index + 1,
                Name = "返回值",
                ParameterName = key,
                Value = key,
                Remark = description
            }));
    }

    /// <summary>
    /// 判断步骤当前参数是否偏离默认生成结果。
    /// </summary>
    public bool HasModifiedOperationParameters(
        WorkStepOperation operation,
        IEnumerable<WorkStepOperationParameter>? parameters = null)
    {
        ObservableCollection<WorkStepOperationParameter> defaultParameters = CreateDefaultOperationParameters(operation);
        return !HasSameOperationParameters(parameters ?? operation.Parameters, defaultParameters) ||
               HasModifiedOperationReturnParameters(operation);
    }

    /// <summary>
    /// 刷新单条步骤的“参数已修改”标记。
    /// </summary>
    public void RefreshOperationParameterModifiedState(WorkStepOperation operation)
    {
        operation.AreParametersModified = HasModifiedOperationParameters(operation);
    }

    /// <summary>
    /// 批量刷新步骤的“参数已修改”标记。
    /// </summary>
    public void RefreshOperationParameterModifiedStates(IEnumerable<WorkStepOperation> operations)
    {
        foreach (WorkStepOperation operation in operations.Where(operation => operation is not null))
        {
            RefreshOperationParameterModifiedState(operation);
        }
    }

    /// <summary>
    /// 将步骤参数恢复为按当前方法推导出的默认值。
    /// </summary>
    public void ResetOperationParametersToDefault(WorkStepOperation operation)
    {
        if (operation is null)
        {
            return;
        }

        operation.Parameters = CreateDefaultOperationParameters(operation);
        operation.AreParametersModified = false;
    }

    /// <summary>
    /// 比较两组步骤参数是否完全一致。
    /// </summary>
    private static bool HasSameOperationParameters(
        IEnumerable<WorkStepOperationParameter> first,
        IEnumerable<WorkStepOperationParameter> second)
    {
        List<WorkStepOperationParameter> firstItems = first
            .OrderBy(parameter => parameter.Sequence)
            .ToList();
        List<WorkStepOperationParameter> secondItems = second
            .OrderBy(parameter => parameter.Sequence)
            .ToList();

        if (firstItems.Count != secondItems.Count)
        {
            return false;
        }

        for (int index = 0; index < firstItems.Count; index++)
        {
            WorkStepOperationParameter left = firstItems[index];
            WorkStepOperationParameter right = secondItems[index];
            if (left.Sequence != right.Sequence ||
                !TextEquals(left.Name, right.Name) ||
                !TextEquals(left.ParameterName, right.ParameterName) ||
                !TextEquals(left.ValueType, right.ValueType) ||
                !TextEquals(left.Value, right.Value) ||
                !TextEquals(left.Remark, right.Remark))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 判断步骤返回值相关配置是否偏离默认值。
    /// </summary>
    private static bool HasModifiedOperationReturnParameters(WorkStepOperation operation)
    {
        string defaultReturnValue = ResolveDefaultOperationReturnValue(operation);
        return !TextEquals(operation.ReturnValue, defaultReturnValue) ||
               operation.ShowDataToView ||
               !string.IsNullOrWhiteSpace(operation.ViewDataName) ||
               !string.IsNullOrWhiteSpace(operation.ViewJudgeType) ||
               !string.IsNullOrWhiteSpace(operation.ViewJudgeCondition);
    }

    /// <summary>
    /// 推导当前步骤默认应使用的返回值键。
    /// </summary>
    private static string ResolveDefaultOperationReturnValue(WorkStepOperation operation)
    {
        if (operation is null ||
            IsLuaOperationObject(operation.OperationObject) ||
            IsLuaOperationObject(operation.OperationType))
        {
            return string.Empty;
        }

        string protocolName = operation.ProtocolName?.Trim() ?? string.Empty;
        string commandName = string.IsNullOrWhiteSpace(operation.CommandName)
            ? operation.InvokeMethod?.Trim() ?? string.Empty
            : operation.CommandName.Trim();

        return ResolveDefaultProtocolCommandReturnValueKey(protocolName, commandName);
    }

    /// <summary>
    /// 根据系统方法元数据生成输入参数集合。
    /// </summary>
    private ObservableCollection<WorkStepOperationParameter> CreateOperationParametersFromSystemMethod(
        SystemMethodSelectionItem? method,
        bool useTypeAsDefaultValue)
    {
        ObservableCollection<WorkStepOperationParameter> parameters = new();
        if (method is null)
        {
            return parameters;
        }

        int sequence = 1;
        foreach (SystemMethodParameterSelectionItem parameterMetadata in method.Parameters)
        {
            parameters.Add(new WorkStepOperationParameter
            {
                Sequence = sequence,
                Name = ParameterTypeOptions.FirstOrDefault() ?? "设置值",
                ParameterName = parameterMetadata.Name,
                ValueType = parameterMetadata.Type,
                Value = parameterMetadata.DefaultValue,
                Remark = parameterMetadata.Description
            });
            sequence++;
        }

        return parameters;
    }

    /// <summary>
    /// 根据业务方法描述生成输入参数集合。
    /// 运行时注入参数不会出现在这里。
    /// </summary>
    private ObservableCollection<WorkStepOperationParameter> CreateOperationParametersFromBusinessOperation(
        BusinessOperationDescriptor operation)
    {
        ObservableCollection<WorkStepOperationParameter> parameters = new();
        foreach (BusinessParameterDescriptor parameterMetadata in operation.Parameters.OrderBy(parameter => parameter.Sequence))
        {
            parameters.Add(new WorkStepOperationParameter
            {
                Sequence = parameterMetadata.Sequence,
                Name = ParameterTypeOptions.FirstOrDefault() ?? "Literal",
                ParameterName = parameterMetadata.Name,
                ValueType = parameterMetadata.TypeName,
                Value = parameterMetadata.DefaultValue,
                Remark = string.IsNullOrWhiteSpace(parameterMetadata.Description)
                    ? parameterMetadata.DisplayName
                    : parameterMetadata.Description
            });
        }

        return parameters;
    }

    /// <summary>
    /// 刷新操作对象下拉项，并尽量保留当前编辑选择。
    /// </summary>
    private void RefreshOperationObjectOptions(bool updateStatus)
    {
        string previousSelection = EditingOperationObject;
        OperationObjectOptions.Clear();

        if (_isDecisionOperationMode)
        {
            OperationObjectOptions.Add(JudgeOperationObjectName);
        }
        else
        {
            OperationObjectOptions.Add(SystemOperationObjectName);
            OperationObjectOptions.Add(LuaOperationObjectName);
            foreach (string option in LoadDeviceOperationObjectOptions()
                         .Where(option => !string.IsNullOrWhiteSpace(option))
                         .Select(option => option.Trim())
                         .Where(option => !IsSystemOperationObject(option))
                         .Where(option => !IsLuaOperationObject(option))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(option => option, StringComparer.OrdinalIgnoreCase))
            {
                OperationObjectOptions.Add(option);
            }
        }

        if (!string.IsNullOrWhiteSpace(previousSelection) &&
            OperationObjectOptions.Any(option => string.Equals(option, previousSelection, StringComparison.OrdinalIgnoreCase)))
        {
            EditingOperationObject = previousSelection;
        }
        else if (_isDecisionOperationMode)
        {
            EditingOperationObject = JudgeOperationObjectName;
        }
        else
        {
            EditingOperationObject = SystemOperationObjectName;
        }

        RefreshProtocolOptions(updateStatus: false);
        RefreshInvokeMethodOptions(updateStatus: false);

        if (updateStatus)
        {
            SetPageStatus("已刷新操作对象。", SuccessBrush);
        }
    }

    /// <summary>
    /// 根据当前操作对象刷新可选协议列表。
    /// </summary>
    private void RefreshProtocolOptions(bool updateStatus)
    {
        string previousSelection = EditingProtocolName;
        ProtocolOptions.Clear();

        if (IsSystemOrJudgeOperationSelected || IsLuaOperationSelected)
        {
            EditingProtocolName = string.Empty;
            RefreshCommandOptions(updateStatus: false);
            return;
        }

        foreach (string option in LoadProtocolOptions())
        {
            ProtocolOptions.Add(option);
        }

        if (!string.IsNullOrWhiteSpace(previousSelection) &&
            ProtocolOptions.Any(option => string.Equals(option, previousSelection, StringComparison.OrdinalIgnoreCase)))
        {
            EditingProtocolName = previousSelection;
        }
        else
        {
            EditingProtocolName = ProtocolOptions.FirstOrDefault() ?? string.Empty;
        }

        RefreshCommandOptions(updateStatus: false);

        if (updateStatus)
        {
            SetPageStatus("已刷新协议列表。", SuccessBrush);
        }
    }

    /// <summary>
    /// 根据当前协议刷新可选指令列表。
    /// </summary>
    private void RefreshCommandOptions(bool updateStatus)
    {
        string previousSelection = EditingCommandName;
        CommandOptions.Clear();

        if (IsSystemOrJudgeOperationSelected || IsLuaOperationSelected || string.IsNullOrWhiteSpace(EditingProtocolName))
        {
            EditingCommandName = string.Empty;
            return;
        }

        foreach (string option in LoadProtocolCommandOptions(EditingProtocolName))
        {
            CommandOptions.Add(option);
        }

        if (!string.IsNullOrWhiteSpace(previousSelection) &&
            CommandOptions.Any(option => string.Equals(option, previousSelection, StringComparison.OrdinalIgnoreCase)))
        {
            EditingCommandName = previousSelection;
        }
        else
        {
            EditingCommandName = CommandOptions.FirstOrDefault() ?? string.Empty;
        }

        EditingInvokeMethod = EditingCommandName;
        RefreshInvokeParametersFromSelectedCommand();

        if (updateStatus)
        {
            SetPageStatus($"已按协议“{EditingProtocolName}”刷新指令。", SuccessBrush);
        }
    }

    /// <summary>
    /// 根据当前协议指令刷新占位符参数列表。
    /// </summary>
    private void RefreshInvokeParametersFromSelectedCommand()
    {
        if (IsSystemOrJudgeOperationSelected || IsLuaOperationSelected)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(EditingProtocolName) ||
            string.IsNullOrWhiteSpace(EditingCommandName))
        {
            EditingInvokeParameters.Clear();
            SelectedEditingInvokeParameter = null;
            return;
        }

        IReadOnlyList<ProtocolPlaceholderSelectionItem> placeholders =
            LoadProtocolCommandPlaceholders(EditingProtocolName, EditingCommandName);

        Dictionary<string, WorkStepOperationParameter> existingByPlaceholder = EditingInvokeParameters
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Description))
            .GroupBy(parameter => parameter.Description.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        string? previousSelectedId = SelectedEditingInvokeParameter?.Id;
        EditingInvokeParameters.Clear();
        int sequence = 1;
        foreach (ProtocolPlaceholderSelectionItem placeholder in placeholders)
        {
            if (string.IsNullOrWhiteSpace(placeholder.Name))
            {
                continue;
            }

            WorkStepOperationParameter parameter;
            if (existingByPlaceholder.TryGetValue(placeholder.Name, out WorkStepOperationParameter? existing))
            {
                parameter = existing.Clone();
                parameter.ParameterName = placeholder.Name;
                parameter.Description = placeholder.Name;
                if (parameter.Sequence <= 0)
                {
                    parameter.Sequence = sequence;
                }
            }
            else
            {
                parameter = new WorkStepOperationParameter
                {
                    Sequence = sequence,
                    Name = ParameterTypeOptions.FirstOrDefault() ?? "设置值",
                    ParameterName = placeholder.Name,
                    Value = placeholder.Value,
                    Remark = placeholder.Name
                };
            }

            EditingInvokeParameters.Add(parameter);
            sequence++;
        }

        NormalizeInvokeParameterSequences();
        SortInvokeParametersBySequence();
        SelectedEditingInvokeParameter = EditingInvokeParameters
            .FirstOrDefault(parameter => string.Equals(parameter.Id, previousSelectedId, StringComparison.OrdinalIgnoreCase))
            ?? EditingInvokeParameters.FirstOrDefault();
        RefreshReturnValueOptions();
        ApplyDefaultReturnValueKey();
    }

    /// <summary>
    /// 根据当前系统方法刷新参数列表。
    /// </summary>
    private void RefreshInvokeParametersFromSelectedSystemMethod(bool clearWhenNoMetadata)
    {
        if (!IsSystemOperationSelected)
        {
            return;
        }

        SystemMethodSelectionItem? method = FindSystemMethodByName(EditingInvokeMethod);
        if (method is null)
        {
            if (clearWhenNoMetadata)
            {
                EditingInvokeParameters.Clear();
                SelectedEditingInvokeParameter = null;
            }

            return;
        }

        EditingInvokeParameters.Clear();
        int sequence = 1;
        foreach (SystemMethodParameterSelectionItem parameterMetadata in method.Parameters)
        {
            EditingInvokeParameters.Add(new WorkStepOperationParameter
            {
                Sequence = sequence,
                Name = ParameterTypeOptions.FirstOrDefault() ?? "设置值",
                ParameterName = parameterMetadata.Name,
                ValueType = parameterMetadata.Type,
                Value = parameterMetadata.DefaultValue,
                Remark = parameterMetadata.Description
            });
            sequence++;
        }

        NormalizeInvokeParameterSequences();
        SortInvokeParametersBySequence();
        SelectedEditingInvokeParameter = EditingInvokeParameters.FirstOrDefault();
    }

    /// <summary>
    /// 根据当前判断方法刷新参数列表。
    /// </summary>
    private void RefreshInvokeParametersFromSelectedJudgeMethod(bool clearWhenNoMetadata)
    {
        if (!IsJudgeOperationSelected)
        {
            return;
        }

        SystemMethodSelectionItem? method = FindJudgeMethodByName(EditingInvokeMethod);
        if (method is null)
        {
            if (clearWhenNoMetadata)
            {
                EditingInvokeParameters.Clear();
                SelectedEditingInvokeParameter = null;
            }

            return;
        }

        EditingInvokeParameters.Clear();
        int sequence = 1;
        foreach (SystemMethodParameterSelectionItem parameterMetadata in method.Parameters)
        {
            EditingInvokeParameters.Add(new WorkStepOperationParameter
            {
                Sequence = sequence,
                Name = ParameterTypeOptions.FirstOrDefault() ?? "设置值",
                ParameterName = parameterMetadata.Name,
                ValueType = parameterMetadata.Type,
                Value = parameterMetadata.DefaultValue,
                Remark = parameterMetadata.Description
            });
            sequence++;
        }

        NormalizeInvokeParameterSequences();
        SortInvokeParametersBySequence();
        SelectedEditingInvokeParameter = EditingInvokeParameters.FirstOrDefault();
    }

    /// <summary>
    /// 将系统方法摘要同步到方法说明编辑项。
    /// </summary>
    private void SyncSystemInvokeMethodRemarkFromMethod()
    {
        SystemMethodSelectionItem? method = FindSystemMethodByName(EditingInvokeMethod);
        string remark = method?.Summary ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(remark) &&
            !InvokeMethodRemarkOptions.Any(option => string.Equals(option, remark, StringComparison.OrdinalIgnoreCase)))
        {
            InvokeMethodRemarkOptions.Add(remark);
        }

        _isSyncingSystemInvokeMethodSelection = true;
        try
        {
            EditingInvokeMethodRemark = remark;
        }
        finally
        {
            _isSyncingSystemInvokeMethodSelection = false;
        }
    }

    /// <summary>
    /// 将判断方法摘要同步到方法说明编辑项。
    /// </summary>
    private void SyncJudgeInvokeMethodRemarkFromMethod()
    {
        SystemMethodSelectionItem? method = FindJudgeMethodByName(EditingInvokeMethod);
        string remark = method?.Summary ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(remark) &&
            !InvokeMethodRemarkOptions.Any(option => string.Equals(option, remark, StringComparison.OrdinalIgnoreCase)))
        {
            InvokeMethodRemarkOptions.Add(remark);
        }

        _isSyncingSystemInvokeMethodSelection = true;
        try
        {
            EditingInvokeMethodRemark = remark;
        }
        finally
        {
            _isSyncingSystemInvokeMethodSelection = false;
        }
    }

    /// <summary>
    /// 根据系统方法说明反向匹配方法名称。
    /// </summary>
    private void SyncSystemInvokeMethodFromRemark()
    {
        if (string.IsNullOrWhiteSpace(EditingInvokeMethodRemark))
        {
            return;
        }

        SystemMethodSelectionItem? method = LoadSystemMethodSelectionItems()
            .FirstOrDefault(item => TextEquals(item.Summary, EditingInvokeMethodRemark));
        if (method is null)
        {
            return;
        }

        if (!InvokeMethodOptions.Any(option => string.Equals(option, method.Name, StringComparison.OrdinalIgnoreCase)))
        {
            InvokeMethodOptions.Add(method.Name);
        }

        _isSyncingSystemInvokeMethodSelection = true;
        try
        {
            EditingInvokeMethod = method.Name;
        }
        finally
        {
            _isSyncingSystemInvokeMethodSelection = false;
        }
    }

    /// <summary>
    /// 根据判断方法说明反向匹配方法名称。
    /// </summary>
    private void SyncJudgeInvokeMethodFromRemark()
    {
        if (string.IsNullOrWhiteSpace(EditingInvokeMethodRemark))
        {
            return;
        }

        SystemMethodSelectionItem? method = LoadJudgeMethodSelectionItems()
            .FirstOrDefault(item => TextEquals(item.Summary, EditingInvokeMethodRemark));
        if (method is null)
        {
            return;
        }

        if (!InvokeMethodOptions.Any(option => string.Equals(option, method.Name, StringComparison.OrdinalIgnoreCase)))
        {
            InvokeMethodOptions.Add(method.Name);
        }

        _isSyncingSystemInvokeMethodSelection = true;
        try
        {
            EditingInvokeMethod = method.Name;
        }
        finally
        {
            _isSyncingSystemInvokeMethodSelection = false;
        }
    }

    /// <summary>
    /// 按当前操作对象刷新可选调用方法及说明列表。
    /// </summary>
    private void RefreshInvokeMethodOptions(bool updateStatus)
    {
        string previousSelection = null;
        bool hasPreviousSelection = false;
        if (IsJudgeOperationSelected)
        {
            previousSelection = EditingInvokeMethod;
            InvokeMethodOptions.Clear();
            InvokeMethodRemarkOptions.Clear();
            IReadOnlyList<SystemMethodSelectionItem> judgeMethods = LoadJudgeMethodSelectionItems();

            foreach (string option in judgeMethods
                         .Select(method => method.Name)
                         .Where(option => !string.IsNullOrWhiteSpace(option))
                         .Select(option => option.Trim())
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                InvokeMethodOptions.Add(option);
            }

            foreach (string option in judgeMethods
                         .Select(method => method.Summary)
                         .Where(option => !string.IsNullOrWhiteSpace(option))
                         .Select(option => option.Trim())
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                InvokeMethodRemarkOptions.Add(option);
            }

            hasPreviousSelection =
                !string.IsNullOrWhiteSpace(previousSelection) &&
                InvokeMethodOptions.Any(option => string.Equals(option, previousSelection, StringComparison.OrdinalIgnoreCase));

            if (InvokeMethodOptions.Count == 0)
            {
                EditingInvokeMethod = string.Empty;
            }
            else if (hasPreviousSelection)
            {
                EditingInvokeMethod = previousSelection.Trim();
            }
            else
            {
                EditingInvokeMethod = InvokeMethodOptions.First();
            }

            SyncJudgeInvokeMethodRemarkFromMethod();
            if (!_isInitializingOperationDrawer)
            {
                RefreshInvokeParametersFromSelectedJudgeMethod(clearWhenNoMetadata: true);
            }

            if (updateStatus)
            {
                SetPageStatus($"已按“{EditingOperationObject}”刷新调用方法。", SuccessBrush);
            }

            return;
        }

        if (IsLuaOperationSelected)
        {
            InvokeMethodOptions.Clear();
            InvokeMethodRemarkOptions.Clear();
            EditingInvokeMethodRemark = string.Empty;
            EditingInvokeMethod = LuaOperationObjectName;
            EditingInvokeParameters.Clear();
            SelectedEditingInvokeParameter = null;
            return;
        }

        if (!IsSystemOperationSelected)
        {
            previousSelection = EditingInvokeMethod;
            InvokeMethodOptions.Clear();
            InvokeMethodRemarkOptions.Clear();
            foreach (string option in LoadDeviceInvokeMethodOptions(EditingOperationObject)
                         .Where(option => !string.IsNullOrWhiteSpace(option))
                         .Select(option => option.Trim())
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                InvokeMethodOptions.Add(option);
            }

            _isSyncingSystemInvokeMethodSelection = true;
            try
            {
                EditingInvokeMethodRemark = string.Empty;
            }
            finally
            {
                _isSyncingSystemInvokeMethodSelection = false;
            }

            if (InvokeMethodOptions.Count == 0)
            {
                EditingInvokeMethod = EditingCommandName;
            }
            else if (!string.IsNullOrWhiteSpace(previousSelection) &&
                     InvokeMethodOptions.Any(option => string.Equals(option, previousSelection, StringComparison.OrdinalIgnoreCase)))
            {
                EditingInvokeMethod = previousSelection.Trim();
            }
            else
            {
                EditingInvokeMethod = InvokeMethodOptions.First();
            }

            BusinessOperationDescriptor? businessOperation = BusinessOperationBindingResolver.FindOperationForOperationObject(
                EditingOperationObject,
                _drawerOperation?.DeviceId,
                EditingInvokeMethod);
            if (businessOperation is not null)
            {
                EditingInvokeParameters.Clear();
                foreach (WorkStepOperationParameter parameter in CreateOperationParametersFromBusinessOperation(businessOperation))
                {
                    EditingInvokeParameters.Add(parameter);
                }
                SelectedEditingInvokeParameter = EditingInvokeParameters.FirstOrDefault();
            }

            return;
        }

        previousSelection = EditingInvokeMethod;
        InvokeMethodOptions.Clear();
        InvokeMethodRemarkOptions.Clear();
        IReadOnlyList<SystemMethodSelectionItem> systemMethods = LoadSystemMethodSelectionItems();

        foreach (string option in systemMethods
                     .Select(method => method.Name)
                     .DefaultIfEmpty()
                     .Where(option => !string.IsNullOrWhiteSpace(option))
                     .Select(option => option!.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            InvokeMethodOptions.Add(option);
        }

        foreach (string option in systemMethods
                     .Select(method => method.Summary)
                     .Where(option => !string.IsNullOrWhiteSpace(option))
                     .Select(option => option!.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            InvokeMethodRemarkOptions.Add(option);
        }

        hasPreviousSelection =
            !string.IsNullOrWhiteSpace(previousSelection) &&
            InvokeMethodOptions.Any(option => string.Equals(option, previousSelection, StringComparison.OrdinalIgnoreCase));

        //if (!hasPreviousSelection &&
        //    !IsPlaceholderInvokeMethod(previousSelection) &&
        //    !string.IsNullOrWhiteSpace(previousSelection))
        //{
        //    InvokeMethodOptions.Add(previousSelection.Trim());
        //    hasPreviousSelection = true;
        //}

        if (InvokeMethodOptions.Count == 0)
        {
            EditingInvokeMethod = IsPlaceholderInvokeMethod(previousSelection)
                ? string.Empty
                : previousSelection;
        }
        else if (hasPreviousSelection && !IsPlaceholderInvokeMethod(previousSelection))
        {
            EditingInvokeMethod = previousSelection.Trim();
        }
        else
        {
            EditingInvokeMethod = InvokeMethodOptions.First();
        }

        SyncSystemInvokeMethodRemarkFromMethod();
        if (!_isInitializingOperationDrawer)
        {
            RefreshInvokeParametersFromSelectedSystemMethod(clearWhenNoMetadata: true);
        }

        if (updateStatus)
        {
            SetPageStatus($"已按“{EditingOperationObject}”刷新调用方法。", SuccessBrush);
        }
    }

    /// <summary>
    /// 按名称查找系统方法定义。
    /// </summary>
    private static SystemMethodSelectionItem? FindSystemMethodByName(string methodName)
    {
        if (string.IsNullOrWhiteSpace(methodName))
        {
            return null;
        }

        return LoadSystemMethodSelectionItems()
            .FirstOrDefault(method => TextEquals(method.Name, methodName));
    }

    /// <summary>
    /// 按名称查找判断方法定义。
    /// </summary>
    private static SystemMethodSelectionItem? FindJudgeMethodByName(string methodName)
    {
        if (string.IsNullOrWhiteSpace(methodName))
        {
            return null;
        }

        return LoadJudgeMethodSelectionItems()
            .FirstOrDefault(method => TextEquals(method.Name, methodName));
    }

    /// <summary>
    /// 加载内置判断方法的选择项定义。
    /// </summary>
    private static IReadOnlyList<SystemMethodSelectionItem> LoadJudgeMethodSelectionItems()
    {
        return new[]
        {
            CreateJudgeMethod(
                "等于判断",
                "判断两个值是否相等",
                ("左值", "左侧待比较的值"),
                ("右值", "右侧待比较的值")),
            CreateJudgeMethod(
                "不等判断",
                "判断两个值是否不相等",
                ("左值", "左侧待比较的值"),
                ("右值", "右侧待比较的值")),
            CreateJudgeMethod(
                "大于判断",
                "判断左值是否大于右值",
                ("左值", "左侧待比较的值"),
                ("右值", "右侧待比较的值")),
            CreateJudgeMethod(
                "大于等于判断",
                "判断左值是否大于等于右值",
                ("左值", "左侧待比较的值"),
                ("右值", "右侧待比较的值")),
            CreateJudgeMethod(
                "小于判断",
                "判断左值是否小于右值",
                ("左值", "左侧待比较的值"),
                ("右值", "右侧待比较的值")),
            CreateJudgeMethod(
                "小于等于判断",
                "判断左值是否小于等于右值",
                ("左值", "左侧待比较的值"),
                ("右值", "右侧待比较的值")),
            CreateJudgeMethod(
                "包含判断",
                "判断文本是否包含指定关键字",
                ("待判断值", "待检查的文本"),
                ("关键字", "用于匹配的关键字")),
            CreateJudgeMethod(
                "不包含判断",
                "判断文本是否不包含指定关键字",
                ("待判断值", "待检查的文本"),
                ("关键字", "用于匹配的关键字")),
            CreateJudgeMethod(
                "为空判断",
                "判断指定值是否为空",
                ("待判断值", "待检查的值")),
            CreateJudgeMethod(
                "不为空判断",
                "判断指定值是否不为空",
                ("待判断值", "待检查的值"))
        };
    }

    /// <summary>
    /// 创建单个判断方法的元数据定义。
    /// </summary>
    private static SystemMethodSelectionItem CreateJudgeMethod(
        string name,
        string summary,
        params (string Name, string Description)[] parameters)
    {
        return new SystemMethodSelectionItem(
            name,
            summary,
            parameters.Select(parameter => new SystemMethodParameterSelectionItem(
                parameter.Name,
                string.Empty,
                parameter.Description)));
    }

    /// <summary>
    /// 加载系统方法的选择项定义。
    /// </summary>
    private static IReadOnlyList<SystemMethodSelectionItem> LoadSystemMethodSelectionItems()
    {
        return LoadBusinessMethodSelectionItems(SystemOperationObjectName);
    }

    /// <summary>
    /// 将业务目录中的方法描述映射成系统方法选择项模型。
    /// </summary>
    private static IReadOnlyList<SystemMethodSelectionItem> LoadBusinessMethodSelectionItems(string deviceId)
    {
        return BusinessOperationCatalog.GetOperations(deviceId)
            .Select(operation => new SystemMethodSelectionItem(
                operation.OperationId,
                string.IsNullOrWhiteSpace(operation.Description) ? operation.DisplayName : operation.Description,
                operation.Parameters.Select(parameter => new SystemMethodParameterSelectionItem(
                    parameter.Name,
                    parameter.TypeName,
                    string.IsNullOrWhiteSpace(parameter.Description) ? parameter.DisplayName : parameter.Description,
                    parameter.DefaultValue))))
            .ToArray();
    }

    /// <summary>
    /// 枚举系统方法源码的候选文件路径。
    /// </summary>
    private static IEnumerable<string> GetSystemMethodSourceFileCandidates()
    {
        HashSet<string> seenPaths = new(StringComparer.OrdinalIgnoreCase);
        foreach (string root in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() }
                     .Where(root => !string.IsNullOrWhiteSpace(root)))
        {
            DirectoryInfo? directory = new(root);
            while (directory is not null)
            {
                foreach (string relativePath in new[]
                         {
                             Path.Combine("Business", "System.cs"),
                             Path.Combine("Business", "System"),
                             Path.Combine("Module.Business", "Business", "System.cs"),
                             Path.Combine("Module.Business", "Business", "System")
                         })
                {
                    string candidate = Path.Combine(directory.FullName, relativePath);
                    if (seenPaths.Add(candidate))
                    {
                        yield return candidate;
                    }
                }

                directory = directory.Parent;
            }
        }
    }

    /// <summary>
    /// 从源码文本中解析系统方法列表。
    /// </summary>
    private static IReadOnlyList<SystemMethodSelectionItem> ParseSystemMethodSelectionItems(string sourceText)
    {
        List<SystemMethodSelectionItem> methods = new();
        List<string> documentationLines = new();
        string[] lines = (sourceText ?? string.Empty)
            .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            string trimmedLine = line.TrimStart();
            if (trimmedLine.StartsWith("///", StringComparison.Ordinal))
            {
                documentationLines.Add(line);
                continue;
            }

            if (string.IsNullOrWhiteSpace(trimmedLine) ||
                trimmedLine.StartsWith("[", StringComparison.Ordinal))
            {
                continue;
            }

            if (!TryReadSystemMethodSignature(lines, ref index, out Match match))
            {
                documentationLines.Clear();
                continue;
            }

            string methodName = match.Groups["name"].Value.Trim();
            if (string.IsNullOrWhiteSpace(methodName) || methodName.Contains('<', StringComparison.Ordinal))
            {
                documentationLines.Clear();
                continue;
            }

            (string summary, Dictionary<string, string> parameterDescriptions) =
                ParseSystemMethodDocumentation(string.Join(Environment.NewLine, documentationLines));
            IReadOnlyList<SystemMethodParameterSelectionItem> parameters =
                ParseSystemMethodParameters(match.Groups["parameters"].Value, parameterDescriptions);

            methods.Add(new SystemMethodSelectionItem(methodName, summary, parameters));
            documentationLines.Clear();
        }

        return methods;
    }

    /// <summary>
    /// 从当前行开始读取完整的方法签名文本。
    /// </summary>
    private static bool TryReadSystemMethodSignature(string[] lines, ref int index, out Match match)
    {
        StringBuilder signatureBuilder = new(lines[index].Trim());
        int parenthesisDepth = CountParenthesisDepth(signatureBuilder.ToString());
        while (parenthesisDepth > 0 && index + 1 < lines.Length)
        {
            index++;
            string nextLine = lines[index].Trim();
            signatureBuilder.Append(' ').Append(nextLine);
            parenthesisDepth += CountParenthesisDepth(nextLine);
        }

        string signature = signatureBuilder.ToString();
        int closeParenthesisIndex = signature.IndexOf(')');
        if (closeParenthesisIndex >= 0)
        {
            signature = signature[..(closeParenthesisIndex + 1)];
        }

        match = SystemMethodSignatureRegex.Match(signature);
        return match.Success;
    }

    /// <summary>
    /// 统计文本中圆括号的深度变化。
    /// </summary>
    private static int CountParenthesisDepth(string text)
    {
        int depth = 0;
        foreach (char value in text)
        {
            if (value == '(')
            {
                depth++;
            }
            else if (value == ')')
            {
                depth--;
            }
        }

        return depth;
    }

    /// <summary>
    /// 解析系统方法 XML 文档摘要和参数说明。
    /// </summary>
    private static (string Summary, Dictionary<string, string> ParameterDescriptions) ParseSystemMethodDocumentation(string documentationText)
    {
        string xmlText = string.Join(
            Environment.NewLine,
            (documentationText ?? string.Empty)
                .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)
                .Select(line => Regex.Replace(line, @"^\s*///\s?", string.Empty)));

        try
        {
            XElement document = XElement.Parse($"<doc>{xmlText}</doc>");
            string summary = NormalizeDocumentationText(document.Element("summary")?.Value);
            Dictionary<string, string> parameterDescriptions = document
                .Elements("param")
                .Where(element => element.Attribute("name") is not null)
                .GroupBy(
                    element => element.Attribute("name")!.Value.Trim(),
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => NormalizeDocumentationText(group.First().Value),
                    StringComparer.OrdinalIgnoreCase);

            return (summary, parameterDescriptions);
        }
        catch
        {
            return ParseSystemMethodDocumentationFallback(xmlText);
        }
    }

    /// <summary>
    /// XML 解析失败时用正则回退解析方法文档。
    /// </summary>
    private static (string Summary, Dictionary<string, string> ParameterDescriptions) ParseSystemMethodDocumentationFallback(string xmlText)
    {
        string summary = NormalizeDocumentationText(
            Regex.Match(xmlText ?? string.Empty, @"<summary>(?<value>.*?)</summary>", RegexOptions.Singleline)
                .Groups["value"]
                .Value);

        Dictionary<string, string> parameterDescriptions = new(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Regex.Matches(
                     xmlText ?? string.Empty,
                     @"<param\s+name=""(?<name>[^""]+)"">(?<value>.*?)</param>",
                     RegexOptions.Singleline))
        {
            string name = match.Groups["name"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                parameterDescriptions[name] = NormalizeDocumentationText(match.Groups["value"].Value);
            }
        }

        return (summary, parameterDescriptions);
    }

    /// <summary>
    /// 解析系统方法签名中的参数列表。
    /// </summary>
    private static IReadOnlyList<SystemMethodParameterSelectionItem> ParseSystemMethodParameters(
        string parameterText,
        IReadOnlyDictionary<string, string> parameterDescriptions)
    {
        List<SystemMethodParameterSelectionItem> parameters = new();
        foreach (string rawParameter in SplitSystemMethodParameters(parameterText))
        {
            string parameter = rawParameter.Trim();
            if (string.IsNullOrWhiteSpace(parameter))
            {
                continue;
            }

            int defaultValueIndex = parameter.IndexOf('=');
            if (defaultValueIndex >= 0)
            {
                parameter = parameter[..defaultValueIndex].Trim();
            }

            string[] parts = Regex.Replace(parameter, @"\s+", " ")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            string name = parts[^1].Trim().TrimStart('@');
            string type = string.Join(
                " ",
                parts
                    .Take(parts.Length - 1)
                    .Where(part => !IsParameterModifier(part)));
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(type))
            {
                continue;
            }

            string description = parameterDescriptions.TryGetValue(name, out string? parameterDescription) &&
                                 !string.IsNullOrWhiteSpace(parameterDescription)
                ? parameterDescription
                : name;
            parameters.Add(new SystemMethodParameterSelectionItem(name, type, description));
        }

        return parameters;
    }

    /// <summary>
    /// 按逗号拆分方法参数文本，同时处理泛型嵌套场景。
    /// </summary>
    private static IEnumerable<string> SplitSystemMethodParameters(string parameterText)
    {
        if (string.IsNullOrWhiteSpace(parameterText))
        {
            yield break;
        }

        int genericDepth = 0;
        int startIndex = 0;
        for (int index = 0; index < parameterText.Length; index++)
        {
            char current = parameterText[index];
            if (current == '<')
            {
                genericDepth++;
            }
            else if (current == '>')
            {
                genericDepth = Math.Max(0, genericDepth - 1);
            }
            else if (current == ',' && genericDepth == 0)
            {
                yield return parameterText[startIndex..index];
                startIndex = index + 1;
            }
        }

        yield return parameterText[startIndex..];
    }

    /// <summary>
    /// 判断标记是否属于参数修饰符。
    /// </summary>
    private static bool IsParameterModifier(string value)
    {
        return value is "ref" or "out" or "in" or "params" or "this";
    }

    /// <summary>
    /// 规范化文档文本，去除标签和多余空白。
    /// </summary>
    private static string NormalizeDocumentationText(string? value)
    {
        string text = Regex.Replace(value ?? string.Empty, "<.*?>", string.Empty);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    /// <summary>
    /// 以忽略大小写和首尾空白的方式比较文本。
    /// </summary>
    private static bool TextEquals(string? left, string? right)
    {
        return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 读取设备操作对象下拉项；当前只返回通信配置中的设备名称。
    /// </summary>
    private static IEnumerable<string> LoadDeviceOperationObjectOptions()
    {
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
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(filePath));
                if (document.RootElement.TryGetProperty("LocalName", out JsonElement localNameElement))
                {
                    string? localName = localNameElement.GetString();
                    if (!string.IsNullOrWhiteSpace(localName))
                    {
                        names.Add(localName.Trim());
                    }
                }
            }
            catch
            {
                // 忽略损坏或非通信配置 JSON，刷新下拉时不阻断编辑流程。
            }
        }

        return names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 读取指定设备在通信配置中声明的支持协议名称。
    /// </summary>
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
                    string protocolName = GetJsonString(protocolElement, "ProtocolName");
                    if (!string.IsNullOrWhiteSpace(protocolName))
                    {
                        names.Add(protocolName.Trim());
                    }
                }

                break;
            }
            catch
            {
                // 忽略损坏或非通信配置 JSON，避免阻断步骤编辑。
            }
        }

        return names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 加载全部协议名称选项。
    /// </summary>
    private static IEnumerable<string> LoadProtocolOptions()
    {
        return LoadProtocolSelectionItems()
            .Select(item => item.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 加载指定协议下的全部指令名称。
    /// </summary>
    private static IEnumerable<string> LoadProtocolCommandOptions(string protocolName)
    {
        return LoadProtocolSelectionItems()
            .Where(item => string.Equals(item.Name, protocolName?.Trim(), StringComparison.OrdinalIgnoreCase))
            .SelectMany(item => item.Commands.Select(command => command.Name))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 加载指定协议指令的占位符定义。
    /// </summary>
    private static IReadOnlyList<ProtocolPlaceholderSelectionItem> LoadProtocolCommandPlaceholders(
        string protocolName,
        string commandName)
    {
        if (string.IsNullOrWhiteSpace(protocolName) || string.IsNullOrWhiteSpace(commandName))
        {
            return Array.Empty<ProtocolPlaceholderSelectionItem>();
        }

        ProtocolCommandSelectionItem? command = LoadProtocolSelectionItems()
            .Where(item => string.Equals(item.Name, protocolName.Trim(), StringComparison.OrdinalIgnoreCase))
            .SelectMany(item => item.Commands)
            .FirstOrDefault(command => string.Equals(command.Name, commandName.Trim(), StringComparison.OrdinalIgnoreCase));

        return command is null
            ? Array.Empty<ProtocolPlaceholderSelectionItem>()
            : command.Placeholders;
    }

    /// <summary>
    /// 加载指定协议指令支持的返回值键。
    /// </summary>
    private static IReadOnlyList<string> LoadProtocolCommandReturnValueKeys(
        string protocolName,
        string commandName)
    {
        if (string.IsNullOrWhiteSpace(protocolName) || string.IsNullOrWhiteSpace(commandName))
        {
            return Array.Empty<string>();
        }

        ProtocolCommandSelectionItem? command = LoadProtocolSelectionItems()
            .Where(item => string.Equals(item.Name, protocolName.Trim(), StringComparison.OrdinalIgnoreCase))
            .SelectMany(item => item.Commands)
            .FirstOrDefault(command => string.Equals(command.Name, commandName.Trim(), StringComparison.OrdinalIgnoreCase));

        return command is null
            ? Array.Empty<string>()
            : command.ReturnValueKeys;
    }

    /// <summary>
    /// 推导协议指令默认的返回值键。
    /// </summary>
    private static string ResolveDefaultProtocolCommandReturnValueKey(
        string protocolName,
        string commandName)
    {
        IReadOnlyList<string> keys = LoadProtocolCommandReturnValueKeys(protocolName, commandName);
        return keys.Count == 1 ? keys[0] : string.Empty;
    }

    /// <summary>
    /// 从协议配置目录加载协议及指令定义。
    /// </summary>
    private static IEnumerable<ProtocolSelectionItem> LoadProtocolSelectionItems()
    {
        string protocolConfigDirectory = Path.Combine(AppContext.BaseDirectory, "Config", "Protocol");
        if (!Directory.Exists(protocolConfigDirectory))
        {
            return Enumerable.Empty<ProtocolSelectionItem>();
        }

        List<ProtocolSelectionItem> items = new();
        foreach (string filePath in Directory.EnumerateFiles(protocolConfigDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                string storageText = File.ReadAllText(filePath, Encoding.UTF8);
                string json = TryReadProtocolJson(storageText);
                using JsonDocument document = JsonDocument.Parse(json);

                if (!document.RootElement.TryGetProperty("Name", out JsonElement nameElement))
                {
                    continue;
                }

                string? protocolName = nameElement.GetString();
                if (string.IsNullOrWhiteSpace(protocolName))
                {
                    continue;
                }

                List<ProtocolCommandSelectionItem> commands = new();
                if (document.RootElement.TryGetProperty("Commands", out JsonElement commandsElement) &&
                    commandsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement commandElement in commandsElement.EnumerateArray())
                    {
                        if (commandElement.TryGetProperty("Name", out JsonElement commandNameElement) &&
                            !string.IsNullOrWhiteSpace(commandNameElement.GetString()))
                        {
                            string commandName = commandNameElement.GetString()!.Trim();
                            string contentTemplate = GetJsonString(commandElement, "ContentTemplate");
                            string placeholderValuesText = GetJsonString(commandElement, "PlaceholderValuesText");
                            commands.Add(new ProtocolCommandSelectionItem(
                                commandName,
                                BuildProtocolPlaceholderSelectionItems(contentTemplate, placeholderValuesText),
                                GetJsonStringArray(commandElement, "ParsedResultKeys")));
                        }
                    }
                }

                if (commands.Count == 0)
                {
                    commands.Add(new ProtocolCommandSelectionItem(
                        "指令 1",
                        BuildProtocolPlaceholderSelectionItems(
                            GetJsonString(document.RootElement, "ContentTemplate"),
                            GetJsonString(document.RootElement, "PlaceholderValuesText")),
                        GetJsonStringArray(document.RootElement, "ParsedResultKeys")));
                }

                items.Add(new ProtocolSelectionItem(protocolName.Trim(), commands));
            }
            catch
            {
                // 忽略损坏或无法解密的协议配置，避免阻断工步编辑。
            }
        }

        return items;
    }

    /// <summary>
    /// 读取协议配置内容，兼容加密和明文两种格式。
    /// </summary>
    private static string TryReadProtocolJson(string storageText)
    {
        try
        {
            return storageText.DesDecrypt();
        }
        catch
        {
            return storageText;
        }
    }

    /// <summary>
    /// 从 JSON 元素中安全读取字符串属性。
    /// </summary>
    private static string GetJsonString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement propertyElement)
            ? propertyElement.GetString() ?? string.Empty
            : string.Empty;
    }

    /// <summary>
    /// 从 JSON 元素中安全读取字符串数组属性。
    /// </summary>
    private static IReadOnlyList<string> GetJsonStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement propertyElement) ||
            propertyElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return propertyElement
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// 根据模板文本和默认值构建占位符选择项。
    /// </summary>
    private static IReadOnlyList<ProtocolPlaceholderSelectionItem> BuildProtocolPlaceholderSelectionItems(
        string contentTemplate,
        string placeholderValuesText)
    {
        Dictionary<string, string> valuesByName = ParseProtocolPlaceholderValues(placeholderValuesText);
        List<ProtocolPlaceholderSelectionItem> placeholders = new();
        foreach (string placeholderName in ExtractProtocolPlaceholderNames(contentTemplate))
        {
            valuesByName.TryGetValue(placeholderName, out string? value);
            placeholders.Add(new ProtocolPlaceholderSelectionItem(placeholderName, value ?? string.Empty));
        }

        return placeholders;
    }

    /// <summary>
    /// 从协议模板中提取占位符名称。
    /// </summary>
    private static IEnumerable<string> ExtractProtocolPlaceholderNames(string contentTemplate)
    {
        HashSet<string> seenNames = new(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in ProtocolPlaceholderRegex.Matches(contentTemplate ?? string.Empty))
        {
            string placeholderName = match.Groups["name"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(placeholderName) && seenNames.Add(placeholderName))
            {
                yield return placeholderName;
            }
        }
    }

    /// <summary>
    /// 解析占位符默认值配置文本。
    /// </summary>
    private static Dictionary<string, string> ParseProtocolPlaceholderValues(string placeholderValuesText)
    {
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
        string[] lines = (placeholderValuesText ?? string.Empty)
            .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

        foreach (string rawLine in lines)
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

            string key = line[..equalsIndex].Trim();
            string value = line[(equalsIndex + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(key))
            {
                values[key] = value;
            }
        }

        return values;
    }

    /// <summary>
    /// 确保指定操作对象存在于当前下拉选项中。
    /// </summary>
    private void EnsureOperationObjectOption(string operationObject)
    {
        RefreshOperationObjectOptions(updateStatus: false);
        if (!string.IsNullOrWhiteSpace(operationObject) &&
            !OperationObjectOptions.Any(option => string.Equals(option, operationObject, StringComparison.OrdinalIgnoreCase)))
        {
            OperationObjectOptions.Add(operationObject.Trim());
        }
    }

    /// <summary>
    /// 确保指定协议名存在于当前协议选项中，并同步编辑值。
    /// </summary>
    private void EnsureProtocolOption(string protocolName)
    {
        RefreshProtocolOptions(updateStatus: false);
        if (!string.IsNullOrWhiteSpace(protocolName) &&
            !ProtocolOptions.Any(option => string.Equals(option, protocolName, StringComparison.OrdinalIgnoreCase)))
        {
            ProtocolOptions.Add(protocolName.Trim());
        }

        if (!string.IsNullOrWhiteSpace(protocolName))
        {
            EditingProtocolName = protocolName.Trim();
        }
    }

    /// <summary>
    /// 确保指定指令名存在于当前指令选项中，并同步编辑值。
    /// </summary>
    private void EnsureCommandOption(string commandName)
    {
        RefreshCommandOptions(updateStatus: false);
        if (!string.IsNullOrWhiteSpace(commandName) &&
            !CommandOptions.Any(option => string.Equals(option, commandName, StringComparison.OrdinalIgnoreCase)))
        {
            CommandOptions.Add(commandName.Trim());
        }

        if (!string.IsNullOrWhiteSpace(commandName))
        {
            EditingCommandName = commandName.Trim();
        }
    }

    /// <summary>
    /// 解析步骤在编辑态下应显示的操作对象名称。
    /// </summary>
    private static string ResolveOperationObjectForEditing(WorkStepOperation operation)
    {
        if (IsLuaOperationObject(operation.OperationType) ||
            IsLuaOperationObject(operation.OperationObject))
        {
            return LuaOperationObjectName;
        }

        if (IsJudgeOperationObject(operation.OperationType) ||
            IsJudgeOperationObject(operation.OperationObject))
        {
            return JudgeOperationObjectName;
        }

        if (IsLegacySystemOperationType(operation.OperationType) ||
            IsSystemOperationObject(operation.OperationObject))
        {
            return SystemOperationObjectName;
        }

        return string.IsNullOrWhiteSpace(operation.OperationObject)
            ? SystemOperationObjectName
            : operation.OperationObject.Trim();
    }

    /// <summary>
    /// 判断操作类型是否为旧版系统类型标记。
    /// </summary>
    private static bool IsLegacySystemOperationType(string? operationType)
    {
        return string.Equals(operationType?.Trim(), "系统", StringComparison.OrdinalIgnoreCase);
    }

    internal const string SystemOperationObjectName = "System";

    internal const string JudgeOperationObjectName = "判断";

    internal const string LuaOperationObjectName = "Lua";

    /// <summary>
    /// 判断操作对象是否为系统对象。
    /// </summary>
    internal static bool IsSystemOperationObject(string? operationObject)
    {
        return string.Equals(operationObject?.Trim(), SystemOperationObjectName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(operationObject?.Trim(), "系统", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断操作对象是否为判断对象。
    /// </summary>
    internal static bool IsJudgeOperationObject(string? operationObject)
    {
        return string.Equals(operationObject?.Trim(), JudgeOperationObjectName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断操作对象是否为 Lua 对象。
    /// </summary>
    internal static bool IsLuaOperationObject(string? operationObject)
    {
        return string.Equals(operationObject?.Trim(), LuaOperationObjectName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断调用方法是否仍为占位文本。
    /// </summary>
    private static bool IsPlaceholderInvokeMethod(string? invokeMethod)
    {
        return string.IsNullOrWhiteSpace(invokeMethod) ||
               string.Equals(invokeMethod.Trim(), "调用方法", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class SystemMethodSelectionItem
    {
        /// <summary>
        /// 创建系统方法选择项。
        /// </summary>
        public SystemMethodSelectionItem(
            string name,
            string summary,
            IEnumerable<SystemMethodParameterSelectionItem> parameters)
        {
            Name = name;
            Summary = summary;
            Parameters = parameters.ToList();
        }

        public string Name { get; }

        public string Summary { get; }

        public List<SystemMethodParameterSelectionItem> Parameters { get; }
    }

    private sealed class SystemMethodParameterSelectionItem
    {
        /// <summary>
        /// 创建系统方法参数选择项。
        /// </summary>
        public SystemMethodParameterSelectionItem(string name, string type, string description, string defaultValue = "")
        {
            Name = name;
            Type = type;
            Description = description;
            DefaultValue = defaultValue;
        }

        public string Name { get; }

        public string Type { get; }

        public string Description { get; }

        public string DefaultValue { get; }
    }

    private sealed class ProtocolSelectionItem
    {
        /// <summary>
        /// 创建协议选择项。
        /// </summary>
        public ProtocolSelectionItem(string name, IEnumerable<ProtocolCommandSelectionItem> commands)
        {
            Name = name;
            Commands = commands.ToList();
        }

        public string Name { get; }

        public List<ProtocolCommandSelectionItem> Commands { get; }
    }

    private sealed class ProtocolCommandSelectionItem
    {
        /// <summary>
        /// 创建协议指令选择项。
        /// </summary>
        public ProtocolCommandSelectionItem(
            string name,
            IEnumerable<ProtocolPlaceholderSelectionItem> placeholders,
            IEnumerable<string> returnValueKeys)
        {
            Name = name;
            Placeholders = placeholders.ToList();
            ReturnValueKeys = returnValueKeys
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Select(key => key.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public string Name { get; }

        public List<ProtocolPlaceholderSelectionItem> Placeholders { get; }

        public List<string> ReturnValueKeys { get; }
    }

    private sealed class ProtocolPlaceholderSelectionItem
    {
        /// <summary>
        /// 创建协议占位符选择项。
        /// </summary>
        public ProtocolPlaceholderSelectionItem(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }

        public string Value { get; }
    }

    /// <summary>
    /// 刷新所有页面命令的可执行状态。
    /// </summary>
    private void RaiseCommandStatesChanged()
    {
        RaiseCommandState(DuplicateWorkStepCommand);
        RaiseCommandState(DeleteWorkStepCommand);
        RaiseCommandState(AddOperationCommand);
        RaiseCommandState(CopyOperationCommand);
        RaiseCommandState(PasteOperationCommand);
        RaiseCommandState(DeleteOperationCommand);
        RaiseCommandState(SaveOperationDrawerCommand);
        RaiseCommandState(CloseOperationDrawerCommand);
        RaiseCommandState(RefreshOperationObjectsCommand);
        RaiseCommandState(AddInvokeParameterCommand);
        RaiseCommandState(DeleteInvokeParameterCommand);
    }

    /// <summary>
    /// 如果命令是 <see cref="RelayCommand"/>，则触发其可执行状态刷新。
    /// </summary>
    private static void RaiseCommandState(ICommand? command)
    {
        if (command is RelayCommand relayCommand)
        {
            relayCommand.RaiseCanExecuteChanged();
        }
    }

    #endregion


    /// <summary>
    /// 以独立编辑模式打开单条步骤，不影响宿主页面的数据上下文。
    /// </summary>
    public void BeginStandaloneOperationEdit(
        WorkStepOperation? operation,
        string stepName = "流程图处理块",
        bool isDecisionOperationMode = false)
    {
        _isDecisionOperationMode = isDecisionOperationMode;
        _isStandaloneOperationEditMode = true;
        WorkStepProfile temporaryWorkStep = new()
        {
            StepName = string.IsNullOrWhiteSpace(stepName) ? "流程图处理块" : stepName.Trim(),
            Steps = new ObservableCollection<WorkStepOperation>()
        };

        WorkStepOperation editingOperation = operation?.Clone() ?? CreateDefaultStandaloneOperation();
        if (_isDecisionOperationMode &&
            (operation is null || string.IsNullOrWhiteSpace(operation.OperationObject)))
        {
            editingOperation.OperationObject = JudgeOperationObjectName;
        }

        WorkSteps.Clear();
        WorkSteps.Add(temporaryWorkStep);
        SelectedWorkStep = temporaryWorkStep;
        SelectedOperation = null;

        if (operation is not null)
        {
            temporaryWorkStep.Steps.Add(editingOperation);
            OpenOperationDrawerForEdit(editingOperation);
            return;
        }

        BeginOperationDrawer(editingOperation, isNewOperation: true);
        SetPageStatus(
            _isDecisionOperationMode ? "正在编辑流程图判断块。" : "正在编辑流程图处理块。",
            NeutralBrush);
    }

    /// <summary>
    /// 设置独立编辑模式可引用的外部返回值，例如流程图其它节点的返回值。
    /// </summary>
    public void SetStandaloneReturnValueOptions(IEnumerable<string> returnValues)
    {
        ReplaceStringOptions(ExternalReturnValueOptions, returnValues);
        RefreshParameterValueOptions();
        RefreshReturnValueOptions();
    }

    /// <summary>
    /// 尝试保存当前独立编辑的步骤；若校验失败则保持编辑状态。
    /// </summary>
    public bool TrySaveStandaloneOperationEdit()
    {
        bool wasOpen = IsOperationDrawerOpen;
        SaveOperationDrawer();

        return wasOpen &&
               !IsOperationDrawerOpen &&
               SelectedWorkStep is not null &&
               SelectedWorkStep.Steps.Any();
    }

    /// <summary>
    /// 取消当前独立编辑。
    /// </summary>
    public void CancelStandaloneOperationEdit()
    {
        CloseOperationDrawer();
        SelectedOperation = null;
        SelectedWorkStep = null;
        WorkSteps.Clear();
        _isDecisionOperationMode = false;
        _isStandaloneOperationEditMode = false;
    }

    /// <summary>
    /// 获取独立编辑后的当前步骤快照。
    /// </summary>
    public WorkStepOperation? CreateEditedOperationSnapshot()
    {
        WorkStepOperation? operation = SelectedWorkStep?.Steps.FirstOrDefault() ?? SelectedOperation;
        return operation?.Clone();
    }

    /// <summary>
    /// 创建独立编辑模式下使用的默认步骤模板。
    /// </summary>
    private static WorkStepOperation CreateDefaultStandaloneOperation()
    {
        return new WorkStepOperation
        {
            OperationObject = SystemOperationObjectName,
            DeviceId = SystemOperationObjectName,
            InvokeMethod = string.Empty,
            OperationId = string.Empty,
            ReturnValue = string.Empty,
            ShowDataToView = false,
            ViewDataName = string.Empty,
            ViewJudgeType = string.Empty,
            ViewJudgeCondition = string.Empty,
            DelayMilliseconds = 0,
            Remark = string.Empty
        };
    }
}
