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
            ReturnValue = "Result"
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
            ShowDataToView = true,
            ViewDataName = "Result"
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
            ShowDataToView = true,
            ViewDataName = "Result"
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
        scheme.Steps.Add(new SchemeWorkStepItem
        {
            WorkStepId = template.Id,
            StepName = template.StepName
        });

        SchemeConfigurationCatalog catalog = new()
        {
            WorkSteps = new ObservableCollection<WorkStepProfile> { template },
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
        _ = Application.Current ?? new App();

        SchemeConfigurationViewModel viewModel = new();

        WorkStepProfile workStep = new()
        {
            StepName = "工步 1"
        };

        WorkStepOperation previousOperation = new()
        {
            OperationObject = "System",
            InvokeMethod = "前序步骤",
            ReturnValue = "PrevKey"
        };

        WorkStepOperation currentOperation = new()
        {
            OperationObject = "System",
            InvokeMethod = "当前步骤",
            ReturnValue = "CurrentKey",
            Parameters = new ObservableCollection<WorkStepOperationParameter>
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

        viewModel.SelectedWorkStep = workStep;
        viewModel.OpenOperationDrawerForEdit(currentOperation);

        ObservableCollection<WorkStepOperationParameter> editingParameters =
            viewModel.EditingInvokeParameters;

        WorkStepOperationParameter parameter = editingParameters[0];
        parameter.Type = "返回值";

        Assert.That(parameter.ValueOptions, Does.Contain("PrevKey"));
        Assert.That(parameter.ValueOptions, Does.Not.Contain("CurrentKey"));
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void TrySaveStepEditor_WhenReturnParameterRowIsEdited_SavesDisplaySettings()
    {
        _ = Application.Current ?? new App();

        SchemeConfigurationViewModel viewModel = new();
        WorkStepProfile workStep = new()
        {
            StepName = "Step"
        };
        WorkStepOperation operation = new()
        {
            OperationObject = "System",
            DeviceId = "System",
            InvokeMethod = "CustomReturn",
            OperationId = "CustomReturn",
            ReturnValue = "Result"
        };
        workStep.Steps.Add(operation);

        viewModel.SelectedWorkStep = workStep;
        viewModel.OpenOperationDrawerForEdit(operation);

        InlineParameterEditorViewModel.InlineReturnParameterRow row =
            viewModel.StepEditorReturnParameterRows.Single();
        row.ShowDataToView = true;
        row.ViewDataName = "DisplayResult";
        row.ViewJudgeType = ">=";
        row.FirstJudgeConditionValue = "5";

        bool saved = viewModel.TrySaveStepEditor();

        Assert.Multiple(() =>
        {
            Assert.That(saved, Is.True);
            Assert.That(viewModel.IsStepEditorOpen, Is.True);
            Assert.That(operation.ReturnValue, Is.EqualTo("Result"));
            Assert.That(operation.ShowDataToView, Is.True);
            Assert.That(operation.ViewDataName, Is.EqualTo("DisplayResult"));
            Assert.That(operation.ViewJudgeType, Is.EqualTo(">="));
            Assert.That(operation.ViewJudgeCondition, Is.EqualTo("5"));
        });
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void TrySaveStepEditor_WhenNewStepIsSaved_KeepsStepEditorOpenAsEdit()
    {
        _ = Application.Current ?? new App();

        SchemeConfigurationViewModel viewModel = new();
        WorkStepProfile workStep = new()
        {
            StepName = "Step"
        };

        viewModel.SelectedWorkStep = workStep;
        viewModel.AddStepCommand.Execute(null);
        viewModel.SelectedOperationMethod = viewModel.OperationMethods.Single(method => method.InvokeMethod == "StringtoHex");

        bool saved = viewModel.TrySaveStepEditor();

        Assert.Multiple(() =>
        {
            Assert.That(saved, Is.True);
            Assert.That(viewModel.IsStepEditorOpen, Is.True);
            Assert.That(viewModel.StepEditorTitle, Is.EqualTo("编辑步骤"));
            Assert.That(workStep.Steps, Has.Count.EqualTo(1));
            Assert.That(workStep.Steps.Single().InvokeMethod, Is.EqualTo("StringtoHex"));
            Assert.That(viewModel.SelectedStep, Is.SameAs(workStep.Steps.Single()));
        });
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void SelectedStep_WhenStepEditorIsOpen_SwitchesEditorToSelectedStep()
    {
        _ = Application.Current ?? new App();

        SchemeConfigurationViewModel viewModel = new();
        WorkStepProfile workStep = new()
        {
            StepName = "Step"
        };
        WorkStepOperation firstOperation = new()
        {
            OperationObject = "System",
            DeviceId = "System",
            InvokeMethod = "HextoString",
            OperationId = "HextoString",
            ReturnValue = "FirstResult"
        };
        WorkStepOperation secondOperation = new()
        {
            OperationObject = "System",
            DeviceId = "System",
            InvokeMethod = "StringtoHex",
            OperationId = "StringtoHex",
            ReturnValue = "SecondResult"
        };
        workStep.Steps.Add(firstOperation);
        workStep.Steps.Add(secondOperation);

        viewModel.SelectedWorkStep = workStep;
        viewModel.AddStepCommand.Execute(null);

        viewModel.SelectedStep = secondOperation;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsStepEditorOpen, Is.True);
            Assert.That(viewModel.StepEditorTitle, Is.EqualTo("编辑步骤"));
            Assert.That(viewModel.SelectedStep, Is.SameAs(secondOperation));
            Assert.That(viewModel.EditingInvokeMethod, Is.EqualTo("StringtoHex"));
            Assert.That(viewModel.EditingReturnValue, Is.EqualTo("SecondResult"));
        });
    }
}
