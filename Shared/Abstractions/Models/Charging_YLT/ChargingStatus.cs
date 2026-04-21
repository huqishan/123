using Shared.Abstractions.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Abstractions.Models.Charging_YLT
{
    public class ChargingStatus
    {
        /// <summary>
        /// 序列号
        /// </summary>
        public string SerialNumber { get; set; }
        /// <summary>
        /// 加密标志
        /// </summary>
        public string EncryptionFlag { get; set; }
        /// <summary>
        /// 桩编码
        /// </summary>
        [ShowName(3, "桩编码")]
        public string PileCode { get; set; }
        /// <summary>
        /// 是否归位
        /// </summary>
        [ShowName(4, "是否归位")]
        public bool IsReturnToPosition { get; set; } = true;
        /// <summary>
        /// 是否插枪
        /// </summary>
        [ShowName(5, "是否插枪")]
        public bool IsInsert { get; set; } = false;
        /// <summary>
        /// 输出电压
        /// </summary>
        [ShowName(6, "输出电压")]
        public double OutputVoltage { get; set; } = 0.0;
        /// <summary>
        /// 输出电流
        /// </summary>
        [ShowName(7, "输出电流")]
        public double OutputCurrent { get; set; } = 0.0;
        /// <summary>
        /// SOC
        /// </summary>
        [ShowName(8, "SOC")]
        public double SOC { get; set; } = 0.0;
        /// <summary>
        /// 最高温度
        /// </summary>
        [ShowName(9, "最高温度")]
        public double TemperatureMax { get; set; } = 0.0;
        /// <summary>
        /// 最低温度
        /// </summary>
        [ShowName(10, "最低温度")]
        public double TemperatureMin { get; set; } = 0.0;
        /// <summary>
        /// 最高单体电压
        /// </summary>
        [ShowName(11, "最高单体电压")]
        public double VoltageMax { get; set; } = 0.0;
        /// <summary>
        /// 最低单体电压
        /// </summary>
        [ShowName(12, "最低单体电压")]
        public double VoltageMin { get; set; } = 0.0;
        /// <summary>
        /// 电压需求
        /// </summary>
        [ShowName(13, "电压需求")]
        public double VoltageDemand { get; set; } = 0.0;
        /// <summary>
        /// 电流需求
        /// </summary>
        [ShowName(14, "电流需求")]
        public double CurrentDemand { get; set; } = 0.0;
        /// <summary>
        /// BMS 充电电压测量值
        /// </summary>
        [ShowName(15, "BMS电压测量值")]
        public double BMSVoltage { get; set; } = 0.0;
        /// <summary>
        /// BMS 充电电流测量值
        /// </summary>
        [ShowName(16, "BMS电流测量值")]
        public double BMSCurrent { get; set; } = 0.0;
        /// <summary>
        /// BMS 当前荷电状态 SOC（ %）
        /// </summary>
        [ShowName(17, "BMS荷电状态 SOC（ %）")]
        public double BMSSOC { get; set; } = 0.0;
        /// <summary>
        /// 充电模式
        /// </summary>
        [ShowName(18, "充电模式")]
        public string ChargingType { get; set; }
        /// <summary>
        /// 启动结果
        /// </summary>
        [ShowName(19, "启动结果")]
        public bool IsCharging { get; set; } = false;
        /// <summary>
        /// 启动失败原因
        /// </summary>
        [ShowName(20, "启动失败原因")]
        public short ErrorCode { get; set; } = 0;
    }
}
