using Module.Business.Features.OperationEditing.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Module.Business.Features.OperationEditing.Views;

/// <summary>
/// 操作编辑抽屉，统一封装遮罩、标题、命令和开关动画。
/// </summary>
public partial class OperationEditorDrawerView : UserControl
{
    #region 动画配置

    private const double ClosedOffset = 56d;
    private static readonly Duration AnimationDuration = new(TimeSpan.FromMilliseconds(220));
    private static readonly IEasingFunction Easing = new CubicEase { EasingMode = EasingMode.EaseOut };

    #endregion

    #region 依赖属性

    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen), typeof(bool), typeof(OperationEditorDrawerView),
        new PropertyMetadata(false, OnIsOpenChanged));

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(OperationEditorDrawerView), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty BtnTitleProperty = DependencyProperty.Register(
        nameof(BtnTitle), typeof(string), typeof(OperationEditorDrawerView), new PropertyMetadata(string.Empty));
    
    public static readonly DependencyProperty HostStepNameProperty = DependencyProperty.Register(
        nameof(HostStepName), typeof(string), typeof(OperationEditorDrawerView), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty EditorProperty = DependencyProperty.Register(
        nameof(Editor), typeof(OperationEditorViewModel), typeof(OperationEditorDrawerView));

    public static readonly DependencyProperty SaveCommandProperty = DependencyProperty.Register(
        nameof(SaveCommand), typeof(ICommand), typeof(OperationEditorDrawerView));

    public static readonly DependencyProperty CloseCommandProperty = DependencyProperty.Register(
        nameof(CloseCommand), typeof(ICommand), typeof(OperationEditorDrawerView));

    /// <summary>
    /// 是否从宿主区域右侧展开；默认值为 false，以保留方案配置页面原有的左侧抽屉行为。
    /// </summary>
    public static readonly DependencyProperty IsRightAlignedProperty = DependencyProperty.Register(
        nameof(IsRightAligned), typeof(bool), typeof(OperationEditorDrawerView),
        new PropertyMetadata(false, OnIsRightAlignedChanged));

    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string BtnTitle { get => (string)GetValue(BtnTitleProperty); set => SetValue(BtnTitleProperty, value); }
    public string HostStepName { get => (string)GetValue(HostStepNameProperty); set => SetValue(HostStepNameProperty, value); }
    public OperationEditorViewModel? Editor { get => (OperationEditorViewModel?)GetValue(EditorProperty); set => SetValue(EditorProperty, value); }
    public ICommand? SaveCommand { get => (ICommand?)GetValue(SaveCommandProperty); set => SetValue(SaveCommandProperty, value); }
    public ICommand? CloseCommand { get => (ICommand?)GetValue(CloseCommandProperty); set => SetValue(CloseCommandProperty, value); }
    public bool IsRightAligned { get => (bool)GetValue(IsRightAlignedProperty); set => SetValue(IsRightAlignedProperty, value); }

    #endregion

    #region 构造与交互

    public OperationEditorDrawerView()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateVisual(animate: false);
    }

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is OperationEditorDrawerView drawer && drawer.IsLoaded)
        {
            drawer.UpdateVisual(animate: true);
        }
    }

    private static void OnIsRightAlignedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not OperationEditorDrawerView drawer)
        {
            return;
        }

        drawer.DrawerHost.HorizontalAlignment = drawer.IsRightAligned
            ? HorizontalAlignment.Right
            : HorizontalAlignment.Left;

        if (drawer.IsLoaded)
        {
            drawer.UpdateVisual(animate: false);
        }
    }

    private void DrawerBackdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (CloseCommand?.CanExecute(null) == true)
        {
            CloseCommand.Execute(null);
        }
    }

    #endregion

    #region 动画

    private void UpdateVisual(bool animate)
    {
        double targetOpacity = IsOpen ? 1d : 0d;
        double targetOffset = IsOpen ? 0d : IsRightAligned ? ClosedOffset : -ClosedOffset;
        if (IsOpen)
        {
            DrawerHost.IsHitTestVisible = true;
        }

        if (!animate)
        {
            DrawerHost.Opacity = targetOpacity;
            DrawerTranslateTransform.X = targetOffset;
            DrawerHost.IsHitTestVisible = IsOpen;
            return;
        }

        DoubleAnimation opacityAnimation = new(targetOpacity, AnimationDuration) { EasingFunction = Easing };
        if (!IsOpen)
        {
            opacityAnimation.Completed += (_, _) => DrawerHost.IsHitTestVisible = IsOpen;
        }

        DrawerHost.BeginAnimation(OpacityProperty, opacityAnimation);
        DrawerTranslateTransform.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(targetOffset, AnimationDuration) { EasingFunction = Easing });
    }

    #endregion
}
