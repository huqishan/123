using Module.MES.Configuration;
using Shared.Infrastructure.PackMethod;
using Shared.Models.MES;
using System.IO;

namespace Module.MES.Features.SystemConfig.Services;

public sealed class MesSystemConfigStore
{
    public string ConfigFilePath => MesConfigRegistry.MesSystemConfigFilePath;

    public MesSystemConfig LoadOrDefault()
    {
        try
        {
            return JsonHelper.ReadJson<MesSystemConfig>(ConfigFilePath) ?? new MesSystemConfig();
        }
        catch
        {
            return new MesSystemConfig();
        }
    }

    public void Save(MesSystemConfig config)
    {
        Directory.CreateDirectory(MesConfigRegistry.MesSystemConfigDirectory);
        JsonHelper.SaveJson(config, ConfigFilePath);
    }
}
