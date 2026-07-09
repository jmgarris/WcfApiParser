using System.Diagnostics;

namespace WcfNetTcpClientGenerator.Core;

public sealed class NuGetPackageBuilder
{
    public async Task<GenerationResult> BuildAsync(string projectFilePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(projectFilePath))
        {
            return new GenerationResult
            {
                Success = false,
                Diagnostics =
                [
                    new GenerationDiagnostic(
                        DiagnosticSeverity.Error,
                        $"Generated project file was not found: {projectFilePath}",
                        "PROJECT_FILE_MISSING")
                ]
            };
        }

        var outputDirectory = Path.Combine(Path.GetDirectoryName(projectFilePath)!, "artifacts");
        Directory.CreateDirectory(outputDirectory);

        var buildResult = await RunDotNetAsync(
            Path.GetDirectoryName(projectFilePath)!,
            ["build", projectFilePath, "-c", "Release"],
            cancellationToken).ConfigureAwait(false);

        if (buildResult.ExitCode != 0)
        {
            return new GenerationResult
            {
                Success = false,
                ProjectFilePath = projectFilePath,
                OutputDirectory = Path.GetDirectoryName(projectFilePath),
                Diagnostics =
                [
                    new GenerationDiagnostic(
                        DiagnosticSeverity.Error,
                        $"Generated project build failed. {(string.IsNullOrWhiteSpace(buildResult.StandardError) ? buildResult.StandardOutput : buildResult.StandardError)}",
                        "GENERATED_PROJECT_BUILD_FAILED")
                ]
            };
        }

        var packResult = await RunDotNetAsync(
            Path.GetDirectoryName(projectFilePath)!,
            ["pack", projectFilePath, "-c", "Release", "--no-build", "-o", outputDirectory],
            cancellationToken).ConfigureAwait(false);

        if (packResult.ExitCode != 0)
        {
            return new GenerationResult
            {
                Success = false,
                ProjectFilePath = projectFilePath,
                OutputDirectory = Path.GetDirectoryName(projectFilePath),
                Diagnostics =
                [
                    new GenerationDiagnostic(
                        DiagnosticSeverity.Error,
                        $"NuGet packaging failed. {(string.IsNullOrWhiteSpace(packResult.StandardError) ? packResult.StandardOutput : packResult.StandardError)}",
                        "NUGET_PACK_FAILED")
                ]
            };
        }

        var packagePath = Directory
            .EnumerateFiles(outputDirectory, "*.nupkg", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(static file => !file.EndsWith(".symbols.nupkg", StringComparison.OrdinalIgnoreCase));

        return new GenerationResult
        {
            Success = !string.IsNullOrWhiteSpace(packagePath),
            ProjectFilePath = projectFilePath,
            OutputDirectory = Path.GetDirectoryName(projectFilePath),
            PackagePath = packagePath,
            Diagnostics =
            [
                new GenerationDiagnostic(DiagnosticSeverity.Info, $"Package created at {packagePath}.")
            ]
        };
    }

    private static async Task<ProcessResult> RunDotNetAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory
            }
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();

        var standardOutput = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var standardError = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return new ProcessResult(process.ExitCode, standardOutput, standardError);
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
