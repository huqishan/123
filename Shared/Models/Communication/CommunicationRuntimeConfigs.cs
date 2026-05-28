using Shared.Abstractions.Enum;
using System;

namespace Shared.Models.Communication;

#region 运行时配置契约

/// <summary>
/// 通信运行时配置契约，用于将界面配置转换为通信工厂可创建的强类型配置。
/// </summary>
public interface ICommunicationRuntimeConfig
{
    /// <summary>
    /// 通信实例本地名称，也是运行时通信实例仓库的唯一键。
    /// </summary>
    string LocalName { get; }

    /// <summary>
    /// 通信实例所属的运行时通信类型。
    /// </summary>
    CommuniactionType Type { get; }
}

#endregion

#region PLC 公共选项

/// <summary>
/// Siemens S7 CPU 类型名称。
/// </summary>
public static class S7CpuTypeNames
{
    /// <summary>
    /// S7-200 CPU 类型。
    /// </summary>
    public const string S7200 = "S7200";

    /// <summary>
    /// S7-300 CPU 类型。
    /// </summary>
    public const string S7300 = "S7300";

    /// <summary>
    /// S7-400 CPU 类型。
    /// </summary>
    public const string S7400 = "S7400";

    /// <summary>
    /// S7-1200 CPU 类型。
    /// </summary>
    public const string S71200 = "S71200";

    /// <summary>
    /// S7-1500 CPU 类型。
    /// </summary>
    public const string S71500 = "S71500";

    /// <summary>
    /// 规范化 S7 CPU 类型名称。
    /// </summary>
    /// <param name="value">原始 CPU 类型名称。</param>
    /// <returns>已识别的 CPU 类型名称；未知时返回 S7-1200。</returns>
    public static string Normalize(string? value)
    {
        return value?.Trim() switch
        {
            var cpu when string.Equals(cpu, S7200, StringComparison.OrdinalIgnoreCase) => S7200,
            var cpu when string.Equals(cpu, S7300, StringComparison.OrdinalIgnoreCase) => S7300,
            var cpu when string.Equals(cpu, S7400, StringComparison.OrdinalIgnoreCase) => S7400,
            var cpu when string.Equals(cpu, S71500, StringComparison.OrdinalIgnoreCase) => S71500,
            _ => S71200
        };
    }
}

#endregion

#region Socket 运行时配置

/// <summary>
/// TCP 客户端运行时配置。
/// </summary>
/// <param name="LocalName">通信实例本地名称。</param>
/// <param name="RemoteIpAddress">远端 IP 地址。</param>
/// <param name="RemotePort">远端端口。</param>
/// <param name="LocalIpAddress">本地 IP 地址。</param>
/// <param name="LocalPort">本地端口，0 表示由系统分配。</param>
public sealed record TcpClientRuntimeConfig(
    string LocalName,
    string RemoteIpAddress,
    int RemotePort,
    string LocalIpAddress,
    int LocalPort) : ICommunicationRuntimeConfig
{
    /// <summary>
    /// TCP 客户端通信类型。
    /// </summary>
    public CommuniactionType Type => CommuniactionType.TCPClient;
}

/// <summary>
/// TCP 服务端运行时配置。
/// </summary>
/// <param name="LocalName">通信实例本地名称。</param>
/// <param name="LocalIpAddress">本地监听 IP 地址。</param>
/// <param name="LocalPort">本地监听端口。</param>
public sealed record TcpServerRuntimeConfig(
    string LocalName,
    string LocalIpAddress,
    int LocalPort) : ICommunicationRuntimeConfig
{
    /// <summary>
    /// TCP 服务端通信类型。
    /// </summary>
    public CommuniactionType Type => CommuniactionType.TCPServer;
}

/// <summary>
/// UDP 客户端运行时配置。
/// </summary>
/// <param name="LocalName">通信实例本地名称。</param>
/// <param name="RemoteIpAddress">远端 IP 地址。</param>
/// <param name="RemotePort">远端端口。</param>
/// <param name="LocalIpAddress">本地 IP 地址。</param>
/// <param name="LocalPort">本地端口，0 表示由系统分配。</param>
public sealed record UdpClientRuntimeConfig(
    string LocalName,
    string RemoteIpAddress,
    int RemotePort,
    string LocalIpAddress,
    int LocalPort) : ICommunicationRuntimeConfig
{
    /// <summary>
    /// UDP 通信类型。
    /// </summary>
    public CommuniactionType Type => CommuniactionType.UDP;
}

/// <summary>
/// UDP 服务端运行时配置。
/// </summary>
/// <param name="LocalName">通信实例本地名称。</param>
/// <param name="LocalIpAddress">本地监听 IP 地址。</param>
/// <param name="LocalPort">本地监听端口。</param>
public sealed record UdpServerRuntimeConfig(
    string LocalName,
    string LocalIpAddress,
    int LocalPort) : ICommunicationRuntimeConfig
{
    /// <summary>
    /// UDP 服务端通信类型。
    /// </summary>
    public CommuniactionType Type => CommuniactionType.UDPServer;
}

