using ControlLibrary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;

namespace Module.Business.Features.SchemeConfiguration
{
    /// <summary>
    /// 业务方案实体，包含工步集合。
    /// </summary>
    public sealed class SchemeProfile : ViewModelProperties
    {
        #region 私有字段

        private string _id = Guid.NewGuid().ToString("N");
        private string _schemeName = "方案 1";
        private DateTime _lastModifiedAt = DateTime.Now;
        private ObservableCollection<SchemeWorkStepItem> _steps = new();

        #endregion

        #region 构造函数

        public SchemeProfile()
        {
            AttachSteps(_steps);
        }

        #endregion

        #region 属性定义

        /// <summary>
        /// 方案唯一标识。
        /// </summary>
        public string Id
        {
            get => _id;
            set => SetField(ref _id, string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim());
        }

        /// <summary>
        /// 方案名称。
        /// </summary>
        public string SchemeName
        {
            get => _schemeName;
            set
            {
                if (SetField(ref _schemeName, value ?? string.Empty, true))
                {
                    MarkModified();
                }
            }
        }

        /// <summary>
        /// 最后修改时间。
        /// </summary>
        public DateTime LastModifiedAt
        {
            get => _lastModifiedAt;
            set
            {
                DateTime normalizedValue = value == default ? DateTime.Now : value;
                if (SetField(ref _lastModifiedAt, normalizedValue))
                {
                    OnPropertyChanged(nameof(LastModifiedText));
                }
            }
        }

        /// <summary>
        /// 工步项集合。
        /// </summary>
        public ObservableCollection<SchemeWorkStepItem> Steps
        {
            get => _steps;
            set
            {
                if (ReferenceEquals(_steps, value))
                {
                    return;
                }

                DetachSteps(_steps);
                _steps = value ?? new ObservableCollection<SchemeWorkStepItem>();
                AttachSteps(_steps);
                OnPropertyChanged();
                OnPropertyChanged(nameof(StepCount));
                MarkModified();
            }
        }

        /// <summary>
        /// 工步数量。
        /// </summary>
        [JsonIgnore]
        public int StepCount => Steps.Count;

        /// <summary>
        /// 最后修改时间文本。
        /// </summary>
        [JsonIgnore]
        public string LastModifiedText => $"最后修改：{LastModifiedAt:yyyy-MM-dd HH:mm:ss}";

        #endregion

        #region 集合变更通知

        private void AttachSteps(ObservableCollection<SchemeWorkStepItem> steps)
        {
            steps.CollectionChanged += Steps_CollectionChanged;
            foreach (SchemeWorkStepItem step in steps)
            {
                step.PropertyChanged += Step_PropertyChanged;
            }

            RefreshStepDisplayOrders(steps);
        }

        private void DetachSteps(ObservableCollection<SchemeWorkStepItem> steps)
        {
            steps.CollectionChanged -= Steps_CollectionChanged;
            foreach (SchemeWorkStepItem step in steps)
            {
                step.PropertyChanged -= Step_PropertyChanged;
            }
        }

        private void Steps_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Move)
            {
                if (sender is ObservableCollection<SchemeWorkStepItem> movedSteps)
                {
                    RefreshStepDisplayOrders(movedSteps);
                }

                OnPropertyChanged(nameof(Steps));
                MarkModified();
                return;
            }

            if (e.NewItems is not null)
            {
                foreach (SchemeWorkStepItem step in e.NewItems.OfType<SchemeWorkStepItem>())
                {
                    step.PropertyChanged += Step_PropertyChanged;
                }
            }

            if (e.OldItems is not null)
            {
                foreach (SchemeWorkStepItem step in e.OldItems.OfType<SchemeWorkStepItem>())
                {
                    step.PropertyChanged -= Step_PropertyChanged;
                }
            }

            if (sender is ObservableCollection<SchemeWorkStepItem> changedSteps)
            {
                RefreshStepDisplayOrders(changedSteps);
            }

