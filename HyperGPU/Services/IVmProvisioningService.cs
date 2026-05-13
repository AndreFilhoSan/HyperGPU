using HyperGPU.Models;

namespace HyperGPU.Services;

public interface IVmProvisioningService
{
    Task<VmExecutionResult> ExecuteCreateVmAsync(VmConfigurationDraft draft, IProgress<VmExecutionStageResult>? progress, IProgress<string>? outputProgress, CancellationToken cancellationToken);

    Task<VmExecutionResult> ExecuteUpdateExistingVmAsync(VmConfigurationDraft draft, IProgress<VmExecutionStageResult>? progress, IProgress<string>? outputProgress, CancellationToken cancellationToken);
}