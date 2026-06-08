using ControlLibrary;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;

namespace Module.Business.Features.SchemeConfiguration
{   
    /// <summary>
    /// 方案配置，保存方案名称和工步引用快照。
    /// </summary>
    public sealed class SchemeProfile : ViewModelProperties
    {
        #region 构造方法

        public SchemeProfile()
        {
        }

        #endregion

        #region 绑定属性
        #region 唯一标识
        private string _id = Guid.NewGuid().ToString("N");
        /// <summary>
        /// 唯一标识
        /// </summary>
        public string Id
        {
            get => _id;
            set => SetField(ref _id, string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim());
        }
        #endregion
        #region 方案名称
        private string _schemeName = "方案 1";
        /// <summary>
        /// 方案名称
        /// </summary>
        public string SchemeName
        {
            get => _schemeName;
            set => SetField(ref _schemeName, (value ?? string.Empty).Trim());
        }
        #endregion
        #region 工步集合
        private ObservableCollection<WorkStepProfile> _steps = new();
        /// <summary>
        /// 工步集合
        /// </summary>
        public ObservableCollection<WorkStepProfile> Steps
        {
            get => _steps;
            set
            {
                if (ReferenceEquals(_steps, value))
                {
                    return;
                }
                _steps = value ?? new ObservableCollection<WorkStepProfile>();
                OnPropertyChanged();
            }
        }
        #endregion
        #endregion

        #region 复制方法

        public SchemeProfile Clone()
        {
            return new SchemeProfile
            {
                Id = Id,
                SchemeName = SchemeName,
                Steps = new ObservableCollection<WorkStepProfile>(Steps.Select(step => step.Clone())),
                LastModifiedAt = LastModifiedAt
            };
        }
        #endregion
    }
    public sealed partial class WorkStepProfile : ViewModelProperties
    {
        #region 构造方法

        public WorkStepProfile()
        {
            InitializeSchemeState();
        }

        #endregion

        #region 基础属性
        #region 唯一标识
        private string _id = Guid.NewGuid().ToString("N");
        /// <summary>
        /// 唯一标识
        /// </summary>
        public string Id
        {
            get => _id;
            set => SetField(ref _id, string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim());
        }
        #endregion
        #region 是否启用启动
        private bool _isStartupEnabled = true;
        /// <summary>
        /// 是否启用启动
        /// </summary>
        public bool IsStartupEnabled
        {
            get => _isStartupEnabled;
            set
            {
                if (SetField(ref _isStartupEnabled, value))
                {
                    LastModifiedAt = DateTime.Now;
                }
            }
        }
        #endregion
        #region 显示顺序
        private int _displayOrder = 1;
        /// <summary>
        /// 显示顺序
        /// </summary>
        public int DisplayOrder
        {
            get => _displayOrder;
            set => SetField(ref _displayOrder, Math.Max(1, value));
        }
        #endregion
        #region 工步名称
        private string _stepName = "工步 1";
        /// <summary>
        /// 工步名称
        /// </summary>
        public string StepName
        {
            get => _stepName;
            set => SetField(ref _stepName, (value ?? string.Empty).Trim());
        }
        #endregion
        #region 步骤集合
        private ObservableCollection<WorkStepOperation> _steps = new();
        /// <summary>
        /// 步骤集合
        /// </summary>
        public ObservableCollection<WorkStepOperation> Steps
        {
            get => _steps;
            set
            {
                if (ReferenceEquals(_steps, value))
                {
                    return;
                }
                _steps = value ?? new ObservableCollection<WorkStepOperation>();
                OnPropertyChanged();
                OnPropertyChanged(nameof(Steps));
            }
        }
        #endregion
        #endregion

        #region 复制方法

        public WorkStepProfile Clone()
        {
            WorkStepProfile clone = new()
            {
                Id = Id,
                WorkStepId = WorkStepId,
                StepName = StepName,
                SchemeStepName = _schemeStepName,
                IsStartupEnabled = IsStartupEnabled,
                DisplayOrder = DisplayOrder,
                LastModifiedAt = LastModifiedAt,
                Steps = new ObservableCollection<WorkStepOperation>(Steps.Select(step => step.Clone())),
                Parameters = new ObservableCollection<SchemeWorkStepParameter>(Parameters.Select(parameter => parameter.Clone()))
            };
            return clone;
        }

        #endregion
    }

