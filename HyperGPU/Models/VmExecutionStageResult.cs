namespace HyperGPU.Models;

public sealed class VmExecutionStageResult
{
    public VmExecutionStageResult(string title, VmExecutionStageState state, string statusLabel, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(statusLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Title = title;
        State = state;
        StatusLabel = statusLabel;
        Message = message;
    }

    public string Message { get; }

    public VmExecutionStageState State { get; }

    public string StatusLabel { get; }

    public string Title { get; }
}