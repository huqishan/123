using Module.MES.Configuration;
using System.IO;
using Shared.Infrastructure.PackMethod;
using Shared.Models.MES;

namespace Module.MES.Features.ApiConfig.Services;

public sealed class ApiConfigStore
{
    public string ConfigDirectory => MesConfigRegistry.ApiConfigDirectory;

    public IEnumerable<string> EnumerateConfigFiles()
    {
        return Directory.Exists(ConfigDirectory)
            ? Directory.EnumerateFiles(ConfigDirectory, "*.json").OrderBy(Path.GetFileName)
            : Enumerable.Empty<string>();
    }

    public APIConfig? Load(string filePath)
    {
        return JsonHelper.ReadJson<APIConfig>(filePath);
    }

    public void Save(APIConfig config, string fileName)
    {
        Directory.CreateDirectory(ConfigDirectory);
        JsonHelper.SaveJson(config, Path.Combine(ConfigDirectory, fileName));
    }
}
