using System;
using System.Windows.Controls;
using Module.Business.Features.LuaScript;

namespace Module.Business.Features.LuaScript.Views;

/// <summary>
/// Lua script editor view.
/// </summary>
public partial class LuaScriptView : UserControl
{
    public LuaScriptView()
    {
        InitializeComponent();
    }

    public LuaScriptView(LuaScriptViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }
}
