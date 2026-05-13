namespace HyperGPU.Services;

public interface IFilePickerService
{
    Task<string?> PickIsoFileAsync(CancellationToken cancellationToken);

    Task<string?> PickVhdFolderAsync(CancellationToken cancellationToken);
}