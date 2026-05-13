namespace HyperGPU.Models;

public sealed class VmPlanIssue
{
    public VmPlanIssue(VmPlanIssueSeverity severity, string severityLabel, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(severityLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Severity = severity;
        SeverityLabel = severityLabel;
        Message = message;
    }

    public string Message { get; }

    public VmPlanIssueSeverity Severity { get; }

    public string SeverityLabel { get; }
}