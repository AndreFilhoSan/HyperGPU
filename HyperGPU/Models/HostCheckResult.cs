namespace HyperGPU.Models;

public sealed class HostCheckResult
{
    public HostCheckResult(string title, string statusLabel, string description, string resolution, HostCheckState state, HostCheckActionKind quickAction = HostCheckActionKind.None, string quickActionLabel = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(statusLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        Title = title;
        StatusLabel = statusLabel;
        Description = description;
        Resolution = resolution;
        State = state;
        QuickAction = quickAction;
        QuickActionLabel = quickActionLabel;
    }

    public string Title { get; }

    public string StatusLabel { get; }

    public string Description { get; }

    public string Resolution { get; }

    public HostCheckState State { get; }

    public HostCheckActionKind QuickAction { get; }

    public string QuickActionLabel { get; }

    public bool HasResolution => !string.IsNullOrWhiteSpace(Resolution);

    public bool HasQuickAction => QuickAction != HostCheckActionKind.None && !string.IsNullOrWhiteSpace(QuickActionLabel);

    public bool IsReady => State == HostCheckState.Ready;

    public bool IsWarning => State == HostCheckState.Warning;

    public bool IsError => State == HostCheckState.Error;

    public string StatusGlyph => State switch
    {
        HostCheckState.Ready => "\uE73E",
        HostCheckState.Warning => "\uE7BA",
        HostCheckState.Error => "\uEA39",
        _ => "\uE9CE",
    };
}

public enum HostCheckActionKind
{
    None,
    RefreshChecks,
    OpenWindowsUpdate,
    RelaunchElevated,
    EnableHyperV,
    StartVmManagementService,
    OpenRestartSettings,
    OpenDeviceManager,
}