using Module.Business.Business;
using Shared.Abstractions.Enum;
using Shared.Abstractions.ICommunication;
using System.Threading;

namespace TestProject;

[TestFixture]
public sealed class S7BusinessTests
{
    [Test]
    public async Task WaitAddressUntil_WhenReadValueMatches_ReturnsTrue()
    {
        FakePlcCommunication plcCommunication = new("5");

        bool result = await S7Business.WaitAddressUntil(
            plcCommunication,
            "D100",
            "Decimal",
            ">=",
            "5",
            100,
            1,
            CancellationToken.None);

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task WaitAddressUntil_WhenReadValueDoesNotMatch_ReturnsFalseAfterTimeout()
    {
        FakePlcCommunication plcCommunication = new("1");

        bool result = await S7Business.WaitAddressUntil(
            plcCommunication,
            "D100",
            "Decimal",
            ">",
            "5",
            20,
            1,
            CancellationToken.None);

        Assert.That(result, Is.False);
    }

    private sealed class FakePlcCommunication : IPlcCommunication
    {
        private readonly string _value;

        public FakePlcCommunication(string value)
        {
            _value = value;
        }

        public PlcReadResult Read(string address, int length, DataType dataType = DataType.Decimal)
        {
            return PlcReadResult.Create(true, address, length, dataType, _value);
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
