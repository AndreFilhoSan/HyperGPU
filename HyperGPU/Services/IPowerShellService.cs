namespace HyperGPU.Services;

public interface IPowerShellService
{
    Task<string> InvokeScriptAsync(string script, CancellationToken cancellationToken);

    Task<string> InvokeScriptAsync(string script, IProgress<string>? outputProgress, CancellationToken cancellationToken);

    Task<string> InvokeScriptFileAsync(string scriptPath, CancellationToken cancellationToken);

    Task<string> InvokeScriptFileAsync(string scriptPath, IProgress<string>? outputProgress, CancellationToken cancellationToken);

    Task<T?> InvokeJsonAsync<T>(string script, CancellationToken cancellationToken);
}