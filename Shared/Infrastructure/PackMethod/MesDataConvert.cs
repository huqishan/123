using Newtonsoft.Json.Linq;
using JsonFormatting = Newtonsoft.Json.Formatting;
using Shared.Abstractions.ICommunication;
using Shared.Global;
using Shared.Infrastructure.Communication;
using Shared.Infrastructure.Extensions;
using Shared.Infrastructure.Lua;
using Shared.Models.Communication;
using Shared.Models.Log;
using Shared.Models.MES;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml;
using System.Xml.Linq;

namespace Shared.Infrastructure.PackMethod
{
    public static class MesDataConvert
    {
        static string _LayoutFile = $"{System.AppDomain.CurrentDomain.SetupInformation.ApplicationBase}\\Config\\MES_Config";
        static string _ErrorCode = null;
        static Dictionary<string, CommunicationBase> _MESObj = new Dictionary<string, CommunicationBase>();
        public static string Convert(MesDataInfoTree sourceData, DataSruct dataLayout)
        {
            if (dataLayout == null || dataLayout.Structure == null || dataLayout.Structure.Count == 0) return null;
            try
            {
                switch (dataLayout.StructureType)
                {
                    case "JSON":
                        return BuildJsonToken(dataLayout.Structure, CreateJsonBuildContext(sourceData))
                            .ToString(JsonFormatting.None);
                    case "JSONREMOVEQUE"://json的key没有引号
                        return BuildJsonToken(dataLayout.Structure, CreateJsonBuildContext(sourceData))
                            .ToString(JsonFormatting.None)
                            .JsonRemoveQuo();
                    case "JOINT":
                        return ItemsToString(sourceData, dataLayout.Structure);
                    case "SOAP":
                        return BuildSoapDocument(dataLayout.Structure[0], CreateSoapBuildContext(sourceData)).ToString();
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"添加{_ErrorCode} 失败 {ex.Message}");
            }
            return null;
        }
        public static string Convert(MesDataInfoTree sourceData, string structName)
        {
            DataSruct dataLayout = JsonHelper.ReadJson<DataSruct>($"{_LayoutFile}\\DataStructure\\{structName}.json");
            if (dataLayout == null || dataLayout.Structure == null || dataLayout.Structure.Count == 0) throw new Exception($"上位机数据结构为空！！！");
            JObject jsonObj;
            try
            {
                return Convert(sourceData, dataLayout);
            }
            catch (Exception ex)
            {
                throw new Exception($"上位机添加{_ErrorCode}Exp异常：{ex.Message}");
            }
            throw new Exception($"上位机数据转换为空，请检查数据结构");
        }
        public static MesResult ExecuteApi(string apiName, MesDataInfoTree sourceData = null)
        {
            string apiFilePath = $"{_LayoutFile}\\ApiConfig\\{apiName}.json";
            APIConfig apiConfig = JsonHelper.ReadJson<APIConfig>(apiName);
            MesResult mesResult = new MesResult();
            if (apiConfig == null)
            {
                mesResult.Message = $"Unfulfilled 上位机未找到 【{apiName}】 接口,{apiFilePath}";
                mesResult.State = MesStatus.UnUpLoad;
                goto sendNG;
            }
            if (string.IsNullOrWhiteSpace(apiConfig.Lua))
            {
                apiConfig.Lua = $"return SendMES(\"{apiName}\")";
            }
            LuaManage luaManage = new LuaManage(sourceData);
            var result = luaManage.DoString(apiConfig.Lua);
            if (result[0] is MesResult mesResult1) return mesResult1;
            Global_Event.WriteLog($"执行脚本出错，错误{result[0]}\r\n脚本：{apiConfig.Lua}", apiName);
            mesResult.Message = $"执行脚本出错，错误{result[0]}\r\n脚本：{apiConfig.Lua}";
            mesResult.State = MesStatus.UpLoadNG;
            return mesResult;
        sendNG:
            Global_Event.WriteLog($"系统校验：{mesResult.State}\r\n系统反馈数据：\r\n{mesResult.Message}", apiName);
            return mesResult;
        }
        public static MesResult SendMES(string apiName, ref string data, MesDataInfoTree sourceData = null)
        {
            string apiFilePath = $"{_LayoutFile}\\ApiConfig\\{apiName}.json";
            APIConfig apiConfig = JsonHelper.ReadJson<APIConfig>(apiFilePath);
            MesSystemConfig mesSystemConfig = JsonHelper.ReadJson<MesSystemConfig>($"{_LayoutFile}\\MesSystemConfig\\MesSystemConfig.json");
            MesResult mesResult = new MesResult();
            if (mesSystemConfig == null)
            {
                mesResult.Message = $"Unfulfilled 未找到【{apiName}】接口，{apiFilePath}";
                mesResult.State = MesStatus.UnUpLoad;
                goto sendNG;
            }
            if (!apiConfig.IsEnabledAPI)
            {
                mesResult.Message = $"Unfulfilled【{apiName}】接口未启用！！！";
                mesResult.State = MesStatus.UnUpLoad;
                goto sendNG;
            }
            if (string.IsNullOrEmpty(data) && apiConfig.DataStructName == null)
            {
                mesResult.Message = $"【{apiName}】接口未选择数据结构！！！";
                mesResult.State = MesStatus.StructNG;
                goto sendNG;
            }
            if (string.IsNullOrEmpty(data))
            {
                try
                {
                    data = Convert(sourceData, apiConfig.DataStructName);
                }
                catch (Exception ex)
                {
                    mesResult.Message = $"【{apiName}】接口数据结构转换失败，错误{ex}";
                    mesResult.State = MesStatus.StructNG;
                    goto sendNG;
                }
            }
            Global_Event.WriteLog($"【{apiName}】转换后数据：\r\n{data}", apiName);
            int sendSta = -1;
            int sendCount = 0;
            double time = 0;
        SendMES:
            sendCount++;
            Global_Event.WriteLog("开始上传MES。。。", apiName);
            switch (apiConfig.SelectMESType.ToUpper())
            {
                case "WEBSERVICE":
                    XMLConfig xmlConfig = new XMLConfig();
                    xmlConfig.UserName = apiConfig.UserName;
                    xmlConfig.Password = apiConfig.Password;
                    xmlConfig.XMLAction = apiConfig.Action;
                    mesResult.Message = WebServiceHelper.Send(data, apiConfig.Url, xmlConfig, ref sendSta).ToXMLFormat();
                    break;
                case "WEBAPI":
                    string tokenValue = null;
                    if (!string.IsNullOrEmpty(apiConfig.TokenUrl))
                    {
                        mesResult.Message = WebApiHelper.Send(null, apiConfig.TokenUrl, ref sendSta, apiConfig?.Heads?.ToDictionary(r => r.Key, r => r.Value.ToString()), "GET", mesSystemConfig.TimeOut);
                        tokenValue = JsonHelper.GetJsonValue(mesResult.Message, apiConfig.TokenName);
                        Global_Event.WriteLog($"Token: {tokenValue}", apiName);
                        apiConfig.Url = apiConfig.Url.Replace(apiConfig.TokenName.ToUpper(), tokenValue);
                    }
                    Dictionary<string, string> headDic = new Dictionary<string, string>();
                    if (apiConfig.Heads != null && apiConfig.Heads.Count() != 0)
                        foreach (var item in apiConfig.Heads)
                        {
                            if (item.Value.ToUpper().Contains(apiConfig.TokenName.ToUpper()))
                                headDic[item.Key] = item.Value.Replace(apiConfig.TokenName.ToUpper(), tokenValue);
                            else if (item.Value.ToUpper().Contains("GUID"))
                                headDic[item.Key] = Guid.NewGuid().ToString();
                            else if (Global_Data.Heads.Keys.Contains(item.Value))
                                headDic[item.Key] = $"{(item.Key.Equals("Authorization") ? "Bearer " : "")}{item.Value.Replace(item.Value, Global_Data.Heads[item.Value].ToString().Replace("Bearer ", ""))}";
                            else headDic[item.Key] = item.Value;
                            Global_Event.WriteLog($"WEBAPI Head:{item.Key} {headDic[item.Key]}", apiName);
                        }
                    DateTime now = DateTime.Now;
                    apiConfig.Url = GetUrl(apiConfig.Url, sourceData);
                    Global_Event.WriteLog($"WEBAPI Url:{apiConfig.Url}", apiName);
                    mesResult.Message = WebApiHelper.Send(data, apiConfig.Url, ref sendSta, headDic, apiConfig.WebApiType, mesSystemConfig.TimeOut * 1000).ToJsonFormat();
                    time = DateTime.Now.Subtract(now).TotalMilliseconds;
                    break;
                case "TCP CLIENT":
                    string ipPort = $"{apiConfig.TCPRemoteIpAddress}:{apiConfig.TCPRemotePort}";
                    if (!_MESObj.Keys.Contains(ipPort))
                    {
                        TcpClientRuntimeConfig tcpConfig = new TcpClientRuntimeConfig("MES", apiConfig.TCPRemoteIpAddress, apiConfig.TCPRemotePort, apiConfig.TCPLocalIpAddress, apiConfig.TCPLocalPort);
                        _MESObj.Add(ipPort, CommunicationFactory.CreateCommunicationProtocol(tcpConfig));
                        _MESObj[ipPort].OnLog -= MESCommuniactionObj_OnLog;
                        _MESObj[ipPort].OnLog += MESCommuniactionObj_OnLog;
                        _MESObj[ipPort].Start();
                    }
                    Thread.Sleep(100);
                    SendReceiveModel send = new SendReceiveModel(data + (apiConfig.IsEnter ? "\r\n" : ""), mesSystemConfig.TimeOut * 1000);
                    if (_MESObj[ipPort] is not ICommunication messageCommunication)
                    {
                        throw new InvalidOperationException("MES TCP communication object does not support message send.");
                    }

                    sendSta = messageCommunication.Send(ref send, !string.IsNullOrWhiteSpace(apiConfig.ResultCheck)) ? 200 : 500;
                    mesResult.Message = send.Result == null ? "" : send.Result.ToString();
                    if (!apiConfig.IsEnabledTCPKeepAlive) _MESObj[ipPort].Close();
                    break;
                case "FTP":
                    if (apiConfig.IsDown)
                    {
                        mesResult.Message = FTPHelper.Download(apiConfig.Url, apiConfig.UserName, apiConfig.Password, apiConfig.DownPath);
                    }
                    else
                    {
                        mesResult.Message = FTPHelper.UploadFile(apiConfig.Url, apiConfig.UserName, apiConfig.Password, data);
                    }
                    sendSta = 200;
                    break;
                default:
                    break;
            }
            if (sendSta == 200)
            {
                mesResult.State = mesResult.Message.ToUpper().Contains($"{(apiConfig.ResultCheck ?? $"@$#@#$@#$#").ToUpper()}") ? MesStatus.ResultOK : MesStatus.ResultNG;
            }
            else if (sendSta == 401)
            {
                mesResult.State = MesStatus.ResultNG;
                if (string.IsNullOrEmpty(mesResult.Message))
                {
                    mesResult.Message = "用户验证已过期";
                }
            }
            else
            {
                mesResult.State = MesStatus.ResultNG;
            }
            Global_Event.WriteLog($"MES服务器校验：{mesResult.State}\r\nMES服务器反馈数据：\r\n{mesResult.Message}", apiName);
            if (mesResult.State > MesStatus.ResultOK)
            {
                if (sendCount <= mesSystemConfig.RetransmissionsNum && mesResult.Message.ToUpper().Contains("操作超时"))
                {
                    Global_Event.WriteLog($"第 {sendCount} 次上传MES超时，等待 {mesSystemConfig.Interval} s后开始重传。。。", apiName);
                    Thread.Sleep(mesSystemConfig.Interval * 1000);
                    goto SendMES;
                }
                mesResult.Message = $"【{apiName}】MES服务器反馈NG，请检查MES服务器具体报错：MES返回消息编码：{sendSta}；MES返回消息内容：{mesResult.Message}";
            }
            return mesResult;
        sendNG:
            Global_Event.WriteLog($"系统校验：{mesResult.State}\r\n系统反馈数据：\r\n{mesResult.Message}", apiName);
            return mesResult;
        }
        #region JsonToModel
        public static TreeModel DeserializeFromJsonFile(string jsonPath)
        {
            JsonNode jsonNode = JsonNode.Parse(File.ReadAllText(jsonPath));
            TreeModel model = new TreeModel() { ClientCode = Path.GetFileNameWithoutExtension(jsonPath), DataType = "JSON"};
            return model;
        }
        private static void AddItemFromJsonNote(JsonNode jsonNode, ref TreeModel tree)
        {
            if (jsonNode == null) return;
            if (jsonNode is JsonObject jsonObj)
            {
                foreach (var kvp in jsonObj)
                {
                    if (kvp.Value is JsonObject)
                    {
                        TreeModel treeModel = new TreeModel()
                        {
                            MESCode = kvp.Key,
                            DataType = "Json"
                        };
                        tree.Children.Add(treeModel);
                        string key = kvp.Key;
                        JsonNode value = kvp.Value;
                        AddItemFromJsonNote(value, ref treeModel);
                    }
                    else if (kvp.Value is JsonArray jsonArr)
                    {
                        TreeModel treeModel = new TreeModel() { MESCode = kvp.Key, DataType = "List" };
                        tree.Children.Add(treeModel);
                        for (int i = 0; i < jsonArr.Count; i++)
                        {
                            JsonNode item = jsonArr[i];
                            TreeModel treeModel1 = new TreeModel() { MESCode = kvp.Key, DataType = "Model" };
                            treeModel.Children.Add(treeModel1);
                            AddItemFromJsonNote(item, ref treeModel1);
                        }
                    }
                    else if (kvp.Value is JsonValue)
                    {
                        tree.Children.Add(new TreeModel { MESCode = kvp.Key, DataType = GetJsonObjectType(kvp), ClientCode = kvp.Value.ToString() });
                    }
                }
            }
            else if (jsonNode is JsonArray jsonArr)
            {
                for (int i = 0; i < jsonArr.Count; i++)
                {
                    JsonNode item = jsonArr[i];
                }
            }
        }
        private static string GetJsonObjectType(KeyValuePair<string, JsonNode> obj)
        {
            var kind = obj.Value.GetValueKind();
            string valuetype = string.Empty;
            switch (kind)
            {
                case System.Text.Json.JsonValueKind.Number:
                    valuetype = "Double";
                    break;
                case System.Text.Json.JsonValueKind.True:
                case System.Text.Json.JsonValueKind.False:
                    valuetype = "Bool";
                    break;
                default:
                    valuetype = "String";
                    break;
            }
            return valuetype;
        }
        #endregion
        #region XMLToModel
        public static TreeModel DeserializeFromXMLFile(string xmlPath)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(File.ReadAllText(xmlPath));
            TreeModel model = new TreeModel() { ClientCode = Path.GetFileNameWithoutExtension(xmlPath), DataType = "SOAP"};
            AddItemFromXmlNote(xmlDoc.FirstChild, ref model);
            return model;
        }
        private static void AddItemFromXmlNote(XmlNode xmlNode, ref TreeModel tree)
        {
            if (xmlNode == null) return;
            bool isEmpty = false;
            if (xmlNode.GetType().GetMethod("get_IsEmpty") != null)
                isEmpty = System.Convert.ToBoolean(xmlNode.GetType().GetMethod("get_IsEmpty").Invoke(xmlNode, null));
            if (xmlNode?.Attributes == null) return;
            TreeModel treeModel = new TreeModel() { MESCode = xmlNode.LocalName, DataType = isEmpty ? "XMLNULL" : "String", XMLNameSpace = xmlNode.NamespaceURI };
            foreach (XmlAttribute attribute in xmlNode?.Attributes)
            {
                treeModel.Children.Add(new TreeModel() { MESCode = attribute.LocalName, DataType = "XMLNamespac", XMLNameSpace = attribute.InnerText });
            }
            if (xmlNode?.ChildNodes.Count > 0)
            {
                foreach (XmlNode childNode in xmlNode.ChildNodes)
                {
                    AddItemFromXmlNote(childNode, ref treeModel);
                }
            }
            tree.Children.Add(treeModel);
        }
        #endregion
        private static string GetUrl(string url, MesDataInfoTree sourceData)
        {
            string result = url;
            if (url.Contains("{"))
            {
                int startIndex = url.IndexOf("{") + 1;
                int length = url.IndexOf("}") - startIndex;
                string clientName = url.Substring(startIndex, length);
                string value = sourceData.MesDataInfoItems.FirstOrDefault(r => r.Code == clientName).Value.ToString();
                result = url.Remove(startIndex, length).Insert(startIndex, value).Replace("{", "").Replace("}", "");
            }
            return result;
        }
        private static void MESCommuniactionObj_OnLog(LogMessageModel obj)
        {
            Global_Event.WriteLog(obj.Message, null);
        }
        #region JSON
        /// <summary>
        /// 为单次 JSON 转换建立源字段索引。索引属于本次调用，避免静态状态导致并发请求互相读取数据。
        /// 同名字段保持旧实现 FirstOrDefault 的行为，只记录首次出现的值。
        /// </summary>
        private sealed class JsonBuildContext
        {
            public JsonBuildContext(MesDataInfoTree sourceData, IReadOnlyDictionary<string, object> sourceValues, int? whileIndex = null,
                Dictionary<string, TreeModel> referencedLayouts = null, HashSet<string> activeReferencedLayouts = null)
            {
                SourceData = sourceData;
                SourceValues = sourceValues;
                WhileIndex = whileIndex;
                ReferencedLayouts = referencedLayouts ?? new Dictionary<string, TreeModel>(StringComparer.OrdinalIgnoreCase);
                ActiveReferencedLayouts = activeReferencedLayouts ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            public MesDataInfoTree SourceData { get; }
            public IReadOnlyDictionary<string, object> SourceValues { get; }
            public int? WhileIndex { get; }
            public Dictionary<string, TreeModel> ReferencedLayouts { get; }
            public HashSet<string> ActiveReferencedLayouts { get; }

            public JsonBuildContext WithWhileIndex(int whileIndex)
            {
                return new JsonBuildContext(SourceData, SourceValues, whileIndex, ReferencedLayouts, ActiveReferencedLayouts);
            }
        }

