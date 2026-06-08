using Module.Business.Models;
using Module.Business.Features.SchemeConfiguration;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Module.Business.Features.OperationEditing;

/// <summary>
/// 工位操作参数编辑视图，负责参数表格编辑和操作快照同步。
/// </summary>
public partial class StationOperationEditorView : UserControl
{
    private bool _isRefreshingReturnParameters;
    private INotifyPropertyChanged? _subscribedViewModel;

    /// <summary>
    /// 初始化工位操作参数编辑视图。
    /// </summary>
    public StationOperationEditorView()
    {
        InitializeComponent();
        Loaded += StationOperationEditorView_Loaded;
        DataContextChanged += StationOperationEditorView_DataContextChanged;
    }

    private SchemeConfigurationViewModel? ViewModel => DataContext as SchemeConfigurationViewModel;

    /// <summary>
    /// 处理视图加载后的初始化逻辑。
    /// </summary>
    private void StationOperationEditorView_Loaded(object sender, RoutedEventArgs e)
    {
        AttachViewModelSubscription();
        EnableParameterEditing();
        RefreshReturnParametersFromEditorState();
    }

    /// <summary>
    /// 处理状态或数据变更后的联动刷新。
    /// </summary>
    private void StationOperationEditorView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        DetachViewModelSubscription(e.OldValue as INotifyPropertyChanged);
        AttachViewModelSubscription();
        EnableParameterEditing();
        RefreshReturnParametersFromEditorState();
    }

    /// <summary>
    /// 处理状态或数据变更后的联动刷新。
    /// </summary>
    private void OperationMethodDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshingReturnParameters || OperationMethodDataGrid.SelectedItem is not StationOperationMethodItem methodItem)
        {
            return;
        }

        ApplyMethod(methodItem);
    }

    /// <summary>
    /// 处理界面按钮点击事件。
    /// </summary>
    private void OperationMethodDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        StationOperationMethodItem? methodItem =
            FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject)?.Item as StationOperationMethodItem;
        if (methodItem is null)
        {
            return;
        }

        ApplyMethod(methodItem);
        e.Handled = true;
    }

    /// <summary>
    /// 提交参数表格单元格编辑结果。
    /// </summary>
    private void InvokeParameterDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        EnableParameterEditing();
    }

    /// <summary>
    /// 应用当前编辑结果到目标对象。
    /// </summary>
    private void ApplyMethod(StationOperationMethodItem methodItem)
    {
        if (ViewModel is null)
        {
            return;
        }

        WorkStepOperation? operation = ViewModel.CreateOperationFromMethodItem(methodItem);
        if (operation is null)
        {
            return;
        }

        ApplyMethod(operation);
    }

    /// <summary>
    /// 应用当前编辑结果到目标对象。
    /// </summary>
    private void ApplyMethod(WorkStepOperation operation, ObservableCollection<WorkStepOperationParameter>? parameters = null)
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.EditingOperationObject = operation.OperationObject;
        ViewModel.EditingProtocolName = operation.ProtocolName;
        ViewModel.EditingCommandName = string.IsNullOrWhiteSpace(operation.CommandName)
            ? operation.InvokeMethod
            : operation.CommandName;
        ViewModel.EditingInvokeMethod = operation.InvokeMethod;
        WorkStepOperationParameter? primaryReturnParameter =
            WorkStepOperationRuntimeMetadata.GetPrimaryReturnParameter(operation);
        ViewModel.EditingReturnValue =
            WorkStepOperationRuntimeMetadata.GetReturnParameterKey(primaryReturnParameter);
        ViewModel.EditingShowDataToView = primaryReturnParameter?.ShowDataToView ?? false;
        ViewModel.EditingViewDataName = primaryReturnParameter?.ViewDataName ?? string.Empty;
        ViewModel.EditingViewJudgeType = primaryReturnParameter?.ViewJudgeType ?? string.Empty;
        ViewModel.EditingViewJudgeCondition = primaryReturnParameter?.ViewJudgeCondition ?? string.Empty;
        ViewModel.EditingModifyInvokeParameters = true;
        ViewModel.EditingInvokeParameters.Clear();

        IEnumerable<WorkStepOperationParameter> sourceParameters = parameters is null
            ? WorkStepOperationRuntimeMetadata.GetOrderedInputParameters(operation)
            : parameters.OrderBy(parameter => parameter.Sequence);

        foreach (WorkStepOperationParameter parameter in sourceParameters)
        {
            ViewModel.EditingInvokeParameters.Add(parameter.Clone());
        }

        ViewModel.SelectedEditingInvokeParameter = ViewModel.EditingInvokeParameters.FirstOrDefault();
        ClearDisplayOptions();
        RefreshReturnParameters(operation);
    }

    /// <summary>
    /// 订阅当前视图模型属性变化事件。
    /// </summary>
    private void AttachViewModelSubscription()
    {
        if (ViewModel is not INotifyPropertyChanged propertyChanged || ReferenceEquals(_subscribedViewModel, propertyChanged))
        {
            return;
        }

        DetachViewModelSubscription(_subscribedViewModel);
        _subscribedViewModel = propertyChanged;
        _subscribedViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    /// <summary>
    /// 取消订阅指定视图模型属性变化事件。
    /// </summary>
    private void DetachViewModelSubscription(INotifyPropertyChanged? propertyChanged)
    {
        if (propertyChanged is null)
        {
            return;
        }

        propertyChanged.PropertyChanged -= ViewModel_PropertyChanged;
        if (ReferenceEquals(_subscribedViewModel, propertyChanged))
        {
            _subscribedViewModel = null;
        }
    }

    /// <summary>
    /// 处理状态或数据变更后的联动刷新。
    /// </summary>
    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SchemeConfigurationViewModel.EditingOperationObject)
            or nameof(SchemeConfigurationViewModel.EditingProtocolName)
            or nameof(SchemeConfigurationViewModel.EditingCommandName)
            or nameof(SchemeConfigurationViewModel.EditingInvokeMethod)
            or nameof(SchemeConfigurationViewModel.EditingReturnValue))
        {
            RefreshReturnParametersFromEditorState();
        }
    }

    /// <summary>
    /// 启用参数表格编辑能力。
    /// </summary>
    private void EnableParameterEditing()
    {
        if (ViewModel is null || ViewModel.IsLuaOperationSelected)
        {
            return;
        }

        ViewModel.EditingModifyInvokeParameters = true;
        ClearDisplayOptions();
    }

    /// <summary>
    /// 清空返回值显示配置。
    /// </summary>
    private void ClearDisplayOptions()
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.EditingShowDataToView = false;
        ViewModel.EditingViewDataName = string.Empty;
        ViewModel.EditingViewJudgeType = string.Empty;
        ViewModel.EditingViewJudgeCondition = string.Empty;
    }

    /// <summary>
    /// 刷新对应的界面或业务状态。
    /// </summary>
    private void RefreshReturnParametersFromEditorState()
    {
        if (ViewModel is null)
        {
            return;
        }

        if (ViewModel.IsLuaOperationSelected)
        {
            _isRefreshingReturnParameters = true;
            try
            {
                ViewModel.EditingReturnValue = string.Empty;
                ClearDisplayOptions();
                ViewModel.ClearEditingReturnParameters();
            }
            finally
            {
                _isRefreshingReturnParameters = false;
            }

            return;
        }

        RefreshReturnParameters(CreateEditingOperationSnapshot());
    }

    /// <summary>
    /// 刷新对应的界面或业务状态。
    /// </summary>
    private void RefreshReturnParameters(WorkStepOperation? operation)
    {
        if (ViewModel is null)
        {
            return;
        }

        _isRefreshingReturnParameters = true;
        try
        {
            ObservableCollection<WorkStepOperationParameter> parameters =
                ViewModel.CreateReturnParametersFromOperation(operation);
            ViewModel.ReplaceEditingReturnParameters(parameters);
            ViewModel.SelectedEditingReturnParameter = null;
        }
        finally
        {
            _isRefreshingReturnParameters = false;
        }
    }

    /// <summary>
    /// 创建当前正在编辑的操作快照。
    /// </summary>
    private WorkStepOperation CreateEditingOperationSnapshot()
    {
        ObservableCollection<WorkStepOperationParameter> inputParameters =
            WorkStepOperationRuntimeMetadata.CloneParameters(ViewModel?.EditingInvokeParameters);
        ObservableCollection<WorkStepOperationParameter> returnParameters = CreateEditingReturnParameterSnapshot();

        return new WorkStepOperation
        {
            OperationObject = ViewModel?.EditingOperationObject ?? string.Empty,
            InvokeMethod = ViewModel?.EditingInvokeMethod ?? string.Empty,
            DelayMilliseconds = 0,
            Remark = string.Empty,
            InputParameters = inputParameters,
            ReturnParameters = returnParameters
        };
    }

    private ObservableCollection<WorkStepOperationParameter> CreateEditingReturnParameterSnapshot()
    {
        if (ViewModel is null || ViewModel.IsLuaOperationSelected)
        {
            return [];
        }

        List<WorkStepOperationParameter> parameters = ViewModel.EditingReturnParameters
            .Select(parameter => parameter.Clone())
            .OrderBy(parameter => parameter.Sequence)
            .ToList();

        string primaryKey = ViewModel.EditingReturnValue?.Trim() ?? string.Empty;
        WorkStepOperationParameter? primaryParameter = parameters.FirstOrDefault(parameter =>
            string.Equals(
                WorkStepOperationRuntimeMetadata.GetReturnParameterKey(parameter),
                primaryKey,
                StringComparison.OrdinalIgnoreCase));

        if (primaryParameter is null &&
            (!string.IsNullOrWhiteSpace(primaryKey) ||
             ViewModel.EditingShowDataToView ||
             !string.IsNullOrWhiteSpace(ViewModel.EditingViewDataName) ||
             !string.IsNullOrWhiteSpace(ViewModel.EditingViewJudgeType) ||
             !string.IsNullOrWhiteSpace(ViewModel.EditingViewJudgeCondition)))
        {
            primaryParameter = new WorkStepOperationParameter
            {
                Sequence = parameters.Count + 1,
                Name = "返回值",
                ParameterName = primaryKey,
                Value = primaryKey
            };
            parameters.Add(primaryParameter);
        }

        if (primaryParameter is not null)
        {
            primaryParameter.ParameterName = primaryKey;
            primaryParameter.Value = primaryKey;
            primaryParameter.ShowDataToView = ViewModel.EditingShowDataToView;
            primaryParameter.ViewDataName = ViewModel.EditingViewDataName?.Trim() ?? string.Empty;
            primaryParameter.ViewJudgeType = ViewModel.EditingViewJudgeType?.Trim() ?? string.Empty;
            primaryParameter.ViewJudgeCondition = ViewModel.EditingViewJudgeCondition?.Trim() ?? string.Empty;
        }

        parameters = parameters
            .Where(parameter =>
                !string.IsNullOrWhiteSpace(WorkStepOperationRuntimeMetadata.GetReturnParameterKey(parameter)) ||
                parameter.ShowDataToView ||
                !string.IsNullOrWhiteSpace(parameter.ViewDataName) ||
                !string.IsNullOrWhiteSpace(parameter.ViewJudgeType) ||
                !string.IsNullOrWhiteSpace(parameter.ViewJudgeCondition))
            .Select((parameter, index) =>
            {
                parameter.Sequence = index + 1;
                return parameter;
            })
            .ToList();

        return new ObservableCollection<WorkStepOperationParameter>(parameters);
    }

    private static T? FindAncestor<T>(DependencyObject? source)
        where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T target)
            {
                return target;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }
}