            OnPropertyChanged(nameof(Steps));
            OnPropertyChanged(nameof(StepCount));
            MarkModified();
        }

        private void Step_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(SchemeWorkStepItem.StepName)
                or nameof(SchemeWorkStepItem.IsStartupEnabled)
                or nameof(SchemeWorkStepItem.IsReTestEnabled)
                or nameof(SchemeWorkStepItem.ReTestCount)
                or nameof(SchemeWorkStepItem.IsConfirmReTest)
                or nameof(SchemeWorkStepItem.Operations)
                or nameof(SchemeWorkStepItem.LastModifiedAt)
                or nameof(SchemeWorkStepItem.LastModifiedText))
            {
                OnPropertyChanged(nameof(Steps));
                MarkModified();
            }
        }

        private static void RefreshStepDisplayOrders(ObservableCollection<SchemeWorkStepItem> steps)
        {
            for (int index = 0; index < steps.Count; index++)
            {
                steps[index].Num = index + 1;
            }
        }

        #endregion

        #region 克隆与修改

        /// <summary>
        /// 克隆当前方案。
        /// </summary>
        public SchemeProfile Clone()
        {
            return new SchemeProfile
            {
                Id = Id,
                SchemeName = SchemeName,
                LastModifiedAt = LastModifiedAt,
                Steps = new ObservableCollection<SchemeWorkStepItem>(Steps.Select(step => step.Clone()))
            };
        }

        /// <summary>
        /// 标记方案已修改。
        /// </summary>
        public void MarkModified()
        {
            LastModifiedAt = DateTime.Now;
        }

        #endregion
    }

    /// <summary>
    /// 工步实体，方案中的工步项，包含步骤集合。
    /// </summary>
    public sealed class SchemeWorkStepItem : ViewModelProperties
    {
        #region 私有字段

        private string _id = Guid.NewGuid().ToString("N");
        private int _num = 1;
        private string _stepName = "工步 1";
        private bool _isStartupEnabled = true;
        private bool _isReTestEnabled;
        private int _reTestCount = 1;
        private bool _isConfirmReTest;
        private DateTime _lastModifiedAt = DateTime.Now;
        private ObservableCollection<WorkStepOperation> _operations = new();

        #endregion

        #region 构造函数

        public SchemeWorkStepItem()
        {
            AttachOperations(_operations);
        }

        #endregion

        #region 属性定义

        /// <summary>
        /// 工步唯一标识。
        /// </summary>
        public string Id
        {
            get => _id;
            set => SetField(ref _id, string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim());
        }

        /// <summary>
        /// 序号。
        /// </summary>
        public int Num
        {
            get => _num;
            set => SetField(ref _num, Math.Max(1, value));
        }

        /// <summary>
        /// 工步名称。
        /// </summary>
        public string StepName
        {
            get => _stepName;
            set
            {
                if (SetField(ref _stepName, value ?? string.Empty, true))
                {
                    LastModifiedAt = DateTime.Now;
                }
            }
        }

        /// <summary>
        /// 是否启用。
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

        /// <summary>
        /// 是否NG重测。
        /// </summary>
        public bool IsReTestEnabled
        {
            get => _isReTestEnabled;
            set
            {
                if (SetField(ref _isReTestEnabled, value))
                {
                    LastModifiedAt = DateTime.Now;
                }
            }
        }

        /// <summary>
        /// 重测次数。
        /// </summary>
        public int ReTestCount
        {
            get => _reTestCount;
            set
            {
                int normalizedValue = Math.Max(1, value);
                if (SetField(ref _reTestCount, normalizedValue))
                {
                    LastModifiedAt = DateTime.Now;
                }
            }
        }

        /// <summary>
        /// 弹框确认是否重测。
        /// </summary>
        public bool IsConfirmReTest
        {
            get => _isConfirmReTest;
            set
            {
                if (SetField(ref _isConfirmReTest, value))
                {
                    LastModifiedAt = DateTime.Now;
                }
            }
        }

        /// <summary>
        /// 最后修改时间。
        /// </summary>
        public DateTime LastModifiedAt
        {
            get => _lastModifiedAt;
            set
            {
                DateTime normalizedValue = value == default ? DateTime.Now : value;
                if (SetField(ref _lastModifiedAt, normalizedValue))
                {
                    OnPropertyChanged(nameof(LastModifiedText));
                }
            }
        }

        /// <summary>
        /// 步骤集合。
        /// </summary>
        public ObservableCollection<WorkStepOperation> Operations
        {
            get => _operations;
            set
            {
                if (ReferenceEquals(_operations, value))
                {
                    return;
                }

                DetachOperations(_operations);
                _operations = value ?? new ObservableCollection<WorkStepOperation>();
                AttachOperations(_operations);
                OnPropertyChanged();
                LastModifiedAt = DateTime.Now;
            }
        }

        /// <summary>
        /// 最后修改时间文本。
        /// </summary>
        [JsonIgnore]
        public string LastModifiedText => $"最后修改：{LastModifiedAt:yyyy-MM-dd HH:mm:ss}";

        #endregion

        #region 集合变更通知

        private void AttachOperations(ObservableCollection<WorkStepOperation> operations)
        {
            operations.CollectionChanged += Operations_CollectionChanged;
            foreach (WorkStepOperation operation in operations)
            {
                operation.PropertyChanged += Operation_PropertyChanged;
            }

            RefreshOperationDisplayOrders(operations);
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

                LastModifiedAt = DateTime.Now;
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

            LastModifiedAt = DateTime.Now;
        }

        private void Operation_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(WorkStepOperation.OperationObjectName)
                or nameof(WorkStepOperation.PCommandName)
                or nameof(WorkStepOperation.Parameters)
                or nameof(WorkStepOperation.ReturnValues)
                or nameof(WorkStepOperation.LuaScript)
                or nameof(WorkStepOperation.DelayMilliseconds)
                or nameof(WorkStepOperation.DisplayText)
                or nameof(WorkStepOperation.IsEditParameter))
            {
                LastModifiedAt = DateTime.Now;
            }
        }

        private static void RefreshOperationDisplayOrders(ObservableCollection<WorkStepOperation> operations)
        {
            for (int index = 0; index < operations.Count; index++)
            {
                operations[index].Num = index + 1;
            }
        }

        #endregion

        #region 克隆方法

        /// <summary>
        /// 克隆当前工步。
        /// </summary>
        public SchemeWorkStepItem Clone()
        {
            return new SchemeWorkStepItem
            {
                Id = Id,
                Num = Num,
                StepName = StepName,
                IsStartupEnabled = IsStartupEnabled,
                IsReTestEnabled = IsReTestEnabled,
                ReTestCount = ReTestCount,
                IsConfirmReTest = IsConfirmReTest,
                LastModifiedAt = LastModifiedAt,
                Operations = new ObservableCollection<WorkStepOperation>(Operations.Select(operation => operation.Clone()))
            };
        }

        #endregion
    }

    /// <summary>
    /// 步骤实体，工步中的操作步骤。
    /// </summary>
    public sealed class WorkStepOperation : ViewModelProperties
    {
        #region 私有字段

        private string _id = Guid.NewGuid().ToString("N");
        private int _num = 1;
        private string _operationObjectName = "System";
        private string _pCommandName = string.Empty;
        private ObservableCollection<InputParameter> _parameters = new();
        private ObservableCollection<ReturnValue> _returnValues = new();
        private string _luaScript = string.Empty;
        private int _delayMilliseconds;
        private bool _isEditParameter;

        #endregion

        #region 构造函数

        public WorkStepOperation()
        {
            AttachParameters(_parameters);
            AttachReturnValues(_returnValues);
        }

        #endregion

        #region 属性定义

        /// <summary>
        /// 步骤唯一标识。
        /// </summary>
        public string Id
        {
            get => _id;
            set => SetField(ref _id, string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim());
        }

        /// <summary>
        /// 序号。
        /// </summary>
        public int Num
        {
            get => _num;
            set => SetField(ref _num, Math.Max(1, value));
        }

        /// <summary>
        /// 操作对象名称（如"System"或具体设备名称）。
        /// </summary>
        public string OperationObjectName
        {
            get => _operationObjectName;
            set
            {
                if (SetField(ref _operationObjectName, value ?? string.Empty, true))
                {
                    OnPropertyChanged(nameof(DisplayText));
                }
            }
        }

        /// <summary>
        /// 协议名称/命令/调用方法。
        /// </summary>
        public string PCommandName
        {
            get => _pCommandName;
            set
            {
                if (SetField(ref _pCommandName, value ?? string.Empty, true))
                {
                    OnPropertyChanged(nameof(DisplayText));
                }
            }
        }

        /// <summary>
        /// 输入参数集合。
        /// </summary>
        public ObservableCollection<InputParameter> Parameters
        {
            get => _parameters;
            set
            {
                if (ReferenceEquals(_parameters, value))
                {
                    return;
                }

                DetachParameters(_parameters);
                _parameters = value ?? new ObservableCollection<InputParameter>();
                AttachParameters(_parameters);
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayText));
            }
        }

        /// <summary>
        /// 返回值集合。
        /// </summary>
        public ObservableCollection<ReturnValue> ReturnValues
        {
            get => _returnValues;
            set
            {
                if (ReferenceEquals(_returnValues, value))
                {
                    return;
                }

                DetachReturnValues(_returnValues);
                _returnValues = value ?? new ObservableCollection<ReturnValue>();
                AttachReturnValues(_returnValues);
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayText));
            }
        }

        /// <summary>
        /// Lua 脚本内容。
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

        /// <summary>
        /// 延时时间（毫秒）。
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

        /// <summary>
        /// 是否修改了参数。
        /// </summary>
        [JsonIgnore]
        public bool IsEditParameter
        {
            get => _isEditParameter;
            set => SetField(ref _isEditParameter, value);
        }

        /// <summary>
        /// 用于界面显示的摘要文本。
        /// </summary>
        [JsonIgnore]
        public string DisplayText
        {
            get
            {
                if (IsLuaOperation())
                {
                    string delayText = DelayMilliseconds <= 0 ? string.Empty : $" / {DelayMilliseconds}ms";
                    return $"Lua{delayText}";
                }

                string returnText = ReturnValues.Count == 0 ? string.Empty : $" -> {string.Join(", ", ReturnValues.Select(rv => rv.ReturnParameterName))}";
                string delayText2 = DelayMilliseconds <= 0 ? string.Empty : $" / {DelayMilliseconds}ms";
                string parameterText = Parameters.Count == 0 ? string.Empty : $" / 参数{Parameters.Count}";
                string commandText = string.IsNullOrWhiteSpace(PCommandName) ? string.Empty : $".{PCommandName}";
                string operationPath = $"{OperationObjectName}{commandText}";

                return $"{operationPath}{returnText}{delayText2}{parameterText}";
            }
        }

        #endregion

        #region 集合变更通知

        private void AttachParameters(ObservableCollection<InputParameter> parameters)
        {
            parameters.CollectionChanged += Parameters_CollectionChanged;
            foreach (InputParameter parameter in parameters)
            {
                parameter.PropertyChanged += Parameter_PropertyChanged;
            }
        }

        private void DetachParameters(ObservableCollection<InputParameter> parameters)
        {
            parameters.CollectionChanged -= Parameters_CollectionChanged;
            foreach (InputParameter parameter in parameters)
            {
                parameter.PropertyChanged -= Parameter_PropertyChanged;
            }
        }

        private void Parameters_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems is not null)
            {
                foreach (InputParameter parameter in e.NewItems.OfType<InputParameter>())
                {
                    parameter.PropertyChanged += Parameter_PropertyChanged;
                }
            }

            if (e.OldItems is not null)
            {
                foreach (InputParameter parameter in e.OldItems.OfType<InputParameter>())
                {
                    parameter.PropertyChanged -= Parameter_PropertyChanged;
                }
            }

            if (sender is ObservableCollection<InputParameter> changedParameters)
            {
                RefreshParameterDisplayOrders(changedParameters);
            }

            OnPropertyChanged(nameof(DisplayText));
        }

        private void Parameter_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(InputParameter.ParameterName)
                or nameof(InputParameter.ParameterType)
                or nameof(InputParameter.Value))
            {
                OnPropertyChanged(nameof(DisplayText));
            }
        }

        private void AttachReturnValues(ObservableCollection<ReturnValue> returnValues)
        {
            returnValues.CollectionChanged += ReturnValues_CollectionChanged;
            foreach (ReturnValue returnValue in returnValues)
            {
                returnValue.PropertyChanged += ReturnValue_PropertyChanged;
            }
        }

        private void DetachReturnValues(ObservableCollection<ReturnValue> returnValues)
        {
            returnValues.CollectionChanged -= ReturnValues_CollectionChanged;
            foreach (ReturnValue returnValue in returnValues)
            {
                returnValue.PropertyChanged -= ReturnValue_PropertyChanged;
            }
        }

        private void ReturnValues_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems is not null)
            {
                foreach (ReturnValue returnValue in e.NewItems.OfType<ReturnValue>())
                {
                    returnValue.PropertyChanged += ReturnValue_PropertyChanged;
                }
            }

            if (e.OldItems is not null)
            {
                foreach (ReturnValue returnValue in e.OldItems.OfType<ReturnValue>())
                {
                    returnValue.PropertyChanged -= ReturnValue_PropertyChanged;
                }
            }

            if (sender is ObservableCollection<ReturnValue> changedReturnValues)
            {
                RefreshReturnValueDisplayOrders(changedReturnValues);
            }

            OnPropertyChanged(nameof(DisplayText));
        }

        private void ReturnValue_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ReturnValue.ReturnParameterName)
                or nameof(ReturnValue.IsShowView))
            {
                OnPropertyChanged(nameof(DisplayText));
            }
        }

        private static void RefreshParameterDisplayOrders(ObservableCollection<InputParameter> parameters)
        {
            for (int index = 0; index < parameters.Count; index++)
            {
                parameters[index].Num = index + 1;
            }
        }

        private static void RefreshReturnValueDisplayOrders(ObservableCollection<ReturnValue> returnValues)
        {
            for (int index = 0; index < returnValues.Count; index++)
            {
                returnValues[index].Num = index + 1;
            }
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 判断是否为 Lua 操作。
        /// </summary>
        private bool IsLuaOperation()
        {
            return string.Equals(OperationObjectName?.Trim(), "Lua", StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region 克隆方法

        /// <summary>
        /// 克隆当前步骤。
        /// </summary>
        public WorkStepOperation Clone()
        {
            return new WorkStepOperation
            {
                Id = Id,
                Num = Num,
                OperationObjectName = OperationObjectName,
                PCommandName = PCommandName,
                LuaScript = LuaScript,
                DelayMilliseconds = DelayMilliseconds,
                IsEditParameter = IsEditParameter,
                Parameters = new ObservableCollection<InputParameter>(Parameters.Select(parameter => parameter.Clone())),
                ReturnValues = new ObservableCollection<ReturnValue>(ReturnValues.Select(returnValue => returnValue.Clone()))
            };
        }

        #endregion
    }

    /// <summary>
    /// 输入参数实体。
    /// </summary>
    public sealed class InputParameter : ViewModelProperties
    {
        #region 私有字段

        private string _id = Guid.NewGuid().ToString("N");
        private int _num = 1;
        private string _parameterName = string.Empty;
        private string _parameterType = "设置值";
        private string _value = string.Empty;

        #endregion

        #region 属性定义

        /// <summary>
        /// 参数唯一标识。
        /// </summary>
        public string Id
        {
            get => _id;
            set => SetField(ref _id, string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim());
        }

        /// <summary>
        /// 序号。
        /// </summary>
        public int Num
        {
            get => _num;
            set => SetField(ref _num, Math.Max(1, value));
        }

        /// <summary>
        /// 参数名称。
        /// </summary>
        public string ParameterName
        {
            get => _parameterName;
            set => SetField(ref _parameterName, value ?? string.Empty, true);
        }

        /// <summary>
        /// 参数类型（设置值/返回值/全局值）。
        /// </summary>
        public string ParameterType
        {
            get => _parameterType;
            set => SetField(ref _parameterType, string.IsNullOrWhiteSpace(value) ? "设置值" : value.Trim(), true);
        }

        /// <summary>
        /// 设置值或者返回值、全局值名称。
        /// </summary>
        public string Value
        {
            get => _value;
            set => SetField(ref _value, value ?? string.Empty, true);
        }

        #endregion

        #region 克隆方法

        /// <summary>
        /// 克隆当前输入参数。
        /// </summary>
        public InputParameter Clone()
        {
            return new InputParameter
            {
                Id = Id,
                Num = Num,
                ParameterName = ParameterName,
                ParameterType = ParameterType,
                Value = Value
            };
        }

        #endregion
    }

    /// <summary>
    /// 返回值实体。
    /// </summary>
    public sealed class ReturnValue : ViewModelProperties
    {
        #region 私有字段

        private string _id = Guid.NewGuid().ToString("N");
        private int _num = 1;
        private string _returnParameterName = string.Empty;
        private bool _isShowView;
        private string _judgeType = string.Empty;
        private string _judgeSymbols = string.Empty;
        private string _judgeValue = string.Empty;
        private string _originalUnit = string.Empty;
        private string _showUnit = string.Empty;
        private int _decimalPlaces;

        #endregion

        #region 属性定义

        /// <summary>
        /// 返回值唯一标识。
        /// </summary>
        public string Id
        {
            get => _id;
            set => SetField(ref _id, string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim());
        }

        /// <summary>
        /// 序号。
        /// </summary>
        public int Num
        {
            get => _num;
            set => SetField(ref _num, Math.Max(1, value));
        }

        /// <summary>
        /// 返回值参数名称。
        /// </summary>
        public string ReturnParameterName
        {
            get => _returnParameterName;
            set => SetField(ref _returnParameterName, value ?? string.Empty, true);
        }

        /// <summary>
        /// 是否显示到界面。
        /// </summary>
        public bool IsShowView
        {
            get => _isShowView;
            set => SetField(ref _isShowView, value);
        }

        /// <summary>
        /// 判断的类型（支持多个显示到界面的数据判断条件统一一个类型，只需要配置一个判断条件）。
        /// </summary>
        public string JudgeType
        {
            get => _judgeType;
            set => SetField(ref _judgeType, value ?? string.Empty, true);
        }

        /// <summary>
        /// 判断的符号。
        /// </summary>
        public string JudgeSymbols
        {
            get => _judgeSymbols;
            set => SetField(ref _judgeSymbols, value ?? string.Empty, true);
        }

        /// <summary>
        /// 判断值。
        /// </summary>
        public string JudgeValue
        {
            get => _judgeValue;
            set => SetField(ref _judgeValue, value ?? string.Empty, true);
        }

        /// <summary>
        /// 原始单位。
        /// </summary>
        public string OriginalUnit
        {
            get => _originalUnit;
            set => SetField(ref _originalUnit, value ?? string.Empty, true);
        }

        /// <summary>
        /// 显示单位。
        /// </summary>
        public string ShowUnit
        {
            get => _showUnit;
            set => SetField(ref _showUnit, value ?? string.Empty, true);
        }

        /// <summary>
        /// 小数位数。
        /// </summary>
        public int DecimalPlaces
        {
            get => _decimalPlaces;
            set => SetField(ref _decimalPlaces, Math.Max(0, value));
        }

        #endregion

        #region 克隆方法

        /// <summary>
        /// 克隆当前返回值。
        /// </summary>
        public ReturnValue Clone()
        {
            return new ReturnValue
            {
                Id = Id,
                Num = Num,
                ReturnParameterName = ReturnParameterName,
                IsShowView = IsShowView,
                JudgeType = JudgeType,
                JudgeSymbols = JudgeSymbols,
                JudgeValue = JudgeValue,
                OriginalUnit = OriginalUnit,
                ShowUnit = ShowUnit,
                DecimalPlaces = DecimalPlaces
            };
        }

        #endregion
    }
}
