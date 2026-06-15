namespace Module.User.Features.AccountManagement.ViewModels.PresentationModels;

public sealed record AccountManagementPresentationModel(
    string Account,
    string Name,
    string PermissionDisplayName);
