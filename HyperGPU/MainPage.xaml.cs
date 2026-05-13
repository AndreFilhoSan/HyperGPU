using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using HyperGPU.ViewModels;

namespace HyperGPU;

public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel { get; }

    public MainPage()
    {
        InitializeComponent();

        ViewModel = App.Services.GetRequiredService<MainViewModel>();
        PasswordTextBox.Password = ViewModel.Password;
        ApplySelectedTheme();
        ThemeComboBox.SelectedIndex = string.Equals(ViewModel.SelectedTheme, "Light", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        Loaded += OnLoaded;
    }

    private void ThemeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string theme)
        {
            ViewModel.SelectedTheme = theme;
            ApplySelectedTheme();
        }
    }

    private void ApplySelectedTheme()
    {
        RequestedTheme = string.Equals(ViewModel.SelectedTheme, "Light", StringComparison.OrdinalIgnoreCase)
            ? ElementTheme.Light
            : ElementTheme.Dark;
    }

    private void PasswordTextBox_OnPasswordChanged(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.Password = PasswordTextBox.Password;
    }

    private async void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        if (!ViewModel.HasSnapshot)
        {
            await ViewModel.RefreshAsync();
        }
    }

    private void OverviewNextButton_OnClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        RootTabView.SelectedItem = ConfigurationTabItem;
    }

    private void BuildVmPlanButton_OnClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.BuildVmPlanCommand.Execute(null);

        if (ViewModel.CanContinueFromConfiguration)
        {
            RootTabView.SelectedItem = ExecutionTabItem;
        }
    }

    private void ExecutionNextButton_OnClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        RootTabView.SelectedItem = DownloadsTabItem;
    }
}
