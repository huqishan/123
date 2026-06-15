namespace Module.Communication.Features.ProtocolConfig.Services;

public sealed class ProtocolConfigService
{
    public ProtocolConfigService(ProtocolStore store)
    {
        Store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public ProtocolStore Store { get; }
}
