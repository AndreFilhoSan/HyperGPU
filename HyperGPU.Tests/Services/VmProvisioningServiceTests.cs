using HyperGPU.Models;
using HyperGPU.Services;

namespace HyperGPU.Tests.Services;

[TestClass]
[DoNotParallelize]
public sealed class VmProvisioningServiceTests
{
    [TestMethod]
    public async Task ExecuteCreateVmAsync_PatchesWorkingCopyBeforeRunning()
    {
        string assetsPath = Path.Combine(AppContext.BaseDirectory, "AutomationAssets");
        if (Directory.Exists(assetsPath))
        {
            Directory.Delete(assetsPath, true);
        }

        Directory.CreateDirectory(assetsPath);
        Directory.CreateDirectory(Path.Combine(assetsPath, "User"));
        Directory.CreateDirectory(Path.Combine(assetsPath, "VMScripts"));

        await File.WriteAllTextAsync(Path.Combine(assetsPath, "Add-VMGpuPartitionAdapterFiles.psm1"), "function Test-Asset {}\n");
        await File.WriteAllTextAsync(Path.Combine(assetsPath, "autounattend.xml"), "<unattend />\n");
        await File.WriteAllTextAsync(Path.Combine(assetsPath, "gpt.ini"), "[General]\n");
        await File.WriteAllTextAsync(Path.Combine(assetsPath, "Update-VMGpuPartitionDriver.ps1"), "param()\nImport-Module $PSSCriptRoot\\Add-VMGpuPartitionAdapterFiles.psm1\n$DriveLetter = (Mount-VHD -Path $VHD.Path -PassThru | Get-Disk | Get-Partition | Get-Volume | Where-Object {$_.DriveLetter} | ForEach-Object DriveLetter)\n");
        await File.WriteAllTextAsync(Path.Combine(assetsPath, "User", "Install.ps1"), "Write-Host 'Install'\n");
        await File.WriteAllTextAsync(Path.Combine(assetsPath, "User", "psscripts.ini"), "0Parameters=\n");
        await File.WriteAllTextAsync(Path.Combine(assetsPath, "VMScripts", "VBCableInstall.ps1"), "Write-Host 'VBCable'\n");
        await File.WriteAllTextAsync(
            Path.Combine(assetsPath, "CopyFilesToVM.ps1"),
                "$params = @{\n    VMName = 'OLD'\n}\n\nImport-Module $PSSCriptRoot\\Add-VMGpuPartitionAdapterFiles.psm1\n\nFunction SmartExit {\nparam (\n[switch]$NoHalt,\n[string]$ExitReason\n)\nRead-host -Prompt \"Press any key to Exit...\"\nExit\n}\n\nFunction Check-Params {\n}\n\nAdd-Type -TypeDefinition $code -ReferencedAssemblies \"System.Xml\",\"System.Linq\",\"System.Xml.Linq\" -ErrorAction SilentlyContinue\n        vmconnect localhost $VMName\n\nSmartExit -ExitReason \"If all went well the Virtual Machine will have started, \r\nIn a few minutes it will load the Windows desktop, \r\nwhen it does, install your favorite high performance remote desktop! \" ");

        RecordingPowerShellService powerShellService = new();
        VmProvisioningService service = new(powerShellService, new FakeResourceService());

        VmExecutionResult result = await service.ExecuteCreateVmAsync(
            new VmConfigurationDraft
            {
                CpuCoresText = "4",
                DiskSizeGbText = "40",
                GpuName = "AUTO",
                GpuSharePercentageText = "50",
                IsAutoLogonEnabled = true,
                MemoryGbText = "8",
                NetworkSwitchName = "Default Switch",
                Password = "Password123!",
                SourcePath = "C:\\ISO\\Windows.iso",
                Username = "GPUVM",
                VhdPath = "C:\\HyperV",
                VmName = "GPUPV",
            },
            progress: null,
            outputProgress: null,
            CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(3, result.Stages.Count);
        Assert.AreEqual(VmExecutionStageState.Completed, result.Stages[0].State);
        Assert.AreEqual(VmExecutionStageState.Completed, result.Stages[1].State);
        Assert.AreEqual(VmExecutionStageState.Completed, result.Stages[2].State);
        Assert.IsNotNull(powerShellService.ScriptPath);
        string patchedScript = await File.ReadAllTextAsync(powerShellService.ScriptPath!);
        StringAssert.Contains(patchedScript, "VMName = 'GPUPV'");
        StringAssert.Contains(patchedScript, "throw $ExitReason");
        StringAssert.Contains(patchedScript, "'System.Xml.dll','System.Xml.Linq.dll'");
        StringAssert.Contains(patchedScript, "Start-Process -FilePath 'vmconnect.exe' -ArgumentList @('localhost', $VMName) | Out-Null");
        Assert.IsFalse(patchedScript.Contains("Read-host -Prompt", StringComparison.Ordinal));
        Assert.IsTrue(powerShellService.InlineScripts.Any(script => script.Contains("Get-VM -Name $vmName", StringComparison.Ordinal)));
        Assert.IsTrue(powerShellService.InlineScripts.Any(script => script.Contains("Remove-VM -Name $vmName -Force", StringComparison.Ordinal)));
        StringAssert.Contains(powerShellService.LastInlineScript!, "-VMName 'GPUPV' -GPUName 'AUTO'");

        string patchedDriverScript = await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(powerShellService.ScriptPath!)!, "Update-VMGpuPartitionDriver.ps1"));
        StringAssert.Contains(patchedDriverScript, "Get-DiskImage -ImagePath $VHD.Path");
        StringAssert.Contains(patchedDriverScript, "VHDX is already mounted; reusing the existing mount.");
        StringAssert.Contains(patchedDriverScript, "Add-PartitionAccessPath");
        StringAssert.Contains(patchedDriverScript, "Unlock-BitLocker");
        StringAssert.Contains(patchedDriverScript, "Dismount-VHD -Path $VHD.Path");
    }

    [TestMethod]
    public async Task ExecuteUpdateExistingVmAsync_RunsOnlyRobustDriverRefresh()
    {
        string assetsPath = Path.Combine(AppContext.BaseDirectory, "AutomationAssets");
        if (Directory.Exists(assetsPath))
        {
            Directory.Delete(assetsPath, true);
        }

        Directory.CreateDirectory(assetsPath);
        await File.WriteAllTextAsync(Path.Combine(assetsPath, "Update-VMGpuPartitionDriver.ps1"), "param()\nImport-Module $PSSCriptRoot\\Add-VMGpuPartitionAdapterFiles.psm1\nMount-VHD -Path $VHD.Path\n");

        RecordingPowerShellService powerShellService = new();
        VmProvisioningService service = new(powerShellService, new FakeResourceService());

        VmExecutionResult result = await service.ExecuteUpdateExistingVmAsync(
            new VmConfigurationDraft
            {
                GpuName = "AUTO",
                VmName = "GPUPV",
            },
            progress: null,
            outputProgress: null,
            CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(2, result.Stages.Count);
        Assert.IsNull(powerShellService.ScriptPath);
        Assert.IsNotNull(powerShellService.LastInlineScript);
        StringAssert.Contains(powerShellService.LastInlineScript!, "-VMName 'GPUPV' -GPUName 'AUTO'");
        string driverScriptPath = ExtractQuotedScriptPath(powerShellService.LastInlineScript!);
        string patchedDriverScript = await File.ReadAllTextAsync(driverScriptPath);
        StringAssert.Contains(patchedDriverScript, "$ErrorActionPreference = 'Stop'");
        StringAssert.Contains(patchedDriverScript, "BitLockerRecoveryPassword");
        StringAssert.Contains(patchedDriverScript, "Get-DiskImage -ImagePath $VHD.Path");
    }

    private static string ExtractQuotedScriptPath(string command)
    {
        int start = command.IndexOf("& '", StringComparison.Ordinal) + 3;
        int end = command.IndexOf("' -VMName", start, StringComparison.Ordinal);
        return command[start..end];
    }

    private sealed class RecordingPowerShellService : IPowerShellService
    {
        public string? LastInlineScript { get; private set; }

        public List<string> InlineScripts { get; } = [];

        public string? ScriptPath { get; private set; }

        public Task<T?> InvokeJsonAsync<T>(string script, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<string> InvokeScriptAsync(string script, CancellationToken cancellationToken)
        {
            LastInlineScript = script;
            InlineScripts.Add(script);
            if (script.Contains("Get-Service -Name vmms", StringComparison.Ordinal))
            {
                return Task.FromResult("Running");
            }

            return Task.FromResult("driver done");
        }

        public Task<string> InvokeScriptAsync(string script, IProgress<string>? outputProgress, CancellationToken cancellationToken)
        {
            outputProgress?.Report("INFO   : Refreshing guest drivers...");
            return InvokeScriptAsync(script, cancellationToken);
        }

        public Task<string> InvokeScriptFileAsync(string scriptPath, CancellationToken cancellationToken)
        {
            ScriptPath = scriptPath;
            return Task.FromResult("done");
        }

        public Task<string> InvokeScriptFileAsync(string scriptPath, IProgress<string>? outputProgress, CancellationToken cancellationToken)
        {
            outputProgress?.Report("INFO   : Creating sparse disk...");
            return InvokeScriptFileAsync(scriptPath, cancellationToken);
        }
    }

    private sealed class FakeResourceService : IAppResourceService
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal)
        {
            ["VmExecutionAssetsMissingFormat"] = "Expected automation assets folder not found: {0}",
            ["VmExecutionStageCompleted"] = "Completed",
            ["VmExecutionStageCreateCompleted"] = "The VM creation script completed.",
            ["VmExecutionStageCreatePending"] = "The VM creation script is waiting to start.",
            ["VmExecutionStageCreateRunning"] = "The VM creation script is running.",
            ["VmExecutionStageCreateTitle"] = "Create VM",
            ["VmExecutionStageDriverCompleted"] = "The guest GPU driver refresh completed.",
            ["VmExecutionStageDriverPending"] = "The guest GPU driver refresh is waiting to start.",
            ["VmExecutionStageDriverRunning"] = "The guest GPU driver refresh is running.",
            ["VmExecutionStageDriverSkipped"] = "The guest GPU driver refresh was skipped because the VM creation step failed.",
            ["VmExecutionStageDriverTitle"] = "Refresh guest drivers",
            ["VmExecutionStageFailed"] = "Failed",
            ["VmExecutionStagePending"] = "Pending",
            ["VmExecutionStagePrepareCompletedFormat"] = "Automation assets were prepared in {0}.",
            ["VmExecutionStagePreparePending"] = "The temporary automation workspace is waiting to be prepared.",
            ["VmExecutionStagePrepareRunning"] = "The temporary automation workspace is being prepared.",
            ["VmExecutionStagePrepareTitle"] = "Prepare workspace",
            ["VmExecutionStageRunning"] = "Running",
            ["VmExecutionStageSkipped"] = "Skipped",
            ["VmExecutionSummaryAssetsMissing"] = "Automation assets are missing from the app output.",
            ["VmExecutionSummaryCreateFailed"] = "VM provisioning stopped during the VM creation step.",
            ["VmExecutionSummaryDriverFailed"] = "VM creation finished, but the guest GPU driver refresh failed.",
            ["VmExecutionSummarySuccess"] = "VM provisioning and guest GPU driver refresh finished.",
            ["VmExecutionSummaryUpdateSuccess"] = "Existing VM guest GPU driver refresh finished.",
        };

        public string GetString(string key)
        {
            return _values.TryGetValue(key, out string? value) ? value : key;
        }
    }
}