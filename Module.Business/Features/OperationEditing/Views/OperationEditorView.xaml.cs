using Module.Business.Features.SchemeConfiguration;
using Module.Business.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Module.Business.Features.OperationEditing.Views;

public partial class OperationEditorView : UserControl
{
    private bool _isRefreshingReturnParameters;
    private bool _isRefreshingLuaScriptTemplates;
    private bool _isSynchronizingOperationMethodSelection;
    private INotifyPropertyChanged? _subscribedViewModel;

    public OperationEditorView()
    {
        InitializeComponent();
        Loaded += OperationEditorView_Loaded;
        DataContextChanged += OperationEditorView_DataContextChanged;
    }

    private SchemeConfigurationViewModel? ViewModel => DataContext as SchemeConfigurationViewModel;

    private void OperationEditorView_Loaded(object sender, RoutedEventArgs e)
    {
        AttachViewModelSubscription();
        RefreshLuaScriptTemplateOptions();
        EnableParameterEditing();
        RefreshReturnParametersFromEditorState();
        SynchronizeOperationMethodSelection();
    }

    private void OperationEditorView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        DetachViewModelSubscription(e.OldValue as INotifyPropertyChanged);
        AttachViewModelSubscription();
        RefreshLuaScriptTemplateOptions();
        EnableParameterEditing();
        RefreshReturnParametersFromEditorState();
        SynchronizeOperationMethodSelection();
    }

    private void OperationMethodDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshingReturnParameters ||
            _isSynchronizingOperationMethodSelection ||
            ViewModel?.IsInitializingStepEditor == true ||
            OperationMethodDataGrid.SelectedItem is not StationOperationMethodItem methodItem)
        {
            return;
        }

        ApplyMethod(methodItem);
    }

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

    private void LuaScriptTemplateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshingLuaScriptTemplates ||
            LuaScriptTemplateComboBox.SelectedItem is not string templateName)
        {
            return;
        }

        ViewModel?.ApplyLuaScriptTemplate(templateName);
    }

    private void RefreshLuaScriptTemplateOptions()
    {
        _isRefreshingLuaScriptTemplates = true;
        try
        {
            ViewModel?.RefreshLuaScriptTemplateOptions();
            LuaScriptTemplateComboBox.SelectedItem = null;
        }
        finally
        {
            _isRefreshingLuaScriptTemplates = false;
        }
    }

    private void InvokeParameterDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        EnableParameterEditing();
    }

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

        ViewModel.EditingOperationObject = methodItem.OperationObject;
        ViewModel.EditingProtocolName = methodItem.ProtocolName;
        ViewModel.EditingCommandName = methodItem.CommandName;
        ViewModel.EditingInvokeMethod = methodItem.InvokeMethod;
        ViewModel.EditingReturnValue = operation.ReturnValues.FirstOrDefault()?.ReturnParameterName ?? string.Empty;
        ViewModel.EditingShowDataToView = operation.ReturnValues.FirstOrDefault()?.IsShowView ?? false;
        ViewModel.EditingViewJudgeType = operation.ReturnValues.FirstOrDefault()?.JudgeType ?? string.Empty;
        ViewModel.EditingViewJudgeCondition = operation.ReturnValues.FirstOrDefault()?.JudgeSymbols ?? string.Empty;
        ViewModel.EditingModifyInvokeParameters = true;
        ViewModel.EditingInvokeParameters.Clear();

        IEnumerable<InputParameter> sourceParameters = operation.Parameters.OrderBy(parameter => parameter.Num);
        foreach (InputParameter parameter in sourceParameters)
        {
            ViewModel.EditingInvokeParameters.Add(parameter.Clone());
        }

        ViewModel.SelectedEditingInvokeParameter = ViewModel.EditingInvokeParameters.FirstOrDefault();
        RefreshReturnParameters(operation);
    }

    private void ApplyMethod(WorkStepOperation operation, ObservableCollection<InputParameter>? parameters = null)
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.EditingOperationObject = operation.OperationObjectName;
        ViewModel.EditingProtocolName = ExtractProtocolName(operation.PCommandName);
        ViewModel.EditingCommandName = ExtractCommandName(operation.PCommandName);
        ViewModel.EditingInvokeMethod = operation.PCommandName;
        ViewModel.EditingReturnValue = operation.ReturnValues.FirstOrDefault()?.ReturnParameterName ?? string.Empty;
        ViewModel.EditingShowDataToView = operation.ReturnValues.FirstOrDefault()?.IsShowView ?? false;
        ViewModel.EditingViewJudgeType = operation.ReturnValues.FirstOrDefault()?.JudgeType ?? string.Empty;
        ViewModel.EditingViewJudgeCondition = operation.ReturnValues.FirstOrDefault()?.JudgeSymbols ?? string.Empty;
        ViewModel.EditingModifyInvokeParameters = true;
        ViewModel.EditingInvokeParameters.Clear();

        IEnumerable<InputParameter> sourceParameters = parameters is null
            ? operation.Parameters.OrderBy(parameter => parameter.Num)
            : parameters.OrderBy(parameter => parameter.Num);

        foreach (InputParameter parameter in sourceParameters)
        {
            ViewModel.EditingInvokeParameters.Add(parameter.Clone());
        }

        ViewModel.SelectedEditingInvokeParameter = ViewModel.EditingInvokeParameters.FirstOrDefault();
        RefreshReturnParameters(operation);
    }

    /// <summary>
    /// 从 PCommandName 中提取协议名称（格式：Protocol.Command 或 MethodName）。
    /// </summary>
    private static string ExtractProtocolName(string pCommandName)
    {
        if (string.IsNullOrWhiteSpace(pCommandName))
        {
            return string.Empty;
        }

        int dotIndex = pCommandName.IndexOf('.');
        return dotIndex > 0 ? pCommandName[..dotIndex] : string.Empty;
    }

    /// <summary>
    /// 从 PCommandName 中提取指令名称（格式：Protocol.Command 或 MethodName）。
    /// </summary>
    private static string ExtractCommandName(string pCommandName)
    {
        if (string.IsNullOrWhiteSpace(pCommandName))
        {
            return string.Empty;
        }

        int dotIndex = pCommandName.IndexOf('.');
        return dotIndex > 0 ? pCommandName[(dotIndex + 1)..] : pCommandName;
    }

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

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SchemeConfigurationViewModel.SelectedStationOperationMethod))
        {
            SynchronizeOperationMethodSelection();
        }

        if (e.PropertyName is nameof(SchemeConfigurationViewModel.EditingOperationObject)
            or nameof(SchemeConfigurationViewModel.EditingProtocolName)
            or nameof(SchemeConfigurationViewModel.EditingCommandName)
            or nameof(SchemeConfigurationViewModel.EditingInvokeMethod)
            or nameof(SchemeConfigurationViewModel.EditingReturnValue))
        {
            RefreshReturnParametersFromEditorState();
        }
    }

    private void SynchronizeOperationMethodSelection()
    {
        StationOperationMethodItem? selectedMethod = ViewModel?.SelectedStationOperationMethod;
        _isSynchronizingOperationMethodSelection = true;
        try
        {
            OperationMethodDataGrid.SelectedItem = selectedMethod;
            if (selectedMethod is not null)
            {
                OperationMethodDataGrid.ScrollIntoView(selectedMethod);
            }
        }
        finally
        {
            _isSynchronizingOperationMethodSelection = false;
        }

        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                if (!ReferenceEquals(ViewModel?.SelectedStationOperationMethod, selectedMethod))
                {
                    return;
                }

                _isSynchronizingOperationMethodSelection = true;
                try
                {
                    OperationMethodDataGrid.SelectedItem = selectedMethod;
                    if (selectedMethod is not null && OperationMethodDataGrid.Items.Contains(selectedMethod))
                    {
                        OperationMethodDataGrid.ScrollIntoView(selectedMethod);
                    }
                }
                finally
                {
                    _isSynchronizingOperationMethodSelection = false;
                }
            }),
            DispatcherPriority.Loaded);
    }

    private void EnableParameterEditing()
    {
        if (ViewModel is null || ViewModel.IsLuaOperationSelected)
        {
            return;
        }

        ViewModel.EditingModifyInvokeParameters = true;
    }

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
                ViewModel.StepEditorReturnParameterRows.Clear();
            }
            finally
            {
                _isRefreshingReturnParameters = false;
            }

            return;
        }

        RefreshReturnParameters(CreateEditingOperationSnapshot());
    }

    private void RefreshReturnParameters(WorkStepOperation? operation)
    {
        if (ViewModel is null)
        {
            return;
        }

        _isRefreshingReturnParameters = true;
        try
        {
            ViewModel.StepEditorReturnParameterRows.Clear();
            if (operation is not null)
            {
                foreach (var row in ViewModel.InlineParameterEditor.CreateReturnParameterRows(operation))
                {
                    ViewModel.StepEditorReturnParameterRows.Add(row);
                }
            }
        }
        finally
        {
            _isRefreshingReturnParameters = false;
        }
    }

    private WorkStepOperation CreateEditingOperationSnapshot()
    {
        return new WorkStepOperation
        {
            OperationObjectName = ViewModel?.EditingOperationObject ?? string.Empty,
            PCommandName = ViewModel?.EditingInvokeMethod ?? string.Empty,
            ReturnValues = new ObservableCollection<ReturnValue>
            {
                new ReturnValue
                {
                    ReturnParameterName = ViewModel?.EditingReturnValue ?? string.Empty,
                    IsShowView = ViewModel?.EditingShowDataToView ?? false,
                    JudgeType = ViewModel?.EditingViewJudgeType ?? string.Empty,
                    JudgeSymbols = ViewModel?.EditingViewJudgeCondition ?? string.Empty
                }
            }
        };
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
