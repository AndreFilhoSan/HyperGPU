namespace HyperGPU.Models;

public sealed class VmConfigurationDraft
{
    public string CpuCoresText { get; init; } = string.Empty;

    public string DiskSizeGbText { get; init; } = string.Empty;

    public string GpuName { get; init; } = string.Empty;

    public string GpuSharePercentageText { get; init; } = string.Empty;

    public bool IsAutoLogonEnabled { get; init; }

    public string MemoryGbText { get; init; } = string.Empty;

    public string NetworkSwitchName { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string SourcePath { get; init; } = string.Empty;

    public bool SupportsNamedGpuSelection { get; init; }

    public string Username { get; init; } = string.Empty;

    public string VhdPath { get; init; } = string.Empty;

    public string VmName { get; init; } = string.Empty;
}