        private static JsonBuildContext CreateJsonBuildContext(MesDataInfoTree sourceData)
        {
            Dictionary<string, object> sourceValues = new(StringComparer.Ordinal);
            foreach (MesDataInfoItem sourceItem in sourceData?.MesDataInfoItems ?? Enumerable.Empty<MesDataInfoItem>())
            {
                if (!sourceValues.ContainsKey(sourceItem.Code))
                {
                    sourceValues.Add(sourceItem.Code, sourceItem.Value);
                }
            }
            return new JsonBuildContext(sourceData, sourceValues);
        }

        /// <summary>
        /// 将布局树构建为 JSON 节点。内部始终传递 JToken，只有 Convert 入口负责最终序列化。
        /// </summary>
        private static JToken BuildJsonToken(IEnumerable<TreeModel> layout, JsonBuildContext context)
        {
            IReadOnlyList<TreeModel> items = layout as IReadOnlyList<TreeModel> ?? layout.ToList();
            if (items.Count == 1 && string.IsNullOrEmpty(items[0].MESCode))
            {
                JToken anonymousRoot = BuildJsonNode(items[0], context);
                if (anonymousRoot is JArray) return anonymousRoot;
            }

            JObject result = new();
            foreach (TreeModel item in items)
            {
                AppendJsonNode(result, item, context);
            }
            return result;
        }

