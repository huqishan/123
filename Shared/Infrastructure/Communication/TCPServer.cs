using HPSocket.Tcp;
using HPSocket;
using Shared.Abstractions.Enum;
using Shared.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Shared.Models.Communication;
using Shared.Models.Log;

namespace Shared.Infrastructure.Communication
{
    public class TCPServer : ICommunication
    {
        #region Propertys
        /// <summary>
        /// 服务器IP
        /// </summary>
        public string LocalAddress { get; private set; } = "127.0.0.1";
        /// <summary>
        /// 服务器端口
        /// </summary>
        public ushort LocalPort { get; private set; } = 5555;
        /// <summary>
        /// Tcp服务器
        /// </summary>
        public ITcpServer TcpServer { get; private set; } = new TcpServer();
        public string LocalName { get; private set; } = null;
        private ConnectState _IsConnected = ConnectState.DisConnected;
        public ConnectState IsConnected
        {
            get
            {
                if (TcpServer == null)
                    return ConnectState.DisConnected;
                else
                    return _IsConnected;
            }
            private set
            {
                if (_IsConnected != value)
                {
                    _IsConnected = value;
                    SendState(value);
                }
            }
        }
        private Dictionary<string, IntPtr> keyValuePairs = new Dictionary<string, IntPtr>();
        #endregion

        public TCPServer(CommuniactionConfigModel config)
        {
            LocalAddress = config.LocalIPAddress;
            LocalPort = Convert.ToUInt16(config.LocalPort);
            LocalName = config.LocalName;
            EventInitial();
        }

        #region 方法

        /// <summary>
        /// 开启服务器
        /// </summary>
        /// <returns></returns>
        public bool Start()
        {
            bool result = false;
            try
            {
                if (CheckIpAddressAndPort(LocalAddress, LocalPort.ToString()))//Check IP 及Port是否正确
                {
                    TcpServer.Address = LocalAddress;
                    TcpServer.Port = LocalPort;
                    result = TcpServer.Start();
                    IsConnected = result ? ConnectState.Connected : ConnectState.DisConnected;
                }
                else
                {
                    WriteLog(new LogMessageModel { Message = $"{LocalName} TCP Address or Port Error({LocalAddress}:{LocalPort})", Type = LogType.ERROR });
                }
            }
            catch (Exception ex)
            {
                WriteLog(new LogMessageModel { Message = $"{LocalName} TCP Start Exception:{ex.Message}", Type = LogType.ERROR });
                result = false;
            }
            return result;
        }

        /// <summary>
        /// 关闭服务器
        /// </summary>
        /// <returns></returns>
        public bool Close()
        {
            bool result = false;
            try
            {
                result = TcpServer.Stop();
                IsConnected = result ? ConnectState.Connected : ConnectState.DisConnected;
            }
            catch (Exception ex)
            {
                WriteLog(new LogMessageModel { Message = $"{LocalName} TCP Close Exception:{ex.Message}", Type = LogType.WARN });
                IsConnected = ConnectState.DisConnected;
            }
            return result;
        }

        public bool Read(ref ReadWriteModel readWriteModel)
        {
            return true;
        }
        /// <summary>
        /// 发送
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="receiveObj">接收ID</param>
        /// <returns></returns>
        public bool Write(ref ReadWriteModel readWriteModel, bool isWait = false)
        {
            bool result = true;
            IntPtr client = (IntPtr)0;
            if (readWriteModel.ClientId != null && readWriteModel.ClientId.ToString().Contains(":") && keyValuePairs.ContainsKey(readWriteModel.ClientId.ToString()))
            {
                client = keyValuePairs[readWriteModel.ClientId.ToString()];
            }
            else if (readWriteModel.ClientId is IntPtr)
            {
                client = (IntPtr)readWriteModel.ClientId;
            }
            if (readWriteModel.ClientId == null || client == (IntPtr)0)
            {
                result = false;
                readWriteModel.Result = $"{LocalName} 客户端错误：{readWriteModel.Message}";
                WriteLog(new LogMessageModel { Message = $"{LocalName} 客户端错误：{readWriteModel.Message}", Type = LogType.ERROR });
            }
            else
            {
                byte[] data = Encoding.UTF8.GetBytes(readWriteModel.Message);
                result = TcpServer.Send(client, data, data.Length);
            }
            return result;
        }
        /// <summary>
        /// 发送
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="receiveObj">接收ID</param>
        /// <returns></returns>
        public Task<bool> WriteAsync(ReadWriteModel readWriteModel)
        {
            return Task.Run(() => { return Write(ref readWriteModel); });
        }

        /// <summary>
        /// Check IP 及Port是否正确
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        private static bool CheckIpAddressAndPort(string ip, string port)
        {
            bool result = false;
            try
            {
                if (Regex.IsMatch(ip + ":" + port, @"^((2[0-4]\d|25[0-5]|[1]?\d\d?)\.){3}(2[0-4]\d|25[0-5]|[1]?\d\d?)\:([1-9]|[1-9][0-9]|[1-9][0-9][0-9]|[1-9][0-9][0-9][0-9]|[1-6][0-5][0-5][0-3][0-5])$"))
                {
                    result = true;
                }
            }
            catch (Exception)
            {
                result = false;
            }
            return result;
        }

        #endregion

