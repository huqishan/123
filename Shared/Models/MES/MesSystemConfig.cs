using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Models.MES
{
    public class MesSystemConfig
    {
        /// <summary>
        /// 等待返回超时时间
        /// </summary>
        public int TimeOut { get; set; } = 10;
        /// <summary>
        /// 重传间隔
        /// </summary>
        public int Interval { get; set; } = 2;
        /// <summary>
        /// 重传次数
        /// </summary>
        public int RetransmissionsNum { get; set; } = 0;
        /// <summary>
        /// 是否启用心跳接口
        /// </summary>
        public bool IsHeartbeat { get; set; } = false;
        /// <summary>
        /// 心跳间隔
        /// </summary>
        public int HeartbeatSpan = 1;

    }
}
