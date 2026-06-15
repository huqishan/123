using Module.MES.Configuration;
using Module.MES.Features.DataStructureConfig.ViewModels.PresentationModels;
using Shared.Infrastructure.PackMethod;
using System.IO;
using System.Text;

namespace Module.MES.Features.DataStructureConfig.Services;

public sealed class DataStructureStore
{
    public string ConfigDirectory => MesConfigRegistry.DataStructureConfigDirectory;

    public IEnumerable<string> EnumerateConfigFiles()
    {
        return Directory.Exists(ConfigDirectory)
            ? Directory.EnumerateFiles(ConfigDirectory, "*.json").OrderBy(Path.GetFileName)
            : Enumerable.Empty<string>();
    }

    public DataStructureProfileDocument? Load(string filePath)
    {
        string storageText = File.ReadAllText(filePath, Encoding.UTF8);
        return JsonHelper.DeserializeObject<DataStructureProfileDocument>(storageText);
    }

    public void Save(DataStructureProfileDocument document, string fileName)
    {
        Directory.CreateDirectory(ConfigDirectory);
        JsonHelper.SaveJson(document, Path.Combine(ConfigDirectory, fileName));
    }
}
