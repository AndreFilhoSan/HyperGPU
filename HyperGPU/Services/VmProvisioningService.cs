using System.Globalization;
using System.Text;
using HyperGPU.Models;

namespace HyperGPU.Services;

public sealed class VmProvisioningService : IVmProvisioningService
{
    private const string AutomationAssetsFolderName = "AutomationAssets";
    private const string CopyFilesScriptName = "CopyFilesToVM.ps1";
    private const string DriverUpdateScriptName = "Update-VMGpuPartitionDriver.ps1";
    private const string ModuleImportLine = "Import-Module $PSSCriptRoot\\Add-VMGpuPartitionAdapterFiles.psm1";
    private const string SmartExitMarker = "Function SmartExit {";
    private const string CheckParamsMarker = "Function Check-Params {";
    private const string SuccessExitPrefix = "If all went well the Virtual Machine will have started";

    private readonly IPowerShellService _powerShellService;
    private readonly IAppResourceService _resources;

    public VmProvisioningService(IPowerShellService powerShellService, IAppResourceService resources)
    {
        _powerShellService = powerShellService ?? throw new ArgumentNullException(nameof(powerShellService));
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
    }

    public async Task<VmExecutionResult> ExecuteCreateVmAsync(VmConfigurationDraft draft, IProgress<VmExecutionStageResult>? progress, IProgress<string>? outputProgress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        string prepareTitle = _resources.GetString("VmExecutionStagePrepareTitle");
        string createTitle = _resources.GetString("VmExecutionStageCreateTitle");
        string driverTitle = _resources.GetString("VmExecutionStageDriverTitle");

        List<VmExecutionStageResult> stages =
        [
            CreateStage(prepareTitle, VmExecutionStageState.Pending, "VmExecutionStagePending", "VmExecutionStagePreparePending"),
            CreateStage(createTitle, VmExecutionStageState.Pending, "VmExecutionStagePending", "VmExecutionStageCreatePending"),
            CreateStage(driverTitle, VmExecutionStageState.Pending, "VmExecutionStagePending", "VmExecutionStageDriverPending"),
        ];

        string automationAssetsPath = Path.Combine(AppContext.BaseDirectory, AutomationAssetsFolderName);
        if (!TryValidateAutomationAssets(automationAssetsPath, prepareTitle, stages, progress, out VmExecutionResult? missingAssetsResult))
        {
            return missingAssetsResult;
        }

        UpdateStage(stages, prepareTitle, VmExecutionStageState.Running, "VmExecutionStageRunning", "VmExecutionStagePrepareRunning", progress);

        try
        {
            await EnsureVmManagementServiceRunningAsync(cancellationToken);
            outputProgress?.Report("INFO   : Hyper-V Virtual Machine Management service is running.");
        }
        catch (Exception ex)
        {
            UpdateStage(stages, prepareTitle, VmExecutionStageState.Failed, "VmExecutionStageFailed", ex.Message, progress);
            return new VmExecutionResult(
                false,
                _resources.GetString("VmExecutionSummaryCreateFailed"),
                FormatExecutionOutput(ex.Message, AppContext.BaseDirectory),
                AppContext.BaseDirectory,
                stages);
        }

        if (!Directory.Exists(draft.VhdPath))
        {
            Directory.CreateDirectory(draft.VhdPath);
            outputProgress?.Report($"INFO   : Created VHD destination folder: {draft.VhdPath}");
        }

        try
        {
            await ResetExistingVmArtifactsAsync(draft, outputProgress, cancellationToken);
        }
        catch (Exception ex)
        {
            UpdateStage(stages, prepareTitle, VmExecutionStageState.Failed, "VmExecutionStageFailed", ex.Message, progress);
            return new VmExecutionResult(
                false,
                _resources.GetString("VmExecutionSummaryCreateFailed"),
                FormatExecutionOutput(ex.Message, AppContext.BaseDirectory),
                AppContext.BaseDirectory,
                stages);
        }

        string workingDirectory = Path.Combine(
            Path.GetTempPath(),
            "HyperGPU",
            DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture),
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

        DirectoryCopy(automationAssetsPath, workingDirectory);

        string scriptPath = Path.Combine(workingDirectory, CopyFilesScriptName);
        string scriptContent = await File.ReadAllTextAsync(scriptPath, cancellationToken);
        string updatedContent = PatchCopyFilesScript(scriptContent, draft);
        await File.WriteAllTextAsync(scriptPath, updatedContent, cancellationToken);

        string driverScriptPath = Path.Combine(workingDirectory, DriverUpdateScriptName);
        await PatchDriverUpdateScriptAsync(driverScriptPath, cancellationToken);
        UpdateStage(stages, prepareTitle, VmExecutionStageState.Completed, "VmExecutionStageCompleted", string.Format(CultureInfo.CurrentCulture, _resources.GetString("VmExecutionStagePrepareCompletedFormat"), workingDirectory), progress);

        string createOutput;

        try
        {
            UpdateStage(stages, createTitle, VmExecutionStageState.Running, "VmExecutionStageRunning", "VmExecutionStageCreateRunning", progress);
            createOutput = await _powerShellService.InvokeScriptFileAsync(scriptPath, outputProgress, cancellationToken);
            UpdateStage(stages, createTitle, VmExecutionStageState.Completed, "VmExecutionStageCompleted", "VmExecutionStageCreateCompleted", progress);
        }
        catch (Exception ex)
        {
            UpdateStage(stages, createTitle, VmExecutionStageState.Failed, "VmExecutionStageFailed", ex.Message, progress);
            UpdateStage(stages, driverTitle, VmExecutionStageState.Skipped, "VmExecutionStageSkipped", "VmExecutionStageDriverSkipped", progress);
            return new VmExecutionResult(
                false,
                _resources.GetString("VmExecutionSummaryCreateFailed"),
                FormatExecutionOutput(ex.Message, workingDirectory),
                workingDirectory,
                stages);
        }

        try
        {
            UpdateStage(stages, driverTitle, VmExecutionStageState.Running, "VmExecutionStageRunning", "VmExecutionStageDriverRunning", progress);
            string updateCommand = BuildDriverUpdateCommand(driverScriptPath, draft);
            string driverOutput = await _powerShellService.InvokeScriptAsync(updateCommand, outputProgress, cancellationToken);
            UpdateStage(stages, driverTitle, VmExecutionStageState.Completed, "VmExecutionStageCompleted", "VmExecutionStageDriverCompleted", progress);

            return new VmExecutionResult(
                true,
                _resources.GetString("VmExecutionSummarySuccess"),
                FormatExecutionOutput($"[Create VM]{Environment.NewLine}{createOutput}{Environment.NewLine}{Environment.NewLine}[Refresh Drivers]{Environment.NewLine}{driverOutput}", workingDirectory),
                workingDirectory,
                stages);
        }
        catch (Exception ex)
        {
            UpdateStage(stages, driverTitle, VmExecutionStageState.Failed, "VmExecutionStageFailed", ex.Message, progress);
            return new VmExecutionResult(
                false,
                _resources.GetString("VmExecutionSummaryDriverFailed"),
                FormatExecutionOutput($"[Create VM]{Environment.NewLine}{createOutput}{Environment.NewLine}{Environment.NewLine}[Refresh Drivers]{Environment.NewLine}{ex.Message}", workingDirectory),
                workingDirectory,
                stages);
        }
    }

