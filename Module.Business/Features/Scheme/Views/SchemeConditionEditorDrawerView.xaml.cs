using Module.Business.Features.Scheme.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Module.Business.Features.Scheme.Views;

/// <summary>
/// 方案判断条件右侧抽屉，负责遮罩交互和侧滑动画，条件业务状态由独立 ViewModel 管理。
/// </summary>
public partial class SchemeConditionEditorDrawerView : UserControl
{
    #region 动画配置与订阅

    private const double ClosedOffset = 56d;
    private static readonly Duration AnimationDuration = new(TimeSpan.FromMilliseconds(220));
    private static readonly IEasingFunction Easing = new CubicEase { EasingMode = EasingMode.EaseOut };
    private SchemeConditionEditorViewModel? _subscribedEditor;

    #endregion

    #region 依赖属性

    public static readonly DependencyProperty EditorProperty = DependencyProperty.Register(
        nameof(Editor),
        typeof(SchemeConditionEditorViewModel),
        typeof(SchemeConditionEditorDrawerView),
        new PropertyMetadata(null, OnEditorChanged));

    public SchemeConditionEditorViewModel? Editor
    {
        get => (SchemeConditionEditorViewModel?)GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    #endregion

    #region 构造与交互

    public SchemeConditionEditorDrawerView()
    {
        InitializeComponent();
        Loaded += SchemeConditionEditorDrawerView_Loaded;
        Unloaded += SchemeConditionEditorDrawerView_Unloaded;
    }

    private static void OnEditorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not SchemeConditionEditorDrawerView drawer)
        {
            return;
        }

        drawer.DetachEditor(e.OldValue as SchemeConditionEditorViewModel);
        drawer.AttachEditor(e.NewValue as SchemeConditionEditorViewModel);
        if (drawer.IsLoaded)
        {
            drawer.UpdateVisual(animate: false);
        }
    }

    private void SchemeConditionEditorDrawerView_Loaded(object sender, RoutedEventArgs e)
    {
        AttachEditor(Editor);
        UpdateVisual(animate: false);
    }

    private void SchemeConditionEditorDrawerView_Unloaded(object sender, RoutedEventArgs e)
    {
        DetachEditor(_subscribedEditor);
    }

    private void DrawerBackdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Editor?.CloseCommand.CanExecute(null) == true)
        {
            Editor.CloseCommand.Execute(null);
        }
    }

    #endregion

    #region 状态订阅与动画

    private void AttachEditor(SchemeConditionEditorViewModel? editor)
    {
        if (editor is null || ReferenceEquals(_subscribedEditor, editor))
        {
            return;
        }

        DetachEditor(_subscribedEditor);
        _subscribedEditor = editor;
        _subscribedEditor.PropertyChanged += Editor_PropertyChanged;
    }

    private void DetachEditor(SchemeConditionEditorViewModel? editor)
    {
        if (editor is null)
        {
            return;
        }

        editor.PropertyChanged -= Editor_PropertyChanged;
        if (ReferenceEquals(_subscribedEditor, editor))
        {
            _subscribedEditor = null;
        }
    }

    private void Editor_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SchemeConditionEditorViewModel.IsOpen))
        {
            UpdateVisual(animate: true);
        }
    }

    private void UpdateVisual(bool animate)
    {
        bool isOpen = Editor?.IsOpen == true;
        double targetOpacity = isOpen ? 1d : 0d;
        double targetOffset = isOpen ? 0d : ClosedOffset;
        if (isOpen)
        {
            DrawerHost.IsHitTestVisible = true;
        }

        if (!animate)
        {
            DrawerHost.Opacity = targetOpacity;
            DrawerTranslateTransform.X = targetOffset;
            DrawerHost.IsHitTestVisible = isOpen;
            return;
        }

        DoubleAnimation opacityAnimation = new(targetOpacity, AnimationDuration) { EasingFunction = Easing };
        if (!isOpen)
        {
            opacityAnimation.Completed += (_, _) => DrawerHost.IsHitTestVisible = Editor?.IsOpen == true;
        }

        DrawerHost.BeginAnimation(OpacityProperty, opacityAnimation);
        DrawerTranslateTransform.BeginAnimation(
            TranslateTransform.XProperty,
            new DoubleAnimation(targetOffset, AnimationDuration) { EasingFunction = Easing });
    }

    #endregion
}
