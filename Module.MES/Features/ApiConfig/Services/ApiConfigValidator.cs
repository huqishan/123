using Module.MES.Features.ApiConfig.ViewModels.PresentationModels;

namespace Module.MES.Features.ApiConfig.Services;

public sealed class ApiConfigValidator
{
    public void Validate(ApiInterfaceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (string.IsNullOrWhiteSpace(profile.ApiName))
        {
            throw new InvalidOperationException("方法名称不能为空。");
        }

        ValidatePort(profile.TCPLocalPort, "本地端口");
        ValidatePort(profile.TCPRemotePort, "远程端口");
    }

    private static void ValidatePort(string value, string displayName)
    {
        if (!ushort.TryParse(value?.Trim(), out _))
        {
            throw new InvalidOperationException($"{displayName} 必须是 0-65535 之间的整数。");
        }
    }
}
