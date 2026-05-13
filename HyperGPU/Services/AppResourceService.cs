using Microsoft.Windows.ApplicationModel.Resources;

namespace HyperGPU.Services;

internal sealed class AppResourceService : IAppResourceService
{
    private readonly ResourceLoader _resourceLoader = new();

    public string GetString(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        string? value = _resourceLoader.GetString(key);

        return string.IsNullOrWhiteSpace(value) ? key : value;
    }
}