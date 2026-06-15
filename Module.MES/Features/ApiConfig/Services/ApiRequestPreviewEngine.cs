using Shared.Infrastructure.Extensions;
using Shared.Infrastructure.PackMethod;

namespace Module.MES.Features.ApiConfig.Services;

public sealed class ApiRequestPreviewEngine
{
    public string FormatResponseText(string? responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return string.Empty;
        }

        string trimmed = responseText.Trim();
        if (trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            return trimmed.ToJsonFormat();
        }

        if (trimmed.StartsWith("<", StringComparison.Ordinal))
        {
            return trimmed.ToXMLFormat();
        }

        return responseText;
    }
}
