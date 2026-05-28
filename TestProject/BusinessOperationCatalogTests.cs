using Module.Business.Services.BusinessOperations;
using Shared.Abstractions.Attributes;
using Shared.Abstractions.Enum;
using Shared.Abstractions.ICommunication;
using Shared.Infrastructure.Communication;
using Shared.Models.Communication;
using System.IO;
using System.Reflection;
using System.Threading;

namespace TestProject;

public class BusinessOperationCatalogTests
{
    [SetUp]
    public void SetUp()
    {
        BusinessOperationCatalog.Refresh();
        CommunicationAdapterRegistry.Default.RegisterFromAssembly(typeof(FakeCommunication).Assembly);
        BusinessOperationInvoker.ConfigureServiceResolver(_ => null);
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
            .FirstOrDefault(item => item.OperationId == "SendCurrentCommunication");

        Assert.That(operation, Is.Not.Null);
        Assert.That(operation!.Parameters, Is.Empty);
    }

    [Test]
    public async Task InvokeAsync_ForCommunicationTypeBusiness_InjectsCurrentCommunication()
    {
        const string localName = "TCP客户端01";
        FakeRuntimeConfig config = new(localName, CommuniactionType.TCPClient);

        try
        {
            CommunicationFactory.CreateCommunicationProtocol(config);

            BusinessOperationInvocationResult result = await BusinessOperationInvoker.InvokeAsync(
                CommuniactionType.TCPClient.ToString(),
                "SendCurrentCommunication",
                new Dictionary<string, string>(),
                localName,
                CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Result, Is.EqualTo(localName));
        }
        finally
        {
            CommunicationFactory.Remove(localName);
        }
    }

    [Test]
    public void GetOperations_ForPlcBusiness_HidesRuntimePlcParameter()
    {
        BusinessOperationDescriptor? operation = BusinessOperationCatalog
            .GetOperations(CommuniactionType.PLC.ToString())
            .FirstOrDefault(item => item.OperationId == "ReadCurrentPlcValue");

        Assert.That(operation, Is.Not.Null);
        Assert.That(operation!.Parameters, Is.Empty);
    }

    [Test]
    public async Task InvokeAsync_ForPlcBusiness_InjectsCurrentPlcCommunication()
    {
        const string localName = "PLC-01";
        FakeRuntimeConfig config = new(localName, CommuniactionType.PLC, "123");

        try
        {
            CommunicationFactory.CreateCommunicationProtocol(config);

            BusinessOperationInvocationResult result = await BusinessOperationInvoker.InvokeAsync(
                CommuniactionType.PLC.ToString(),
                "ReadCurrentPlcValue",
                new Dictionary<string, string>(),
                localName,
                CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Result, Is.EqualTo("123"));
        }
        finally
        {
            CommunicationFactory.Remove(localName);
        }
    }

    [DeviceBusiness(CommuniactionType.TCPClient, "TCP客户端业务")]
    private static class TcpClientBusiness
    {
        [BusinessOperation("SendCurrentCommunication", "发送当前通信报文")]
        public static string SendCurrentCommunication(ICommunication communication)
        {
            SendReceiveModel model = new("PING");
            return communication.Send(ref model) ? model.Result?.ToString() ?? string.Empty : string.Empty;
        }
    }

    [DeviceBusiness(CommuniactionType.PLC, "PLC业务")]
    private static class PlcBusiness
    {
        [BusinessOperation("ReadCurrentPlcValue", "读取当前 PLC 值")]
        public static string ReadCurrentPlcValue(IPlcCommunication plcCommunication)
        {
            return plcCommunication.Read("D100", 1).Message;
        }
    }

    [Test]
    public void ResolveCatalogDeviceId_ForCurrentCommunicationTypeId_MapsToRuntimeBusinessDevice()
    {
        const string localName = "type-id-plc-device";
        string directory = Path.Combine(AppContext.BaseDirectory, "Config", "Communication");
        string filePath = Path.Combine(directory, $"{Guid.NewGuid():N}.json");
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            filePath,
            """
            {
              "LocalName": "type-id-plc-device",
              "TypeId": "plc-s7",
              "Config": {}
            }
            """);

        try
        {
            Type resolverType = typeof(BusinessOperationCatalog).Assembly.GetType(
                "Module.Business.Services.BusinessOperations.BusinessOperationBindingResolver",
                throwOnError: true)!;
            MethodInfo resolveMethod = resolverType.GetMethod(
                "ResolveCatalogDeviceId",
                BindingFlags.Public | BindingFlags.Static)!;

            string deviceId = (string)resolveMethod.Invoke(null, new object?[] { localName, null })!;

            Assert.That(deviceId, Is.EqualTo(CommuniactionType.PLC.ToString()));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    public sealed record FakeRuntimeConfig(
        string LocalName,
        CommuniactionType Type,
        string PlcValue = "") : ICommunicationRuntimeConfig;

    [CommunicationAdapter(typeof(FakeRuntimeConfig))]
    public sealed class FakeCommunication : CommunicationBase, ICommunication, IPlcCommunication
    {
        private readonly string _plcValue;

        public FakeCommunication(FakeRuntimeConfig config)
        {
            LocalName = config.LocalName;
            _plcValue = config.PlcValue;
        }

        public override bool Start()
        {
            return true;
        }

        public bool Send(ref SendReceiveModel readWriteModel, bool isWait = false)
        {
            _ = isWait;
            readWriteModel.Result = LocalName;
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

        public override bool Close()
        {
            return true;
        }

        public PlcReadResult Read(string address, int length, DataType dataType = DataType.Decimal)
        {
            return PlcReadResult.Create(true, address, length, dataType, _plcValue);
        }

        public Task<PlcReadResult> ReadAsync(
            string address,
            int length,
            DataType dataType = DataType.Decimal,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Read(address, length, dataType));
        }

        public PlcWriteResult Write(string address, string value, DataType dataType = DataType.Decimal)
        {
            return PlcWriteResult.Create(true, address, value, dataType, "OK");
        }

        public Task<PlcWriteResult> WriteAsync(
            string address,
            string value,
            DataType dataType = DataType.Decimal,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Write(address, value, dataType));
        }
    }
}
