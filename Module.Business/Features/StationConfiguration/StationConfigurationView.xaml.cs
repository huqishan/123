using ControlLibrary.Controls.FlowchartEditor.Models;
using Module.Business.Features.SchemeConfiguration;
using Module.Business.Models;
using Module.Business.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Module.Business.Features.StationConfiguration;

/// <summary>
/// 工位配置视图，负责工位表单和右侧流程图编辑器交互。
/// </summary>
public partial class StationConfigurationView : UserControl
{
    private SchemeConfigurationViewModel? _nodeOperationEditorViewModel;
    private Guid? _editingNodeId;

    /// <summary>
    /// 初始化工位配置视图。
    /// </summary>
    public StationConfigurationView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 使用指定视图模型初始化工位配置视图。
    /// </summary>
    /// <param name="viewModel">工位配置视图模型。</param>
    public StationConfigurationView(StationConfigurationViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    private StationConfigurationViewModel? ViewModel => DataContext as StationConfigurationViewModel;

    /// <summary>
    /// 处理界面按钮点击事件。
    /// </summary>
    private void Editor_NodeDoubleClick(object sender, FlowchartNodeInteractionEventArgs e)
    {
        OpenNodeOperationEditor(e);
    }

    /// <summary>
    /// 处理界面按钮点击事件。
    /// </summary>
    private void NodeOperationEditorSaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_nodeOperationEditorViewModel is null || _editingNodeId is null || ViewModel?.SelectedStation is null)
        {
            return;
        }

        if (!_nodeOperationEditorViewModel.TrySaveStandaloneOperationEdit())
        {
            return;
        }

        WorkStepOperation? operation = _nodeOperationEditorViewModel.CreateEditedOperationSnapshot();
        if (operation is null)
        {
            return;
        }

        FlowchartDocument document = FlowchartPanel.CreateDocumentSnapshot();
        FlowchartNodeDocument? node = document.Nodes.FirstOrDefault(item => item.Id == _editingNodeId.Value);
        if (node is null)
        {
            CloseNodeOperationEditor(cancelChanges: false);
            return;
        }

        node.MetadataJson = SerializeNodeOperation(operation);
        node.Text = BuildNodeText(node.Kind, operation);

        ViewModel.SelectedStation.FlowchartDocument = document;
        FlowchartPanel.LoadDocumentSnapshot(document);

