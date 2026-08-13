using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ControlLibrary.Controls.MessageDialog;

/// <summary>
/// 通用消息弹框，统一提供提示图标、按钮组合和明确的点击结果。
/// </summary>
public partial class MessageDialog : Window
{
    #region 私有字段

    private MessageDialogResult _result = MessageDialogResult.None;
    private MessageDialogResult _closeResult;

    #endregion

    #region 构造与公开入口

    /// <summary>
    /// 创建默认消息弹框。公开无参构造函数用于 WPF 设计器及宿主程序的窗口依赖注入扫描。
    /// 业务代码需要显示消息时应优先调用 <see cref="Show(string,string,MessageDialogButtons,MessageDialogIcon,Window?)"/>。
    /// </summary>
    public MessageDialog()
    {
        InitializeComponent();
        Configure(string.Empty, "提示", MessageDialogButtons.Ok, MessageDialogIcon.None);
    }

    private MessageDialog(string message, string title, MessageDialogButtons buttons, MessageDialogIcon icon)
    {
        InitializeComponent();
        Configure(message, title, buttons, icon);
    }

    /// <summary>
    /// 统一初始化标题、正文、图标和按钮，保证容器构造与静态显示入口使用相同界面状态。
    /// </summary>
    private void Configure(string message, string title, MessageDialogButtons buttons, MessageDialogIcon icon)
    {
        Title = string.IsNullOrWhiteSpace(title) ? "提示" : title.Trim();
        TitleTextBlock.Text = Title;
        MessageTextBlock.Text = message ?? string.Empty;
        _closeResult = GetCloseResult(buttons);
        ConfigureIcon(icon);
        ConfigureButtons(buttons);
    }

    /// <summary>
    /// 显示模态消息弹框，并返回用户点击的按钮结果。
    /// </summary>
    public static MessageDialogResult Show(
        string message,
        string title = "提示",
        MessageDialogButtons buttons = MessageDialogButtons.Ok,
        MessageDialogIcon icon = MessageDialogIcon.None,
        Window? owner = null)
    {
        MessageDialog dialog = new(message, title, buttons, icon);
        Window? resolvedOwner = owner ?? Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive);
        if (resolvedOwner is not null && resolvedOwner != dialog)
        {
            dialog.Owner = resolvedOwner;
        }

        dialog.ShowDialog();
        return dialog._result;
    }

    #endregion

    #region 图标与按钮配置

    private void ConfigureIcon(MessageDialogIcon icon)
    {
        (string text, string color) = icon switch
        {
            MessageDialogIcon.Information => ("i", "#2563EB"),
            MessageDialogIcon.Success => ("✓", "#16A34A"),
            MessageDialogIcon.Warning => ("!", "#D97706"),
            MessageDialogIcon.Error => ("×", "#DC2626"),
            MessageDialogIcon.Question => ("?", "#7C3AED"),
            _ => (string.Empty, "Transparent")
        };

        IconTextBlock.Text = text;
        IconBorder.Background = (Brush)new BrushConverter().ConvertFromString(color)!;
        IconBorder.Visibility = icon == MessageDialogIcon.None ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ConfigureButtons(MessageDialogButtons buttons)
    {
        (string Text, MessageDialogResult Result, bool IsPrimary)[] definitions = buttons switch
        {
            MessageDialogButtons.OkCancel => [("取消", MessageDialogResult.Cancel, false), ("确定", MessageDialogResult.Ok, true)],
            MessageDialogButtons.YesNo => [("否", MessageDialogResult.No, false), ("是", MessageDialogResult.Yes, true)],
            MessageDialogButtons.YesNoCancel => [("取消", MessageDialogResult.Cancel, false), ("否", MessageDialogResult.No, false), ("是", MessageDialogResult.Yes, true)],
            MessageDialogButtons.RetryCancel => [("取消", MessageDialogResult.Cancel, false), ("重试", MessageDialogResult.Retry, true)],
            _ => [("确定", MessageDialogResult.Ok, true)]
        };

        foreach ((string text, MessageDialogResult result, bool isPrimary) in definitions)
        {
            Button button = new()
            {
                Content = text,
                Tag = result,
                IsDefault = isPrimary,
                IsCancel = result == MessageDialogResult.Cancel,
                Style = (Style)FindResource(isPrimary ? "PrimaryDialogButtonStyle" : "DialogButtonStyle")
            };
            button.Click += ResultButton_Click;
            ButtonPanel.Children.Add(button);
        }
    }

    private static MessageDialogResult GetCloseResult(MessageDialogButtons buttons)
    {
        return buttons switch
        {
            MessageDialogButtons.YesNo => MessageDialogResult.No,
            MessageDialogButtons.Ok => MessageDialogResult.Ok,
            _ => MessageDialogResult.Cancel
        };
    }

    #endregion

    #region 界面事件

    private void ResultButton_Click(object sender, RoutedEventArgs e)
    {
        _result = (MessageDialogResult)((Button)sender).Tag;
        DialogResult = true;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _result = _closeResult;
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    #endregion
}

/// <summary>
/// 弹框支持的标准按钮组合。
/// </summary>
public enum MessageDialogButtons
{
    Ok,
    OkCancel,
    YesNo,
    YesNoCancel,
    RetryCancel
}

/// <summary>
/// 弹框提示图标类型。
/// </summary>
public enum MessageDialogIcon
{
    None,
    Information,
    Success,
    Warning,
    Error,
    Question
}

/// <summary>
/// 用户关闭弹框时返回的按钮结果。
/// </summary>
public enum MessageDialogResult
{
    None,
    Ok,
    Cancel,
    Yes,
    No,
    Retry
}
