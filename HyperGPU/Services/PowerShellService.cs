using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace HyperGPU.Services;

internal sealed class PowerShellService : IPowerShellService
{
    private static readonly string[] CandidateExecutables = ["powershell.exe", "pwsh.exe"];

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public Task<string> InvokeScriptAsync(string script, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(script);
        return InvokeTextAsync(script, null, cancellationToken);
    }

    public Task<string> InvokeScriptAsync(string script, IProgress<string>? outputProgress, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(script);
        return InvokeTextAsync(script, outputProgress, cancellationToken);
    }

    public async Task<string> InvokeScriptFileAsync(string scriptPath, CancellationToken cancellationToken)
    {
        return await InvokeScriptFileAsync(scriptPath, null, cancellationToken);
    }

    public async Task<string> InvokeScriptFileAsync(string scriptPath, IProgress<string>? outputProgress, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptPath);

        string escapedPath = scriptPath.Replace("'", "''", StringComparison.Ordinal);
        string? workingDirectory = Path.GetDirectoryName(scriptPath);
        return await InvokeTextAsync($"& '{escapedPath}'", outputProgress, cancellationToken, workingDirectory);
    }

    public async Task<T?> InvokeJsonAsync<T>(string script, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(script);

        string standardOutput = await InvokeTextAsync(script, null, cancellationToken);

        if (string.IsNullOrWhiteSpace(standardOutput))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(standardOutput, SerializerOptions);
    }

    private static async Task<string> InvokeTextAsync(string script, IProgress<string>? outputProgress, CancellationToken cancellationToken, string? workingDirectory = null)
    {
        string wrappedScript = $"$ProgressPreference = 'SilentlyContinue'; $InformationPreference = 'Continue'; $ErrorView = 'NormalView'; {script}";
        string encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(wrappedScript));

        ProcessStartInfo startInfo = new()
        {
            FileName = ResolvePowerShellExecutablePath(),
            Arguments = $"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encodedCommand}",
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        using Process process = new()
        {
            StartInfo = startInfo,
        };

        process.Start();

        StringBuilder standardOutputBuilder = new();
        StringBuilder standardErrorBuilder = new();
        Task standardOutputTask = ReadLinesAsync(process.StandardOutput, standardOutputBuilder, outputProgress, cancellationToken);
        Task standardErrorTask = ReadLinesAsync(process.StandardError, standardErrorBuilder, outputProgress, cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        await Task.WhenAll(standardOutputTask, standardErrorTask);

        string standardOutput = SanitizePowerShellOutput(standardOutputBuilder.ToString());
        string standardError = SanitizePowerShellOutput(standardErrorBuilder.ToString());

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"PowerShell exited with code {process.ExitCode}:{Environment.NewLine}{standardOutput}{Environment.NewLine}{standardError}".Trim());
        }

        return standardOutput.Trim();
    }

    private static async Task ReadLinesAsync(StreamReader reader, StringBuilder builder, IProgress<string>? outputProgress, CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            builder.AppendLine(line);

            if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("#< CLIXML", StringComparison.Ordinal))
            {
                outputProgress?.Report(line);
            }
        }
    }

    private static string SanitizePowerShellOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return string.Empty;
        }

        int clixmlIndex = output.IndexOf("#< CLIXML", StringComparison.Ordinal);
        return clixmlIndex >= 0 ? output[..clixmlIndex].TrimEnd() : output;
    }

    private static string ResolvePowerShellExecutablePath()
    {
        foreach (string executable in CandidateExecutables)
        {
            string? resolvedPath = ResolveExecutablePath(executable);

            if (!string.IsNullOrWhiteSpace(resolvedPath))
            {
                return resolvedPath;
            }
        }

        throw new FileNotFoundException("PowerShell executable was not found. Ensure pwsh.exe or powershell.exe is available on PATH.");
    }

    private static string? ResolveExecutablePath(string executable)
    {
        if (Path.IsPathRooted(executable) && File.Exists(executable))
        {
            return executable;
        }

        string? path = Environment.GetEnvironmentVariable("PATH");

        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (string segment in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string candidate = Path.Combine(segment, executable);

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}