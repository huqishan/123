namespace Module.Communication.Features.DeviceCommunicationConfig.ViewModels.PresentationModels
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
}
