using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using HyperGPU.Models;

namespace HyperGPU.Services;

internal sealed class HostQuickActionService : IHostQuickActionService
{
    public Task ExecuteAsync(HostCheckActionKind action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return action switch
        {
            HostCheckActionKind.None => Task.CompletedTask,
            HostCheckActionKind.RefreshChecks => Task.CompletedTask,
            HostCheckActionKind.OpenWindowsUpdate => LaunchAsync(new ProcessStartInfo("ms-settings:windowsupdate") { UseShellExecute = true }),
            HostCheckActionKind.RelaunchElevated => RelaunchElevatedAsync(),
            HostCheckActionKind.EnableHyperV => LaunchAsync(
                new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoLogo -NoProfile -ExecutionPolicy Bypass -Command \"Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All -All\"",
                    UseShellExecute = true,
                    Verb = "runas",
                }),
            HostCheckActionKind.StartVmManagementService => LaunchAsync(
                new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoLogo -NoProfile -ExecutionPolicy Bypass -Command \"Start-Service -Name vmms; Set-Service -Name vmms -StartupType Automatic\"",
                    UseShellExecute = true,
                    Verb = "runas",
                }),
            HostCheckActionKind.OpenRestartSettings => LaunchAsync(new ProcessStartInfo("ms-settings:recovery") { UseShellExecute = true }),
            HostCheckActionKind.OpenDeviceManager => LaunchAsync(new ProcessStartInfo("devmgmt.msc") { UseShellExecute = true }),
            _ => Task.CompletedTask,
        };
    }

    private static Task RelaunchElevatedAsync()
    {
        string executablePath = Environment.ProcessPath ?? throw new InvalidOperationException("The current application path is unavailable.");

        if (IsRunningElevated())
        {
            return Task.CompletedTask;
        }

        bool launched = TryLaunch(
            new ProcessStartInfo(executablePath)
            {
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppContext.BaseDirectory,
            });

        if (launched)
        {
            App.MainWindow?.Close();
            Environment.Exit(0);
        }

        return Task.CompletedTask;
    }

    private static Task LaunchAsync(ProcessStartInfo startInfo)
    {
        TryLaunch(startInfo);
        return Task.CompletedTask;
    }

    private static bool TryLaunch(ProcessStartInfo startInfo)
    {
        try
        {
            Process.Start(startInfo);
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return false;
        }
    }

    private static bool IsRunningElevated()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}