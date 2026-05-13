using System.Globalization;
using HyperGPU.Models;

namespace HyperGPU.Services;

public sealed class HostReadinessEvaluator
{
    private readonly IAppResourceService _resources;

    public HostReadinessEvaluator(IAppResourceService resources)
    {
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
    }

    public HostInspectionSnapshot Evaluate(HostProbeData probeData, DateTimeOffset capturedAt)
    {
        ArgumentNullException.ThrowIfNull(probeData);

        List<HostCheckResult> checks =
        [
            CreateSupportedWindowsCheck(probeData),
            Create64BitCheck(probeData),
            CreateAdministratorCheck(probeData),
            CreateHyperVCheck(probeData),
            CreateHypervisorCheck(probeData),
            CreateVmManagementServiceCheck(probeData),
            CreateGpuCheck(probeData),
        ];

        List<GpuDeviceInfo> gpuDevices = [];

        foreach (GpuProbeInfo gpu in probeData.Gpus)
        {
            string vendor = GetValueOrUnknown(gpu.Vendor);
            string driverVersion = GetValueOrUnknown(gpu.DriverVersion);
            string description = string.Format(
                CultureInfo.CurrentCulture,
                _resources.GetString("GpuSummaryFormat"),
                vendor,
                driverVersion);

            gpuDevices.Add(new GpuDeviceInfo(GetValueOrUnknown(gpu.Name), description, vendor, driverVersion));
        }

        return new HostInspectionSnapshot(
            GetValueOrUnknown(probeData.OperatingSystemDescription),
            GetValueOrUnknown(probeData.Architecture),
            capturedAt,
            checks,
            gpuDevices,
            probeData.NetworkSwitches);
    }

    private HostCheckResult CreateSupportedWindowsCheck(HostProbeData probeData)
    {
        return CreateCheck(
            "OsSupportedTitle",
            "OsSupportedDescription",
            "OsSupportedResolution",
            probeData.IsSupportedWindowsVersion ? HostCheckState.Ready : HostCheckState.Error,
            HostCheckActionKind.OpenWindowsUpdate,
            "QuickFixWindowsUpdate");
    }

    private HostCheckResult Create64BitCheck(HostProbeData probeData)
    {
        return CreateCheck(
            "Os64BitTitle",
            "Os64BitDescription",
            "Os64BitResolution",
            probeData.Is64BitOperatingSystem ? HostCheckState.Ready : HostCheckState.Error);
    }

    private HostCheckResult CreateAdministratorCheck(HostProbeData probeData)
    {
        return CreateCheck(
            "AdminTitle",
            "AdminDescription",
            "AdminResolution",
            probeData.IsAdministrator ? HostCheckState.Ready : HostCheckState.Warning,
            HostCheckActionKind.RelaunchElevated,
            "QuickFixRunAsAdmin");
    }

    private HostCheckResult CreateHyperVCheck(HostProbeData probeData)
    {
        HostCheckState state = probeData.IsHyperVEnabled switch
        {
            true => HostCheckState.Ready,
            false => HostCheckState.Error,
            null => HostCheckState.Warning,
        };

        return CreateCheck("HyperVFeatureTitle", "HyperVFeatureDescription", "HyperVFeatureResolution", state, HostCheckActionKind.EnableHyperV, "QuickFixEnableHyperV");
    }

    private HostCheckResult CreateHypervisorCheck(HostProbeData probeData)
    {
        HostCheckState state = probeData.IsHypervisorPresent switch
        {
            true => HostCheckState.Ready,
            false => HostCheckState.Warning,
            null => HostCheckState.Warning,
        };

        return CreateCheck("HypervisorTitle", "HypervisorDescription", "HypervisorResolution", state, HostCheckActionKind.OpenRestartSettings, "QuickFixRestartSettings");
    }

    private HostCheckResult CreateVmManagementServiceCheck(HostProbeData probeData)
    {
        HostCheckState state = probeData.IsVmManagementServiceRunning switch
        {
            true => HostCheckState.Ready,
            false => HostCheckState.Error,
            null => HostCheckState.Warning,
        };

        return CreateCheck("VmmsTitle", "VmmsDescription", "VmmsResolution", state, HostCheckActionKind.StartVmManagementService, "QuickFixStartVmms");
    }

    private HostCheckResult CreateGpuCheck(HostProbeData probeData)
    {
        HostCheckState state = probeData.Gpus.Count > 0 ? HostCheckState.Ready : HostCheckState.Error;

        return CreateCheck("GpuDetectedTitle", "GpuDetectedDescription", "GpuDetectedResolution", state, HostCheckActionKind.OpenDeviceManager, "QuickFixOpenDeviceManager");
    }

    private HostCheckResult CreateCheck(string titleKey, string descriptionKey, string resolutionKey, HostCheckState state, HostCheckActionKind quickAction = HostCheckActionKind.None, string quickActionLabelKey = "")
    {
        return new HostCheckResult(
            _resources.GetString(titleKey),
            GetStateLabel(state),
            _resources.GetString(descriptionKey),
            state == HostCheckState.Ready ? string.Empty : _resources.GetString(resolutionKey),
            state,
            state == HostCheckState.Ready ? HostCheckActionKind.None : quickAction,
            state == HostCheckState.Ready || string.IsNullOrWhiteSpace(quickActionLabelKey) ? string.Empty : _resources.GetString(quickActionLabelKey));
    }

    private string GetStateLabel(HostCheckState state)
    {
        return state switch
        {
            HostCheckState.Ready => _resources.GetString("CheckStateReady"),
            HostCheckState.Warning => _resources.GetString("CheckStateWarning"),
            _ => _resources.GetString("CheckStateError"),
        };
    }

    private string GetValueOrUnknown(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? _resources.GetString("UnknownValue") : value;
    }
}