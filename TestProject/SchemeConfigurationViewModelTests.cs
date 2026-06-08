using Module.Business.Models;
using Module.Business.Services;
using Module.Business.Features.SchemeConfiguration;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Threading;
using System.Windows;
using WpfApp;

namespace TestProject;

public class SchemeConfigurationViewModelTests
{
    [Test]
    public void HasModifiedOperationParameters_WhenOnlyReturnValueChanges_ReturnsTrue()
    {
        SchemeConfigurationViewModel viewModel = new();
        WorkStepOperation operation = new()
        {
            OperationObject = "System",
            InvokeMethod = "等待",
            ReturnParameters = new ObservableCollection<WorkStepOperationParameter>
            {
                new()
                {
                    Name = "返回值",
                    ParameterName = "Result",
                    Value = "Result"
                }
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
            OperationObject = "System",
            InvokeMethod = "等待",
            ReturnParameters = new ObservableCollection<WorkStepOperationParameter>
            {
                new()
                {
                    Name = "返回值",
                    ParameterName = "Result",
                    Value = "Result",
                    ShowDataToView = true,
                    ViewDataName = "Result"
                }
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
            OperationObject = "System",
            InvokeMethod = "等待",
            ReturnParameters = new ObservableCollection<WorkStepOperationParameter>
            {
                new()
                {
                    Name = "返回值",
                    ParameterName = "Result",
                    Value = "Result",
                    ShowDataToView = true,
                    ViewDataName = "Result"
                }
            }
        };

        Assert.That(operation.AreParametersModified, Is.False);

        viewModel.RefreshOperationParameterModifiedStates(new[] { operation });

        Assert.That(operation.AreParametersModified, Is.True);
    }

    [Test]
    public void NormalizeCatalog_WhenSchemeStepHasOnlyWorkStepId_DoesNotBackfillOperations()
    {
        WorkStepProfile template = new()
        {
            Id = "template-step",
            StepName = "Template"
        };
        template.Steps.Add(new WorkStepOperation
        {
            OperationObject = "System",
            OperationId = "StringtoHex",
            InvokeMethod = "StringtoHex"
        });

        SchemeProfile scheme = new()
        {
            SchemeName = "Scheme"
        };
        scheme.Steps.Add(new WorkStepProfile
        {
            WorkStepId = template.Id,
            StepName = template.StepName
        });

        SchemeConfigurationCatalog catalog = new()
        {
            WorkSteps = new ObservableCollection<WorkStepProfile> { template },
            Schemes = new ObservableCollection<SchemeProfile> { scheme }
        };

        MethodInfo normalizeMethod = typeof(BusinessConfigurationStore).GetMethod(
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
        _ = Application.Current ?? new App();

        Type editorStateType = typeof(SchemeConfigurationViewModel).Assembly
            .GetType("Module.Business.Features.SchemeConfiguration.SchemeStepEditorState", throwOnError: true)!;
        object editorState = Activator.CreateInstance(editorStateType)!;

        WorkStepProfile workStep = new()
        {
            StepName = "工步 1"
        };

        WorkStepOperation previousOperation = new()
        {
            OperationObject = "System",
            InvokeMethod = "前序步骤",
            ReturnParameters = new ObservableCollection<WorkStepOperationParameter>
            {
                new()
                {
                    Name = "返回值",
                    ParameterName = "PrevKey",
                    Value = "PrevKey"
                }
            }
        };

        WorkStepOperation currentOperation = new()
        {
            OperationObject = "System",
            InvokeMethod = "当前步骤",
            ReturnParameters = new ObservableCollection<WorkStepOperationParameter>
            {
                new()
                {
                    Name = "返回值",
                    ParameterName = "CurrentKey",
                    Value = "CurrentKey"
                }
            },
            InputParameters = new ObservableCollection<WorkStepOperationParameter>
            {
                new()
                {
                    Name = "设置值",
                    ParameterName = "InputKey",
                    Value = string.Empty,
                    Remark = string.Empty
                }
            }
        };

        workStep.Steps.Add(previousOperation);
        workStep.Steps.Add(currentOperation);

        editorStateType.GetProperty("SelectedWorkStep")!.SetValue(editorState, workStep);
        editorStateType.GetMethod("OpenOperationDrawerForEdit")!.Invoke(editorState, new object[] { currentOperation });

        ObservableCollection<WorkStepOperationParameter> editingParameters =
            (ObservableCollection<WorkStepOperationParameter>)editorStateType
                .GetProperty("EditingInvokeParameters")!
                .GetValue(editorState)!;

        WorkStepOperationParameter parameter = editingParameters[0];
        parameter.Type = "返回值";

        Assert.That(parameter.ValueOptions, Does.Contain("PrevKey"));
        Assert.That(parameter.ValueOptions, Does.Not.Contain("CurrentKey"));
    }
    [Test]
    public void SchemeLastModifiedAt_WhenSchemeStepChanges_UpdatesSchemeTime()
    {
        SchemeConfigurationViewModel viewModel = new();
        WorkStepProfile step = new()
        {
            StepName = "Step 1"
        };
        SchemeProfile scheme = new()
        {
            SchemeName = "Scheme",
            Steps = new ObservableCollection<WorkStepProfile> { step }
        };

        viewModel.Schemes.Add(scheme);
        scheme.LastModifiedAt = DateTime.Now.AddMinutes(-5);

        DateTime original = scheme.LastModifiedAt;
        step.StepName = "Step 2";

        Assert.That(scheme.LastModifiedAt, Is.GreaterThan(original));
    }

    [Test]
    public void SchemeLastModifiedAt_WhenOperationChanges_UpdatesSchemeTime()
    {
        WorkStepOperation operation = new()
        {
            OperationObject = "System",
            InvokeMethod = "Before"
        };
        WorkStepProfile step = new()
        {
            StepName = "Step 1",
            Operations = new ObservableCollection<WorkStepOperation> { operation }
        };
        SchemeProfile scheme = new()
        {
            SchemeName = "Scheme",
            Steps = new ObservableCollection<WorkStepProfile> { step }
        };
        SchemeConfigurationViewModel viewModel = new();

        viewModel.Schemes.Add(scheme);
        scheme.LastModifiedAt = DateTime.Now.AddMinutes(-5);

        DateTime original = scheme.LastModifiedAt;
        operation.InvokeMethod = "After";

        Assert.That(scheme.LastModifiedAt, Is.GreaterThan(original));
    }
}
