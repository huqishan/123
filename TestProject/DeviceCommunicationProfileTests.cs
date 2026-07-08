using Module.Communication.Configuration;
using Module.Communication.Features.DeviceCommunicationConfig.Models;
using Module.Communication.Features.DeviceCommunicationConfig.ViewModels;
using Module.Communication.Features.DeviceCommunicationConfig.Views;
using Shared.Abstractions.Enum;
using Shared.Infrastructure.Communication;
using Shared.Models.Communication;
using System.Collections.Specialized;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace TestProject;

[TestFixture]
public sealed class DeviceCommunicationProfileTests
{
    [Test]
    public void Registry_GroupsCommunicationTypesByFamily()
    {
        IReadOnlyList<DeviceCommunicationConfigDescriptor> descriptors =
            DeviceCommunicationConfigRegistry.Default.Descriptors;

        string[] standardTypeIds = descriptors
            .Where(item => item.Family == CommunicationFamily.Standard)
            .Select(item => item.TypeId)
            .ToArray();
        string[] plcTypeIds = descriptors
            .Where(item => item.Family == CommunicationFamily.Plc)
            .Select(item => item.TypeId)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(standardTypeIds, Does.Contain("tcp-client"));
            Assert.That(standardTypeIds, Does.Contain("tcp-server"));
            Assert.That(standardTypeIds, Does.Contain("udp"));
            Assert.That(standardTypeIds, Does.Contain("serial-port"));
            Assert.That(plcTypeIds, Is.EquivalentTo(new[] { "plc-modbus", "plc-mx", "plc-s7" }));
        });
    }

    [Test]
    public void Constructor_WhenTypeIsPlcS7_UsesRegisteredDefaults()
    {
        DeviceCommunicationProfile profile = new("plc-s7");

        Assert.Multiple(() =>
        {
            Assert.That(profile.TypeDisplayName, Is.EqualTo("PLC S7"));
            Assert.That(profile.GetParameter("CpuType"), Is.EqualTo(S7CpuTypeNames.S71200));
            Assert.That(profile.GetParameter("Rack"), Is.EqualTo("0"));
            Assert.That(profile.GetParameter("Slot"), Is.EqualTo("1"));
        });
    }

    [Test]
    public void TypeId_WhenAssignedBlankValue_KeepsCurrentType()
    {
        DeviceCommunicationProfile profile = new("plc-s7");

        profile.TypeId = null!;
        profile.TypeId = string.Empty;

        Assert.That(profile.TypeId, Is.EqualTo("plc-s7"));
    }

    [Test]
    public void SelectedProfile_WhenTypeComboBoxEmitsTransientBlankValue_KeepsCurrentFamilyTypes()
    {
        DeviceCommunicationConfigViewModel viewModel = new();

        viewModel.SelectedCommunicationFamily = CommunicationFamily.Plc.ToString();
        viewModel.SelectedProfile!.TypeId = "plc-s7";
        viewModel.SelectedCommunicationFamily = CommunicationFamily.Can.ToString();

        viewModel.SelectedProfile!.TypeId = null!;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SelectedCommunicationFamily, Is.EqualTo(CommunicationFamily.Can.ToString()));
            Assert.That(viewModel.SelectedProfile.TypeId, Is.EqualTo("can-tcp"));
            Assert.That(viewModel.CommunicationTypes.Select(option => option.Value), Is.EquivalentTo(new[] { "can-tcp" }));
        });
    }

    [Test]
    public void SelectedCommunicationFamily_WhenTypeComboBoxClearsSelectionDuringRefresh_DoesNotMixFamilyTypes()
    {
        DeviceCommunicationConfigViewModel viewModel = new()
        {
            SelectedProfile = new DeviceCommunicationProfile("plc-s7")
        };
        bool emittedTransientBlankValue = false;

        NotifyCollectionChangedEventHandler handler = (_, e) =>
        {
            if (emittedTransientBlankValue || e.Action != NotifyCollectionChangedAction.Reset)
            {
                return;
            }

            emittedTransientBlankValue = true;
            viewModel.SelectedProfile!.TypeId = null!;
        };

        viewModel.CommunicationTypes.CollectionChanged += handler;
        try
        {
            viewModel.SelectedCommunicationFamily = CommunicationFamily.Can.ToString();
        }
        finally
        {
            viewModel.CommunicationTypes.CollectionChanged -= handler;
        }

        Assert.Multiple(() =>
        {
            Assert.That(emittedTransientBlankValue, Is.True);
            Assert.That(viewModel.SelectedCommunicationFamily, Is.EqualTo(CommunicationFamily.Can.ToString()));
            Assert.That(viewModel.SelectedProfile!.TypeId, Is.EqualTo("can-tcp"));
            Assert.That(viewModel.CommunicationTypes.Select(option => option.Value), Is.EquivalentTo(new[] { "can-tcp" }));
        });
    }

    [Test]
    public void TryBuildRuntimeConfig_WhenPlcTypeIsS7_BuildsS7RuntimeConfig()
    {
        DeviceCommunicationProfile profile = new("plc-s7")
        {
            LocalName = "S7 Device"
        };
        profile.SetParameter("RemoteIpAddress", "192.168.0.10");
        profile.SetParameter("CpuType", S7CpuTypeNames.S71500);
        profile.SetParameter("Rack", "0");
        profile.SetParameter("Slot", "1");

        bool succeeded = profile.TryBuildRuntimeConfig(out ICommunicationRuntimeConfig? config, out string validationMessage);
        S7PlcRuntimeConfig? s7Config = config as S7PlcRuntimeConfig;

        Assert.Multiple(() =>
        {
            Assert.That(succeeded, Is.True, validationMessage);
            Assert.That(s7Config, Is.Not.Null);
            Assert.That(s7Config!.Type, Is.EqualTo(CommuniactionType.PLC));
            Assert.That(s7Config.RemoteIpAddress, Is.EqualTo("192.168.0.10"));
            Assert.That(s7Config.CpuType, Is.EqualTo(S7CpuTypeNames.S71500));
            Assert.That(s7Config.Rack, Is.EqualTo(0));
            Assert.That(s7Config.Slot, Is.EqualTo(1));
        });
    }

    [Test]
    public void TryBuildRuntimeConfig_WhenS7RackIsOutOfRange_ReturnsValidationError()
    {
        DeviceCommunicationProfile profile = new("plc-s7")
        {
            LocalName = "S7 Device"
        };
        profile.SetParameter("RemoteIpAddress", "192.168.0.10");
        profile.SetParameter("Rack", "8");

        bool succeeded = profile.TryBuildRuntimeConfig(out _, out string validationMessage);

        Assert.Multiple(() =>
        {
            Assert.That(succeeded, Is.False);
            Assert.That(validationMessage, Is.EqualTo("PLC Rack必须在 0 到 7 之间。"));
        });
    }

    [Test]
    public void SelectedProfile_WhenTypeIdChanges_RefreshesFieldsAndTestVisibility()
    {
        DeviceCommunicationConfigViewModel viewModel = new()
        {
            SelectedProfile = new DeviceCommunicationProfile("tcp-client")
        };

        string[] initialFieldKeys = viewModel.CurrentFields.Select(field => field.Key).ToArray();

        viewModel.SelectedProfile!.TypeId = "plc-s7";

        Assert.Multiple(() =>
        {
            Assert.That(initialFieldKeys, Is.EquivalentTo(new[] { "RemoteIpAddress", "RemotePort", "LocalIpAddress", "LocalPort" }));
            Assert.That(viewModel.CurrentFields.Select(field => field.Key), Is.EquivalentTo(new[] { "RemoteIpAddress", "CpuType", "Rack", "Slot" }));
            Assert.That(viewModel.IsGenericSendTestVisible, Is.False);
            Assert.That(viewModel.IsPlcTestVisible, Is.True);
        });
    }

    [Test]
    public void SelectedCommunicationTypeId_WhenChangedWithinSameFamily_RefreshesFieldsAndTestVisibility()
    {
        DeviceCommunicationConfigViewModel viewModel = new()
        {
            SelectedProfile = new DeviceCommunicationProfile("plc-modbus")
        };

        viewModel.SelectedCommunicationTypeId = "plc-s7";

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SelectedProfile!.TypeId, Is.EqualTo("plc-s7"));
            Assert.That(viewModel.CurrentFields.Select(field => field.Key), Is.EquivalentTo(new[] { "RemoteIpAddress", "CpuType", "Rack", "Slot" }));
            Assert.That(viewModel.IsGenericSendTestVisible, Is.False);
            Assert.That(viewModel.IsPlcTestVisible, Is.True);
        });
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void CommunicationTypeComboBox_AfterFamilySelection_UpdatesFieldsAndTestVisibility()
    {
        EnsureWpfThreadContext();

        DeviceCommunicationConfigViewModel viewModel = new()
        {
            SelectedProfile = new DeviceCommunicationProfile("tcp-client")
        };
        DeviceCommunicationConfigView view = new(viewModel);

        view.Measure(new Size(1280, 900));
        view.Arrange(new Rect(0, 0, 1280, 900));
        view.UpdateLayout();

        ComboBox familyComboBox = FindDescendants<ComboBox>(view)
            .First(comboBox => ReferenceEquals(comboBox.ItemsSource, viewModel.CommunicationFamilies));
        ComboBox communicationTypeComboBox = FindDescendants<ComboBox>(view)
            .First(comboBox => ReferenceEquals(comboBox.ItemsSource, viewModel.CommunicationTypes));

        communicationTypeComboBox.SelectedValue = "tcp-server";
        DrainDispatcher();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SelectedProfile!.TypeId, Is.EqualTo("tcp-server"));
            Assert.That(viewModel.CurrentFields.Select(field => field.Key), Is.EquivalentTo(new[] { "LocalIpAddress", "LocalPort" }));
            Assert.That(viewModel.IsGenericSendTestVisible, Is.True);
            Assert.That(viewModel.IsTcpServerClientSelectionVisible, Is.True);
            Assert.That(viewModel.IsPlcTestVisible, Is.False);
        });

        familyComboBox.SelectedValue = CommunicationFamily.Plc.ToString();
        DrainDispatcher();
        communicationTypeComboBox.SelectedValue = "plc-s7";
        DrainDispatcher();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SelectedCommunicationFamily, Is.EqualTo(CommunicationFamily.Plc.ToString()));
            Assert.That(viewModel.SelectedProfile!.TypeId, Is.EqualTo("plc-s7"));
            Assert.That(viewModel.CurrentFields.Select(field => field.Key), Is.EquivalentTo(new[] { "RemoteIpAddress", "CpuType", "Rack", "Slot" }));
            Assert.That(viewModel.IsGenericSendTestVisible, Is.False);
            Assert.That(viewModel.IsPlcTestVisible, Is.True);
        });
    }

    [Test]
    public void SelectedProfile_WhenTypeIdChangesWhileConnectionIsActive_ClosesActiveCommunication()
    {
        DeviceCommunicationConfigViewModel viewModel = new()
        {
            SelectedProfile = new DeviceCommunicationProfile("tcp-client")
        };
        FakeCommunication fakeCommunication = new();

        typeof(DeviceCommunicationConfigViewModel)
            .GetField("_activeCommunication", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(viewModel, fakeCommunication);
        typeof(DeviceCommunicationConfigViewModel)
            .GetField("_activeCommunicationType", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(viewModel, CommuniactionType.TCPClient);

        viewModel.SelectedProfile!.TypeId = "plc-s7";

        Assert.Multiple(() =>
        {
            Assert.That(fakeCommunication.CloseCalled, Is.True);
            Assert.That(viewModel.IsPlcTestVisible, Is.True);
        });
    }

    private static void EnsureWpfThreadContext()
    {
        _ = Application.Current ?? new Application();
        EnsureApplicationResources();

        if (SynchronizationContext.Current is not DispatcherSynchronizationContext)
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        }
    }

    private static void DrainDispatcher()
    {
        DispatcherFrame frame = new();
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new DispatcherOperationCallback(_ =>
            {
                frame.Continue = false;
                return null;
            }),
            null);

        Dispatcher.PushFrame(frame);
    }

    private static IEnumerable<T> FindDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        int childCount = VisualTreeHelper.GetChildrenCount(root);
        for (int index = 0; index < childCount; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T matched)
            {
                yield return matched;
            }

            foreach (T descendant in FindDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static void EnsureApplicationResources()
    {
        var dictionaries = Application.Current!.Resources.MergedDictionaries;
        if (dictionaries.Any(dictionary => dictionary.Source?.OriginalString?.Contains(
                "AppDictionary.xaml",
                StringComparison.OrdinalIgnoreCase) == true))
        {
            return;
        }

        string[] resourcePaths =
        [
            "Resources/Themes/DarkTheme.xaml",
            "Resources/AppDictionary.xaml",
            "Resources/TabControlStyles.xaml",
            "Resources/ComboBoxStyle.xaml",
            "Resources/TextBoxStyle.xaml",
            "Resources/ScrollBarStyle.xaml",
            "Resources/DataGridStyle.xaml",
            "Resources/ButtonStyle.xaml",
            "Resources/TitleStyle.xaml",
            "Resources/BorderStyle.xaml",
            "Resources/LabelStyle.xaml",
            "Resources/ListBoxStyle.xaml",
            "Resources/ContextMenuStyle.xaml",
            "Resources/ToolTipStyle.xaml",
            "Resources/Language/ZhCN.xaml"
        ];

        foreach (string resourcePath in resourcePaths)
        {
            dictionaries.Add(new ResourceDictionary
            {
                Source = new Uri($"/WpfApp;component/{resourcePath}", UriKind.Relative)
            });
        }
    }

    private sealed class FakeCommunication : CommunicationBase
    {
        public bool CloseCalled { get; private set; }

        public override bool Start()
        {
            return true;
        }

        public override bool Close()
        {
            CloseCalled = true;
            return true;
        }
    }
}