        /// <summary>
        /// 将一个布局节点挂载到父对象。普通节点只产生一个属性；普通字段循环会产生多个动态属性，统一在此处分流。
        /// </summary>
        private static void AppendJsonNode(JObject parent, TreeModel node, JsonBuildContext context)
        {
            JToken token = BuildJsonNode(node, context);
            if (token == null) return;

            // 普通循环字段一次生成多个动态属性，直接合并到当前父对象；其他节点仍以 MESCode 挂载。
            if (node.IsWhile && context.WhileIndex == null && token is JObject whileFields)
            {
                foreach (JProperty property in whileFields.Properties().ToList())
                {
                    property.Remove();
                    parent.Add(property);
                }
                return;
            }
            parent.Add(node.MESCode, token);
        }

        /// <summary>
        /// 根据当前节点自身类型构建 JSON 内容，所有结构类型的判断集中在一个分派点中。
        /// </summary>
        private static JToken BuildJsonNode(TreeModel node, JsonBuildContext context)
        {
            string dataType = node.DataType?.ToUpperInvariant() ?? string.Empty;
            return dataType switch
            {
                "MODEL" when node.IsWhile && context.WhileIndex == null => BuildWhileModels(node, context),
                "MODEL" => BuildJsonModel(node.Children, context),
                "LIST" => BuildJsonList(node.Children, context),
                "ARRAY" => BuildJsonArray(node.Children, context),
                "JSON" => BuildReferencedJson(node, context),
                "STEPMODEL" => BuildStepModels(node, context),
                "STRING" when node.Children != null && node.Children.Count > 0 =>
                    new JValue(BuildJsonToken(node.Children, context).ToString(JsonFormatting.None)),
                _ when node.Children != null && node.Children.Count > 0 =>
                    BuildJsonToken(node.Children, context),
                _ when node.IsWhile && context.WhileIndex == null => BuildWhileFields(node, context),
                _ => BuildScalarJsonValue(node, context)
            };
        }

