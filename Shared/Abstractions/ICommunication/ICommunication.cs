using Shared.Abstractions.Enum;
using Shared.Models.Communication;
using System.Threading.Tasks;

namespace Shared.Abstractions.ICommunication
{
    public delegate void StateChanged(ConnectState connectState, string localName);
    public delegate string ReceiveData(object message, params object[] param);

    /// <summary>
    /// 报文收发能力。
    /// </summary>
    public interface ICommunication
    {
        bool Send(ref SendReceiveModel readWriteModel, bool isWait = false);

        Task<bool> SendAsync(SendReceiveModel readWriteModel);

        bool Receive(ref SendReceiveModel readWriteModel);
    }
}
