using Module.Business.Models;
using Module.Business.Services;
using Module.Business.Features.OperationEditing.Views;
using Module.Business.Features.SchemeConfiguration;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace TestProject;

[Apartment(ApartmentState.STA)]
public class SchemeConfigurationViewModelTests
{
    [Test]
    public void HasModifiedOperationParameters_WhenOnlyReturnValueChanges_ReturnsTrue()
    {
        SchemeConfigurationViewModel viewModel = new();
        WorkStepOperation operation = new()
        {
            OperationObjectName = "System",
            PCommandName = "等待",
            ReturnValues = new ObservableCollection<ReturnValue>
            {
                new() { ReturnParameterName = "Result" }
            }
        };

        bool isModified = viewModel.HasModifiedOperationParameters(operation);

        Assert.That(isModified, Is.True);
    }

    [Test]
    public void HasModifiedOperationParameters_WhenOnlyReturnDisplaySettingsChange_ReturnsTrue()
    {
        SchemeConfigurationViewModel viewModel = new();
        WorkStepOperation operation = new()
        {
            OperationObjectName = "System",
            PCommandName = "等待",
            ReturnValues = new ObservableCollection<ReturnValue>
            {
                new() { IsShowView = true, ReturnParameterName = "Result" }
            }
        };

        bool isModified = viewModel.HasModifiedOperationParameters(operation);

        Assert.That(isModified, Is.True);
    }

    [Test]
    public void RefreshOperationParameterModifiedStates_WhenOperationHasSavedReturnSettings_RestoresModifiedFlag()
    {
        SchemeConfigurationViewModel viewModel = new();
        WorkStepOperation operation = new()
        {
            OperationObjectName = "System",
            PCommandName = "等待",
            ReturnValues = new ObservableCollection<ReturnValue>
            {
                new() { IsShowView = true, ReturnParameterName = "Result" }
            }
        };

        Assert.That(operation.IsEditParameter, Is.False);

        viewModel.RefreshOperationParameterModifiedStates(new[] { operation });

        Assert.That(operation.IsEditParameter, Is.True);
    }

