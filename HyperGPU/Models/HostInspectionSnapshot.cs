namespace HyperGPU.Models;

public sealed class HostInspectionSnapshot
{
    public HostInspectionSnapshot(
        string operatingSystemDescription,
        string architecture,
        DateTimeOffset capturedAt,
        IReadOnlyList<HostCheckResult> checks,
        IReadOnlyList<GpuDeviceInfo> gpuDevices,
        IReadOnlyList<string> networkSwitches)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operatingSystemDescription);
        ArgumentException.ThrowIfNullOrWhiteSpace(architecture);
        ArgumentNullException.ThrowIfNull(checks);
        ArgumentNullException.ThrowIfNull(gpuDevices);
        ArgumentNullException.ThrowIfNull(networkSwitches);

        OperatingSystemDescription = operatingSystemDescription;
        Architecture = architecture;
        CapturedAt = capturedAt;
        Checks = checks;
        GpuDevices = gpuDevices;
        NetworkSwitches = networkSwitches;
    }

    public string OperatingSystemDescription { get; }

    public string Architecture { get; }

    public DateTimeOffset CapturedAt { get; }

    public IReadOnlyList<HostCheckResult> Checks { get; }

    public IReadOnlyList<GpuDeviceInfo> GpuDevices { get; }

    public IReadOnlyList<string> NetworkSwitches { get; }
}