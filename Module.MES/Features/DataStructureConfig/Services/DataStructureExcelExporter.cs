using Module.MES.Features.DataStructureConfig.ViewModels.PresentationModels;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace Module.MES.Features.DataStructureConfig.Services;

/// <summary>
/// 将数据结构配置导出为标准 XLSX 工作簿，不依赖本机安装 Excel，也不引入第三方组件。
/// </summary>
internal static class DataStructureExcelExporter
{
    #region Excel 固定配置

    private const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly string[] StructureHeaders =
    {
        "结构字段", "数据类型", "客户端字段", "MES字段", "默认值", "循环数量", "允许空值",
        "保留小数", "XML命名空间", "判断值", "OK文本", "NG文本"
    };

    #endregion

    #region 导出入口

    /// <summary>
    /// 创建包含“基本信息”和“结构树”两个工作表的 XLSX 文件。
    /// 临时文件完整写入后再替换目标文件，避免异常时留下损坏的工作簿。
    /// </summary>
    public static void Export(DataStructureProfile profile, string filePath)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("导出路径不能为空。", nameof(filePath));

        string fullPath = Path.GetFullPath(filePath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

        string temporaryPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (ZipArchive archive = new(stream, ZipArchiveMode.Create))
            {
                WriteTextEntry(archive, "[Content_Types].xml", ContentTypesXml);
                WriteTextEntry(archive, "_rels/.rels", PackageRelationshipsXml);
                WriteTextEntry(archive, "xl/workbook.xml", WorkbookXml);
                WriteTextEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationshipsXml);
                WriteTextEntry(archive, "xl/styles.xml", StylesXml);
                WriteBasicInformationWorksheet(archive, profile);
                WriteStructureWorksheet(archive, profile);
            }

