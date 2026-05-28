using Shared.Abstractions.Enum;
using Shared.Abstractions.ICommunication;
using Shared.Models.Communication;
using Shared.Models.Log;
using System;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Shared.Infrastructure.Communication
{
    /// <summary>
    /// PLC communication based on Mitsubishi MX Component ActUtlType.
    /// </summary>
    [CommunicationAdapter(typeof(MxPlcRuntimeConfig))]
    public sealed class MxPlcCommunication : CommunicationBase, IPlcCommunication
    {
        private readonly int _logicalStationNumber;
        private readonly string? _password;
        private readonly object _syncRoot = new object();
        private dynamic? _actUtlType;

        public MxPlcCommunication(MxPlcRuntimeConfig config)
        {
            LocalName = config.LocalName;
            _logicalStationNumber = config.StationNumber;
            _password = config.Password;
        }

        public override bool Start()
        {
            lock (_syncRoot)
            {
                CloseCore();

                Type? actType = Type.GetTypeFromProgID("ActUtlType.ActUtlType");
                if (actType is null)
                {
                    WriteLog("未检测到 Mitsubishi MX Component：ActUtlType.ActUtlType。", LogType.ERROR);
                    IsConnected = ConnectState.DisConnected;
                    return false;
                }

                try
                {
                    _actUtlType = Activator.CreateInstance(actType);
                    _actUtlType.ActLogicalStationNumber = _logicalStationNumber;
                    TrySetPassword(_actUtlType, _password);

                    int resultCode = _actUtlType.Open();
                    bool isConnected = resultCode == 0;
                    IsConnected = isConnected ? ConnectState.Connected : ConnectState.DisConnected;
                    WriteLog(
                        isConnected
                            ? $"{LocalName} PLC 连接成功，逻辑站号：{_logicalStationNumber}。"
                            : $"{LocalName} PLC 连接失败，返回码：{resultCode}。",
                        isConnected ? LogType.INFO : LogType.ERROR);
                    return isConnected;
                }
                catch (Exception ex)
                {
                    CloseCore();
                    IsConnected = ConnectState.DisConnected;
                    WriteLog($"{LocalName} PLC 连接异常：{ex.Message}", LogType.ERROR);
                    return false;
                }
            }
        }

        public override bool Close()
        {
            lock (_syncRoot)
            {
                CloseCore();
                IsConnected = ConnectState.DisConnected;
                WriteLog($"{LocalName} PLC 通信已断开。", LogType.WARN);
                return true;
            }
        }

        public PlcReadResult Read(string address, int length, DataType dataType = DataType.Decimal)
        {
            int normalizedLength = Math.Max(1, length);
            lock (_syncRoot)
            {
                if (!EnsureConnected(address, out string normalizedAddress))
                {
                    return PlcReadResult.Create(false, address, normalizedLength, dataType, "PLC 未连接或地址为空。");
                }

                int[] values = new int[normalizedLength];

                try
                {
                    int resultCode = _actUtlType!.ReadDeviceBlock(normalizedAddress, normalizedLength, out values[0]);
                    bool success = resultCode == 0;
                    string resultText = success
                        ? FormatValues(values, dataType)
                        : $"返回码：{resultCode}";

                    WriteLog(
                        $"{LocalName} PLC 读取 {normalizedAddress}，长度 {normalizedLength}，结果：{(success ? resultText : $"失败 {resultCode}")}。",
                        success ? LogType.INFO : LogType.ERROR);

                    if (success)
                    {
                        Task.Run(() => RaiseReceive(resultText, normalizedAddress));
                    }

                    return PlcReadResult.Create(success, address, normalizedLength, dataType, resultText);
                }
                catch (Exception ex)
                {
                    WriteLog($"{LocalName} PLC 读取异常：{ex.Message}", LogType.ERROR);
                    return PlcReadResult.Create(false, address, normalizedLength, dataType, ex.Message);
                }
            }
        }

        public Task<PlcReadResult> ReadAsync(
            string address,
            int length,
            DataType dataType = DataType.Decimal,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Read(address, length, dataType);
            }, cancellationToken);
        }

        public PlcWriteResult Write(string address, string value, DataType dataType = DataType.Decimal)
        {
            string writeValue = value ?? string.Empty;
            lock (_syncRoot)
            {
                if (!EnsureConnected(address, out string normalizedAddress))
                {
                    return PlcWriteResult.Create(false, address, writeValue, dataType, "PLC 未连接或地址为空。");
                }

                int[] values;
                try
                {
                    values = ParseWriteValues(writeValue);
                }
                catch (Exception ex)
                {
                    WriteLog($"{LocalName} PLC 写入参数错误：{ex.Message}", LogType.ERROR);
                    return PlcWriteResult.Create(false, address, writeValue, dataType, ex.Message);
                }

                try
                {
                    int resultCode = _actUtlType!.WriteDeviceBlock(normalizedAddress, values.Length, ref values[0]);
                    bool success = resultCode == 0;
                    string response = success ? "OK" : $"返回码：{resultCode}";
                    WriteLog(
                        $"{LocalName} PLC 写入 {normalizedAddress}，长度 {values.Length}，值 {string.Join(", ", values)}，结果：{(success ? "成功" : $"失败 {resultCode}")}。",
                        success ? LogType.INFO : LogType.ERROR);
                    return PlcWriteResult.Create(success, address, writeValue, dataType, response);
                }
                catch (Exception ex)
                {
                    WriteLog($"{LocalName} PLC 写入异常：{ex.Message}", LogType.ERROR);
                    return PlcWriteResult.Create(false, address, writeValue, dataType, ex.Message);
                }
            }
        }

        public Task<PlcWriteResult> WriteAsync(
            string address,
            string value,
            DataType dataType = DataType.Decimal,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PlcWriteResult result = Write(address, value, dataType);
                cancellationToken.ThrowIfCancellationRequested();
                return result;
            }, cancellationToken);
        }

        private bool EnsureConnected(object plcAddress, out string address)
        {
            address = plcAddress?.ToString()?.Trim() ?? string.Empty;
            return IsConnected == ConnectState.Connected &&
                   _actUtlType is not null &&
                   !string.IsNullOrWhiteSpace(address);
        }

        private void CloseCore()
        {
            try
            {
                _actUtlType?.Close();
            }
            catch
            {
            }
            finally
            {
                if (_actUtlType is not null && Marshal.IsComObject(_actUtlType))
                {
                    Marshal.FinalReleaseComObject(_actUtlType);
                }

                _actUtlType = null;
            }
        }

        private static void TrySetPassword(dynamic actUtlType, string? password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return;
            }

            try
            {
                actUtlType.ActPassword = password.Trim();
            }
            catch
            {
                // Older MX Component versions may not expose ActPassword.
            }
        }

        private static int[] ParseWriteValues(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new InvalidOperationException("写入值不能为空。");
            }

            return message
                .Split(new[] { ',', ';', ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(ParseNumber)
                .ToArray();
        }

        private static int ParseNumber(string rawValue)
        {
            string value = rawValue.Trim();
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return Convert.ToInt32(value[2..], 16);
            }

            return int.Parse(value, CultureInfo.InvariantCulture);
        }

        private static string FormatValues(int[] values, DataType type)
        {
            return type switch
            {
                DataType.Hexadecimal => string.Join(", ", values.Select(value => $"0x{value:X}")),
                DataType.Binary => string.Join(", ", values.Select(value => Convert.ToString(value, 2))),
                DataType.Octal => string.Join(", ", values.Select(value => Convert.ToString(value, 8))),
                DataType.Acsaii or DataType.String => new string(values.Select(value => (char)value).ToArray()),
                _ => string.Join(", ", values)
            };
        }

    }
}