    public async Task<VmExecutionResult> ExecuteUpdateExistingVmAsync(VmConfigurationDraft draft, IProgress<VmExecutionStageResult>? progress, IProgress<string>? outputProgress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        string prepareTitle = _resources.GetString("VmExecutionStagePrepareTitle");
        string driverTitle = _resources.GetString("VmExecutionStageDriverTitle");

        List<VmExecutionStageResult> stages =
        [
            CreateStage(prepareTitle, VmExecutionStageState.Pending, "VmExecutionStagePending", "VmExecutionStagePreparePending"),
            CreateStage(driverTitle, VmExecutionStageState.Pending, "VmExecutionStagePending", "VmExecutionStageDriverPending"),
        ];

        string automationAssetsPath = Path.Combine(AppContext.BaseDirectory, AutomationAssetsFolderName);
        if (!TryValidateAutomationAssets(automationAssetsPath, prepareTitle, stages, progress, out VmExecutionResult? missingAssetsResult))
        {
            return missingAssetsResult;
        }

        UpdateStage(stages, prepareTitle, VmExecutionStageState.Running, "VmExecutionStageRunning", "VmExecutionStagePrepareRunning", progress);

        try
        {
            await EnsureVmManagementServiceRunningAsync(cancellationToken);
            outputProgress?.Report("INFO   : Hyper-V Virtual Machine Management service is running.");
        }
        catch (Exception ex)
        {
            UpdateStage(stages, prepareTitle, VmExecutionStageState.Failed, "VmExecutionStageFailed", ex.Message, progress);
            return new VmExecutionResult(
                false,
                _resources.GetString("VmExecutionSummaryDriverFailed"),
                FormatExecutionOutput(ex.Message, AppContext.BaseDirectory),
                AppContext.BaseDirectory,
                stages);
        }

        string workingDirectory = Path.Combine(
            Path.GetTempPath(),
            "HyperGPU",
            DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture),
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

        DirectoryCopy(automationAssetsPath, workingDirectory);
        string driverScriptPath = Path.Combine(workingDirectory, DriverUpdateScriptName);
        await PatchDriverUpdateScriptAsync(driverScriptPath, cancellationToken);
        UpdateStage(stages, prepareTitle, VmExecutionStageState.Completed, "VmExecutionStageCompleted", string.Format(CultureInfo.CurrentCulture, _resources.GetString("VmExecutionStagePrepareCompletedFormat"), workingDirectory), progress);

        try
        {
            UpdateStage(stages, driverTitle, VmExecutionStageState.Running, "VmExecutionStageRunning", "VmExecutionStageDriverRunning", progress);
            string updateCommand = BuildDriverUpdateCommand(driverScriptPath, draft);
            string driverOutput = await _powerShellService.InvokeScriptAsync(updateCommand, outputProgress, cancellationToken);
            UpdateStage(stages, driverTitle, VmExecutionStageState.Completed, "VmExecutionStageCompleted", "VmExecutionStageDriverCompleted", progress);

            return new VmExecutionResult(
                true,
                _resources.GetString("VmExecutionSummaryUpdateSuccess"),
                FormatExecutionOutput($"[Refresh Existing VM Drivers]{Environment.NewLine}{driverOutput}", workingDirectory),
                workingDirectory,
                stages);
        }
        catch (Exception ex)
        {
            UpdateStage(stages, driverTitle, VmExecutionStageState.Failed, "VmExecutionStageFailed", ex.Message, progress);
            return new VmExecutionResult(
                false,
                _resources.GetString("VmExecutionSummaryDriverFailed"),
                FormatExecutionOutput($"[Refresh Existing VM Drivers]{Environment.NewLine}{ex.Message}", workingDirectory),
                workingDirectory,
                stages);
        }
    }

