using Shared.Abstractions.Enum;
using System.Threading;
using System.Threading.Tasks;

namespace Shared.Abstractions.ICommunication;

public interface IPlcCommunication
{
    PlcReadResult Read(string address, int length, DataType dataType = DataType.Decimal);

    Task<PlcReadResult> ReadAsync(
        string address,
        int length,
        DataType dataType = DataType.Decimal,
        CancellationToken cancellationToken = default);

    PlcWriteResult Write(string address, string value, DataType dataType = DataType.Decimal);

    Task<PlcWriteResult> WriteAsync(
        string address,
        string value,
        DataType dataType = DataType.Decimal,
        CancellationToken cancellationToken = default);
}

public sealed record PlcReadResult(
    bool IsSuccess,
    string Address,
    int Length,
    DataType DataType,
    object? Value,
    string Message)
{
    public static PlcReadResult Create(
        bool isSuccess,
        string address,
        int length,
        DataType dataType,
        object? value)
    {
        return new PlcReadResult(
            isSuccess,
            address ?? string.Empty,
            length,
            dataType,
            value,
            value?.ToString() ?? string.Empty);
    }
}

public sealed record PlcWriteResult(
    bool IsSuccess,
    string Address,
    string Value,
    DataType DataType,
    object? Response,
    string Message)
{
    public static PlcWriteResult Create(
        bool isSuccess,
        string address,
        string value,
        DataType dataType,
        object? response)
    {
        return new PlcWriteResult(
            isSuccess,
            address ?? string.Empty,
            value ?? string.Empty,
            dataType,
            response,
            response?.ToString() ?? string.Empty);
    }
}
