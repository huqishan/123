using Shared.Infrastructure.PackMethod;
using Shared.Models.MES;

namespace Module.MES.Features.DataStructureConfig.Services;

public sealed class DataStructurePreviewEngine
{
    public string BuildPreview(MesDataInfoTree sourceData, string structureName)
    {
        return MesDataConvert.Convert(sourceData, structureName);
    }
}
