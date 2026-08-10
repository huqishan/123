using ControlLibrary;
using Module.Business.Features.OperationEditing.Models;
using Module.Business.Features.OperationEditing.Services;
using Module.Business.Features.OperationEditing.ViewModels.PresentationModels;
using Module.Business.Features.Scheme.ViewModels.PresentationModels;
using Module.Business.Models;
using Module.Business.Services.BusinessOperations;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Module.Business.Features.OperationEditing.ViewModels;

/// <summary>
/// 步骤操作编辑器视图模型。
/// 编辑弹框的临时状态、参数集合和返回参数集合均由该类独立持有，方案页面只接收最终保存结果。
/// </summary>
public sealed class OperationEditorViewModel : ViewModelProperties
{
    #region 私有字段

    private readonly HashSet<InputParameter> _trackedInvokeParameters = new();
    private readonly HashSet<ReturnValue> _trackedReturnParameters = new();
    private InputParameter? _selectedEditingInvokeParameter;
    private ReturnValue? _selectedEditingReturnParameter;
    private bool _isInitializingOperationDrawer;
    private WorkStepOperation _editingOperation = new();
    private bool _isNewOperation;
    private bool _isOpen;
    private readonly List<string> _parameterReturnValueOptions = new();
    private readonly List<string> _externalReturnValueOptions = new();
    private bool _isRefreshingMetadata;
    private bool _decisionMode;

    #endregion

    #region 构造与集合

    public OperationEditorViewModel()
    {
        AttachEditingOperation(_editingOperation);
        SaveCommand = new RelayCommand(_ => Save());
        CloseCommand = new RelayCommand(_ => Close());
        RefreshOperationObjectOptions();
        RefreshLuaScriptTemplateOptions();
    }

    public ObservableCollection<string> OperationObjectOptions { get; } = new();

    public ObservableCollection<string> LuaScriptTemplateOptions { get; } = new();

    public ObservableCollection<StationOperationMethodItem> StationOperationMethodCollection { get; } = new();

    public ObservableCollection<StationOperationMethodItem> OperationMethods => StationOperationMethodCollection;

    /// <summary>
    /// 输入参数的界面编辑行；业务数据仍以 EditingOperation.Parameters 为唯一保存来源。
    /// </summary>
    public ObservableCollection<InputParameterEditorItem> EditingParameterRows { get; } = new();

    public ObservableCollection<string> ParameterTypeOptions { get; } = new()
    {
        "设置值",
        "返回值",
        "全局值"
    };

    /// <summary>
    /// 条件执行界面与 Judge 操作共同使用的判断条件名称。
    /// 直接从现有 Judge 方法定义生成，避免条件区域单独维护关系符并造成两套判断能力不一致。
    /// </summary>
    public IReadOnlyList<string> JudgmentConditionOptions { get; } =
        LoadJudgeMethodSelectionItems().Select(method => method.Name).ToArray();

    public ObservableCollection<string> ProtocolOptions { get; } = new();

    public ObservableCollection<string> CommandOptions { get; } = new();

    public ObservableCollection<string> InvokeMethodOptions { get; } = new();

    public ObservableCollection<string> InvokeMethodRemarkOptions { get; } = new();

    public ObservableCollection<string> ReturnValueOptions { get; } = new();

    public bool HasVisibleReturnValueName => EditingOperation.ReturnValues.Any(item => item.IsShowView);

    #endregion

    #region 编辑状态

