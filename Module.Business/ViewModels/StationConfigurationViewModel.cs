using ControlLibrary;
using ControlLibrary.Controls.FlowchartEditor.Control;
using ControlLibrary.Controls.FlowchartEditor.Models;
using Microsoft.Win32;
using Module.Business.Models;
using Module.Business.Services;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Module.Business.ViewModels;

/// <summary>
/// 工位配置视图模型，负责工位列表维护、流程图导入导出和流程图预览执行。
/// </summary>
public sealed class StationConfigurationViewModel : ViewModelProperties
{
    #region 样式字段

    /// <summary>
    /// 成功状态提示颜色。
    /// </summary>
    private static readonly Brush SuccessBrush =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A"));

    /// <summary>
    /// 警告状态提示颜色。
    /// </summary>
    private static readonly Brush WarningBrush =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EA580C"));

    /// <summary>
    /// 中性状态提示颜色。
    /// </summary>
    private static readonly Brush NeutralBrush =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));

    /// <summary>
    /// 流程图开始节点颜色。
    /// </summary>
    private static readonly Brush StartBrush =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"));

    /// <summary>
    /// 流程图处理节点颜色。
    /// </summary>
    private static readonly Brush ProcessBrush =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F766E"));

    /// <summary>
    /// 流程图判断节点颜色。
    /// </summary>
    private static readonly Brush DecisionBrush =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A16207"));

    /// <summary>
    /// 流程图结束节点颜色。
    /// </summary>
    private static readonly Brush EndBrush =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));

    #endregion

    #region 私有状态字段

    /// <summary>
    /// 当前工位配置目录。
    /// </summary>
    private readonly StationConfigurationCatalog _catalog = BusinessConfigurationStore.LoadStationCatalog();

    /// <summary>
    /// 当前选中的工位配置。
    /// </summary>
    private StationProfile? _selectedStation;

    /// <summary>
    /// 工位列表搜索关键字。
    /// </summary>
    private string _searchText = string.Empty;

    /// <summary>
    /// 页面状态文本。
    /// </summary>
    private string _pageStatusText = "等待编辑";

    /// <summary>
    /// 页面状态提示颜色。
    /// </summary>
    private Brush _pageStatusBrush = NeutralBrush;

    /// <summary>
    /// 流程图执行状态文本。
    /// </summary>
    private string _executionStatusText = "状态：等待操作";

    /// <summary>
    /// 流程图执行状态颜色。
    /// </summary>
    private Brush _executionStatusBrush = NeutralBrush;

    /// <summary>
    /// 流程图是否正在预览执行。
    /// </summary>
    private bool _isExecuting;

    /// <summary>
    /// 流程图预览执行是否处于暂停状态。
    /// </summary>
    private bool _isPaused;

    /// <summary>
    /// 上一次新建或复制命令触发时间，用于避免短时间重复触发。
    /// </summary>
    private DateTime _lastCreateOrCopyCommandAt = DateTime.MinValue;

    #endregion

    #region 构造与初始化

    /// <summary>
    /// 初始化工位配置视图模型，加载工位、构建节点模板并绑定命令。
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

    #region 绑定集合与属性

    /// <summary>
    /// 当前工位配置集合。
    /// </summary>
    public ObservableCollection<StationProfile> Stations => _catalog.Stations;

    /// <summary>
    /// 工位列表视图，用于搜索过滤和当前项同步。
    /// </summary>
    public ICollectionView StationsView { get; }

    /// <summary>
    /// 流程图节点模板集合。
    /// </summary>
    public ObservableCollection<FlowchartNodeTemplate> NodeTemplates { get; } = new();

    /// <summary>
    /// 流程图预览执行日志集合。
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
    /// 页面状态显示文本。
    /// </summary>
    public string PageStatusText
    {
        get => _pageStatusText;
        private set => SetField(ref _pageStatusText, value);
    }

    /// <summary>
    /// 页面状态显示颜色。
    /// </summary>
    public Brush PageStatusBrush
    {
        get => _pageStatusBrush;
        private set => SetField(ref _pageStatusBrush, value);
    }

    /// <summary>
    /// 流程图执行状态显示文本。
    /// </summary>
    public string ExecutionStatusText
    {
        get => _executionStatusText;
        private set => SetField(ref _executionStatusText, value);
    }

    /// <summary>
    /// 流程图执行状态显示颜色。
    /// </summary>
    public Brush ExecutionStatusBrush
    {
        get => _executionStatusBrush;
        private set => SetField(ref _executionStatusBrush, value);
    }

    /// <summary>
    /// 流程图是否正在预览执行。
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
    /// 流程图预览执行是否暂停。
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
    /// 当前页面是否允许编辑工位配置。
    /// </summary>
    public bool CanEdit => !IsExecuting;

    /// <summary>
    /// 工位数量显示文本。
    /// </summary>
    public string StationCountText => $"{Stations.Count} 个工位";

    /// <summary>
    /// 当前是否存在选中的工位。
    /// </summary>
    public bool HasSelectedStation => SelectedStation is not null;

    /// <summary>
    /// 当前工位摘要显示文本。
    /// </summary>
    public string CurrentStationSummary => SelectedStation?.Summary ?? "未选择工位";

    #endregion

    #region 命令属性

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
    /// 新建一个工位配置并选中。
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

        BusinessConfigurationStore.SaveStationCatalog(_catalog);
        StationsView.Refresh();
        SetPageStatus($"已保存 {Stations.Count} 个工位。", SuccessBrush);
    }

    #endregion

    #region 流程图导入导出

    /// <summary>
    /// 从文件导入当前工位的流程图。
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

    #region 流程图预览执行控制

    /// <summary>
    /// 预览执行当前工位的流程图。
    /// </summary>
    /// <param name="parameter">流程图编辑器控件。</param>
    /// <returns>异步执行任务。</returns>
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
    /// 暂停或继续当前流程图预览执行。
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
    /// 停止当前流程图预览执行。
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
    /// 捕获当前流程图编辑器文档并同步到选中工位。
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

    #region 集合与属性变更跟踪

    /// <summary>
    /// 处理工位集合变更，维护属性订阅并刷新列表状态。
    /// </summary>
    /// <param name="sender">触发变更的集合。</param>
    /// <param name="e">集合变更事件参数。</param>
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
    /// 批量订阅工位属性变更。
    /// </summary>
    /// <param name="stations">待订阅的工位集合。</param>
    private void HookStations(IEnumerable<StationProfile> stations)
    {
        foreach (StationProfile station in stations)
        {
            HookStation(station);
        }
    }

    /// <summary>
    /// 订阅单个工位的属性变更。
    /// </summary>
    /// <param name="station">待订阅的工位配置。</param>
    private void HookStation(StationProfile station)
    {
        station.PropertyChanged += Station_PropertyChanged;
    }

    /// <summary>
    /// 取消订阅单个工位的属性变更。
    /// </summary>
    /// <param name="station">待取消订阅的工位配置。</param>
    private void UnhookStation(StationProfile station)
    {
        station.PropertyChanged -= Station_PropertyChanged;
    }

    /// <summary>
    /// 处理工位属性变更，刷新更新时间、摘要和过滤结果。
    /// </summary>
    /// <param name="sender">触发变更的工位配置。</param>
    /// <param name="e">属性变更事件参数。</param>
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
    /// 判断指定属性变更是否需要刷新最后修改时间。
    /// </summary>
    /// <param name="propertyName">属性名称。</param>
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

    #region 过滤与校验

    /// <summary>
    /// 根据搜索关键字过滤工位。
    /// </summary>
    /// <param name="item">待过滤的工位对象。</param>
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
    /// 判断文本是否包含指定关键字。
    /// </summary>
    /// <param name="source">原始文本。</param>
    /// <param name="keyword">搜索关键字。</param>
    /// <returns>包含关键字时返回 true。</returns>
    private static bool Contains(string? source, string keyword)
    {
        return source?.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// 校验工位名称和编码是否为空或重复。
    /// </summary>
    /// <param name="message">校验失败时的提示文本。</param>
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

    #region 新建选择与名称生成

    /// <summary>
    /// 选中新建或复制出的工位，并清理搜索条件。
    /// </summary>
    /// <param name="station">需要选中的工位配置。</param>
    private void SelectCreatedStation(StationProfile station)
    {
        SearchText = string.Empty;
        StationsView.Refresh();
        SelectedStation = station;
        StationsView.MoveCurrentTo(station);
    }

    /// <summary>
    /// 判断当前是否允许执行新建或复制命令。
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
    /// 生成唯一工位名称。
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
    /// 根据原工位名称生成唯一副本名称。
    /// </summary>
    /// <param name="baseName">原工位名称。</param>
    /// <returns>唯一副本名称。</returns>
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
    /// 生成唯一工位编码。
    /// </summary>
    /// <param name="baseCode">可选的编码前缀。</param>
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
    /// 更新页面状态文本和颜色。
    /// </summary>
    /// <param name="text">状态文本。</param>
    /// <param name="brush">状态颜色。</param>
    private void SetPageStatus(string text, Brush brush)
    {
        PageStatusText = text;
        PageStatusBrush = brush;
    }

    /// <summary>
    /// 更新流程图执行状态。
    /// </summary>
    /// <param name="text">执行状态文本。</param>
    /// <param name="brush">执行状态颜色。</param>
    private void SetExecutionStatus(string text, Brush brush)
    {
        SetPageStatus(text, brush);
    }

    /// <summary>
    /// 刷新所有依赖当前选中工位和执行状态的命令。
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
    /// 刷新单个 RelayCommand 的可执行状态。
    /// </summary>
    /// <param name="command">待刷新的命令。</param>
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
    /// 清理文件名中的非法字符。
    /// </summary>
    /// <param name="fileName">原始文件名。</param>
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
