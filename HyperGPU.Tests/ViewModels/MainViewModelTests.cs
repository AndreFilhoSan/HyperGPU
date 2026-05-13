using HyperGPU.Models;
using HyperGPU.Services;
using HyperGPU.ViewModels;

namespace HyperGPU.Tests.ViewModels;

[TestClass]
public sealed class MainViewModelTests
{
    [TestMethod]
    public async Task RefreshAsync_WhenInspectionSucceeds_UpdatesSummaryAndCollections()
    {
        HostInspectionSnapshot snapshot = new(
            "Windows 11",
            "X64",
            new DateTimeOffset(2026, 5, 12, 8, 0, 0, TimeSpan.Zero),
            [
                new HostCheckResult("Check A", "Ready", "Description A", string.Empty, HostCheckState.Ready),
                new HostCheckResult("Check B", "Warning", "Description B", "Resolution B", HostCheckState.Warning),
            ],
            [new GpuDeviceInfo("NVIDIA GeForce", "Vendor: NVIDIA  Driver: 1.0", "NVIDIA", "1.0")],
            ["Default Switch"]);

        MainViewModel viewModel = new(new FakeHostInspectionService(snapshot), new FakeFilePickerService(), new FakeHostQuickActionService(), new FakeResourceService(), new FakeAppStateService(), new VmExecutionPlanBuilder(new FakeResourceService()), new FakeVmProvisioningService());

        await viewModel.RefreshAsync();

        Assert.IsTrue(viewModel.HasSnapshot);
        Assert.AreEqual(1, viewModel.ReadyCount);
        Assert.AreEqual(1, viewModel.WarningCount);
        Assert.AreEqual(0, viewModel.ErrorCount);
        Assert.AreEqual(2, viewModel.Checks.Count);
        Assert.AreEqual(1, viewModel.GpuDevices.Count);
        Assert.IsTrue(viewModel.HasGpuDevices);
        Assert.IsTrue(viewModel.HasNetworkSwitches);
        StringAssert.Contains(viewModel.StatusSummary, "1 warning");
    }

    [TestMethod]
    public async Task RefreshAsync_WhenInspectionThrows_AddsFailureCheck()
    {
        MainViewModel viewModel = new(new ThrowingHostInspectionService(), new FakeFilePickerService(), new FakeHostQuickActionService(), new FakeResourceService(), new FakeAppStateService(), new VmExecutionPlanBuilder(new FakeResourceService()), new FakeVmProvisioningService());

        await viewModel.RefreshAsync();

        Assert.IsTrue(viewModel.HasSnapshot);
        Assert.AreEqual(1, viewModel.ErrorCount);
        Assert.AreEqual(1, viewModel.Checks.Count);
        Assert.AreEqual("Host inspection failed", viewModel.Checks[0].Title);
    }

    [TestMethod]
    public async Task ExecuteVmPlanAsync_WhenPlanIsValid_UpdatesExecutionStagesAndLog()
    {
        HostInspectionSnapshot snapshot = new(
            "Windows 11",
            "X64",
            new DateTimeOffset(2026, 5, 12, 8, 0, 0, TimeSpan.Zero),
            [new HostCheckResult("Check A", "Ready", "Description A", string.Empty, HostCheckState.Ready)],
            [new GpuDeviceInfo("NVIDIA GeForce", "Vendor: NVIDIA  Driver: 1.0", "NVIDIA", "1.0")],
            ["Default Switch"]);

        MainViewModel viewModel = new(new FakeHostInspectionService(snapshot), new FakeFilePickerService(), new FakeHostQuickActionService(), new FakeResourceService(), new FakeAppStateService(), new VmExecutionPlanBuilder(new FakeResourceService()), new FakeVmProvisioningService());

        string isoPath = typeof(MainViewModelTests).Assembly.Location;
        string vhdPath = Path.GetDirectoryName(isoPath)!;
        viewModel.SourcePath = isoPath;
        viewModel.VhdPath = vhdPath;

        await viewModel.RefreshAsync();
        viewModel.BuildVmPlanCommand.Execute(null);

        await viewModel.ExecuteVmPlanCommand.ExecuteAsync(null);

        StringAssert.Contains(viewModel.ExecutionStagesText, "Prepare workspace");
        StringAssert.Contains(viewModel.ExecutionStagesText, "Refresh guest drivers");
        StringAssert.Contains(viewModel.ExecutionLog, "Execution output");
        Assert.IsTrue(viewModel.HasExecutionOutput);
    }

