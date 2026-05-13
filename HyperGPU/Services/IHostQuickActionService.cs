using HyperGPU.Models;

namespace HyperGPU.Services;

public interface IHostQuickActionService
{
    Task ExecuteAsync(HostCheckActionKind action, CancellationToken cancellationToken);
}