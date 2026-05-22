using Module.Business.Services.BusinessOperations;
using Shared.Abstractions;
using Shared.Abstractions.Attributes;
using Shared.Abstractions.Enum;
using Shared.Models.Communication;
using Shared.Models.Log;
using System.Threading;

namespace TestProject;

public class BusinessOperationCatalogTests
{
    [SetUp]
    public void SetUp()
    {
        BusinessOperationCatalog.Refresh();
    }

    [Test]
    public void GetOperations_ForSystem_ReturnsAttributedMethods()
    {
        BusinessOperationDescriptor? operation = BusinessOperationCatalog
            .GetOperations("System")
            .FirstOrDefault(item => item.OperationId == "StringtoHex");

        Assert.That(operation, Is.Not.Null);
        Assert.That(operation!.Parameters, Has.Count.EqualTo(1));
        Assert.That(operation.Parameters[0].Name, Is.EqualTo("input"));
        Assert.That(operation.Parameters[0].TypeName, Is.EqualTo("string"));
    }

    [Test]
    public async Task InvokeAsync_ForSystemMethod_ConvertsAndReturnsValue()
    {
        BusinessOperationInvocationResult result = await BusinessOperationInvoker.InvokeAsync(
            "System",
            "StringtoHex",
            new Dictionary<string, string>
            {
                ["input"] = "A"
            });

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Result, Is.EqualTo("41"));
    }

    [Test]
    public void GetOperations_ForCommunicationTypeBusiness_HidesRuntimeCommunicationParameter()
    {
        BusinessOperationDescriptor? operation = BusinessOperationCatalog
            .GetOperations(CommuniactionType.TCPClient.ToString())
            .FirstOrDefault(item => item.OperationId == "GetCurrentCommunicationName");

        Assert.That(operation, Is.Not.Null);
        Assert.That(operation!.Parameters, Is.Empty);
    }

    [Test]
    public async Task InvokeAsync_ForCommunicationTypeBusiness_InjectsCurrentCommunication()
    {
        FakeCommunication communication = new("TCP客户端-01");

        BusinessOperationInvocationResult result = await BusinessOperationInvoker.InvokeAsync(
            CommuniactionType.TCPClient.ToString(),
            "GetCurrentCommunicationName",
            new Dictionary<string, string>(),
            communication,
            CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Result, Is.EqualTo("TCP客户端-01"));
    }

    [DeviceBusiness(CommuniactionType.TCPClient, "TCP客户端业务")]
    private static class TcpClientBusiness
    {
        [BusinessOperation("GetCurrentCommunicationName", "获取当前通信名称")]
        public static string GetCurrentCommunicationName(ICommunication communication)
        {
            return communication.LocalName;
        }
    }

    private sealed class FakeCommunication : ICommunication
    {
        public FakeCommunication(string localName)
        {
            LocalName = localName;
        }

        public event ReceiveData? OnReceive;

        public event StateChanged? StateChange;

        public event Action<LogMessageModel>? OnLog;

        public ConnectState IsConnected => ConnectState.Connected;

        public string LocalName { get; }

        public bool Start()
        {
            return true;
        }

        public bool Send(ref SendReceiveModel readWriteModel, bool isWait = false)
        {
            _ = isWait;
            return true;
        }

        public Task<bool> SendAsync(SendReceiveModel readWriteModel)
        {
            return Task.FromResult(true);
        }

        public bool Receive(ref SendReceiveModel readWriteModel)
        {
            return true;
        }

        public bool Close()
        {
            return true;
        }
    }
}
