namespace HyperGPU.Models;

public sealed class VmExecutionPlan
{
    public VmExecutionPlan(
        IReadOnlyList<VmPlanIssue> issues,
        string summary,
        string createVmParametersPreview,
        string updateDriverCommandPreview)
    {
        ArgumentNullException.ThrowIfNull(issues);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentException.ThrowIfNullOrWhiteSpace(createVmParametersPreview);
        ArgumentException.ThrowIfNullOrWhiteSpace(updateDriverCommandPreview);

        Issues = issues;
        Summary = summary;
        CreateVmParametersPreview = createVmParametersPreview;
        UpdateDriverCommandPreview = updateDriverCommandPreview;
    }

    public bool HasBlockingIssues => Issues.Any(issue => issue.Severity == VmPlanIssueSeverity.Error);

    public string CreateVmParametersPreview { get; }

    public IReadOnlyList<VmPlanIssue> Issues { get; }

    public string Summary { get; }

    public string UpdateDriverCommandPreview { get; }
}