using ControlLibrary;
using Module.Communication.Configuration;
using System.Linq;
using System.Windows.Data;
using Module.Communication.Features.DeviceCommunicationConfig.Models;
using Module.Communication.Features.DeviceCommunicationConfig.Services;
using Module.Communication.Features.DeviceCommunicationConfig.ViewModels.PresentationModels;
using Module.Communication.Features.ProtocolConfig.Models;
using Module.Communication.Features.ProtocolConfig.Services;
using Shared.Abstractions.Enum;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Shared.Infrastructure.Communication;
using Shared.Infrastructure.Extensions;
using Shared.Infrastructure.PackMethod;
using Shared.Models.Communication;
using Shared.Models.Log;
using System.IO.Ports;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Shared.Abstractions.ICommunication;

namespace Module.Communication.Features.DeviceCommunicationConfig.ViewModels;

public sealed class DeviceCommunicationConfigViewModel : ViewModelProperties
{
public DeviceCommunicationConfigViewModel()
    {
        InitializeSelectionOptions();
        InitializeCommands();

        ProfilesView = CollectionViewSource.GetDefaultView(Profiles);
        ProfilesView.Filter = FilterProfiles;
        AvailableProtocolsView = CollectionViewSource.GetDefaultView(AvailableProtocols);
        AvailableProtocolsView.Filter = FilterAvailableProtocols;
        SupportedProtocolCommandsView = CollectionViewSource.GetDefaultView(SupportedProtocolCommands);
        SupportedProtocolCommandsView.Filter = FilterSupportedProtocolCommands;

        int loadedProfileCount = _communicationStore.Load(IsSupportedCommunicationType, AddProfile, AppendReceiveLine);
        if (loadedProfileCount == 0)
        {
            SeedProfiles();
        }

        RefreshAvailableProtocols();
        SelectedProfile = Profiles.FirstOrDefault();

        AppendReceiveLine(
            loadedProfileCount > 0
                ? $"Loaded {loadedProfileCount} communication profile(s) from {_communicationStore.ConfigDirectory}."
                : $"No local communication profiles were found. A default profile was created and will be saved to {_communicationStore.ConfigDirectory}.");
    }


    #region 常量与静态资源

    /// <summary>
    /// 接收日志允许保留的最大字符数，避免界面长期运行后文本过大。
    /// </summary>
    private const int MaxReceiveTextLength = 100_000;

    /// <summary>
    /// 通信配置文件本地存储目录。
    /// </summary>
    private static readonly string CommunicationConfigDirectory =
        Path.Combine(AppContext.BaseDirectory, "Config", "Communication");

    /// <summary>
    /// 协议配置文件本地存储目录。
    /// </summary>
    private static readonly string ProtocolConfigDirectory =
        Path.Combine(AppContext.BaseDirectory, "Config", "Protocol");

    /// <summary>
    /// 成功状态使用的高亮颜色。
    /// </summary>
    private static readonly Brush SuccessBrush =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A"));

    /// <summary>
    /// 警告或失败状态使用的高亮颜色。
    /// </summary>
    private static readonly Brush WarningBrush =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EA580C"));