    /// <summary>
    /// 工步内的单个步骤。
    /// </summary>
    public sealed class WorkStepOperation : ViewModelProperties
    {
        #region 构造方法

        public WorkStepOperation()
        {
            AttachParameters(_inputParameters);
            AttachParameters(_returnParameters);
        }

        #endregion

        #region 绑定属性
        #region 唯一标识
        private string _id = Guid.NewGuid().ToString("N");
        /// <summary>
        /// 唯一标识
        /// </summary>
        public string Id
        {
            get => _id;
            set => SetField(ref _id, string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim());
        }
        #endregion

        #region 操作对象

        private string _operationObject = "System";

        /// <summary>
        /// 操作对象
        /// </summary>
        public string OperationObject
        {
            get => _operationObject;
            set
            {
                if (SetField(ref _operationObject, (value ?? string.Empty).Trim()))
                {
                    OnPropertyChanged(nameof(DisplayText));
                }
            }
        }

        #endregion

        #region 调用方法

        private string _invokeMethod = "等待";

        /// <summary>
        /// 调用方法
        /// </summary>
        public string InvokeMethod
        {
            get => _invokeMethod;
            set
            {
                if (SetField(ref _invokeMethod, (value ?? string.Empty).Trim()))
                {
                    OnPropertyChanged(nameof(DisplayText));
                }
            }
        }

        #endregion

        #region Lua脚本

        private string _luaScript = string.Empty;

        /// <summary>
        /// Lua脚本
        /// </summary>
        public string LuaScript
        {
            get => _luaScript;
            set
            {
                if (SetField(ref _luaScript, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(DisplayText));
                }
            }
        }

        #endregion

        #region 延时时间

        private int _delayMilliseconds;

        /// <summary>
        /// 延时时间
        /// </summary>
        public int DelayMilliseconds
        {
            get => _delayMilliseconds;
            set
            {
                int normalizedValue = Math.Max(0, value);
                if (SetField(ref _delayMilliseconds, normalizedValue))
                {
                    OnPropertyChanged(nameof(DisplayText));
                }
            }
        }

        #endregion

        #region 备注

        private string _remark = string.Empty;

        /// <summary>
        /// 备注
        /// </summary>
        public string Remark
        {
            get => _remark;
            set
            {
                if (SetField(ref _remark, (value ?? string.Empty).Trim()))
                {
                    OnPropertyChanged(nameof(DisplayText));
                }
            }
        }

        #endregion

        #region 是否选中

        private bool _isChecked;

