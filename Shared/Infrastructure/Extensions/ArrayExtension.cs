using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Infrastructure.Extensions
{
    public static class ArrayExtension
    {
        #region byte数组转16进制字符串
        public static string ByteArrayToHexString(this byte[] arr)
        {
            return BitConverter.ToString(arr, 0).Replace("-", "");
        }
        #endregion
        public static short[] ByteToShort(this byte[] arr, bool isConvertData = false)
        {
            short[] result = new short[arr.Length];
            if (!isConvertData)
                result = arr.Select(item => (short)item).ToArray();
            else
            {
                //两个byte合成一个short
                result = new short[Convert.ToInt32(Math.Round((double)arr.Length / 2, MidpointRounding.AwayFromZero))];
                IntPtr p = Marshal.UnsafeAddrOfPinnedArrayElement(arr, 0);
                Marshal.Copy(p, result, 0, result.Length);
            }
            return result;
        }
        public static short[] ShortToBinary(this short[] arr)
        {
            int cursor = 0;
            short[] binaryArr = new short[arr.Length * 16];
            for (int i = 0; i < arr.Length; i++)
            {
                string str = Convert.ToString(arr[i], 2).PadLeft(16, '0').ReverseExtension();
                for (int j = 0; j < str.Length; j++)
                {
                    binaryArr[cursor] = Convert.ToInt16(str[j].ToString());
                    cursor++;
                }
            }
            return binaryArr;
        }
        public static byte[] ShortToByte(this short[] arr, bool isConvertData = false)
        {
            byte[] result = new byte[arr.Length];
            if (!isConvertData)
                result = arr.Select(item => (byte)item).ToArray();
            else
            {
                //一个short拆分为两个byte
                result = new byte[arr.Length * Marshal.SizeOf(arr[0])];
                IntPtr p = Marshal.UnsafeAddrOfPinnedArrayElement(arr, 0);
                Marshal.Copy(p, result, 0, result.Length);
            }
            return result;
        }
        public static string AsciiToString(this short[] arr)
        {
            ASCIIEncoding ASCIITochar = new ASCIIEncoding();
            char[] ascii = ASCIITochar.GetChars(arr.ShortToByte());      // 将接收字节解码为ASCII字符数组
            return string.Join("", ascii);
        }
        public static int[] ShortToInt(this short[] arr, bool isConvertData = false)
        {
            int[] result = new int[arr.Length];
            if (!isConvertData)
                result = arr.Select(item => (int)item).ToArray();
            else
            {
                //两个byte合成一个short
                result = new int[Convert.ToInt32(Math.Round((double)arr.Length / 2, MidpointRounding.AwayFromZero))];
                IntPtr p = Marshal.UnsafeAddrOfPinnedArrayElement(arr, 0);
                Marshal.Copy(p, result, 0, result.Length);
            }
            return result;
        }
        public static int ShortToInt(short highData, short lowData2)
        {
            uint ret = 0;
            ret = (uint)(highData & 0xFFFF) | (uint)(lowData2 << 16);
            return (int)ret;
        }
        public static List<T> ArryToList<T>(T[] arr)
        {
            return arr.ToList();
        }
        /// <summary>
        /// 合并byte数组
        /// </summary>
        /// <param name="sourceBytesArray">要合并的数组集合</param>
        /// <returns>合并后的byte数组</returns>
        public static byte[] ConcatBytes(params byte[][] sourceBytesArray)
        {
            int allLength = sourceBytesArray.Sum(o => o.Length);
            byte[] resultBytes = new byte[allLength];
            for (int i = 0; i < sourceBytesArray.Length; i++)
            {
                int copyToIndex = GetCopyToIndex(sourceBytesArray, i);
                sourceBytesArray[i].CopyTo(resultBytes, copyToIndex);
            }
            return resultBytes;
        }
        /// <summary>
        /// 获取复制开始处的索引
        /// </summary>
        /// <param name="sourceBytesArray">byte[]的所在数组</param>
        /// <param name="index">byte[]的所在数组的索引</param>
        /// <returns>复制开始处的索引</returns>
        private static int GetCopyToIndex(byte[][] sourceBytesArray, int index)
        {
            if (index == 0)
            {
                return 0;
            }
            return sourceBytesArray[index - 1].Length + GetCopyToIndex(sourceBytesArray, index - 1);
        }
    }
}
