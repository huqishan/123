using RabbitMQ.Client;
using RestSharp;
using RestSharp.Authenticators;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Infrastructure.PackMethod
{
    public static class WebApiHelper
    {
        //application/json
        public static string Send(string data, string url, ref int sta, Dictionary<string, string> heads = null, string method = "POST", int timeOut = 10000)
        {

            RestClient client = new RestClient(url);
            var request = new RestRequest();
            if (heads.ContainsKey("Basic Auth"))
            {
                request.AddParameter("Authorization", "Basic " + Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{heads["Username"]}:{heads["Password"]}")), ParameterType.HttpHeader);
                heads.Remove("Basic Auth");
                heads.Remove("Username");
                heads.Remove("Password");
            }

            request.Method = method.Equals("POST") ? Method.Post : Method.Get;
            if (heads != null && heads.Count > 0)
            {
                foreach (var pair in heads)
                {
                    request.AddHeader(pair.Key, pair.Value);
                }
            }
            request.Timeout = TimeSpan.FromMilliseconds(timeOut);
            if (!string.IsNullOrEmpty(data))
            {
                if (heads.ContainsKey("Content-Type") && heads["Content-Type"] == "application/x-www-form-urlencoded")
                {
                    var paramList = data.Split('&');
                    foreach (var pair in paramList)
                    {
                        var charIndex = pair.IndexOf('=');
                        request.AddParameter(pair.Substring(0, charIndex), pair.Substring(charIndex + 1));
                    }
                }
                else
                {
                    request.AddParameter("application/json", data, ParameterType.RequestBody);
                }
            }
            var result = client.Execute(request);
            sta = Convert.ToInt32(result.StatusCode);
            if (!string.IsNullOrEmpty(result.ErrorException?.Message))
                return result.ErrorException.Message;
            return result.Content;
        }
        private static List<HttpListener> HttpListenerList = new List<HttpListener>();

        /// <summary>
        /// 初始化服务
        /// </summary>
        public static void InitService(object obj)
        {
            // 根据命名空间反射类的Type
            Type type = obj.GetType();
            string url = null;
            if (type.GetProperty("Url") != null)
            {
                url = type.GetProperty("Url").GetValue(obj).ToString();
            }
            // 获取所有的方法
            MethodInfo[] info = type.GetMethods();
            // 遍历所有的方法
            foreach (MethodInfo item in info)
            {
                // 获取Http请求方法
                HttpMethod httpMethod = item.GetCustomAttribute<HttpMethod>();
                // 获取Action
                ActionUrl actionUrl = item.GetCustomAttribute<ActionUrl>();
                // 判断有没有特性
                if (httpMethod != null || actionUrl != null)
                {
                    HttpListener listerner = new HttpListener();
                    listerner.AuthenticationSchemes = AuthenticationSchemes.Anonymous;//指定身份验证 Anonymous匿名访问
                    var ttt = string.IsNullOrEmpty(url) ? actionUrl.URL : url + "/";
                    listerner.Prefixes.Add(string.IsNullOrEmpty(url) ? actionUrl.URL : url + "/");
                    //开启服务
                    if (!listerner.IsListening)
                    {
                        listerner.Start();
                        AsyncCallback ac = new AsyncCallback(GetContextAsyncCallback);
                        CallbackObject callback = new CallbackObject() { Listerner = listerner, MethodItem = item, ClassInstance = obj, HttpMethod = httpMethod.method };
                        listerner.BeginGetContext(ac, callback);
                        HttpListenerList.Add(listerner);
                    }
                }

            }
        }

        /// <summary>
        /// 收到监听请求回调
        /// </summary>
        /// <param name="ia"></param>
        private static void GetContextAsyncCallback(IAsyncResult ia)
        {
            CallbackObject state = ia.AsyncState as CallbackObject;
            if (ia.IsCompleted)
            {
                HttpListenerContext ctx = state.Listerner.EndGetContext(ia);
                var request = ctx.Request;
                HttpListenerResponse response = ctx.Response;
                try
                {

                    //判断 请求 方式
                    if (request.HttpMethod.ToUpper() == state.HttpMethod.ToString().ToUpper() || RequestMethod.All.ToString().ToUpper() == state.HttpMethod.ToString().ToUpper())
                    {
                        string rawData;

                        using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                        {
                            rawData = reader.ReadToEnd();
                        }
                        object resobj = state.MethodItem.Invoke(state.ClassInstance, new object[] { rawData });
                        if (typeof(string) == resobj.GetType())
                        {
                            ResponseWrite(response, resobj.ToString());
                        }
                    }
                    else
                    {
                        ResponseWrite(response, $"不支持{request.HttpMethod.ToUpper()}方法请求！");
                    }
                }
                catch (Exception ex)
                {
                    ResponseWrite(response, $"服务出现异常，异常信息:{ex.Message}");
                }
            }
            //重新监听  不写的话只能调用一次 
            AsyncCallback ac = new AsyncCallback(GetContextAsyncCallback);
            state.Listerner.BeginGetContext(ac, state);
        }

        /// <summary>
        /// 回写响应
        /// </summary>
        /// <param name="response"></param>
        /// <param name="Content"></param>
        private static void ResponseWrite(HttpListenerResponse response, string Content)
        {
            //使用Writer输出http响应代码
            using (System.IO.StreamWriter writer = new System.IO.StreamWriter(response.OutputStream, new UTF8Encoding()))
            {
                response.ContentType = "application/json; charset=utf-8";
                writer.WriteLine(Content);
                writer.Close();
                response.Close();
            }
        }
    }
    public enum RequestMethod
    {
        All, Post, Get
    }

    public class CallbackObject
    {
        /// <summary>
        /// 监听
        /// </summary>
        public HttpListener Listerner { get; set; }

        /// <summary>
        /// 方法
        /// </summary>
        public MethodInfo MethodItem { get; set; }

        /// <summary>
        /// 调用者 对象
        /// </summary>
        public object ClassInstance { get; set; }

        /// <summary>
        /// 调用方式 Get Post
        /// </summary>
        public RequestMethod HttpMethod { get; set; }
    }
    [AttributeUsage(AttributeTargets.Method)]
    public class ActionUrl : Attribute
    {
        public string URL { get; set; }
        public ActionUrl(string url)
        {
            this.URL = url;
        }
    }
    [AttributeUsage(AttributeTargets.Method)]
    public class HttpMethod : Attribute
    {
        public RequestMethod method { get; set; }
        public HttpMethod()
        {
            this.method = RequestMethod.All;
        }

        public HttpMethod(RequestMethod _method)
        {
            this.method = _method;
        }

    }
    [AttributeUsage(AttributeTargets.Class)]
    public class WebAPIServer : Attribute
    {
    }
}
