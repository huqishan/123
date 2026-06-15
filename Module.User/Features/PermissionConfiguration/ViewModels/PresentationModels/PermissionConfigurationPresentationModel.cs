namespace Module.User.Features.PermissionConfiguration.ViewModels.PresentationModels;

public sealed record PermissionConfigurationPresentationModel(
    string RoleName,
    int PageCount,
    int DialogCount,
    int ButtonCount);