    private static string PatchCopyFilesScript(string scriptContent, VmConfigurationDraft draft)
    {
        string withParams = ReplaceSection(
            scriptContent,
            "$params = @{",
            ModuleImportLine,
            BuildParameterBlock(draft) + Environment.NewLine + Environment.NewLine + ModuleImportLine);

        string withNonInteractiveExit = ReplaceSection(
            withParams,
            SmartExitMarker,
            CheckParamsMarker,
            BuildNonInteractiveSmartExit() + Environment.NewLine + Environment.NewLine + CheckParamsMarker);

        string withNonBlockingVmConnect = PatchVmConnectLaunch(withNonInteractiveExit);
        return PatchWindowsImageTypeReferences(withNonBlockingVmConnect);
    }

    private static string PatchVmConnectLaunch(string scriptContent)
    {
        return scriptContent.Replace(
            "        vmconnect localhost $VMName",
            "        Start-Process -FilePath 'vmconnect.exe' -ArgumentList @('localhost', $VMName) | Out-Null",
            StringComparison.Ordinal);
    }

    private static string PatchWindowsImageTypeReferences(string scriptContent)
    {
        return scriptContent.Replace(
            "Add-Type -TypeDefinition $code -ReferencedAssemblies \"System.Xml\",\"System.Linq\",\"System.Xml.Linq\" -ErrorAction SilentlyContinue",
            "Add-Type -TypeDefinition $code -ReferencedAssemblies 'System.Xml.dll','System.Xml.Linq.dll' -ErrorAction Stop",
            StringComparison.Ordinal);
    }

