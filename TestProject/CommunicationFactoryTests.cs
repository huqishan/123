using Shared.Abstractions.Enum;
using Shared.Abstractions.ICommunication;
using Shared.Infrastructure.Communication;
using Shared.Models.Communication;

namespace TestProject;

[TestFixture]
public sealed class CommunicationFactoryTests
{
    [Test]
    public void CreateCommunicationProtocol_WhenTypeIsTcpClient_UsesRegisteredAdapterAndStoresByName()
    {
        const string localName = "Factory-Tcp";
        TcpClientRuntimeConfig config = new(
            localName,
            "127.0.0.1",
            502,
            "127.0.0.1",
            0);

        try
        {
            CommunicationBase communication = CommunicationFactory.CreateCommunicationProtocol(config);

            Assert.Multiple(() =>
            {
                Assert.That(communication, Is.TypeOf<TCPClient>());
                Assert.That(communication, Is.AssignableTo<ICommunication>());
                Assert.That(CommunicationFactory.Get("factory-tcp"), Is.SameAs(communication));
            });
        }
        finally
        {
            CommunicationFactory.Remove(localName);
        }
    }

    [Test]
    public void CreateCommunicationProtocol_WhenPlcTypeIsS7_UsesS7Adapter()
    {
        const string localName = "Factory-S7";
        S7PlcRuntimeConfig config = new(
            localName,
            "127.0.0.1",
            S7CpuTypeNames.S71500,
            0,
            1);

        try
        {
            CommunicationBase communication = CommunicationFactory.CreateCommunicationProtocol(config);

            Assert.Multiple(() =>
            {
                Assert.That(communication, Is.TypeOf<S7PlcCommunication>());
                Assert.That(communication, Is.AssignableTo<IPlcCommunication>());
                Assert.That(communication, Is.Not.AssignableTo<ICommunication>());
            });
        }
        finally
        {
            CommunicationFactory.Remove(localName);
        }
    }

    [Test]
    public void CreateCommunicationProtocol_WhenPlcTypeIsMx_UsesMxAdapterAndPlcInterface()
    {
        const string localName = "Factory-Mx";
        MxPlcRuntimeConfig config = new(localName, 1, null);

        try
        {
            CommunicationBase communication = CommunicationFactory.CreateCommunicationProtocol(config);

            Assert.Multiple(() =>
            {
                Assert.That(config.Type, Is.EqualTo(CommuniactionType.PLC));
                Assert.That(communication, Is.TypeOf<MxPlcCommunication>());
                Assert.That(communication, Is.AssignableTo<IPlcCommunication>());
                Assert.That(communication, Is.Not.AssignableTo<ICommunication>());
            });
        }
        finally
        {
            CommunicationFactory.Remove(localName);
        }
    }

    [Test]
    public void CreateCommunicationProtocol_WhenPlcTypeIsModbus_UsesModbusAdapter()
    {
        const string localName = "Factory-Modbus";
        ModbusTcpPlcRuntimeConfig config = new(
            localName,
            "127.0.0.1",
            502,
            "0.0.0.0",
            0);

        try
        {
            CommunicationBase communication = CommunicationFactory.CreateCommunicationProtocol(config);

            Assert.Multiple(() =>
            {
                Assert.That(communication, Is.TypeOf<ModbusTcpPlcCommunication>());
                Assert.That(communication, Is.AssignableTo<IPlcCommunication>());
                Assert.That(communication, Is.Not.AssignableTo<ICommunication>());
            });
        }
        finally
        {
            CommunicationFactory.Remove(localName);
        }
    }

    [Test]
    public void CreateCommunicationProtocol_WhenSameNameIsCreated_ReplacesOldInstance()
    {
        const string localName = "Factory-Replace";
        TcpClientRuntimeConfig tcpConfig = new(
            localName,
            "127.0.0.1",
            502,
            "127.0.0.1",
            0);
        UdpClientRuntimeConfig udpConfig = new(
            localName,
            "127.0.0.1",
            7000,
            "127.0.0.1",
            0);

        try
        {
            CommunicationBase oldCommunication = CommunicationFactory.CreateCommunicationProtocol(tcpConfig);
            CommunicationBase newCommunication = CommunicationFactory.CreateCommunicationProtocol(udpConfig);

            Assert.Multiple(() =>
            {
                Assert.That(oldCommunication, Is.TypeOf<TCPClient>());
                Assert.That(newCommunication, Is.TypeOf<UDPClient>());
                Assert.That(CommunicationFactory.Get(localName), Is.SameAs(newCommunication));
            });
        }
        finally
        {
            CommunicationFactory.Remove(localName);
        }
    }
}
