using Autofac;
using ControlLibrary;
using Module.Business.Services;
using Module.Business.Services.BusinessOperations;
using Module.Communication.Services;
using Module.MES.Services;
using Module.Business.Test.Services;
using Module.User.Features.Authentication.Services;
using Module.User.Services;
using Shared.Infrastructure.DependencyInjection;
using Shared.Infrastructure.Events;
using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using WpfApp.Infrastructure;
using BusinessSystem = Module.Business.Business.System;

namespace WpfApp;

internal static class WpfAppComposition
{
    public static void Register(ContainerBuilder builder)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.RegisterType<AutofacViewFactory>()
            .As<IViewFactory>()
            .SingleInstance();

        RegisterApplicationServices(builder);
        RegisterViewsAndViewModels(builder);
    }

    public static void Initialize(ILifetimeScope scope)
    {
        IEventAggregator eventAggregator = scope.Resolve<IEventAggregator>();
        BusinessSystem.ConfigureEventAggregator(eventAggregator);
        BusinessOperationInvoker.ConfigureServiceResolver(type => scope.ResolveOptional(type));
        BusinessOperationCatalog.Refresh();

        // Resolve long-lived module services that subscribe to application events.
        _ = scope.Resolve<SchemeService>();
    }

    private static void RegisterApplicationServices(ContainerBuilder builder)
    {
        builder.RegisterType<SchemeService>().SingleInstance();
        builder.RegisterType<CommunicationService>()
            .AsSelf()
            .SingleInstance();
        builder.RegisterType<MESService>()
            .AsSelf()
            .SingleInstance();
        builder.RegisterType<TestService>().SingleInstance();
        builder.RegisterType<AuthenticationService>()
            .As<IAuthenticationService>()
            .SingleInstance();
        builder.RegisterType<UserService>().SingleInstance();
    }

    private static void RegisterViewsAndViewModels(ContainerBuilder builder)
    {
        Assembly[] assemblies =
        [
            typeof(App).Assembly,
            typeof(Module.Business.Features.Scheme.Views.SchemeConfigurationView).Assembly,
            typeof(Module.Communication.Features.DeviceCommunicationConfig.Views.DeviceCommunicationConfigView).Assembly,
            typeof(Module.MES.Features.ApiConfig.Views.ApiConfigView).Assembly,
            typeof(Module.Business.Test.Views.TestView).Assembly,
            typeof(Module.User.Features.AccountManagement.Views.AccountManagementView).Assembly,
            typeof(ControlLibrary.Controls.Navigation.Control.ModernNavigationBar).Assembly
        ];

        ServiceCollectionHelper.RegisterMediatorHandlers(builder, assemblies);

        builder.RegisterAssemblyTypes(assemblies)
            .Where(type => typeof(Window).IsAssignableFrom(type) || typeof(UserControl).IsAssignableFrom(type))
            .AsSelf()
            .InstancePerDependency();

        builder.RegisterAssemblyTypes(assemblies)
            // OperationEditorViewModel 是 SchemeConfigurationViewModel 内部组合的子视图模型，
            // 构造时必须传入当前宿主，不能由 Autofac 脱离宿主单独创建。
            .Where(type => type.Name.EndsWith("ViewModel", StringComparison.Ordinal) &&
                           type != typeof(Module.Business.Features.OperationEditing.ViewModels.OperationEditorViewModel))
            .AsSelf()
            .InstancePerDependency();
    }
}