        private static JToken BuildScalarJsonValue(TreeModel node, JsonBuildContext context)
        {
            object value = ResolveSourceValue(context, node);
            if (!node.IsNull && string.IsNullOrEmpty(value?.ToString())) return null;
            return BuildJsonValue(value, node);
        }

        /// <summary>
        /// 构建外部 JSON 布局，并保持原有“作为 JSON 字符串写入当前字段”的配置语义。
        /// </summary>
        private static JToken BuildReferencedJson(TreeModel node, JsonBuildContext context)
        {
            if (!context.ActiveReferencedLayouts.Add(node.ClientCode))
            {
                throw new InvalidOperationException($"JSON 布局存在循环引用：{node.ClientCode}");
            }
            try
            {
                if (!context.ReferencedLayouts.TryGetValue(node.ClientCode, out TreeModel referencedLayout))
                {
                    referencedLayout = JsonHelper.ReadJson<TreeModel>($"{_LayoutFile}\\MESConvertConfig\\{node.ClientCode}.json");
                    context.ReferencedLayouts.Add(node.ClientCode, referencedLayout);
                }
                return new JValue(BuildJsonToken(referencedLayout.Children, context).ToString(JsonFormatting.None));
            }
            finally
            {
                context.ActiveReferencedLayouts.Remove(node.ClientCode);
            }
        }

        /// <summary>
        /// 展开普通字段循环。字段名在每轮继续递进，并将最后生成的名称保留到布局节点中。
        /// </summary>
        private static JObject BuildWhileFields(TreeModel node, JsonBuildContext context)
        {
            JObject fields = new();
            string mesCode = node.MESCode;
            for (int i = 1; i <= node.WhileCount; i++)
            {
                JsonBuildContext whileContext = context.WithWhileIndex(i);
                object value = ResolveSourceValue(whileContext, node);
                if (!node.IsNull && string.IsNullOrEmpty(value?.ToString())) continue;

                node.MESCode = GetWhileName(i, mesCode);
                fields.Add(node.MESCode, BuildJsonValue(value, node));
            }
            return fields;
        }

