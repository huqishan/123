using System;

namespace Shared.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class BusinessOperationAttribute : Attribute
{
    public BusinessOperationAttribute(string operationId, string displayName = "")
    {
        OperationId = operationId?.Trim() ?? string.Empty;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? OperationId : displayName.Trim();
    }

    public string OperationId { get; }

    public string DisplayName { get; }

    public string Description { get; set; } = string.Empty;
}
