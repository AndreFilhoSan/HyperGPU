using Windows.Storage;

namespace HyperGPU.Services;

public sealed class AppStateService : IAppStateService
{
    private readonly ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;

    public string GetString(string key, string defaultValue = "")
    {
        return _localSettings.Values.TryGetValue(key, out object? value) && value is string stringValue
            ? stringValue
            : defaultValue;
    }

    public void SetString(string key, string value)
    {
        _localSettings.Values[key] = value;
    }

    public bool GetBoolean(string key, bool defaultValue = false)
    {
        return _localSettings.Values.TryGetValue(key, out object? value) && value is bool boolValue
            ? boolValue
            : defaultValue;
    }

    public void SetBoolean(string key, bool value)
    {
        _localSettings.Values[key] = value;
    }
}