    private async Task PatchDriverUpdateScriptAsync(string scriptPath, CancellationToken cancellationToken)
    {
        string scriptContent = await File.ReadAllTextAsync(scriptPath, cancellationToken);
        await File.WriteAllTextAsync(scriptPath, BuildRobustDriverUpdateScript(scriptContent), cancellationToken);
    }

    private static string BuildRobustDriverUpdateScript(string originalScript)
    {
        string importLine = originalScript.Contains(ModuleImportLine, StringComparison.Ordinal)
            ? ModuleImportLine
            : "Import-Module $PSSCriptRoot\\Add-VMGpuPartitionAdapterFiles.psm1";

        return $$"""
$ErrorActionPreference = 'Stop'

Param (
[string]$VMName,
[string]$GPUName,
[string]$Hostname = $ENV:Computername,
[string]$BitLockerRecoveryPassword = ''
)

{{importLine}}

$VM = Get-VM -VMName $VMName -ErrorAction Stop
$VHD = Get-VHD -VMId $VM.VMId -ErrorAction Stop
$stateWasRunning = $VM.State -eq 'Running'

if ($VM.State -ne 'Off') {
    Write-Host 'INFO   : Attempting to shut down VM before refreshing GPU driver files...'
    Stop-VM -Name $VMName -Force -ErrorAction Stop
}

while ((Get-VM -VMName $VMName -ErrorAction Stop).State -ne 'Off') {
    Start-Sleep -Seconds 3
    Write-Host 'INFO   : Waiting for VM to shut down before mounting the VHDX...'
}

$mountedHere = $false
$disk = $null

try {
    Write-Host 'INFO   : Resolving VHDX mount state...'
    $diskImage = Get-DiskImage -ImagePath $VHD.Path -ErrorAction SilentlyContinue

    if ($null -ne $diskImage -and $diskImage.Attached) {
        Write-Host 'INFO   : VHDX is already mounted; reusing the existing mount.'
        $disk = $diskImage | Get-Disk -ErrorAction Stop
    }
    else {
        Write-Host 'INFO   : Mounting VHDX...'
        $disk = Mount-VHD -Path $VHD.Path -PassThru -ErrorAction Stop | Get-Disk -ErrorAction Stop
        $mountedHere = $true
    }

    if ($disk.IsOffline) {
        Set-Disk -Number $disk.Number -IsOffline $false -ErrorAction Stop
    }

    if ($disk.IsReadOnly) {
        Set-Disk -Number $disk.Number -IsReadOnly $false -ErrorAction Stop
    }

    $partition = $disk | Get-Partition -ErrorAction Stop | Where-Object { $_.DriveLetter } | Sort-Object -Property PartitionNumber | Select-Object -First 1

    if ($null -eq $partition) {
        $partition = $disk | Get-Partition -ErrorAction Stop | Where-Object { $_.Type -ne 'Reserved' -and $_.Size -gt 1GB } | Sort-Object -Property Size -Descending | Select-Object -First 1

        if ($null -eq $partition) {
            throw 'Could not find a usable Windows partition in the mounted VHDX.'
        }

        Write-Host 'INFO   : Assigning a temporary drive letter to the guest Windows partition...'
        Add-PartitionAccessPath -DiskNumber $partition.DiskNumber -PartitionNumber $partition.PartitionNumber -AssignDriveLetter -ErrorAction Stop
        $partition = Get-Partition -DiskNumber $partition.DiskNumber -PartitionNumber $partition.PartitionNumber -ErrorAction Stop
    }

    $driveLetter = $partition.DriveLetter

    if ([string]::IsNullOrWhiteSpace($driveLetter)) {
        throw 'The mounted VHDX partition did not receive a drive letter.'
    }

    $mountPoint = "${driveLetter}:"

    if (Get-Command -Name Get-BitLockerVolume -ErrorAction SilentlyContinue) {
        $bitLockerVolume = Get-BitLockerVolume -MountPoint $mountPoint -ErrorAction SilentlyContinue

        if ($null -ne $bitLockerVolume -and $bitLockerVolume.LockStatus -eq 'Locked') {
            if ([string]::IsNullOrWhiteSpace($BitLockerRecoveryPassword)) {
                throw 'The guest VHDX is BitLocker locked. Provide a BitLocker recovery password before refreshing driver files.'
            }

            Write-Host 'INFO   : Unlocking BitLocker-protected guest volume...'
            Unlock-BitLocker -MountPoint $mountPoint -RecoveryPassword $BitLockerRecoveryPassword -ErrorAction Stop | Out-Null
        }
    }

    Write-Host 'INFO   : Copying GPU driver files into the guest VHDX. This could take a while...'
    Add-VMGPUPartitionAdapterFiles -hostname $Hostname -DriveLetter $driveLetter -GPUName $GPUName
}
finally {
    if ($mountedHere) {
        Write-Host 'INFO   : Dismounting VHDX...'
        Dismount-VHD -Path $VHD.Path -ErrorAction SilentlyContinue
    }

    if ($stateWasRunning) {
        Write-Host 'INFO   : Previous state was running, starting VM...'
        Start-VM $VMName -ErrorAction SilentlyContinue
    }
}

Write-Host 'INFO   : Guest GPU driver refresh completed.'
""";
    }

