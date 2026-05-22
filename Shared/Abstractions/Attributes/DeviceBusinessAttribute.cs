using Shared.Abstractions.Enum;
using System;

namespace Shared.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DeviceBusinessAttribute : Attribute
{
    public DeviceBusinessAttribute(CommuniactionType communicationType, string displayName = "")
        : this(communicationType.ToString(), displayName)
    {
        CommunicationType = communicationType;
    }

    public DeviceBusinessAttribute(string deviceId, string displayName = "")
    {
        DeviceId = deviceId?.Trim() ?? string.Empty;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? DeviceId : displayName.Trim();
    }

    public string DeviceId { get; }

    public string DisplayName { get; }

    public CommuniactionType? CommunicationType { get; }

    public string Description { get; set; } = string.Empty;
}
