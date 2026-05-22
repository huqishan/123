using System;

namespace Shared.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class BusinessParamAttribute : Attribute
{
    public BusinessParamAttribute(string displayName)
    {
        DisplayName = displayName?.Trim() ?? string.Empty;
    }

    public string DisplayName { get; }

    public string Description { get; set; } = string.Empty;

    public string DefaultValue { get; set; } = string.Empty;
}
