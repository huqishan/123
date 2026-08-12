using Module.MES.Features.DataStructureConfig.Services;
using Module.MES.Features.DataStructureConfig.ViewModels.PresentationModels;
using System.IO;
using System.IO.Compression;

namespace TestProject;

/// <summary>
/// 数据结构 Excel 导入导出回归测试，确保工作簿包结构有效并且树形配置可以完整往返。
/// </summary>
public class DataStructureExcelTests
{
    #region Excel 往返测试

    [Test]
    public void ExportAndImport_嵌套结构_应完整还原字段配置和父子关系()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"DataStructure-{Guid.NewGuid():N}.xlsx");
        try
        {
            DataStructureProfile profile = new()
            {
                Name = "StationOut",
                StructureType = "JSON"
            };
            DataStructureLayout root = new()
            {
                // LIST、ARRAY 等结构允许使用匿名根节点，导入时不能因为两个字段名为空就跳过该节点。
                ClientCode = string.Empty,
                MesCode = string.Empty,
                DataType = "MODEL"
            };
            root.Children.Add(new DataStructureLayout
            {
                ClientCode = "Value[01]",
                MesCode = "Value",
                DataType = "STRING",
                DefaultValue = "Default",
                WhileCount = 2,
                IsNull = true,
                KeepCount = 3,
                JudgeValue = "1",
                OKText = "OK",
                NGText = "NG"
            });
            profile.Structure.Add(root);

            DataStructureExcelExporter.Export(profile, filePath);
            DataStructureExcelImportResult imported = DataStructureExcelImporter.Import(filePath);

            Assert.Multiple(() =>
            {
                Assert.That(imported.Name, Is.EqualTo("StationOut"));
                Assert.That(imported.StructureType, Is.EqualTo("JSON"));
                Assert.That(imported.Structure, Has.Count.EqualTo(1));
                Assert.That(imported.Structure[0].DataType, Is.EqualTo("Model"));
                Assert.That(imported.Structure[0].Children, Has.Count.EqualTo(1));
                Assert.That(imported.Structure[0].Children[0].ClientCode, Is.EqualTo("Value[01]"));
                Assert.That(imported.Structure[0].Children[0].WhileCount, Is.EqualTo(2));
                Assert.That(imported.Structure[0].Children[0].IsNull, Is.True);
                Assert.That(imported.Structure[0].Children[0].OKText, Is.EqualTo("OK"));
            });

            using ZipArchive archive = ZipFile.OpenRead(filePath);
            Assert.That(archive.GetEntry("xl/worksheets/sheet1.xml"), Is.Not.Null);
            Assert.That(archive.GetEntry("xl/worksheets/sheet2.xml"), Is.Not.Null);
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    #endregion
}
