using Module.MES.Features.SystemConfig.ViewModels;
using System;
using System.Windows.Controls;

namespace Module.MES.Features.SystemConfig.Views;

public partial class MesSystemConfigView : UserControl
{
    public MesSystemConfigView()
    {
        InitializeComponent();
    }

    public MesSystemConfigView(MesSystemConfigViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }
}