        /// <summary>
        /// 将普通字段转换为 JSON 值节点。该方法不再感知父对象，也不负责属性挂载。
        /// </summary>
        private static JToken BuildJsonValue(object value, TreeModel layout)
        {
            _ErrorCode = $"ClientName:{layout.ClientCode} MesName:{layout.MESCode} Value:{value}";
            if (!string.IsNullOrEmpty(layout.JudgeValue))
            {
                value = layout.JudgeValue.Equals(value) ? layout.OKText : layout.NGText;
            }

            switch (layout.DataType?.ToUpperInvariant())
            {
                case "STRING":
                    return new JValue(value?.ToString() ?? string.Empty);
                case "BOOL":
                    bool boolValue;
                    if (!bool.TryParse(value?.ToString(), out boolValue))
                    {
                        boolValue = string.Equals(value?.ToString(), layout.JudgeValue, StringComparison.OrdinalIgnoreCase);
                    }
                    return new JValue(boolValue);
                case "DATETIME":
                    if (!DateTime.TryParse(value?.ToString(), out _)) value = DateTime.Now.ToString();
                    return new JValue(System.Convert.ToDateTime(value).ToString(layout.DefectValue));
                case "TIMETICKS13":
                    if (string.IsNullOrEmpty(value?.ToString())) value = DateTime.Now.ToString();
                    DateTime time = System.Convert.ToDateTime(value);
                    DateTime startTime = TimeZone.CurrentTimeZone.ToLocalTime(new DateTime(1970, 1, 1, 0, 0, 0, 0));
                    return new JValue((time.Ticks - startTime.Ticks) / 10000);
                case "INT":
                    return new JValue(System.Convert.ToInt32(value));
                case "DOUBLE":
                    return new JValue(Math.Round(System.Convert.ToDouble(value), System.Convert.ToInt32(layout.KeepDecimalLength)));
                default:
                    return JValue.CreateNull();
            }
        }

        private static JArray BuildJsonArray(List<TreeModel> layout, JsonBuildContext context)
        {
            JArray result = new();
            foreach (TreeModel item in layout)
            {
                // 非循环字段允许通过 ClientCode 中的 [分隔符] 将一个源值展开成多个数组元素。
                if (!item.IsWhile && TryAppendDelimitedArrayValues(result, item, context))
                {
                    continue;
                }

                if (item.IsWhile && context.WhileIndex == null)
                {
                    for (int i = 1; i <= item.WhileCount; i++)
                    {
                        JToken token = BuildJsonNode(item, context.WithWhileIndex(i));
                        if (token == null) break;
                        result.Add(token);
                    }
                    continue;
                }

                JToken itemToken = BuildJsonNode(item, context);
                if (itemToken != null) result.Add(itemToken);
            }
            return result;
        }

        private static bool TryAppendDelimitedArrayValues(JArray result, TreeModel item, JsonBuildContext context)
        {
            if (item.Children.Count > 0 || string.IsNullOrEmpty(item.ClientCode)) return false;
            int openIndex = item.ClientCode.IndexOf('[');
            int closeIndex = item.ClientCode.IndexOf(']', openIndex + 1);
            if (openIndex < 0 || closeIndex != openIndex + 2) return false;

            char separator = item.ClientCode[openIndex + 1];
            string sourceCode = item.ClientCode.Remove(openIndex, closeIndex - openIndex + 1);
            object value = ResolveSourceValue(context, item, sourceCode);
            string text = value?.ToString() ?? string.Empty;
            if (!text.Contains(separator))
            {
                result.Add(BuildJsonValue(value, item));
                return true;
            }

            foreach (string part in text.Split(separator)) result.Add(part);
            return true;
        }

        private static JArray BuildJsonList(List<TreeModel> layout, JsonBuildContext context)
        {
            JArray result = new();
            foreach (TreeModel item in layout)
            {
                JToken token = BuildJsonNode(item, context);
                if (token == null) continue;

                // 循环 Model 和匿名数组节点代表多个列表元素，需要平铺到当前列表。
                if (token is JArray multipleItems && (item.IsWhile || string.IsNullOrEmpty(item.MESCode)))
                {
                    foreach (JToken child in multipleItems.Children().ToList())
                    {
                        child.Remove();
                        result.Add(child);
                    }
                    continue;
                }

                // LIST 中的普通值保持原格式，以 MESCode 包装成单属性对象。
                if (token is JValue || token is JArray)
                {
                    result.Add(new JObject { [item.MESCode ?? string.Empty] = token });
                    continue;
                }
                result.Add(token);
            }
            return result;
        }
        /// <summary>
        /// 按 WhileCount 指定的数量构建循环 Model。
        /// 每一组先完成全部字段取值和必填校验，通过后再整体写入，避免输出只有部分字段的 Model。
        /// </summary>
        private static JArray BuildWhileModels(TreeModel layout, JsonBuildContext context)
        {
            JArray models = new();
            for (int i = 1; i <= layout.WhileCount; i++)
            {
                JsonBuildContext whileContext = context.WithWhileIndex(i);
                JObject model = BuildJsonModel(layout.Children, whileContext);
                bool missingRequiredField = layout.Children.Any(field =>
                    field.Children.Count == 0 &&
                    !field.IsNull &&
                    !model.ContainsKey(field.MESCode));
                if (missingRequiredField)
                {
                    break;
                }
                models.Add(model);
            }
            return models;
        }

        private static JObject BuildJsonModel(List<TreeModel> layout, JsonBuildContext context)
        {
            JObject result = new();
            foreach (TreeModel item in layout)
            {
                AppendJsonNode(result, item, context);
            }
            return result;
        }

