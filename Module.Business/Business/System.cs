using Shared.Infrastructure.Events;
using Shared.Abstractions.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Module.Business.Business
{
    [DeviceBusiness("System", "系统")]
    public static class System
    {
        private static IEventAggregator? _eventAggregator;

        public static void ConfigureEventAggregator(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
        }

        /// <summary>
        /// 十六进制字符串转换为普通字符串
        /// </summary>
        /// <param name="hexString">十六进制字符串</param>
        /// <returns></returns>
        [BusinessOperation("HextoString", "十六进制转字符串")]
        public static string HextoString([BusinessParam("十六进制字符串")] string hexString)
        {
            if (string.IsNullOrEmpty(hexString))
                return string.Empty;
            try
            {
                var bytes = new byte[hexString.Length / 2];
                for (int i = 0; i < bytes.Length; i++)
                {
                    bytes[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
                }
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                // Handle invalid hex string format
                return string.Empty;
            }
        }

        /// <summary>
        /// 字符串转换为十六进制字符串
        /// </summary>
        /// <param name="input">要转换的字符串</param>
        /// <returns>十六进制表示的字符串</returns>
        [BusinessOperation("StringtoHex", "字符串转十六进制")]
        public static string StringtoHex([BusinessParam("输入字符串")] string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;
            var bytes = Encoding.UTF8.GetBytes(input);
            var hexString = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
            {
                hexString.AppendFormat("{0:x2}", b);
            }
            return hexString.ToString();
        }

        /// <summary>
        /// 获取当前用户名
        /// </summary>
        /// <returns></returns>
        [BusinessOperation("GetCurrentUserName", "获取当前用户名")]
        public static string GetCurrentUserName()
        {
            return Environment.UserName;
        }
        /// <summary>
        /// 发送数据到视图层
        /// </summary>
        /// <param name="name">数据名称</param>
        /// <param name="type">判断类型</param>
        /// <param name="value">数据值</param>
        /// <param name="judge">判断条件</param>
        [BusinessOperation("SendDataToView", "发送数据到视图层")]
        public static void SendDataToView(
            [BusinessParam("数据名称")] string name,
            [BusinessParam("判断类型")] string type,
            [BusinessParam("数据值")] string value,
            [BusinessParam("判断条件")] string judge)
        {
            _eventAggregator?.GetEvent<MessageShowView>().Publish(new MessageShowView
            {
                Name = name,
                Type = type,
                Value = value,
                Judge = judge
            });
        }
        public sealed class MessageShowView : PubSubEvent<MessageShowView>
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public string Value { get; set; }
            public string Judge { get; set; }
        }
    }

}
