using ControlLibrary;
using ControlLibrary.ControlViews.Flowchart;
using ControlLibrary.Controls.FlowchartEditor.Control;
using ControlLibrary.Controls.FlowchartEditor.Models;
using Microsoft.Win32;
using Module.Business.Features.SchemeConfiguration;
using Module.Business.Models;
using Module.Business.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Module.Business.Features.StationConfiguration;

/// <summary>
/// 工位配置页面视图模型，负责工位列表、流程图导入导出和预览执行状态。
/// </summary>
public sealed class StationConfigurationViewModel : ViewModelProperties
{
    #region 样式字段

    /// <summary>
    /// 成功状态画刷。
    /// </summary>
    private static readonly Brush SuccessBrush =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A"));

    /// <summary>
    /// 警告状态画刷。
    /// </summary>
    private static readonly Brush WarningBrush =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EA580C"));

    /// <summary>
    /// 中性状态画刷。
    /// </summary>
    private static readonly Brush NeutralBrush =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));

    /// <summary>
    /// 开始节点画刷。
    /// </summary>
    private static readonly Brush StartBrush =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"));

    /// <summary>
    /// 处理节点画刷。
    /// </summary>
    private static readonly Brush ProcessBrush =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F766E"));

    /// <summary>
    /// 判断节点画刷。
    /// </summary>
    private static readonly Brush DecisionBrush =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A16207"));

    /// <summary>
    /// 结束节点画刷。
    /// </summary>
    private static readonly Brush EndBrush =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));

    #endregion

    #region 数据与状态字段
    /// <summary>
    /// 当前工位配置目录。
    /// </summary>
    private readonly StationConfigurationCatalog _catalog = StationConfigurationStore.LoadCatalog();

    /// <summary>
    /// 当前选中的工位。
    /// </summary>
    private StationProfile? _selectedStation;

    /// <summary>
    /// 工位搜索关键字。
    /// </summary>
    private string _searchText = string.Empty;

    /// <summary>
    /// 页面顶部状态文本。
    /// </summary>
    private string _pageStatusText = "等待编辑";

    /// <summary>
    /// 页面顶部状态画刷。
    /// </summary>
    private Brush _pageStatusBrush = NeutralBrush;

    /// <summary>
    /// 流程图预览执行状态文本。
    /// </summary>
    private string _executionStatusText = "状态：等待操作";

    /// <summary>
    /// 流程图预览执行状态画刷。
    /// </summary>
    private Brush _executionStatusBrush = NeutralBrush;

    /// <summary>
    /// 是否正在预览执行流程图。
    /// </summary>
    private bool _isExecuting;

    /// <summary>
    /// 当前流程图预览是否已暂停。
    /// </summary>
    private bool _isPaused;

    /// <summary>
    /// 当前流程图节点操作编辑器。
    /// </summary>
    private SchemeConfigurationViewModel? _nodeOperationEditor;

    /// <summary>
    /// 当前正在编辑的流程图节点编号。
    /// </summary>
    private Guid? _editingNodeId;

    /// <summary>
    /// 上一次新建或复制命令触发时间，用于防止连点。
    /// </summary>
    private DateTime _lastCreateOrCopyCommandAt = DateTime.MinValue;

    #endregion

    #region 构造与初始化
    /// <summary>
    /// 初始化工位配置页面数据、集合视图和命令。
    /// </summary>
    public StationConfigurationViewModel()
    {
        Stations.CollectionChanged += Stations_CollectionChanged;
        HookStations(Stations);

        StationsView = CollectionViewSource.GetDefaultView(Stations);
        StationsView.Filter = FilterStations;

        InitializeNodeTemplates();

        SelectedStation = Stations.FirstOrDefault();
        SetPageStatus(
            Stations.Count == 0 ? "暂无工位配置，请点击新建。" : $"已加载 {Stations.Count} 个工位。",
            NeutralBrush);

        NewStationCommand = new RelayCommand(_ => NewStation());
        DuplicateStationCommand = new RelayCommand(_ => DuplicateSelectedStation(), _ => SelectedStation is not null);
        DeleteStationCommand = new RelayCommand(_ => DeleteSelectedStation(), _ => SelectedStation is not null);
        SaveStationCommand = new RelayCommand(_ => SaveStations());
        OpenFlowchartCommand = new RelayCommand(ImportFlowchart, _ => SelectedStation is not null);
        ExportFlowchartCommand = new RelayCommand(ExportFlowchart, _ => SelectedStation is not null);
        ExecuteFlowchartCommand = new RelayCommand(
            async parameter => await ExecuteFlowchartAsync(parameter),
            _ => SelectedStation is not null && !IsExecuting);
        PauseFlowchartCommand = new RelayCommand(
            TogglePauseFlowchart,
            _ => SelectedStation is not null && IsExecuting);
        StopFlowchartCommand = new RelayCommand(
            StopFlowchart,
            _ => SelectedStation is not null && IsExecuting);
    }

    #endregion

    #region 绑定属性
    /// <summary>
    /// 工位配置集合。
    /// </summary>
    public ObservableCollection<StationProfile> Stations => _catalog.Stations;

    /// <summary>
    /// 支持搜索过滤的工位集合视图。
    /// </summary>
    public ICollectionView StationsView { get; }

    /// <summary>
    /// 流程图节点模板集合。
    /// </summary>
    public ObservableCollection<FlowchartNodeTemplate> NodeTemplates { get; } = new();

    /// <summary>
    /// 流程图预览执行日志。
    /// </summary>
    public ObservableCollection<string> ExecutionLogs { get; } = new();

    /// <summary>
    /// 工位搜索关键字。
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetField(ref _searchText, value ?? string.Empty))
            {
                return;
            }

            StationsView.Refresh();
        }
    }

    /// <summary>
    /// 当前选中的工位配置。
    /// </summary>
    public StationProfile? SelectedStation
    {
        get => _selectedStation;
        set
        {
            if (ReferenceEquals(_selectedStation, value))
            {
                return;
            }

            _selectedStation = value;
            ExecutionLogs.Clear();
            SetExecutionStatus("状态：等待操作", NeutralBrush);
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedStation));
            OnPropertyChanged(nameof(CurrentStationSummary));
            RaiseCommandStatesChanged();
        }
    }

    /// <summary>
    /// 页面状态提示文本。
    /// </summary>
    public string PageStatusText
    {
        get => _pageStatusText;
        private set => SetField(ref _pageStatusText, value);
    }

    /// <summary>
    /// 页面状态提示画刷。
    /// </summary>
    public Brush PageStatusBrush
    {
        get => _pageStatusBrush;
        private set => SetField(ref _pageStatusBrush, value);
    }

    /// <summary>
    /// 流程图预览执行状态文本。
    /// </summary>
    public string ExecutionStatusText
    {
        get => _executionStatusText;
        private set => SetField(ref _executionStatusText, value);
    }

    /// <summary>
    /// 流程图预览执行状态画刷。
    /// </summary>
    public Brush ExecutionStatusBrush
    {
        get => _executionStatusBrush;
        private set => SetField(ref _executionStatusBrush, value);
    }

    /// <summary>
    /// 是否正在执行流程图预览。
    /// </summary>
    public bool IsExecuting
    {
        get => _isExecuting;
        private set
        {
            if (SetField(ref _isExecuting, value))
            {
                OnPropertyChanged(nameof(CanEdit));
                RaiseCommandStatesChanged();
            }
        }
    }

    /// <summary>
    /// 流程图预览是否暂停。
    /// </summary>
    public bool IsPaused
    {
        get => _isPaused;
        private set
        {
            if (SetField(ref _isPaused, value))
            {
                RaiseCommandStatesChanged();
            }
        }
    }

    /// <summary>
    /// 当前是否允许编辑工位配置。
    /// </summary>
    /// <summary>
    /// 当前流程图节点操作编辑器。
    /// </summary>
    public SchemeConfigurationViewModel? NodeOperationEditor
    {
        get => _nodeOperationEditor;
        private set
        {
            if (SetField(ref _nodeOperationEditor, value))
            {
                OnPropertyChanged(nameof(IsNodeOperationEditorOpen));
            }
        }
    }

    /// <summary>
    /// 是否正在编辑流程图节点操作。
    /// </summary>
    public bool IsNodeOperationEditorOpen => NodeOperationEditor is not null;

    public bool CanEdit => !IsExecuting;

    /// <summary>
    /// 工位数量显示文本。
    /// </summary>
    public string StationCountText => $"{Stations.Count} 个工位";

    /// <summary>
    /// 是否已选择工位。
    /// </summary>
    public bool HasSelectedStation => SelectedStation is not null;

    /// <summary>
    /// 当前工位摘要信息。
    /// </summary>
    public string CurrentStationSummary => SelectedStation?.Summary ?? "未选择工位";

    #endregion

    #region 页面命令
    /// <summary>
    /// 新建工位命令。
    /// </summary>
    public ICommand NewStationCommand { get; }

    /// <summary>
    /// 复制当前工位命令。
    /// </summary>
    public ICommand DuplicateStationCommand { get; }

    /// <summary>
    /// 删除当前工位命令。
    /// </summary>
    public ICommand DeleteStationCommand { get; }

    /// <summary>
    /// 保存工位配置命令。
    /// </summary>
    public ICommand SaveStationCommand { get; }

    /// <summary>
    /// 导入流程图命令。
    /// </summary>
    public ICommand OpenFlowchartCommand { get; }

    /// <summary>
    /// 导出流程图命令。
    /// </summary>
    public ICommand ExportFlowchartCommand { get; }

    /// <summary>
    /// 预览执行流程图命令。
    /// </summary>
    public ICommand ExecuteFlowchartCommand { get; }

    /// <summary>
    /// 暂停或继续流程图预览命令。
    /// </summary>
    public ICommand PauseFlowchartCommand { get; }

    /// <summary>
    /// 停止流程图预览命令。
    /// </summary>
    public ICommand StopFlowchartCommand { get; }

    #endregion

    #region 节点模板

    /// <summary>
    /// 初始化流程图节点模板。
    /// </summary>
    private void InitializeNodeTemplates()
    {
        NodeTemplates.Add(new FlowchartNodeTemplate("开始", "开始", FlowchartNodeKind.Start, StartBrush));
        NodeTemplates.Add(new FlowchartNodeTemplate("处理", "处理", FlowchartNodeKind.Process, ProcessBrush));
        NodeTemplates.Add(new FlowchartNodeTemplate("判断", "判断", FlowchartNodeKind.Decision, DecisionBrush));
        NodeTemplates.Add(new FlowchartNodeTemplate("结束", "结束", FlowchartNodeKind.End, EndBrush));
    }

    #endregion

    #region 工位命令处理

    /// <summary>
    /// 新建一个工位配置。
    /// </summary>
    private void NewStation()
    {
        if (!CanRunCreateOrCopyCommand())
        {
            return;
        }

        StationProfile station = new()
        {
            StationName = GenerateUniqueStationName(),
            StationCode = GenerateUniqueStationCode(),
            LastModifiedAt = DateTime.Now
        };

        Stations.Add(station);
        SelectCreatedStation(station);
        SetPageStatus("已新增工位，请继续编辑后保存。", SuccessBrush);
    }

    /// <summary>
    /// 复制当前选中的工位配置。
    /// </summary>
    private void DuplicateSelectedStation()
    {
        if (!CanRunCreateOrCopyCommand() || SelectedStation is null)
        {
            return;
        }

        StationProfile station = SelectedStation.CopyAsNew(
            GenerateCopyStationName(SelectedStation.StationName),
            GenerateUniqueStationCode(SelectedStation.StationCode));
        station.LastModifiedAt = DateTime.Now;

        Stations.Add(station);
        SelectCreatedStation(station);
        SetPageStatus($"已复制工位：{station.StationName}", SuccessBrush);
    }

    /// <summary>
    /// 删除当前选中的工位配置。
    /// </summary>
    private void DeleteSelectedStation()
    {
        if (SelectedStation is null)
        {
            return;
        }

        int index = Stations.IndexOf(SelectedStation);
        Stations.Remove(SelectedStation);
        SelectedStation = Stations.Count == 0
            ? null
            : Stations[Math.Clamp(index, 0, Stations.Count - 1)];

        SetPageStatus("已删除工位，点击保存后生效。", WarningBrush);
    }

    /// <summary>
    /// 校验并保存工位配置。
    /// </summary>
    private void SaveStations()
    {
        if (!ValidateStations(out string message))
        {
            SetPageStatus(message, WarningBrush);
            return;
        }

        StationConfigurationStore.SaveCatalog(_catalog);
        StationsView.Refresh();
        SetPageStatus($"已保存 {Stations.Count} 个工位。", SuccessBrush);
    }

    #endregion

    #region 流程图导入导出
    /// <summary>
    /// 从文件导入流程图到当前工位。
    /// </summary>
    /// <param name="parameter">流程图编辑器控件。</param>
    private void ImportFlowchart(object? parameter)
    {
        if (parameter is not FlowchartEditorControl editor || SelectedStation is null)
        {
            SetPageStatus("请先选择工位。", WarningBrush);
            return;
        }

        OpenFileDialog dialog = new()
        {
            Filter = "流程图文件 (*.flowchart.json)|*.flowchart.json|JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
            DefaultExt = ".flowchart.json"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            editor.LoadFromFile(dialog.FileName);
            SelectedStation.FlowchartDocument = editor.CreateDocumentSnapshot();
            SelectedStation.LastModifiedAt = DateTime.Now;
            SetPageStatus($"已导入流程图：{dialog.FileName}", SuccessBrush);
        }
        catch (Exception ex)
        {
            SetPageStatus($"导入流程图失败：{ex.Message}", WarningBrush);
        }
    }

    /// <summary>
    /// 将当前工位流程图导出到文件。
    /// </summary>
    /// <param name="parameter">流程图编辑器控件。</param>
    private void ExportFlowchart(object? parameter)
    {
        if (parameter is not FlowchartEditorControl editor || SelectedStation is null)
        {
            SetPageStatus("请先选择工位。", WarningBrush);
            return;
        }

        CaptureCurrentEditorDocument(editor);

        SaveFileDialog dialog = new()
        {
            Filter = "流程图文件 (*.flowchart.json)|*.flowchart.json|JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
            DefaultExt = ".flowchart.json",
            FileName = $"{SanitizeFileName(SelectedStation.StationName)}.flowchart.json"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            editor.SaveToFile(dialog.FileName);
            SetPageStatus($"已导出流程图：{dialog.FileName}", SuccessBrush);
        }
        catch (Exception ex)
        {
            SetPageStatus($"导出流程图失败：{ex.Message}", WarningBrush);
        }
    }

    #endregion

    #region 流程图预览控制
    /// <summary>
    /// 在页面内预览执行当前流程图。
    /// </summary>
    /// <param name="parameter">流程图编辑器控件。</param>
    /// <returns>异步预览执行任务。</returns>
    #region 流程图节点操作编辑

    /// <summary>
    /// 打开流程图节点操作编辑器。
    /// </summary>
    public bool OpenNodeOperationEditor(FlowchartNodeInteractionEventArgs e, FlowchartDocument document)
    {
        if (!CanEditNode(e.NodeKind) || CanEdit != true || SelectedStation is null)
        {
            return false;
        }

        WorkStepOperation operation = DeserializeNodeOperation(e);
        _editingNodeId = e.NodeId;

        SchemeConfigurationViewModel operationEditor = new();
        operationEditor.SetExternalReturnValueOptions(GetFlowchartReturnValueOptions(document, operationEditor));
        operationEditor.SetOperationObjectOptionsForDecisionMode(e.NodeKind == FlowchartNodeKind.Decision);

        WorkStepProfile temporaryWorkStep = new()
        {
            StepName = GetNodeEditorTitle(e.NodeKind),
            Steps = new ObservableCollection<WorkStepOperation>()
        };

        WorkStepOperation editingOperation = operation.Clone();
        if (e.NodeKind == FlowchartNodeKind.Decision &&
            string.IsNullOrWhiteSpace(editingOperation.OperationObject))
        {
            editingOperation.OperationObject = SchemeConfigurationViewModel.JudgeOperationObjectName;
        }

        temporaryWorkStep.Steps.Add(editingOperation);
        operationEditor.WorkSteps.Clear();
        operationEditor.WorkSteps.Add(temporaryWorkStep);
        operationEditor.SelectedWorkStep = temporaryWorkStep;
        operationEditor.SelectedOperation = editingOperation;
        operationEditor.OpenOperationDrawerForEdit(editingOperation);

        NodeOperationEditor = operationEditor;
        return true;
    }

    /// <summary>
    /// 保存流程图节点操作编辑结果。
    /// </summary>
    public bool TrySaveNodeOperationEdit(FlowchartNodePanelView editor)
    {
        if (NodeOperationEditor is null || _editingNodeId is null || SelectedStation is null)
        {
            return false;
        }

        if (!NodeOperationEditor.TrySaveStepEditor())
        {
            return false;
        }

        WorkStepOperation? operation = NodeOperationEditor.CreateSelectedOperationSnapshot();
        if (operation is null)
        {
            return false;
        }

        FlowchartDocument document = editor.CreateDocumentSnapshot();
        FlowchartNodeDocument? node = document.Nodes.FirstOrDefault(item => item.Id == _editingNodeId.Value);
        if (node is null)
        {
            CloseNodeOperationEditor();
            return false;
        }

        node.MetadataJson = JsonSerializer.Serialize(operation);
        node.Text = BuildNodeText(node.Kind, operation);

        SelectedStation.FlowchartDocument = document;
        editor.LoadDocumentSnapshot(document);

        CloseNodeOperationEditor();
        return true;
    }

    /// <summary>
    /// 取消流程图节点操作编辑。
    /// </summary>
    public void CancelNodeOperationEdit()
    {
        NodeOperationEditor?.CloseStepEditor();
        CloseNodeOperationEditor();
    }

    private void CloseNodeOperationEditor()
    {
        _editingNodeId = null;
        NodeOperationEditor = null;
    }

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
        WorkStepOperation operationTemplate = SchemeConfigurationViewModel.CreateDefaultOperation();
        operationTemplate.OperationObject = operationObject;
        operationTemplate.DeviceId = operationObject;
        operationTemplate.Remark = summary;
        return operationTemplate;
    }

    private static bool CanEditNode(FlowchartNodeKind nodeKind)
    {
        return nodeKind == FlowchartNodeKind.Process || nodeKind == FlowchartNodeKind.Decision;
    }

    private static string GetNodeEditorTitle(FlowchartNodeKind nodeKind)
    {
        return nodeKind == FlowchartNodeKind.Decision
            ? "流程图判断块"
            : "流程图处理块";
    }

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

    private static string ResolveOperationObject(FlowchartNodeKind nodeKind, string firstLine)
    {
        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return nodeKind == FlowchartNodeKind.Process
                ? SchemeConfigurationViewModel.SystemOperationObjectName
                : GetDefaultNodeText(nodeKind);
        }

        if (nodeKind == FlowchartNodeKind.Process &&
            string.Equals(firstLine, "处理", StringComparison.Ordinal))
        {
            return SchemeConfigurationViewModel.SystemOperationObjectName;
        }

        return firstLine.Trim();
    }

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

    private static IEnumerable<string> GetNodeReturnValueOptions(
        FlowchartNodeDocument node,
        SchemeConfigurationViewModel operationEditorViewModel)
    {
        if (!TryDeserializeNodeOperationMetadata(node.MetadataJson, out WorkStepOperation? operation) ||
            operation is null)
        {
            yield break;
        }

        if (!string.IsNullOrWhiteSpace(operation.ReturnValue))
        {
            yield return operation.ReturnValue.Trim();
        }

        foreach (WorkStepOperationParameter parameter in operationEditorViewModel.CreateReturnParametersFromOperation(operation))
        {
            string value = string.IsNullOrWhiteSpace(parameter.ParameterName)
                ? parameter.Value
                : parameter.ParameterName;
            if (!string.IsNullOrWhiteSpace(value))
            {
                yield return value.Trim();
            }
        }
    }

    private static bool TryDeserializeNodeOperationMetadata(string? metadataJson, out WorkStepOperation? operation)
    {
        operation = null;
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return false;
        }

        try
        {
            operation = JsonSerializer.Deserialize<WorkStepOperation>(metadataJson);
            return operation is not null;
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeInlineText(string? text)
    {
        return NormalizeInlineText((text ?? string.Empty)
            .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None));
    }

    private static string NormalizeInlineText(IEnumerable<string> values)
    {
        return string.Join(
            " ",
            values.Select(value => value?.Trim() ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    #endregion

    private async Task ExecuteFlowchartAsync(object? parameter)
    {
        if (parameter is not FlowchartEditorControl editor || SelectedStation is null)
        {
            SetExecutionStatus("状态：请先选择工位。", WarningBrush);
            return;
        }

        CaptureCurrentEditorDocument(editor);

        IsExecuting = true;
        IsPaused = false;
        ExecutionLogs.Clear();
        SetExecutionStatus("状态：开始预览流程图", NeutralBrush);

        try
        {
            void HandleExecutionStepChanged(object? sender, FlowchartExecutionStepEventArgs e)
            {
                ExecutionLogs.Add(e.Message);
                SetExecutionStatus($"状态：{e.Message}", NeutralBrush);
            }

            editor.ExecutionStepChanged += HandleExecutionStepChanged;
            FlowchartExecutionResult result;
            try
            {
                result = await editor.ExecuteFlowAsync();
            }
            finally
            {
                editor.ExecutionStepChanged -= HandleExecutionStepChanged;
            }

            foreach (string step in result.Steps)
            {
                if (!ExecutionLogs.Contains(step))
                {
                    ExecutionLogs.Add(step);
                }
            }

            SetExecutionStatus(
                $"状态：{result.Message}",
                result.IsSuccess ? SuccessBrush : WarningBrush);
        }
        catch (Exception ex)
        {
            SetExecutionStatus($"状态：预览流程图失败：{ex.Message}", WarningBrush);
        }
        finally
        {
            IsPaused = false;
            IsExecuting = false;
        }
    }

    /// <summary>
    /// 切换流程图预览的暂停和继续状态。
    /// </summary>
    /// <param name="parameter">流程图编辑器控件。</param>
    private void TogglePauseFlowchart(object? parameter)
    {
        if (parameter is not FlowchartEditorControl editor || SelectedStation is null)
        {
            SetExecutionStatus("状态：未选择工位。", WarningBrush);
            return;
        }

        bool isSuccess = IsPaused
            ? editor.ResumeExecution()
            : editor.PauseExecution();

        if (isSuccess)
        {
            IsPaused = !IsPaused;
            SetExecutionStatus(IsPaused ? "状态：流程图预览已暂停。" : "状态：流程图预览已继续。", NeutralBrush);
            return;
        }

        SetExecutionStatus(IsPaused ? "状态：流程图预览未处于暂停状态。" : "状态：没有正在预览的流程图。", WarningBrush);
    }

    /// <summary>
    /// 停止当前流程图预览。
    /// </summary>
    /// <param name="parameter">流程图编辑器控件。</param>
    private void StopFlowchart(object? parameter)
    {
        if (parameter is not FlowchartEditorControl editor || SelectedStation is null)
        {
            SetExecutionStatus("状态：未选择工位。", WarningBrush);
            return;
        }

        bool isSuccess = editor.StopExecution();
        if (isSuccess)
        {
            IsPaused = false;
            SetExecutionStatus("状态：已发送停止预览请求。", WarningBrush);
            return;
        }

        SetExecutionStatus("状态：没有正在预览的流程图。", WarningBrush);
    }

    #endregion

    #region 编辑器文档同步
    /// <summary>
    /// 捕获当前编辑器中的流程图文档到选中工位。
    /// </summary>
    /// <param name="parameter">流程图编辑器控件。</param>
    public void CaptureCurrentEditorDocument(object? parameter)
    {
        if (parameter is FlowchartEditorControl editor && SelectedStation is not null)
        {
            SelectedStation.FlowchartDocument = editor.CreateDocumentSnapshot();
        }
    }

    #endregion

    #region 集合与属性变更处理
    /// <summary>
    /// 处理工位集合增删后的事件订阅和界面刷新。
    /// </summary>
    /// <param name="sender">参数 sender。</param>
    /// <param name="e">参数 e。</param>
    private void Stations_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (StationProfile station in e.OldItems.OfType<StationProfile>())
            {
                UnhookStation(station);
            }
        }

        if (e.NewItems is not null)
        {
            foreach (StationProfile station in e.NewItems.OfType<StationProfile>())
            {
                HookStation(station);
            }
        }

        OnPropertyChanged(nameof(StationCountText));
        StationsView.Refresh();
        RaiseCommandStatesChanged();
    }

    /// <summary>
    /// 为工位集合订阅属性变更事件。
    /// </summary>
    /// <param name="stations">参数 stations。</param>
    private void HookStations(IEnumerable<StationProfile> stations)
    {
        foreach (StationProfile station in stations)
        {
            HookStation(station);
        }
    }

    /// <summary>
    /// 为单个工位订阅属性变更事件。
    /// </summary>
    /// <param name="station">参数 station。</param>
    private void HookStation(StationProfile station)
    {
        station.PropertyChanged += Station_PropertyChanged;
    }

    /// <summary>
    /// 取消单个工位的属性变更订阅。
    /// </summary>
    /// <param name="station">参数 station。</param>
    private void UnhookStation(StationProfile station)
    {
        station.PropertyChanged -= Station_PropertyChanged;
    }

    /// <summary>
    /// 处理工位属性变化并刷新状态。
    /// </summary>
    /// <param name="sender">参数 sender。</param>
    /// <param name="e">参数 e。</param>
    private void Station_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not StationProfile station)
        {
            return;
        }

        if (ShouldRefreshLastModified(e.PropertyName))
        {
            station.LastModifiedAt = DateTime.Now;
        }

        if (ReferenceEquals(station, SelectedStation))
        {
            OnPropertyChanged(nameof(CurrentStationSummary));
        }

        StationsView.Refresh();
        SetPageStatus("工位配置已修改，记得保存。", NeutralBrush);
    }

    /// <summary>
    /// 判断指定属性变化是否需要刷新最后修改时间。
    /// </summary>
    /// <param name="propertyName">参数 propertyName。</param>
    /// <returns>需要刷新时返回 true。</returns>
    private static bool ShouldRefreshLastModified(string? propertyName)
    {
        return propertyName is nameof(StationProfile.StationName)
            or nameof(StationProfile.StationCode)
            or nameof(StationProfile.IsEnabled)
            or nameof(StationProfile.FlowchartDocument)
            or nameof(StationProfile.Summary);
    }

    #endregion

    #region 搜索与校验
    /// <summary>
    /// 根据搜索关键字过滤工位列表。
    /// </summary>
    /// <param name="item">参数 item。</param>
    /// <returns>符合过滤条件时返回 true。</returns>
    private bool FilterStations(object item)
    {
        if (item is not StationProfile station)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        string keyword = SearchText.Trim();
        return Contains(station.StationName, keyword) ||
               Contains(station.StationCode, keyword);
    }

    /// <summary>
    /// 判断文本是否包含搜索关键字。
    /// </summary>
    /// <param name="source">参数 source。</param>
    /// <param name="keyword">参数 keyword。</param>
    /// <returns>包含关键字时返回 true。</returns>
    private static bool Contains(string? source, string keyword)
    {
        return source?.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// 校验工位名称和编码是否完整且唯一。
    /// </summary>
    /// <param name="message">参数 message。</param>
    /// <returns>校验通过时返回 true。</returns>
    private bool ValidateStations(out string message)
    {
        HashSet<string> stationNames = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> stationCodes = new(StringComparer.OrdinalIgnoreCase);

        foreach (StationProfile station in Stations)
        {
            if (string.IsNullOrWhiteSpace(station.StationName))
            {
                message = "工位名称不能为空。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(station.StationCode))
            {
                message = $"工位“{station.StationName}”的工位编码不能为空。";
                return false;
            }

            if (!stationNames.Add(station.StationName.Trim()))
            {
                message = $"工位名称不能重复：{station.StationName}";
                return false;
            }

            if (!stationCodes.Add(station.StationCode.Trim()))
            {
                message = $"工位编码不能重复：{station.StationCode}";
                return false;
            }
        }

        message = string.Empty;
        return true;
    }

    #endregion

    #region 工位名称与编码工具
    /// <summary>
    /// 选择刚创建或复制出的工位。
    /// </summary>
    /// <param name="station">参数 station。</param>
    private void SelectCreatedStation(StationProfile station)
    {
        SearchText = string.Empty;
        StationsView.Refresh();
        SelectedStation = station;
        StationsView.MoveCurrentTo(station);
    }

    /// <summary>
    /// 判断是否允许执行新建或复制命令。
    /// </summary>
    /// <returns>允许执行时返回 true。</returns>
    private bool CanRunCreateOrCopyCommand()
    {
        DateTime now = DateTime.UtcNow;
        if (now - _lastCreateOrCopyCommandAt < TimeSpan.FromMilliseconds(300))
        {
            return false;
        }

        _lastCreateOrCopyCommandAt = now;
        return true;
    }

    /// <summary>
    /// 生成不重复的新工位名称。
    /// </summary>
    /// <returns>唯一工位名称。</returns>
    private string GenerateUniqueStationName()
    {
        HashSet<string> existingNames = new(Stations.Select(station => station.StationName), StringComparer.OrdinalIgnoreCase);
        for (int index = 1; ; index++)
        {
            string candidate = $"工位 {index}";
            if (!existingNames.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>
    /// 基于原名称生成复制工位名称。
    /// </summary>
    /// <param name="baseName">参数 baseName。</param>
    /// <returns>唯一复制工位名称。</returns>
    private string GenerateCopyStationName(string baseName)
    {
        HashSet<string> existingNames = new(Stations.Select(station => station.StationName), StringComparer.OrdinalIgnoreCase);
        string copyName = $"{baseName.Trim()} 副本";
        if (!existingNames.Contains(copyName))
        {
            return copyName;
        }

        for (int index = 2; ; index++)
        {
            string candidate = $"{copyName} {index}";
            if (!existingNames.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>
    /// 生成不重复的工位编码。
    /// </summary>
    /// <param name="baseCode">参数 baseCode。</param>
    /// <returns>唯一工位编码。</returns>
    private string GenerateUniqueStationCode(string? baseCode = null)
    {
        HashSet<string> existingCodes = new(
            Stations.Select(station => station.StationCode?.Trim() ?? string.Empty),
            StringComparer.OrdinalIgnoreCase);

        string root = string.IsNullOrWhiteSpace(baseCode) ? "ST" : baseCode.Trim().ToUpperInvariant();
        for (int index = 1; ; index++)
        {
            string candidate = string.IsNullOrWhiteSpace(baseCode)
                ? $"ST-{index:00}"
                : $"{root}-{index:00}";
            if (!existingCodes.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    #endregion

    #region 状态与命令刷新

    /// <summary>
    /// 更新页面状态提示。
    /// </summary>
    /// <param name="text">参数 text。</param>
    /// <param name="brush">参数 brush。</param>
    private void SetPageStatus(string text, Brush brush)
    {
        PageStatusText = text;
        PageStatusBrush = brush;
    }

    /// <summary>
    /// 更新流程图预览执行状态。
    /// </summary>
    /// <param name="text">参数 text。</param>
    /// <param name="brush">参数 brush。</param>
    private void SetExecutionStatus(string text, Brush brush)
    {
        SetPageStatus(text, brush);
    }

    /// <summary>
    /// 刷新所有命令的可执行状态。
    /// </summary>
    private void RaiseCommandStatesChanged()
    {
        RaiseCommandState(DuplicateStationCommand);
        RaiseCommandState(DeleteStationCommand);
        RaiseCommandState(OpenFlowchartCommand);
        RaiseCommandState(ExportFlowchartCommand);
        RaiseCommandState(ExecuteFlowchartCommand);
        RaiseCommandState(PauseFlowchartCommand);
        RaiseCommandState(StopFlowchartCommand);
    }

    /// <summary>
    /// 刷新单个命令的可执行状态。
    /// </summary>
    /// <param name="command">参数 command。</param>
    private static void RaiseCommandState(ICommand? command)
    {
        if (command is RelayCommand relayCommand)
        {
            relayCommand.RaiseCanExecuteChanged();
        }
    }

    #endregion

    #region 文件名工具
    /// <summary>
    /// 清理流程图导出文件名中的非法字符。
    /// </summary>
    /// <param name="fileName">参数 fileName。</param>
    /// <returns>可用于文件系统的安全文件名。</returns>
    private static string SanitizeFileName(string fileName)
    {
        string safeName = string.IsNullOrWhiteSpace(fileName) ? "flowchart" : fileName.Trim();
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            safeName = safeName.Replace(invalidChar, '_');
        }

        return safeName;
    }

    #endregion
}
