using HyperGPU.Models;
using HyperGPU.Services;

namespace HyperGPU.Tests.Services;

[TestClass]
public sealed class HostReadinessEvaluatorTests
{
    [TestMethod]
    public void Evaluate_AllRequirementsSatisfied_ReturnsReadyChecks()
    {
        HostProbeData probeData = new()
        {
            Architecture = "X64",
            Gpus = [new GpuProbeInfo { DriverVersion = "1.0.0", Name = "NVIDIA GeForce", Vendor = "NVIDIA" }],
            Is64BitOperatingSystem = true,
            IsAdministrator = true,
            IsHyperVEnabled = true,
            IsHypervisorPresent = true,
            IsVmManagementServiceRunning = true,
            IsSupportedWindowsVersion = true,
            OperatingSystemDescription = "Windows 11",
        };

        HostReadinessEvaluator evaluator = new(new FakeResourceService());

        HostInspectionSnapshot snapshot = evaluator.Evaluate(probeData, new DateTimeOffset(2026, 5, 12, 8, 0, 0, TimeSpan.Zero));

        Assert.AreEqual(7, snapshot.Checks.Count);
        Assert.AreEqual(0, snapshot.Checks.Count(check => check.State != HostCheckState.Ready));
        Assert.AreEqual(1, snapshot.GpuDevices.Count);
        Assert.AreEqual("NVIDIA GeForce", snapshot.GpuDevices[0].Name);
        Assert.AreEqual(0, snapshot.NetworkSwitches.Count);
    }

    [TestMethod]
    public void Evaluate_HyperVDisabled_ReturnsBlockingCheck()
    {
        HostProbeData probeData = new()
        {
            Architecture = "X64",
            Gpus = [new GpuProbeInfo { DriverVersion = "1.0.0", Name = "Intel Iris", Vendor = "Intel" }],
            Is64BitOperatingSystem = true,
            IsAdministrator = true,
            IsHyperVEnabled = false,
            IsHypervisorPresent = false,
            IsSupportedWindowsVersion = true,
            OperatingSystemDescription = "Windows 11",
        };

        HostReadinessEvaluator evaluator = new(new FakeResourceService());

        HostInspectionSnapshot snapshot = evaluator.Evaluate(probeData, DateTimeOffset.Now);
        HostCheckResult hyperVCheck = snapshot.Checks.Single(check => check.Title == "Hyper-V feature enabled");

        Assert.AreEqual(HostCheckState.Error, hyperVCheck.State);
        Assert.IsTrue(hyperVCheck.HasResolution);
    }

    private sealed class FakeResourceService : IAppResourceService
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal)
        {
            ["AdminDescription"] = "Administrative rights are required to inspect and later configure Hyper-V resources.",
            ["AdminResolution"] = "Launch HyperGPU as administrator before creating or modifying VMs.",
            ["AdminTitle"] = "Elevated session",
            ["CheckStateError"] = "Blocking issue",
            ["CheckStateReady"] = "Ready",
            ["CheckStateWarning"] = "Warning",
            ["GpuDetectedDescription"] = "At least one display adapter should be visible for future partition and driver workflows.",
            ["GpuDetectedResolution"] = "Install or repair the host GPU driver, then refresh the inspection.",
            ["GpuDetectedTitle"] = "GPU adapters detected",
            ["GpuSummaryFormat"] = "Vendor: {0}  Driver: {1}",
            ["HyperVFeatureDescription"] = "Microsoft-Hyper-V-All should be enabled on the host.",
            ["HyperVFeatureResolution"] = "Enable Hyper-V in Windows Features and reboot the host.",
            ["HyperVFeatureTitle"] = "Hyper-V feature enabled",
            ["HypervisorDescription"] = "The Hyper-V hypervisor should be active after feature enablement.",
            ["HypervisorResolution"] = "Reboot the host after enabling Hyper-V, then refresh the inspection.",
            ["HypervisorTitle"] = "Hypervisor active",
            ["QuickFixStartVmms"] = "Start Hyper-V service",
            ["VmmsDescription"] = "The Hyper-V Virtual Machine Management service must be running before VHDX and VM operations can start.",
            ["VmmsResolution"] = "Start the Hyper-V Virtual Machine Management service, then refresh host checks.",
            ["VmmsTitle"] = "Hyper-V management service",
            ["Os64BitDescription"] = "GPU-PV automation expects a 64-bit Windows installation.",
            ["Os64BitResolution"] = "Use a 64-bit edition of Windows on the host.",
            ["Os64BitTitle"] = "64-bit operating system",
            ["OsSupportedDescription"] = "Windows 10 20H1 or newer is required for the first GPU-PV workflows.",
            ["OsSupportedResolution"] = "Update Windows to at least version 2004 before continuing.",
            ["OsSupportedTitle"] = "Supported Windows build",
            ["UnknownValue"] = "Unknown",
        };

        public string GetString(string key)
        {
            return _values.TryGetValue(key, out string? value) ? value : key;
        }
    }
}