        /// <summary>
        /// 根据步骤名称数据构建步骤 Model 列表。特殊的步骤字段定位规则留在该结构方法内，不参与类型分派。
        /// </summary>
        private static JArray BuildStepModels(TreeModel layout, JsonBuildContext context)
        {
            JArray models = new();
            IEnumerable<MesDataInfoItem> stepNames = context.SourceData?.MesDataInfoItems?
                .Where(item => item.Code.Contains("_StepName", StringComparison.OrdinalIgnoreCase))
                ?? Enumerable.Empty<MesDataInfoItem>();
            foreach (MesDataInfoItem stepName in stepNames)
            {
                JObject model = new();
                foreach (TreeModel field in layout.Children)
                {
                    string clientCode = stepName.Code.Replace("_StepName", $"_{field.ClientCode}");
                    object value = ResolveSourceValue(context, field, clientCode);
                    if (!field.IsNull && string.IsNullOrEmpty(value?.ToString())) continue;
                    model.Add(field.MESCode, BuildJsonValue(value, field));
                }
                models.Add(model);
            }
            return models;
        }

        /// <summary>
        /// 按 ClientCode 从单次转换索引取值，并保持原有的空值回退和循环默认值规则。
        /// </summary>
        private static object ResolveSourceValue(JsonBuildContext context, TreeModel node, string clientCode = null)
        {
            string resolvedClientCode = clientCode ?? node.ClientCode ?? string.Empty;
            if (clientCode == null && context.WhileIndex.HasValue)
            {
                resolvedClientCode = GetWhileName(context.WhileIndex.Value, resolvedClientCode);
            }
            context.SourceValues.TryGetValue(resolvedClientCode, out object value);
            if (!string.IsNullOrEmpty(value?.ToString())) return value;
            if (context.WhileIndex.HasValue && !string.IsNullOrEmpty(node.DefectValue) && node.DefectValue.Contains("["))
            {
                return GetWhileName(context.WhileIndex.Value, node.DefectValue);
            }
            return node.DefectValue;
        }

        private static string GetWhileName(int index, string name)
        {
            index--;
            string result = null;
            if (name.Contains("["))
            {
                int startIndex = name.IndexOf("[") + 1;
                int length = name.IndexOf("]") - startIndex;
                string clientValue = name.Substring(startIndex, length);
                int value = System.Convert.ToInt32(clientValue) + index;
                result = name.Remove(startIndex, length).Insert(startIndex, value.ToString().PadLeft(length, '0')).Replace("[", "").Replace("]", "");
            }
            else
            {
                result = name;
            }
            return result;
        }
        private static void ValueTypeConvert(object value, TreeModel layout, ref JObject root)
        {
            JObject jsonObj = new JObject();
            _ErrorCode = $"ClientName:{layout.ClientCode} MesName:{layout.MESCode} Value:{value}";
            if (!layout.IsNull && string.IsNullOrEmpty(value?.ToString())) return;
            if (!string.IsNullOrEmpty(layout.JudgeValue))
            {
                value = layout.JudgeValue.Equals(value) ? layout.OKText : layout.NGText;
            }
            switch (layout.DataType.ToUpper())
            {
                case "STRING":
                    root.Add(layout.MESCode, (value ?? "")?.ToString());
                    break;
                case "BOOL":
                    bool result = false;
                    if (!bool.TryParse(value.ToString(), out result)) result = value.ToString().ToUpper() == layout.JudgeValue.ToUpper();
                    root.Add(layout.MESCode, result);
                    break;
                case "DATETIME":
                    if (string.IsNullOrEmpty(value?.ToString())) value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ffff");
                    root.Add(layout.MESCode, System.Convert.ToDateTime(value).ToString(layout.DefectValue));
                    break;
                case "TIMETICKS13":
                    if (string.IsNullOrEmpty(value?.ToString())) value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ffff");
                    DateTime time = System.Convert.ToDateTime(value);
                    DateTime starttime = TimeZone.CurrentTimeZone.ToLocalTime(new DateTime(1970, 1, 1, 0, 0, 0, 0));
                    long tick = (time.Ticks - starttime.Ticks) / 10000;
                    root.Add(layout.MESCode, tick);
                    break;
                case "INT":
                    root.Add(layout.MESCode, System.Convert.ToInt32(value));
                    break;
                case "DOUBLE":
                    root.Add(layout.MESCode, Math.Round(System.Convert.ToDouble(value), System.Convert.ToInt32(layout.KeepDecimalLength)));
                    break;
                case "LIST":
                    break;
                case "MODEL":
                    break;
                case "ARRAY":
                    break;
                default://null
                    root.Add(layout.MESCode, null);
                    break;
            }
        }
        private static void ValueTypeConvert(MesDataInfoTree sourceData, TreeModel layout, ref JObject root)
        {

            foreach (TreeModel item in layout.Children)
            {
                JObject jsonObj = new JObject();
                object value = sourceData?.MesDataInfoItems?.FirstOrDefault(r => r.Code == item.ClientCode)?.Value;
                _ErrorCode = $"ClientName:{layout.ClientCode} MesName:{layout.MESCode} Value:{value}";
                if (!layout.IsNull && string.IsNullOrEmpty(value?.ToString())) return;
                if (!string.IsNullOrEmpty(layout.JudgeValue))
                {
                    value = layout.JudgeValue.Equals(value) ? layout.OKText : layout.NGText;
                }
                switch (layout.DataType.ToUpper())
                {
                    case "STRING":
                        root.Add(layout.MESCode, (value ?? "")?.ToString());
                        break;
                    case "BOOL":
                        bool result = false;
                        if (!bool.TryParse(value.ToString(), out result)) result = value.ToString().ToUpper() == layout.JudgeValue.ToUpper();
                        root.Add(layout.MESCode, result);
                        break;
                    case "DATETIME":
                        if (string.IsNullOrEmpty(value?.ToString())) value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ffff");
                        root.Add(layout.MESCode, System.Convert.ToDateTime(value).ToString(layout.DefectValue));
                        break;
                    case "TIMETICKS13":
                        if (string.IsNullOrEmpty(value?.ToString())) value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ffff");
                        DateTime time = System.Convert.ToDateTime(value);
                        DateTime starttime = TimeZone.CurrentTimeZone.ToLocalTime(new DateTime(1970, 1, 1, 0, 0, 0, 0));
                        long tick = (time.Ticks - starttime.Ticks) / 10000;
                        root.Add(layout.MESCode, tick);
                        break;
                    case "INT":
                        root.Add(layout.MESCode, System.Convert.ToInt32(value));
                        break;
                    case "DOUBLE":
                        root.Add(layout.MESCode, Math.Round(System.Convert.ToDouble(value), System.Convert.ToInt32(layout.KeepDecimalLength)));
                        break;
                    case "LIST":
                        JArray jArray = new JArray();
                        ValueTypeConvert(sourceData, item, ref jsonObj);
                        jArray.Add(jsonObj);
                        root.Add(item.MESCode ?? "", jArray);
                        break;
                    case "MODEL":
                        ValueTypeConvert(sourceData, item, ref jsonObj);
                        root.Add(item.MESCode ?? "", jsonObj);
                        break;
                    case "ARRAY":
                        break;
                    default://null
                        root.Add(layout.MESCode, null);
                        break;
                }
            }
        }
        #endregion
        #region SOAP
        private sealed class SoapBuildContext
        {
            public SoapBuildContext(MesDataInfoTree sourceData, IReadOnlyDictionary<string, object> sourceValues, XNamespace defaultNamespace,
                int? whileIndex = null, Dictionary<string, TreeModel> referencedLayouts = null, HashSet<string> activeReferencedLayouts = null)
            {
                SourceData = sourceData;
                SourceValues = sourceValues;
                DefaultNamespace = defaultNamespace;
                WhileIndex = whileIndex;
                ReferencedLayouts = referencedLayouts ?? new Dictionary<string, TreeModel>(StringComparer.OrdinalIgnoreCase);
                ActiveReferencedLayouts = activeReferencedLayouts ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            public MesDataInfoTree SourceData { get; }
            public IReadOnlyDictionary<string, object> SourceValues { get; }
            public XNamespace DefaultNamespace { get; }
            public int? WhileIndex { get; }
            public Dictionary<string, TreeModel> ReferencedLayouts { get; }
            public HashSet<string> ActiveReferencedLayouts { get; }

            public SoapBuildContext WithDefaultNamespace(XNamespace defaultNamespace)
            {
                return new SoapBuildContext(SourceData, SourceValues, defaultNamespace, WhileIndex, ReferencedLayouts, ActiveReferencedLayouts);
            }

            public SoapBuildContext WithWhileIndex(int whileIndex)
            {
                return new SoapBuildContext(SourceData, SourceValues, DefaultNamespace, whileIndex, ReferencedLayouts, ActiveReferencedLayouts);
            }
        }

