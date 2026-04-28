using Shared.Infrastructure.Lua;
using System;
using ControlLibrary.Controls.LuaScripEditor.Control;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ControlLibrary.ControlViews.LuaScrip
{
    /// <summary>
    /// LuaScriptView.xaml 的交互逻辑
    /// </summary>
    public partial class LuaScriptView : UserControl, INotifyPropertyChanged
    {
        public LuaScriptView()
        {
            InitializeComponent();
            DataContext = this;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
