using Shared.Abstractions;
using Shared.Abstractions.Attributes;
using Shared.Abstractions.Enum;
using System.Threading;
using System.Threading.Tasks;

namespace Module.Business.Business;

[DeviceBusiness(CommuniactionType.PLC, "西门子 S7")]
public static class S7Business
{
    [BusinessOperation("WaitAddressUntil", "等待地址满足条件")]
    public static async Task<bool> WaitAddressUntil(
        ICommunication communication,
        [BusinessParam("地址")] string address,
        [BusinessParam("数据类型")] string dataType,
        [BusinessParam("判断符")] string judge,
        [BusinessParam("目标值")] string expectedValue,
        [BusinessParam("超时时间ms", DefaultValue = "30000")] int timeoutMs,
        [BusinessParam("轮询间隔ms", DefaultValue = "200")] int intervalMs,
        CancellationToken cancellationToken)
    {
        _ = communication;
        _ = address;
        _ = dataType;
        _ = judge;
        _ = expectedValue;
        _ = timeoutMs;
        _ = intervalMs;

        await Task.Delay(1000, cancellationToken);
        return true;
    }
}
