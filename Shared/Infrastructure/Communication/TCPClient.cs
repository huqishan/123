using Shared.Abstractions.Enum;
using Shared.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HPSocket;
using Shared.Infrastructure.Extensions;
using Shared.Models.Communication;
using Shared.Models.Log;

namespace Shared.Infrastructure.Communication
{
    public class TCPClient : ICommunication
    {
        #region Propertys
        /// <summary>
        /// 远程服务器IP 地址
        /// </summary>
        public string RemoteAddress { get; private set; } = "127.0.0.1";

        /// <summary>
        /// 远程服务器端口
        /// </summary>
        public ushort RemotePort { get; private set; } = 5555;

        /// <summary>
        /// TCP 客户端
        /// </summary>
        public ITcpClient TcpClient { get; private set; } = new HPSocket.Tcp.TcpClient();

        /// <summary>
        /// TCP 客户端名字
        /// </summary>
        public string LocalClientName { get; private set; } = string.Empty;
        private Thread _ReconnectionThread;
        private AutoResetEvent IsWhile = new AutoResetEvent(false);
        private BlockingCollection<string> _RespQueue = new BlockingCollection<string>();
        private ConnectState _IsConnected = ConnectState.DisConnected;
        bool isHex = false;
        /// <summary>
        /// TCP 客户端连接状态
        /// </summary>  
        public ConnectState IsConnected
        {
            get
            {
                if (TcpClient == null)
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

        public string LocalName { get; }
        object _Locker = new object();
        #endregion

        #region 构造
        public TCPClient(CommuniactionConfigModel config)
        {
            LocalName = config.LocalName;
            if (!string.IsNullOrEmpty(config.LocalIPAddress))
                TcpClient.BindAddress = config.LocalIPAddress;
            if (config.LocalPort > 0)
                TcpClient.BindPort = Convert.ToUInt16(config.LocalPort);
            RemoteAddress = config.RemoteIPAddress;
            RemotePort = Convert.ToUInt16(config.RemotePort);
            LocalClientName = config.LocalName;
            EventInitial();
        }
        #endregion

        #region 方法
        /// <summary>
        /// 连接服务器
        /// </summary>
        /// <returns></returns>
        public bool Start()
        {

            bool result = false;
            try
            {
                TcpClient.KeepAliveInterval = 500;
                TcpClient.KeepAliveTime = 500;
                TcpClient.SocketBufferSize = 1024 * 1024 * 5;
                
                if (CheckIpAddressAndPort(RemoteAddress, RemotePort.ToString()))//Check IP 及Port是否正确
                {
                    IsWhile.Reset();
                    result = TcpClient.Connect(RemoteAddress, RemotePort);
                    _ReconnectionThread?.Abort();
                    _ReconnectionThread = new Thread(() =>
                    {
                        while (true)
                        {
                            if (!TcpClient.IsConnected) TcpClient.Connect(RemoteAddress, RemotePort);
                            if (IsWhile.WaitOne(500))
                                break;
                        }
                    })
                    { IsBackground = true };
                    _ReconnectionThread.Start();
                }
                else
                {
                    WriteLog(new LogMessageModel { Message = $"{LocalClientName} TCP Address or Port Error({RemoteAddress}:{RemotePort})", Type = LogType.ERROR });
                }
            }
            catch (Exception ex)
            {
                WriteLog(new LogMessageModel { Message = $"{LocalClientName} TCP Connect Exception:{ex.Message}", Type = LogType.ERROR });
                result = false;
            }
            return result;
        }
        /// <summary>
        /// 断开服务器
        /// </summary>
        /// <returns></returns>
        public bool Close()
        {
            bool result = false;
            try
            {
                if (TcpClient != null)
                {
                    IsWhile.Set();
                    if (_IsConnected == ConnectState.Connected)
                    {
                        result = TcpClient.Stop();
                        TcpClient.Dispose();
                        GC.Collect();
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog(new LogMessageModel { Message = $"{LocalClientName} TCP Stop Exception:{ex.Message}", Type = LogType.ERROR });
            }
            return result;
        }
        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="message">信息</param>
        /// <param name="ReceiveId">接收方对象</param>
        /// <returns></returns>
        public bool Write(ref ReadWriteModel readWriteModel, bool isWait = false)
        {
            int count = -1;
        Connect:
            if (!TcpClient.IsConnected)
            {
                bool connect = TcpClient.Connect();
                count++;
                if (!connect && count < 10)
                {
                    Thread.Sleep(1000);
                    goto Connect;
                }
            }
            bool result = true;
            if (!string.IsNullOrEmpty(readWriteModel.Message) && TcpClient != null && TcpClient.IsConnected)
            {
                lock (_Locker)
                {
                    ClearQueue();
                    byte[] data = new byte[1];
                    if (readWriteModel.Message.Contains("0x"))
                    {
                        isHex = true;
                        data = readWriteModel.Message.Replace("0x", "").HexStringToByteArray();
                    }
                    else
                    {
                        isHex = false;
                        data = Encoding.UTF8.GetBytes(readWriteModel.Message);
                    }

                    result = TcpClient.Send(data, 0, data.Length);
                    if (isWait)
                    {
                        for (int i = 0; i < 50; i++)
                        {
                            Thread.Sleep(100);
                            if (_RespQueue.Count > 0)
                            {
                                readWriteModel.Result = _RespQueue.Take();
                                return result;
                            }
                        }
                        readWriteModel.Result = $"{LocalClientName},TcpClientIsConnect:{TcpClient.IsConnected} 接受数据超时！！！";
                        WriteLog(new LogMessageModel { Message = $"{LocalClientName},TcpClientIsConnect:{TcpClient.IsConnected} 接受数据超时！！！", Type = LogType.INFO });
                        result = false;
                    }
                }
            }
            else
            {
                readWriteModel.Result = $"{LocalClientName} TCP Connect Exception Command:{readWriteModel.Message},TcpClient:{TcpClient == null},TcpClientIsConnect:{TcpClient.IsConnected} ";
                WriteLog(new LogMessageModel { Message = $"{LocalClientName} TCP Connect Exception Command:{readWriteModel.Message},TcpClient:{TcpClient == null},TcpClientIsConnect:{TcpClient.IsConnected} ", Type = LogType.ERROR });
                result = false;
                //发送失败
            }
            return result;
        }
        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="message">信息</param>
        /// <param name="ReceiveId">接收方对象</param>
        public Task<bool> WriteAsync(ReadWriteModel readWriteModel)
        {
            return Task.Run(() => { return Write(ref readWriteModel); });
        }
        public bool Read(ref ReadWriteModel readWriteModel)
        {
            return true;
        }
        /// <summary>
        /// Check IP 及Port是否正确
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public static bool CheckIpAddressAndPort(string ip, string port)
        {
            bool result = false;
            try
            {
                result = true;
                //if (Regex.IsMatch(ip + ":" + port, @"^((2[0-4]\d|25[0-5]|[1]?\d\d?)\.){3}(2[0-4]\d|25[0-5]|[1]?\d\d?)\:([1-9]|[1-9][0-9]|[1-9][0-9][0-9]|[1-9][0-9][0-9][0-9]|[1-6][0-5][0-5][0-3][0-5])$"))
                //{

                //}
            }
            catch (Exception)
            {
                result = false;
            }
            return result;
        }
        private void ClearQueue()
        {
            int count = _RespQueue.Count;
            for (int i = 0; i < count; i++)
            {
                _RespQueue.Take();
            }
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
            TcpClient.OnClose -= TcpClient_OnClose;//客户端断开事件
            TcpClient.OnClose += TcpClient_OnClose;//客户端断开事件
            TcpClient.OnConnect -= TcpClient_OnConnect;//客户端已连接事件
            TcpClient.OnConnect += TcpClient_OnConnect;//客户端已连接事件
            TcpClient.OnPrepareConnect -= TcpClient_OnPrepareConnect;//客户端正在连接事件
            TcpClient.OnPrepareConnect += TcpClient_OnPrepareConnect;//客户端正在连接事件
            TcpClient.OnReceive -= TcpClient_OnReceive;//接收服务器发送的数据事件
            TcpClient.OnReceive += TcpClient_OnReceive;//接收服务器发送的数据事件
            TcpClient.OnSend -= TcpClient_OnSend;//客户端发送数据事件
            TcpClient.OnSend += TcpClient_OnSend;//客户端发送数据事件
        }

        #region 与服务器断开连接事件
        /// <summary>
        /// 与服务器断开连接事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="socketOperation"></param>
        /// <param name="errorCode"></param>
        /// <returns></returns>
        private HandleResult TcpClient_OnClose(HPSocket.IClient sender, SocketOperation socketOperation, int errorCode)
        {
            WriteLog(new LogMessageModel { Message = $"{LocalClientName} 与服务器({RemoteAddress}:{RemotePort}) 断开连接！", Type = LogType.WARN });
            IsConnected = ConnectState.DisConnected;
            return HandleResult.Ok;
        }
        #endregion

        #region 已经连接服务器事件
        /// <summary>
        /// 已经连接服务器事件
        /// </summary>
        /// <param name="sender"></param>
        /// <returns></returns>
        private HandleResult TcpClient_OnConnect(HPSocket.IClient sender)
        {
            WriteLog(new LogMessageModel { Message = $"{LocalClientName} 连接服务器({RemoteAddress}:{RemotePort}) 成功！", Type = LogType.INFO });
            IsConnected = ConnectState.Connected;
            return HandleResult.Ok;
        }
        #endregion

        #region 正在连接服务器事件
        /// <summary>
        ///正在连接服务器事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="socket"></param>
        /// <returns></returns>
        private HandleResult TcpClient_OnPrepareConnect(IClient sender, IntPtr socket)
        {
            IsConnected = ConnectState.DisConnected;
            WriteLog(new LogMessageModel { Message = $"{LocalClientName} 正在连接服务器({RemoteAddress}:{RemotePort})........", Type = LogType.INFO });
            return HandleResult.Ok;
        }
        #endregion

        #region  接收数据
        /// <summary>
        /// 接收数据
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        private HandleResult TcpClient_OnReceive(HPSocket.IClient sender, byte[] data)
        {
            string[] Commands = OnReceiveHandler(data);

            foreach (var item in Commands)
            {
                WriteLog(new LogMessageModel { Message = $"服务器{RemoteAddress}:{RemotePort}-->{LocalClientName}:{item}", Type = LogType.INFO });
                _RespQueue.Add(item);
                Task.Run(() =>
                {
                    OnReceive?.Invoke(item, sender.ConnectionId, RemoteAddress, RemotePort);
                });
            }
            return HandleResult.Ok;
        }

        public virtual string[] OnReceiveHandler(byte[] data)
        {
            if (isHex)
            {
                return new string[] { BitConverter.ToString(data).Replace("-", "") };
            }
            return new string[] { Encoding.UTF8.GetString(data) };
        }
        #endregion

        #region  发送数据
        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        private HandleResult TcpClient_OnSend(HPSocket.IClient sender, byte[] data)
        {
            string command = OnSendHandler(data);
            WriteLog(new LogMessageModel { Message = $"{LocalClientName}-->服务器({RemoteAddress}:{RemotePort}) : {command}", Type = LogType.INFO });
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
        #endregion
    }
}
