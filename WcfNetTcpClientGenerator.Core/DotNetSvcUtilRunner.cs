using System.Diagnostics;
using System.Text;

namespace WcfNetTcpClientGenerator.Core;

public sealed class DotNetSvcUtilRunner
{
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        var result = await RunProcessAsync(
            ["dotnet-svcutil", "--help"],
            ResolveToolWorkingDirectory(),
            cancellationToken).ConfigureAwait(false);

        return result.ExitCode == 0;
    }

    public async Task<DotNetSvcUtilResult> GenerateProxyAsync(
        IReadOnlyList<string> metadataSources,
        string outputDirectory,
        string outputFilePath,
        string clrNamespace,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);

        var arguments = new List<string> { "dotnet-svcutil" };
        arguments.AddRange(metadataSources);
        arguments.AddRange(
        [
            "--outputDir",
            outputDirectory,
            "--outputFile",
            outputFilePath,
            "--namespace",
            $"*,{clrNamespace}",
            "--serializer",
            "Auto",
            "--targetFramework",
            "net10.0",
            "--noLogo",
            "--verbosity",
            "Minimal"
        ]);

        var result = await RunProcessAsync(arguments, ResolveToolWorkingDirectory(), cancellationToken).ConfigureAwait(false);

        return new DotNetSvcUtilResult
        {
            Success = result.ExitCode == 0 && File.Exists(outputFilePath),
            ExitCode = result.ExitCode,
            StandardOutput = result.StandardOutput,
            StandardError = result.StandardError,
            ProxyFilePath = File.Exists(outputFilePath) ? outputFilePath : null
        };
    }

    private static async Task<ProcessExecutionResult> RunProcessAsync(
        IReadOnlyList<string> toolArguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.StartInfo.ArgumentList.Add("tool");
        process.StartInfo.ArgumentList.Add("run");

        foreach (var argument in toolArguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        try
        {
            process.Start();
        }
        catch (Exception exception)
        {
            return new ProcessExecutionResult(-1, string.Empty, exception.Message);
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return new ProcessExecutionResult(
            process.ExitCode,
            await outputTask.ConfigureAwait(false),
            await errorTask.ConfigureAwait(false));
    }

    private static string ResolveToolWorkingDirectory()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "dotnet-tools.json")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    public sealed class DotNetSvcUtilResult
    {
        public bool Success { get; init; }

        public int ExitCode { get; init; }

        public string StandardOutput { get; init; } = string.Empty;

        public string StandardError { get; init; } = string.Empty;

        public string? ProxyFilePath { get; init; }
    }

    private sealed record ProcessExecutionResult(int ExitCode, string StandardOutput, string StandardError);
}
