using Shared.Abstractions.Attributes;
using Shared.Abstractions.Enum;
using Shared.Abstractions.ICommunication;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Module.Business.Business;

[DeviceBusiness(CommuniactionType.PLC, "西门子 S7")]
public static class S7Business
{
    [BusinessOperation("WaitAddressUntil", "等待地址满足条件")]
    public static async Task<bool> WaitAddressUntil(
        IPlcCommunication plcCommunication,
        [BusinessParam("地址")] string address,
        [BusinessParam("数据类型")] string dataType,
        [BusinessParam("判断符")] string judge,
        [BusinessParam("目标值")] string expectedValue,
        [BusinessParam("超时时间ms", DefaultValue = "30000")] int timeoutMs,
        [BusinessParam("轮询间隔ms", DefaultValue = "200")] int intervalMs,
        CancellationToken cancellationToken)
    {
        DataType plcDataType = ParseDataType(dataType);
        int normalizedTimeout = Math.Max(0, timeoutMs);
        int normalizedInterval = Math.Max(1, intervalMs);
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMilliseconds(normalizedTimeout);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            PlcReadResult readResult = await plcCommunication
                .ReadAsync(address, 1, plcDataType, cancellationToken)
                .ConfigureAwait(false);

            if (readResult.IsSuccess && IsConditionMatched(readResult.Message, judge, expectedValue))
            {
                return true;
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                return false;
            }

            await Task.Delay(normalizedInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    private static DataType ParseDataType(string value)
    {
        string normalizedValue = value?.Trim() ?? string.Empty;
        if (Enum.TryParse(normalizedValue, true, out DataType dataType))
        {
            return dataType;
        }

        if (int.TryParse(normalizedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int numericValue) &&
            Enum.IsDefined(typeof(DataType), numericValue))
        {
            return (DataType)numericValue;
        }

        return normalizedValue switch
        {
            "二进制" => DataType.Binary,
            "八进制" => DataType.Octal,
            "十进制" => DataType.Decimal,
            "十六进制" => DataType.Hexadecimal,
            "ASCII" => DataType.Acsaii,
            "字符串" => DataType.String,
            _ => DataType.Decimal
        };
    }

    private static bool IsConditionMatched(string actualValue, string judge, string expectedValue)
    {
        string normalizedJudge = (judge ?? string.Empty).Trim();
        string normalizedActual = NormalizeScalar(actualValue);
        string normalizedExpected = NormalizeScalar(expectedValue);

        return normalizedJudge switch
        {
            ">" or "大于" => CompareAsNumber(normalizedActual, normalizedExpected) > 0,
            ">=" or "大于等于" => CompareAsNumber(normalizedActual, normalizedExpected) >= 0,
            "<" or "小于" => CompareAsNumber(normalizedActual, normalizedExpected) < 0,
            "<=" or "小于等于" => CompareAsNumber(normalizedActual, normalizedExpected) <= 0,
            "!=" or "<>" or "不等于" => !ValuesEqual(normalizedActual, normalizedExpected),
            "=" or "==" or "等于" or "" => ValuesEqual(normalizedActual, normalizedExpected),
            _ => ValuesEqual(normalizedActual, normalizedExpected)
        };
    }

    private static string NormalizeScalar(string value)
    {
        string normalizedValue = value?.Trim() ?? string.Empty;
        if (normalizedValue.Length == 0)
        {
            return string.Empty;
        }

        string[] parts = normalizedValue
            .Split(new[] { ',', ';', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .ToArray();

        return parts.Length == 0 ? normalizedValue : parts[0];
    }

    private static bool ValuesEqual(string actualValue, string expectedValue)
    {
        return TryParseNumber(actualValue, out decimal actualNumber) &&
               TryParseNumber(expectedValue, out decimal expectedNumber)
            ? actualNumber == expectedNumber
            : string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareAsNumber(string actualValue, string expectedValue)
    {
        if (!TryParseNumber(actualValue, out decimal actualNumber) ||
            !TryParseNumber(expectedValue, out decimal expectedNumber))
        {
            return string.Compare(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase);
        }

        return actualNumber.CompareTo(expectedNumber);
    }

    private static bool TryParseNumber(string value, out decimal result)
    {
        string normalizedValue = value?.Trim() ?? string.Empty;
        if (normalizedValue.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            result = 1m;
            return true;
        }

        if (normalizedValue.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            result = 0m;
            return true;
        }

        if (normalizedValue.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            long.TryParse(normalizedValue[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long hexValue))
        {
            result = hexValue;
            return true;
        }

        return decimal.TryParse(
            normalizedValue,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out result);
    }
}
