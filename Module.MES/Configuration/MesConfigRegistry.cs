using System;
using System.IO;

namespace Module.MES.Configuration;

public static class MesConfigRegistry
{
    public static string RootDirectory { get; } =
        Path.Combine(AppContext.BaseDirectory, "Config", "MES_Config");

    public static string ApiConfigDirectory { get; } =
        Path.Combine(RootDirectory, "ApiConfig");

    public static string DataStructureConfigDirectory { get; } =
        Path.Combine(RootDirectory, "DataStructure");

    public static string MesSystemConfigDirectory { get; } =
        Path.Combine(RootDirectory, "MesSystemConfig");

    public static string MesSystemConfigFilePath { get; } =
        Path.Combine(MesSystemConfigDirectory, "MesSystemConfig.json");
}
