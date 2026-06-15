using ControlLibrary;
using System.Linq;
using System.Windows.Data;
using Module.Communication.Features.ProtocolConfig.Models;
using Module.Communication.Features.ProtocolConfig.Services;
using Module.Communication.Features.ProtocolConfig.ViewModels.PresentationModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Module.Communication.Features.ProtocolConfig.ViewModels;

public sealed class ProtocolConfigViewModel : ViewModelProperties
{
    #region 构造方法
    public ProtocolConfigViewModel()
    {
        _previewUpdateDebounceTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = PreviewUpdateDebounceInterval
        };
        _previewUpdateDebounceTimer.Tick += (_, _) =>
        {
            _previewUpdateDebounceTimer.Stop();
            UpdatePreviews();
        };

        InitializeOptionCollections();
        InitializeCommands();

        int loadedProfileCount = _protocolStore.Load(AddProfile, message => SetPageStatus(message, WarningBrush));
        if (loadedProfileCount == 0)
        {
            SeedProfiles();
            SetPageStatus("未发现本地协议配置，已创建默认示例。", NeutralBrush);
        }
        else
        {
            SetPageStatus($"已从 {_protocolStore.ConfigDirectory} 读取 {loadedProfileCount} 个协议配置。", SuccessBrush);
        }