#endregion

#region 串口运行时配置

/// <summary>
/// 串口运行时配置。
/// </summary>
/// <param name="LocalName">通信实例本地名称。</param>
/// <param name="PortName">串口名称。</param>
/// <param name="BaudRate">波特率。</param>
/// <param name="Parity">校验位。</param>
/// <param name="DataBits">数据位。</param>
/// <param name="StopBits">停止位。</param>
public sealed record SerialPortRuntimeConfig(
    string LocalName,
    string PortName,
    int BaudRate,
    int Parity,
    int DataBits,
    int StopBits) : ICommunicationRuntimeConfig
{
    /// <summary>
    /// 串口通信类型。
    /// </summary>
    public CommuniactionType Type => CommuniactionType.COM;
}

#endregion

#region PLC 运行时配置

/// <summary>
/// 三菱 MX PLC 运行时配置。
/// </summary>
/// <param name="LocalName">通信实例本地名称。</param>
/// <param name="StationNumber">PLC 逻辑站号。</param>
/// <param name="Password">PLC 访问密码。</param>
public sealed record MxPlcRuntimeConfig(
    string LocalName,
    int StationNumber,
    string? Password) : ICommunicationRuntimeConfig
{
    /// <summary>
    /// PLC 通信类型。三菱 MX 是 PLC 协议的一种，不再作为运行时大类。
    /// </summary>
    public CommuniactionType Type => CommuniactionType.PLC;
}

/// <summary>
/// Modbus TCP PLC 运行时配置。
/// </summary>
/// <param name="LocalName">通信实例本地名称。</param>
/// <param name="RemoteIpAddress">远端 IP 地址。</param>
/// <param name="RemotePort">远端端口。</param>
/// <param name="LocalIpAddress">本地 IP 地址。</param>
/// <param name="LocalPort">本地端口，0 表示由系统分配。</param>
public sealed record ModbusTcpPlcRuntimeConfig(
    string LocalName,
    string RemoteIpAddress,
    int RemotePort,
    string LocalIpAddress,
    int LocalPort) : ICommunicationRuntimeConfig
{
    /// <summary>
    /// PLC 通信类型。
    /// </summary>
    public CommuniactionType Type => CommuniactionType.PLC;
}

/// <summary>
/// Siemens S7 PLC 运行时配置。
/// </summary>
/// <param name="LocalName">通信实例本地名称。</param>
/// <param name="RemoteIpAddress">远端 IP 地址。</param>
/// <param name="CpuType">S7 CPU 类型。</param>
/// <param name="Rack">PLC Rack 编号。</param>
/// <param name="Slot">PLC Slot 编号。</param>
public sealed record S7PlcRuntimeConfig(
    string LocalName,
    string RemoteIpAddress,
    string CpuType,
    int Rack,
    int Slot) : ICommunicationRuntimeConfig
{
    /// <summary>
    /// PLC 通信类型。
    /// </summary>
    public CommuniactionType Type => CommuniactionType.PLC;
}

#endregion

#region RabbitMQ 运行时配置

/// <summary>
/// RabbitMQ RPC 服务端运行时配置。
/// </summary>
/// <param name="LocalName">通信实例本地名称。</param>
/// <param name="RemoteIpAddress">RabbitMQ 主机地址。</param>
/// <param name="RemotePort">RabbitMQ 端口。</param>
/// <param name="UserName">用户名。</param>
/// <param name="Password">密码。</param>
public sealed record RabbitMqRpcServerRuntimeConfig(
    string LocalName,
    string RemoteIpAddress,
    int RemotePort,
    string UserName,
    string Password) : ICommunicationRuntimeConfig
{
    /// <summary>
    /// RabbitMQ RPC 服务端通信类型。
    /// </summary>
    public CommuniactionType Type => CommuniactionType.RabbitMQRPCServer;
}

/// <summary>
/// RabbitMQ RPC 客户端运行时配置。
/// </summary>
/// <param name="LocalName">通信实例本地名称。</param>
/// <param name="RemoteIpAddress">RabbitMQ 主机地址。</param>
/// <param name="RemotePort">RabbitMQ 端口。</param>
/// <param name="UserName">用户名。</param>
/// <param name="Password">密码。</param>
public sealed record RabbitMqRpcClientRuntimeConfig(
    string LocalName,
    string RemoteIpAddress,
    int RemotePort,
    string UserName,
    string Password) : ICommunicationRuntimeConfig
{
    /// <summary>
    /// RabbitMQ RPC 客户端通信类型。
    /// </summary>
    public CommuniactionType Type => CommuniactionType.RabbitMQRPCClient;
}

#endregion
