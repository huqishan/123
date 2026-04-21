using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Xml;

namespace Shared.Infrastructure.PackMethod
{
    public static class XMLHelper
    {
        public static string ObjectToXML<T>(T obj)
        {
            using (MemoryStream Stream = new MemoryStream())
            {
                XmlSerializer xml = new XmlSerializer(typeof(T));
                //序列化对象
                xml.Serialize(Stream, obj);
                using (StreamReader sr = new StreamReader(Stream))
                {
                    Stream.Position = 0;
                    string str = sr.ReadToEnd();
                    return str;
                }
            }
        }
        public static T XMLToObject<T>(string xml) where T : class
        {
            using (StringReader sr = new StringReader(xml))
            {
                XmlSerializer xmldes = new XmlSerializer(typeof(T));
                return xmldes.Deserialize(sr) as T;
            }
        }
        public static bool SaveXML<T>(T obj, string filePath)
        {
            if (string.IsNullOrEmpty(Path.GetFileName(filePath)))
            {
                filePath += $"\\{typeof(T).Name}.xml";
            }
            string path = Path.GetDirectoryName(filePath);
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            using (Stream writer = new FileStream(filePath, FileMode.Create))
            {
                serializer.Serialize(writer, obj);
            }
            return true;
        }
        public static T ReadXML<T>(string filePath) where T : class
        {
            if (File.Exists(filePath))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(T));
                using (Stream read = new FileStream(filePath, FileMode.Open))
                {
                    return serializer.Deserialize(read) as T;
                }
            }
            return null;

        }
        #region XML数据格式化
        public static string ToXMLFormat(string xmlString)
        {
            try
            {
                XmlDocument document = new XmlDocument();
                document.LoadXml(xmlString);
                MemoryStream memoryStream = new MemoryStream();
                XmlTextWriter writer = new XmlTextWriter(memoryStream, null)
                {
                    Formatting = System.Xml.Formatting.Indented//缩进
                };
                document.Save(writer);
                StreamReader streamReader = new StreamReader(memoryStream);
                memoryStream.Position = 0;
                string xml = streamReader.ReadToEnd();
                streamReader.Close();
                memoryStream.Close();
                return xml;
            }
            catch (Exception)
            {
                return xmlString;
            }

        }
        #endregion
        public static string GetXMLValueForKey(string xmlStr, string key)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xmlStr);
            foreach (XmlElement item in xmlDoc.ChildNodes)
            {
                return Getvalue(item, key);
            }
            return string.Empty;
        }
        private static string Getvalue(XmlElement element, string key)
        {
            foreach (XmlElement item in element.ChildNodes)
            {
                if (item.Name.Equals(key))
                    return item.InnerText;
                else if (!item.HasChildNodes || (item.InnerText == item.InnerXml))
                    continue;
                else
                    return Getvalue(item, key);
            }
            return null;
        }
    }
}