        ProfilesView = CollectionViewSource.GetDefaultView(Profiles);
        ProfilesView.Filter = FilterProfiles;
        SelectedProfile = Profiles.FirstOrDefault();
    }

    #endregion

    #region 状态颜色与路径字段
    private static readonly string ProtocolConfigDirectory =
        System.IO.Path.Combine(AppContext.BaseDirectory, "Config", "Protocol");

    private static readonly Brush SuccessBrush =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A"));

    private static readonly Brush WarningBrush =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EA580C"));

    private static readonly Brush NeutralBrush =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
    private static readonly TimeSpan PreviewUpdateDebounceInterval = TimeSpan.FromMilliseconds(300);

    #endregion

    #region 私有状态字段
    private readonly ProtocolStore _protocolStore = new(ProtocolConfigDirectory);
    private readonly DispatcherTimer _previewUpdateDebounceTimer;

    #endregion

    #region 集合属性
    #region 协议配置集合
    /// <summary>
    /// 协议配置集合。
    /// </summary>
    public ObservableCollection<ProtocolConfigProfile> Profiles { get; } = new();
    #endregion

    #region 协议配置视图
    /// <summary>
    /// 协议配置列表视图。
    /// </summary>
    public ICollectionView ProfilesView { get; private set; } = null!;
    #endregion

    #region 载荷格式选项
    /// <summary>
    /// 载荷格式下拉选项。
    /// </summary>
    public ObservableCollection<ProtocolOption<ProtocolPayloadFormat>> PayloadFormats { get; } = new();
    #endregion

    #region 执行模式选项
    /// <summary>
    /// 执行模式下拉选项。
    /// </summary>
    public ObservableCollection<ProtocolOption<ProtocolExecutionMode>> ExecutionModes { get; } = new();
    #endregion

    #region CRC 校验选项
    /// <summary>
    /// CRC 校验下拉选项。
    /// </summary>
    public ObservableCollection<ProtocolOption<ProtocolCrcMode>> CrcModes { get; } = new();
    #endregion

    #endregion

    #region 当前编辑属性
    #region 当前协议配置
    private ProtocolConfigProfile? _selectedProfile;

    /// <summary>
    /// 当前协议配置。
    /// </summary>
    public ProtocolConfigProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (ReferenceEquals(_selectedProfile, value))
            {
                return;
            }

            if (_selectedProfile is not null)
            {
                _selectedProfile.PropertyChanged -= SelectedProfile_PropertyChanged;
            }

            _selectedProfile = value;
            _previewUpdateDebounceTimer.Stop();

            if (_selectedProfile is not null)
            {
                _selectedProfile.PropertyChanged += SelectedProfile_PropertyChanged;
            }

            OnPropertyChanged();
            UpdatePreviews();
            CloseCommandDrawer();
            ClearGeneratedOutputs();
            RaiseCommandStatesChanged();
        }
    }
    #endregion

    #region 搜索文本
    private string _searchText = string.Empty;

    /// <summary>
    /// 搜索文本。
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

            ProfilesView?.Refresh();
        }
    }
    #endregion

    #region 指令抽屉打开状态
    private bool _isCommandDrawerOpen;

    /// <summary>
    /// 指令抽屉打开状态。
    /// </summary>
    public bool IsCommandDrawerOpen
    {
        get => _isCommandDrawerOpen;
        private set
        {
            if (SetField(ref _isCommandDrawerOpen, value))
            {
                RaiseCommandStatesChanged();
            }
        }
    }
    #endregion

    #endregion

    #region 页面状态属性
    #region 页面状态文本
    private string _pageStatusText = "等待初始化";

    /// <summary>
    /// 页面状态文本。
    /// </summary>
    public string PageStatusText
    {
        get => _pageStatusText;
        private set => SetField(ref _pageStatusText, value);
    }
    #endregion

    #region 页面状态颜色
    private Brush _pageStatusBrush = NeutralBrush;

    /// <summary>
    /// 页面状态颜色。
    /// </summary>
    public Brush PageStatusBrush
    {
        get => _pageStatusBrush;
        private set => SetField(ref _pageStatusBrush, value);
    }
    #endregion

    #region 发送预览状态文本
    private string _requestPreviewStatusText = "等待输入";

    /// <summary>
    /// 发送预览状态文本。
    /// </summary>
    public string RequestPreviewStatusText
    {
        get => _requestPreviewStatusText;
        private set => SetField(ref _requestPreviewStatusText, value);
    }
    #endregion

    #region 发送预览状态颜色
    private Brush _requestPreviewStatusBrush = NeutralBrush;

    /// <summary>
    /// 发送预览状态颜色。
    /// </summary>
    public Brush RequestPreviewStatusBrush
    {
        get => _requestPreviewStatusBrush;
        private set => SetField(ref _requestPreviewStatusBrush, value);
    }
    #endregion

    #region 返回预览状态文本
    private string _responsePreviewStatusText = "等待输入";

    /// <summary>
    /// 返回预览状态文本。
    /// </summary>
    public string ResponsePreviewStatusText
    {
        get => _responsePreviewStatusText;
        private set => SetField(ref _responsePreviewStatusText, value);
    }
    #endregion

    #region 返回预览状态颜色
    private Brush _responsePreviewStatusBrush = NeutralBrush;

    /// <summary>
    /// 返回预览状态颜色。
    /// </summary>
    public Brush ResponsePreviewStatusBrush
    {
        get => _responsePreviewStatusBrush;
        private set => SetField(ref _responsePreviewStatusBrush, value);
    }
    #endregion

    #region 发送预览文本
    private string _requestPreviewText = "请先选择或创建一个协议配置。";

    /// <summary>
    /// 发送预览文本。
    /// </summary>
    public string RequestPreviewText
    {
        get => _requestPreviewText;
        private set => SetField(ref _requestPreviewText, value);
    }
    #endregion

    #region 返回预览文本
    private string _responsePreviewText = "请填写示例返回数据后查看解析预览。";

    /// <summary>
    /// 返回预览文本。
    /// </summary>
    public string ResponsePreviewText
    {
        get => _responsePreviewText;
        private set => SetField(ref _responsePreviewText, value);
    }
    #endregion

    #region 生成指令文本
    private string _generatedCommandText = string.Empty;

    /// <summary>
    /// 生成指令文本。
    /// </summary>
    public string GeneratedCommandText
    {
        get => _generatedCommandText;
        private set => SetField(ref _generatedCommandText, value);
    }
    #endregion

    #region 解析结果文本
    private string _parsedResultText = string.Empty;

    /// <summary>
    /// 解析结果文本。
    /// </summary>
    public string ParsedResultText
    {
        get => _parsedResultText;
        private set => SetField(ref _parsedResultText, value);
    }
    #endregion

    #endregion

    #region 命令属性
    public ICommand NewProfileCommand { get; private set; } = null!;

    public ICommand DuplicateProfileCommand { get; private set; } = null!;

    public ICommand DeleteProfileCommand { get; private set; } = null!;

    public ICommand SaveProfilesCommand { get; private set; } = null!;

    public ICommand NewCommandCommand { get; private set; } = null!;

    public ICommand DuplicateCommandCommand { get; private set; } = null!;

    public ICommand DeleteCommandCommand { get; private set; } = null!;

    public ICommand GenerateCommandCommand { get; private set; } = null!;

    public ICommand ParseResultCommand { get; private set; } = null!;

    public ICommand CloseCommandDrawerCommand { get; private set; } = null!;

    #endregion

    #region 属性联动方法
    private void SelectedProfile_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProtocolConfigProfile.Name) or nameof(ProtocolConfigProfile.Summary))
        {
            ProfilesView.Refresh();
        }

        if (e.PropertyName == nameof(ProtocolConfigProfile.SelectedCommand))
        {
            ClearGeneratedOutputs();

            if (SelectedProfile?.SelectedCommand is null)
            {
                CloseCommandDrawer();
            }
            else
            {
                OpenCommandDrawer();
            }
        }

        if (ShouldDebouncePreviewUpdate(e.PropertyName))
        {
            SchedulePreviewUpdate();
        }
        else
        {
            _previewUpdateDebounceTimer.Stop();
            UpdatePreviews();
        }

        RaiseCommandStatesChanged();
    }

    private void SchedulePreviewUpdate()
    {
        _previewUpdateDebounceTimer.Stop();
        _previewUpdateDebounceTimer.Start();
    }

    private static bool ShouldDebouncePreviewUpdate(string? propertyName)
    {
        return propertyName is nameof(ProtocolConfigProfile.ContentTemplate)
            or nameof(ProtocolConfigProfile.PlaceholderValuesText)
            or nameof(ProtocolConfigProfile.SampleResponseText)
            or nameof(ProtocolConfigProfile.ParseRulesText);
    }

    #endregion

    #region 初始化与生命周期方法
    /// <summary>
    /// 初始化协议页面的下拉选项。
    /// </summary>
    private void InitializeOptionCollections()
    {
        PayloadFormats.Add(new ProtocolOption<ProtocolPayloadFormat>(ProtocolPayloadFormat.Hex, "Hex", "按十六进制字节内容构建报文。"));
        PayloadFormats.Add(new ProtocolOption<ProtocolPayloadFormat>(ProtocolPayloadFormat.Ascii, "ASCII", "按 ASCII 文本内容构建报文。"));

        ExecutionModes.Add(new ProtocolOption<ProtocolExecutionMode>(ProtocolExecutionMode.SendOnly, "发送不等待返回", "只发送指令，不解析返回数据。"));
        ExecutionModes.Add(new ProtocolOption<ProtocolExecutionMode>(ProtocolExecutionMode.SendAndWaitForResponse, "发送等待数据返回", "发送指令后等待设备返回数据，并执行解析。"));
        ExecutionModes.Add(new ProtocolOption<ProtocolExecutionMode>(ProtocolExecutionMode.ParseOnly, "仅解析", "不发送指令，直接接收一帧数据并执行解析。"));

        CrcModes.Add(new ProtocolOption<ProtocolCrcMode>(ProtocolCrcMode.None, "无校验", "不自动追加 CRC。"));
        CrcModes.Add(new ProtocolOption<ProtocolCrcMode>(ProtocolCrcMode.ModbusCrc16, "Modbus CRC16", "低字节在前，高字节在后。"));
        CrcModes.Add(new ProtocolOption<ProtocolCrcMode>(ProtocolCrcMode.Crc16Ibm, "CRC16-IBM", "IBM 反射模式，低字节在前。"));
        CrcModes.Add(new ProtocolOption<ProtocolCrcMode>(ProtocolCrcMode.Crc16CcittFalse, "CRC16-CCITT-FALSE", "高字节在前，常用于工业协议。"));
        CrcModes.Add(new ProtocolOption<ProtocolCrcMode>(ProtocolCrcMode.Crc32, "CRC32", "四字节 CRC32，小端追加。"));
    }

    /// <summary>
    /// 初始化页面全部按钮命令，避免在 View.xaml.cs 中保留业务点击逻辑。
    /// </summary>
    private void InitializeCommands()
    {
        NewProfileCommand = new RelayCommand(_ => NewProfile());
        DuplicateProfileCommand = new RelayCommand(_ => DuplicateProfile(), _ => SelectedProfile is not null);
        DeleteProfileCommand = new RelayCommand(_ => DeleteProfile(), _ => SelectedProfile is not null);
        SaveProfilesCommand = new RelayCommand(_ => SaveProfiles());
        NewCommandCommand = new RelayCommand(_ => NewCommand(), _ => SelectedProfile is not null);
        DuplicateCommandCommand = new RelayCommand(_ => DuplicateCommand(), _ => SelectedProfile?.SelectedCommand is not null);
        DeleteCommandCommand = new RelayCommand(_ => DeleteCommand(), _ => SelectedProfile?.SelectedCommand is not null);
        GenerateCommandCommand = new RelayCommand(_ => GenerateCommand(), _ => SelectedProfile?.SelectedCommand is not null);
        ParseResultCommand = new RelayCommand(_ => ParseResult(), _ => SelectedProfile?.SelectedCommand is not null);
        CloseCommandDrawerCommand = new RelayCommand(_ => CloseCommandDrawer(), _ => IsCommandDrawerOpen);
    }

    /// <summary>
    /// 视图加载时恢复对当前选中协议的事件订阅。
    /// </summary>
    public void OnViewLoaded()
    {
        if (_selectedProfile is not null)
        {
            _selectedProfile.PropertyChanged -= SelectedProfile_PropertyChanged;
            _selectedProfile.PropertyChanged += SelectedProfile_PropertyChanged;
        }

        UpdatePreviews();
        RaiseCommandStatesChanged();
    }

    /// <summary>
    /// 视图卸载时取消对当前选中协议的事件订阅。
    /// </summary>
    public void OnViewUnloaded()
    {
        if (_selectedProfile is not null)
        {
            _selectedProfile.PropertyChanged -= SelectedProfile_PropertyChanged;
        }
    }

    /// <summary>
    /// 打开当前选中指令的抽屉编辑区。
    /// </summary>
    public void OpenCommandDrawer()
    {
        if (SelectedProfile?.SelectedCommand is null)
        {
            return;
        }

        IsCommandDrawerOpen = true;
    }

    /// <summary>
    /// 关闭当前指令抽屉编辑区。
    /// </summary>
    public void CloseCommandDrawer()
    {
        IsCommandDrawerOpen = false;
    }

    #endregion

    #region 配置命令方法
    /// <summary>
    /// 新建一个通用协议模板配置。
    /// </summary>
    private void NewProfile()
    {
        ProtocolConfigProfile profile = CreateGenericProfile(GenerateUniqueName("协议"));
        AddProfile(profile);
        SelectedProfile = profile;
        SetPageStatus($"已新建协议配置：{profile.Name}。", SuccessBrush);
    }

    /// <summary>
    /// 复制当前协议配置及其全部指令。
    /// </summary>
    private void DuplicateProfile()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        ProtocolConfigProfile profile = SelectedProfile.Clone(GenerateCopyName(SelectedProfile.Name));
        AddProfile(profile);
        SelectedProfile = profile;
        SetPageStatus($"已复制协议配置：{profile.Name}。", SuccessBrush);
    }

    /// <summary>
    /// 删除当前协议配置，并同步删除本地存储文件。
    /// </summary>
    private void DeleteProfile()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        int currentIndex = Profiles.IndexOf(SelectedProfile);
        ProtocolConfigProfile deletedProfile = SelectedProfile;
        Profiles.Remove(deletedProfile);
        _protocolStore.DeleteStoredProfileFile(deletedProfile);

        if (Profiles.Count == 0)
        {
            ProtocolConfigProfile profile = CreateGenericProfile(GenerateUniqueName("协议"));
            AddProfile(profile);
        }

        SelectedProfile = Profiles[Math.Clamp(currentIndex, 0, Profiles.Count - 1)];
        SetPageStatus($"已删除协议配置：{deletedProfile.Name}。", NeutralBrush);
    }

    /// <summary>
    /// 新建当前协议下的一条指令，并自动打开抽屉编辑。
    /// </summary>
    private void NewCommand()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        ProtocolCommandConfig command = new()
        {
            Name = GenerateUniqueCommandName(SelectedProfile, "指令")
        };
        SelectedProfile.AddCommand(command);
        SelectedProfile.SelectedCommand = command;
        ClearGeneratedOutputs();
        OpenCommandDrawer();
        SetPageStatus($"已新建指令：{command.Name}。", SuccessBrush);
    }

    /// <summary>
    /// 复制当前选中的指令，并自动打开抽屉编辑。
    /// </summary>
    private void DuplicateCommand()
    {
        ProtocolConfigProfile? profile = SelectedProfile;
        ProtocolCommandConfig? selectedCommand = profile?.SelectedCommand;
        if (profile is null || selectedCommand is null)
        {
            return;
        }

        ProtocolCommandConfig command = selectedCommand.Clone(GenerateUniqueCommandName(profile, $"{selectedCommand.Name} 副本"));
        profile.AddCommand(command);
        profile.SelectedCommand = command;
        ClearGeneratedOutputs();
        OpenCommandDrawer();
        SetPageStatus($"已复制指令：{command.Name}。", SuccessBrush);
    }

    /// <summary>
    /// 删除当前选中的指令，保证每个协议至少保留一条指令。
    /// </summary>
    private void DeleteCommand()
    {
        ProtocolConfigProfile? profile = SelectedProfile;
        ProtocolCommandConfig? selectedCommand = profile?.SelectedCommand;
        if (profile is null || selectedCommand is null)
        {
            return;
        }

        ClearGeneratedOutputs();
        profile.RemoveCommand(selectedCommand);
        if (profile.Commands.Count == 0)
        {
            ProtocolCommandConfig command = new()
            {
                Name = GenerateUniqueCommandName(profile, "指令")
            };
            profile.AddCommand(command);
        }

        SetPageStatus($"已删除指令：{selectedCommand.Name}。", NeutralBrush);
    }

    /// <summary>
    /// 根据当前模板、占位符和 CRC 规则生成最终发送指令。
    /// </summary>
    private void GenerateCommand()
    {
        ProtocolCommandConfig? selectedCommand = SelectedProfile?.SelectedCommand;
        if (selectedCommand is null)
        {
            GeneratedCommandText = string.Empty;
            SetPageStatus("请先选择设备指令后再生成实际指令。", WarningBrush);
            return;
        }

        if (ProtocolPreviewEngine.TryBuildRequestPreview(selectedCommand, out ProtocolRequestPreviewResult? previewResult, out string message) &&
            previewResult is not null)
        {
            GeneratedCommandText = BuildGeneratedCommandText(selectedCommand, previewResult);
            SetPageStatus(message, SuccessBrush);
            return;
        }

        GeneratedCommandText = string.Empty;
        SetPageStatus(message, WarningBrush);
    }

    /// <summary>
    /// 根据当前返回示例和解析规则执行结果解析预览。
    /// </summary>
    private void ParseResult()
    {
        ProtocolCommandConfig? selectedCommand = SelectedProfile?.SelectedCommand;
        if (selectedCommand is null)
        {
            ParsedResultText = string.Empty;
            SetPageStatus("请先选择设备指令后再解析返回数据。", WarningBrush);
            return;
        }

        if (!selectedCommand.WaitForResponse && !selectedCommand.IsParseOnly)
        {
            ParsedResultText = string.Empty;
            SetPageStatus("当前指令未启用等待数据返回。", WarningBrush);
            return;
        }

        if (ProtocolPreviewEngine.TryBuildResponsePreview(selectedCommand, out ProtocolResponsePreviewResult? previewResult, out string message) &&
            previewResult is not null)
        {
            ParsedResultText = previewResult.ParsedJson;
            SetPageStatus(message, SuccessBrush);
            return;
        }

        ParsedResultText = string.Empty;
        SetPageStatus(message, WarningBrush);
    }

    /// <summary>
    /// 保存当前全部协议配置到本地目录。
    /// </summary>
    private void SaveProfiles()
    {
        try
        {
            int savedCount = _protocolStore.Save(Profiles);
            SetPageStatus($"已保存 {savedCount} 个协议配置到 {_protocolStore.ConfigDirectory}。", SuccessBrush);
        }
        catch (Exception ex)
        {
            SetPageStatus($"保存协议配置失败：{ex.Message}", WarningBrush);
        }
    }

    #endregion

    #region 预览与状态辅助方法
    private void ClearGeneratedOutputs()
    {
        GeneratedCommandText = string.Empty;
        ParsedResultText = string.Empty;
    }

    private static string BuildGeneratedCommandText(ProtocolCommandConfig command, ProtocolRequestPreviewResult previewResult)
    {
        return command.RequestFormat == ProtocolPayloadFormat.Hex
            ? previewResult.RequestHex
            : previewResult.RequestAscii;
    }

    /// <summary>
    /// 根据当前选中协议刷新发送预览和返回解析预览。
    /// </summary>
    private void UpdatePreviews()
    {
        ProtocolConfigProfile? profile = SelectedProfile;
        if (profile is null)
        {
            RequestPreviewText = "请先选择或创建一个协议配置。";
            ResponsePreviewText = "请先选择或创建一个协议配置。";
            SetRequestPreviewStatus("未选择配置", NeutralBrush);
            SetResponsePreviewStatus("未选择配置", NeutralBrush);
            return;
        }

        if (ProtocolPreviewEngine.TryBuildRequestPreview(profile, out ProtocolRequestPreviewResult? requestResult, out string requestMessage) &&
            requestResult is not null)
        {
            RequestPreviewText = BuildRequestPreviewText(requestResult);
            SetRequestPreviewStatus(requestMessage, SuccessBrush);
        }
        else
        {
            RequestPreviewText = $"当前配置无法生成发送帧预览。{Environment.NewLine}{Environment.NewLine}{requestMessage}";
            SetRequestPreviewStatus(requestMessage, WarningBrush);
        }

        if (ProtocolPreviewEngine.TryBuildResponsePreview(profile, out ProtocolResponsePreviewResult? responseResult, out string responseMessage) &&
            responseResult is not null)
        {
            ResponsePreviewText = BuildResponsePreviewText(responseResult);
            Brush responseBrush = string.IsNullOrWhiteSpace(profile.SampleResponseText) ||
                                 string.IsNullOrWhiteSpace(profile.ParseRulesText)
                ? NeutralBrush
                : SuccessBrush;
            SetResponsePreviewStatus(responseMessage, responseBrush);
        }
        else
        {
            ResponsePreviewText = $"当前配置无法生成返回解析预览。{Environment.NewLine}{Environment.NewLine}{responseMessage}";
            SetResponsePreviewStatus(responseMessage, WarningBrush);
        }
    }

    private static string BuildRequestPreviewText(ProtocolRequestPreviewResult previewResult)
    {
        return string.Join(
            Environment.NewLine + Environment.NewLine,
            "渲染模板",
            previewResult.RenderedTemplate,
            "发送 Hex",
            previewResult.RequestHex,
            "发送 ASCII",
            previewResult.RequestAscii);
    }

    private static string BuildResponsePreviewText(ProtocolResponsePreviewResult previewResult)
    {
        return string.Join(
            Environment.NewLine + Environment.NewLine,
            "返回 Hex",
            previewResult.ResponseHex,
            "返回 ASCII",
            previewResult.ResponseAscii,
            "解析结果",
            previewResult.ParsedJson);
    }

    private bool FilterProfiles(object item)
    {
        if (item is not ProtocolConfigProfile profile)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        string keyword = SearchText.Trim();
        return Contains(profile.Name, keyword) ||
               Contains(profile.Summary, keyword) ||
               profile.Commands.Any(command => Contains(command.Name, keyword));
    }

    private static bool Contains(string? source, string keyword)
    {
        return source?.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void SetPageStatus(string text, Brush brush)
    {
        PageStatusText = text;
        PageStatusBrush = brush;
    }

    private void SetRequestPreviewStatus(string text, Brush brush)
    {
        RequestPreviewStatusText = text;
        RequestPreviewStatusBrush = brush;
    }

    private void SetResponsePreviewStatus(string text, Brush brush)
    {
        ResponsePreviewStatusText = text;
        ResponsePreviewStatusBrush = brush;
    }

    #endregion

    #region 配置加载与保存方法
    /// <summary>
    /// 在首次使用时生成默认示例协议配置。
    /// </summary>
    private void SeedProfiles()
    {
        AddProfile(CreateModbusDemoProfile("Modbus 读寄存器"));
        AddProfile(CreateAsciiDemoProfile("ASCII 文本协议"));
    }

    private static ProtocolConfigProfile CreateGenericProfile(string name)
    {
        return new ProtocolConfigProfile
        {
            Name = name,
            RequestFormat = ProtocolPayloadFormat.Hex,
            ResponseFormat = ProtocolPayloadFormat.Hex,
            ReplyAggregationMilliseconds = "200",
            CrcMode = ProtocolCrcMode.None,
            ContentTemplate = "AA {{Address}} {{Command}}",
            PlaceholderValuesText = "Address=01\r\nCommand=03",
            SampleResponseText = "AA 01 03",
            ParseRulesText = "return data;"
        };
    }

    private static ProtocolConfigProfile CreateModbusDemoProfile(string name)
    {
        ProtocolConfigProfile profile = new()
        {
            Name = name,
            CommandName = "读保持寄存器",
            RequestFormat = ProtocolPayloadFormat.Hex,
            ResponseFormat = ProtocolPayloadFormat.Hex,
            ReplyAggregationMilliseconds = "200",
            CrcMode = ProtocolCrcMode.ModbusCrc16,
            ContentTemplate = "{{Station}} {{Function}} {{AddressHi}} {{AddressLo}} {{CountHi}} {{CountLo}}",
            PlaceholderValuesText = "Station=01\r\nFunction=03\r\nAddressHi=00\r\nAddressLo=00\r\nCountHi=00\r\nCountLo=02",
            SampleResponseText = "01 03 04 00 0A 00 14",
            ParseRulesText = "return {\r\n    Station = string.sub(data, 1, 2),\r\n    Function = string.sub(data, 3, 4),\r\n    ByteCount = string.sub(data, 5, 6),\r\n    DataHex = string.sub(data, 7)\r\n}"
        };

        profile.AddCommand(new ProtocolCommandConfig
        {
            Name = "写单个寄存器",
            RequestFormat = ProtocolPayloadFormat.Hex,
            ResponseFormat = ProtocolPayloadFormat.Hex,
            ReplyAggregationMilliseconds = "200",
            CrcMode = ProtocolCrcMode.ModbusCrc16,
            ContentTemplate = "{{Station}} 06 {{AddressHi}} {{AddressLo}} {{ValueHi}} {{ValueLo}}",
            PlaceholderValuesText = "Station=01\r\nAddressHi=00\r\nAddressLo=01\r\nValueHi=00\r\nValueLo=0A",
            SampleResponseText = "01 06 00 01 00 0A",
            ParseRulesText = "return {\r\n    Station = string.sub(data, 1, 2),\r\n    Function = string.sub(data, 3, 4),\r\n    Address = string.sub(data, 5, 8),\r\n    Value = string.sub(data, 9, 12)\r\n}"
        });

        profile.SelectedCommand = null;
        return profile;
    }

    private static ProtocolConfigProfile CreateAsciiDemoProfile(string name)
    {
        ProtocolConfigProfile profile = new()
        {
            Name = name,
            CommandName = "读取通道",
            RequestFormat = ProtocolPayloadFormat.Ascii,
            ResponseFormat = ProtocolPayloadFormat.Ascii,
            ReplyAggregationMilliseconds = "300",
            CrcMode = ProtocolCrcMode.None,
            ContentTemplate = "READ {{Channel}}",
            PlaceholderValuesText = "Channel=T1",
            SampleResponseText = "OK,T1,25.6",
            ParseRulesText = "local parts = {}\r\nfor value in string.gmatch(data, \"([^,]+)\") do\r\n    parts[#parts + 1] = value\r\nend\r\nreturn {\r\n    Status = parts[1],\r\n    Channel = parts[2],\r\n    Value = parts[3]\r\n}"
        };

        profile.SelectedCommand = null;
        return profile;
    }

    private void AddProfile(ProtocolConfigProfile profile)
    {
        Profiles.Add(profile);
    }

    private string GenerateUniqueName(string prefix)
    {
        for (int index = 1; ; index++)
        {
            string name = $"{prefix} {index}";
            if (!Profiles.Any(profile => string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return name;
            }
        }
    }

    private static string GenerateUniqueCommandName(ProtocolConfigProfile profile, string prefix)
    {
        string baseName = string.IsNullOrWhiteSpace(prefix) ? "指令" : prefix.Trim();
        if (!profile.Commands.Any(command => string.Equals(command.Name, baseName, StringComparison.OrdinalIgnoreCase)))
        {
            return baseName;
        }

        for (int index = 2; ; index++)
        {
            string name = $"{baseName} {index}";
            if (!profile.Commands.Any(command => string.Equals(command.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return name;
            }
        }
    }

    private string GenerateCopyName(string baseName)
    {
        string prefix = string.IsNullOrWhiteSpace(baseName) ? "协议" : baseName.Trim();
        string firstName = $"{prefix} 副本";
        if (!Profiles.Any(profile => string.Equals(profile.Name, firstName, StringComparison.OrdinalIgnoreCase)))
        {
            return firstName;
        }

        for (int index = 2; ; index++)
        {
            string name = $"{prefix} 副本 {index}";
            if (!Profiles.Any(profile => string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return name;
            }
        }
    }

    #endregion

    #region 命令状态方法
    private void RaiseCommandStatesChanged()
    {
        RaiseCommandState(NewProfileCommand);
        RaiseCommandState(DuplicateProfileCommand);
        RaiseCommandState(DeleteProfileCommand);
        RaiseCommandState(SaveProfilesCommand);
        RaiseCommandState(NewCommandCommand);
        RaiseCommandState(DuplicateCommandCommand);
        RaiseCommandState(DeleteCommandCommand);
        RaiseCommandState(GenerateCommandCommand);
        RaiseCommandState(ParseResultCommand);
        RaiseCommandState(CloseCommandDrawerCommand);
    }

    private static void RaiseCommandState(ICommand? command)
    {
        if (command is RelayCommand relayCommand)
        {
            relayCommand.RaiseCanExecuteChanged();
        }
    }

    #endregion
}