    [TestMethod]
    public async Task UpdateExistingVmCommand_WhenPlanIsValid_UpdatesDriverStagesAndLog()
    {
        HostInspectionSnapshot snapshot = new(
            "Windows 11",
            "X64",
            new DateTimeOffset(2026, 5, 12, 8, 0, 0, TimeSpan.Zero),
            [new HostCheckResult("Check A", "Ready", "Description A", string.Empty, HostCheckState.Ready)],
            [new GpuDeviceInfo("NVIDIA GeForce", "Vendor: NVIDIA  Driver: 1.0", "NVIDIA", "1.0")],
            ["Default Switch"]);

        MainViewModel viewModel = new(new FakeHostInspectionService(snapshot), new FakeFilePickerService(), new FakeHostQuickActionService(), new FakeResourceService(), new FakeAppStateService(), new VmExecutionPlanBuilder(new FakeResourceService()), new FakeVmProvisioningService());

        string isoPath = typeof(MainViewModelTests).Assembly.Location;
        viewModel.SourcePath = isoPath;
        viewModel.VhdPath = Path.GetDirectoryName(isoPath)!;

        await viewModel.RefreshAsync();
        viewModel.BuildVmPlanCommand.Execute(null);

        await viewModel.UpdateExistingVmCommand.ExecuteAsync(null);

        StringAssert.Contains(viewModel.ExecutionStagesText, "Prepare workspace");
        StringAssert.Contains(viewModel.ExecutionStagesText, "Refresh guest drivers");
        Assert.IsFalse(viewModel.ExecutionStagesText.Contains("Create VM", StringComparison.Ordinal));
        StringAssert.Contains(viewModel.ExecutionLog, "Existing update output");
        Assert.IsTrue(viewModel.HasExecutionOutput);
    }

    [TestMethod]
    public async Task BrowseCommands_WhenPickerReturnsPath_UpdateFields()
    {
        FakeFilePickerService pickerService = new()
        {
            IsoPath = @"C:\ISO\Windows11.iso",
            VhdFolderPath = @"C:\HyperV",
        };

        MainViewModel viewModel = new(new ThrowingHostInspectionService(), pickerService, new FakeHostQuickActionService(), new FakeResourceService(), new FakeAppStateService(), new VmExecutionPlanBuilder(new FakeResourceService()), new FakeVmProvisioningService());

        await viewModel.BrowseIsoCommand.ExecuteAsync(null);
        await viewModel.BrowseVhdPathCommand.ExecuteAsync(null);

        Assert.AreEqual(@"C:\ISO\Windows11.iso", viewModel.SourcePath);
        Assert.AreEqual(@"C:\HyperV", viewModel.VhdPath);
    }

    [TestMethod]
    public void FormChoices_ArePersistedAndRestored()
    {
        FakeAppStateService appState = new();
        MainViewModel firstViewModel = new(new ThrowingHostInspectionService(), new FakeFilePickerService(), new FakeHostQuickActionService(), new FakeResourceService(), appState, new VmExecutionPlanBuilder(new FakeResourceService()), new FakeVmProvisioningService());

        firstViewModel.VmName = "GPUVM01";
        firstViewModel.Username = "Andre";
        firstViewModel.SourcePath = @"D:\ISO\Win11.iso";
        firstViewModel.VhdPath = @"E:\VM";
        firstViewModel.SelectedGpuName = "AMD Radeon RX 7700 XT";
        firstViewModel.SelectedNetworkSwitch = "Default Switch";
        firstViewModel.CpuCoresText = "8";
        firstViewModel.MemoryGbText = "16";
        firstViewModel.DiskSizeGbText = "80";
        firstViewModel.GpuSharePercentageText = "60";
        firstViewModel.IsAutoLogonEnabled = false;

        MainViewModel restoredViewModel = new(new ThrowingHostInspectionService(), new FakeFilePickerService(), new FakeHostQuickActionService(), new FakeResourceService(), appState, new VmExecutionPlanBuilder(new FakeResourceService()), new FakeVmProvisioningService());

        Assert.AreEqual("GPUVM01", restoredViewModel.VmName);
        Assert.AreEqual("Andre", restoredViewModel.Username);
        Assert.AreEqual(@"D:\ISO\Win11.iso", restoredViewModel.SourcePath);
        Assert.AreEqual(@"E:\VM", restoredViewModel.VhdPath);
        Assert.AreEqual("AMD Radeon RX 7700 XT", restoredViewModel.SelectedGpuName);
        Assert.AreEqual("Default Switch", restoredViewModel.SelectedNetworkSwitch);
        Assert.AreEqual("8", restoredViewModel.CpuCoresText);
        Assert.AreEqual("16", restoredViewModel.MemoryGbText);
        Assert.AreEqual("80", restoredViewModel.DiskSizeGbText);
        Assert.AreEqual("60", restoredViewModel.GpuSharePercentageText);
        Assert.IsFalse(restoredViewModel.IsAutoLogonEnabled);
    }