    /// <summary>
    /// 中性状态使用的高亮颜色。
    /// </summary>
    private static readonly Brush NeutralBrush =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));

    public bool IsProtocolConfigurationEditable => !_isConnectionEstablished;

    public string ProtocolConfigurationEditHint =>
        IsProtocolConfigurationEditable
            ? "Protocols can be edited."
            : "Close the active test connection before editing protocols.";

    #endregion

    #region 私有字段

    /// <summary>
    /// 记录通信配置与其落盘文件名的映射，便于保存和删除时同步处理旧文件。
    /// </summary>
    private readonly DeviceCommunicationStore _communicationStore = new(CommunicationConfigDirectory);

    /// <summary>
    /// 当前选中的通信配置。
    /// </summary>
    private DeviceCommunicationProfile? _selectedProfile;

    /// <summary>
    /// 当前已经创建并启动的通信对象。
    /// </summary>
    private CommunicationBase? _activeCommunication;

    /// <summary>
    /// 当前通信对象的客户端来源，供 TCP 服务端刷新客户端列表。
    /// </summary>
    private ICommunicationClientSource? _activeClientSource;

    /// <summary>
    /// 当前活动通信对象对应的配置名称。
    /// </summary>
    private string? _activeProfileName;

    /// <summary>
    /// 当前活动通信对象的通信类型。
    /// </summary>
    private CommuniactionType? _activeCommunicationType;

    /// <summary>
    /// TCP 服务端模式下当前选中的客户端。
    /// </summary>
    private ConnectedClientOption? _selectedServerClient;

    /// <summary>
    /// 顶部通信状态文案。
    /// </summary>
    private string _connectionStatusText = "未连接";

    /// <summary>
    /// 顶部通信状态文案对应的画刷。
    /// </summary>
    private Brush _connectionStatusBrush = NeutralBrush;

    /// <summary>
    /// 报文发送文本框当前内容。
    /// </summary>
    private string _sendText = string.Empty;

    /// <summary>
    /// 接收区和日志区的完整文本内容。
    /// </summary>
    private string _receiveText = string.Empty;

    /// <summary>
    /// PLC 读写测试默认地址。
    /// </summary>
    private string _plcAddress = "D100";

    /// <summary>
    /// PLC 读写测试默认长度。
    /// </summary>
    private string _plcLength = "1";

    /// <summary>
    /// PLC 写入测试默认值。
    /// </summary>
    private string _plcWriteValue = "0";

    /// <summary>
    /// PLC 读写测试当前选择的数据类型。
    /// </summary>
    private string _selectedPlcDataType = DataType.Decimal.ToString();

    /// <summary>
    /// 左侧配置列表搜索关键字。
    /// </summary>
    private string _searchText = string.Empty;
    private string _availableProtocolSearchText = string.Empty;
    private string _supportedProtocolCommandSearchText = string.Empty;
    private string _selectedCommunicationFamily = CommunicationFamily.Standard.ToString();
    private bool _isSyncingCommunicationFamily;
    private string _selectedCommunicationTypeId = DeviceCommunicationConfigRegistry.Default.DefaultTypeId;
    private bool _isSyncingCommunicationType;

    /// <summary>
    /// 协议列表抽屉是否处于打开状态。
    /// </summary>
    private bool _isProtocolLibraryOpen;

    /// <summary>
    /// 指令列表抽屉是否处于打开状态。
    /// </summary>
    private bool _isProtocolCommandLibraryOpen;
    private bool _isConnectionEstablished;
    private readonly List<BoundParseOnlyCommand> _activeParseOnlyCommands = new();

    #endregion

    #region 集合属性

    /// <summary>
    /// 当前页面维护的全部通信配置集合。
    /// </summary>
    public ObservableCollection<DeviceCommunicationProfile> Profiles { get; } = new();

    /// <summary>
    /// 通信配置列表视图，支持搜索过滤。
    /// </summary>
    public ICollectionView ProfilesView { get; private set; } = null!;

    public ICollectionView AvailableProtocolsView { get; private set; } = null!;

    public ICollectionView SupportedProtocolCommandsView { get; private set; } = null!;

    /// <summary>
    /// 通信类型下拉选项集合。
    /// </summary>
    public ObservableCollection<SelectionOption> CommunicationFamilies { get; } = new();

    public ObservableCollection<CommunicationTypeOption> CommunicationTypes { get; } = new();

    /// <summary>
    /// 当前通信类型的连接参数字段集合。
    /// </summary>
    public ObservableCollection<DeviceCommunicationConfigFieldViewModel> CurrentFields { get; } = new();

    /// <summary>
    /// 串口名称候选集合。
    /// </summary>
    public ObservableCollection<string> PortNameOptions { get; } = new();

    /// <summary>
    /// PLC 数据类型候选集合。
    /// </summary>
    public ObservableCollection<SelectionOption> PlcDataTypeOptions { get; } = new();

    /// <summary>
    /// TCP 服务端当前已连接客户端集合。
    /// </summary>
    public ObservableCollection<ConnectedClientOption> ConnectedServerClients { get; } = new();

    /// <summary>
    /// 本地协议库中可供关联的协议集合。
    /// </summary>
    public ObservableCollection<AvailableProtocolOption> AvailableProtocols { get; } = new();

    /// <summary>
    /// 当前选中通信配置所支持协议中的全部指令集合。
    /// </summary>
    public ObservableCollection<SupportedProtocolCommandOption> SupportedProtocolCommands { get; } = new();

    #endregion

    #region 页面状态属性

    /// <summary>
    /// 当前选中的通信配置。
    /// </summary>
    public DeviceCommunicationProfile? SelectedProfile
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

            if (_selectedProfile is not null)
            {
                _selectedProfile.PropertyChanged += SelectedProfile_PropertyChanged;
            }

            SyncCommunicationFamilyFromSelectedProfile();
            SyncCommunicationTypeFromSelectedProfile();
            RefreshCurrentFields();
            RefreshSupportedProtocolCommands();
            CloseProtocolCommandLibrary();
            OnPropertyChanged();
            RaiseCommunicationVisibilityChanged();
            RaiseCommandStatesChanged();
        }
    }

    /// <summary>
    /// 当前选中的 TCP 服务端客户端。
    /// </summary>
    public ConnectedClientOption? SelectedServerClient
    {
        get => _selectedServerClient;
        set => SetField(ref _selectedServerClient, value);
    }

    /// <summary>
    /// 配置列表搜索关键字。
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

    public string AvailableProtocolSearchText
    {
        get => _availableProtocolSearchText;
        set
        {
            if (!SetField(ref _availableProtocolSearchText, value ?? string.Empty))
            {
                return;
            }

            AvailableProtocolsView?.Refresh();
        }
    }

    public string SupportedProtocolCommandSearchText
    {
        get => _supportedProtocolCommandSearchText;
        set
        {
            if (!SetField(ref _supportedProtocolCommandSearchText, value ?? string.Empty))
            {
                return;
            }

            SupportedProtocolCommandsView?.Refresh();
        }
    }

    public string SelectedCommunicationFamily
    {
        get => _selectedCommunicationFamily;
        set
        {
            string normalizedFamily = NormalizeCommunicationFamily(value).ToString();
            if (!SetField(ref _selectedCommunicationFamily, normalizedFamily))
            {
                return;
            }

            RefreshCommunicationTypesForSelectedFamily();
            if (!_isSyncingCommunicationFamily)
            {
                SelectDefaultTypeForCurrentFamily();
            }
        }
    }

    public string SelectedCommunicationTypeId
    {
        get => _selectedCommunicationTypeId;
        set
        {
            if (string.IsNullOrWhiteSpace(value) ||
                !DeviceCommunicationConfigRegistry.Default.Contains(value))
            {
                return;
            }

            string normalizedTypeId = DeviceCommunicationConfigRegistry.Default.GetOrDefault(value).TypeId;
            if (!SetField(ref _selectedCommunicationTypeId, normalizedTypeId))
            {
                return;
            }

            if (_isSyncingCommunicationType || SelectedProfile is null)
            {
                return;
            }

            if (!string.Equals(SelectedProfile.TypeId, normalizedTypeId, StringComparison.OrdinalIgnoreCase))
            {
                SelectedProfile.TypeId = normalizedTypeId;
            }

            RefreshSelectedProfileTypeState();
        }
    }

    /// <summary>
    /// 当前待发送报文。
    /// </summary>
    public string SendText
    {
        get => _sendText;
        set => SetField(ref _sendText, value ?? string.Empty);
    }

    /// <summary>
    /// 当前接收日志文本。
    /// </summary>
    public string ReceiveText
    {
        get => _receiveText;
        private set => SetField(ref _receiveText, value ?? string.Empty);
    }

    /// <summary>
    /// PLC 测试地址。
    /// </summary>
    public string PlcAddress
    {
        get => _plcAddress;
        set => SetField(ref _plcAddress, value ?? string.Empty);
    }

    /// <summary>
    /// PLC 测试长度。
    /// </summary>
    public string PlcLength
    {
        get => _plcLength;
        set => SetField(ref _plcLength, value ?? string.Empty);
    }

    /// <summary>
    /// PLC 写入值。
    /// </summary>
    public string PlcWriteValue
    {
        get => _plcWriteValue;
        set => SetField(ref _plcWriteValue, value ?? string.Empty);
    }

    /// <summary>
    /// PLC 测试数据类型。
    /// </summary>
    public string SelectedPlcDataType
    {
        get => _selectedPlcDataType;
        set => SetField(ref _selectedPlcDataType, value ?? DataType.Decimal.ToString());
    }

    /// <summary>
    /// 协议列表抽屉是否打开。
    /// </summary>
    public bool IsProtocolLibraryOpen
    {
        get => _isProtocolLibraryOpen;
        private set
        {
            if (SetField(ref _isProtocolLibraryOpen, value))
            {
                RaiseCommandStatesChanged();
            }
        }
    }

    /// <summary>
    /// 指令列表抽屉是否打开。
    /// </summary>
    public bool IsProtocolCommandLibraryOpen
    {
        get => _isProtocolCommandLibraryOpen;
        private set
        {
            if (SetField(ref _isProtocolCommandLibraryOpen, value))
            {
                RaiseCommandStatesChanged();
            }
        }
    }

    /// <summary>
    /// 通信状态文案。
    /// </summary>
    public string ConnectionStatusText
    {
        get => _connectionStatusText;
        private set => SetField(ref _connectionStatusText, value);
    }

    /// <summary>
    /// 通信状态颜色。
    /// </summary>
    public Brush ConnectionStatusBrush
    {
        get => _connectionStatusBrush;
        private set => SetField(ref _connectionStatusBrush, value);
    }

    /// <summary>
    /// 是否显示 TCP 服务端客户端选择区域。
    /// </summary>
    public bool IsTcpServerClientSelectionVisible =>
        SelectedProfile?.IsTcpServerType == true ||
        _activeCommunicationType == CommuniactionType.TCPServer;

    /// <summary>
    /// 是否显示 PLC 读写测试区域。
    /// </summary>
    public bool IsPlcTestVisible => SelectedProfile?.IsPlcType == true;

    /// <summary>
    /// 是否显示通用报文发送区域。
    /// </summary>
    public bool IsGenericSendTestVisible => SelectedProfile?.SupportsGenericSendTest == true;

    public bool IsPortRefreshVisible => SelectedProfile?.IsSerialType == true;

    /// <summary>
    /// 已连接 TCP 服务端客户端状态文案。
    /// </summary>
    public string ConnectedServerClientStatusText =>
        ConnectedServerClients.Count == 0
            ? "暂无已连接客户端"
            : $"已连接 {ConnectedServerClients.Count} 个客户端";

    #endregion

    #region 命令属性

    /// <summary>
    /// 新建通信配置命令。
    /// </summary>
    public ICommand NewProfileCommand { get; private set; } = null!;

    /// <summary>
    /// 复制通信配置命令。
    /// </summary>
    public ICommand DuplicateProfileCommand { get; private set; } = null!;

    /// <summary>
    /// 删除通信配置命令。
    /// </summary>
    public ICommand DeleteProfileCommand { get; private set; } = null!;

    /// <summary>
    /// 保存通信配置命令。
    /// </summary>
    public ICommand SaveProfilesCommand { get; private set; } = null!;

    /// <summary>
    /// 打开协议列表抽屉命令。
    /// </summary>
    public ICommand AddSupportedProtocolCommand { get; private set; } = null!;

    /// <summary>
    /// 将协议库中的协议添加到当前通信配置命令。
    /// </summary>
    public ICommand AddAvailableProtocolCommand { get; private set; } = null!;

    /// <summary>
    /// 删除支持协议命令。
    /// </summary>
    public ICommand DeleteSupportedProtocolCommand { get; private set; } = null!;

    /// <summary>
    /// 加载本地协议文件命令。
    /// </summary>
    public ICommand LoadSupportedProtocolFileCommand { get; private set; } = null!;

    /// <summary>
    /// 打开指令列表抽屉命令。
    /// </summary>
    public ICommand OpenProtocolCommandLibraryCommand { get; private set; } = null!;

    /// <summary>
    /// 双击指令后填充报文命令。
    /// </summary>
    public ICommand FillSupportedProtocolCommandCommand { get; private set; } = null!;

    /// <summary>
    /// 关闭协议列表抽屉命令。
    /// </summary>
    public ICommand CloseProtocolLibraryCommand { get; private set; } = null!;

    /// <summary>
    /// 关闭指令列表抽屉命令。
    /// </summary>
    public ICommand CloseProtocolCommandLibraryCommand { get; private set; } = null!;

    /// <summary>
    /// 创建并测试通信连接命令。
    /// </summary>
    public ICommand TestConnectionCommand { get; private set; } = null!;

    /// <summary>
    /// 发送当前报文命令。
    /// </summary>
    public ICommand SendCommand { get; private set; } = null!;

    /// <summary>
    /// TCP 服务端群发命令。
    /// </summary>
    public ICommand SendAllCommand { get; private set; } = null!;

    /// <summary>
    /// PLC 读取命令。
    /// </summary>
    public ICommand ReadPlcCommand { get; private set; } = null!;

    /// <summary>
    /// PLC 写入命令。
    /// </summary>
    public ICommand WritePlcCommand { get; private set; } = null!;

    /// <summary>
    /// 关闭当前测试连接命令。
    /// </summary>
    public ICommand CloseConnectionCommand { get; private set; } = null!;

    /// <summary>
    /// 清空日志命令。
    /// </summary>
    public ICommand ClearReceiveCommand { get; private set; } = null!;

    /// <summary>
    /// 刷新串口列表命令。
    /// </summary>
    public ICommand RefreshPortsCommand { get; private set; } = null!;

    #endregion

    #region 属性变更处理

    /// <summary>
    /// 监听当前选中通信配置的属性变化，并刷新界面依赖状态。
    /// </summary>
    private void SelectedProfile_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DeviceCommunicationProfile.TypeId))
        {
            SyncCommunicationFamilyFromSelectedProfile();
            SyncCommunicationTypeFromSelectedProfile();
            RefreshSelectedProfileTypeState();
        }

        if (e.PropertyName is nameof(DeviceCommunicationProfile.LocalName) or
            nameof(DeviceCommunicationProfile.Summary) or
            nameof(DeviceCommunicationProfile.SupportedProtocolsSummary))
        {
            ProfilesView?.Refresh();
        }

        if (e.PropertyName is nameof(DeviceCommunicationProfile.SupportedProtocolsSummary))
        {
            RefreshSupportedProtocolCommands();
        }

        RaiseCommandStatesChanged();
    }

    /// <summary>
    /// 通知界面刷新不同通信模式下的可见性绑定。
    /// </summary>
    private void RaiseCommunicationVisibilityChanged()
    {
        OnPropertyChanged(nameof(IsTcpServerClientSelectionVisible));
        OnPropertyChanged(nameof(IsPlcTestVisible));
        OnPropertyChanged(nameof(IsGenericSendTestVisible));
        OnPropertyChanged(nameof(IsPortRefreshVisible));
    }

    private void RefreshSelectedProfileTypeState()
    {
        CloseActiveCommunicationForConfigurationChange();
        RefreshCurrentFields();
        ProfilesView?.Refresh();
        RaiseCommunicationVisibilityChanged();
        RaiseCommandStatesChanged();
    }

    private sealed record BoundParseOnlyCommand(
        string ProtocolName,
        string CommandName,
        ProtocolCommandConfig Command);

    #endregion

    #region 初始化

    /// <summary>
    /// 初始化页面下拉选项。
    /// </summary>
    private void InitializeSelectionOptions()
    {
        RefreshPortNameOptions(updateSelectedProfile: false);

        foreach (CommunicationFamily family in Enum.GetValues<CommunicationFamily>())
        {
            if (DeviceCommunicationConfigRegistry.Default.Descriptors.Any(descriptor => descriptor.Family == family))
            {
                CommunicationFamilies.Add(new SelectionOption(family.ToString(), GetCommunicationFamilyDisplayName(family)));
            }
        }

        RefreshCommunicationTypesForSelectedFamily();

        foreach (DataType type in Enum.GetValues<DataType>())
        {
            PlcDataTypeOptions.Add(new SelectionOption(type.ToString(), GetPlcDataTypeDisplayName(type)));
        }
    }

    private void RefreshCommunicationTypesForSelectedFamily()
    {
        CommunicationFamily selectedFamily = NormalizeCommunicationFamily(SelectedCommunicationFamily);
        CommunicationTypes.Clear();

        foreach (DeviceCommunicationConfigDescriptor descriptor in DeviceCommunicationConfigRegistry.Default.Descriptors
                     .Where(descriptor => descriptor.Family == selectedFamily))
        {
            CommunicationTypes.Add(new CommunicationTypeOption(
                descriptor.TypeId,
                descriptor.DisplayName,
                descriptor.Description));
        }
    }

    private void SelectDefaultTypeForCurrentFamily()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        if (CommunicationTypes.Any(option =>
                string.Equals(option.Value, SelectedProfile.TypeId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        CommunicationTypeOption? firstType = CommunicationTypes.FirstOrDefault();
        if (firstType is not null)
        {
            SelectedCommunicationTypeId = firstType.Value;
        }
    }

    private void SyncCommunicationFamilyFromSelectedProfile()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        DeviceCommunicationConfigDescriptor descriptor =
            DeviceCommunicationConfigRegistry.Default.GetOrDefault(SelectedProfile.TypeId);

        _isSyncingCommunicationFamily = true;
        try
        {
            SelectedCommunicationFamily = descriptor.Family.ToString();
        }
        finally
        {
            _isSyncingCommunicationFamily = false;
        }
    }

    private void SyncCommunicationTypeFromSelectedProfile()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        _isSyncingCommunicationType = true;
        try
        {
            SelectedCommunicationTypeId = SelectedProfile.TypeId;
        }
        finally
        {
            _isSyncingCommunicationType = false;
        }
    }

    private static CommunicationFamily NormalizeCommunicationFamily(string? value)
    {
        return Enum.TryParse(value, ignoreCase: true, out CommunicationFamily family)
            ? family
            : CommunicationFamily.Standard;
    }

    private static string GetCommunicationFamilyDisplayName(CommunicationFamily family)
    {
        return family switch
        {
            CommunicationFamily.Standard => "标准通信",
            CommunicationFamily.Plc => "PLC",
            CommunicationFamily.Can => "CAN",
            _ => family.ToString()
        };
    }

    /// <summary>
    /// 获取 PLC 数据类型的显示文本。
    /// </summary>
    private static string GetPlcDataTypeDisplayName(DataType type)
    {
        return type switch
        {
            DataType.Binary => "二进制",
            DataType.Octal => "八进制",
            DataType.Decimal => "十进制",
            DataType.Hexadecimal => "十六进制",
            DataType.Acsaii => "ASCII",
            DataType.String => "字符串",
            _ => type.ToString()
        };
    }

    /// <summary>
    /// 初始化页面命令。
    /// </summary>
    private void InitializeCommands()
    {
        NewProfileCommand = new RelayCommand(_ => NewProfile());
        DuplicateProfileCommand = new RelayCommand(_ => DuplicateProfile(), _ => SelectedProfile is not null);
        DeleteProfileCommand = new RelayCommand(_ => DeleteProfile(), _ => SelectedProfile is not null);
        SaveProfilesCommand = new RelayCommand(_ => SaveProfiles());
        AddSupportedProtocolCommand = new RelayCommand(_ => AddSupportedProtocol(), _ => SelectedProfile is not null);
        AddAvailableProtocolCommand = new RelayCommand(
            parameter => AddAvailableProtocol(parameter as AvailableProtocolOption),
            parameter => SelectedProfile is not null && parameter is AvailableProtocolOption);
        DeleteSupportedProtocolCommand = new RelayCommand(
            parameter => DeleteSupportedProtocol(parameter as DeviceSupportedProtocol),
            parameter => SelectedProfile is not null && parameter is DeviceSupportedProtocol);
        LoadSupportedProtocolFileCommand = new RelayCommand(
            parameter => LoadSupportedProtocolFile(parameter as DeviceSupportedProtocol),
            parameter => SelectedProfile is not null && parameter is DeviceSupportedProtocol);
        OpenProtocolCommandLibraryCommand = new RelayCommand(
            _ => OpenProtocolCommandLibrary(),
            _ => SelectedProfile is not null && SupportedProtocolCommands.Count > 0);
        FillSupportedProtocolCommandCommand = new RelayCommand(
            parameter => FillSupportedProtocolCommand(parameter as SupportedProtocolCommandOption),
            parameter => parameter is SupportedProtocolCommandOption);
        CloseProtocolLibraryCommand = new RelayCommand(_ => CloseProtocolLibrary(), _ => IsProtocolLibraryOpen);
        CloseProtocolCommandLibraryCommand = new RelayCommand(_ => CloseProtocolCommandLibrary(), _ => IsProtocolCommandLibraryOpen);
        TestConnectionCommand = new RelayCommand(_ => TestConnection(), _ => SelectedProfile is not null);
        SendCommand = new RelayCommand(async _ => await SendAsync());
        SendAllCommand = new RelayCommand(async _ => await SendAllAsync());
        ReadPlcCommand = new RelayCommand(async _ => await ReadPlcAsync());
        WritePlcCommand = new RelayCommand(async _ => await WritePlcAsync());
        CloseConnectionCommand = new RelayCommand(_ => CloseConnection(), _ => _activeCommunication is not null);
        ClearReceiveCommand = new RelayCommand(_ => ClearReceive());
        RefreshPortsCommand = new RelayCommand(_ => RefreshPorts());
    }

    #endregion

    #region 页面生命周期

    /// <summary>
    /// 页面卸载时释放事件绑定和通信资源。
    /// </summary>
    public void OnViewUnloaded()
    {
        if (_selectedProfile is not null)
        {
            _selectedProfile.PropertyChanged -= SelectedProfile_PropertyChanged;
        }

        CloseActiveCommunication(updateStatus: false);
    }

    #endregion

    #region 配置管理

    /// <summary>
    /// 新建通信配置。
    /// </summary>
    private void NewProfile()
    {
        string type = SelectedProfile?.TypeId ?? DeviceCommunicationConfigRegistry.Default.DefaultTypeId;
        DeviceCommunicationProfile profile = CreateProfile(type, GenerateUniqueName(type));
        AddProfile(profile);
        SelectedProfile = profile;
        AppendReceiveLine($"已创建设备通信配置：{profile.LocalName}。");
    }

    private void DuplicateProfile()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        DeviceCommunicationProfile profile = SelectedProfile.Clone(GenerateUniqueName(SelectedProfile.TypeId));
        AddProfile(profile);
        SelectedProfile = profile;
        AppendReceiveLine($"已复制设备通信配置：{profile.LocalName}。");
    }

    private void DeleteProfile()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        int currentIndex = Profiles.IndexOf(SelectedProfile);
        DeviceCommunicationProfile deletedProfile = SelectedProfile;
        Profiles.Remove(deletedProfile);
        _communicationStore.DeleteStoredProfileFile(deletedProfile);

        if (Profiles.Count == 0)
        {
            string defaultTypeId = DeviceCommunicationConfigRegistry.Default.DefaultTypeId;
            AddProfile(CreateProfile(defaultTypeId, GenerateUniqueName(defaultTypeId)));
        }

        SelectedProfile = Profiles[Math.Clamp(currentIndex, 0, Profiles.Count - 1)];
        AppendReceiveLine($"已删除设备通信配置：{deletedProfile.LocalName}。");
    }

    private void SaveProfiles()
    {
        try
        {
            int savedCount = _communicationStore.Save(Profiles);
            AppendReceiveLine($"已保存 {savedCount} 个通信配置到 {_communicationStore.ConfigDirectory}。");
        }
        catch (Exception ex)
        {
            AppendReceiveLine($"保存通信配置失败：{ex.Message}");
        }
    }

    #endregion

    #region 协议管理

    /// <summary>
    /// 打开协议列表抽屉。
    /// </summary>
    private void AddSupportedProtocol()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        if (!IsProtocolConfigurationEditable)
        {
            AppendReceiveLine("当前测试连接已建立，关闭连接后才能修改绑定协议。");
            return;
        }

        OpenProtocolLibrary();
        AppendReceiveLine("已打开协议列表。");
    }

    private void AddAvailableProtocol(AvailableProtocolOption? option)
    {
        TryApplyAvailableProtocol(option, null);
    }

    public bool TryApplyAvailableProtocol(AvailableProtocolOption? option, DeviceSupportedProtocol? targetProtocol)
    {
        if (SelectedProfile is null || option is null)
        {
            return false;
        }

        if (!IsProtocolConfigurationEditable)
        {
            AppendReceiveLine("当前测试连接已建立，关闭连接后才能修改绑定协议。");
            return false;
        }

        DeviceSupportedProtocol target = ResolveSupportedProtocolTarget(SelectedProfile, option.Name, option.FilePath, targetProtocol);
        target.ProtocolName = option.Name;
        target.ProtocolFilePath = option.FilePath;
        RefreshSupportedProtocolCommands();
        AppendReceiveLine($"已关联协议：{option.Name}。");
        return true;
    }

    private void DeleteSupportedProtocol(DeviceSupportedProtocol? protocol)
    {
        if (SelectedProfile is null || protocol is null)
        {
            return;
        }

        if (!IsProtocolConfigurationEditable)
        {
            AppendReceiveLine("当前测试连接已建立，关闭连接后才能修改绑定协议。");
            return;
        }

        SelectedProfile.SupportedProtocols.Remove(protocol);
        RefreshSupportedProtocolCommands();
        AppendReceiveLine(string.IsNullOrWhiteSpace(protocol.ProtocolName)
            ? "已删除空协议行。"
            : $"已删除支持协议：{protocol.ProtocolName}。");
    }

    private void LoadSupportedProtocolFile(DeviceSupportedProtocol? targetProtocol)
    {
        if (SelectedProfile is null || targetProtocol is null)
        {
            return;
        }

        if (!IsProtocolConfigurationEditable)
        {
            AppendReceiveLine("当前测试连接已建立，关闭连接后才能修改绑定协议。");
            return;
        }

        OpenFileDialog dialog = new()
        {
            Title = "选择协议文件",
            Filter = "协议文件 (*.json)|*.json|所有文件 (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (!TryReadProtocolProfileFromFile(dialog.FileName, out ProtocolConfigProfile? protocolProfile, out string message) ||
            protocolProfile is null)
        {
            MessageBox.Show(message, "加载协议失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            AppendReceiveLine($"加载协议文件失败：{message}");
            return;
        }

        if (!TrySaveProtocolProfileToLocalDirectory(protocolProfile, out string savedPath, out message))
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                AppendReceiveLine(message);
            }

            return;
        }

        DeviceSupportedProtocol target = ResolveSupportedProtocolTarget(SelectedProfile, protocolProfile.Name, savedPath, targetProtocol);
        target.ProtocolName = protocolProfile.Name;
        target.ProtocolFilePath = savedPath;
        RefreshAvailableProtocols();
        RefreshSupportedProtocolCommands();
        AppendReceiveLine($"已加载协议文件：{protocolProfile.Name} -> {savedPath}");
    }

    private void OpenProtocolLibrary()
    {
        CloseProtocolCommandLibrary();
        RefreshAvailableProtocols();
        IsProtocolLibraryOpen = true;
    }

    private void CloseProtocolLibrary()
    {
        IsProtocolLibraryOpen = false;
    }

    #endregion

    #region 指令填充

    /// <summary>
    /// 打开当前支持协议的指令列表抽屉。
    /// </summary>
    private void OpenProtocolCommandLibrary()
    {
        RefreshSupportedProtocolCommands();
        if (SupportedProtocolCommands.Count == 0)
        {
            AppendReceiveLine("未找到可填充的协议指令，请先为当前配置关联协议。");
            return;
        }

        CloseProtocolLibrary();
        IsProtocolCommandLibraryOpen = true;
    }

    /// <summary>
    /// 关闭指令列表抽屉。
    /// </summary>
    private void CloseProtocolCommandLibrary()
    {
        IsProtocolCommandLibraryOpen = false;
    }

    /// <summary>
    /// 将选中的协议指令填充到报文文本框。
    /// </summary>
    private bool FillSupportedProtocolCommand(SupportedProtocolCommandOption? option)
    {
        if (option is null)
        {
            return false;
        }

        if (!option.CanFill || string.IsNullOrWhiteSpace(option.FillMessage))
        {
            AppendReceiveLine($"指令 {option.DisplayName} 当前没有可发送报文。");
            return false;
        }

        SendText = option.FillMessage;
        CloseProtocolCommandLibrary();
        AppendReceiveLine($"已将指令 {option.DisplayName} 填充到报文文本框。");
        return true;
    }

    /// <summary>
    /// 刷新当前支持协议对应的全部指令列表。
    /// </summary>
    private void RefreshSupportedProtocolCommands()
    {
        List<SupportedProtocolCommandOption> commands = LoadSupportedProtocolCommands();

        SupportedProtocolCommands.Clear();
        foreach (SupportedProtocolCommandOption command in commands)
        {
            SupportedProtocolCommands.Add(command);
        }

        SupportedProtocolCommandsView?.Refresh();

        if (IsProtocolCommandLibraryOpen && SupportedProtocolCommands.Count == 0)
        {
            IsProtocolCommandLibraryOpen = false;
        }

        RaiseCommandStatesChanged();
    }

    /// <summary>
    /// 从当前支持协议中聚合可展示的全部指令。
    /// </summary>
    private List<SupportedProtocolCommandOption> LoadSupportedProtocolCommands()
    {
        List<SupportedProtocolCommandOption> commands = new();
        if (SelectedProfile is null)
        {
            return commands;
        }

        IEnumerable<DeviceSupportedProtocol> supportedProtocols = SelectedProfile.SupportedProtocols
            .Where(protocol =>
                !string.IsNullOrWhiteSpace(protocol.ProtocolName) &&
                !string.IsNullOrWhiteSpace(protocol.ProtocolFilePath))
            .GroupBy(
                protocol => $"{protocol.ProtocolName}|{protocol.ProtocolFilePath}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First());

        foreach (DeviceSupportedProtocol supportedProtocol in supportedProtocols)
        {
            if (!TryReadProtocolProfileFromFile(
                    supportedProtocol.ProtocolFilePath,
                    out ProtocolConfigProfile? profile,
                    out string message) ||
                profile is null)
            {
                commands.Add(new SupportedProtocolCommandOption(
                    supportedProtocol.ProtocolName,
                    supportedProtocol.ProtocolFilePath,
                    "协议读取失败",
                    message,
                    string.Empty,
                    string.Empty,
                    false));
                continue;
            }

            foreach (ProtocolCommandConfig command in profile.Commands.Where(item => !item.IsParseOnly))
            {
                commands.Add(BuildSupportedProtocolCommandOption(
                    profile.Name,
                    supportedProtocol.ProtocolFilePath,
                    command));
            }
        }

        return commands
            .OrderBy(command => command.ProtocolName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(command => command.CommandName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 构建指令列表项，并预生成可直接发送的报文。
    /// </summary>
    private static SupportedProtocolCommandOption BuildSupportedProtocolCommandOption(
        string protocolName,
        string protocolFilePath,
        ProtocolCommandConfig command)
    {
        bool canFill = false;
        string fillMessage = string.Empty;
        string previewMessage;
        string buildMessage = string.Empty;

        if (!command.IsParseOnly &&
            ProtocolPreviewEngine.TryBuildRequestPreview(command, out ProtocolRequestPreviewResult? preview, out buildMessage) &&
            preview is not null)
        {
            fillMessage = command.RequestFormat == ProtocolPayloadFormat.Hex
                ? $"0x{preview.RequestHex}"
                : preview.RequestAscii;
            previewMessage = fillMessage;
            canFill = !string.IsNullOrWhiteSpace(fillMessage);
        }
        else if (command.IsParseOnly)
        {
            previewMessage = "该指令为仅解析模式，没有发送报文。";
        }
        else
        {
            previewMessage = buildMessage;
        }

        return new SupportedProtocolCommandOption(
            protocolName,
            protocolFilePath,
            command.Name,
            command.Summary,
            previewMessage,
            fillMessage,
            canFill);
    }

    #endregion

    #region 通信测试

    private void TestConnection()
    {
        if (SelectedProfile is null)
        {
            SetConnectionStatus("未选择配置", WarningBrush);
            AppendReceiveLine("通信测试失败：请先选择通信配置。");
            return;
        }

        if (!SelectedProfile.TryBuildRuntimeConfig(out ICommunicationRuntimeConfig? config, out string message) || config is null)
        {
            SetConnectionStatus("配置无效", WarningBrush);
            AppendReceiveLine($"通信测试失败：{message}");
            return;
        }

        CloseActiveCommunication(updateStatus: false);

        try
        {
            CommunicationBase communication = CommunicationFactory.CreateCommunicationProtocol(config);
            _activeCommunication = communication;
            _activeProfileName = config.LocalName;
            _activeCommunicationType = config.Type;
            RefreshActiveParseOnlyCommands(SelectedProfile);

            RaiseCommunicationVisibilityChanged();
            RefreshConnectedServerClients(Array.Empty<CommunicationClientInfo>());
            AttachActiveCommunicationEvents(communication);

            SetConnectionStatus("连接中", NeutralBrush);
            AppendReceiveLine($"开始测试连接：{config.LocalName}（{config.Type}）。");

            bool started = communication.Start();
            SetConnectionEstablished(started && communication.IsConnected == ConnectState.Connected);
            SetConnectionStatus(
                started ? $"{config.LocalName} 已启动" : $"{config.LocalName} 启动失败",
                started ? SuccessBrush : WarningBrush);
            AppendReceiveLine(started
                ? "通信测试已启动。"
                : "通信测试启动失败，请检查配置或设备状态。");
        }
        catch (Exception ex)
        {
            CloseActiveCommunication(updateStatus: false);
            SetConnectionStatus("连接异常", WarningBrush);
            AppendReceiveLine($"通信测试异常：{ex.Message}");
        }

        RaiseCommandStatesChanged();
    }

    private async Task SendAsync()
    {
        try
        {
            CommunicationBase? communication = _activeCommunication;
            CommuniactionType? activeType = _activeCommunicationType;
            if (communication is null || activeType is null)
            {
                AppendReceiveLine("发送失败：请先执行通信测试。");
                return;
            }

            string message = SendText;
            if (string.IsNullOrWhiteSpace(message))
            {
                AppendReceiveLine("发送失败：报文不能为空。");
                return;
            }

            if (IsPlcCommunicationType(activeType))
            {
                AppendReceiveLine("发送失败：PLC 通信请使用 PLC 读写测试。");
                return;
            }

            if (communication is not ICommunication messageCommunication)
            {
                AppendReceiveLine("发送失败：当前通信对象不支持报文发送。");
                return;
            }

            if (activeType == CommuniactionType.TCPServer)
            {
                if (SelectedServerClient is null)
                {
                    AppendReceiveLine("发送失败：请先选择已连接的 TCP 客户端。");
                    return;
                }

                await SendToServerClientAsync(messageCommunication, message, SelectedServerClient);
                return;
            }
            SendReceiveModel readWriteModel = new(message);
            bool result = await messageCommunication.SendAsync(readWriteModel);
            string resultText = readWriteModel.Result is null ? string.Empty : $"，响应：{FormatMessage(readWriteModel.Result)}";
            AppendReceiveLine($"已发送：{message}，结果：{(result ? "成功" : "失败")}{resultText}");
        }
        catch (Exception ex)
        {
            AppendReceiveLine($"发送异常：{ex.Message}");
        }
    }

    private async Task SendAllAsync()
    {
        try
        {
            if (_activeCommunication is not ICommunication communication ||
                _activeCommunicationType != CommuniactionType.TCPServer)
            {
                AppendReceiveLine("群发失败：请先启动 TCP 服务端测试连接。");
                return;
            }

            string message = SendText;
            if (string.IsNullOrWhiteSpace(message))
            {
                AppendReceiveLine("群发失败：报文不能为空。");
                return;
            }

            List<ConnectedClientOption> clients = ConnectedServerClients.ToList();
            if (clients.Count == 0)
            {
                AppendReceiveLine("群发失败：当前没有已连接客户端。");
                return;
            }

            int successCount = 0;
            foreach (ConnectedClientOption client in clients)
            {
                if (await SendToServerClientAsync(communication, message, client))
                {
                    successCount++;
                }
            }

            AppendReceiveLine($"群发完成：{successCount}/{clients.Count} 个客户端发送成功。");
        }
        catch (Exception ex)
        {
            AppendReceiveLine($"群发异常：{ex.Message}");
        }
    }

    private async Task ReadPlcAsync()
    {
        try
        {
            if (!TryGetActivePlcCommunication(out IPlcCommunication? communication) ||
                !TryGetPlcTestArguments(out string address, out int length, out DataType dataType))
            {
                return;
            }

            PlcReadResult readResult = await communication!
                .ReadAsync(address, length, dataType)
                .ConfigureAwait(false);
            bool result = readResult.IsSuccess;
            SendReceiveModel readWriteModel = new(string.Empty, address, length, dataType)
            {
                Result = readResult.Message
            };

            AppendReceiveLine($"PLC 读取 {address}，长度 {length}，结果：{(result ? "成功" : "失败")}，响应：{FormatMessage(readWriteModel.Result)}");
        }
        catch (Exception ex)
        {
            AppendReceiveLine($"PLC 读取异常：{ex.Message}");
        }
    }

    private async Task WritePlcAsync()
    {
        try
        {
            if (!TryGetActivePlcCommunication(out IPlcCommunication? communication) ||
                !TryGetPlcTestArguments(out string address, out int length, out DataType dataType))
            {
                return;
            }

            string value = PlcWriteValue.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                AppendReceiveLine("PLC 写入失败：写入值不能为空。");
                return;
            }

            SendReceiveModel readWriteModel = new(value, address, length, dataType);
            PlcWriteResult writeResult = await communication!
                .WriteAsync(address, value, dataType)
                .ConfigureAwait(false);
            readWriteModel.Result = writeResult.Message;
            bool result = writeResult.IsSuccess;
            AppendReceiveLine($"PLC 写入 {address}，值 {value}，结果：{(result ? "成功" : "失败")}，响应：{FormatMessage(readWriteModel.Result)}");
        }
        catch (Exception ex)
        {
            AppendReceiveLine($"PLC 写入异常：{ex.Message}");
        }
    }

    private void CloseConnection()
    {
        CloseActiveCommunication(updateStatus: true);
    }

    private void ClearReceive()
    {
        ReceiveText = string.Empty;
    }

    private void RefreshPorts()
    {
        RefreshCurrentFields();
        RefreshPortNameOptions(updateSelectedProfile: true);
        AppendReceiveLine(PortNameOptions.Count == 0
            ? "未检测到串口，可手动输入端口名称。"
            : $"串口已刷新：{string.Join(", ", PortNameOptions)}。");
    }

    #endregion

    #region 配置与文件读写

    private void SeedProfiles()
    {
        string defaultTypeId = DeviceCommunicationConfigRegistry.Default.DefaultTypeId;
        AddProfile(CreateProfile(defaultTypeId, GenerateUniqueName(defaultTypeId)));
    }

    private DeviceCommunicationProfile CreateProfile(string typeId, string name)
    {
        DeviceCommunicationProfile profile = new(typeId)
        {
            LocalName = name
        };
        return profile;
    }

    private void AddProfile(DeviceCommunicationProfile profile)
    {
        Profiles.Add(profile);
        ProfilesView?.Refresh();
    }

    private void RefreshAvailableProtocols()
    {
        List<AvailableProtocolOption> options = LoadAvailableProtocolsFromDisk();

        AvailableProtocols.Clear();
        foreach (AvailableProtocolOption option in options)
        {
            AvailableProtocols.Add(option);
        }

        AvailableProtocolsView?.Refresh();
    }

    private List<AvailableProtocolOption> LoadAvailableProtocolsFromDisk()
    {
        List<AvailableProtocolOption> options = new();
        if (!Directory.Exists(ProtocolConfigDirectory))
        {
            return options;
        }

        foreach (string filePath in Directory.EnumerateFiles(ProtocolConfigDirectory, "*.json").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            if (!TryReadProtocolProfileFromFile(filePath, out ProtocolConfigProfile? profile, out _) || profile is null)
            {
                continue;
            }

            options.Add(new AvailableProtocolOption(profile.Name, filePath, profile.Summary));
        }

        return options;
    }

    private static bool TryReadProtocolProfileFromFile(string filePath, out ProtocolConfigProfile? profile, out string message)
    {
        profile = null;
        message = string.Empty;

        if (!File.Exists(filePath))
        {
            message = $"未找到协议文件：{filePath}";
            return false;
        }

        try
        {
            string storageText = File.ReadAllText(filePath, Encoding.UTF8);
            if (TryDeserializeProtocolProfile(storageText, out ProtocolConfigProfileDocument? document) && document is not null)
            {
                profile = document.ToProfile();
                return true;
            }

            message = $"文件 {Path.GetFileName(filePath)} 不是有效的协议配置。";
            return false;
        }
        catch (Exception ex)
        {
            message = $"读取协议文件失败：{ex.Message}";
            return false;
        }
    }

    private static bool TryDeserializeProtocolProfile(string storageText, out ProtocolConfigProfileDocument? document)
    {
        document = null;
        if (string.IsNullOrWhiteSpace(storageText))
        {
            return false;
        }

        try
        {
            document = JsonHelper.DeserializeObject<ProtocolConfigProfileDocument>(storageText.DesDecrypt());
            if (document is not null)
            {
                return true;
            }
        }
        catch
        {
        }

        try
        {
            document = JsonHelper.DeserializeObject<ProtocolConfigProfileDocument>(storageText);
            return document is not null;
        }
        catch
        {
            return false;
        }
    }

    private bool TrySaveProtocolProfileToLocalDirectory(ProtocolConfigProfile profile, out string savedPath, out string message)
    {
        savedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            message = "协议名称不能为空。";
            return false;
        }

        try
        {
            Directory.CreateDirectory(ProtocolConfigDirectory);

            string fileName = BuildProtocolStorageFileName(profile.Name);
            savedPath = Path.Combine(ProtocolConfigDirectory, fileName);

            if (File.Exists(savedPath))
            {
                MessageBoxResult result = MessageBox.Show(
                    $"协议“{profile.Name}”已存在，是否覆盖？",
                    "协议文件已存在",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    message = $"已取消覆盖协议“{profile.Name}”。";
                    savedPath = string.Empty;
                    return false;
                }
            }

            if (!ProtocolPreviewEngine.TryRefreshParsedResultKeys(profile, out string parseMessage))
            {
                message = parseMessage;
                savedPath = string.Empty;
                return false;
            }

            string storageText = JsonHelper.SerializeObject(ProtocolConfigProfileDocument.FromProfile(profile)).Encrypt();
            File.WriteAllText(savedPath, storageText, Encoding.UTF8);
            message = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            message = $"保存协议文件失败：{ex.Message}";
            savedPath = string.Empty;
            return false;
        }
    }

    private static string BuildProtocolStorageFileName(string protocolName)
    {
        string safeName = BuildSafeFileName(protocolName);
        if (string.Equals(safeName, "Communication", StringComparison.Ordinal))
        {
            safeName = "Protocol";
        }

        return $"{safeName}.json";
    }

    private DeviceSupportedProtocol ResolveSupportedProtocolTarget(
        DeviceCommunicationProfile profile,
        string protocolName,
        string protocolFilePath,
        DeviceSupportedProtocol? preferredTarget)
    {
        if (preferredTarget is not null && !profile.SupportedProtocols.Contains(preferredTarget))
        {
            preferredTarget = null;
        }

        DeviceSupportedProtocol? duplicate = profile.SupportedProtocols.FirstOrDefault(item =>
            !ReferenceEquals(item, preferredTarget) &&
            (string.Equals(item.ProtocolName, protocolName, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(item.ProtocolFilePath, protocolFilePath, StringComparison.OrdinalIgnoreCase)));

        if (duplicate is not null)
        {
            if (preferredTarget is not null && preferredTarget.IsEmpty)
            {
                profile.SupportedProtocols.Remove(preferredTarget);
            }

            return duplicate;
        }

        if (preferredTarget is not null)
        {
            return preferredTarget;
        }

        DeviceSupportedProtocol? placeholder = profile.SupportedProtocols.FirstOrDefault(item => item.IsEmpty);
        if (placeholder is not null)
        {
            return placeholder;
        }

        DeviceSupportedProtocol created = new();
        profile.SupportedProtocols.Add(created);
        return created;
    }

    private static string BuildSafeFileName(string localName)
    {
        HashSet<char> invalidChars = new(Path.GetInvalidFileNameChars());
        StringBuilder builder = new(localName.Trim().Length);
        foreach (char value in localName.Trim())
        {
            builder.Append(invalidChars.Contains(value) || char.IsControl(value)
                ? '_'
                : char.IsWhiteSpace(value) ? '_' : value);
        }

        string safeName = builder.ToString().Trim(' ', '.');
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "Communication";
        }

        return safeName.Length <= 80 ? safeName : safeName[..80];
    }

    private string GenerateUniqueName(string typeId)
    {
        string prefix = DeviceCommunicationConfigRegistry.Default.GetOrDefault(typeId).DisplayName;
        /*
            CommuniactionType.TCPClient => "TCP客户端",
            CommuniactionType.TCPServer => "TCP服务端",
            CommuniactionType.UDP => "UDP",
            CommuniactionType.COM => "串口",
            CommuniactionType.PLC => "PLC",
            _ => "通信配置"
        */

        for (int index = 1; ; index++)
        {
            string name = $"{prefix} {index}";
            if (!Profiles.Any(profile => string.Equals(profile.LocalName, name, StringComparison.OrdinalIgnoreCase)))
            {
                return name;
            }
        }
    }

    #endregion

    #region 通信对象事件

    private void AttachActiveCommunicationEvents(CommunicationBase communication)
    {
        communication.OnReceive += ActiveCommunication_OnReceive;
        communication.StateChange += ActiveCommunication_StateChange;
        communication.OnLog += ActiveCommunication_OnLog;

        if (communication is ICommunicationClientSource clientSource)
        {
            _activeClientSource = clientSource;
            clientSource.ClientsChanged += ActiveCommunication_ClientsChanged;
            RefreshConnectedServerClients(clientSource.GetConnectedClients());
        }
    }

    private void DetachActiveCommunicationEvents(CommunicationBase communication)
    {
        communication.OnReceive -= ActiveCommunication_OnReceive;
        communication.StateChange -= ActiveCommunication_StateChange;
        communication.OnLog -= ActiveCommunication_OnLog;

        if (_activeClientSource is not null)
        {
            _activeClientSource.ClientsChanged -= ActiveCommunication_ClientsChanged;
            _activeClientSource = null;
        }
    }

    private void CloseActiveCommunication(bool updateStatus)
    {
        CommunicationBase? communication = _activeCommunication;
        string? profileName = _activeProfileName;
        if (communication is null)
        {
            if (updateStatus)
            {
                SetConnectionStatus("未连接", NeutralBrush);
            }

            RaiseCommandStatesChanged();
            return;
        }

        try
        {
            DetachActiveCommunicationEvents(communication);
            if (!string.IsNullOrWhiteSpace(profileName))
            {
                CommunicationFactory.Remove(profileName);
            }
            else
            {
                communication.Close();
            }
        }
        catch (Exception ex)
        {
            AppendReceiveLine($"关闭连接时发生异常：{ex.Message}");
        }
        finally
        {
            _activeCommunication = null;
            _activeProfileName = null;
            _activeCommunicationType = null;
            _activeParseOnlyCommands.Clear();
            SetConnectionEstablished(false);
            RefreshConnectedServerClients(Array.Empty<CommunicationClientInfo>());
            RaiseCommunicationVisibilityChanged();
        }

        if (updateStatus)
        {
            SetConnectionStatus("未连接", NeutralBrush);
            AppendReceiveLine("已关闭当前测试连接。");
        }

        RaiseCommandStatesChanged();
    }

    private string ActiveCommunication_OnReceive(object message, params object[] param)
    {
        string endpointText = FormatEndpoint(param);
        if (_activeCommunicationType == CommuniactionType.TCPServer && param.Length > 0)
        {
            SelectServerClient(param[0]?.ToString());
        }

        AppendReceiveLine($"收到{endpointText}：{FormatMessage(message)}");
        string rawMessage = BuildRawProtocolData(message);
        TryParseIncomingProtocolMessage(message, rawMessage);
        return string.Empty;
    }

    private void ActiveCommunication_StateChange(ConnectState connectState, string localName)
    {
        string stateText = connectState == ConnectState.Connected ? "已连接" : "已断开";
        Brush stateBrush = connectState == ConnectState.Connected ? SuccessBrush : WarningBrush;
        SetConnectionEstablished(connectState == ConnectState.Connected);
        SetConnectionStatus($"{localName} {stateText}", stateBrush);
        AppendReceiveLine($"状态变化：{localName} {stateText}。");
        RaiseCommandStatesChanged();
    }

    private void ActiveCommunication_OnLog(LogMessageModel log)
    {
        AppendReceiveLine($"日志 {log.Type}：{log.Message}");
    }

    private void ActiveCommunication_ClientsChanged(IReadOnlyList<CommunicationClientInfo> clients)
    {
        RefreshConnectedServerClients(clients);
    }

    private async Task<bool> SendToServerClientAsync(ICommunication communication, string message, ConnectedClientOption client)
    {
        try
        {
            SendReceiveModel readWriteModel = new(message, client.ClientId);
            bool result = await communication.SendAsync(readWriteModel);
            string resultText = readWriteModel.Result is null ? string.Empty : $"，响应：{FormatMessage(readWriteModel.Result)}";
            AppendReceiveLine($"发送到 {client.DisplayName}：{message}，结果：{(result ? "成功" : "失败")}{resultText}");
            return result;
        }
        catch (Exception ex)
        {
            AppendReceiveLine($"发送到 {client.DisplayName} 失败：{ex.Message}");
            return false;
        }
    }

    private void RefreshConnectedServerClients(IReadOnlyList<CommunicationClientInfo> clients)
    {
        RunOnUiThread(() =>
        {
            string? selectedClientId = SelectedServerClient?.ClientId;
            List<ConnectedClientOption> options = clients
                .Select(client => new ConnectedClientOption(client.ClientId, client.DisplayName, client.Address, client.Port))
                .ToList();

            ConnectedServerClients.Clear();
            foreach (ConnectedClientOption option in options)
            {
                ConnectedServerClients.Add(option);
            }

            SelectedServerClient = options.FirstOrDefault(option =>
                                     string.Equals(option.ClientId, selectedClientId, StringComparison.OrdinalIgnoreCase))
                                 ?? options.FirstOrDefault();

            OnPropertyChanged(nameof(ConnectedServerClientStatusText));
        });
    }

    private void SelectServerClient(string? clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return;
        }

        RunOnUiThread(() =>
        {
            ConnectedClientOption? matchedClient = ConnectedServerClients.FirstOrDefault(option =>
                string.Equals(option.ClientId, clientId, StringComparison.OrdinalIgnoreCase));

            if (matchedClient is not null)
            {
                SelectedServerClient = matchedClient;
            }
        });
    }

    private bool TryGetActivePlcCommunication(out IPlcCommunication? communication)
    {
        communication = _activeCommunication as IPlcCommunication;
        if (communication is not null && IsPlcCommunicationType(_activeCommunicationType))
        {
            return true;
        }

        AppendReceiveLine("PLC 测试失败：请先启动 PLC 测试连接。");
        return false;
    }

    private bool TryGetPlcTestArguments(out string address, out int length, out DataType dataType)
    {
        address = PlcAddress.Trim();
        length = 0;
        dataType = DataType.Decimal;

        if (string.IsNullOrWhiteSpace(address))
        {
            AppendReceiveLine("PLC 测试失败：PLC 地址不能为空。");
            return false;
        }

        if (!int.TryParse(PlcLength.Trim(), out length) || length <= 0)
        {
            AppendReceiveLine("PLC 测试失败：长度必须大于 0。");
            return false;
        }

        if (!Enum.TryParse(SelectedPlcDataType, out dataType))
        {
            dataType = DataType.Decimal;
        }

        return true;
    }

    #endregion

    #region 视图与辅助方法

    private void SetConnectionEstablished(bool isConnected)
    {
        if (_isConnectionEstablished == isConnected)
        {
            return;
        }

        _isConnectionEstablished = isConnected;
        if (isConnected && IsProtocolLibraryOpen)
        {
            CloseProtocolLibrary();
        }

        OnPropertyChanged(nameof(IsProtocolConfigurationEditable));
        OnPropertyChanged(nameof(ProtocolConfigurationEditHint));
        RaiseCommandStatesChanged();
    }

    private void RefreshActiveParseOnlyCommands(DeviceCommunicationProfile? profile)
    {
        _activeParseOnlyCommands.Clear();
        if (profile is null)
        {
            return;
        }

        IEnumerable<DeviceSupportedProtocol> supportedProtocols = profile.SupportedProtocols
            .Where(protocol =>
                !string.IsNullOrWhiteSpace(protocol.ProtocolName) &&
                !string.IsNullOrWhiteSpace(protocol.ProtocolFilePath))
            .GroupBy(
                protocol => $"{protocol.ProtocolName}|{protocol.ProtocolFilePath}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First());

        foreach (DeviceSupportedProtocol supportedProtocol in supportedProtocols)
        {
            if (!TryReadProtocolProfileFromFile(
                    supportedProtocol.ProtocolFilePath,
                    out ProtocolConfigProfile? protocolProfile,
                    out _) ||
                protocolProfile is null)
            {
                continue;
            }

            foreach (ProtocolCommandConfig command in protocolProfile.Commands.Where(item => item.IsParseOnly))
            {
                _activeParseOnlyCommands.Add(new BoundParseOnlyCommand(
                    protocolProfile.Name,
                    command.Name,
                    command.Clone(command.Name)));
            }
        }
    }

    private void TryParseIncomingProtocolMessage(object message, string rawMessage)
    {
        if (_activeParseOnlyCommands.Count == 0)
        {
            return;
        }

        foreach (BoundParseOnlyCommand parseCommand in _activeParseOnlyCommands)
        {
            string responseText = BuildProtocolResponseText(message, rawMessage, parseCommand.Command.ResponseFormat);
            if (string.IsNullOrWhiteSpace(responseText))
            {
                continue;
            }

            ProtocolCommandConfig command = parseCommand.Command.Clone(parseCommand.CommandName);
            command.SampleResponseText = responseText;
            if (!ProtocolPreviewEngine.TryBuildResponsePreview(
                    command,
                    out ProtocolResponsePreviewResult? previewResult,
                    out string parseMessage) ||
                previewResult is null)
            {
                AppendReceiveLine($"协议解析失败：{parseCommand.ProtocolName}/{parseCommand.CommandName}，原因：{(string.IsNullOrWhiteSpace(parseMessage) ? "未返回可用解析结果。" : parseMessage)}");
                continue;
            }

            AppendParsedProtocolResults(previewResult.ParsedJson, rawMessage);
        }
    }

    private void AppendParsedProtocolResults(string parsedJson, string rawMessage)
    {
        if (string.IsNullOrWhiteSpace(parsedJson))
        {
            return;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(parsedJson);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in document.RootElement.EnumerateObject())
                {
                    AppendParsedProtocolResultLog(property.Name, FormatJsonValue(property.Value), rawMessage);
                }

                return;
            }

            AppendParsedProtocolResultLog("Data", FormatJsonValue(document.RootElement), rawMessage);
        }
        catch (JsonException)
        {
            AppendParsedProtocolResultLog("Data", parsedJson, rawMessage);
        }
    }

    private void AppendParsedProtocolResultLog(string key, string value, string rawMessage)
    {
        AppendReceiveLine($"解析结果：Key={key ?? string.Empty}，Value={value ?? string.Empty}，Data={rawMessage ?? string.Empty}");
    }

    private static string BuildRawProtocolData(object? message)
    {
        return message switch
        {
            null => string.Empty,
            byte[] bytes => BitConverter.ToString(bytes).Replace("-", string.Empty, StringComparison.Ordinal),
            _ => message.ToString() ?? string.Empty
        };
    }

    private static string BuildProtocolResponseText(object? message, string rawMessage, ProtocolPayloadFormat responseFormat)
    {
        if (message is byte[] bytes)
        {
            return responseFormat == ProtocolPayloadFormat.Hex
                ? BitConverter.ToString(bytes).Replace("-", string.Empty, StringComparison.Ordinal)
                : Encoding.UTF8.GetString(bytes);
        }

        return rawMessage;
    }

    private static string FormatJsonValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            JsonValueKind.Null => string.Empty,
            _ => value.GetRawText()
        };
    }

    private bool FilterProfiles(object item)
    {
        if (item is not DeviceCommunicationProfile profile)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        string keyword = SearchText.Trim();
        return Contains(profile.LocalName, keyword) ||
               Contains(profile.TypeDisplayName, keyword) ||
               Contains(profile.Summary, keyword) ||
               profile.SupportedProtocols.Any(protocol =>
                   Contains(protocol.ProtocolName, keyword) ||
                   Contains(protocol.ProtocolFilePath, keyword));
    }

    private bool FilterAvailableProtocols(object item)
    {
        if (item is not AvailableProtocolOption option)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(AvailableProtocolSearchText))
        {
            return true;
        }

        string keyword = AvailableProtocolSearchText.Trim();
        return Contains(option.Name, keyword) ||
               Contains(option.FilePath, keyword) ||
               Contains(option.Summary, keyword);
    }

    private bool FilterSupportedProtocolCommands(object item)
    {
        if (item is not SupportedProtocolCommandOption option)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SupportedProtocolCommandSearchText))
        {
            return true;
        }

        string keyword = SupportedProtocolCommandSearchText.Trim();
        return Contains(option.ProtocolName, keyword) ||
               Contains(option.CommandName, keyword) ||
               Contains(option.DisplayName, keyword) ||
               Contains(option.Summary, keyword) ||
               Contains(option.PreviewMessage, keyword);
    }

    private static bool Contains(string? source, string keyword)
    {
        return source?.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void SetConnectionStatus(string text, Brush brush)
    {
        RunOnUiThread(() =>
        {
            ConnectionStatusText = text;
            ConnectionStatusBrush = brush;
        });
    }

    private void AppendReceiveLine(string message)
    {
        RunOnUiThread(() =>
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            ReceiveText = string.IsNullOrEmpty(ReceiveText)
                ? line
                : $"{ReceiveText}{Environment.NewLine}{line}";

            if (ReceiveText.Length > MaxReceiveTextLength)
            {
                ReceiveText = ReceiveText[^MaxReceiveTextLength..];
            }
        });
    }

    private void RunOnUiThread(Action action)
    {
        Dispatcher dispatcher = GetUiDispatcher();
        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.BeginInvoke(action);
    }

    private static Dispatcher GetUiDispatcher()
    {
        return Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
    }

    private static string FormatEndpoint(object[] param)
    {
        if (param.Length >= 3)
        {
            return $" [{param[1]}:{param[2]}]";
        }

        return param.Length > 0 ? $" [{FormatMessage(param[0])}]" : string.Empty;
    }

    private static string FormatMessage(object? message)
    {
        if (message is null)
        {
            return string.Empty;
        }

        if (message is byte[] bytes)
        {
            string text = Encoding.UTF8.GetString(bytes);
            string hex = BitConverter.ToString(bytes);
            return string.IsNullOrWhiteSpace(text) ? hex : $"{text} ({hex})";
        }

        return message.ToString() ?? string.Empty;
    }

    private static bool IsSupportedCommunicationType(string? typeId)
    {
        return DeviceCommunicationConfigRegistry.Default.Contains(typeId);
    }

    private static bool IsPlcCommunicationType(CommuniactionType? type)
    {
        return type == CommuniactionType.PLC;
    }

    private void RaiseCommandStatesChanged()
    {
        RaiseCommandState(NewProfileCommand);
        RaiseCommandState(DuplicateProfileCommand);
        RaiseCommandState(DeleteProfileCommand);
        RaiseCommandState(SaveProfilesCommand);
        RaiseCommandState(AddSupportedProtocolCommand);
        RaiseCommandState(AddAvailableProtocolCommand);
        RaiseCommandState(DeleteSupportedProtocolCommand);
        RaiseCommandState(LoadSupportedProtocolFileCommand);
        RaiseCommandState(OpenProtocolCommandLibraryCommand);
        RaiseCommandState(FillSupportedProtocolCommandCommand);
        RaiseCommandState(CloseProtocolLibraryCommand);
        RaiseCommandState(CloseProtocolCommandLibraryCommand);
        RaiseCommandState(TestConnectionCommand);
        RaiseCommandState(CloseConnectionCommand);
    }

    private void CloseActiveCommunicationForConfigurationChange()
    {
        if (_activeCommunication is null)
        {
            return;
        }

        CloseActiveCommunication(updateStatus: false);
        SetConnectionStatus("配置已变更", NeutralBrush);
        AppendReceiveLine("驱动类型已变更，已关闭当前测试连接。");
    }

    private static void RaiseCommandState(ICommand? command)
    {
        if (command is RelayCommand relayCommand)
        {
            relayCommand.RaiseCanExecuteChanged();
        }
    }

    private void RefreshCurrentFields()
    {
        CurrentFields.Clear();
        if (SelectedProfile is null)
        {
            return;
        }

        DeviceCommunicationConfigDescriptor descriptor =
            DeviceCommunicationConfigRegistry.Default.GetOrDefault(SelectedProfile.TypeId);
        foreach (DeviceCommunicationConfigFieldViewModel field in descriptor.CreateFieldViewModels(SelectedProfile))
        {
            CurrentFields.Add(field);
        }

        if (SelectedProfile.IsSerialType)
        {
            RefreshPortNameOptions(updateSelectedProfile: false);
        }

        OnPropertyChanged(nameof(IsPortRefreshVisible));
    }

    private void RefreshPortNameOptions(bool updateSelectedProfile)
    {
        List<string> detectedPortNames = GetDetectedSerialPortNames();

        PortNameOptions.Clear();
        foreach (string portName in detectedPortNames)
        {
            PortNameOptions.Add(portName);
        }

        if (updateSelectedProfile && SelectedProfile?.IsSerialType == true && detectedPortNames.Count > 0)
        {
            DeviceCommunicationConfigFieldViewModel? portField =
                CurrentFields.FirstOrDefault(field => string.Equals(field.Key, "PortName", StringComparison.OrdinalIgnoreCase));
            if (portField is not null &&
                (string.IsNullOrWhiteSpace(portField.Value) ||
                 (string.Equals(portField.Value, "COM1", StringComparison.OrdinalIgnoreCase) &&
                  !ContainsPortName(detectedPortNames, portField.Value))))
            {
                portField.Value = detectedPortNames[0];
            }
        }
    }

    private static List<string> GetDetectedSerialPortNames()
    {
        try
        {
            return SerialPort.GetPortNames()
                .Where(portName => !string.IsNullOrWhiteSpace(portName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(GetSerialPortSortNumber)
                .ThenBy(portName => portName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static bool ContainsPortName(IEnumerable<string> portNames, string portName)
    {
        return portNames.Any(value => string.Equals(value, portName, StringComparison.OrdinalIgnoreCase));
    }

    private static int GetSerialPortSortNumber(string portName)
    {
        if (portName.StartsWith("COM", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(portName[3..], out int number))
        {
            return number;
        }

        return int.MaxValue;
    }

    #endregion
}