        /// <summary>
        /// 是否选中
        /// </summary>
        [JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsChecked
        {
            get => _isChecked;
            set => SetField(ref _isChecked, value);
        }

        #endregion

        #region 参数是否修改

        private bool _areParametersModified;

        /// <summary>
        /// 参数是否修改
        /// </summary>
        [JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public bool AreParametersModified
        {
            get => _areParametersModified;
            set => SetField(ref _areParametersModified, value);
        }

        #endregion

        #region 显示顺序

        private int _displayOrder = 1;

        /// <summary>
        /// 显示顺序
        /// </summary>
        [JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public int DisplayOrder
        {
            get => _displayOrder;
            set => SetField(ref _displayOrder, Math.Max(1, value));
        }

        #endregion

        #region 输入参数集合

        private ObservableCollection<WorkStepOperationParameter> _inputParameters = new();

        /// <summary>
        /// 输入参数集合
        /// </summary>
        public ObservableCollection<WorkStepOperationParameter> InputParameters
        {
            get => _inputParameters;
            set
            {
                if (ReferenceEquals(_inputParameters, value))
                {
                    return;
                }

                DetachParameters(_inputParameters);
                _inputParameters = value ?? new ObservableCollection<WorkStepOperationParameter>();
                AttachParameters(_inputParameters);
                OnPropertyChanged();
                OnPropertyChanged(nameof(ParameterCount));
                OnPropertyChanged(nameof(DisplayText));
            }
        }

        #endregion

        #region 返回参数集合

        private ObservableCollection<WorkStepOperationParameter> _returnParameters = new();

        /// <summary>
        /// 返回参数集合
        /// </summary>
        public ObservableCollection<WorkStepOperationParameter> ReturnParameters
        {
            get => _returnParameters;
            set
            {
                if (ReferenceEquals(_returnParameters, value))
                {
                    return;
                }

                DetachParameters(_returnParameters);
                _returnParameters = value ?? new ObservableCollection<WorkStepOperationParameter>();
                AttachParameters(_returnParameters);
                OnPropertyChanged();
                OnPropertyChanged(nameof(ReturnParameterCount));
                OnPropertyChanged(nameof(DisplayText));
            }
        }

        #endregion

        #region 兼容属性

        private string _operationTypeHint = string.Empty;
        private string _deviceIdHint = string.Empty;
        private string _protocolNameHint = string.Empty;
        private string _commandNameHint = string.Empty;
        private string _operationIdHint = string.Empty;

        [JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public string OperationType
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_operationTypeHint))
                {
                    return _operationTypeHint;
                }

                if (WorkStepOperationRuntimeMetadata.IsLuaOperation(this))
                {
                    return SchemeStepEditorState.LuaOperationObjectName;
                }

                if (WorkStepOperationRuntimeMetadata.IsJudgeOperation(this))
                {
                    return SchemeStepEditorState.JudgeOperationObjectName;
                }

                return WorkStepOperationRuntimeMetadata.IsSystemOperation(this) ? "系统" : "设备";
            }
            set
            {
                string normalizedValue = value?.Trim() ?? string.Empty;
                if (!SetField(ref _operationTypeHint, normalizedValue))
                {
                    return;
                }

                if (string.Equals(normalizedValue, SchemeStepEditorState.LuaOperationObjectName, StringComparison.OrdinalIgnoreCase))
                {
                    OperationObject = SchemeStepEditorState.LuaOperationObjectName;
                }
                else if (string.Equals(normalizedValue, SchemeStepEditorState.JudgeOperationObjectName, StringComparison.OrdinalIgnoreCase))
                {
                    OperationObject = SchemeStepEditorState.JudgeOperationObjectName;
                }
                else if (string.Equals(normalizedValue, SchemeStepEditorState.SystemOperationObjectName, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(normalizedValue, "系统", StringComparison.OrdinalIgnoreCase))
                {
                    OperationObject = SchemeStepEditorState.SystemOperationObjectName;
                }
            }
        }

        [JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public string DeviceId
        {
            get => string.IsNullOrWhiteSpace(_deviceIdHint) ? OperationObject : _deviceIdHint;
            set => SetField(ref _deviceIdHint, (value ?? string.Empty).Trim());
        }

        [JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public string ProtocolName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_protocolNameHint))
                {
                    return _protocolNameHint;
                }

                return WorkStepOperationRuntimeMetadata.TryResolveProtocolCommand(this, out string protocolName, out _)
                    ? protocolName
                    : string.Empty;
            }
            set => SetField(ref _protocolNameHint, (value ?? string.Empty).Trim());
        }

