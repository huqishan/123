namespace Module.MES.Features.ApiConfig.Services;

public sealed class ApiConfigService
{
    public ApiConfigService(ApiConfigStore store, ApiConfigValidator validator, ApiRequestPreviewEngine previewEngine, ApiTestSession testSession)
    {
        Store = store ?? throw new ArgumentNullException(nameof(store));
        Validator = validator ?? throw new ArgumentNullException(nameof(validator));
        PreviewEngine = previewEngine ?? throw new ArgumentNullException(nameof(previewEngine));
        TestSession = testSession ?? throw new ArgumentNullException(nameof(testSession));
    }

    public ApiConfigStore Store { get; }

    public ApiConfigValidator Validator { get; }

    public ApiRequestPreviewEngine PreviewEngine { get; }

    public ApiTestSession TestSession { get; }
}