        #region 事件
        public event Action<LogMessageModel> OnLog;
        public event ReceiveData OnReceive;
        public event StateChanged StateChange;
        private void WriteLog(LogMessageModel message)
        {
            Task.Run(() => { OnLog?.Invoke(message); });
        }
        private void SendState(ConnectState connectState)
        {
            Task.Run(() => { StateChange?.Invoke(connectState, LocalName); });
        }
        /// <summary>
        /// 事件初始化
        /// </summary>
        private void EventInitial()
        {
            TcpServer.OnPrepareListen -= TcpServer_OnPrepareListen;//服务器启动事件
            TcpServer.OnPrepareListen += TcpServer_OnPrepareListen;//服务器启动事件

            TcpServer.OnAccept -= TcpServer_OnAccept;//客户端连接成功事件
            TcpServer.OnAccept += TcpServer_OnAccept;//客户端连接成功事件

            TcpServer.OnSend -= TcpServer_OnSend;//发送数据事件
            TcpServer.OnSend += TcpServer_OnSend;//发送数据事件

            TcpServer.OnReceive -= TcpServer_OnReceive;//接收数据事件
            TcpServer.OnReceive += TcpServer_OnReceive;//接收数据事件

            TcpServer.OnClose -= TcpServer_OnClose;//客户端断开事件
            TcpServer.OnClose += TcpServer_OnClose;//客户端断开事件

            TcpServer.OnShutdown -= TcpServer_OnShutdown;//服务器停止事件
            TcpServer.OnShutdown += TcpServer_OnShutdown;//服务器停止事件
        }
        #region 服务器启动事件
        /// <summary>
        /// 服务器启动
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="listen"></param>
        /// <returns></returns>
        public HandleResult TcpServer_OnPrepareListen(HPSocket.IServer sender, IntPtr listen)
        {
            WriteLog(new LogMessageModel { Message = $"服务器 {LocalName} ({sender.Address}:{sender.Port}) 启动 成功！", Type = LogType.INFO });
            return HandleResult.Ok;
        }
        #endregion

        #region 客户端连接成功事件
        /// <summary>
        /// 客户端连接成功
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="connId"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        public HandleResult TcpServer_OnAccept(HPSocket.IServer sender, IntPtr connId, IntPtr client)
        {
            if (!sender.GetRemoteAddress(connId, out var ip, out var port))
            {
                WriteLog(new LogMessageModel { Message = $"{LocalName} Get TcpClient ConnId Fail！", Type = LogType.ERROR });
                return HandleResult.Error;
            }
            if (keyValuePairs.ContainsKey($"{ip}:{port}"))
                keyValuePairs[$"{ip}:{port}"] = connId;
            else
                keyValuePairs.Add($"{ip}:{port}", connId);
            WriteLog(new LogMessageModel { Message = $"{LocalName} TcpClient({ip}:{port}) 已连接！", Type = LogType.INFO });
            return HandleResult.Ok;
        }
        #endregion

        #region 客户端断开事件
        /// <summary>
        /// 客户端断开事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="connId"></param>
        /// <param name="socketOperation"></param>
        /// <param name="errorCode"></param>
        /// <returns></returns>
        public HandleResult TcpServer_OnClose(HPSocket.IServer sender, IntPtr connId, SocketOperation socketOperation, int errorCode)
        {
            if (!sender.GetRemoteAddress(connId, out var ip, out var port))
            {
                return HandleResult.Error;
            }
            keyValuePairs.Remove($"{ip}:{port}");
            WriteLog(new LogMessageModel { Message = $"{LocalName} TcpClient({ip}:{port}) 断开连接！", Type = LogType.WARN });
            return HandleResult.Ok;
        }
        #endregion

        #region   接收数据事件
        /// <summary>
        /// 接收数据事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="connId"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public HandleResult TcpServer_OnReceive(HPSocket.IServer sender, IntPtr connId, byte[] data)
        {
            if (!sender.GetRemoteAddress(connId, out var ip, out var port))
            {
                return HandleResult.Error;
            }
            string command = OnReceiveHandler(data);

            WriteLog(new LogMessageModel { Message = $"TcpClient({ip}:{port})-->{LocalName}:{command}", Type = LogType.INFO });
            OnReceive?.Invoke(command, connId, ip, port);
            return HandleResult.Ok;
        }


        /// <summary>
        ///  接收数据处理
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public virtual string OnReceiveHandler(byte[] data)
        {
            string result = "";
            try
            {
                result = Encoding.UTF8.GetString(data);
            }
            catch (Exception)
            {
            }
            return result;
        }
        #endregion

        #region 发送数据事件
        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="connId"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public HandleResult TcpServer_OnSend(HPSocket.IServer sender, IntPtr connId, byte[] data)
        {
            if (!sender.GetRemoteAddress(connId, out var ip, out var port))
            {
                return HandleResult.Error;
            }
            string command = OnSendHandler(data);
            WriteLog(new LogMessageModel { Message = $"{LocalName}-->TcpClient({ip}:{port}):{command}", Type = LogType.INFO });
            return HandleResult.Ok;
        }

        /// <summary>
        /// 发送数据处理
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public virtual string OnSendHandler(byte[] data)
        {
            string result = "";
            try
            {
                result = Encoding.Default.GetString(data);
            }
            catch (Exception)
            {
            }
            return result;
        }
        #endregion

        #region 服务器停止事件
        /// <summary>
        /// 服务器停止事件
        /// </summary>
        /// <param name="sender"></param>
        /// <returns></returns>
        public HandleResult TcpServer_OnShutdown(HPSocket.IServer sender)
        {
            IsConnected = ConnectState.DisConnected;
            WriteLog(new LogMessageModel { Message = $"服务器{LocalName}({sender.Address}:{sender.Port}) 已关闭！", Type = LogType.WARN });
            return HandleResult.Ok;
        }

        #endregion


        #endregion
    }
}