    private static string BuildParameterBlock(VmConfigurationDraft draft)
    {
        StringBuilder builder = new();

        builder.AppendLine("$params = @{");
        builder.AppendLine($"    VMName = {Quote(draft.VmName)}");
        builder.AppendLine($"    SourcePath = {Quote(draft.SourcePath)}");
        builder.AppendLine("    Edition = 6");
        builder.AppendLine("    VhdFormat = 'VHDX'");
        builder.AppendLine("    DiskLayout = 'UEFI'");
        builder.AppendLine($"    SizeBytes = {draft.DiskSizeGbText}GB");
        builder.AppendLine($"    MemoryAmount = {draft.MemoryGbText}GB");
        builder.AppendLine($"    CPUCores = {draft.CpuCoresText}");
        builder.AppendLine($"    NetworkSwitch = {Quote(draft.NetworkSwitchName)}");
        builder.AppendLine($"    VHDPath = {Quote(draft.VhdPath)}");
        builder.AppendLine("    UnattendPath = \"$PSScriptRoot\\autounattend.xml\"");
        builder.AppendLine($"    GPUName = {Quote(draft.GpuName)}");
        builder.AppendLine($"    GPUResourceAllocationPercentage = {draft.GpuSharePercentageText}");
        builder.AppendLine("    Team_ID = ''");
        builder.AppendLine("    Key = ''");
        builder.AppendLine($"    Username = {Quote(draft.Username)}");
        builder.AppendLine($"    Password = {Quote(draft.Password)}");
        builder.AppendLine($"    Autologon = {Quote(draft.IsAutoLogonEnabled ? "true" : "false")}");
        builder.Append('}');

        return builder.ToString();
    }

