namespace Module.MES.Features.DataStructureConfig.Services;

public sealed class DataStructureConfigService
{
    public DataStructureConfigService(DataStructureStore store, DataStructureParser parser, DataStructurePreviewEngine previewEngine)
    {
        Store = store ?? throw new ArgumentNullException(nameof(store));
        Parser = parser ?? throw new ArgumentNullException(nameof(parser));
        PreviewEngine = previewEngine ?? throw new ArgumentNullException(nameof(previewEngine));
    }

    public DataStructureStore Store { get; }

    public DataStructureParser Parser { get; }

    public DataStructurePreviewEngine PreviewEngine { get; }
}
