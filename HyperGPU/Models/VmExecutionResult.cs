namespace HyperGPU.Models;

public sealed class VmExecutionResult
{
    public VmExecutionResult(bool isSuccess, string summary, string output, string workingDirectory, IReadOnlyList<VmExecutionStageResult> stages)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentException.ThrowIfNullOrWhiteSpace(output);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(stages);

        IsSuccess = isSuccess;
        Summary = summary;
        Output = output;
        WorkingDirectory = workingDirectory;
        Stages = stages;
    }

    public bool IsSuccess { get; }

    public string Output { get; }

    public IReadOnlyList<VmExecutionStageResult> Stages { get; }

    public string Summary { get; }

    public string WorkingDirectory { get; }
}