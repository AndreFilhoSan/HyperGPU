using System.Globalization;
using System.Text;
using HyperGPU.Models;

namespace HyperGPU.Services;

public sealed class VmExecutionPlanBuilder
{
    private readonly IAppResourceService _resources;

    public VmExecutionPlanBuilder(IAppResourceService resources)
    {
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
    }

    public VmExecutionPlan Build(VmConfigurationDraft draft, HostInspectionSnapshot? snapshot)
    {
        ArgumentNullException.ThrowIfNull(draft);

        List<VmPlanIssue> issues = [];
        AddDraftValidationIssues(draft, snapshot, issues);

        string summary = BuildSummary(issues);

        return new VmExecutionPlan(
            issues,
            summary,
            BuildCreateVmPreview(draft),
            BuildDriverUpdatePreview(draft));
    }

    private void AddDraftValidationIssues(VmConfigurationDraft draft, HostInspectionSnapshot? snapshot, List<VmPlanIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(draft.VmName))
        {
            issues.Add(CreateIssue(VmPlanIssueSeverity.Error, "VmPlanIssueVmNameRequired"));
        }
        else if (!IsAlphaNumeric(draft.VmName) || draft.VmName.Length > 15)
        {
            issues.Add(CreateIssue(VmPlanIssueSeverity.Error, "VmPlanIssueVmNameInvalid"));
        }