    private sealed class FakeHostInspectionService : IHostInspectionService
    {
        private readonly HostInspectionSnapshot _snapshot;

        public FakeHostInspectionService(HostInspectionSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public Task<HostInspectionSnapshot> InspectAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_snapshot);
        }
    }

    private sealed class ThrowingHostInspectionService : IHostInspectionService
    {
        public Task<HostInspectionSnapshot> InspectAsync(CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Inspection failed.");
        }
    }

    private sealed class FakeFilePickerService : IFilePickerService
    {
        public string? IsoPath { get; init; }

        public string? VhdFolderPath { get; init; }

        public Task<string?> PickIsoFileAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(IsoPath);
        }

        public Task<string?> PickVhdFolderAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(VhdFolderPath);
        }
    }

    private sealed class FakeVmProvisioningService : IVmProvisioningService
    {
        public Task<VmExecutionResult> ExecuteCreateVmAsync(VmConfigurationDraft draft, IProgress<VmExecutionStageResult>? progress, IProgress<string>? outputProgress, CancellationToken cancellationToken)
        {
            VmExecutionStageResult[] stages =
            [
                new("Prepare workspace", VmExecutionStageState.Completed, "Completed", "Prepared temporary files."),
                new("Create VM", VmExecutionStageState.Completed, "Completed", "Created the VM."),
                new("Refresh guest drivers", VmExecutionStageState.Completed, "Completed", "Updated guest drivers."),
            ];

            foreach (VmExecutionStageResult stage in stages)
            {
                progress?.Report(stage);
            }

            outputProgress?.Report("INFO   : Creating sparse disk...");
            outputProgress?.Report("INFO   : Finding and copying driver files for Test GPU to VM. This could take a while...");

            return Task.FromResult(new VmExecutionResult(true, "VM provisioning script finished.", "Execution output", "C:\\Temp\\HyperGPU", stages));
        }

        public Task<VmExecutionResult> ExecuteUpdateExistingVmAsync(VmConfigurationDraft draft, IProgress<VmExecutionStageResult>? progress, IProgress<string>? outputProgress, CancellationToken cancellationToken)
        {
            VmExecutionStageResult[] stages =
            [
                new("Prepare workspace", VmExecutionStageState.Completed, "Completed", "Prepared temporary files."),
                new("Refresh guest drivers", VmExecutionStageState.Completed, "Completed", "Updated guest drivers."),
            ];

            foreach (VmExecutionStageResult stage in stages)
            {
                progress?.Report(stage);
            }

            outputProgress?.Report("INFO   : Copying GPU driver files into the guest VHDX. This could take a while...");

            return Task.FromResult(new VmExecutionResult(true, "Existing VM guest GPU driver refresh finished.", "Existing update output", "C:\\Temp\\HyperGPU", stages));
        }
    }

    private sealed class FakeHostQuickActionService : IHostQuickActionService
    {
        public Task ExecuteAsync(HostCheckActionKind action, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAppStateService : IAppStateService
    {
        private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);

        public string GetString(string key, string defaultValue = "")
        {
            return _values.TryGetValue(key, out object? value) && value is string stringValue
                ? stringValue
                : defaultValue;
        }

        public void SetString(string key, string value)
        {
            _values[key] = value;
        }

        public bool GetBoolean(string key, bool defaultValue = false)
        {
            return _values.TryGetValue(key, out object? value) && value is bool boolValue
                ? boolValue
                : defaultValue;
        }

        public void SetBoolean(string key, bool value)
        {
            _values[key] = value;
        }
    }

    private sealed class FakeResourceService : IAppResourceService
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal)
        {
            ["CheckStateError"] = "Blocking issue",
            ["InspectionFailedDescription"] = "HyperGPU could not complete the current host inspection.",
            ["InspectionFailedResolution"] = "Check PowerShell availability and try refreshing again.",
            ["InspectionFailedTitle"] = "Host inspection failed",
            ["LastUpdatedFormat"] = "Last updated: {0}",
            ["SummaryAllClear"] = "Host inspection complete. No blocking issues were found.",
            ["SummaryNotRun"] = "Run the inspection to see current host readiness.",
            ["SummaryWithIssues"] = "Host inspection complete. {0} ready, {1} warning, {2} blocking.",
            ["VmPlanIssueCpuInvalid"] = "CPU cores must be a whole number between 1 and 64.",
            ["VmPlanIssueCpuRequired"] = "Enter the number of CPU cores.",
            ["VmPlanIssueDiskInvalid"] = "Disk size must be a whole number between 20 and 4096 GB.",
            ["VmPlanIssueDiskRequired"] = "Enter the disk size in GB.",
            ["VmPlanIssueGpuRequired"] = "Select a GPU option.",
            ["VmPlanIssueGpuShareInvalid"] = "GPU allocation must be a whole number between 1 and 100.",
            ["VmPlanIssueGpuShareRequired"] = "Enter the GPU allocation percentage.",
            ["VmPlanIssueHostBlockingFormat"] = "The current host snapshot still has {0} blocking readiness check(s).",
            ["VmPlanIssueHostWarningsFormat"] = "The current host snapshot still has {0} warning check(s).",
            ["VmPlanIssueIsoMissing"] = "The Windows ISO path does not exist.",
            ["VmPlanIssueIsoRequired"] = "Enter the path to a Windows ISO.",
            ["VmPlanIssueMemoryInvalid"] = "Memory must be a whole number between 1 and 512 GB.",
            ["VmPlanIssueMemoryRequired"] = "Enter the memory amount in GB.",
            ["VmPlanIssueNoInspection"] = "No host inspection snapshot is available yet. Refresh checks before trusting the plan.",
            ["VmPlanIssuePasswordRequired"] = "Enter a guest password.",
            ["VmPlanIssueSeverityError"] = "Error:",
            ["VmPlanIssueSeverityInformation"] = "Info:",
            ["VmPlanIssueSeverityWarning"] = "Warning:",
            ["VmPlanIssueSwitchRequired"] = "Select a Hyper-V network switch.",
            ["VmPlanIssueUsernameInvalid"] = "Guest username must be alphanumeric.",
            ["VmPlanIssueUsernameMatchesVm"] = "Guest username cannot match the VM name.",
            ["VmPlanIssueUsernameRequired"] = "Enter a guest username.",
            ["VmPlanIssueVhdPathMissing"] = "The target VHD folder path does not exist.",
            ["VmPlanIssueVhdPathRequired"] = "Enter the target VHD folder path.",
            ["VmPlanIssueVmNameInvalid"] = "VM name must be alphanumeric and no longer than 15 characters.",
            ["VmPlanIssueVmNameRequired"] = "Enter a VM name.",
            ["VmPlanIssueWindows10GpuSelection"] = "Windows 10 hosts must keep GPU selection set to AUTO.",
            ["VmPlanNotGenerated"] = "Build the VM execution plan to see a generated preview.",
            ["VmPlanSummaryBlockedFormat"] = "Plan generated with {0} blocking issue(s) and {1} warning(s).",
            ["VmPlanSummaryReadyFormat"] = "Plan generated. Blocking validation passed. {0} warning(s) remain for review.",
            ["VmExecutionBlocked"] = "Execution is blocked until all validation errors are resolved.",
            ["VmExecutionNotRun"] = "Run VM provisioning to capture a script execution log.",
            ["VmExecutionReady"] = "The plan is ready to run.",
            ["VmExecutionRunning"] = "VM provisioning is running. This can take several minutes.",
            ["VmExecutionUpdatingExisting"] = "Existing VM driver refresh is running.",
            ["PostProvisioningGuideText"] = "After creating the VM, use VMConnect only for initial access.",
            ["VmExecutionStagePending"] = "Pending",
            ["VmExecutionStageRunning"] = "Running",
            ["VmExecutionStagePreparePending"] = "The temporary automation workspace is waiting to be prepared.",
            ["VmExecutionStagePrepareTitle"] = "Prepare workspace",
            ["VmExecutionStageCreatePending"] = "The VM creation script is waiting to start.",
            ["VmExecutionStageCreateTitle"] = "Create VM",
            ["VmExecutionStageDriverPending"] = "The guest GPU driver refresh is waiting to start.",
            ["VmExecutionStageDriverTitle"] = "Refresh guest drivers",
            ["DriverUpdateCommandFormat"] = "Update-VMGpuPartitionDriver.ps1 -VMName '{0}' -GPUName '{1}'",
        };

        public string GetString(string key)
        {
            return _values.TryGetValue(key, out string? value) ? value : key;
        }
    }
}