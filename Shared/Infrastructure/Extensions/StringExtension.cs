using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Shared.Infrastructure.Extensions
{
    public static class StringExtension
    {
        #region 字符串反转
        public static string ReverseExtension(this string str)
        {
            return new string(str.ToCharArray().Reverse().ToArray<char>());
        }
        #endregion

        #region String 转 Ascaii
        public static byte[] ToAscaii(this string str)
        {
            char[] charBuf = str.ToArray();    // 将字符串转换为字符数组
            ASCIIEncoding charToASCII = new ASCIIEncoding();
            return charToASCII.GetBytes(charBuf); 　　 // 转换为各字符对应的ASCII
        }
        #endregion

        #region DES加密字符串
        private static byte[] DESKeys = { 0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF };//密钥
        private static byte[] DesKey = { 0xFE, 0xDC, 0xBA, 0x09, 0x87, 0x65, 0x43, 0x21 };
        ///<summary>   
        ///DES加密字符串   
        ///</summary>   
        ///<param name="str">待加密的字符串</param>   
        ///<returns>加密后的字符串</returns>   
        public static string EncryptDES(this string str)
        {
            byte[] inputByteArray = Encoding.UTF8.GetBytes(str);
            DESCryptoServiceProvider provider = new DESCryptoServiceProvider();
            MemoryStream mStream = new MemoryStream();
            CryptoStream cStream = new CryptoStream(mStream, provider.CreateEncryptor(DesKey, DESKeys), CryptoStreamMode.Write);
            cStream.Write(inputByteArray, 0, inputByteArray.Length);
            cStream.FlushFinalBlock();
            return Convert.ToBase64String(mStream.ToArray());
        }
        #endregion

        #region DES解密字符串
        /// <summary>
        /// DES解密字符串
        /// </summary>
        /// <param name="decryptString"></param>
        /// <returns></returns>
        public static string DesDecrypt(this string decryptString)
        {
            byte[] inputByteArray = Convert.FromBase64String(decryptString);
            DESCryptoServiceProvider provider = new DESCryptoServiceProvider();
            MemoryStream mStream = new MemoryStream();
            CryptoStream cStream = new CryptoStream(mStream, provider.CreateDecryptor(DesKey, DESKeys), CryptoStreamMode.Write);
            cStream.Write(inputByteArray, 0, inputByteArray.Length);
            cStream.FlushFinalBlock();
            return Encoding.UTF8.GetString(mStream.ToArray());
        }
        #endregion

        #region 首字母大写
        /// <summary>
        /// 首字母大写
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string ConvertAString(this string str)
        {
            return $"{str.Substring(0, 1).ToUpper()}{str.Substring(1, str.Length - 1).ToLower()}";
        }
        #endregion

        #region 16进制字符串转byte数组
        /// <summary>
        /// 16进制字符串转byte数组
        /// </summary>
        /// <param name="hexString"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public static byte[] HexStringToByteArray(this string hexString)
        {
            if (hexString == null)
                throw new ArgumentNullException(nameof(hexString));

            if (hexString.Length % 2 != 0)
                throw new ArgumentException("The binary key cannot have an odd number of digits");

            byte[] data = new byte[hexString.Length / 2];
            for (int i = 0; i < data.Length; i++)
            {
                string hex = hexString.Substring(i * 2, 2);
                data[i] = Convert.ToByte(hex, 16);
            }
            return data;
        }
        #endregion

        #region XML数据格式化
        public static string ToXMLFormat(this string xmlStr) 
        {
            try
            {
                XmlDocument document = new XmlDocument();
                document.LoadXml(xmlStr);
                MemoryStream memoryStream = new MemoryStream();
                XmlTextWriter writer = new XmlTextWriter(memoryStream, null) {
                Formatting=System.Xml.Formatting.Indented//缩进
                };
                document.Save(writer);
                StreamReader streamReader=new StreamReader(memoryStream);
                memoryStream.Position = 0;
                string xml = streamReader.ReadToEnd();
                streamReader.Close();
                memoryStream.Close();
                return xml;
            }
            catch (Exception)
            {
                return xmlStr;
            }
        }
        #endregion

    }
}
