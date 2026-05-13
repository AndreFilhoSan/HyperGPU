using HyperGPU.Models;

namespace HyperGPU.Services;

public interface IHostInspectionService
{
    Task<HostInspectionSnapshot> InspectAsync(CancellationToken cancellationToken);
}