        private static SoapBuildContext CreateSoapBuildContext(MesDataInfoTree sourceData)
        {
            Dictionary<string, object> sourceValues = new(StringComparer.Ordinal);
            foreach (MesDataInfoItem sourceItem in sourceData?.MesDataInfoItems ?? Enumerable.Empty<MesDataInfoItem>())
            {
                if (!sourceValues.ContainsKey(sourceItem.Code)) sourceValues.Add(sourceItem.Code, sourceItem.Value);
            }
            return new SoapBuildContext(sourceData, sourceValues, XNamespace.None);
        }

        /// <summary>
        /// 构建 SOAP/XML 根节点，后续所有子节点都通过 AppendSoapNode 进入同一结构分派流程。
        /// </summary>
        private static XElement BuildSoapDocument(TreeModel rootLayout, SoapBuildContext context)
        {
            XNamespace rootNamespace = ResolveSoapNamespace(rootLayout, context);
            XElement root = new(rootNamespace + rootLayout.MESCode);
            SoapBuildContext childContext = context.WithDefaultNamespace(rootNamespace);
            foreach (TreeModel child in rootLayout.Children)
            {
                AppendSoapNode(root, child, childContext);
            }
            return root;
        }

        /// <summary>
        /// SOAP 结构的唯一分派点。结构方法只负责自己的 XML 语义，不再重复判断全部 DataType。
        /// </summary>
        private static void AppendSoapNode(XElement parent, TreeModel node, SoapBuildContext context)
        {
            string dataType = node.DataType?.ToUpperInvariant() ?? string.Empty;
            switch (dataType)
            {
                case "MODEL" when node.IsWhile && context.WhileIndex == null:
                    foreach (XElement model in BuildWhileSoapModels(node, context)) parent.Add(model);
                    return;
                case "MODEL":
                    parent.Add(BuildSoapModel(node, context));
                    return;
                case "LIST":
                    parent.Add(BuildSoapList(node, context));
                    return;
                case "XMLNAMESPACE":
                    AddSoapNamespaceDeclaration(parent, node);
                    return;
                case "JSON":
                    parent.Add(BuildReferencedSoap(node, context));
                    return;
                default:
                    XElement valueElement = BuildSoapValueElement(node, context);
                    if (valueElement != null) parent.Add(valueElement);
                    return;
            }
        }

        private static XElement BuildSoapModel(TreeModel model, SoapBuildContext context)
        {
            XNamespace nodeNamespace = ResolveSoapNamespace(model, context);
            XElement element = new(nodeNamespace + model.MESCode);
            SoapBuildContext childContext = context.WithDefaultNamespace(nodeNamespace);
            foreach (TreeModel child in model.Children) AppendSoapNode(element, child, childContext);
            return element;
        }

        private static XElement BuildSoapList(TreeModel list, SoapBuildContext context)
        {
            XNamespace nodeNamespace = ResolveSoapNamespace(list, context);
            XElement element = new(nodeNamespace + list.MESCode);
            SoapBuildContext childContext = context.WithDefaultNamespace(nodeNamespace);
            foreach (TreeModel child in list.Children) AppendSoapNode(element, child, childContext);
            return element;
        }

        private static IEnumerable<XElement> BuildWhileSoapModels(TreeModel model, SoapBuildContext context)
        {
            for (int i = 1; i <= model.WhileCount; i++)
            {
                SoapBuildContext whileContext = context.WithWhileIndex(i);
                bool missingRequiredField = model.Children.Any(child =>
                    child.Children.Count == 0 &&
                    !child.IsNull &&
                    string.IsNullOrEmpty(ResolveSoapValue(child, whileContext)?.ToString()));
                if (missingRequiredField) yield break;
                yield return BuildSoapModel(model, whileContext);
            }
        }

