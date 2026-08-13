using ControlLibrary.Controls.FlowchartEditor.Models;
using Module.Business.Features.StationConfiguration;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Module.Business.Features.Station.Views;

public partial class StationConfigurationView : UserControl
{
    public StationConfigurationView()
    {
        InitializeComponent();
    }

    public StationConfigurationView(StationConfigurationViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    private StationConfigurationViewModel? ViewModel => DataContext as StationConfigurationViewModel;

    private void Editor_NodeDoubleClick(object sender, FlowchartNodeInteractionEventArgs e)
    {
        // 判断块不再使用普通步骤编辑器；双击入口只处理流程图处理块。
        if (e.NodeKind == FlowchartNodeKind.Process)
        {
            ViewModel?.OpenNodeOperationEditor(e, FlowchartPanel.CreateDocumentSnapshot());
        }
    }

    private void NodeOperationEditorSaveButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.TrySaveNodeOperationEdit(FlowchartPanel);
    }

    private void NodeOperationEditorCancelButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.CancelNodeOperationEdit();
    }

    private void NodeOperationEditorBackdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ViewModel?.CancelNodeOperationEdit();
    }
}
