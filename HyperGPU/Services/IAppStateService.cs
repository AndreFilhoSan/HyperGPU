namespace HyperGPU.Services;

public interface IAppStateService
{
    string GetString(string key, string defaultValue = "");

    void SetString(string key, string value);

    bool GetBoolean(string key, bool defaultValue = false);

    void SetBoolean(string key, bool value);
}