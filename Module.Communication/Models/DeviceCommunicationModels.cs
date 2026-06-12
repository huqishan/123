using ControlLibrary;
using Module.Communication.ViewModels.PropertyVMs;
using Shared.Abstractions.Enum;
using Shared.Models.Communication;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Module.Communication.Models
{
    #region 通用选项模型

    /// <summary>
    /// 通信类型下拉选项。
    /// </summary>
    public sealed class CommunicationTypeOption
    {
        /// <summary>
        /// 创建通信类型选项。
        /// </summary>
        /// <param name="value">通信配置类型标识。</param>
        /// <param name="displayName">显示名称。</param>
        /// <param name="description">说明文本。</param>
        public CommunicationTypeOption(string value, string displayName, string description)
        {
            Value = value;
            DisplayName = displayName;
            Description = description;
        }

        /// <summary>
        /// 通信配置类型标识。
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// 显示名称。
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// 说明文本。
        /// </summary>
        public string Description { get; }

    }

    /// <summary>
    /// 通用值与显示文本选项。
    /// </summary>
    public sealed class SelectionOption
    {
        /// <summary>
        /// 创建通用下拉选项。
        /// </summary>
        /// <param name="value">选项值。</param>
        /// <param name="displayName">选项显示文本。</param>
        public SelectionOption(string value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }

        /// <summary>
        /// 选项值。
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// 选项显示文本。
        /// </summary>
        public string DisplayName { get; }
    }

    /// <summary>
    /// TCP 服务端已连接客户端选项。
    /// </summary>
    public sealed class ConnectedClientOption
    {
        /// <summary>
        /// 创建 TCP 服务端客户端选项。
        /// </summary>
        /// <param name="clientId">客户端标识。</param>
        /// <param name="displayName">显示名称。</param>
        /// <param name="address">客户端地址。</param>
        /// <param name="port">客户端端口。</param>
        public ConnectedClientOption(string clientId, string displayName, string address, int port)
        {
            ClientId = clientId;
            DisplayName = displayName;
            Address = address;
            Port = port;
        }

        /// <summary>
        /// 客户端标识。
        /// </summary>
        public string ClientId { get; }

        /// <summary>
        /// 显示名称。
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// 客户端地址。
        /// </summary>
        public string Address { get; }

        /// <summary>
        /// 客户端端口。
        /// </summary>
        public int Port { get; }
    }

    /// <summary>
    /// 可关联协议选项。
    /// </summary>
    public sealed class AvailableProtocolOption
    {
        /// <summary>
        /// 创建可关联协议选项。
        /// </summary>
        /// <param name="name">协议名称。</param>
        /// <param name="filePath">协议文件路径。</param>
        /// <param name="summary">协议摘要。</param>
        public AvailableProtocolOption(string name, string filePath, string summary)
        {
            Name = name;
            FilePath = filePath;
            Summary = summary;
        }

        /// <summary>
        /// 协议名称。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 协议文件路径。
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// 协议摘要。
        /// </summary>
        public string Summary { get; }
    }

    /// <summary>
    /// 支持协议中的指令选项。
    /// </summary>
    public sealed class SupportedProtocolCommandOption
    {
        /// <summary>
        /// 创建支持协议指令选项。
        /// </summary>
        /// <param name="protocolName">协议名称。</param>
        /// <param name="protocolFilePath">协议文件路径。</param>
        /// <param name="commandName">指令名称。</param>
        /// <param name="summary">指令摘要。</param>
        /// <param name="previewMessage">预览报文。</param>
        /// <param name="fillMessage">填充报文。</param>
        /// <param name="canFill">是否允许填充到发送框。</param>
        public SupportedProtocolCommandOption(
            string protocolName,
            string protocolFilePath,
            string commandName,
            string summary,
            string previewMessage,
            string fillMessage,
            bool canFill)
        {
            ProtocolName = protocolName;
            ProtocolFilePath = protocolFilePath;
            CommandName = commandName;
            Summary = summary;
            PreviewMessage = previewMessage;
            FillMessage = fillMessage;
            CanFill = canFill;
        }

        /// <summary>
        /// 协议名称。
        /// </summary>
        public string ProtocolName { get; }

        /// <summary>
        /// 协议文件路径。
        /// </summary>
        public string ProtocolFilePath { get; }

        /// <summary>
        /// 指令名称。
        /// </summary>
        public string CommandName { get; }

        /// <summary>
        /// 指令摘要。
        /// </summary>
        public string Summary { get; }

        /// <summary>
        /// 预览报文。
        /// </summary>
        public string PreviewMessage { get; }

        /// <summary>
        /// 填充报文。
        /// </summary>
        public string FillMessage { get; }

        /// <summary>
        /// 是否允许填充到发送框。
        /// </summary>
        public bool CanFill { get; }

        /// <summary>
        /// 指令显示名称。
        /// </summary>
        public string DisplayName => $"{ProtocolName} / {CommandName}";
    }

    #endregion

    #region 配置文档模型

    /// <summary>
    /// 设备支持协议的持久化文档模型。
    /// </summary>
    internal sealed class DeviceSupportedProtocolDocument
    {
        /// <summary>
        /// 协议名称。
        /// </summary>
        public string? ProtocolName { get; set; }

        /// <summary>
        /// 协议文件路径。
        /// </summary>
        public string? ProtocolFilePath { get; set; }

        /// <summary>
        /// 从界面模型创建持久化文档模型。
        /// </summary>
        /// <param name="protocol">设备支持协议界面模型。</param>
        /// <returns>设备支持协议文档模型。</returns>
        public static DeviceSupportedProtocolDocument FromModel(DeviceSupportedProtocol protocol)
        {
            return new DeviceSupportedProtocolDocument
            {
                ProtocolName = protocol.ProtocolName,
                ProtocolFilePath = protocol.ProtocolFilePath
            };
        }

        /// <summary>
        /// 将持久化文档模型转换为界面模型。
        /// </summary>
        /// <returns>设备支持协议界面模型。</returns>
        public DeviceSupportedProtocol ToModel()
        {
            return new DeviceSupportedProtocol
            {
                ProtocolName = ProtocolName ?? string.Empty,
                ProtocolFilePath = ProtocolFilePath ?? string.Empty
            };
        }
    }

    /// <summary>
    /// 设备通信配置的持久化文档模型。
    /// </summary>
    internal sealed class DeviceCommunicationProfileDocument
    {
        /// <summary>
        /// 配置文档版本。
        /// </summary>
        public int Version { get; set; } = 3;

        /// <summary>
        /// 配置名称。
        /// </summary>
        public string? LocalName { get; set; }

        /// <summary>
        /// 通信配置类型标识。
        /// </summary>
        public string? TypeId { get; set; }

        /// <summary>
        /// 连接参数字典。
        /// </summary>
        public Dictionary<string, string>? Config { get; set; }

        /// <summary>
        /// 支持协议文档集合。
        /// </summary>
        public List<DeviceSupportedProtocolDocument>? SupportedProtocols { get; set; }

        /// <summary>
        /// 从设备通信配置创建持久化文档模型。
        /// </summary>
        /// <param name="profile">设备通信配置。</param>
        /// <returns>设备通信配置文档模型。</returns>
        public static DeviceCommunicationProfileDocument FromProfile(DeviceCommunicationProfile profile)
        {
            return new DeviceCommunicationProfileDocument
            {
                LocalName = profile.LocalName,
                TypeId = profile.TypeId,
                Config = profile.Parameters.ToDictionary(
                    item => item.Key,
                    item => item.Value,
                    StringComparer.OrdinalIgnoreCase),
                SupportedProtocols = profile.SupportedProtocols
                    .Where(protocol => !protocol.IsEmpty)
                    .Select(DeviceSupportedProtocolDocument.FromModel)
                    .ToList()
            };
        }

        /// <summary>
        /// 将持久化文档模型转换为设备通信配置。
        /// </summary>
        /// <returns>设备通信配置。</returns>
        public DeviceCommunicationProfile ToProfile()
        {
            DeviceCommunicationProfile profile = new(TypeId)
            {
                LocalName = string.IsNullOrWhiteSpace(LocalName) ? "通信配置" : LocalName.Trim()
            };

            profile.ReplaceParameters(Config);

            if (SupportedProtocols is { Count: > 0 })
            {
                foreach (DeviceSupportedProtocolDocument protocolDocument in SupportedProtocols)
                {
                    DeviceSupportedProtocol protocol = protocolDocument.ToModel();
                    if (!protocol.IsEmpty)
                    {
                        profile.SupportedProtocols.Add(protocol);
                    }
                }
            }

            return profile;
        }
    }

    #endregion

    #region 设备通信配置模型

    /// <summary>
    /// 设备通信配置界面模型。
    /// </summary>
    public sealed class DeviceCommunicationProfile : ViewModelProperties
    {
        #region 字段

        /// <summary>
        /// 连接参数字典，键为字段标识。
        /// </summary>
        private readonly Dictionary<string, string> _parameters =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 配置名称。
        /// </summary>
        private string _localName = "TCP客户端 1";

        /// <summary>
        /// 当前通信配置类型标识。
        /// </summary>
        private string _typeId;

        #endregion

        #region 构造函数与事件

        /// <summary>
        /// 使用默认通信配置类型创建设备通信配置。
        /// </summary>
        public DeviceCommunicationProfile()
            : this(DeviceCommunicationConfigRegistry.Default.DefaultTypeId)
        {
        }

        /// <summary>
        /// 使用指定通信配置类型创建设备通信配置。
        /// </summary>
        /// <param name="typeId">通信配置类型标识。</param>
        public DeviceCommunicationProfile(string? typeId)
        {
            _typeId = NormalizeTypeId(typeId);
            ResetParametersToDefaults(raiseChanged: false);
            SupportedProtocols.CollectionChanged += SupportedProtocols_CollectionChanged;
        }

        #endregion

        #region 集合与状态属性

        /// <summary>
        /// 当前设备支持的协议集合。
        /// </summary>
        public ObservableCollection<DeviceSupportedProtocol> SupportedProtocols { get; } = new();

        /// <summary>
        /// 当前设备通信连接参数。
        /// </summary>
        public IReadOnlyDictionary<string, string> Parameters => _parameters;

        /// <summary>
        /// 配置名称。
        /// </summary>
        public string LocalName
        {
            get => _localName;
            set
            {
                if (SetField(ref _localName, value?.Trim() ?? string.Empty))
                {
                    RaiseStateChanged();
                }
            }
        }

        /// <summary>
        /// 通信配置类型标识。
        /// </summary>
        public string TypeId
        {
            get => _typeId;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                string normalizedTypeId = NormalizeTypeId(value);
                if (string.Equals(_typeId, normalizedTypeId, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _typeId = normalizedTypeId;
                ResetParametersToDefaults(raiseChanged: false);
                OnPropertyChanged();
                RaiseTypeStateChanged();
            }
        }

        /// <summary>
        /// 通信配置类型显示名称。
        /// </summary>
        public string TypeDisplayName => Descriptor.DisplayName;

        /// <summary>
        /// 通信配置类型说明文本。
        /// </summary>
        public string TypeDescription => Descriptor.Description;

        /// <summary>
        /// 当前配置对应的运行时通信类型。
        /// </summary>
        public CommuniactionType RuntimeType => Descriptor.RuntimeType;

        /// <summary>
        /// 当前配置是否为串口通信。
        /// </summary>
        public bool IsSerialType => Descriptor.IsSerialPort;

        /// <summary>
        /// 当前配置是否为 TCP 服务端。
        /// </summary>
        public bool IsTcpServerType => Descriptor.RuntimeType == CommuniactionType.TCPServer;

        /// <summary>
        /// 当前配置是否支持 PLC 读写测试。
        /// </summary>
        public bool IsPlcType => Descriptor.SupportsPlcReadWriteTest;

        /// <summary>
        /// 当前配置是否支持通用报文发送测试。
        /// </summary>
        public bool SupportsGenericSendTest => Descriptor.SupportsGenericSendTest;

        /// <summary>
        /// 当前配置摘要。
        /// </summary>
        public string Summary => Descriptor.BuildSummary(this);

        /// <summary>
        /// 已关联协议名称摘要。
        /// </summary>
        public string SupportedProtocolsSummary
        {
            get
            {
                string[] protocolNames = SupportedProtocols
                    .Select(item => item.ProtocolName?.Trim() ?? string.Empty)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                return protocolNames.Length == 0 ? "无" : string.Join("&", protocolNames);
            }
        }

        /// <summary>
        /// 已关联协议显示文本。
        /// </summary>
        public string SupportedProtocolsDisplayText => $"支持协议：{SupportedProtocolsSummary}";

        /// <summary>
        /// 当前通信配置类型描述符。
        /// </summary>
        private DeviceCommunicationConfigDescriptor Descriptor =>
            DeviceCommunicationConfigRegistry.Default.GetOrDefault(TypeId);

        #endregion

        #region 参数读写

        /// <summary>
        /// 获取指定连接参数值。
        /// </summary>
        /// <param name="key">参数键。</param>
        /// <returns>参数值；不存在时返回空字符串。</returns>
        public string GetParameter(string key)
        {
            return _parameters.TryGetValue(key, out string? value) ? value : string.Empty;
        }

        /// <summary>
        /// 判断指定连接参数是否存在。
        /// </summary>
        /// <param name="key">参数键。</param>
        /// <returns>存在返回 true，否则返回 false。</returns>
        public bool HasParameter(string key)
        {
            return _parameters.ContainsKey(key);
        }

        /// <summary>
        /// 设置指定连接参数值。
        /// </summary>
        /// <param name="key">参数键。</param>
        /// <param name="value">参数值。</param>
        /// <param name="raiseChanged">是否触发状态变更通知。</param>
        /// <returns>参数发生变化返回 true，否则返回 false。</returns>
        public bool SetParameter(string key, string value, bool raiseChanged = true)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            value ??= string.Empty;
            if (_parameters.TryGetValue(key, out string? existing) &&
                string.Equals(existing, value, StringComparison.Ordinal))
            {
                return false;
            }

            _parameters[key] = value;
            if (raiseChanged)
            {
                RaiseStateChanged();
            }

            return true;
        }

        /// <summary>
        /// 用指定参数字典替换当前连接参数。
        /// </summary>
        /// <param name="parameters">新的连接参数字典。</param>
        public void ReplaceParameters(IDictionary<string, string>? parameters)
        {
            _parameters.Clear();
            ResetParametersToDefaults(raiseChanged: false);

            if (parameters is not null)
            {
                foreach (KeyValuePair<string, string> parameter in parameters)
                {
                    _parameters[parameter.Key] = parameter.Value ?? string.Empty;
                }
            }

            RaiseStateChanged();
        }

        /// <summary>
        /// 将连接参数重置为当前通信配置类型的默认值。
        /// </summary>
        public void ResetParametersToDefaults()
        {
            ResetParametersToDefaults(raiseChanged: true);
        }

        /// <summary>
        /// 将连接参数重置为当前通信配置类型的默认值。
        /// </summary>
        /// <param name="raiseChanged">是否触发状态变更通知。</param>
        private void ResetParametersToDefaults(bool raiseChanged)
        {
            _parameters.Clear();
            foreach (KeyValuePair<string, string> parameter in Descriptor.CreateDefaultParameters())
            {
                _parameters[parameter.Key] = parameter.Value;
            }

            if (raiseChanged)
            {
                RaiseStateChanged();
            }
        }

        #endregion

        #region 克隆与运行时配置

        /// <summary>
        /// 克隆当前设备通信配置。
        /// </summary>
        /// <param name="localName">克隆后的配置名称。</param>
        /// <returns>克隆后的设备通信配置。</returns>
        public DeviceCommunicationProfile Clone(string localName)
        {
            DeviceCommunicationProfile clone = new(TypeId)
            {
                LocalName = localName
            };
            clone.ReplaceParameters(_parameters);

            foreach (DeviceSupportedProtocol protocol in SupportedProtocols.Where(item => !item.IsEmpty))
            {
                clone.SupportedProtocols.Add(new DeviceSupportedProtocol
                {
                    ProtocolName = protocol.ProtocolName,
                    ProtocolFilePath = protocol.ProtocolFilePath
                });
            }

            return clone;
        }

        /// <summary>
        /// 尝试构建运行时通信配置。
        /// </summary>
        /// <param name="config">构建成功后的运行时通信配置。</param>
        /// <param name="validationMessage">验证或构建结果消息。</param>
        /// <returns>构建成功返回 true，否则返回 false。</returns>
        public bool TryBuildRuntimeConfig(out ICommunicationRuntimeConfig? config, out string validationMessage)
        {
            return Descriptor.TryBuildRuntimeConfig(this, out config, out validationMessage);
        }

        #endregion

        #region 状态通知

        /// <summary>
        /// 通知通信配置类型相关属性发生变化。
        /// </summary>
        private void RaiseTypeStateChanged()
        {
            OnPropertyChanged(nameof(TypeDisplayName));
            OnPropertyChanged(nameof(TypeDescription));
            OnPropertyChanged(nameof(RuntimeType));
            OnPropertyChanged(nameof(IsSerialType));
            OnPropertyChanged(nameof(IsTcpServerType));
            OnPropertyChanged(nameof(IsPlcType));
            OnPropertyChanged(nameof(SupportsGenericSendTest));
            RaiseStateChanged();
        }

        /// <summary>
        /// 通知摘要和协议显示状态发生变化。
        /// </summary>
        private void RaiseStateChanged()
        {
            OnPropertyChanged(nameof(Summary));
            OnPropertyChanged(nameof(SupportedProtocolsSummary));
            OnPropertyChanged(nameof(SupportedProtocolsDisplayText));
        }

        /// <summary>
        /// 处理支持协议集合变化。
        /// </summary>
        /// <param name="sender">事件来源。</param>
        /// <param name="e">集合变化参数。</param>
        private void SupportedProtocols_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems is not null)
            {
                foreach (DeviceSupportedProtocol protocol in e.NewItems.OfType<DeviceSupportedProtocol>())
                {
                    protocol.PropertyChanged += SupportedProtocol_PropertyChanged;
                }
            }

            if (e.OldItems is not null)
            {
                foreach (DeviceSupportedProtocol protocol in e.OldItems.OfType<DeviceSupportedProtocol>())
                {
                    protocol.PropertyChanged -= SupportedProtocol_PropertyChanged;
                }
            }

            RaiseStateChanged();
        }

        /// <summary>
        /// 处理支持协议属性变化。
        /// </summary>
        /// <param name="sender">事件来源。</param>
        /// <param name="e">属性变化参数。</param>
        private void SupportedProtocol_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(DeviceSupportedProtocol.ProtocolName) or nameof(DeviceSupportedProtocol.ProtocolFilePath))
            {
                RaiseStateChanged();
            }
        }


        /// <summary>
        /// 规范化通信配置类型标识。
        /// </summary>
        /// <param name="typeId">原始通信配置类型标识。</param>
        /// <returns>已注册的通信配置类型标识。</returns>
        private static string NormalizeTypeId(string? typeId)
        {
            DeviceCommunicationConfigRegistry registry = DeviceCommunicationConfigRegistry.Default;
            return registry.GetOrDefault(typeId).TypeId;
        }

        #endregion
    }

    #endregion
    public sealed class DeviceSupportedProtocol : ViewModelProperties
    {
        private string _protocolName = string.Empty;
        private string _protocolFilePath = string.Empty;

        public string ProtocolName
        {
            get => _protocolName;
            set
            {
                if (SetField(ref _protocolName, value?.Trim() ?? string.Empty))
                {
                    OnPropertyChanged(nameof(DisplayProtocolName));
                }
            }
        }

        public string ProtocolFilePath
        {
            get => _protocolFilePath;
            set
            {
                if (SetField(ref _protocolFilePath, value?.Trim() ?? string.Empty))
                {
                    OnPropertyChanged(nameof(DisplayProtocolFilePath));
                }
            }
        }

        public string DisplayProtocolName =>
            string.IsNullOrWhiteSpace(ProtocolName) ? "未选择协议" : ProtocolName;

        public string DisplayProtocolFilePath =>
            string.IsNullOrWhiteSpace(ProtocolFilePath) ? "点击加载协议文件" : ProtocolFilePath;

        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(ProtocolName) &&
            string.IsNullOrWhiteSpace(ProtocolFilePath);

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