        if (string.IsNullOrWhiteSpace(draft.Username))
        {
            issues.Add(CreateIssue(VmPlanIssueSeverity.Error, "VmPlanIssueUsernameRequired"));
        }
        else if (!IsAlphaNumeric(draft.Username))
        {
            issues.Add(CreateIssue(VmPlanIssueSeverity.Error, "VmPlanIssueUsernameInvalid"));
        }
        else if (string.Equals(draft.Username, draft.VmName, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(CreateIssue(VmPlanIssueSeverity.Error, "VmPlanIssueUsernameMatchesVm"));
        }

        if (string.IsNullOrWhiteSpace(draft.Password))
        {
            issues.Add(CreateIssue(VmPlanIssueSeverity.Error, "VmPlanIssuePasswordRequired"));
        }

        if (string.IsNullOrWhiteSpace(draft.SourcePath))
        {
            issues.Add(CreateIssue(VmPlanIssueSeverity.Error, "VmPlanIssueIsoRequired"));
        }
        else if (!File.Exists(draft.SourcePath))
        {
            issues.Add(CreateIssue(VmPlanIssueSeverity.Error, "VmPlanIssueIsoMissing"));
        }

        if (string.IsNullOrWhiteSpace(draft.VhdPath))
        {
            issues.Add(CreateIssue(VmPlanIssueSeverity.Error, "VmPlanIssueVhdPathRequired"));
        }
        else if (!Directory.Exists(draft.VhdPath))
        {
            issues.Add(CreateIssue(VmPlanIssueSeverity.Information, $"The VHD folder does not exist and will be created automatically: {draft.VhdPath}"));
        }

        ValidatePositiveInt(draft.CpuCoresText, 1, 64, "VmPlanIssueCpuRequired", "VmPlanIssueCpuInvalid", issues);
        ValidatePositiveInt(draft.MemoryGbText, 1, 512, "VmPlanIssueMemoryRequired", "VmPlanIssueMemoryInvalid", issues);
        ValidatePositiveInt(draft.DiskSizeGbText, 20, 4096, "VmPlanIssueDiskRequired", "VmPlanIssueDiskInvalid", issues);
        ValidatePositiveInt(draft.GpuSharePercentageText, 1, 100, "VmPlanIssueGpuShareRequired", "VmPlanIssueGpuShareInvalid", issues);

        if (string.IsNullOrWhiteSpace(draft.NetworkSwitchName))
        {
            issues.Add(CreateIssue(VmPlanIssueSeverity.Error, "VmPlanIssueSwitchRequired"));
        }

        if (string.IsNullOrWhiteSpace(draft.GpuName))
        {
            issues.Add(CreateIssue(VmPlanIssueSeverity.Error, "VmPlanIssueGpuRequired"));
        }
        else if (!draft.SupportsNamedGpuSelection && !string.Equals(draft.GpuName, "AUTO", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(CreateIssue(VmPlanIssueSeverity.Error, "VmPlanIssueWindows10GpuSelection"));
        }

        if (snapshot is null)
        {
            issues.Add(CreateIssue(VmPlanIssueSeverity.Warning, "VmPlanIssueNoInspection"));
        }
        else
        {
            int blockingChecks = snapshot.Checks.Count(check => check.State == HostCheckState.Error);
            int warningChecks = snapshot.Checks.Count(check => check.State == HostCheckState.Warning);

            if (blockingChecks > 0)
            {
                issues.Add(CreateIssue(VmPlanIssueSeverity.Warning, string.Format(CultureInfo.CurrentCulture, _resources.GetString("VmPlanIssueHostBlockingFormat"), blockingChecks)));
            }

            if (warningChecks > 0)
            {
                issues.Add(CreateIssue(VmPlanIssueSeverity.Information, string.Format(CultureInfo.CurrentCulture, _resources.GetString("VmPlanIssueHostWarningsFormat"), warningChecks)));
            }
        }
    }

    private string BuildCreateVmPreview(VmConfigurationDraft draft)
    {
        StringBuilder builder = new();

        builder.AppendLine("$params = @{");
        builder.AppendLine($"    VMName = {Quote(draft.VmName)}");
        builder.AppendLine($"    SourcePath = {Quote(draft.SourcePath)}");
        builder.AppendLine("    Edition = 6");
        builder.AppendLine("    VhdFormat = 'VHDX'");
        builder.AppendLine("    DiskLayout = 'UEFI'");
        builder.AppendLine($"    SizeBytes = {draft.DiskSizeGbText}GB");
        builder.AppendLine($"    MemoryAmount = {draft.MemoryGbText}GB");
        builder.AppendLine($"    CPUCores = {draft.CpuCoresText}");
        builder.AppendLine($"    NetworkSwitch = {Quote(draft.NetworkSwitchName)}");
        builder.AppendLine($"    VHDPath = {Quote(draft.VhdPath)}");
        builder.AppendLine("    UnattendPath = 'autounattend.xml'");
        builder.AppendLine($"    GPUName = {Quote(draft.GpuName)}");
        builder.AppendLine($"    GPUResourceAllocationPercentage = {draft.GpuSharePercentageText}");
        builder.AppendLine("    Team_ID = ''");
        builder.AppendLine("    Key = ''");
        builder.AppendLine($"    Username = {Quote(draft.Username)}");
        builder.AppendLine($"    Password = {Quote(draft.Password)}");
        builder.AppendLine($"    Autologon = {Quote(draft.IsAutoLogonEnabled ? "true" : "false")}");
        builder.Append('}');

        return builder.ToString();
    }

    private string BuildDriverUpdatePreview(VmConfigurationDraft draft)
    {
        return string.Format(
            CultureInfo.CurrentCulture,
            _resources.GetString("DriverUpdateCommandFormat"),
            draft.VmName,
            draft.GpuName);
    }

    private string BuildSummary(IReadOnlyList<VmPlanIssue> issues)
    {
        int errors = issues.Count(issue => issue.Severity == VmPlanIssueSeverity.Error);
        int warnings = issues.Count(issue => issue.Severity == VmPlanIssueSeverity.Warning);

        return errors == 0
            ? string.Format(CultureInfo.CurrentCulture, _resources.GetString("VmPlanSummaryReadyFormat"), warnings)
            : string.Format(CultureInfo.CurrentCulture, _resources.GetString("VmPlanSummaryBlockedFormat"), errors, warnings);
    }

    private VmPlanIssue CreateIssue(VmPlanIssueSeverity severity, string resourceKeyOrText)
    {
        string message = resourceKeyOrText.Contains(' ')
            ? resourceKeyOrText
            : _resources.GetString(resourceKeyOrText);

        string severityLabel = severity switch
        {
            VmPlanIssueSeverity.Error => _resources.GetString("VmPlanIssueSeverityError"),
            VmPlanIssueSeverity.Warning => _resources.GetString("VmPlanIssueSeverityWarning"),
            _ => _resources.GetString("VmPlanIssueSeverityInformation"),
        };

        return new VmPlanIssue(severity, severityLabel, message);
    }

    private static bool IsAlphaNumeric(string value)
    {
        return value.All(char.IsLetterOrDigit);
    }

    private static string Quote(string value)
    {
        return $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
    }

    private void ValidatePositiveInt(string value, int minimum, int maximum, string emptyResourceKey, string invalidResourceKey, List<VmPlanIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add(CreateIssue(VmPlanIssueSeverity.Error, emptyResourceKey));
            return;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedValue) || parsedValue < minimum || parsedValue > maximum)
        {
            issues.Add(CreateIssue(VmPlanIssueSeverity.Error, invalidResourceKey));
        }
    }
}