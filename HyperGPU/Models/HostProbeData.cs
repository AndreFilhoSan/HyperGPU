namespace HyperGPU.Models;

public sealed class HostProbeData
{
    public string OperatingSystemDescription { get; init; } = string.Empty;

    public string Architecture { get; init; } = string.Empty;

    public bool IsSupportedWindowsVersion { get; init; }

    public bool Is64BitOperatingSystem { get; init; }

    public bool IsAdministrator { get; init; }

    public bool? IsHyperVEnabled { get; init; }

    public bool? IsHypervisorPresent { get; init; }

    public bool? IsVmManagementServiceRunning { get; init; }

    public IReadOnlyList<GpuProbeInfo> Gpus { get; init; } = [];

    public IReadOnlyList<string> NetworkSwitches { get; init; } = [];
}

public sealed class GpuProbeInfo
{
    public string Name { get; init; } = string.Empty;

    public string Vendor { get; init; } = string.Empty;

    public string DriverVersion { get; init; } = string.Empty;
}