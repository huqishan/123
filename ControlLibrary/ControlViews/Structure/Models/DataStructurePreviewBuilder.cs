using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;

namespace ControlLibrary.ControlViews.Structure.Models
{
    internal static class DataStructurePreviewBuilder
    {
        private static readonly JsonSerializerOptions PreviewJsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public static bool TryBuildPreview(DataStructureProfile profile, out string previewText, out string message)
        {
            previewText = string.Empty;

            if (string.IsNullOrWhiteSpace(profile.Name))
            {
                message = "配置名称不能为空。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(profile.RootName))
            {
                message = "根节点名称不能为空。";
                return false;
            }

            return profile.Format switch
            {
                DataStructureFormat.Json => TryBuildJsonPreview(profile, out previewText, out message),
                DataStructureFormat.Xml => TryBuildXmlPreview(profile, out previewText, out message),
                _ => Fail("暂不支持当前数据结构格式。", out previewText, out message)
            };
        }

        private static bool TryBuildJsonPreview(DataStructureProfile profile, out string previewText, out string message)
        {
            JsonObject root = new JsonObject();
            HashSet<string> usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (DataStructureFieldConfig field in profile.Fields)
            {
                string propertyName = field.ResolveOutputName();
                if (string.IsNullOrWhiteSpace(propertyName))
                {
                    return Fail("JSON 字段名称不能为空，请检查 ClientCode 或字段名称。", out previewText, out message);
                }

                if (!usedNames.Add(propertyName))
                {
                    return Fail($"JSON 字段名称重复：{propertyName}。", out previewText, out message);
                }

                if (!TryBuildJsonValue(field, out JsonNode? jsonValue, out string errorMessage))
                {
                    return Fail(errorMessage, out previewText, out message);
                }

                root[propertyName] = jsonValue;
            }

            JsonObject payload = new JsonObject
            {
                [profile.RootName.Trim()] = root
            };

            previewText = payload.ToJsonString(PreviewJsonOptions);
            message = $"已生成 JSON 结构预览，共 {profile.Fields.Count} 个字段。";
            return true;
        }

        private static bool TryBuildXmlPreview(DataStructureProfile profile, out string previewText, out string message)
        {
            string rootName = profile.RootName.Trim();
            XNamespace xNamespace = string.IsNullOrWhiteSpace(profile.XmlNamespace)
                ? XNamespace.None
                : profile.XmlNamespace.Trim();

            XElement rootElement;
            try
            {
                rootElement = new XElement(xNamespace + XmlConvert.VerifyName(rootName));
            }
            catch (Exception ex) when (ex is XmlException or ArgumentException)
            {
                return Fail($"XML 根节点名称无效：{rootName}。{ex.Message}", out previewText, out message);
            }

            HashSet<string> usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataStructureFieldConfig field in profile.Fields)
            {
                string elementName = field.ResolveOutputName();
                if (string.IsNullOrWhiteSpace(elementName))
                {
                    return Fail("XML 节点名称不能为空，请检查 ClientCode 或字段名称。", out previewText, out message);
                }

                if (!usedNames.Add(elementName))
                {
                    return Fail($"XML 节点名称重复：{elementName}。", out previewText, out message);
                }

                try
                {
                    XElement element = new XElement(
                        xNamespace + XmlConvert.VerifyName(elementName),
                        field.DefaultValue ?? string.Empty);

                    if (!string.IsNullOrWhiteSpace(field.MesCode))
                    {
                        element.SetAttributeValue("mesCode", field.MesCode.Trim());
                    }

                    if (!string.IsNullOrWhiteSpace(field.ClientCode))
                    {
                        element.SetAttributeValue("clientCode", field.ClientCode.Trim());
                    }

                    if (!string.IsNullOrWhiteSpace(field.DataType))
                    {
                        element.SetAttributeValue("dataType", field.DataType.Trim());
                    }

                    rootElement.Add(element);
                }
                catch (Exception ex) when (ex is XmlException or ArgumentException)
                {
                    return Fail($"XML 节点名称无效：{elementName}。{ex.Message}", out previewText, out message);
                }
            }

            XDocument document = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), rootElement);
            previewText = document.ToString();
            message = $"已生成 XML 结构预览，共 {profile.Fields.Count} 个字段。";
            return true;
        }

        private static bool TryBuildJsonValue(DataStructureFieldConfig field, out JsonNode? jsonValue, out string message)
        {
            string rawValue = field.DefaultValue?.Trim() ?? string.Empty;

            switch (field.JsonValueKind)
            {
                case JsonFieldValueKind.String:
                    jsonValue = JsonValue.Create(field.DefaultValue ?? string.Empty);
                    message = string.Empty;
                    return true;
                case JsonFieldValueKind.Integer:
                    if (string.IsNullOrWhiteSpace(rawValue))
                    {
                        jsonValue = JsonValue.Create(0);
                        message = string.Empty;
                        return true;
                    }

                    if (long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integerValue))
                    {
                        jsonValue = JsonValue.Create(integerValue);
                        message = string.Empty;
                        return true;
                    }

                    jsonValue = null;
                    message = $"字段 {field.Name} 的默认值不是有效整数。";
                    return false;
                case JsonFieldValueKind.Number:
                    if (string.IsNullOrWhiteSpace(rawValue))
                    {
                        jsonValue = JsonValue.Create(0d);
                        message = string.Empty;
                        return true;
                    }

                    if (double.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double numberValue))
                    {
                        jsonValue = JsonValue.Create(numberValue);
                        message = string.Empty;
                        return true;
                    }

                    jsonValue = null;
                    message = $"字段 {field.Name} 的默认值不是有效数字。";
                    return false;
                case JsonFieldValueKind.Boolean:
                    if (string.IsNullOrWhiteSpace(rawValue))
                    {
                        jsonValue = JsonValue.Create(false);
                        message = string.Empty;
                        return true;
                    }

                    if (TryParseBoolean(rawValue, out bool booleanValue))
                    {
                        jsonValue = JsonValue.Create(booleanValue);
                        message = string.Empty;
                        return true;
                    }

                    jsonValue = null;
                    message = $"字段 {field.Name} 的默认值不是有效布尔值。";
                    return false;
                case JsonFieldValueKind.Object:
                    if (string.IsNullOrWhiteSpace(rawValue))
                    {
                        jsonValue = new JsonObject();
                        message = string.Empty;
                        return true;
                    }

                    return TryParseRawJsonNode(field, rawValue, JsonValueKind.Object, out jsonValue, out message);
                case JsonFieldValueKind.Array:
                    if (string.IsNullOrWhiteSpace(rawValue))
                    {
                        jsonValue = new JsonArray();
                        message = string.Empty;
                        return true;
                    }

                    return TryParseRawJsonNode(field, rawValue, JsonValueKind.Array, out jsonValue, out message);
                case JsonFieldValueKind.Null:
                    jsonValue = null;
                    message = string.Empty;
                    return true;
                default:
                    jsonValue = null;
                    message = "暂不支持当前 JSON 字段类型。";
                    return false;
            }
        }

        private static bool TryParseRawJsonNode(
            DataStructureFieldConfig field,
            string rawValue,
            JsonValueKind expectedKind,
            out JsonNode? jsonValue,
            out string message)
        {
            try
            {
                jsonValue = JsonNode.Parse(rawValue);
                if (jsonValue is null || jsonValue.GetValueKind() != expectedKind)
                {
                    message = $"字段 {field.Name} 的默认值与所选 JSON 类型不匹配。";
                    return false;
                }

                message = string.Empty;
                return true;
            }
            catch (JsonException ex)
            {
                jsonValue = null;
                message = $"字段 {field.Name} 的默认值不是有效 JSON：{ex.Message}";
                return false;
            }
        }

        private static bool TryParseBoolean(string rawValue, out bool value)
        {
            switch (rawValue.Trim().ToLowerInvariant())
            {
                case "true":
                case "1":
                case "yes":
                case "y":
                    value = true;
                    return true;
                case "false":
                case "0":
                case "no":
                case "n":
                    value = false;
                    return true;
                default:
                    value = false;
                    return false;
            }
        }

        private static bool Fail(string errorMessage, out string previewText, out string message)
        {
            previewText = string.Empty;
            message = errorMessage;
            return false;
        }
    }
}
