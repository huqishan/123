using Module.Business.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
        private const string SchemeStepDragDataFormat = "Module.Business.WorkStepProfile";
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
        private WorkStepProfile? _pendingDraggedSchemeStep;
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
            _pendingDraggedSchemeStep = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject)?.Item as WorkStepProfile;
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

            WorkStepProfile draggedSchemeStep = _pendingDraggedSchemeStep;
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
            if (TryGetSchemeStepDropInfo(e, out WorkStepProfile? draggedSchemeStep, out WorkStepProfile? targetSchemeStep, out bool insertAfter) &&
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
            out WorkStepProfile? draggedSchemeStep,
            out WorkStepProfile? targetSchemeStep,
            out bool insertAfter)
        {
            draggedSchemeStep = e.Data.GetDataPresent(SchemeStepDragDataFormat)
                ? e.Data.GetData(SchemeStepDragDataFormat) as WorkStepProfile
                : null;
            targetSchemeStep = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject)?.Item as WorkStepProfile;
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
            ViewModel?.OpenInlineParameterEditor(operation);
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

            if (ViewModel is null)
            {
                return;
            }

            if (ViewModel.ApplyInlineParameterEditor())
            {
                CloseInlineParameterDrawer();
                e.Handled = true;
            }
        }

        /// <summary>
        /// 处理状态或数据变更后的联动刷新。
        /// </summary>
        private void InlineParameterTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!ReferenceEquals(e.OriginalSource, sender))
            {
                return;
            }

            InlineReturnParameterDataGrid?.CommitEdit(DataGridEditingUnit.Cell, true);
            InlineReturnParameterDataGrid?.CommitEdit(DataGridEditingUnit.Row, true);
            ViewModel?.RefreshInlineParameterEditor();
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
            ViewModel?.CloseInlineParameterEditor();
            UpdateInlineParameterDrawerVisual(animate: true);
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
