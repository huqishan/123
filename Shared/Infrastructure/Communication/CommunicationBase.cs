using Shared.Abstractions.Enum;
using Shared.Abstractions.ICommunication;
using Shared.Models.Log;
using System;
using System.Threading.Tasks;

namespace Shared.Infrastructure.Communication;

public abstract class CommunicationBase
{
    private ConnectState _isConnected = ConnectState.DisConnected;

    protected CommunicationBase()
    {
    }

    protected CommunicationBase(string localName)
    {
        LocalName = localName?.Trim() ?? string.Empty;
    }

    public event ReceiveData OnReceive = (_, _) => string.Empty;

    public event StateChanged StateChange = delegate { };

    public event Action<LogMessageModel> OnLog = delegate { };

    public ConnectState IsConnected
    {
        get => _isConnected;
        protected set
        {
            if (_isConnected == value)
            {
                return;
            }

            _isConnected = value;
            Task.Run(() => StateChange(value, LocalName));
        }
    }

    public string LocalName { get; protected set; } = string.Empty;

    public abstract bool Start();

    public abstract bool Close();

    protected string RaiseReceive(object message, params object[] param)
    {
        return OnReceive(message, param);
    }

    protected void WriteLog(LogMessageModel message)
    {
        Task.Run(() => OnLog(message));
    }

    protected void WriteLog(string message, LogType type)
    {
        WriteLog(new LogMessageModel { Message = message, Type = type });
    }
}
