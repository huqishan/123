using Shared.Models.MES;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Infrastructure.PackMethod
{
    public static class WebServiceHelper
    {
        private static HttpWebRequest _Client = null;

        public static string Send(string data, string url, XMLConfig xmlConfig, ref int sta, string contentType = "text/xml; charset=utf-8")
        {
            System.Net.WebResponse webResponse;
            string result;
            try
            {
                System.Net.HttpWebRequest webRequest = System.Net.HttpWebRequest.Create(url) as System.Net.HttpWebRequest;
                webRequest.ContentType = "text/xml; charset=utf-8";
                webRequest.Method = "POST";
                webRequest.Headers.Add("SOAPAction", xmlConfig.XMLAction);
                CredentialCache mycred = new CredentialCache();
                mycred.Add(new Uri(url), "Basic", new NetworkCredential(xmlConfig.UserName, xmlConfig.Password));
                webRequest.Credentials = mycred;
                webRequest.KeepAlive = false;
                byte[] paramBytes = Encoding.UTF8.GetBytes(data);
                webRequest.ContentLength = paramBytes.Length;
                using (Stream requestStream = webRequest.GetRequestStream())
                {
                    requestStream.Write(paramBytes, 0, paramBytes.Length);
                }
                webResponse = webRequest.GetResponse();
                using (StreamReader myStreamReader = new StreamReader(webResponse.GetResponseStream(), Encoding.UTF8))
                {
                    result = myStreamReader.ReadToEnd();
                }
                sta = 200;
            }
            catch (WebException ex)
            {
                result = ex.Message;
                sta = 500;
            }
            return result;
        }
    }
}