        [JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public string CommandName
        {
            get => string.IsNullOrWhiteSpace(_commandNameHint) ? InvokeMethod : _commandNameHint;
            set => SetField(ref _commandNameHint, (value ?? string.Empty).Trim());
        }

        [JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public string OperationId
        {
            get => string.IsNullOrWhiteSpace(_operationIdHint) ? InvokeMethod : _operationIdHint;
            set => SetField(ref _operationIdHint, (value ?? string.Empty).Trim());
        }

        [JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public string ReturnValue
        {
            get => WorkStepOperationRuntimeMetadata.GetReturnParameterKey(WorkStepOperationRuntimeMetadata.GetPrimaryReturnParameter(this));
            set
            {
                string normalizedValue = value?.Trim() ?? string.Empty;
                WorkStepOperationParameter? parameter = WorkStepOperationRuntimeMetadata.GetPrimaryReturnParameter(this);
                if (parameter is null && string.IsNullOrWhiteSpace(normalizedValue))
                {
                    return;
                }

                parameter ??= EnsurePrimaryReturnParameter();
                parameter.ParameterName = normalizedValue;
                parameter.Value = normalizedValue;
                CleanupReturnParameter(parameter);
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayText));
            }
        }

        [JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public bool ShowDataToView
        {
            get => WorkStepOperationRuntimeMetadata.GetPrimaryReturnParameter(this)?.ShowDataToView ?? false;
            set
            {
                WorkStepOperationParameter? parameter = WorkStepOperationRuntimeMetadata.GetPrimaryReturnParameter(this);
                if (parameter is null && !value)
                {
                    return;
                }

                parameter ??= EnsurePrimaryReturnParameter();
                parameter.ShowDataToView = value;
                CleanupReturnParameter(parameter);
                OnPropertyChanged();
            }
        }

        [JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public string ViewDataName
        {
            get => WorkStepOperationRuntimeMetadata.GetPrimaryReturnParameter(this)?.ViewDataName ?? string.Empty;
            set
            {
                string normalizedValue = value?.Trim() ?? string.Empty;
                WorkStepOperationParameter? parameter = WorkStepOperationRuntimeMetadata.GetPrimaryReturnParameter(this);
                if (parameter is null && string.IsNullOrWhiteSpace(normalizedValue))
                {
                    return;
                }

                parameter ??= EnsurePrimaryReturnParameter();
                parameter.ViewDataName = normalizedValue;
                CleanupReturnParameter(parameter);
                OnPropertyChanged();
            }
        }

        [JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public string ViewJudgeType
        {
            get => WorkStepOperationRuntimeMetadata.GetPrimaryReturnParameter(this)?.ViewJudgeType ?? string.Empty;
            set
            {
                string normalizedValue = value?.Trim() ?? string.Empty;
                WorkStepOperationParameter? parameter = WorkStepOperationRuntimeMetadata.GetPrimaryReturnParameter(this);
                if (parameter is null && string.IsNullOrWhiteSpace(normalizedValue))
                {
                    return;
                }

                parameter ??= EnsurePrimaryReturnParameter();
                parameter.ViewJudgeType = normalizedValue;
                CleanupReturnParameter(parameter);
                OnPropertyChanged();
            }
        }

        [JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public string ViewJudgeCondition
        {
            get => WorkStepOperationRuntimeMetadata.GetPrimaryReturnParameter(this)?.ViewJudgeCondition ?? string.Empty;
            set
            {
                string normalizedValue = value?.Trim() ?? string.Empty;
                WorkStepOperationParameter? parameter = WorkStepOperationRuntimeMetadata.GetPrimaryReturnParameter(this);
                if (parameter is null && string.IsNullOrWhiteSpace(normalizedValue))
                {
                    return;
                }

                parameter ??= EnsurePrimaryReturnParameter();
                parameter.ViewJudgeCondition = normalizedValue;
                CleanupReturnParameter(parameter);
                OnPropertyChanged();
            }
        }

        [JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public ObservableCollection<WorkStepOperationParameter> Parameters
        {
            get => InputParameters;
            set => InputParameters = value ?? new ObservableCollection<WorkStepOperationParameter>();
        }

        #endregion

        [JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public int ParameterCount => InputParameters.Count;

        [JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public int ReturnParameterCount => ReturnParameters.Count;

        [JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public string DisplayText
        {
            get
            {
                List<string> returnKeys = ReturnParameters
                    .OrderBy(parameter => parameter.Sequence)
                    .Select(WorkStepOperationRuntimeMetadata.GetReturnParameterKey)
                    .Where(key => !string.IsNullOrWhiteSpace(key))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                string returnText = returnKeys.Count switch
                {
                    0 => string.Empty,
                    1 => $" -> {returnKeys[0]}",
                    _ => $" -> {returnKeys[0]} +{returnKeys.Count - 1}"
                };
                string delayText = DelayMilliseconds <= 0 ? string.Empty : $" / {DelayMilliseconds}ms";
                string remarkText = string.IsNullOrWhiteSpace(Remark) ? string.Empty : $" / {Remark}";
                string parameterText = ParameterCount == 0 ? string.Empty : $" / 参数{ParameterCount}";
                if (WorkStepOperationRuntimeMetadata.IsLuaOperation(this))
                {
                    return $"Lua{delayText}{remarkText}";
                }

                string methodText = string.IsNullOrWhiteSpace(InvokeMethod) ? "步骤" : InvokeMethod;
                string operationObject = string.IsNullOrWhiteSpace(OperationObject) ? "System" : OperationObject;
                string operationPath = WorkStepOperationRuntimeMetadata.IsSystemOperation(this)
                    ? methodText
                    : $"{operationObject}.{methodText}";

                return $"{operationPath}{returnText}{delayText}{remarkText}{parameterText}";
            }
        }

        [System.Text.Json.Serialization.JsonExtensionData]
        public IDictionary<string, JsonElement>? LegacyData { get; set; }

        #endregion

        private WorkStepOperationParameter EnsurePrimaryReturnParameter()
        {
            WorkStepOperationParameter? existingParameter = WorkStepOperationRuntimeMetadata.GetPrimaryReturnParameter(this);
            if (existingParameter is not null)
            {
                return existingParameter;
            }

            WorkStepOperationParameter parameter = new()
            {
                Sequence = ReturnParameters.Count + 1,
                Name = "返回值"
            };
            ReturnParameters.Add(parameter);
            return parameter;
        }

        private void CleanupReturnParameter(WorkStepOperationParameter parameter)
        {
            if (ReturnParameters.Contains(parameter) &&
                string.IsNullOrWhiteSpace(WorkStepOperationRuntimeMetadata.GetReturnParameterKey(parameter)) &&
                !parameter.ShowDataToView &&
                string.IsNullOrWhiteSpace(parameter.ViewDataName) &&
                string.IsNullOrWhiteSpace(parameter.ViewJudgeType) &&
                string.IsNullOrWhiteSpace(parameter.ViewJudgeCondition))
            {
                ReturnParameters.Remove(parameter);
            }
        }

        #region 集合通知

        private void AttachParameters(ObservableCollection<WorkStepOperationParameter> parameters)
        {
            parameters.CollectionChanged += Parameters_CollectionChanged;
            foreach (WorkStepOperationParameter parameter in parameters)
            {
                parameter.PropertyChanged += Parameter_PropertyChanged;
            }
        }

        private void DetachParameters(ObservableCollection<WorkStepOperationParameter> parameters)
        {
            parameters.CollectionChanged -= Parameters_CollectionChanged;
            foreach (WorkStepOperationParameter parameter in parameters)
            {
                parameter.PropertyChanged -= Parameter_PropertyChanged;
            }
        }

        private void Parameters_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems is not null)
            {
                foreach (WorkStepOperationParameter parameter in e.NewItems.OfType<WorkStepOperationParameter>())
                {
                    parameter.PropertyChanged += Parameter_PropertyChanged;
                }
            }

            if (e.OldItems is not null)
            {
                foreach (WorkStepOperationParameter parameter in e.OldItems.OfType<WorkStepOperationParameter>())
                {
                    parameter.PropertyChanged -= Parameter_PropertyChanged;
                }
            }

            OnPropertyChanged(nameof(ParameterCount));
            OnPropertyChanged(nameof(ReturnParameterCount));
            OnPropertyChanged(nameof(DisplayText));
        }

        private void Parameter_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(WorkStepOperationParameter.Name)
                or nameof(WorkStepOperationParameter.Type)
                or nameof(WorkStepOperationParameter.ParameterName)
                or nameof(WorkStepOperationParameter.ValueType)
                or nameof(WorkStepOperationParameter.Sequence)
                or nameof(WorkStepOperationParameter.Value)
                or nameof(WorkStepOperationParameter.Remark)
                or nameof(WorkStepOperationParameter.Description)
                or nameof(WorkStepOperationParameter.ShowDataToView)
                or nameof(WorkStepOperationParameter.ViewDataName)
                or nameof(WorkStepOperationParameter.ViewJudgeType)
                or nameof(WorkStepOperationParameter.ViewJudgeCondition))
            {
                OnPropertyChanged(nameof(DisplayText));
            }
        }

        #endregion

        #region 复制方法

        public WorkStepOperation Clone()
        {
            return new WorkStepOperation
            {
                Id = Id,
                OperationObject = OperationObject,
                InvokeMethod = InvokeMethod,
                LuaScript = LuaScript,
                DelayMilliseconds = DelayMilliseconds,
                Remark = Remark,
                IsChecked = false,
                AreParametersModified = AreParametersModified,
                DisplayOrder = DisplayOrder,
                InputParameters = new ObservableCollection<WorkStepOperationParameter>(InputParameters.Select(parameter => parameter.Clone())),
                ReturnParameters = new ObservableCollection<WorkStepOperationParameter>(ReturnParameters.Select(parameter => parameter.Clone()))
            };
        }

        #endregion
    }

    /// <summary>
    /// 工步步骤调用方法参数。
    /// </summary>
    public sealed class WorkStepOperationParameter : ViewModelProperties
    {
        #region 绑定属性
        #region 唯一标识
        private string _id = Guid.NewGuid().ToString("N");
        /// <summary>
        /// 唯一标识
        /// </summary>
        public string Id
        {
            get => _id;
            set => SetField(ref _id, string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim());
        }
        #endregion
        #region 序号
        private int _sequence = 1;
        /// <summary>
        /// 序号
        /// </summary>
        public int Sequence
        {
            get => _sequence;
            set => SetField(ref _sequence, Math.Max(1, value));
        }
        #endregion
        #region 名称
        private string _name = "设置值";
        /// <summary>
        /// 名称
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetParameterType(value, nameof(Name));
        }
        #endregion

        [JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public string Type
        {
            get => _name;
            set => SetParameterType(value, nameof(Type));
        }
        #region 值
        private string _value = string.Empty;
        /// <summary>
        /// 值
        /// </summary>
        public string Value
        {
            get => _value;
            set => SetField(ref _value, (value ?? string.Empty).Trim());
        }
        #endregion
        #region 参数名称
        private string _parameterName = string.Empty;
        /// <summary>
        /// 参数名称
        /// </summary>
        public string ParameterName
        {
            get => _parameterName;
            set => SetField(ref _parameterName, (value ?? string.Empty).Trim());
        }
        #endregion
        #region 值类型
        private string _valueType = string.Empty;
        /// <summary>
        /// 值类型
        /// </summary>
        public string ValueType
        {
            get => _valueType;
            set => SetField(ref _valueType, (value ?? string.Empty).Trim());
        }
        #endregion
        #region 备注
        private string _remark = string.Empty;
        /// <summary>
        /// 备注
        /// </summary>
        public string Remark
        {
            get => _remark;
            set => SetParameterDescription(value, nameof(Remark));
        }
        #endregion

        [JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public string Description
        {
            get => _remark;
            set => SetParameterDescription(value, nameof(Description));
        }

        [JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public ObservableCollection<string> ValueOptions { get; } = new();

        [JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public bool UsesTextValueEditor =>
            string.Equals(Type, "设置值", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Type, "工步值", StringComparison.OrdinalIgnoreCase);

        [JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public bool UsesComboValueEditor => !UsesTextValueEditor;

        #region 显示到界面
        private bool _showDataToView;
        /// <summary>
        /// 显示到界面
        /// </summary>
        public bool ShowDataToView
        {
            get => _showDataToView;
            set => SetField(ref _showDataToView, value);
        }
        #endregion
        #region 显示名称
        private string _viewDataName = string.Empty;
        /// <summary>
        /// 显示名称
        /// </summary>
        public string ViewDataName
        {
            get => _viewDataName;
            set => SetField(ref _viewDataName, (value ?? string.Empty).Trim());
        }
        #endregion
        #region 判断类型
        private string _viewJudgeType = string.Empty;
        /// <summary>
        /// 判断类型
        /// </summary>
        public string ViewJudgeType
        {
            get => _viewJudgeType;
            set => SetField(ref _viewJudgeType, (value ?? string.Empty).Trim());
        }
        #endregion
        #region 判断条件
        private string _viewJudgeCondition = string.Empty;
        /// <summary>
        /// 判断条件
        /// </summary>
        public string ViewJudgeCondition
        {
            get => _viewJudgeCondition;
            set => SetField(ref _viewJudgeCondition, (value ?? string.Empty).Trim());
        }
        #endregion

        #endregion

        #region 属性别名方法

        private void SetParameterType(string? value, string propertyName)
        {
            string normalizedValue = string.IsNullOrWhiteSpace(value) ? "设置值" : value.Trim();
            if (!SetField(ref _name, normalizedValue, true, propertyName))
            {
                return;
            }

            OnPropertyChanged(propertyName == nameof(Name) ? nameof(Type) : nameof(Name));
            OnPropertyChanged(nameof(UsesTextValueEditor));
            OnPropertyChanged(nameof(UsesComboValueEditor));
        }

        private void SetParameterDescription(string? value, string propertyName)
        {
            string normalizedValue = value?.Trim() ?? string.Empty;
            if (!SetField(ref _remark, normalizedValue, true, propertyName))
            {
                return;
            }

            OnPropertyChanged(propertyName == nameof(Remark) ? nameof(Description) : nameof(Remark));
        }

        #endregion

        #region 复制方法

        public WorkStepOperationParameter Clone()
        {
            return new WorkStepOperationParameter
            {
                Id = Id,
                Sequence = Sequence,
                Name = Name,
                ParameterName = ParameterName,
                ValueType = ValueType,
                Value = Value,
                Remark = Remark,
                ShowDataToView = ShowDataToView,
                ViewDataName = ViewDataName,
                ViewJudgeType = ViewJudgeType,
                ViewJudgeCondition = ViewJudgeCondition
            };
        }

        #endregion
    }
    /// <summary>
    /// 方案工步参数。
    /// </summary>
    public sealed class SchemeWorkStepParameter : ViewModelProperties
    {
        private static readonly string[] DefaultJudgeTypeOptions =
        {
        "等于"
    };
        #region 绑定属性
        #region 唯一标识
        private string _id = Guid.NewGuid().ToString("N");
        /// <summary>
        /// 唯一标识
        /// </summary>
        public string Id
        {
            get => _id;
            set => SetField(ref _id, string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim());
        }
        #endregion
        #region 来源操作标识
        private string _sourceOperationId = string.Empty;
        /// <summary>
        /// 来源操作标识
        /// </summary>
        public string SourceOperationId
        {
            get => _sourceOperationId;
            set => SetField(ref _sourceOperationId, (value ?? string.Empty).Trim());
        }
        #endregion
        #region 来源参数标识
        private string _sourceParameterId = string.Empty;
        /// <summary>
        /// 来源参数标识
        /// </summary>
        public string SourceParameterId
        {
            get => _sourceParameterId;
            set => SetField(ref _sourceParameterId, (value ?? string.Empty).Trim());
        }
        #endregion
        #region 参数名称
        private string _parameterName = "参数";
        /// <summary>
        /// 参数名称
        /// </summary>
        public string ParameterName
        {
            get => _parameterName;
            set => SetField(ref _parameterName, string.IsNullOrWhiteSpace(value) ? "参数" : value.Trim());
        }
        #endregion
        #region 参数类型
        private string _parameterType = "设置值";
        /// <summary>
        /// 参数类型
        /// </summary>
        public string ParameterType
        {
            get => _parameterType;
            set
            {
                string normalizedValue = string.IsNullOrWhiteSpace(value) ? "设置值" : value.Trim();
                if (!SetField(ref _parameterType, normalizedValue, true, nameof(ParameterType)))
                {
                    return;
                }

                if (UsesJudgeType && string.IsNullOrWhiteSpace(_judgeType))
                {
                    _judgeType = "等于";
                    OnPropertyChanged(nameof(JudgeType));
                }
                else if (!UsesJudgeType && !string.IsNullOrWhiteSpace(_judgeType))
                {
                    _judgeType = string.Empty;
                    OnPropertyChanged(nameof(JudgeType));
                }

                OnPropertyChanged(nameof(UsesJudgeType));
            }
        }
        #endregion
        #region 判断类型
        private string _judgeType = string.Empty;
        /// <summary>
        /// 判断类型
        /// </summary>
        public string JudgeType
        {
            get => _judgeType;
            set => SetField(ref _judgeType, (value ?? string.Empty).Trim());
        }
        #endregion
        #region 判断条件
        private string _judgeCondition = string.Empty;
        /// <summary>
        /// 判断条件
        /// </summary>
        public string JudgeCondition
        {
            get => _judgeCondition;
            set => SetField(ref _judgeCondition, (value ?? string.Empty).Trim());
        }
        #endregion

        [JsonIgnore]
        public bool UsesJudgeType => string.Equals(ParameterType, "判断值", StringComparison.OrdinalIgnoreCase);

        [JsonIgnore]
        public ObservableCollection<string> JudgeTypeOptions { get; } = new(DefaultJudgeTypeOptions);

        public void ReplaceJudgeTypeOptions(IEnumerable<string> options)
        {
            List<string> normalizedOptions = options
                .Where(option => !string.IsNullOrWhiteSpace(option))
                .Select(option => option.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedOptions.Count == 0)
            {
                normalizedOptions = DefaultJudgeTypeOptions.ToList();
            }

            if (JudgeTypeOptions.SequenceEqual(normalizedOptions, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            JudgeTypeOptions.Clear();
            foreach (string option in normalizedOptions)
            {
                JudgeTypeOptions.Add(option);
            }
        }

        #endregion

        #region 复制方法

        public SchemeWorkStepParameter Clone()
        {
            return new SchemeWorkStepParameter
            {
                Id = Id,
                SourceOperationId = SourceOperationId,
                SourceParameterId = SourceParameterId,
                ParameterName = ParameterName,
                ParameterType = ParameterType,
                JudgeType = JudgeType,
                JudgeCondition = JudgeCondition
            };
        }

        #endregion
    }
}
