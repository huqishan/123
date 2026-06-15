namespace Module.MES.Features.SystemConfig.Services;

public sealed class MesSystemConfigService
{
    public MesSystemConfigService(MesSystemConfigStore store)
    {
        Store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public MesSystemConfigStore Store { get; }
}
