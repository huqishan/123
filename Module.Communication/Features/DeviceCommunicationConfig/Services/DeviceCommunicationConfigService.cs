namespace Module.Communication.Features.DeviceCommunicationConfig.Services;

public sealed class DeviceCommunicationConfigService
{
    public DeviceCommunicationConfigService(DeviceCommunicationStore store)
    {
        Store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public DeviceCommunicationStore Store { get; }
}