    /// <summary>
    /// 当前业务编辑副本；弹框取消时不会污染方案中的原步骤。
    /// </summary>
    public WorkStepOperation EditingOperation
    {
        get => _editingOperation;
        private set
        {
            if (ReferenceEquals(_editingOperation, value))
            {
                return;
            }

            DetachEditingOperation(_editingOperation);
            _editingOperation = value;
            AttachEditingOperation(_editingOperation);
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasVisibleReturnValueName));
        }
    }

    public InputParameter? SelectedEditingInvokeParameter { get => _selectedEditingInvokeParameter; set => SetEditorField(ref _selectedEditingInvokeParameter, value); }

    public ReturnValue? SelectedEditingReturnParameter { get => _selectedEditingReturnParameter; set => SetEditorField(ref _selectedEditingReturnParameter, value); }

    public bool IsInitializingOperationDrawer
    {
        get => _isInitializingOperationDrawer;
        internal set => SetEditorField(ref _isInitializingOperationDrawer, value);
    }

    public bool IsLuaOperationSelected => IsLuaOperationObject(EditingOperation.OperationObjectName);

    public bool IsSystemOperationSelected => IsSystemOperationObject(EditingOperation.OperationObjectName);

    public bool IsJudgeOperationSelected => IsJudgeOperationObject(EditingOperation.OperationObjectName);

    public bool IsSystemOrJudgeOperationSelected => IsSystemOperationSelected || IsJudgeOperationSelected;

    public bool IsOpen
    {
        get => _isOpen;
        private set
        {
            if (_isOpen != value)
            {
                SetEditorField(ref _isOpen, value);
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(BtnTitle));
            }
        }
    }

    public string Title => _isNewOperation ? "新建步骤" : "编辑步骤";
    public string BtnTitle => _isNewOperation ? "新建" : "保存";
    public ICommand SaveCommand { get; }

    public ICommand CloseCommand { get; }

    #endregion

    #region 编辑器事件

    /// <summary>
    /// 编辑状态变化通知；方案层仅在确实依赖方案上下文时响应。
    /// </summary>
    public event PropertyChangedEventHandler? EditorStateChanged;

    /// <summary>
    /// 编辑器生成最终操作后的保存通知。
    /// </summary>
    public event EventHandler<OperationEditorSavedEventArgs>? OperationSaved;

    internal void PublishSaved(WorkStepOperation operation, bool isNewOperation)
    {
        OperationSaved?.Invoke(this, new OperationEditorSavedEventArgs(operation, isNewOperation));
    }

    private void SetEditorField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (!SetField(ref field, value, propertyName))
        {
            return;
        }

        EditorStateChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        RefreshMetadataForProperty(propertyName);
    }

    /// <summary>
    /// 订阅业务编辑副本及其参数集合变化，将实体属性变化转换为编辑器联动通知。
    /// </summary>
    private void AttachEditingOperation(WorkStepOperation operation)
    {
        operation.PropertyChanged += EditingOperation_PropertyChanged;
        operation.Parameters.CollectionChanged += EditingInvokeParameters_CollectionChanged;
        operation.ReturnValues.CollectionChanged += EditingReturnParameters_CollectionChanged;

        // 克隆后的编辑实体通常已经包含参数。仅监听 CollectionChanged 无法订阅这些现有项，
        // 会导致用户切换参数类型时收不到 PropertyChanged，返回值候选集合也就不会刷新。
        foreach (InputParameter parameter in operation.Parameters)
        {
            if (_trackedInvokeParameters.Add(parameter))
            {
                parameter.PropertyChanged += EditingInvokeParameter_PropertyChanged;
            }
        }

        foreach (ReturnValue returnValue in operation.ReturnValues)
        {
            if (_trackedReturnParameters.Add(returnValue))
            {
                returnValue.PropertyChanged += EditingReturnParameter_PropertyChanged;
            }
        }

        SynchronizeEditingParameterRows();
    }

    private void DetachEditingOperation(WorkStepOperation operation)
    {
        operation.PropertyChanged -= EditingOperation_PropertyChanged;
        operation.Parameters.CollectionChanged -= EditingInvokeParameters_CollectionChanged;
        operation.ReturnValues.CollectionChanged -= EditingReturnParameters_CollectionChanged;

        // 编辑对象切换时同步解除子项订阅，避免旧步骤参数继续影响当前编辑器。
        foreach (InputParameter parameter in _trackedInvokeParameters)
        {
            parameter.PropertyChanged -= EditingInvokeParameter_PropertyChanged;
        }

        foreach (ReturnValue returnValue in _trackedReturnParameters)
        {
            returnValue.PropertyChanged -= EditingReturnParameter_PropertyChanged;
        }

        _trackedInvokeParameters.Clear();
        _trackedReturnParameters.Clear();
    }

    private void EditingOperation_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorkStepOperation.Parameters))
        {
            return;
        }

        if (e.PropertyName == nameof(WorkStepOperation.ReturnValues))
        {
            OnPropertyChanged(nameof(HasVisibleReturnValueName));
            return;
        }

        if (e.PropertyName is not (nameof(WorkStepOperation.OperationObjectName)
            or nameof(WorkStepOperation.PCommandName)
            or nameof(WorkStepOperation.ReturnValue)
            or nameof(WorkStepOperation.LuaScript)
            or nameof(WorkStepOperation.Summary)))
        {
            return;
        }

        OnPropertyChanged(e.PropertyName);

        if (e.PropertyName == nameof(WorkStepOperation.OperationObjectName))
        {
            OnPropertyChanged(nameof(IsLuaOperationSelected));
            OnPropertyChanged(nameof(IsSystemOperationSelected));
            OnPropertyChanged(nameof(IsJudgeOperationSelected));
            OnPropertyChanged(nameof(IsSystemOrJudgeOperationSelected));
        }

        EditorStateChanged?.Invoke(this, new PropertyChangedEventArgs(e.PropertyName));
        RefreshMetadataForProperty(e.PropertyName);
    }

    #endregion

    #region 模板与方法应用

    #region 操作元数据与默认参数

    public IEnumerable<string> LoadDeviceOperationObjectNames()
    {
        return OperationConfigurationStore.LoadDeviceNames();
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
            return new[] { "Lua" };
        }

        if (IsJudgeOperationObject(normalizedOperationObject))
        {
            return LoadJudgeMethodSelectionItems().Select(method => method.Name);
        }

        if (IsSystemOperationObject(normalizedOperationObject))
        {
            return LoadSystemMethodSelectionItems().Select(method => method.Name);
        }

        return LoadDeviceInvokeMethodOptions(normalizedOperationObject);
    }

    private static IEnumerable<string> LoadDeviceInvokeMethodOptions(string operationObject)
    {
        IEnumerable<string> businessOperations = BusinessOperationBindingResolver
            .GetOperationsForOperationObject(operationObject)
            .Select(operation => operation.OperationId);
        HashSet<string> allowedProtocols = new(
            OperationConfigurationStore.LoadDeviceSupportedProtocolNames(operationObject),
            StringComparer.OrdinalIgnoreCase);
        return businessOperations.Concat(OperationConfigurationStore.LoadProtocolSelectionItems()
            .Where(protocol => allowedProtocols.Contains(protocol.Name))
            .SelectMany(protocol => protocol.Commands.Select(command => command.Name)));
    }

    public void SynchronizeOperationMetadata(WorkStepOperation operation, IReadOnlyList<string> invokeMethodOptions)
    {
        ArgumentNullException.ThrowIfNull(operation);
        string operationObject = operation.OperationObjectName?.Trim() ?? string.Empty;
        if (IsLuaOperationObject(operationObject))
        {
            operation.OperationObjectName = "Lua";
            operation.PCommandName = "Lua";
            return;
        }

        if (!string.IsNullOrWhiteSpace(operation.PCommandName) &&
            !invokeMethodOptions.Any(option => string.Equals(option?.Trim(), operation.PCommandName?.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            operation.PCommandName = string.Empty;
        }

        if (IsSystemOperationObject(operationObject))
        {
            operation.OperationObjectName = "System";
        }
    }

    private static bool TryFindDeviceCommand(string operationObject, string invokeMethod, out string protocolName, out string commandName)
    {
        protocolName = string.Empty;
        commandName = string.Empty;
        HashSet<string> allowedProtocols = new(
            OperationConfigurationStore.LoadDeviceSupportedProtocolNames(operationObject),
            StringComparer.OrdinalIgnoreCase);
        foreach (ProtocolSelectionItem protocol in OperationConfigurationStore.LoadProtocolSelectionItems()
                     .Where(protocol => allowedProtocols.Contains(protocol.Name)))
        {
            ProtocolCommandSelectionItem? command = protocol.Commands.FirstOrDefault(item =>
                string.Equals(item.Name?.Trim(), invokeMethod?.Trim(), StringComparison.OrdinalIgnoreCase));
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

    public ObservableCollection<InputParameter> CreateDefaultOperationParameters(WorkStepOperation operation)
    {
        if (operation is null || IsLuaOperationObject(operation.OperationObjectName))
        {
            return new ObservableCollection<InputParameter>();
        }

        string operationObject = operation.OperationObjectName?.Trim() ?? string.Empty;
        string invokeMethod = operation.PCommandName?.Trim() ?? string.Empty;
        if (IsJudgeOperationObject(operationObject))
        {
            return CreateOperationParametersFromSystemMethod(FindJudgeMethodByName(invokeMethod));
        }

        if (IsSystemOperationObject(operationObject))
        {
            return CreateOperationParametersFromSystemMethod(FindSystemMethodByName(invokeMethod));
        }

        BusinessOperationDescriptor? businessOperation = BusinessOperationBindingResolver.FindOperationForOperationObject(operationObject, null, invokeMethod);
        if (businessOperation is not null)
        {
            return CreateOperationParametersFromBusinessOperation(businessOperation);
        }

        if (TryFindDeviceCommand(operationObject, invokeMethod, out string protocolName, out string commandName))
        {
            ObservableCollection<InputParameter> parameters = new();
            int num = 1;
            foreach (ProtocolPlaceholderSelectionItem placeholder in LoadProtocolCommandPlaceholders(protocolName, commandName))
            {
                parameters.Add(new InputParameter
                {
                    Num = num++,
                    ParameterType = ParameterTypeOptions.First(),
                    ParameterName = placeholder.Name,
                    Value = placeholder.Value,
                    Description = placeholder.Name
                });
            }

            return parameters;
        }

        return new ObservableCollection<InputParameter>();
    }

    private ObservableCollection<InputParameter> CreateOperationParametersFromSystemMethod(SystemMethodSelectionItem? method)
    {
        return method is null
            ? new ObservableCollection<InputParameter>()
            : new ObservableCollection<InputParameter>(method.Parameters.Select((parameter, index) => new InputParameter
            {
                Num = index + 1,
                ParameterType = ParameterTypeOptions.First(),
                ParameterName = parameter.Name,
                Value = parameter.DefaultValue,
                Description = parameter.Description
            }));
    }

    private ObservableCollection<InputParameter> CreateOperationParametersFromBusinessOperation(BusinessOperationDescriptor operation)
    {
        return new ObservableCollection<InputParameter>(operation.Parameters.OrderBy(parameter => parameter.Sequence).Select(parameter => new InputParameter
        {
            Num = parameter.Sequence,
            ParameterType = ParameterTypeOptions.First(),
            ParameterName = parameter.Name,
            Value = parameter.DefaultValue,
            Description = string.IsNullOrWhiteSpace(parameter.Description) ? parameter.DisplayName : parameter.Description
        }));
    }

    private static SystemMethodSelectionItem? FindSystemMethodByName(string methodName)
    {
        return LoadSystemMethodSelectionItems().FirstOrDefault(method =>
            string.Equals(method.Name, methodName?.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static SystemMethodSelectionItem? FindJudgeMethodByName(string methodName)
    {
        return LoadJudgeMethodSelectionItems().FirstOrDefault(method =>
            string.Equals(method.Name, methodName?.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<SystemMethodSelectionItem> LoadJudgeMethodSelectionItems()
    {
        return new[]
        {
            CreateJudgeMethod("等于判断", "判断两个值是否相等", ("左值", "左侧待比较的值"), ("右值", "右侧待比较的值")),
            CreateJudgeMethod("不等判断", "判断两个值是否不相等", ("左值", "左侧待比较的值"), ("右值", "右侧待比较的值")),
            CreateJudgeMethod("大于判断", "判断左值是否大于右值", ("左值", "左侧待比较的值"), ("右值", "右侧待比较的值")),
            CreateJudgeMethod("大于等于判断", "判断左值是否大于等于右值", ("左值", "左侧待比较的值"), ("右值", "右侧待比较的值")),
            CreateJudgeMethod("小于判断", "判断左值是否小于右值", ("左值", "左侧待比较的值"), ("右值", "右侧待比较的值")),
            CreateJudgeMethod("小于等于判断", "判断左值是否小于等于右值", ("左值", "左侧待比较的值"), ("右值", "右侧待比较的值")),
            CreateJudgeMethod("包含判断", "判断文本是否包含指定关键字", ("待判断值", "待检查的文本"), ("关键字", "用于匹配的关键字")),
            CreateJudgeMethod("不包含判断", "判断文本是否不包含指定关键字", ("待判断值", "待检查的文本"), ("关键字", "用于匹配的关键字")),
            CreateJudgeMethod("为空判断", "判断指定值是否为空", ("待判断值", "待检查的值")),
            CreateJudgeMethod("不为空判断", "判断指定值是否不为空", ("待判断值", "待检查的值"))
        };
    }

    private static SystemMethodSelectionItem CreateJudgeMethod(string name, string summary, params (string Name, string Description)[] parameters)
    {
        return new SystemMethodSelectionItem(name, summary, parameters.Select(parameter =>
            new SystemMethodParameterSelectionItem(parameter.Name, string.Empty, parameter.Description)));
    }

    private static IReadOnlyList<SystemMethodSelectionItem> LoadSystemMethodSelectionItems()
    {
        return LoadBusinessMethodSelectionItems("System");
    }

    private static IReadOnlyList<SystemMethodSelectionItem> LoadBusinessMethodSelectionItems(string deviceId)
    {
        return BusinessOperationCatalog.GetOperations(deviceId).Select(operation => new SystemMethodSelectionItem(
            operation.OperationId,
            string.IsNullOrWhiteSpace(operation.Description) ? operation.DisplayName : operation.Description,
            operation.Parameters.Select(parameter => new SystemMethodParameterSelectionItem(
                parameter.Name,
                parameter.TypeName,
                string.IsNullOrWhiteSpace(parameter.Description) ? parameter.DisplayName : parameter.Description,
                parameter.DefaultValue)))).ToArray();
    }

    private static IReadOnlyList<ProtocolPlaceholderSelectionItem> LoadProtocolCommandPlaceholders(string protocolName, string commandName)
    {
        return OperationConfigurationStore.LoadProtocolCommandPlaceholders(protocolName, commandName)
            .Select(item => new ProtocolPlaceholderSelectionItem(item.Name, item.Value))
            .ToArray();
    }

    public static bool IsSystemOperationObject(string? operationObject)
    {
        return string.Equals(operationObject?.Trim(), "System", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(operationObject?.Trim(), "系统", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsJudgeOperationObject(string? operationObject)
    {
        return string.Equals(operationObject?.Trim(), "判断", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(operationObject?.Trim(), "Judge", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsLuaOperationObject(string? operationObject)
    {
        return string.Equals(operationObject?.Trim(), "Lua", StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    public void SetDecisionMode(bool isDecisionMode)
    {
        _decisionMode = isDecisionMode;
        RefreshOperationObjectOptions();
    }

    public void SetExternalReturnValueOptions(IEnumerable<string>? values)
    {
        _externalReturnValueOptions.Clear();
        _externalReturnValueOptions.AddRange((values ?? Enumerable.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase));
        ReplaceStringOptions(ReturnValueOptions, _parameterReturnValueOptions
            .Concat(_externalReturnValueOptions)
            .Concat(new[] { EditingOperation.ReturnValue }));
        RefreshParameterValueOptions();
    }

    /// <summary>
    /// 编辑字段变化时在编辑器内部刷新候选项和默认参数，避免方案 ViewModel 承担弹框联动。
    /// </summary>
    private void RefreshMetadataForProperty(string? propertyName)
    {
        if (_isRefreshingMetadata || IsInitializingOperationDrawer)
        {
            return;
        }

        _isRefreshingMetadata = true;
        try
        {
            if (propertyName == nameof(EditingOperation.OperationObjectName))
            {
                RefreshProtocolAndMethodOptions();
            }
            else if (propertyName == nameof(EditingOperation.PCommandName))
            {
                if (!IsSystemOrJudgeOperationSelected && !IsLuaOperationSelected)
                {
                    RefreshProtocolCommandParameters();
                }
                else
                {
                    RefreshSelectedMethodParameters();
                }
            }
        }
        finally
        {
            _isRefreshingMetadata = false;
        }
    }

    private void RefreshOperationObjectOptions()
    {
        IEnumerable<string> options = _decisionMode
            ? new[] { "判断" }
            : new[] { "System", "Lua" }.Concat(OperationConfigurationStore.LoadDeviceNames());
        ReplaceStringOptions(OperationObjectOptions, options);
        if (string.IsNullOrWhiteSpace(EditingOperation.OperationObjectName) || !OperationObjectOptions.Contains(EditingOperation.OperationObjectName))
        {
            EditingOperation.OperationObjectName = OperationObjectOptions.FirstOrDefault() ?? string.Empty;
        }
    }

    private void RefreshProtocolAndMethodOptions()
    {
        ProtocolOptions.Clear();
        CommandOptions.Clear();
        InvokeMethodOptions.Clear();
        InvokeMethodRemarkOptions.Clear();
        StationOperationMethodCollection.Clear();

        if (IsLuaOperationSelected)
        {
            EditingOperation.PCommandName = "Lua";
            EditingOperation.Parameters.Clear();
            EditingOperation.ReturnValues.Clear();
            OnPropertyChanged(nameof(OperationMethods));
            return;
        }

        if (IsJudgeOperationSelected)
        {
            string[] judgeMethods = { "等于判断", "不等判断", "大于判断", "大于等于判断", "小于判断", "小于等于判断", "包含判断", "不包含判断", "为空判断", "不为空判断" };
            foreach (string method in judgeMethods)
            {
                InvokeMethodOptions.Add(method);
                StationOperationMethodCollection.Add(new StationOperationMethodItem
                {
                    Kind = "方法",
                    OperationType = "判断",
                    OperationObject = "判断",
                    InvokeMethod = method,
                    Summary = method,
                    ParameterCount = method.Contains("为空", StringComparison.Ordinal) ? 1 : 2
                });
            }
        }
        else if (IsSystemOperationSelected)
        {
            foreach (BusinessOperationDescriptor operation in BusinessOperationCatalog.GetOperations("System"))
            {
                InvokeMethodOptions.Add(operation.OperationId);
                InvokeMethodRemarkOptions.Add(string.IsNullOrWhiteSpace(operation.Description) ? operation.DisplayName : operation.Description);
                StationOperationMethodCollection.Add(new StationOperationMethodItem
                {
                    Kind = "方法",
                    OperationType = "系统",
                    OperationObject = "System",
                    InvokeMethod = operation.OperationId,
                    Summary = string.IsNullOrWhiteSpace(operation.Description) ? operation.DisplayName : operation.Description,
                    ParameterCount = operation.Parameters.Count
                });
            }
        }
        else
        {
            foreach (string protocol in OperationConfigurationStore.LoadDeviceSupportedProtocolNames(EditingOperation.OperationObjectName))
            {
                ProtocolOptions.Add(protocol);
                foreach (string command in OperationConfigurationStore.LoadProtocolCommandNames(protocol))
                {
                    CommandOptions.Add(command);
                    InvokeMethodOptions.Add(command);
                    StationOperationMethodCollection.Add(new StationOperationMethodItem
                    {
                        Kind = "指令",
                        OperationType = "设备",
                        OperationObject = EditingOperation.OperationObjectName,
                        ProtocolName = protocol,
                        CommandName = command,
                        InvokeMethod = command,
                        Summary = protocol,
                        ParameterCount = OperationConfigurationStore.LoadProtocolCommandPlaceholders(protocol, command).Count
                    });
                }
            }

            foreach (BusinessOperationDescriptor operation in BusinessOperationBindingResolver.GetOperationsForOperationObject(EditingOperation.OperationObjectName))
            {
                InvokeMethodOptions.Add(operation.OperationId);
                StationOperationMethodCollection.Add(new StationOperationMethodItem
                {
                    Kind = "业务",
                    OperationType = "业务",
                    OperationObject = EditingOperation.OperationObjectName,
                    InvokeMethod = operation.OperationId,
                    Summary = string.IsNullOrWhiteSpace(operation.Description) ? operation.DisplayName : operation.Description,
                    ParameterCount = operation.Parameters.Count
                });
            }

        }

        if (string.IsNullOrWhiteSpace(EditingOperation.PCommandName) || !InvokeMethodOptions.Contains(EditingOperation.PCommandName))
        {
            EditingOperation.PCommandName = InvokeMethodOptions.FirstOrDefault() ?? string.Empty;
        }

        // 方法集合填充完成后通知视图恢复当前步骤对应的选中行。
        // OperationMethods 始终返回同一个集合实例，集合变更只能刷新行，无法表达当前方法的选中状态。
        OnPropertyChanged(nameof(OperationMethods));
    }

    private void RefreshProtocolCommandParameters()
    {
        if (!TryFindDeviceCommand(
                EditingOperation.OperationObjectName,
                EditingOperation.PCommandName,
                out string protocolName,
                out string commandName))
        {
            BusinessOperationDescriptor? businessOperation = BusinessOperationBindingResolver.FindOperationForOperationObject(
                EditingOperation.OperationObjectName,
                null,
                EditingOperation.PCommandName);
            if (businessOperation is not null)
            {
                ObservableCollection<InputParameter> parameters = CreateOperationParametersFromBusinessOperation(businessOperation);
                EditingOperation.Parameters.Clear();
                foreach (InputParameter parameter in parameters)
                {
                    EditingOperation.Parameters.Add(parameter);
                }
                SelectedEditingInvokeParameter = EditingOperation.Parameters.FirstOrDefault();
            }
            return;
        }

        EditingOperation.Parameters.Clear();
        int num = 1;
        foreach (ProtocolPlaceholderDefinition placeholder in OperationConfigurationStore.LoadProtocolCommandPlaceholders(protocolName, commandName))
        {
            EditingOperation.Parameters.Add(new InputParameter
            {
                Num = num++,
                ParameterType = ParameterTypeOptions.First(),
                ParameterName = placeholder.Name,
                Value = placeholder.Value,
                Description = placeholder.Name
            });
        }

        ReplaceStringOptions(ReturnValueOptions, OperationConfigurationStore.LoadProtocolCommandReturnValueKeys(protocolName, commandName));
        ReplaceStepEditorReturnParameterRows(new WorkStepOperation
        {
            ReturnValues = new ObservableCollection<ReturnValue>(ReturnValueOptions.Select((key, index) => new ReturnValue
            {
                Num = index + 1,
                ReturnParameterName = key
            }))
        });
        SelectedEditingInvokeParameter = EditingOperation.Parameters.FirstOrDefault();
    }

    private void RefreshSelectedMethodParameters()
    {
        if (!IsSystemOperationSelected && !IsJudgeOperationSelected)
        {
            return;
        }

        EditingOperation.Parameters.Clear();
        if (IsJudgeOperationSelected)
        {
            int count = EditingOperation.PCommandName.Contains("为空", StringComparison.Ordinal) ? 1 : 2;
            for (int index = 0; index < count; index++)
            {
                EditingOperation.Parameters.Add(new InputParameter
                {
                    Num = index + 1,
                    ParameterType = ParameterTypeOptions.First(),
                    ParameterName = index == 0 ? "左值" : "右值",
                    Description = index == 0 ? "左侧待比较的值" : "右侧待比较的值"
                });
            }
        }
        else
        {
            BusinessOperationDescriptor? operation = BusinessOperationCatalog.GetOperations("System")
                .FirstOrDefault(item => string.Equals(item.OperationId, EditingOperation.PCommandName, StringComparison.OrdinalIgnoreCase));
            if (operation is not null)
            {
                foreach (BusinessParameterDescriptor parameter in operation.Parameters.OrderBy(item => item.Sequence))
                {
                    EditingOperation.Parameters.Add(new InputParameter
                    {
                        Num = parameter.Sequence,
                        ParameterType = ParameterTypeOptions.First(),
                        ParameterName = parameter.Name,
                        Value = parameter.DefaultValue,
                        Description = string.IsNullOrWhiteSpace(parameter.Description) ? parameter.DisplayName : parameter.Description
                    });
                }
            }
        }

        SelectedEditingInvokeParameter = EditingOperation.Parameters.FirstOrDefault();
    }

    /// <summary>
    /// 打开编辑器并从业务实体建立独立编辑副本，避免取消编辑时污染原数据。
    /// </summary>
    public void Open(
        WorkStepOperation operation,
        bool isNewOperation,
        IEnumerable<WorkStepOperation>? operations = null)
    {
        ArgumentNullException.ThrowIfNull(operation);

        IsInitializingOperationDrawer = true;
        try
        {
            EditingOperation = operation.Clone();
            _isNewOperation = isNewOperation;
            RefreshOperationContext(operation, operations);
            SelectedEditingInvokeParameter = EditingOperation.Parameters.FirstOrDefault();
            SelectedEditingReturnParameter = EditingOperation.ReturnValues.FirstOrDefault();
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(BtnTitle));
            IsOpen = true;
        }
        finally
        {
            IsInitializingOperationDrawer = false;
        }

        RefreshMetadataForProperty(nameof(EditingOperation.OperationObjectName));
    }

    /// <summary>
    /// 将当前编辑状态一次性构建成最终业务实体并发布，编辑器不直接修改方案集合。
    /// </summary>
    public void Save()
    {
        WorkStepOperation result = EditingOperation.Clone();
        result.OperationObjectName = result.OperationObjectName.Trim();
        result.PCommandName = IsLuaOperationSelected ? string.Empty : result.PCommandName.Trim();
        result.ReturnValue = IsLuaOperationSelected ? string.Empty : result.ReturnValue.Trim();
        result.LuaScript = IsLuaOperationSelected ? result.LuaScript : string.Empty;
        result.Summary = result.Summary.Trim();
        result.Parameters = IsLuaOperationSelected
            ? new ObservableCollection<InputParameter>()
            : new ObservableCollection<InputParameter>(EditingOperation.Parameters.OrderBy(parameter => parameter.Num).Select(parameter => parameter.Clone()));
        result.ReturnValues = IsLuaOperationSelected
            ? new ObservableCollection<ReturnValue>()
            : new ObservableCollection<ReturnValue>(EditingOperation.ReturnValues.OrderBy(parameter => parameter.Num).Select(parameter => parameter.Clone()));

        PublishSaved(result, _isNewOperation);
        EditingOperation = result.Clone();
        EditingOperation.Id = Guid.NewGuid().ToString("N");
        foreach (InputParameter parameter in EditingOperation.Parameters)
        {
            parameter.Id = Guid.NewGuid().ToString("N");
        }

        // ValueOptions 是仅供弹框使用的 JsonIgnore 编辑态集合，InputParameter.Clone 不会复制该集合。
        // 保存后编辑器继续保留当前操作时，必须基于原有前置步骤返回值上下文重新生成候选，
        // 否则参数中已经选择的 Value 虽然仍在，但下拉返回值集合会被显示为空。
        RefreshParameterValueOptions();
        SelectedEditingInvokeParameter = EditingOperation.Parameters.FirstOrDefault();
        SelectedEditingReturnParameter = EditingOperation.ReturnValues.FirstOrDefault();
        //if (_isNewOperation)
        //{
            
        //}
        //else
        //{
        //    Close();
        //}
    }

    public void Close()
    {
        IsOpen = false;
        EditingOperation = new WorkStepOperation();
        _isNewOperation = false;
        EditingOperation.Parameters.Clear();
        EditingOperation.ReturnValues.Clear();
        SelectedEditingInvokeParameter = null;
        SelectedEditingReturnParameter = null;
    }

    public void RefreshLuaScriptTemplateOptions()
    {
        ReplaceStringOptions(LuaScriptTemplateOptions, OperationConfigurationStore.LoadLuaScriptTemplateNames());
    }

    public void ApplyLuaScriptTemplate(string? templateName)
    {
        if (string.IsNullOrWhiteSpace(templateName))
        {
            return;
        }

        LuaScriptProfileDocument? document = OperationConfigurationStore.LoadLuaScriptTemplate(templateName.Trim());
        if (document is not null)
        {
            EditingOperation.LuaScript = document.ScriptText ?? string.Empty;
        }
    }

    public WorkStepOperation? CreateOperationFromMethodItem(StationOperationMethodItem? item)
    {
        if (item is null)
        {
            return null;
        }

        WorkStepOperation operation = new()
        {
            OperationObjectName = item.OperationObject,
            PCommandName = item.InvokeMethod,
            Summary = item.Summary
        };

        // 选中新的方法或指令时必须基于该方法重新生成参数，不能复制上一个编辑方法的集合。
        operation.Parameters = CreateDefaultOperationParameters(operation);
        operation.ReturnValues = CreateReturnParametersFromOperation(operation);
        return operation;
    }

    /// <summary>
    /// 根据操作定义推导可输出的返回参数，供方案和流程图编辑场景复用。
    /// </summary>
    public ObservableCollection<ReturnValue> CreateReturnParametersFromOperation(WorkStepOperation? operation)
    {
        if (operation is null || string.Equals(operation.OperationObjectName, "Lua", StringComparison.OrdinalIgnoreCase))
        {
            return new ObservableCollection<ReturnValue>();
        }

        if (operation.ReturnValues.Count > 0)
        {
            return new ObservableCollection<ReturnValue>(operation.ReturnValues.Select(item => item.Clone()));
        }

        BusinessOperationDescriptor? businessOperation = BusinessOperationBindingResolver.FindOperationForOperationObject(
            operation.OperationObjectName,
            null,
            operation.PCommandName);
        if (businessOperation is not null && !string.Equals(businessOperation.ReturnTypeName, "void", StringComparison.OrdinalIgnoreCase))
        {
            return new ObservableCollection<ReturnValue>
            {
                new() { Num = 1, ReturnParameterName = businessOperation.OperationId }
            };
        }

        foreach (string protocol in OperationConfigurationStore.LoadDeviceSupportedProtocolNames(operation.OperationObjectName))
        {
            if (!OperationConfigurationStore.LoadProtocolCommandNames(protocol)
                    .Any(command => string.Equals(command, operation.PCommandName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            return new ObservableCollection<ReturnValue>(OperationConfigurationStore
                .LoadProtocolCommandReturnValueKeys(protocol, operation.PCommandName)
                .Select((key, index) => new ReturnValue { Num = index + 1, ReturnParameterName = key }));
        }

        return new ObservableCollection<ReturnValue>();
    }

    #endregion

    #region 返回参数

    public void ReplaceStepEditorReturnParameterRows(WorkStepOperation? operation)
    {
        // 先生成独立快照再清空界面集合，既避免传入当前 EditingOperation 时枚举源被清空，
        // 也按返回值键统一去重，防止方法元数据或重复刷新产生同名返回参数。
        List<ReturnValue> rows = (operation?.ReturnValues ?? Enumerable.Empty<ReturnValue>())
            .Where(item => !string.IsNullOrWhiteSpace(item.ReturnParameterName))
            .GroupBy(item => item.ReturnParameterName.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderBy(item => item.Num).First().Clone())
            .OrderBy(item => item.Num)
            .ToList();

        EditingOperation.ReturnValues.Clear();
        for (int index = 0; index < rows.Count; index++)
        {
            rows[index].Num = index + 1;
            EditingOperation.ReturnValues.Add(rows[index]);
        }

        SelectedEditingReturnParameter = EditingOperation.ReturnValues.FirstOrDefault();
    }

    public void ClearStepEditorReturnParameterRows()
    {
        EditingOperation.ReturnValues.Clear();
        SelectedEditingReturnParameter = null;
    }

    #endregion

    #region 参数集合监听

    private void EditingInvokeParameters_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateTrackedItems(e, _trackedInvokeParameters, EditingInvokeParameter_PropertyChanged);
        NormalizeInvokeParameterNums();
        SynchronizeEditingParameterRows();
        RefreshParameterValueOptions();
    }

    private void EditingReturnParameters_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateTrackedItems(e, _trackedReturnParameters, EditingReturnParameter_PropertyChanged);
        OnPropertyChanged(nameof(HasVisibleReturnValueName));
    }

    private static void UpdateTrackedItems<T>(
        NotifyCollectionChangedEventArgs e,
        HashSet<T> trackedItems,
        PropertyChangedEventHandler handler)
        where T : INotifyPropertyChanged
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (T item in trackedItems.ToList())
            {
                item.PropertyChanged -= handler;
            }

            trackedItems.Clear();
        }

        if (e.NewItems is not null)
        {
            foreach (T item in e.NewItems.OfType<T>())
            {
                if (trackedItems.Add(item))
                {
                    item.PropertyChanged += handler;
                }
            }
        }

        if (e.OldItems is not null)
        {
            foreach (T item in e.OldItems.OfType<T>())
            {
                if (trackedItems.Remove(item))
                {
                    item.PropertyChanged -= handler;
                }
            }
        }
    }

    private void EditingInvokeParameter_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is InputParameter parameter &&
            e.PropertyName == nameof(InputParameter.ParameterType))
        {
            // 仅类型变化时重建候选项。参数值选择过程中清空 ItemsSource 会中断 ComboBox 的选中提交。
            UpdateParameterValueOptions(parameter);
        }

        if (e.PropertyName == nameof(InputParameter.Num))
        {
            List<InputParameter> ordered = EditingOperation.Parameters.OrderBy(item => item.Num).ToList();
            for (int index = 0; index < ordered.Count; index++)
            {
                int oldIndex = EditingOperation.Parameters.IndexOf(ordered[index]);
                if (oldIndex != index)
                {
                    EditingOperation.Parameters.Move(oldIndex, index);
                }
            }
        }
    }

    private void EditingReturnParameter_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReturnValue.IsShowView))
        {
            OnPropertyChanged(nameof(HasVisibleReturnValueName));
        }
    }

    private void NormalizeInvokeParameterNums()
    {
        HashSet<int> used = new();
        int next = 1;
        foreach (InputParameter parameter in EditingOperation.Parameters)
        {
            if (parameter.Num <= 0 || !used.Add(parameter.Num))
            {
                while (used.Contains(next))
                {
                    next++;
                }

                parameter.Num = next;
                used.Add(next);
            }

            next = Math.Max(next, parameter.Num + 1);
        }
    }

    /// <summary>
    /// 刷新输入参数可引用的前置步骤返回值。
    /// </summary>
    public void RefreshParameterValueOptions()
    {
        foreach (InputParameterEditorItem row in EditingParameterRows)
        {
            UpdateParameterValueOptions(row.Parameter);
        }
    }

    private void UpdateParameterValueOptions(InputParameter parameter)
    {
        InputParameterEditorItem? editingRow = EditingParameterRows.FirstOrDefault(row => ReferenceEquals(row.Parameter, parameter));
        if (editingRow is null)
        {
            return;
        }

        IEnumerable<string> options = string.Equals(parameter.ParameterType?.Trim(), "返回值", StringComparison.Ordinal)
            ? _parameterReturnValueOptions.Concat(_externalReturnValueOptions)
            : Enumerable.Empty<string>();
        ReplaceStringOptions(editingRow.ValueOptions, options);
    }

    /// <summary>
    /// 根据业务参数集合重建编辑行。编辑行只保存界面候选，参数对象保持同一引用，
    /// 因此 DataGrid 编辑结果会直接进入 EditingOperation.Parameters，并由保存流程统一克隆。
    /// </summary>
    private void SynchronizeEditingParameterRows()
    {
        Dictionary<InputParameter, InputParameterEditorItem> existingRows = EditingParameterRows
            .ToDictionary(row => row.Parameter);

        EditingParameterRows.Clear();
        foreach (InputParameter parameter in EditingOperation.Parameters)
        {
            EditingParameterRows.Add(existingRows.TryGetValue(parameter, out InputParameterEditorItem? row)
                ? row
                : new InputParameterEditorItem(parameter));
        }
    }

    /// <summary>
    /// 根据当前工步操作顺序建立编辑器所需的返回值上下文，方案层无需保存编辑临时状态。
    /// </summary>
    private void RefreshOperationContext(WorkStepOperation editingOperation, IEnumerable<WorkStepOperation>? operations)
    {
        List<WorkStepOperation> operationList = operations?.ToList() ?? new List<WorkStepOperation>();
        int editingIndex = operationList.FindIndex(item =>
            ReferenceEquals(item, editingOperation) || string.Equals(item.Id, editingOperation.Id, StringComparison.Ordinal));
        if (editingIndex < 0)
        {
            editingIndex = operationList.Count;
        }

        _parameterReturnValueOptions.Clear();
        _parameterReturnValueOptions.AddRange(operationList
            .Take(editingIndex)
            .Where(item => !string.IsNullOrWhiteSpace(item.ReturnValue))
            .SelectMany(item => item.ReturnValues
                .Where(returnValue => !string.IsNullOrWhiteSpace(returnValue.ReturnParameterName))
                .Select(returnValue => $"{item.ReturnValue.Trim()}_{returnValue.ReturnParameterName.Trim()}"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
        ReplaceStringOptions(ReturnValueOptions, _parameterReturnValueOptions
            .Concat(_externalReturnValueOptions)
            .Concat(new[] { EditingOperation.ReturnValue }));
        RefreshParameterValueOptions();
    }

    private static void ReplaceStringOptions(ObservableCollection<string> target, IEnumerable<string> values)
    {
        target.Clear();
        foreach (string value in values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            target.Add(value);
        }
    }

    #endregion
}

/// <summary>
/// 操作编辑器保存结果。
/// </summary>
public sealed class OperationEditorSavedEventArgs : EventArgs
{
    public OperationEditorSavedEventArgs(WorkStepOperation operation, bool isNewOperation)
    {
        Operation = operation ?? throw new ArgumentNullException(nameof(operation));
        IsNewOperation = isNewOperation;
    }

    public WorkStepOperation Operation { get; }

    public bool IsNewOperation { get; }
}
