using ControlLibrary;
using Module.Business.Features.OperationEditing.ViewModels.PresentationModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Module.Business.Features.Scheme.ViewModels.PresentationModels
{
    /// <summary>
    /// 业务方案，承载工作流程和执行顺序的顶层容器
    /// </summary>
    public sealed class SchemeProfile : ViewModelProperties
    {
        #region 私有字段

        private string _id = Guid.NewGuid().ToString("N");
        private string _schemeName = "方案 1";
        private DateTime _lastModifiedAt = DateTime.Now;
        private ObservableCollection<SchemeWorkStepItem> _steps = new();
        private bool _isModified;

        #endregion

        #region 构造函数

        public SchemeProfile()
        {
            AttachSteps(_steps);
        }

        #endregion

        #region 属性

        /// <summary>
        /// 方案唯一标识
        /// </summary>
        public string Id
        {
            get => _id;
            set => SetField(ref _id, string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim());
        }

        /// <summary>
        /// 方案名称
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
        /// 最后修改时间
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
        /// 工步项集合
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

        [JsonIgnore]
        public int StepCount => Steps.Count;

        [JsonIgnore]
        public string LastModifiedText => $"最后修改：{LastModifiedAt:yyyy-MM-dd HH:mm:ss}";

        /// <summary>
        /// 当前方案内容是否存在尚未保存的修改；该状态仅用于页面保存流程，不写入配置文件。
        /// </summary>
        [JsonIgnore]
        public bool IsModified => _isModified;

        #endregion

        #region 集合通知

        private void AttachSteps(ObservableCollection<SchemeWorkStepItem> steps)
        {
            steps.CollectionChanged += Steps_CollectionChanged;
            foreach (SchemeWorkStepItem step in steps)
            {
                step.PropertyChanged += Step_PropertyChanged;
            }

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
                or nameof(SchemeWorkStepItem.Operations))
            {
                OnPropertyChanged(nameof(Steps));
                MarkModified();
            }
        }

        #endregion

        #region 克隆

        public SchemeProfile Clone()
        {
            SchemeProfile clone = new()
            {
                Id = Id,
                SchemeName = SchemeName,
                LastModifiedAt = LastModifiedAt,
                Steps = new ObservableCollection<SchemeWorkStepItem>(Steps.Select(step => step.Clone()))
            };

            if (!IsModified)
            {
                clone.AcceptChanges();
            }

            return clone;
        }

        #endregion

        #region 修改时间戳

        /// <summary>
        /// 标记方案存在尚未保存的修改。最后修改时间只在配置成功保存时更新。
        /// </summary>
        public void MarkModified()
        {
            if (_isModified)
            {
                return;
            }

            _isModified = true;
            OnPropertyChanged(nameof(IsModified));
        }

        /// <summary>
        /// 使用本次成功保存的时间提交修改，并清除未保存标记。
        /// </summary>
        public void AcceptChanges(DateTime savedAt)
        {
            LastModifiedAt = savedAt;
            AcceptChanges();
        }

        /// <summary>
        /// 保留当前最后修改时间，仅清除加载、克隆或保存后的临时修改标记。
        /// </summary>
        public void AcceptChanges()
        {
            if (!_isModified)
            {
                return;
            }

            _isModified = false;
            OnPropertyChanged(nameof(IsModified));
        }

        #endregion
    }

    /// <summary>
    /// 方案中的工步项
    /// </summary>
    public sealed class SchemeWorkStepItem : ViewModelProperties
    {
        #region 私有字段

        private string _id = Guid.NewGuid().ToString("N");
        private int _num = 1;
        private string _stepName = string.Empty;
        private bool _isStartupEnabled = true;
        private bool _isReTestEnabled;
        private int _reTestCount = 1;
        private bool _isConfirmReTest;
        private ObservableCollection<WorkStepOperation> _operations = new();
        private readonly HashSet<WorkStepOperation> _trackedOperations = new();

        #endregion

        #region 构造函数

        public SchemeWorkStepItem()
        {
            AttachOperations(_operations);
        }

        #endregion

        #region 属性

        /// <summary>
        /// 工步唯一标识
        /// </summary>
        public string Id
        {
            get => _id;
            set => SetField(ref _id, string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim());
        }

        /// <summary>
        /// 序号
        /// </summary>
        public int Num
        {
            get => _num;
            set => SetField(ref _num, Math.Max(1, value));
        }

        /// <summary>
        /// 工步名称
        /// </summary>
        public string StepName
        {
            get => _stepName;
            set => SetField(ref _stepName, value ?? string.Empty, true);
        }

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsStartupEnabled
        {
            get => _isStartupEnabled;
            set => SetField(ref _isStartupEnabled, value);
        }

        /// <summary>
        /// 是否NG重测
        /// </summary>
        public bool IsReTestEnabled
        {
            get => _isReTestEnabled;
            set => SetField(ref _isReTestEnabled, value);
        }

        /// <summary>
        /// 重测次数
        /// </summary>
        public int ReTestCount
        {
            get => _reTestCount;
            set => SetField(ref _reTestCount, Math.Max(1, value));
        }

        /// <summary>
        /// 弹框确认是否重测
        /// </summary>
        public bool IsConfirmReTest
        {
            get => _isConfirmReTest;
            set => SetField(ref _isConfirmReTest, value);
        }

        /// <summary>
        /// 步骤集合
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
                OnPropertyChanged(nameof(OperationCount));
            }
        }

        [JsonIgnore]
        public int OperationCount => Operations.Count;

        #endregion

        #region 集合通知

        private void AttachOperations(ObservableCollection<WorkStepOperation> operations)
        {
            operations.CollectionChanged += Operations_CollectionChanged;
            foreach (WorkStepOperation operation in operations)
            {
                if (_trackedOperations.Add(operation))
                {
                    operation.PropertyChanged += Operation_PropertyChanged;
                }
            }

            RefreshOperationNums(operations);
        }

        private void DetachOperations(ObservableCollection<WorkStepOperation> operations)
        {
            operations.CollectionChanged -= Operations_CollectionChanged;
            foreach (WorkStepOperation operation in _trackedOperations)
            {
                operation.PropertyChanged -= Operation_PropertyChanged;
            }

            _trackedOperations.Clear();
        }

        private void Operations_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (WorkStepOperation operation in _trackedOperations)
                {
                    operation.PropertyChanged -= Operation_PropertyChanged;
                }

                _trackedOperations.Clear();
            }

            if (e.NewItems is not null)
            {
                foreach (WorkStepOperation operation in e.NewItems.OfType<WorkStepOperation>())
                {
                    if (_trackedOperations.Add(operation))
                    {
                        operation.PropertyChanged += Operation_PropertyChanged;
                    }
                }
            }

            if (e.OldItems is not null)
            {
                foreach (WorkStepOperation operation in e.OldItems.OfType<WorkStepOperation>())
                {
                    if (_trackedOperations.Remove(operation))
                    {
                        operation.PropertyChanged -= Operation_PropertyChanged;
                    }
                }
            }

            if (e.Action == NotifyCollectionChangedAction.Move)
            {
                if (sender is ObservableCollection<WorkStepOperation> movedOperations)
                {
                    RefreshOperationNums(movedOperations);
                }

                // Operations 引用没有变化，但顺序属于方案内容变化，必须向上层方案发送修改通知。
                OnPropertyChanged(nameof(Operations));
                return;
            }

            if (sender is ObservableCollection<WorkStepOperation> changedOperations)
            {
                RefreshOperationNums(changedOperations);
            }

            OnPropertyChanged(nameof(OperationCount));
            OnPropertyChanged(nameof(Operations));
        }

        /// <summary>
        /// 操作实体内部字段发生变化时向方案层转发 Operations 通知，
        /// 确保操作对象、方法、参数、返回值或描述更新后都能刷新方案最后修改时间。
        /// </summary>
        private void Operation_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(Operations));
        }

        private static void RefreshOperationNums(ObservableCollection<WorkStepOperation> operations)
        {
            for (int index = 0; index < operations.Count; index++)
            {
                operations[index].Num = index + 1;
            }
        }

        #endregion

        #region 克隆

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
                Operations = new ObservableCollection<WorkStepOperation>(Operations.Select(operation => operation.Clone()))
            };
        }

        #endregion
    }

}
