using Module.Business.Models;
using Shared.Infrastructure.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Module.Business.Features.SchemeConfiguration
{
    /// <summary>
    /// 方案配置视图，负责工步拖拽、操作编辑抽屉和行内参数编辑。
    /// </summary>
    public partial class SchemeConfigurationView : UserControl
    {
        #region 拖拽数据格式
        private const string SchemeStepDragDataFormat = "Module.Business.SchemeWorkStepItem";
        private const string OperationDragDataFormat = "Module.Business.WorkStepOperation";
        private const double OperationDrawerClosedOffset = 56d;
        private const double InlineParameterDrawerClosedOffset = 56d;

        private static readonly Duration OperationDrawerAnimationDuration =
            new(TimeSpan.FromMilliseconds(220));

        private static readonly IEasingFunction OperationDrawerEasing =
            new CubicEase { EasingMode = EasingMode.EaseOut };

        private Point _schemeStepDragStartPoint;
        private Point _operationDragStartPoint;
        private Point _operationMethodDragStartPoint;
        private SchemeWorkStepItem? _pendingDraggedSchemeStep;
        private WorkStepOperation? _pendingDraggedOperation;
        private StationOperationMethodItem? _pendingDraggedOperationMethod;
        private bool _isInlineParameterDrawerOpen;

        #endregion

        #region 构造与生命周期

        /// <summary>
        /// 初始化方案配置视图。
        /// </summary>
        public SchemeConfigurationView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 使用指定视图模型初始化方案配置视图。
        /// </summary>
        /// <param name="viewModel">方案配置视图模型。</param>
        public SchemeConfigurationView(SchemeConfigurationViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            HookOperationMethodDragEvents();
            Loaded += SchemeConfigurationView_Loaded;
            Unloaded += SchemeConfigurationView_Unloaded;
            UpdateOperationDrawerVisual(animate: false);
            UpdateInlineParameterDrawerVisual(animate: false);
        }

        private SchemeConfigurationViewModel? ViewModel => DataContext as SchemeConfigurationViewModel;

        /// <summary>
        /// 处理视图加载后的初始化逻辑。
        /// </summary>
        private void SchemeConfigurationView_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel is not null)
            {
                ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            }

            UpdateOperationDrawerVisual(animate: false);
            UpdateInlineParameterDrawerVisual(animate: false);
        }

        /// <summary>
        /// 处理视图加载后的初始化逻辑。
        /// </summary>
        private void SchemeConfigurationView_Unloaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel is not null)
            {
                ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }
        }

        #endregion

        #region 视图模型联动
        /// <summary>
        /// 响应视图模型属性变化并刷新界面状态。
        /// </summary>
        /// <summary>
        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SchemeConfigurationViewModel.IsStepEditorOpen))
            {
                UpdateOperationDrawerVisual(animate: true);
            }
        }

        #endregion

        #region 方案工步拖拽

        /// <summary>
        /// 处理界面按钮点击事件。
        /// </summary>
        private void SaveSchemesButton_Click(object sender, RoutedEventArgs e)
        {
            CommitSchemeNameTextBoxes();
            CommitEditableDataGrids();

            if (ViewModel?.SaveSchemesCommand.CanExecute(null) == true)
            {
                ViewModel.SaveSchemesCommand.Execute(null);
            }
        }

        /// <summary>
        /// 提交页面内所有可编辑表格的当前编辑。
        /// </summary>
        private void CommitEditableDataGrids()
        {
            SchemeStepsDataGrid?.CommitEdit(DataGridEditingUnit.Cell, true);
            SchemeStepsDataGrid?.CommitEdit(DataGridEditingUnit.Row, true);
            OperationsDataGrid?.CommitEdit(DataGridEditingUnit.Cell, true);
            OperationsDataGrid?.CommitEdit(DataGridEditingUnit.Row, true);
        }

        /// <summary>
        /// 提交方案名称文本框的当前输入。
        /// </summary>
        private void CommitSchemeNameTextBoxes()
        {
            if (SchemesListBox is null)
            {
                return;
            }

            foreach (TextBox textBox in FindVisualChildren<TextBox>(SchemesListBox))
            {
                BindingExpression? bindingExpression = textBox.GetBindingExpression(TextBox.TextProperty);
                if (bindingExpression?.ParentBinding?.Path?.Path == nameof(SchemeProfile.SchemeName))
                {
                    bindingExpression.UpdateSource();
                }
            }
        }

        /// <summary>
        /// 处理鼠标交互事件。
        /// </summary>
        private void SchemeStepsDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsInlineEditableSchemeStepElement(e.OriginalSource as DependencyObject))
            {
                _pendingDraggedSchemeStep = null;
                return;
            }

            _schemeStepDragStartPoint = e.GetPosition(SchemeStepsDataGrid);
            _pendingDraggedSchemeStep = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject)?.Item as SchemeWorkStepItem;
        }

        /// <summary>
        /// 处理鼠标交互事件。
        /// </summary>
        private void SchemeStepsDataGrid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _pendingDraggedSchemeStep is null)
            {
                return;
            }

            Point currentPoint = e.GetPosition(SchemeStepsDataGrid);
            if (Math.Abs(currentPoint.X - _schemeStepDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(currentPoint.Y - _schemeStepDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            SchemeWorkStepItem draggedSchemeStep = _pendingDraggedSchemeStep;
            _pendingDraggedSchemeStep = null;

            DataObject dataObject = new();
            dataObject.SetData(SchemeStepDragDataFormat, draggedSchemeStep);
            DragDrop.DoDragDrop(SchemeStepsDataGrid, dataObject, DragDropEffects.Move);
        }

        /// <summary>
        /// 处理鼠标交互事件。
        /// </summary>
        private void SchemeStepsDataGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject) is not DataGridRow row)
            {
                return;
            }

            row.IsSelected = true;
            SchemeStepsDataGrid.SelectedItem = row.Item;
            row.Focus();
        }

        /// <summary>
        /// 处理拖拽交互逻辑。
        /// </summary>
        private void SchemeStepsDataGrid_DragOver(object sender, DragEventArgs e)
        {
            if (!TryGetSchemeStepDropInfo(e, out _, out _, out bool insertAfter))
            {
                HideSchemeStepDropIndicator();
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            ShowSchemeStepDropIndicator(FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject), insertAfter);
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        /// <summary>
        /// 处理拖拽交互逻辑。
        /// </summary>
        private void SchemeStepsDataGrid_DragLeave(object sender, DragEventArgs e)
        {
            HideSchemeStepDropIndicator();
        }

        /// <summary>
        /// 处理拖拽交互逻辑。
        /// </summary>
        private void SchemeStepsDataGrid_Drop(object sender, DragEventArgs e)
        {
            if (TryGetSchemeStepDropInfo(e, out SchemeWorkStepItem? draggedSchemeStep, out SchemeWorkStepItem? targetSchemeStep, out bool insertAfter) &&
                draggedSchemeStep is not null &&
                targetSchemeStep is not null)
            {
                ViewModel?.MoveSchemeStep(draggedSchemeStep, targetSchemeStep, insertAfter);
            }

            _pendingDraggedSchemeStep = null;
            HideSchemeStepDropIndicator();
            e.Handled = true;
        }

        /// <summary>
        /// 处理拖拽交互逻辑。
        /// </summary>
        private bool TryGetSchemeStepDropInfo(
            DragEventArgs e,
            out SchemeWorkStepItem? draggedSchemeStep,
            out SchemeWorkStepItem? targetSchemeStep,
            out bool insertAfter)
        {
            draggedSchemeStep = e.Data.GetDataPresent(SchemeStepDragDataFormat)
                ? e.Data.GetData(SchemeStepDragDataFormat) as SchemeWorkStepItem
                : null;
            targetSchemeStep = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject)?.Item as SchemeWorkStepItem;
            insertAfter = false;

            if (draggedSchemeStep is null || targetSchemeStep is null || ReferenceEquals(draggedSchemeStep, targetSchemeStep))
            {
                return false;
            }

            DataGridRow? targetRow = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject);
            if (targetRow is not null)
            {
                insertAfter = e.GetPosition(targetRow).Y > targetRow.ActualHeight / 2d;
            }

            return true;
        }

        /// <summary>
        /// 处理拖拽交互逻辑。
        /// </summary>
        private void ShowSchemeStepDropIndicator(DataGridRow? targetRow, bool insertAfter)
        {
            if (targetRow is null || SchemeStepDropIndicatorCanvas is null || SchemeStepDropIndicator is null)
            {
                HideSchemeStepDropIndicator();
                return;
            }

            double horizontalPadding = 8d;
            double indicatorHeight = 3d;
            double width = Math.Max(0d, SchemeStepDropIndicatorCanvas.ActualWidth - horizontalPadding * 2);
            Point rowTopLeft = targetRow.TranslatePoint(new Point(0, 0), SchemeStepDropIndicatorCanvas);
            double top = rowTopLeft.Y + (insertAfter ? targetRow.ActualHeight : 0d) - indicatorHeight / 2d;
            top = Math.Clamp(top, 0d, Math.Max(0d, SchemeStepDropIndicatorCanvas.ActualHeight - indicatorHeight));

            SchemeStepDropIndicator.Width = width;
            SchemeStepDropIndicator.Height = indicatorHeight;
            Canvas.SetLeft(SchemeStepDropIndicator, horizontalPadding);
            Canvas.SetTop(SchemeStepDropIndicator, top);
            SchemeStepDropIndicator.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 处理拖拽交互逻辑。
        /// </summary>
        private void HideSchemeStepDropIndicator()
        {
            if (SchemeStepDropIndicator is not null)
            {
                SchemeStepDropIndicator.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        #region 方法指令拖拽

        /// <summary>
        /// 处理拖拽交互逻辑。
        /// </summary>
        private void HookOperationMethodDragEvents()
        {
            OperationMethodDataGrid.PreviewMouseLeftButtonDown += OperationMethodDataGrid_PreviewMouseLeftButtonDown;
            OperationMethodDataGrid.PreviewMouseMove += OperationMethodDataGrid_PreviewMouseMove;
            OperationMethodDataGrid.SelectionChanged += OperationMethodDataGrid_SelectionChanged;
        }

        /// <summary>
        /// 处理状态或数据变更后的联动刷新。
        /// </summary>
        private void OperationMethodDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel is not null)
            {
                ViewModel.SelectedStationOperationMethod = OperationMethodDataGrid.SelectedItem as StationOperationMethodItem;
            }
        }

        /// <summary>
        /// 处理鼠标交互事件。
        /// </summary>
        private void OperationMethodDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _operationMethodDragStartPoint = e.GetPosition(OperationMethodDataGrid);
            _pendingDraggedOperationMethod = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject)?.Item as StationOperationMethodItem;
        }

        /// <summary>
        /// 处理鼠标交互事件。
        /// </summary>
        private void OperationMethodDataGrid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _pendingDraggedOperationMethod is null)
            {
                return;
            }

            Point currentPoint = e.GetPosition(OperationMethodDataGrid);
            if (Math.Abs(currentPoint.X - _operationMethodDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(currentPoint.Y - _operationMethodDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            WorkStepOperation? operation = ViewModel?.CreateStepFromInvokeMethodItem(_pendingDraggedOperationMethod);
            _pendingDraggedOperationMethod = null;
            if (operation is null)
            {
                return;
            }

            DataObject dataObject = new();
            dataObject.SetData(OperationDragDataFormat, operation);
            dataObject.SetData(DataFormats.StringFormat, operation.DisplayText);
            DragDrop.DoDragDrop(OperationMethodDataGrid, dataObject, DragDropEffects.Copy);
        }

        #endregion

        #region 操作编辑抽屉
        /// <summary>
        /// 处理界面按钮点击事件。
        /// </summary>
        private void OperationsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (IsOperationSelectionCheckBox(e.OriginalSource as DependencyObject) ||
                IsInlineEditableOperationCell(e.OriginalSource as DependencyObject))
            {
                return;
            }

            if (FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject)?.Item is not WorkStepOperation operation)
            {
                return;
            }

            OperationsDataGrid.SelectedItem = operation;
            ViewModel?.OpenStepEditorForEdit(operation);
            e.Handled = true;
        }

        /// <summary>
        /// 处理鼠标交互事件。
        /// </summary>
        private void OperationsDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsOperationSelectionCheckBox(e.OriginalSource as DependencyObject) ||
                IsInlineEditableOperationCell(e.OriginalSource as DependencyObject))
            {
                _pendingDraggedOperation = null;
                return;
            }

            _operationDragStartPoint = e.GetPosition(OperationsDataGrid);
            _pendingDraggedOperation = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject)?.Item as WorkStepOperation;
        }

        /// <summary>
        /// 处理鼠标交互事件。
        /// </summary>
        private void OperationsDataGrid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _pendingDraggedOperation is null)
            {
                return;
            }

            Point currentPoint = e.GetPosition(OperationsDataGrid);
            if (Math.Abs(currentPoint.X - _operationDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(currentPoint.Y - _operationDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            WorkStepOperation draggedOperation = _pendingDraggedOperation;
            _pendingDraggedOperation = null;

            DataObject dataObject = new();
            dataObject.SetData(OperationDragDataFormat, draggedOperation);
            DragDrop.DoDragDrop(OperationsDataGrid, dataObject, DragDropEffects.Move);
        }

        /// <summary>
        /// 处理拖拽交互逻辑。
        /// </summary>
        private void OperationsDataGrid_DragOver(object sender, DragEventArgs e)
        {
            if (!TryGetOperationDropInfo(e, out WorkStepOperation? draggedOperation, out _, out bool insertAfter, out bool isExistingOperation) ||
                draggedOperation is null)
            {
                HideOperationDropIndicator();
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            ShowOperationDropIndicator(FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject), insertAfter);
            e.Effects = isExistingOperation ? DragDropEffects.Move : DragDropEffects.Copy;
            e.Handled = true;
        }

        /// <summary>
        /// 处理拖拽交互逻辑。
        /// </summary>
        private void OperationsDataGrid_DragLeave(object sender, DragEventArgs e)
        {
            HideOperationDropIndicator();
        }

        /// <summary>
        /// 处理拖拽交互逻辑。
        /// </summary>
        private void OperationsDataGrid_Drop(object sender, DragEventArgs e)
        {
            if (TryGetOperationDropInfo(
                    e,
                    out WorkStepOperation? draggedOperation,
                    out WorkStepOperation? targetOperation,
                    out bool insertAfter,
                    out bool isExistingOperation) &&
                draggedOperation is not null)
            {
                if (isExistingOperation && targetOperation is not null)
                {
                    ViewModel?.MoveStep(draggedOperation, targetOperation, insertAfter);
                }
                else if (!isExistingOperation)
                {
                    ViewModel?.InsertStep(draggedOperation, targetOperation, insertAfter);
                }
            }

            _pendingDraggedOperation = null;
            HideOperationDropIndicator();
            e.Handled = true;
        }

        /// <summary>
        /// 处理拖拽交互逻辑。
        /// </summary>
        private bool TryGetOperationDropInfo(
            DragEventArgs e,
            out WorkStepOperation? draggedOperation,
            out WorkStepOperation? targetOperation,
            out bool insertAfter,
            out bool isExistingOperation)
        {
            draggedOperation = e.Data.GetDataPresent(OperationDragDataFormat)
                ? e.Data.GetData(OperationDragDataFormat) as WorkStepOperation
                : null;
            targetOperation = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject)?.Item as WorkStepOperation;
            insertAfter = false;
            isExistingOperation = draggedOperation is not null &&
                                  ViewModel?.ContainsCurrentStep(draggedOperation) == true;

            if (draggedOperation is null)
            {
                return false;
            }

            DataGridRow? targetRow = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject);
            if (targetRow is not null)
            {
                insertAfter = e.GetPosition(targetRow).Y > targetRow.ActualHeight / 2d;
            }

            if (isExistingOperation)
            {
                return targetOperation is not null && !ReferenceEquals(draggedOperation, targetOperation);
            }

            return ViewModel?.HasCurrentSchemeStep() == true;
        }

        /// <summary>
        /// 处理拖拽交互逻辑。
        /// </summary>
        private void ShowOperationDropIndicator(DataGridRow? targetRow, bool insertAfter)
        {
            if (targetRow is null || OperationDropIndicatorCanvas is null || OperationDropIndicator is null)
            {
                HideOperationDropIndicator();
                return;
            }

            double horizontalPadding = 8d;
            double indicatorHeight = 3d;
            double width = Math.Max(0d, OperationDropIndicatorCanvas.ActualWidth - horizontalPadding * 2);
            Point rowTopLeft = targetRow.TranslatePoint(new Point(0, 0), OperationDropIndicatorCanvas);
            double top = rowTopLeft.Y + (insertAfter ? targetRow.ActualHeight : 0d) - indicatorHeight / 2d;
            top = Math.Clamp(top, 0d, Math.Max(0d, OperationDropIndicatorCanvas.ActualHeight - indicatorHeight));

            OperationDropIndicator.Width = width;
            OperationDropIndicator.Height = indicatorHeight;
            Canvas.SetLeft(OperationDropIndicator, horizontalPadding);
            Canvas.SetTop(OperationDropIndicator, top);
            OperationDropIndicator.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 处理拖拽交互逻辑。
        /// </summary>
        private void HideOperationDropIndicator()
        {
            if (OperationDropIndicator is not null)
            {
                OperationDropIndicator.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        #region 抽屉动画

        #region 行内参数编辑

        /// <summary>
        /// 处理界面按钮点击事件。
        /// </summary>
        private void InlineOperationParametersButton_Click(object sender, RoutedEventArgs e)
        {
            CommitEditableDataGrids();

            if ((sender as FrameworkElement)?.DataContext is not WorkStepOperation operation)
            {
                return;
            }

            OperationsDataGrid.SelectedItem = operation;
            if (ViewModel is not null)
            {
                ViewModel.SelectedStep = operation;
            }

            InlineParameterEditState state = new(
                operation,
                ViewModel?.StepCollection ?? Enumerable.Empty<WorkStepOperation>(),
                CollectReturnParameterKeys);
            InlineParameterDrawerSheet.Tag = state;
            ViewModel?.SetActiveParameterCollections(
                state.OperationSummary,
                state.InputParameterRows,
                state.ReturnParameterRows);
            OpenInlineParameterDrawer();
            e.Handled = true;
        }

        /// <summary>
        /// 处理鼠标交互事件。
        /// </summary>
        private void InlineParameterDrawerBackdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            CloseInlineParameterDrawer();
        }

        /// <summary>
        /// 处理界面按钮点击事件。
        /// </summary>
        private void CloseInlineParameterDrawerButton_Click(object sender, RoutedEventArgs e)
        {
            CloseInlineParameterDrawer();
        }

        /// <summary>
        /// 处理界面按钮点击事件。
        /// </summary>
        private void ApplyInlineParameterDrawerButton_Click(object sender, RoutedEventArgs e)
        {
            InlineInputParameterDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            InlineInputParameterDataGrid.CommitEdit(DataGridEditingUnit.Row, true);
            InlineReturnParameterDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            InlineReturnParameterDataGrid.CommitEdit(DataGridEditingUnit.Row, true);

            if (ViewModel is null || InlineParameterDrawerSheet.Tag is not InlineParameterEditState state)
            {
                return;
            }

            state.SanitizeReturnParameterTable();
            ObservableCollection<WorkStepOperationParameter> parameters = state.BuildInputParameters();

            state.TargetOperation.Parameters = parameters;
            state.ApplyReturnParameters();
            state.TargetOperation.AreParametersModified =
                ViewModel.HasModifiedStepParameters(state.TargetOperation, parameters);

            CloseInlineParameterDrawer();
            e.Handled = true;
        }

        /// <summary>
        /// 处理状态或数据变更后的联动刷新。
        /// </summary>
        private void InlineParameterTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!ReferenceEquals(e.OriginalSource, sender) ||
                InlineParameterDrawerSheet?.Tag is not InlineParameterEditState state)
            {
                return;
            }

            InlineReturnParameterDataGrid?.CommitEdit(DataGridEditingUnit.Cell, true);
            InlineReturnParameterDataGrid?.CommitEdit(DataGridEditingUnit.Row, true);
            state.SanitizeReturnParameterTable();
            state.RefreshInputValueOptions(
                ViewModel?.StepCollection ?? Enumerable.Empty<WorkStepOperation>());
        }

        /// <summary>
        /// 打开对应的编辑界面或抽屉。
        /// </summary>
        private void OpenInlineParameterDrawer()
        {
            _isInlineParameterDrawerOpen = true;
            UpdateInlineParameterDrawerVisual(animate: true);
        }

        /// <summary>
        /// 关闭对应的编辑界面或抽屉。
        /// </summary>
        private void CloseInlineParameterDrawer()
        {
            _isInlineParameterDrawerOpen = false;
            ViewModel?.ClearActiveParameterCollections();
            InlineParameterDrawerSheet.Tag = null;
            UpdateInlineParameterDrawerVisual(animate: true);
        }

        /// <summary>
        /// 收集指定操作可供后续步骤引用的返回参数键。
        /// </summary>
        private IEnumerable<string> CollectReturnParameterKeys(WorkStepOperation operation)
        {
            return ViewModel?.CreateReturnParametersFromOperation(operation)
                .Select(parameter => parameter.ParameterName) ?? Enumerable.Empty<string>();
        }

        /// <summary>
        /// 更新行内参数编辑抽屉的显示状态。
        /// </summary>
        private void UpdateInlineParameterDrawerVisual(bool animate)
        {
            if (InlineParameterDrawerHost is null || InlineParameterDrawerTranslateTransform is null)
            {
                return;
            }

            double targetOpacity = _isInlineParameterDrawerOpen ? 1d : 0d;
            double targetOffset = _isInlineParameterDrawerOpen ? 0d : InlineParameterDrawerClosedOffset;

            if (_isInlineParameterDrawerOpen)
            {
                InlineParameterDrawerHost.IsHitTestVisible = true;
            }

            if (!animate)
            {
                InlineParameterDrawerHost.BeginAnimation(UIElement.OpacityProperty, null);
                InlineParameterDrawerTranslateTransform.BeginAnimation(TranslateTransform.YProperty, null);
                InlineParameterDrawerHost.Opacity = targetOpacity;
                InlineParameterDrawerTranslateTransform.Y = targetOffset;
                InlineParameterDrawerHost.IsHitTestVisible = _isInlineParameterDrawerOpen;
                return;
            }

            DoubleAnimation opacityAnimation = new(targetOpacity, OperationDrawerAnimationDuration)
            {
                EasingFunction = OperationDrawerEasing
            };

            if (!_isInlineParameterDrawerOpen)
            {
                opacityAnimation.Completed += (_, _) =>
                {
                    if (!_isInlineParameterDrawerOpen)
                    {
                        InlineParameterDrawerHost.IsHitTestVisible = false;
                    }
                };
            }

            DoubleAnimation translateAnimation = new(targetOffset, OperationDrawerAnimationDuration)
            {
                EasingFunction = OperationDrawerEasing
            };

            InlineParameterDrawerHost.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
            InlineParameterDrawerTranslateTransform.BeginAnimation(TranslateTransform.YProperty, translateAnimation);
        }

        private sealed class InlineParameterEditState
        {
            private readonly Func<WorkStepOperation, IEnumerable<string>> _collectReturnParameterKeys;

            public InlineParameterEditState(
                WorkStepOperation operation,
                IEnumerable<WorkStepOperation> currentOperations,
                Func<WorkStepOperation, IEnumerable<string>> collectReturnParameterKeys)
            {
                TargetOperation = operation;
                _collectReturnParameterKeys = collectReturnParameterKeys;
                OperationTitle = operation.DisplayText;
                OperationSummary = $"{operation.OperationObject}.{operation.InvokeMethod}";
                InputParameterRows = CreateInputParameterRows(operation.Parameters);
                ReturnParameterRows = CreateReturnParameterRows(operation, out IReadOnlyList<string> parsedReturnKeys);
                ParsedReturnKeys = parsedReturnKeys;
                RefreshInputValueOptions(currentOperations);
                Parameters = new ObservableCollection<WorkStepOperationParameter>(
                    operation.Parameters
                        .OrderBy(parameter => parameter.Sequence)
                        .Select(parameter => parameter.Clone()));
                ParameterSummary = InputParameterRows.Count == 0
                    ? "无输入参数"
                    : $"{InputParameterRows.Count} 个输入参数";
            }
            public WorkStepOperation TargetOperation { get; }

            public string OperationTitle { get; }

            public string OperationSummary { get; }

            public string ParameterSummary { get; }

            public ObservableCollection<InlineInputParameterRow> InputParameterRows { get; }

            public ObservableCollection<InlineReturnParameterRow> ReturnParameterRows { get; }

            public IReadOnlyList<string> ParsedReturnKeys { get; }

            /// <summary>
            /// 刷新对应的界面或业务状态。
            /// </summary>
            public void RefreshInputValueOptions(IEnumerable<WorkStepOperation> currentOperations)
            {
                List<string> options = BuildInputReturnValueOptions(
                        currentOperations,
                        TargetOperation,
                        _collectReturnParameterKeys)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (InlineInputParameterRow row in InputParameterRows)
                {
                    ReplaceStringOptions(row.ValueOptions, options);
                }
            }

            /// <summary>
            /// 构建并返回对应的业务数据。
            /// </summary>
            public ObservableCollection<WorkStepOperationParameter> BuildInputParameters()
            {
                List<WorkStepOperationParameter> parameters = new();
                foreach (InlineInputParameterRow row in InputParameterRows)
                {
                    parameters.Add(new WorkStepOperationParameter
                    {
                        Id = row.Id,
                        Sequence = Math.Max(1, row.Sequence),
                        Name = row.Type,
                        ParameterName = row.ParameterName,
                        Value = row.Value,
                        Remark = row.Description
                    });
                }

                return new ObservableCollection<WorkStepOperationParameter>(
                    parameters
                        .OrderBy(parameter => parameter.Sequence)
                        .Select((parameter, index) =>
                        {
                            parameter.Sequence = index + 1;
                            return parameter;
                        }));
            }

            /// <summary>
            /// 应用当前编辑结果到目标对象。
            /// </summary>
            public void ApplyReturnParameters()
            {
                SanitizeReturnParameterTable();
                List<InlineReturnParameterRow> rows = ReturnParameterRows
                    .Where(item => !IsEmptyReturnParameterRow(item))
                    .Where(IsAllowedReturnParameterRow)
                    .ToList();
                InlineReturnParameterRow? row = rows.FirstOrDefault(item =>
                    !string.IsNullOrWhiteSpace(TargetOperation.ReturnValue) &&
                    string.Equals(
                        item.Key,
                        TargetOperation.ReturnValue.Trim(),
                        StringComparison.OrdinalIgnoreCase)) ??
                    rows.FirstOrDefault(item => item.ShowDataToView) ??
                    (rows.Count == 1 ? rows[0] : null);
                if (row is null)
                {
                    TargetOperation.ReturnValue = string.Empty;
                    TargetOperation.ShowDataToView = false;
                    TargetOperation.ViewDataName = string.Empty;
                    TargetOperation.ViewJudgeType = string.Empty;
                    TargetOperation.ViewJudgeCondition = string.Empty;
                    return;
                }

                TargetOperation.ReturnValue = row.Key;
                TargetOperation.ShowDataToView = row.ShowDataToView;
                TargetOperation.ViewDataName = row.ViewDataName?.Trim() ?? string.Empty;
                TargetOperation.ViewJudgeType = row.ViewJudgeType?.Trim() ?? string.Empty;
                TargetOperation.ViewJudgeCondition = row.ViewJudgeCondition?.Trim() ?? string.Empty;
            }

            /// <summary>
            /// 清理返回参数表格中的无效显示项。
            /// </summary>
            public void SanitizeReturnParameterTable()
            {
                HashSet<string> seenKeys = new(StringComparer.OrdinalIgnoreCase);
                List<InlineReturnParameterRow> rowsToRemove = new();
                foreach (InlineReturnParameterRow row in ReturnParameterRows)
                {
                    if (IsEmptyReturnParameterRow(row))
                    {
                        rowsToRemove.Add(row);
                        continue;
                    }

                    string returnValue = row.Key;
                    if (ParsedReturnKeys.Count > 0 &&
                        !ParsedReturnKeys.Any(key => string.Equals(key, returnValue, StringComparison.OrdinalIgnoreCase)))
                    {
                        rowsToRemove.Add(row);
                        continue;
                    }

                    if (!seenKeys.Add(returnValue))
                    {
                        rowsToRemove.Add(row);
                    }
                }

                foreach (InlineReturnParameterRow row in rowsToRemove)
                {
                    ReturnParameterRows.Remove(row);
                }
            }

            public ObservableCollection<WorkStepOperationParameter> Parameters { get; }

            /// <summary>
            /// 根据操作参数创建输入参数行集合。
            /// </summary>
            private static ObservableCollection<InlineInputParameterRow> CreateInputParameterRows(IEnumerable<WorkStepOperationParameter> parameters)
            {
                return new ObservableCollection<InlineInputParameterRow>(
                    parameters
                        .OrderBy(parameter => parameter.Sequence)
                        .Select(parameter => new InlineInputParameterRow
                        {
                            Id = parameter.Id,
                            Sequence = parameter.Sequence,
                            Type = parameter.Type,
                            ParameterName = parameter.ParameterName,
                            Value = parameter.Value,
                            Description = parameter.Description
                        }));
            }

            /// <summary>
            /// 根据操作返回值配置创建返回参数行集合。
            /// </summary>
            private ObservableCollection<InlineReturnParameterRow> CreateReturnParameterRows(
                WorkStepOperation operation,
                out IReadOnlyList<string> parsedReturnKeys)
            {
                parsedReturnKeys = Array.Empty<string>();
                ObservableCollection<InlineReturnParameterRow> rows = new();

                JsonElement? command = FindProtocolCommand(operation.ProtocolName?.Trim() ?? string.Empty, operation.CommandName?.Trim() ?? string.Empty);
                if (IsSendOnlyProtocolCommand(command))
                {
                    return rows;
                }

                IReadOnlyList<string> parsedKeys = command is null
                    ? Array.Empty<string>()
                    : GetJsonStringArray(command.Value, "ParsedResultKeys");
                parsedReturnKeys = parsedKeys;
                if (parsedKeys.Count > 0)
                {
                    foreach (string parsedKey in parsedKeys)
                    {
                        bool isCurrentReturnValue = string.Equals(parsedKey, operation.ReturnValue, StringComparison.OrdinalIgnoreCase);
                        rows.Add(new InlineReturnParameterRow
                        {
                            Key = parsedKey,
                            ShowDataToView = isCurrentReturnValue && operation.ShowDataToView,
                            ViewDataName = isCurrentReturnValue ? operation.ViewDataName : string.Empty,
                            ViewJudgeType = isCurrentReturnValue ? operation.ViewJudgeType : string.Empty,
                            ViewJudgeCondition = isCurrentReturnValue ? operation.ViewJudgeCondition : string.Empty
                        });
                    }

                    return rows;
                }

                if (!HasReturnParameter(operation))
                {
                    return rows;
                }

                rows.Add(new InlineReturnParameterRow
                {
                    Key = operation.ReturnValue,
                    ShowDataToView = operation.ShowDataToView,
                    ViewDataName = operation.ViewDataName,
                    ViewJudgeType = operation.ViewJudgeType,
                    ViewJudgeCondition = operation.ViewJudgeCondition
                });
                return rows;
            }

            /// <summary>
            /// 判断操作是否配置了返回参数。
            /// </summary>
            private static bool HasReturnParameter(WorkStepOperation operation)
            {
                return !string.IsNullOrWhiteSpace(operation.ReturnValue) ||
                       operation.ShowDataToView ||
                       !string.IsNullOrWhiteSpace(operation.ViewDataName) ||
                       !string.IsNullOrWhiteSpace(operation.ViewJudgeType) ||
                       !string.IsNullOrWhiteSpace(operation.ViewJudgeCondition);
            }

            /// <summary>
            /// 判断是否满足指定业务条件。
            /// </summary>
            private bool IsAllowedReturnParameterRow(InlineReturnParameterRow row)
            {
                if (ParsedReturnKeys.Count == 0)
                {
                    return true;
                }

                string returnValue = row.Key;
                return ParsedReturnKeys.Any(key => string.Equals(key, returnValue, StringComparison.OrdinalIgnoreCase));
            }

            /// <summary>
            /// 判断是否满足指定业务条件。
            /// </summary>
            private static bool IsEmptyReturnParameterRow(InlineReturnParameterRow row)
            {
                return string.IsNullOrWhiteSpace(row.Key) &&
                       !row.ShowDataToView &&
                       string.IsNullOrWhiteSpace(row.ViewDataName) &&
                       string.IsNullOrWhiteSpace(row.ViewJudgeType) &&
                       string.IsNullOrWhiteSpace(row.ViewJudgeCondition);
            }

            /// <summary>
            /// 构建并返回对应的业务数据。
            /// </summary>
            private static IEnumerable<string> BuildInputReturnValueOptions(
                IEnumerable<WorkStepOperation> currentOperations,
                WorkStepOperation targetOperation,
                Func<WorkStepOperation, IEnumerable<string>> collectReturnParameterKeys)
            {
                List<WorkStepOperation> operations = currentOperations
                    .Where(operation => operation is not null)
                    .ToList();

                int targetIndex = operations.FindIndex(operation =>
                    ReferenceEquals(operation, targetOperation) ||
                    string.Equals(operation.Id, targetOperation.Id, StringComparison.Ordinal));

                if (targetIndex <= 0)
                {
                    return Enumerable.Empty<string>();
                }

                return operations
                    .Take(targetIndex)
                    .SelectMany(operation => collectReturnParameterKeys(operation) ?? Enumerable.Empty<string>());
            }

            /// <summary>
            /// 用候选项集合替换字符串选项集合。
            /// </summary>
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

            public sealed class InlineInputParameterRow : INotifyPropertyChanged
            {
                private string _type = string.Empty;
                private string _value = string.Empty;

                public event PropertyChangedEventHandler? PropertyChanged;

                public string Id { get; set; } = string.Empty;

                public int Sequence { get; set; }

                public string Type
                {
                    get => _type;
                    set
                    {
                        string normalizedValue = value?.Trim() ?? string.Empty;
                        if (string.Equals(_type, normalizedValue, StringComparison.Ordinal))
                        {
                            return;
                        }

                        _type = normalizedValue;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Type)));
                    }
                }

                public string ParameterName { get; set; } = string.Empty;

                public string Value
                {
                    get => _value;
                    set
                    {
                        string normalizedValue = value ?? string.Empty;
                        if (string.Equals(_value, normalizedValue, StringComparison.Ordinal))
                        {
                            return;
                        }

                        _value = normalizedValue;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
                    }
                }

                public string Description { get; set; } = string.Empty;

                public ObservableCollection<string> ValueOptions { get; } = new();
            }

            public sealed class InlineReturnParameterRow : INotifyPropertyChanged
            {
                /// <summary>
                /// 判断条件模板显示项。
                /// </summary>
                public sealed record JudgeTemplateOption(string DisplayText, string Value)
                {
                    /// <summary>
                    /// 返回模板显示文本。
                    /// </summary>
                    public override string ToString() => DisplayText;
                }

                private static readonly IReadOnlyList<JudgeTemplateOption> DefaultJudgeTemplateOptions =
                    Array.AsReadOnly(new[]
                    {
                        new JudgeTemplateOption(">", ">"),
                        new JudgeTemplateOption(">=", ">="),
                        new JudgeTemplateOption("<", "<"),
                        new JudgeTemplateOption("<=", "<="),
                        new JudgeTemplateOption("==", "=="),
                        new JudgeTemplateOption("!=", "!="),
                        new JudgeTemplateOption("<{0}<", "<{0}<"),
                        new JudgeTemplateOption("<={0}<=", "<={0}<="),
                        new JudgeTemplateOption("()", "()"),
                        new JudgeTemplateOption("!()", "!()"),
                        new JudgeTemplateOption("黑名单", "黑名单"),
                        new JudgeTemplateOption("白名单", "白名单"),
                        new JudgeTemplateOption("NA", "NA")
                    });

                private string _key = string.Empty;
                private string _viewJudgeType = string.Empty;
                private string _firstJudgeConditionValue = string.Empty;
                private string _secondJudgeConditionValue = string.Empty;

                public event PropertyChangedEventHandler? PropertyChanged;

                public string Key
                {
                    get => _key;
                    set => _key = value?.Trim() ?? string.Empty;
                }

                public bool ShowDataToView { get; set; }

                public string ViewDataName { get; set; } = string.Empty;

                public IReadOnlyList<JudgeTemplateOption> JudgeTemplateOptions => DefaultJudgeTemplateOptions;

                public string ViewJudgeType
                {
                    get => _viewJudgeType;
                    set
                    {
                        string normalizedValue = value?.Trim() ?? string.Empty;
                        bool wasRangeTemplate = IsRangeJudgeTemplate;
                        if (string.Equals(_viewJudgeType, normalizedValue, StringComparison.Ordinal))
                        {
                            return;
                        }

                        _viewJudgeType = normalizedValue;
                        if (!IsRangeJudgeTemplate && wasRangeTemplate)
                        {
                            _firstJudgeConditionValue = BuildRangeConditionValue();
                            _secondJudgeConditionValue = string.Empty;
                        }
                        else if (IsRangeJudgeTemplate && !wasRangeTemplate)
                        {
                            ParseRangeConditionValue(_firstJudgeConditionValue);
                        }

                        OnPropertyChanged(nameof(ViewJudgeType));
                        OnPropertyChanged(nameof(ViewJudgeCondition));
                        OnPropertyChanged(nameof(IsRangeJudgeTemplate));
                        OnPropertyChanged(nameof(FirstJudgeConditionValue));
                        OnPropertyChanged(nameof(SecondJudgeConditionValue));
                    }
                }

                public string ViewJudgeCondition
                {
                    get => IsRangeJudgeTemplate
                        ? BuildRangeConditionValue()
                        : _firstJudgeConditionValue.Trim();
                    set => ApplyJudgeCondition(value);
                }

                public string FirstJudgeConditionValue
                {
                    get => _firstJudgeConditionValue;
                    set
                    {
                        string normalizedValue = value?.Trim() ?? string.Empty;
                        if (string.Equals(_firstJudgeConditionValue, normalizedValue, StringComparison.Ordinal))
                        {
                            return;
                        }

                        _firstJudgeConditionValue = normalizedValue;
                        OnPropertyChanged(nameof(FirstJudgeConditionValue));
                        OnPropertyChanged(nameof(ViewJudgeCondition));
                    }
                }

                public string SecondJudgeConditionValue
                {
                    get => _secondJudgeConditionValue;
                    set
                    {
                        string normalizedValue = value?.Trim() ?? string.Empty;
                        if (string.Equals(_secondJudgeConditionValue, normalizedValue, StringComparison.Ordinal))
                        {
                            return;
                        }

                        _secondJudgeConditionValue = normalizedValue;
                        OnPropertyChanged(nameof(SecondJudgeConditionValue));
                        OnPropertyChanged(nameof(ViewJudgeCondition));
                    }
                }

                public bool IsRangeJudgeTemplate =>
                    string.Equals(ViewJudgeType, "<{0}<", StringComparison.Ordinal) ||
                    string.Equals(ViewJudgeType, "<={0}<=", StringComparison.Ordinal);

                /// <summary>
                /// 应用当前编辑结果到目标对象。
                /// </summary>
                private void ApplyJudgeCondition(string? value)
                {
                    string normalizedValue = value?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(ViewJudgeType))
                    {
                        string inferredTemplate = InferJudgeTemplate(normalizedValue);
                        if (!string.IsNullOrWhiteSpace(inferredTemplate))
                        {
                            _viewJudgeType = inferredTemplate;
                            OnPropertyChanged(nameof(ViewJudgeType));
                            OnPropertyChanged(nameof(IsRangeJudgeTemplate));
                        }
                    }

                    if (IsRangeJudgeTemplate)
                    {
                        ParseRangeConditionValue(normalizedValue);
                    }
                    else
                    {
                        _firstJudgeConditionValue = StripSimpleTemplate(normalizedValue, ViewJudgeType);
                        _secondJudgeConditionValue = string.Empty;
                    }

                    OnPropertyChanged(nameof(FirstJudgeConditionValue));
                    OnPropertyChanged(nameof(SecondJudgeConditionValue));
                    OnPropertyChanged(nameof(ViewJudgeCondition));
                }

                /// <summary>
                /// 构建并返回对应的业务数据。
                /// </summary>
                private string BuildRangeConditionValue()
                {
                    string firstValue = _firstJudgeConditionValue.Trim();
                    string secondValue = _secondJudgeConditionValue.Trim();
                    if (string.IsNullOrWhiteSpace(firstValue) && string.IsNullOrWhiteSpace(secondValue))
                    {
                        return string.Empty;
                    }

                    return $"{firstValue}|{secondValue}";
                }

                /// <summary>
                /// 解析范围判断条件中的边界值。
                /// </summary>
                private void ParseRangeConditionValue(string value)
                {
                    string normalizedValue = value?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(normalizedValue))
                    {
                        _firstJudgeConditionValue = string.Empty;
                        _secondJudgeConditionValue = string.Empty;
                        return;
                    }

                    string[] placeholderParts = normalizedValue.Split(
                        new[] { "{0}" },
                        StringSplitOptions.None);
                    if (placeholderParts.Length >= 2)
                    {
                        _firstJudgeConditionValue = TrimRangeBoundary(placeholderParts[0]);
                        _secondJudgeConditionValue = TrimRangeBoundary(placeholderParts[1]);
                        return;
                    }

                    string[] delimiterParts = normalizedValue.Split(
                        new[] { '|', ',', ';', '，', '；' },
                        2,
                        StringSplitOptions.TrimEntries);
                    _firstJudgeConditionValue = delimiterParts.ElementAtOrDefault(0)?.Trim() ?? string.Empty;
                    _secondJudgeConditionValue = delimiterParts.ElementAtOrDefault(1)?.Trim() ?? string.Empty;
                }

                /// <summary>
                /// 根据判断条件推断模板类型。
                /// </summary>
                private static string InferJudgeTemplate(string condition)
                {
                    string normalizedCondition = condition?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(normalizedCondition))
                    {
                        return string.Empty;
                    }

                    if (normalizedCondition.Contains("{0}", StringComparison.Ordinal))
                    {
                        return normalizedCondition.Contains("<={0}<=", StringComparison.Ordinal)
                            ? "<={0}<="
                            : "<{0}<";
                    }

                    foreach (JudgeTemplateOption template in DefaultJudgeTemplateOptions
                                 .Where(template => !IsRangeTemplate(template.Value))
                                 .OrderByDescending(template => template.Value.Length))
                    {
                        if (normalizedCondition.StartsWith(template.Value, StringComparison.OrdinalIgnoreCase))
                        {
                            return template.Value;
                        }
                    }

                    return string.Empty;
                }

                /// <summary>
                /// 判断是否满足指定业务条件。
                /// </summary>
                private static bool IsRangeTemplate(string template)
                {
                    return string.Equals(template, "<{0}<", StringComparison.Ordinal) ||
                           string.Equals(template, "<={0}<=", StringComparison.Ordinal);
                }

                /// <summary>
                /// 从判断条件中移除简单模板前缀。
                /// </summary>
                private static string StripSimpleTemplate(string condition, string template)
                {
                    string normalizedCondition = condition?.Trim() ?? string.Empty;
                    string normalizedTemplate = template?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(normalizedCondition) ||
                        string.IsNullOrWhiteSpace(normalizedTemplate))
                    {
                        return normalizedCondition;
                    }

                    if (normalizedCondition.StartsWith("{0}", StringComparison.Ordinal))
                    {
                        normalizedCondition = normalizedCondition[3..].Trim();
                    }

                    if (normalizedCondition.StartsWith(normalizedTemplate, StringComparison.OrdinalIgnoreCase))
                    {
                        normalizedCondition = normalizedCondition[normalizedTemplate.Length..].Trim();
                    }

                    return normalizedCondition;
                }

                /// <summary>
                /// 清理范围边界值的空白和括号。
                /// </summary>
                private static string TrimRangeBoundary(string value)
                {
                    return (value ?? string.Empty).Trim().Trim('<', '>', '=', ' ');
                }

                /// <summary>
                /// 处理状态或数据变更后的联动刷新。
                /// </summary>
                private void OnPropertyChanged(string propertyName)
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                }
            }

            /// <summary>
            /// 判断是否满足指定业务条件。
            /// </summary>
            private static bool IsSendOnlyProtocolCommand(JsonElement? command)
            {
                return command is not null &&
                       !GetJsonBool(command.Value, "WaitForResponse", defaultValue: true) &&
                       !GetJsonBool(command.Value, "IsParseOnly", defaultValue: false);
            }

            /// <summary>
            /// 在协议配置文件中查找指定指令。
            /// </summary>
            private static JsonElement? FindProtocolCommand(string protocolName, string commandName)
            {
                string directory = Path.Combine(AppContext.BaseDirectory, "Config", "Protocol");
                if (!Directory.Exists(directory))
                {
                    return null;
                }

                foreach (string filePath in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        using JsonDocument document = JsonDocument.Parse(ReadPossiblyEncryptedText(filePath));
                        JsonElement root = document.RootElement;
                        if (!string.Equals(GetJsonString(root, "Name"), protocolName, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (root.TryGetProperty("Commands", out JsonElement commandsElement) &&
                            commandsElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (JsonElement commandElement in commandsElement.EnumerateArray())
                            {
                                if (string.Equals(GetJsonString(commandElement, "Name"), commandName, StringComparison.OrdinalIgnoreCase))
                                {
                                    return commandElement.Clone();
                                }
                            }
                        }

                        if (string.Equals(GetJsonString(root, "CommandName"), commandName, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(commandName, "指令 1", StringComparison.OrdinalIgnoreCase))
                        {
                            return root.Clone();
                        }
                    }
                    catch
                    {
                        // Ignore broken protocol files while opening the parameter drawer.
                    }
                }

                return null;
            }

            /// <summary>
            /// 读取可能经过加密保存的协议配置文本。
            /// </summary>
            private static string ReadPossiblyEncryptedText(string filePath)
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

            /// <summary>
            /// 从 JSON 节点读取字符串属性。
            /// </summary>
            private static string GetJsonString(JsonElement element, string propertyName)
            {
                return element.TryGetProperty(propertyName, out JsonElement propertyElement)
                    ? propertyElement.GetString() ?? string.Empty
                    : string.Empty;
            }

            /// <summary>
            /// 从 JSON 节点读取字符串数组属性。
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
            /// 从 JSON 节点读取布尔属性。
            /// </summary>
            private static bool GetJsonBool(JsonElement element, string propertyName, bool defaultValue)
            {
                if (!element.TryGetProperty(propertyName, out JsonElement propertyElement))
                {
                    return defaultValue;
                }

                return propertyElement.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.String when bool.TryParse(propertyElement.GetString(), out bool value) => value,
                    _ => defaultValue
                };
            }

        }

        #endregion

        /// <summary>
        /// 处理鼠标交互事件。
        /// </summary>
        private void OperationDrawerBackdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ViewModel?.CloseStepEditor();
        }

        /// <summary>
        /// 更新操作编辑抽屉的显示状态。
        /// </summary>
        private void UpdateOperationDrawerVisual(bool animate)
        {
            if (OperationDrawerHost is null || OperationDrawerTranslateTransform is null)
            {
                return;
            }

            bool isOpen = ViewModel?.IsStepEditorOpen == true;
            double targetOpacity = isOpen ? 1d : 0d;
            double targetOffset = isOpen ? 0d : -OperationDrawerClosedOffset;

            if (isOpen)
            {
                OperationDrawerHost.IsHitTestVisible = true;
            }

            if (!animate)
            {
                OperationDrawerHost.BeginAnimation(UIElement.OpacityProperty, null);
                OperationDrawerTranslateTransform.BeginAnimation(TranslateTransform.XProperty, null);
                OperationDrawerHost.Opacity = targetOpacity;
                OperationDrawerTranslateTransform.X = targetOffset;
                OperationDrawerHost.IsHitTestVisible = isOpen;
                return;
            }

            DoubleAnimation opacityAnimation = new(targetOpacity, OperationDrawerAnimationDuration)
            {
                EasingFunction = OperationDrawerEasing
            };

            if (!isOpen)
            {
                opacityAnimation.Completed += (_, _) =>
                {
                    if (ViewModel?.IsStepEditorOpen != true)
                    {
                        OperationDrawerHost.IsHitTestVisible = false;
                    }
                };
            }

            DoubleAnimation translateAnimation = new(targetOffset, OperationDrawerAnimationDuration)
            {
                EasingFunction = OperationDrawerEasing
            };

            OperationDrawerHost.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
            OperationDrawerTranslateTransform.BeginAnimation(TranslateTransform.XProperty, translateAnimation);
        }

        #endregion

        #region 交互辅助方法

        /// <summary>
        /// 判断是否满足指定业务条件。
        /// </summary>
        private static bool IsInlineEditableSchemeStepElement(DependencyObject? source)
        {
            return FindAncestor<TextBox>(source) is not null ||
                   FindAncestor<ComboBox>(source) is not null ||
                   FindAncestor<CheckBox>(source) is not null;
        }

        /// <summary>
        /// 判断是否满足指定业务条件。
        /// </summary>
        private static bool IsInlineEditableOperationCell(DependencyObject? source)
        {
            if (FindAncestor<ComboBox>(source) is not null ||
                FindAncestor<TextBox>(source) is not null ||
                FindAncestor<Button>(source) is not null)
            {
                return true;
            }

            DataGridCell? cell = FindAncestor<DataGridCell>(source);
            string? bindingPath = null;
            if (cell?.Column is DataGridBoundColumn boundColumn &&
                boundColumn.Binding is Binding binding)
            {
                bindingPath = binding.Path?.Path;
            }
            else if (cell?.Column is DataGridTemplateColumn templateColumn)
            {
                bindingPath = templateColumn.SortMemberPath;
            }

            return string.Equals(bindingPath, nameof(WorkStepOperation.OperationObject), StringComparison.Ordinal) ||
                   string.Equals(bindingPath, nameof(WorkStepOperation.InvokeMethod), StringComparison.Ordinal) ||
                   string.Equals(bindingPath, nameof(WorkStepOperation.AreParametersModified), StringComparison.Ordinal) ||
                   string.Equals(bindingPath, nameof(WorkStepOperation.DelayMilliseconds), StringComparison.Ordinal) ||
                   string.Equals(bindingPath, nameof(WorkStepOperation.Remark), StringComparison.Ordinal);

        }

        /// <summary>
        /// 判断是否满足指定业务条件。
        /// </summary>
        private static bool IsOperationSelectionCheckBox(DependencyObject? source)
        {
            return FindAncestor<CheckBox>(source) is not null;
        }

        private static T? FindAncestor<T>(DependencyObject? current)
            where T : DependencyObject
        {
            while (current is not null)
            {
                if (current is T ancestor)
                {
                    return ancestor;
                }

                current = GetParentObject(current);
            }

            return null;
        }

        private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject? root)
            where T : DependencyObject
        {
            if (root is null)
            {
                yield break;
            }

            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int index = 0; index < childCount; index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, index);
                if (child is T typedChild)
                {
                    yield return typedChild;
                }

                foreach (T descendant in FindVisualChildren<T>(child))
                {
                    yield return descendant;
                }
            }
        }

        /// <summary>
        /// 获取指定依赖对象的父级对象。
        /// </summary>
        private static DependencyObject? GetParentObject(DependencyObject source)
        {
            if (source is Visual or System.Windows.Media.Media3D.Visual3D)
            {
                return VisualTreeHelper.GetParent(source);
            }

            if (source is FrameworkContentElement frameworkContentElement)
            {
                return frameworkContentElement.Parent as DependencyObject;
            }

            return null;
        }

        #endregion
    }
}