    [Test]
    public void NormalizeCatalog_WhenSchemeStepHasNoOperations_PreservesEmptyOperations()
    {
        SchemeProfile scheme = new()
        {
            SchemeName = "Scheme"
        };
        scheme.Steps.Add(new SchemeWorkStepItem
        {
            StepName = "Template"
        });

        SchemeConfigurationCatalog catalog = new()
        {
            Schemes = new ObservableCollection<SchemeProfile> { scheme }
        };

        MethodInfo normalizeMethod = typeof(SchemeConfigurationStore).GetMethod(
            "NormalizeCatalog",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        SchemeConfigurationCatalog normalized =
            (SchemeConfigurationCatalog)normalizeMethod.Invoke(null, new object?[] { catalog })!;

        Assert.That(normalized.Schemes.Single().Steps.Single().Operations, Is.Empty);
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void EditingInvokeParameters_WhenTypeIsReturnValue_UsesPreviousStepKeysOnly()
    {
        _ = Application.Current ?? new Application();

        SchemeConfigurationViewModel viewModel = new();

        SchemeWorkStepItem workStep = new()
        {
            StepName = "工步 1"
        };

        WorkStepOperation previousOperation = new()
        {
            OperationObjectName = "System",
            PCommandName = "前序步骤",
            ReturnValues = new ObservableCollection<ReturnValue>
            {
                new() { ReturnParameterName = "PrevKey" }
            }
        };

        WorkStepOperation currentOperation = new()
        {
            OperationObjectName = "System",
            PCommandName = "当前步骤",
            ReturnValues = new ObservableCollection<ReturnValue>
            {
                new() { ReturnParameterName = "CurrentKey" }
            },
            Parameters = new ObservableCollection<InputParameter>
            {
                new()
                {
                    ParameterType = "设置值",
                    ParameterName = "InputKey",
                    Value = string.Empty
                }
            }
        };

        workStep.Operations.Add(previousOperation);
        workStep.Operations.Add(currentOperation);

        viewModel.SelectedWorkStep = workStep;
        viewModel.OpenOperationDrawerForEdit(currentOperation);

        ObservableCollection<InputParameter> editingParameters =
            viewModel.EditingInvokeParameters;

        InputParameter parameter = editingParameters[0];
        parameter.ParameterType = "返回值";

        MethodInfo buildMethod = typeof(SchemeConfigurationViewModel).GetMethod(
            "BuildParameterReturnValueOptions",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        IEnumerable<string> options = (IEnumerable<string>)buildMethod.Invoke(viewModel, null)!;

        Assert.That(options, Does.Contain("PrevKey"));
        Assert.That(options, Does.Not.Contain("CurrentKey"));
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void TrySaveStepEditor_WhenReturnParameterRowIsEdited_SavesDisplaySettings()
    {
        EnsureApplicationResources();

        SchemeConfigurationViewModel viewModel = new();
        SchemeWorkStepItem workStep = new()
        {
            StepName = "Step"
        };
        WorkStepOperation operation = new()
        {
            OperationObjectName = "System",
            PCommandName = "CustomReturn",
            ReturnValues = new ObservableCollection<ReturnValue>
            {
                new() { ReturnParameterName = "Result" }
            }
        };
        workStep.Operations.Add(operation);

        OperationEditorView view = new()
        {
            DataContext = viewModel
        };
        view.Measure(new Size(1200, 900));
        view.Arrange(new Rect(0, 0, 1200, 900));
        view.UpdateLayout();

        viewModel.SelectedWorkStep = workStep;
        viewModel.OpenOperationDrawerForEdit(operation);

        InlineParameterEditorViewModel.InlineReturnParameterRow row =
            viewModel.StepEditorReturnParameterRows.Single();
        row.ShowDataToView = true;
        row.Key = "DisplayResult";
        row.ViewJudgeType = ">=";
        row.FirstJudgeConditionValue = "5";

        bool saved = viewModel.TrySaveStepEditor();

        Assert.Multiple(() =>
        {
            Assert.That(saved, Is.True);
            Assert.That(viewModel.IsStepEditorOpen, Is.True);
            Assert.That(operation.ReturnValues.FirstOrDefault()?.IsShowView, Is.True);
            Assert.That(operation.ReturnValues.FirstOrDefault()?.ReturnParameterName, Is.EqualTo("DisplayResult"));
            Assert.That(operation.ReturnValues.FirstOrDefault()?.JudgeType, Is.EqualTo(">="));
            Assert.That(operation.ReturnValues.FirstOrDefault()?.JudgeValue, Is.EqualTo("5"));
        });
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void TrySaveStepEditor_WhenNewStepIsSaved_KeepsStepEditorOpenAsNew()
    {
        _ = Application.Current ?? new Application();

        SchemeConfigurationViewModel viewModel = new();
        SchemeWorkStepItem workStep = new()
        {
            StepName = "Step"
        };

        viewModel.SelectedWorkStep = workStep;
        viewModel.AddStepCommand.Execute(null);
        var methodItem = viewModel.OperationMethods.First(method => method.InvokeMethod == "StringtoHex")!;
        WorkStepOperation operationFromMethod = viewModel.CreateOperationFromMethodItem(methodItem)!;
        viewModel.SelectedOperationMethod = methodItem;
        viewModel.EditingOperationObject = operationFromMethod.OperationObjectName;
        viewModel.EditingProtocolName = methodItem.ProtocolName;
        viewModel.EditingCommandName = methodItem.CommandName;
        viewModel.EditingInvokeMethod = operationFromMethod.PCommandName;

        bool saved = viewModel.TrySaveStepEditor();

        Assert.Multiple(() =>
        {
            Assert.That(saved, Is.True);
            Assert.That(viewModel.IsStepEditorOpen, Is.True);
            Assert.That(viewModel.StepEditorTitle, Is.EqualTo("编辑步骤"));
            Assert.That(workStep.Operations, Has.Count.EqualTo(1));
            Assert.That(workStep.Operations.Single().PCommandName, Is.EqualTo("StringtoHex"));
            Assert.That(viewModel.SelectedStep, Is.Null);
            Assert.That(viewModel.SelectedOperationMethod, Is.Not.Null);
            Assert.That(viewModel.SelectedOperationMethod!.InvokeMethod, Is.EqualTo("StringtoHex"));
            Assert.That(viewModel.EditingOperationObject, Is.EqualTo("System"));
            Assert.That(viewModel.EditingInvokeMethod, Is.EqualTo("StringtoHex"));
        });
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void TrySaveStepEditor_WhenNewStepIsSavedTwice_AddsTwoStepsWithoutClosingEditor()
    {
        _ = Application.Current ?? new Application();

        SchemeConfigurationViewModel viewModel = new();
        SchemeWorkStepItem workStep = new()
        {
            StepName = "Step"
        };

        viewModel.SelectedWorkStep = workStep;
        viewModel.AddStepCommand.Execute(null);

        viewModel.SelectedOperationMethod = viewModel.OperationMethods.First(method => method.InvokeMethod == "StringtoHex");
        viewModel.EditingInvokeMethod = "StringtoHex";
        bool firstSaved = viewModel.TrySaveStepEditor();

        viewModel.AddStepCommand.Execute(null);
        viewModel.SelectedOperationMethod = viewModel.OperationMethods.First(method => method.InvokeMethod == "HextoString");
        viewModel.EditingInvokeMethod = "HextoString";
        bool secondSaved = viewModel.TrySaveStepEditor();

        Assert.Multiple(() =>
        {
            Assert.That(firstSaved, Is.True);
            Assert.That(secondSaved, Is.True);
            Assert.That(viewModel.IsStepEditorOpen, Is.True);
            Assert.That(viewModel.StepEditorTitle, Is.EqualTo("编辑步骤"));
            Assert.That(viewModel.SelectedStep, Is.Null);
            Assert.That(workStep.Operations, Has.Count.EqualTo(2));
            Assert.That(workStep.Operations.Select(step => step.PCommandName), Is.EqualTo(new[] { "StringtoHex", "HextoString" }));
        });
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void SelectedStep_WhenStepEditorIsOpen_SwitchesEditorToSelectedStep()
    {
        _ = Application.Current ?? new Application();

        SchemeConfigurationViewModel viewModel = new();
        SchemeWorkStepItem workStep = new()
        {
            StepName = "Step"
        };
        WorkStepOperation firstOperation = new()
        {
            OperationObjectName = "System",
            PCommandName = "HextoString",
            ReturnValues = new ObservableCollection<ReturnValue>
            {
                new() { ReturnParameterName = "FirstResult" }
            }
        };
        WorkStepOperation secondOperation = new()
        {
            OperationObjectName = "System",
            PCommandName = "StringtoHex",
            ReturnValues = new ObservableCollection<ReturnValue>
            {
                new() { ReturnParameterName = "SecondResult" }
            }
        };
        workStep.Operations.Add(firstOperation);
        workStep.Operations.Add(secondOperation);

        viewModel.SelectedWorkStep = workStep;
        viewModel.AddStepCommand.Execute(null);

        viewModel.SelectedStep = secondOperation;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsStepEditorOpen, Is.True);
            Assert.That(viewModel.StepEditorTitle, Is.EqualTo("编辑步骤"));
            Assert.That(viewModel.SelectedStep, Is.SameAs(secondOperation));
            Assert.That(viewModel.EditingInvokeMethod, Is.EqualTo("StringtoHex"));
            Assert.That(viewModel.EditingViewDataName, Is.EqualTo("SecondResult"));
        });
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void SelectedStep_WhenDeviceTypeIsNotConfigured_StillAddsOperationObjectOptionFromExistingOperations()
    {
        EnsureApplicationResources();

        string unavailableDeviceType = $"UnavailableDeviceType-{Guid.NewGuid():N}";
        SchemeConfigurationViewModel viewModel = new();
        OperationEditorView view = new()
        {
            DataContext = viewModel
        };
        view.Measure(new Size(1200, 900));
        view.Arrange(new Rect(0, 0, 1200, 900));
        view.UpdateLayout();
        WorkStepOperation operation = new()
        {
            OperationObjectName = unavailableDeviceType,
            PCommandName = "LegacyCommand"
        };
        SchemeWorkStepItem workStep = new()
        {
            StepName = "Step"
        };
        workStep.Operations.Add(operation);

        viewModel.SelectedWorkStep = workStep;
        viewModel.AddStepCommand.Execute(null);
        Assert.That(viewModel.OperationObjectOptions, Does.Contain(unavailableDeviceType));

        viewModel.SelectedStep = operation;
        view.UpdateLayout();
        ComboBox operationObjectComboBox = (ComboBox)view.FindName("OperationObject");

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.StepEditorTitle, Is.EqualTo("编辑步骤"));
            Assert.That(viewModel.OperationObjectOptions, Does.Contain(unavailableDeviceType));
            Assert.That(operationObjectComboBox.SelectedItem, Is.EqualTo(unavailableDeviceType));
        });
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void OpenOperationDrawerForEdit_WhenStepHasSystemMethod_SelectsCurrentMethod()
    {
        _ = Application.Current ?? new Application();

        SchemeConfigurationViewModel viewModel = new();
        List<string> changedProperties = new();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not null)
            {
                changedProperties.Add(e.PropertyName);
            }
        };
        SchemeWorkStepItem workStep = new()
        {
            StepName = "Step"
        };
        WorkStepOperation operation = new()
        {
            OperationObjectName = "System",
            PCommandName = "StringtoHex"
        };
        workStep.Operations.Add(operation);

        viewModel.SelectedWorkStep = workStep;
        viewModel.OpenOperationDrawerForEdit(operation);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.StepEditorTitle, Is.EqualTo("编辑步骤"));
            Assert.That(viewModel.SelectedOperationMethod, Is.Not.Null);
            Assert.That(viewModel.SelectedStationOperationMethod, Is.SameAs(viewModel.SelectedOperationMethod));
            Assert.That(viewModel.SelectedOperationMethod!.InvokeMethod, Is.EqualTo("StringtoHex"));
            Assert.That(changedProperties, Does.Contain(nameof(SchemeConfigurationViewModel.SelectedStationOperationMethod)));
        });
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void OpenOperationDrawerForEdit_WhenStepHasDeviceCommand_SelectsCurrentCommand()
    {
        _ = Application.Current ?? new Application();

        string suffix = Guid.NewGuid().ToString("N");
        string deviceName = $"Device-{suffix}";
        string protocolName = $"Protocol-{suffix}";
        string commandName = $"Command-{suffix}";
        string communicationDirectory = Path.Combine(AppContext.BaseDirectory, "Config", "Communication");
        string protocolDirectory = Path.Combine(AppContext.BaseDirectory, "Config", "Protocol");
        Directory.CreateDirectory(communicationDirectory);
        Directory.CreateDirectory(protocolDirectory);

        string communicationFilePath = Path.Combine(communicationDirectory, $"{deviceName}.json");
        string protocolFilePath = Path.Combine(protocolDirectory, $"{protocolName}.json");

        try
        {
            File.WriteAllText(
                communicationFilePath,
                JsonSerializer.Serialize(new
                {
                    Version = 3,
                    LocalName = deviceName,
                    TypeId = "tcp-client",
                    Config = new Dictionary<string, string>(),
                    SupportedProtocols = new[]
                    {
                        new
                        {
                            ProtocolName = protocolName,
                            ProtocolFilePath = protocolFilePath
                        }
                    }
                }),
                System.Text.Encoding.UTF8);
            File.WriteAllText(
                protocolFilePath,
                JsonSerializer.Serialize(new
                {
                    Version = 2,
                    Name = protocolName,
                    Commands = new[]
                    {
                        new
                        {
                            Name = commandName,
                            ContentTemplate = "AA {{Address}}",
                            PlaceholderValuesText = "Address=01",
                            ParsedResultKeys = new[] { "Result" }
                        }
                    }
                }),
                System.Text.Encoding.UTF8);

            SchemeConfigurationViewModel viewModel = new();
            Assert.That(viewModel.LoadDeviceOperationObjectNames(), Does.Contain(deviceName));

            SchemeWorkStepItem workStep = new()
            {
                StepName = "Step"
            };
            WorkStepOperation operation = new()
            {
                OperationObjectName = deviceName,
                PCommandName = commandName
            };
            workStep.Operations.Add(operation);

            viewModel.SelectedWorkStep = workStep;
            viewModel.OpenOperationDrawerForEdit(operation);

            Assert.Multiple(() =>
            {
                Assert.That(viewModel.StepEditorTitle, Is.EqualTo("编辑步骤"));
                Assert.That(viewModel.EditingOperationObject, Is.EqualTo(deviceName));
                Assert.That(viewModel.EditingProtocolName, Is.EqualTo(protocolName));
                Assert.That(viewModel.EditingCommandName, Is.EqualTo(commandName));
                Assert.That(viewModel.OperationMethods.Select(method => method.InvokeMethod), Does.Contain(commandName));
                Assert.That(viewModel.SelectedOperationMethod, Is.Not.Null);
                Assert.That(viewModel.SelectedOperationMethod!.OperationObject, Is.EqualTo(deviceName));
                Assert.That(viewModel.SelectedOperationMethod.ProtocolName, Is.EqualTo(protocolName));
                Assert.That(viewModel.SelectedOperationMethod.CommandName, Is.EqualTo(commandName));
            });
        }
        finally
        {
            if (File.Exists(communicationFilePath))
            {
                File.Delete(communicationFilePath);
            }

            if (File.Exists(protocolFilePath))
            {
                File.Delete(protocolFilePath);
            }
        }
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void OpenOperationDrawerForEdit_WhenDeviceStepStoresDeviceType_SelectsCurrentCommand()
    {
        EnsureApplicationResources();

        string suffix = Guid.NewGuid().ToString("N");
        string deviceName = $"Device-{suffix}";
        string deviceType = $"DeviceType-{suffix}";
        string protocolName = $"Protocol-{suffix}";
        string commandName = $"Command-{suffix}";
        string communicationDirectory = Path.Combine(AppContext.BaseDirectory, "Config", "Communication");
        string protocolDirectory = Path.Combine(AppContext.BaseDirectory, "Config", "Protocol");
        Directory.CreateDirectory(communicationDirectory);
        Directory.CreateDirectory(protocolDirectory);

        string communicationFilePath = Path.Combine(communicationDirectory, $"{deviceName}.json");
        string protocolFilePath = Path.Combine(protocolDirectory, $"{protocolName}.json");

        try
        {
            File.WriteAllText(
                communicationFilePath,
                JsonSerializer.Serialize(new
                {
                    Version = 3,
                    LocalName = deviceName,
                    TypeId = deviceType,
                    Config = new Dictionary<string, string>(),
                    SupportedProtocols = new[]
                    {
                        new
                        {
                            ProtocolName = protocolName,
                            ProtocolFilePath = protocolFilePath
                        }
                    }
                }),
                System.Text.Encoding.UTF8);
            File.WriteAllText(
                protocolFilePath,
                JsonSerializer.Serialize(new
                {
                    Version = 2,
                    Name = protocolName,
                    Commands = new[]
                    {
                        new
                        {
                            Name = commandName,
                            ContentTemplate = "AA {{Address}}",
                            PlaceholderValuesText = "Address=01",
                            ParsedResultKeys = new[] { "Result" }
                        }
                    }
                }),
                System.Text.Encoding.UTF8);

            SchemeConfigurationViewModel viewModel = new();
            OperationEditorView view = new()
            {
                DataContext = viewModel
            };
            view.Measure(new Size(1200, 900));
            view.Arrange(new Rect(0, 0, 1200, 900));
            view.UpdateLayout();
            SchemeWorkStepItem workStep = new()
            {
                StepName = "Step"
            };
            WorkStepOperation operation = new()
            {
                OperationObjectName = deviceName,
                PCommandName = commandName
            };
            workStep.Operations.Add(operation);

            viewModel.SelectedWorkStep = workStep;
            viewModel.AddStepCommand.Execute(null);
            viewModel.SelectedStep = operation;
            view.UpdateLayout();
            ComboBox operationObjectComboBox = (ComboBox)view.FindName("OperationObject");

            Assert.Multiple(() =>
            {
                Assert.That(viewModel.EditingOperationObject, Is.EqualTo(deviceName));
                Assert.That(viewModel.OperationObjectOptions, Does.Contain(deviceName));
                Assert.That(viewModel.OperationObjectOptions, Does.Not.Contain(deviceType));
                Assert.That(operationObjectComboBox.Text, Is.EqualTo(deviceName));
                Assert.That(operationObjectComboBox.SelectedItem, Is.EqualTo(deviceName));
                Assert.That(viewModel.EditingProtocolName, Is.EqualTo(protocolName));
                Assert.That(viewModel.EditingCommandName, Is.EqualTo(commandName));
                Assert.That(viewModel.SelectedOperationMethod, Is.Not.Null);
                Assert.That(viewModel.SelectedOperationMethod!.OperationObject, Is.EqualTo(deviceName));
                Assert.That(viewModel.SelectedOperationMethod.ProtocolName, Is.EqualTo(protocolName));
                Assert.That(viewModel.SelectedOperationMethod.CommandName, Is.EqualTo(commandName));
            });
        }
        finally
        {
            if (File.Exists(communicationFilePath))
            {
                File.Delete(communicationFilePath);
            }

            if (File.Exists(protocolFilePath))
            {
                File.Delete(protocolFilePath);
            }
        }
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void OperationMethodGrid_WhenSelectingDeviceCommandFirstTime_KeepsSelection()
    {
        EnsureApplicationResources();

        string suffix = Guid.NewGuid().ToString("N");
        string deviceName = $"Device-{suffix}";
        string protocolName = $"Protocol-{suffix}";
        string commandName = $"Command-{suffix}";
        string communicationDirectory = Path.Combine(AppContext.BaseDirectory, "Config", "Communication");
        string protocolDirectory = Path.Combine(AppContext.BaseDirectory, "Config", "Protocol");
        Directory.CreateDirectory(communicationDirectory);
        Directory.CreateDirectory(protocolDirectory);

        string communicationFilePath = Path.Combine(communicationDirectory, $"{deviceName}.json");
        string protocolFilePath = Path.Combine(protocolDirectory, $"{protocolName}.json");

        try
        {
            File.WriteAllText(
                communicationFilePath,
                JsonSerializer.Serialize(new
                {
                    Version = 3,
                    LocalName = deviceName,
                    TypeId = "tcp-client",
                    Config = new Dictionary<string, string>(),
                    SupportedProtocols = new[]
                    {
                        new
                        {
                            ProtocolName = protocolName,
                            ProtocolFilePath = protocolFilePath
                        }
                    }
                }),
                System.Text.Encoding.UTF8);
            File.WriteAllText(
                protocolFilePath,
                JsonSerializer.Serialize(new
                {
                    Version = 2,
                    Name = protocolName,
                    Commands = new[]
                    {
                        new
                        {
                            Name = commandName,
                            ContentTemplate = "AA {{Address}}",
                            PlaceholderValuesText = "Address=01",
                            ParsedResultKeys = new[] { "Result" }
                        }
                    }
                }),
                System.Text.Encoding.UTF8);

            SchemeConfigurationViewModel viewModel = new();
            viewModel.SelectedWorkStep = new SchemeWorkStepItem
            {
                StepName = "Step"
            };
            viewModel.AddStepCommand.Execute(null);

            OperationEditorView view = new()
            {
                DataContext = viewModel
            };
            view.Measure(new Size(1200, 900));
            view.Arrange(new Rect(0, 0, 1200, 900));
            view.UpdateLayout();

            viewModel.EditingOperationObject = deviceName;
            viewModel.EditingProtocolName = string.Empty;
            StationOperationMethodItem targetMethod = viewModel.OperationMethods.First(method =>
                method.OperationObject == deviceName &&
                method.ProtocolName == protocolName &&
                method.CommandName == commandName);
            DataGrid methodGrid = (DataGrid)view.FindName("OperationMethodDataGrid");

            methodGrid.SelectedItem = targetMethod;
            methodGrid.RaiseEvent(new SelectionChangedEventArgs(
                Selector.SelectionChangedEvent,
                Array.Empty<object>(),
                new[] { targetMethod }));

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

            Assert.Multiple(() =>
            {
                Assert.That(methodGrid.SelectedItem, Is.Not.Null);
                Assert.That(viewModel.SelectedStationOperationMethod, Is.Not.Null);
                Assert.That(viewModel.SelectedStationOperationMethod?.OperationObject, Is.EqualTo(deviceName));
                Assert.That(viewModel.SelectedStationOperationMethod?.ProtocolName, Is.EqualTo(protocolName));
                Assert.That(viewModel.SelectedStationOperationMethod?.CommandName, Is.EqualTo(commandName));
                Assert.That(viewModel.EditingProtocolName, Is.EqualTo(protocolName));
                Assert.That(viewModel.EditingCommandName, Is.EqualTo(commandName));
                Assert.That(viewModel.EditingInvokeMethod, Is.EqualTo(commandName));
            });
        }
        finally
        {
            if (File.Exists(communicationFilePath))
            {
                File.Delete(communicationFilePath);
            }

            if (File.Exists(protocolFilePath))
            {
                File.Delete(protocolFilePath);
            }
        }
    }

    private static void EnsureApplicationResources()
    {
        Application application = Application.Current ?? new Application();
        var dictionaries = application.Resources.MergedDictionaries;
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
}