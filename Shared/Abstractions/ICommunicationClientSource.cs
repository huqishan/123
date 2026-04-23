using System.Collections.Generic;

namespace Shared.Abstractions
{
    /// <summary>
    /// 单个已连接客户端的只读快照，供界面展示和定向发送使用。
    /// </summary>
    /// <param name="ClientId">通信层内部使用的客户端唯一标识。</param>
    /// <param name="DisplayName">界面展示用的客户端文本。</param>
    /// <param name="Address">远端 IP 地址。</param>
    /// <param name="Port">远端端口。</param>
    public sealed record CommunicationClientInfo(string ClientId, string DisplayName, string Address, int Port);

    /// <summary>
    /// 已连接客户端列表变化事件。
    /// </summary>
    /// <param name="clients">当前客户端快照。</param>
    public delegate void CommunicationClientsChanged(IReadOnlyList<CommunicationClientInfo> clients);

    /// <summary>
    /// 为服务端通信对象提供当前连接客户端列表。
    /// </summary>
    public interface ICommunicationClientSource
    {
        /// <summary>
        /// 客户端连接列表变化时触发。
        /// </summary>
        event CommunicationClientsChanged ClientsChanged;

        /// <summary>
        /// 获取当前已连接客户端列表快照。
        /// </summary>
        /// <returns>当前客户端集合。</returns>
        IReadOnlyList<CommunicationClientInfo> GetConnectedClients();
    }
}