            File.Move(temporaryPath, fullPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    #endregion

    #region 工作表内容

    /// <summary>
    /// 基本信息单独成表，让结构树只保留字段内容，打开工作簿时可以直接阅读结构。
    /// </summary>
    private static void WriteBasicInformationWorksheet(ZipArchive archive, DataStructureProfile profile)
    {
        ZipArchiveEntry entry = archive.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Optimal);
        using Stream stream = entry.Open();
        using XmlWriter writer = CreateXmlWriter(stream);

        writer.WriteStartDocument();
        writer.WriteStartElement("worksheet", SpreadsheetNamespace);
        writer.WriteStartElement("cols", SpreadsheetNamespace);
        WriteColumn(writer, 1, 1, 18);
        WriteColumn(writer, 2, 2, 42);
        writer.WriteEndElement();
        writer.WriteStartElement("sheetData", SpreadsheetNamespace);
        WriteRow(writer, 1, new[] { "项目", "内容" }, 1);
        WriteRow(writer, 2, new[] { "结构名称", profile.Name }, 0);
        WriteRow(writer, 3, new[] { "结构类型", profile.StructureType }, 0);
        WriteRow(writer, 4, new[] { "最后修改时间", profile.LastModifiedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) }, 0);
        WriteRow(writer, 5, new[] { "字段数量", CountFields(profile.Structure).ToString(CultureInfo.InvariantCulture) }, 0);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    /// <summary>
    /// 结构树采用先序排列：父节点在上、子节点紧随其后。首列显示缩进和节点标识，Excel 行分组负责折叠层级。
    /// MODEL、LIST/ARRAY 和普通字段使用不同底色，便于快速辨认结构节点。
    /// </summary>
    private static void WriteStructureWorksheet(ZipArchive archive, DataStructureProfile profile)
    {
        ZipArchiveEntry entry = archive.CreateEntry("xl/worksheets/sheet2.xml", CompressionLevel.Optimal);
        using Stream stream = entry.Open();
        using XmlWriter writer = CreateXmlWriter(stream);

        writer.WriteStartDocument();
        writer.WriteStartElement("worksheet", SpreadsheetNamespace);
        writer.WriteStartElement("sheetPr", SpreadsheetNamespace);
        writer.WriteStartElement("outlinePr", SpreadsheetNamespace);
        writer.WriteAttributeString("summaryBelow", "0");
        writer.WriteEndElement();
        writer.WriteEndElement();
        WriteFrozenHeaderView(writer);
        writer.WriteStartElement("cols", SpreadsheetNamespace);
        WriteColumn(writer, 1, 1, 42);
        WriteColumn(writer, 2, 5, 18);
        WriteColumn(writer, 6, 8, 12);
        WriteColumn(writer, 9, 12, 22);
        writer.WriteEndElement();
        writer.WriteStartElement("sheetFormatPr", SpreadsheetNamespace);
        writer.WriteAttributeString("defaultRowHeight", "18");
        writer.WriteAttributeString("outlineLevelRow", "7");
        writer.WriteEndElement();
        writer.WriteStartElement("sheetData", SpreadsheetNamespace);
        WriteRow(writer, 1, StructureHeaders, 1);

        int rowNumber = 2;
        foreach ((DataStructureLayout Field, int Level) item in Flatten(profile.Structure, 0))
        {
            DataStructureLayout field = item.Field;
            bool hasChildren = field.Children.Count > 0;
            string displayName = string.IsNullOrWhiteSpace(field.MesCode) ? field.ClientCode : field.MesCode;
            string treeName = $"{new string('　', item.Level)}{(hasChildren ? "▼ " : "• ")}{displayName}";
            WriteRow(writer, rowNumber++, new[]
            {
                treeName,
                field.DataType,
                field.ClientCode,
                field.MesCode,
                field.DefaultValue,
                field.WhileCount > 0 ? field.WhileCount.ToString(CultureInfo.InvariantCulture) : string.Empty,
                field.IsNull ? "是" : "否",
                field.KeepCount > 0 ? field.KeepCount.ToString(CultureInfo.InvariantCulture) : string.Empty,
                field.XmlNamespace,
                field.JudgeValue,
                field.OKText,
                field.NGText
            }, ResolveFieldStyle(field), Math.Min(item.Level, 7));
        }

        writer.WriteEndElement();
        writer.WriteStartElement("autoFilter", SpreadsheetNamespace);
        writer.WriteAttributeString("ref", $"A1:L{Math.Max(1, rowNumber - 1)}");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static IEnumerable<(DataStructureLayout Field, int Level)> Flatten(
        IEnumerable<DataStructureLayout> fields,
        int level)
    {
        foreach (DataStructureLayout field in fields)
        {
            yield return (field, level);
            foreach ((DataStructureLayout Field, int Level) child in Flatten(field.Children, level + 1))
            {
                yield return child;
            }
        }
    }

    private static int CountFields(IEnumerable<DataStructureLayout> fields)
    {
        int count = 0;
        foreach (DataStructureLayout field in fields)
        {
            count++;
            count += CountFields(field.Children);
        }
        return count;
    }

    private static int ResolveFieldStyle(DataStructureLayout field)
    {
        return field.DataType?.ToUpperInvariant() switch
        {
            "MODEL" => 2,
            "LIST" or "ARRAY" => 3,
            _ when field.Children.Count > 0 => 4,
            _ => 0
        };
    }

    #endregion

    #region Excel XML 写入

    private static XmlWriter CreateXmlWriter(Stream stream)
    {
        return XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true });
    }

    private static void WriteFrozenHeaderView(XmlWriter writer)
    {
        writer.WriteStartElement("sheetViews", SpreadsheetNamespace);
        writer.WriteStartElement("sheetView", SpreadsheetNamespace);
        writer.WriteAttributeString("workbookViewId", "0");
        writer.WriteStartElement("pane", SpreadsheetNamespace);
        writer.WriteAttributeString("ySplit", "1");
        writer.WriteAttributeString("topLeftCell", "A2");
        writer.WriteAttributeString("activePane", "bottomLeft");
        writer.WriteAttributeString("state", "frozen");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteRow(
        XmlWriter writer,
        int rowNumber,
        IReadOnlyList<string> values,
        int styleIndex,
        int outlineLevel = 0)
    {
        writer.WriteStartElement("row", SpreadsheetNamespace);
        writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));
        if (outlineLevel > 0) writer.WriteAttributeString("outlineLevel", outlineLevel.ToString(CultureInfo.InvariantCulture));
        for (int columnIndex = 0; columnIndex < values.Count; columnIndex++)
        {
            writer.WriteStartElement("c", SpreadsheetNamespace);
            writer.WriteAttributeString("r", $"{GetColumnName(columnIndex + 1)}{rowNumber}");
            writer.WriteAttributeString("t", "inlineStr");
            if (styleIndex > 0) writer.WriteAttributeString("s", styleIndex.ToString(CultureInfo.InvariantCulture));
            writer.WriteStartElement("is", SpreadsheetNamespace);
            writer.WriteStartElement("t", SpreadsheetNamespace);
            writer.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
            writer.WriteString(values[columnIndex] ?? string.Empty);
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private static void WriteColumn(XmlWriter writer, int min, int max, double width)
    {
        writer.WriteStartElement("col", SpreadsheetNamespace);
        writer.WriteAttributeString("min", min.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("max", max.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("width", width.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("customWidth", "1");
        writer.WriteEndElement();
    }

    private static string GetColumnName(int columnNumber)
    {
        StringBuilder name = new();
        while (columnNumber > 0)
        {
            columnNumber--;
            name.Insert(0, (char)('A' + columnNumber % 26));
            columnNumber /= 26;
        }
        return name.ToString();
    }

    #endregion

    #region XLSX 包定义

    private static void WriteTextEntry(ZipArchive archive, string path, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using StreamWriter writer = new(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private const string ContentTypesXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
          <Override PartName="/xl/worksheets/sheet2.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
          <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
        </Types>
        """;

    private const string PackageRelationshipsXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
        </Relationships>
        """;

    private const string WorkbookXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <sheets>
            <sheet name="基本信息" sheetId="1" r:id="rId1"/>
            <sheet name="结构树" sheetId="2" r:id="rId2"/>
          </sheets>
        </workbook>
        """;

    private const string WorkbookRelationshipsXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet2.xml"/>
          <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
        </Relationships>
        """;

    private const string StylesXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <fonts count="2"><font><sz val="11"/><name val="微软雅黑"/></font><font><b/><sz val="11"/><color rgb="FFFFFFFF"/><name val="微软雅黑"/></font></fonts>
          <fills count="6"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FF2563EB"/><bgColor indexed="64"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FFDBEAFE"/><bgColor indexed="64"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FFDCFCE7"/><bgColor indexed="64"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FFFFEDD5"/><bgColor indexed="64"/></patternFill></fill></fills>
          <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
          <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
          <cellXfs count="5"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1"/><xf numFmtId="0" fontId="0" fillId="3" borderId="0" xfId="0" applyFill="1"/><xf numFmtId="0" fontId="0" fillId="4" borderId="0" xfId="0" applyFill="1"/><xf numFmtId="0" fontId="0" fillId="5" borderId="0" xfId="0" applyFill="1"/></cellXfs>
        </styleSheet>
        """;

    #endregion
}
