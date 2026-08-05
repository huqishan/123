using System.Windows;

namespace Module.Business.Features.OperationEditing.Views;

/// <summary>
/// 为不在 WPF 视觉树或逻辑树中的对象传递数据上下文。
/// DataGridColumn 无法继承 DataGrid 的 DataContext，可通过该代理安全访问界面 ViewModel。
/// </summary>
public sealed class BindingProxy : Freezable
{
    #region 依赖属性

    /// <summary>
    /// 代理保存的数据上下文。
    /// </summary>
    public object? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    /// <summary>
    /// Data 依赖属性定义，允许代理对象参与 WPF 数据绑定和属性变更通知。
    /// </summary>
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data),
        typeof(object),
        typeof(BindingProxy),
        new PropertyMetadata(null));

    #endregion

    /// <summary>
    /// 创建代理实例，供 WPF 在资源和绑定过程中复制 Freezable 对象。
    /// </summary>
    protected override Freezable CreateInstanceCore()
    {
        return new BindingProxy();
    }
}
