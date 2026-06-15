using Shared.Infrastructure.PackMethod;
using Shared.Models.MES;

namespace Module.MES.Features.DataStructureConfig.Services;

public sealed class DataStructureParser
{
    public TreeModel ParseJsonFile(string filePath)
    {
        return MesDataConvert.DeserializeFromJsonFile(filePath);
    }

    public TreeModel ParseXmlFile(string filePath)
    {
        return MesDataConvert.DeserializeFromXMLFile(filePath);
    }
}
