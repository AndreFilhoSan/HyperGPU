using HyperGPU.Models;
using HyperGPU.Services;

namespace HyperGPU.Tests.Services;

[TestClass]
public sealed class VmExecutionPlanBuilderTests
{
    [TestMethod]
    public void Build_WithValidDraft_ReturnsReadyPlan()
    {
        VmConfigurationDraft draft = new()
        {
            CpuCoresText = "4",
            DiskSizeGbText = "40",
            GpuName = "AUTO",
            GpuSharePercentageText = "50",
            IsAutoLogonEnabled = true,
            MemoryGbText = "8",
            NetworkSwitchName = "Default Switch",
            Password = "Password123!",
            SourcePath = typeof(VmExecutionPlanBuilderTests).Assembly.Location,
            SupportsNamedGpuSelection = true,
            Username = "GPUVM",
            VhdPath = Path.GetDirectoryName(typeof(VmExecutionPlanBuilderTests).Assembly.Location)!,
            VmName = "GPUPV",
        };

        HostInspectionSnapshot snapshot = new(
            "Windows 11",
            "X64",
            DateTimeOffset.Now,
            [new HostCheckResult("Check A", "Ready", "Description", string.Empty, HostCheckState.Ready)],
            [new GpuDeviceInfo("NVIDIA GeForce", "Vendor: NVIDIA Driver: 1.0", "NVIDIA", "1.0")],
            ["Default Switch"]);

        VmExecutionPlanBuilder builder = new(new FakeResourceService());

        VmExecutionPlan plan = builder.Build(draft, snapshot);

        Assert.IsFalse(plan.HasBlockingIssues);
        StringAssert.Contains(plan.CreateVmParametersPreview, "VMName = 'GPUPV'");
        StringAssert.Contains(plan.UpdateDriverCommandPreview, "Update-VMGpuPartitionDriver.ps1");
    }

    [TestMethod]
    public void Build_WithInvalidDraft_ReturnsBlockingIssues()
    {
        VmConfigurationDraft draft = new()
        {
            CpuCoresText = "0",
            DiskSizeGbText = "10",
            GpuName = "NVIDIA GeForce",
            GpuSharePercentageText = "150",
            MemoryGbText = string.Empty,
            NetworkSwitchName = string.Empty,
            Password = string.Empty,
            SourcePath = "Z:\\missing.iso",
            SupportsNamedGpuSelection = false,
            Username = "SameName",
            VhdPath = "Z:\\missing-folder",
            VmName = "SameName",
        };

        VmExecutionPlanBuilder builder = new(new FakeResourceService());

        VmExecutionPlan plan = builder.Build(draft, null);

        Assert.IsTrue(plan.HasBlockingIssues);
        Assert.IsTrue(plan.Issues.Count(issue => issue.Severity == VmPlanIssueSeverity.Error) >= 6);
    }

    private sealed class FakeResourceService : IAppResourceService
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal)
        {
            ["DriverUpdateCommandFormat"] = "Update-VMGpuPartitionDriver.ps1 -VMName '{0}' -GPUName '{1}'",
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
            ["VmPlanSummaryBlockedFormat"] = "Plan generated with {0} blocking issue(s) and {1} warning(s).",
            ["VmPlanSummaryReadyFormat"] = "Plan generated. Blocking validation passed. {0} warning(s) remain for review.",
        };

        public string GetString(string key)
        {
            return _values.TryGetValue(key, out string? value) ? value : key;
        }
    }
}