        CloseNodeOperationEditor(cancelChanges: false);
    }

    /// <summary>
    /// 处理界面按钮点击事件。
    /// </summary>
    private void NodeOperationEditorCancelButton_Click(object sender, RoutedEventArgs e)
    {
        CloseNodeOperationEditor(cancelChanges: true);
    }

    /// <summary>
    /// 处理鼠标交互事件。
    /// </summary>
    private void NodeOperationEditorBackdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        CloseNodeOperationEditor(cancelChanges: true);
    }

    /// <summary>
    /// 关闭对应的编辑界面或抽屉。
    /// </summary>
    private void CloseNodeOperationEditor(bool cancelChanges)
    {
        if (cancelChanges)
        {
            _nodeOperationEditorViewModel?.CancelStandaloneOperationEdit();
        }

        _nodeOperationEditorViewModel = null;
        _editingNodeId = null;
        NodeOperationEditorHost.Tag = null;
        NodeOperationEditorHost.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// 从流程图节点事件中反序列化工步操作。
    /// </summary>
    private static WorkStepOperation DeserializeNodeOperation(FlowchartNodeInteractionEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(e.MetadataJson))
        {
            if (TryDeserializeNodeOperationMetadata(e.MetadataJson, out WorkStepOperation? operation) &&
                operation is not null)
            {
                return operation;
            }
        }

        string[] lines = (e.Text ?? string.Empty)
            .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        string firstLine = lines
            .Select(line => line?.Trim() ?? string.Empty)
            .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line))
            ?? string.Empty;
        string summary = NormalizeInlineText(lines.Skip(1));

        string operationObject = ResolveOperationObject(e.NodeKind, firstLine);
        return new WorkStepOperation
        {
            OperationObject = operationObject,
            InvokeMethod = string.Empty,
            DelayMilliseconds = 0,
            Remark = summary
        };
    }

    /// <summary>
    /// 判断指定节点类型是否允许编辑操作。
    /// </summary>
    private static bool CanEditNode(FlowchartNodeKind nodeKind)
    {
        return nodeKind == FlowchartNodeKind.Process || nodeKind == FlowchartNodeKind.Decision;
    }

    /// <summary>
    /// 打开对应的编辑界面或抽屉。
    /// </summary>
    private void OpenNodeOperationEditor(FlowchartNodeInteractionEventArgs e)
    {
        if (!CanEditNode(e.NodeKind) || ViewModel?.CanEdit != true || ViewModel.SelectedStation is null)
        {
            return;
        }

        WorkStepOperation operation = DeserializeNodeOperation(e);
        _editingNodeId = e.NodeId;

        _nodeOperationEditorViewModel = new SchemeConfigurationViewModel();
        _nodeOperationEditorViewModel.SetStandaloneReturnValueOptions(
            GetFlowchartReturnValueOptions(FlowchartPanel.CreateDocumentSnapshot(), _nodeOperationEditorViewModel));
        _nodeOperationEditorViewModel.BeginStandaloneOperationEdit(
            operation,
            GetNodeEditorTitle(e.NodeKind),
            e.NodeKind == FlowchartNodeKind.Decision);

        NodeOperationEditorHost.Tag = _nodeOperationEditorViewModel;
        NodeOperationEditorHost.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// 获取节点操作编辑窗口标题。
    /// </summary>
    private static string GetNodeEditorTitle(FlowchartNodeKind nodeKind)
    {
        return nodeKind == FlowchartNodeKind.Decision
            ? "流程图判断块"
            : "流程图处理块";
    }

    /// <summary>
    /// 构建并返回对应的业务数据。
    /// </summary>
    private static string BuildNodeText(FlowchartNodeKind nodeKind, WorkStepOperation operation)
    {
        string operationObject = string.IsNullOrWhiteSpace(operation.OperationObject)
            ? GetDefaultNodeText(nodeKind)
            : operation.OperationObject.Trim();
        string summary = NormalizeInlineText(operation.Remark);

        return string.IsNullOrWhiteSpace(summary)
            ? operationObject
            : $"{operationObject} {summary}";
    }

    /// <summary>
    /// 解析并返回对应的业务值。
    /// </summary>
    private static string ResolveOperationObject(FlowchartNodeKind nodeKind, string firstLine)
    {
        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return nodeKind == FlowchartNodeKind.Process ? "System" : GetDefaultNodeText(nodeKind);
        }

        if (nodeKind == FlowchartNodeKind.Process &&
            string.Equals(firstLine, "处理", StringComparison.Ordinal))
        {
            return "System";
        }

        return firstLine.Trim();
    }

    /// <summary>
    /// 获取指定节点类型的默认文本。
    /// </summary>
    private static string GetDefaultNodeText(FlowchartNodeKind nodeKind)
    {
        return nodeKind switch
        {
            FlowchartNodeKind.Decision => "判断",
            FlowchartNodeKind.Start => "开始",
            FlowchartNodeKind.End => "结束",
            _ => "处理"
        };
    }

    /// <summary>
    /// 获取流程图中可供当前节点引用的返回值选项。
    /// </summary>
    private static IEnumerable<string> GetFlowchartReturnValueOptions(
        FlowchartDocument document,
        SchemeConfigurationViewModel operationEditorViewModel)
    {
        return document.Nodes
            .SelectMany(node => GetNodeReturnValueOptions(node, operationEditorViewModel))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 获取单个节点操作暴露的返回值选项。
    /// </summary>
    private static IEnumerable<string> GetNodeReturnValueOptions(
        FlowchartNodeDocument node,
        SchemeConfigurationViewModel operationEditorViewModel)
    {
        if (!TryDeserializeNodeOperationMetadata(node.MetadataJson, out WorkStepOperation? operation) ||
            operation is null)
        {
            yield break;
        }

        foreach (WorkStepOperationParameter parameter in operationEditorViewModel.CreateReturnParametersFromOperation(operation))
        {
            string value = WorkStepOperationRuntimeMetadata.GetReturnParameterKey(parameter);
            if (!string.IsNullOrWhiteSpace(value))
            {
                yield return value.Trim();
            }
        }
    }

    /// <summary>
    /// 尝试执行操作并返回是否成功。
    /// </summary>
    private static bool TryDeserializeNodeOperationMetadata(string? metadataJson, out WorkStepOperation? operation)
    {
        operation = null;
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return false;
        }

        try
        {
            WorkStepOperation? parsed = JsonSerializer.Deserialize<WorkStepOperation>(metadataJson);
            operation = parsed is null
                ? null
                : BusinessConfigurationStore.NormalizeWorkStepOperation(parsed);
            return operation is not null;
        }
        catch
        {
            return false;
        }
    }

    private static string SerializeNodeOperation(WorkStepOperation operation)
    {
        return JsonSerializer.Serialize(BusinessConfigurationStore.NormalizeWorkStepOperation(operation));
    }

    /// <summary>
    /// 规范化输入数据并返回可用值。
    /// </summary>
    private static string NormalizeInlineText(string? text)
    {
        return NormalizeInlineText((text ?? string.Empty)
            .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None));
    }

    /// <summary>
    /// 规范化输入数据并返回可用值。
    /// </summary>
    private static string NormalizeInlineText(IEnumerable<string> values)
    {
        return string.Join(
            " ",
            values.Select(value => value?.Trim() ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value)));
    }
}