        private static XElement BuildSoapValueElement(TreeModel node, SoapBuildContext context)
        {
            object value = ResolveSoapValue(node, context);
            if (!node.IsNull && string.IsNullOrEmpty(value?.ToString())) return null;
            XNamespace nodeNamespace = ResolveSoapNamespace(node, context);
            return new XElement(nodeNamespace + node.MESCode, ConvertSoapValue(value, node));
        }

        private static object ConvertSoapValue(object value, TreeModel node)
        {
            _ErrorCode = $"ClientName:{node.ClientCode} MESName:{node.MESCode} Value:{value}";
            if (!string.IsNullOrEmpty(node.JudgeValue))
            {
                value = string.Equals(node.JudgeValue, value?.ToString(), StringComparison.OrdinalIgnoreCase)
                    ? node.OKText
                    : node.NGText;
            }
            return node.DataType?.ToUpperInvariant() switch
            {
                "STRING" => value?.ToString() ?? string.Empty,
                "INT" => System.Convert.ToInt32(value),
                "DOUBLE" or "DOUBULE" => Math.Round(System.Convert.ToDouble(value), System.Convert.ToInt32(node.KeepDecimalLength)),
                "DATETIME" => System.Convert.ToDateTime(string.IsNullOrEmpty(value?.ToString()) ? DateTime.Now : value).ToString(node.DefectValue),
                _ => value?.ToString() ?? string.Empty
            };
        }

        private static XElement BuildReferencedSoap(TreeModel node, SoapBuildContext context)
        {
            if (!context.ActiveReferencedLayouts.Add(node.ClientCode))
            {
                throw new InvalidOperationException($"SOAP 引用布局存在循环引用：{node.ClientCode}");
            }
            try
            {
                if (!context.ReferencedLayouts.TryGetValue(node.ClientCode, out TreeModel referencedLayout))
                {
                    referencedLayout = JsonHelper.ReadJson<TreeModel>($"{_LayoutFile}\\MESConvertConfig\\{node.ClientCode}.json");
                    context.ReferencedLayouts.Add(node.ClientCode, referencedLayout);
                }
                JToken json = BuildJsonToken(referencedLayout.Children, CreateJsonBuildContext(context.SourceData));
                return new XElement(ResolveSoapNamespace(node, context) + node.MESCode, json.ToString(JsonFormatting.None));
            }
            finally
            {
                context.ActiveReferencedLayouts.Remove(node.ClientCode);
            }
        }

        private static object ResolveSoapValue(TreeModel node, SoapBuildContext context)
        {
            string clientCode = node.ClientCode ?? string.Empty;
            if (context.WhileIndex.HasValue) clientCode = GetWhileName(context.WhileIndex.Value, clientCode);
            context.SourceValues.TryGetValue(clientCode, out object value);
            if (!string.IsNullOrEmpty(value?.ToString())) return value;
            if (context.WhileIndex.HasValue && !string.IsNullOrEmpty(node.DefectValue) && node.DefectValue.Contains("["))
            {
                return GetWhileName(context.WhileIndex.Value, node.DefectValue);
            }
            return node.DefectValue;
        }

        private static XNamespace ResolveSoapNamespace(TreeModel node, SoapBuildContext context)
        {
            return string.IsNullOrWhiteSpace(node.XMLNameSpace) ? context.DefaultNamespace : node.XMLNameSpace;
        }

        private static void AddSoapNamespaceDeclaration(XElement parent, TreeModel node)
        {
            if (string.IsNullOrWhiteSpace(node.MESCode) || string.IsNullOrWhiteSpace(node.XMLNameSpace)) return;
            parent.Add(new XAttribute(XNamespace.Xmlns + node.MESCode, node.XMLNameSpace));
        }
        #endregion
        #region JOINT
        private static string ItemsToString(MesDataInfoTree sourceData, IEnumerable<TreeModel> layout)
        {
            StringBuilder sb = new StringBuilder();
            foreach (TreeModel item in layout)
            {
                if (item.IsWhile)
                {
                    string data = null;
                    for (int i = 1; i <= item.WhileCount; i++)
                    {
                        var v = sourceData?.MesDataInfoItems?.FirstOrDefault(r => r.Code == $"{item.ClientCode.Replace("_", $"{i}_")}")?.Value;
                        if (string.IsNullOrEmpty(v?.ToString()))
                        {
                            v = !string.IsNullOrEmpty(item.DefectValue) ? $"{item.DefectValue}" : item.DefectValue;
                        }
                        if (!item.IsNull && string.IsNullOrEmpty(v?.ToString())) continue;
                        data += $"{v?.ToString()}{item.MESCode}";
                    }
                    if (data != null)
                        sb.Append(data.Substring(0, data.Length - item.MESCode.Length));
                }
                else if (item.DataType.ToUpper().Contains("DATETIME"))
                {
                    var v = sourceData?.MesDataInfoItems?.FirstOrDefault(r => r.Code == item.ClientCode)?.Value;
                    if (!item.IsNull && string.IsNullOrEmpty(v?.ToString())) continue;
                    sb.Append(System.Convert.ToDateTime(v).ToString(item.DefectValue)?.ToString() + item.MESCode);
                }
                else
                {
                    var v = sourceData?.MesDataInfoItems?.FirstOrDefault(r => r.Code == item.ClientCode)?.Value;
                    if (string.IsNullOrEmpty(v?.ToString())) v = item.DefectValue;
                    if (!item.IsNull && string.IsNullOrEmpty(v?.ToString())) continue;
                    if (!string.IsNullOrEmpty(item.JudgeValue))
                    {
                        v = item.JudgeValue.Equals(v) ? item.OKText : item.NGText;
                    }
                    sb.Append(v?.ToString() + item.MESCode);
                }
            }
            return sb.ToString();
        }
        #endregion
    }
}







