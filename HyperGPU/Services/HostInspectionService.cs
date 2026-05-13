using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using HyperGPU.Models;

namespace HyperGPU.Services;

internal sealed class HostInspectionService : IHostInspectionService
{
    private const string InspectionScript = @"
$ErrorActionPreference = 'Stop'

function Get-HyperVState {
    try {
        return (Get-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All).State -eq 'Enabled'
    }
    catch {
        return $null
    }
}

function Get-HypervisorPresent {
    try {
        return (Get-CimInstance -ClassName Win32_ComputerSystem).HypervisorPresent
    }
    catch {
        return $null
    }
}

function Get-Gpus {
    try {
        return @(
            Get-CimInstance -ClassName Win32_VideoController | ForEach-Object {
                [pscustomobject]@{
                    Name = $_.Name
                    Vendor = $_.AdapterCompatibility
                    DriverVersion = $_.DriverVersion
                }
            }
        )
    }
    catch {
        return @()
    }
}

function Get-NetworkSwitches {
    try {
        return @(
            Get-VMSwitch | Sort-Object -Property Name | ForEach-Object {
                $_.Name
            }
        )
    }
    catch {
        return @()
    }
}

function Get-VmManagementServiceRunning {
    try {
        return (Get-Service -Name vmms).Status -eq 'Running'
    }
    catch {
        return $null
    }
}

function Get-OperatingSystemLabel {
    try {
        $currentVersion = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
        $buildNumber = [int]$currentVersion.CurrentBuildNumber
        $displayVersion = $currentVersion.DisplayVersion
        $platformName = if ($buildNumber -ge 22000) { 'Windows 11' } else { 'Windows 10' }

        if ([string]::IsNullOrWhiteSpace($displayVersion)) {
            return ""$platformName (build $buildNumber)""
        }

        return ""$platformName $displayVersion (build $buildNumber)""
    }
    catch {
        return $null
    }
}

[pscustomobject]@{
    OperatingSystemLabel = Get-OperatingSystemLabel
    IsHyperVEnabled = Get-HyperVState
    IsHypervisorPresent = Get-HypervisorPresent
    IsVmManagementServiceRunning = Get-VmManagementServiceRunning
    Gpus = @(Get-Gpus)
    NetworkSwitches = @(Get-NetworkSwitches)
} | ConvertTo-Json -Depth 5 -Compress
";

    private readonly HostReadinessEvaluator _evaluator;
    private readonly IPowerShellService _powerShellService;

    public HostInspectionService(IPowerShellService powerShellService, HostReadinessEvaluator evaluator)
    {
        _powerShellService = powerShellService ?? throw new ArgumentNullException(nameof(powerShellService));
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
    }

    public async Task<HostInspectionSnapshot> InspectAsync(CancellationToken cancellationToken)
    {
        string powerShellJson = await _powerShellService.InvokeScriptAsync(InspectionScript, cancellationToken);
        PowerShellInspectionResult powerShellResult = DeserializeInspectionResult(powerShellJson);

        HostProbeData probeData = new()
        {
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Gpus = powerShellResult.Gpus,
            Is64BitOperatingSystem = Environment.Is64BitOperatingSystem,
            IsAdministrator = IsAdministrator(),
            IsHyperVEnabled = powerShellResult.IsHyperVEnabled,
            IsHypervisorPresent = powerShellResult.IsHypervisorPresent,
            IsVmManagementServiceRunning = powerShellResult.IsVmManagementServiceRunning,
            NetworkSwitches = powerShellResult.NetworkSwitches,
            IsSupportedWindowsVersion = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041),
            OperatingSystemDescription = string.IsNullOrWhiteSpace(powerShellResult.OperatingSystemLabel) ? RuntimeInformation.OSDescription : powerShellResult.OperatingSystemLabel,
        };

        return _evaluator.Evaluate(probeData, DateTimeOffset.Now);
    }

    private static bool IsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new(identity);

        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static PowerShellInspectionResult DeserializeInspectionResult(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new PowerShellInspectionResult();
        }

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        return new PowerShellInspectionResult
        {
            OperatingSystemLabel = ReadString(root, "OperatingSystemLabel"),
            Gpus = ReadGpuList(root, "Gpus"),
            IsHyperVEnabled = ReadNullableBoolean(root, "IsHyperVEnabled"),
            IsHypervisorPresent = ReadNullableBoolean(root, "IsHypervisorPresent"),
            IsVmManagementServiceRunning = ReadNullableBoolean(root, "IsVmManagementServiceRunning"),
            NetworkSwitches = ReadStringList(root, "NetworkSwitches"),
        };
    }

    private static bool? ReadNullableBoolean(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False
            ? property.GetBoolean()
            : null;
    }

    private static IReadOnlyList<string> ReadStringList(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            return [property.GetString() ?? string.Empty];
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        List<string> results = [];

        foreach (JsonElement item in property.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                results.Add(item.GetString() ?? string.Empty);
            }
        }

        return results;
    }

    private static IReadOnlyList<GpuProbeInfo> ReadGpuList(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        List<GpuProbeInfo> results = [];

        if (property.ValueKind == JsonValueKind.Object)
        {
            results.Add(ReadGpu(property));
            return results;
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (JsonElement item in property.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                results.Add(ReadGpu(item));
            }
        }

        return results;
    }

    private static GpuProbeInfo ReadGpu(JsonElement item)
    {
        return new GpuProbeInfo
        {
            Name = ReadString(item, "Name"),
            Vendor = ReadString(item, "Vendor"),
            DriverVersion = ReadString(item, "DriverVersion"),
        };
    }

    private static string ReadString(JsonElement item, string propertyName)
    {
        return item.TryGetProperty(propertyName, out JsonElement property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private sealed class PowerShellInspectionResult
    {
        public string OperatingSystemLabel { get; init; } = string.Empty;

        public IReadOnlyList<GpuProbeInfo> Gpus { get; init; } = [];

        public bool? IsHyperVEnabled { get; init; }

        public bool? IsHypervisorPresent { get; init; }

        public bool? IsVmManagementServiceRunning { get; init; }

        public IReadOnlyList<string> NetworkSwitches { get; init; } = [];
    }
}