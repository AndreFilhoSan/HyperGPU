using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using HyperGPU.Services;

namespace HyperGPU;

public sealed partial class MainWindow : Window
{
    public MainWindow(IAppResourceService resources)
    {
        ArgumentNullException.ThrowIfNull(resources);

        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        string title = resources.GetString("AppTitle");

        Title = title;
        AppTitleBar.Title = title;
        AppWindow.SetIcon("Assets/AppIcon.ico");

        RootFrame.Navigate(typeof(MainPage));
    }
}