    private async Task EnsureVmManagementServiceRunningAsync(CancellationToken cancellationToken)
    {
        const string script = @"
$service = Get-Service -Name vmms -ErrorAction Stop
if ($service.Status -ne 'Running') {
    Start-Service -Name vmms -ErrorAction Stop
    $service.WaitForStatus('Running', [TimeSpan]::FromSeconds(20))
}
(Get-Service -Name vmms -ErrorAction Stop).Status
";

        string output = await _powerShellService.InvokeScriptAsync(script, cancellationToken);

        if (!output.Contains("Running", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Hyper-V Virtual Machine Management service did not start. Start the service and refresh host checks before creating the VM.");
        }
    }

    private async Task ResetExistingVmArtifactsAsync(VmConfigurationDraft draft, IProgress<string>? outputProgress, CancellationToken cancellationToken)
    {
        string vmName = Quote(draft.VmName);
        string vhdFilePath = Quote(BuildVhdFilePath(draft));

        string script = $@"
$vmName = {vmName}
$vhdPath = {vhdFilePath}
$existingVm = Get-VM -Name $vmName -ErrorAction SilentlyContinue

if ($null -ne $existingVm) {{
    Write-Host ""INFO   : Removing previous VM attempt '$vmName' before retrying...""

    if ($existingVm.State -ne 'Off') {{
        Stop-VM -Name $vmName -TurnOff -Force -ErrorAction Stop | Out-Null
    }}

    Remove-VM -Name $vmName -Force -ErrorAction Stop
    Write-Host ""INFO   : Previous VM '$vmName' was removed.""
}}

if (Test-Path -LiteralPath $vhdPath) {{
    Write-Host ""INFO   : Removing previous VHDX attempt at $vhdPath...""
    Remove-Item -LiteralPath $vhdPath -Force -ErrorAction Stop
    Write-Host ""INFO   : Previous VHDX was removed.""
}}
";

        await _powerShellService.InvokeScriptAsync(script, outputProgress, cancellationToken);
    }

    private bool TryValidateAutomationAssets(string automationAssetsPath, string prepareTitle, List<VmExecutionStageResult> stages, IProgress<VmExecutionStageResult>? progress, out VmExecutionResult result)
    {
        if (Directory.Exists(automationAssetsPath))
        {
            result = null!;
            return true;
        }

        UpdateStage(stages, prepareTitle, VmExecutionStageState.Failed, "VmExecutionStageFailed", string.Format(CultureInfo.CurrentCulture, _resources.GetString("VmExecutionAssetsMissingFormat"), automationAssetsPath), progress);
        result = new VmExecutionResult(
            false,
            _resources.GetString("VmExecutionSummaryAssetsMissing"),
            $"Expected folder not found: {automationAssetsPath}",
            automationAssetsPath,
            stages);
        return false;
    }

    private static string BuildDriverUpdateCommand(string scriptPath, VmConfigurationDraft draft)
    {
        string escapedPath = scriptPath.Replace("'", "''", StringComparison.Ordinal);
        return $"& '{escapedPath}' -VMName {Quote(draft.VmName)} -GPUName {Quote(draft.GpuName)}";
    }

    private static string BuildVhdFilePath(VmConfigurationDraft draft)
    {
        return Path.Combine(draft.VhdPath, $"{draft.VmName}.vhdx");
    }

    private static string BuildNonInteractiveSmartExit()
    {
        return $"Function SmartExit {{\r\nparam (\r\n[switch]$NoHalt,\r\n[string]$ExitReason\r\n)\r\nif ([string]::IsNullOrWhiteSpace($ExitReason)) {{\r\n    throw 'Script terminated without an explicit reason.'\r\n}}\r\nif ($ExitReason.StartsWith('{SuccessExitPrefix}', [System.StringComparison]::Ordinal)) {{\r\n    Write-Host $ExitReason\r\n    return\r\n}}\r\nthrow $ExitReason\r\n}}";
    }

    private static string FormatExecutionOutput(string output, string workingDirectory)
    {
        return $"Working directory: {workingDirectory}{Environment.NewLine}{Environment.NewLine}{output}".Trim();
    }

    private static string Quote(string value)
    {
        return $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
    }

    private static string ReplaceSection(string content, string startMarker, string endMarker, string replacement)
    {
        int startIndex = content.IndexOf(startMarker, StringComparison.Ordinal);
        int endIndex = content.IndexOf(endMarker, StringComparison.Ordinal);

        if (startIndex < 0 || endIndex < 0 || endIndex <= startIndex)
        {
            throw new InvalidOperationException($"Could not patch automation script between '{startMarker}' and '{endMarker}'.");
        }

        return string.Concat(content.AsSpan(0, startIndex), replacement, content.AsSpan(endIndex + endMarker.Length));
    }

    private static void DirectoryCopy(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (string directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
        }

        foreach (string file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDirectory, file);
            string destinationPath = Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(file, destinationPath, true);
        }
    }

    private VmExecutionStageResult CreateStage(string title, VmExecutionStageState state, string statusKey, string messageKey)
    {
        return new VmExecutionStageResult(title, state, _resources.GetString(statusKey), _resources.GetString(messageKey));
    }

    private void UpdateStage(List<VmExecutionStageResult> stages, string title, VmExecutionStageState state, string statusKey, string message, IProgress<VmExecutionStageResult>? progress)
    {
        string resolvedMessage = message.Contains(' ')
            ? message
            : _resources.GetString(message);

        VmExecutionStageResult updatedStage = new(title, state, _resources.GetString(statusKey), resolvedMessage);
        int existingIndex = stages.FindIndex(stage => string.Equals(stage.Title, title, StringComparison.Ordinal));

        if (existingIndex >= 0)
        {
            stages[existingIndex] = updatedStage;
        }

        progress?.Report(updatedStage);
    }
}