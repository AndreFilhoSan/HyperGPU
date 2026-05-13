using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using HyperGPU.Services;

namespace HyperGPU;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    public static IServiceProvider Services { get; private set; } = null!;

    private Window? _window;

    public App()
    {
        InitializeComponent();

        Services = ConfigureServices();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        if (!IsRunningElevated() && TryRelaunchElevated())
        {
            Environment.Exit(0);
            return;
        }

        _window = Services.GetRequiredService<MainWindow>();
        MainWindow = _window;
        _window.Activate();
    }

    private static ServiceProvider ConfigureServices()
    {
        ServiceCollection services = [];

        services.AddSingleton<IAppResourceService, AppResourceService>();
        services.AddSingleton<IAppStateService, AppStateService>();
        services.AddSingleton<IFilePickerService, FilePickerService>();
        services.AddSingleton<IHostQuickActionService, HostQuickActionService>();
        services.AddSingleton<IPowerShellService, PowerShellService>();
        services.AddSingleton<HostReadinessEvaluator>();
        services.AddSingleton<IHostInspectionService, HostInspectionService>();
        services.AddSingleton<VmExecutionPlanBuilder>();
        services.AddSingleton<IVmProvisioningService, VmProvisioningService>();
        services.AddTransient<HyperGPU.ViewModels.MainViewModel>();
        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider();
    }

    private static bool TryRelaunchElevated()
    {
        string executablePath = Environment.ProcessPath ?? throw new InvalidOperationException("The current application path is unavailable.");

        try
        {
            Process.Start(
                new ProcessStartInfo(executablePath)
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = AppContext.BaseDirectory,
                });

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
