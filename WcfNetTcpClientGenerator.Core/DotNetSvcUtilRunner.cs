using System.Diagnostics;
using System.Text.Json;

namespace WcfNetTcpClientGenerator.Core;

public sealed class DotNetSvcUtilRunner : IWcfProxyToolRunner
{
    public string ToolName => "dotnet-svcutil";
    private const string ToolCommandName = "dotnet-svcutil";
    private const string ToolExecutableName = "dotnet-svcutil.exe";
    private const string ImprovedNotFoundMessage = "dotnet-svcutil could not be located from the generator process. It may be installed globally but not visible to the WinUI app process. Install it globally with `dotnet tool install --global dotnet-svcutil`, add `%USERPROFILE%\\.dotnet\\tools` to PATH, select the tool path manually, or add a local tool manifest containing dotnet-svcutil.";

    private readonly IProcessRunner _processRunner;
    private readonly IFileSystem _fileSystem;
    private readonly Func<string> _currentDirectoryProvider;
    private readonly string _applicationBaseDirectory;
    private readonly string _userProfileDirectory;

    public DotNetSvcUtilRunner()
        : this(
            new SystemProcessRunner(),
            new SystemFileSystem(),
            static () => Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
    {
    }

    internal DotNetSvcUtilRunner(
        IProcessRunner processRunner,
        IFileSystem fileSystem,
        Func<string> currentDirectoryProvider,
        string applicationBaseDirectory,
        string userProfileDirectory)
    {
        _processRunner = processRunner;
        _fileSystem = fileSystem;
        _currentDirectoryProvider = currentDirectoryProvider;
        _applicationBaseDirectory = applicationBaseDirectory;
        _userProfileDirectory = userProfileDirectory;
    }

    public Task<DotNetSvcUtilPreflightResult> CheckAvailabilityAsync(
        string? configuredToolPath,
        CancellationToken cancellationToken)
        => CheckAvailabilityAsync(configuredToolPath, workingDirectory: null, cancellationToken);

    public async Task<DotNetSvcUtilPreflightResult> CheckAvailabilityAsync(
        string? configuredToolPath,
        string? workingDirectory,
        CancellationToken cancellationToken)
    {
        var resolvedWorkingDirectory = ResolveWorkingDirectory(workingDirectory);
        var failedAttempts = new List<string>();

        if (!string.IsNullOrWhiteSpace(configuredToolPath))
        {
            var explicitPath = Path.GetFullPath(configuredToolPath.Trim());
            if (_fileSystem.FileExists(explicitPath))
            {
                var helpInvocation = new ProcessInvocation(explicitPath, ["--help"], resolvedWorkingDirectory);
                var helpResult = await _processRunner.RunAsync(helpInvocation, cancellationToken).ConfigureAwait(false);

                if (helpResult.ExitCode == 0)
                {
                    return CreateSuccessResult(
                        DotNetSvcUtilExecutionMode.ExplicitPath,
                        explicitPath,
                        helpResult,
                        resolvedWorkingDirectory,
                        $"Using dotnet-svcutil from the configured path: {explicitPath}");
                }

                failedAttempts.Add($"The configured dotnet-svcutil path failed: {explicitPath}. Exit code: {helpResult.ExitCode}. {SelectOutput(helpResult)}".Trim());
            }
            else
            {
                failedAttempts.Add($"The configured dotnet-svcutil path does not exist: {explicitPath}");
            }
        }

        var globalCommandInvocation = new ProcessInvocation(ToolCommandName, ["--help"], resolvedWorkingDirectory);
        var globalCommandResult = await _processRunner.RunAsync(globalCommandInvocation, cancellationToken).ConfigureAwait(false);
        if (globalCommandResult.ExitCode == 0)
        {
            return CreateSuccessResult(
                DotNetSvcUtilExecutionMode.GlobalCommand,
                ToolCommandName,
                globalCommandResult,
                resolvedWorkingDirectory,
                "Using the global dotnet-svcutil command.");
        }

        failedAttempts.Add($"The global dotnet-svcutil command was not usable. Exit code: {globalCommandResult.ExitCode}. {SelectOutput(globalCommandResult)}".Trim());

        var windowsGlobalPath = Path.Combine(_userProfileDirectory, ".dotnet", "tools", ToolExecutableName);
        if (_fileSystem.FileExists(windowsGlobalPath))
        {
            var globalPathInvocation = new ProcessInvocation(windowsGlobalPath, ["--help"], resolvedWorkingDirectory);
            var globalPathResult = await _processRunner.RunAsync(globalPathInvocation, cancellationToken).ConfigureAwait(false);

            if (globalPathResult.ExitCode == 0)
            {
                return CreateSuccessResult(
                    DotNetSvcUtilExecutionMode.GlobalWindowsPath,
                    windowsGlobalPath,
                    globalPathResult,
                    resolvedWorkingDirectory,
                    $"Using dotnet-svcutil from the Windows global tools path: {windowsGlobalPath}");
            }

            failedAttempts.Add($"The Windows global tool path failed: {windowsGlobalPath}. Exit code: {globalPathResult.ExitCode}. {SelectOutput(globalPathResult)}".Trim());
        }

        var localManifest = FindLocalToolManifest(resolvedWorkingDirectory);
        if (localManifest is not null)
        {
            if (localManifest.ContainsSvcUtil)
            {
                var restoreInvocation = new ProcessInvocation("dotnet", ["tool", "restore"], localManifest.WorkingDirectory);
                var restoreResult = await _processRunner.RunAsync(restoreInvocation, cancellationToken).ConfigureAwait(false);

                if (restoreResult.ExitCode != 0)
                {
                    return new DotNetSvcUtilPreflightResult
                    {
                        ToolFound = false,
                        ToolExecutionMode = DotNetSvcUtilExecutionMode.NotFound,
                        ToolPath = localManifest.ManifestPath,
                        WorkingDirectory = localManifest.WorkingDirectory,
                        VersionOutput = SelectOutput(restoreResult),
                        DiagnosticMessage = $"A local tool manifest was found at {localManifest.ManifestPath} and it contains dotnet-svcutil, but `dotnet tool restore` failed. {SelectOutput(restoreResult)}".Trim()
                    };
                }

                var localHelpInvocation = new ProcessInvocation("dotnet", ["tool", "run", ToolCommandName, "--", "--help"], localManifest.WorkingDirectory);
                var localHelpResult = await _processRunner.RunAsync(localHelpInvocation, cancellationToken).ConfigureAwait(false);

                if (localHelpResult.ExitCode == 0)
                {
                    return CreateSuccessResult(
                        DotNetSvcUtilExecutionMode.LocalTool,
                        localManifest.ManifestPath,
                        localHelpResult,
                        localManifest.WorkingDirectory,
                        $"Using dotnet-svcutil from the local tool manifest at {localManifest.ManifestPath}");
                }

                failedAttempts.Add($"The local tool manifest at {localManifest.ManifestPath} was restored, but `dotnet tool run dotnet-svcutil` failed. Exit code: {localHelpResult.ExitCode}. {SelectOutput(localHelpResult)}".Trim());
            }
            else
            {
                failedAttempts.Add($"A local tool manifest was found at {localManifest.ManifestPath}, but it does not contain dotnet-svcutil.");
            }
        }

        var diagnosticMessage = string.Join(Environment.NewLine, failedAttempts.Where(static value => !string.IsNullOrWhiteSpace(value)));
        if (!string.IsNullOrWhiteSpace(diagnosticMessage))
        {
            diagnosticMessage = $"{ImprovedNotFoundMessage}{Environment.NewLine}{Environment.NewLine}{diagnosticMessage}";
        }
        else
        {
            diagnosticMessage = ImprovedNotFoundMessage;
        }

        return new DotNetSvcUtilPreflightResult
        {
            ToolFound = false,
            ToolExecutionMode = DotNetSvcUtilExecutionMode.NotFound,
            WorkingDirectory = resolvedWorkingDirectory,
            DiagnosticMessage = diagnosticMessage
        };
    }

    public async Task<DotNetSvcUtilResult> GenerateProxyAsync(
        IReadOnlyList<string> metadataSources,
        string outputDirectory,
        string outputFilePath,
        string clrNamespace,
        string? configuredToolPath,
        string targetFramework,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);

        var preflightResult = await CheckAvailabilityAsync(configuredToolPath, outputDirectory, cancellationToken).ConfigureAwait(false);
        if (!preflightResult.ToolFound)
        {
            return new DotNetSvcUtilResult
            {
                Success = false,
                ExitCode = -1,
                StandardError = preflightResult.DiagnosticMessage,
                WorkingDirectory = preflightResult.WorkingDirectory,
                PreflightResult = preflightResult
            };
        }

        var arguments = BuildSvcUtilArguments(metadataSources, outputDirectory, outputFilePath, clrNamespace, targetFramework);
        var invocation = BuildInvocation(preflightResult, arguments);
        var executionResult = await _processRunner.RunAsync(invocation, cancellationToken).ConfigureAwait(false);

        return new DotNetSvcUtilResult
        {
            Success = executionResult.ExitCode == 0 && _fileSystem.FileExists(outputFilePath),
            ExitCode = executionResult.ExitCode,
            StandardOutput = executionResult.StandardOutput,
            StandardError = executionResult.StandardError,
            ProxyFilePath = _fileSystem.FileExists(outputFilePath) ? outputFilePath : null,
            ExecutedCommand = invocation.DisplayCommand,
            WorkingDirectory = invocation.WorkingDirectory,
            PreflightResult = preflightResult
        };
    }

    public Task<DotNetSvcUtilResult> GenerateProxyAsync(IReadOnlyList<string> metadataSources, string outputDirectory, string outputFilePath, string clrNamespace, string? configuredToolPath, CancellationToken cancellationToken)
        => GenerateProxyAsync(metadataSources, outputDirectory, outputFilePath, clrNamespace, configuredToolPath, "net10.0", cancellationToken);

    internal static IReadOnlyList<string> BuildSvcUtilArguments(
        IReadOnlyList<string> metadataSources,
        string outputDirectory,
        string outputFilePath,
        string clrNamespace,
        string targetFramework = "net10.0")
    {
        var outputFileName = Path.GetFileName(outputFilePath);
        return
        [
            .. metadataSources,
            "-n",
            $"*,{clrNamespace}",
            "-d",
            outputDirectory,
            "-o",
            outputFileName,
            "--serializer",
            "Auto",
            "--targetFramework",
            targetFramework,
            "--noLogo",
            "--verbosity",
            "Minimal"
        ];
    }

    private static ProcessInvocation BuildInvocation(DotNetSvcUtilPreflightResult preflightResult, IReadOnlyList<string> svcUtilArguments)
        => preflightResult.ToolExecutionMode switch
        {
            DotNetSvcUtilExecutionMode.ExplicitPath or DotNetSvcUtilExecutionMode.GlobalWindowsPath
                => new ProcessInvocation(preflightResult.ToolPath!, svcUtilArguments, preflightResult.WorkingDirectory),
            DotNetSvcUtilExecutionMode.GlobalCommand
                => new ProcessInvocation(ToolCommandName, svcUtilArguments, preflightResult.WorkingDirectory),
            DotNetSvcUtilExecutionMode.LocalTool
                => new ProcessInvocation("dotnet", ["tool", "run", ToolCommandName, "--", .. svcUtilArguments], preflightResult.WorkingDirectory),
            _ => throw new InvalidOperationException("dotnet-svcutil cannot be executed because no tool was found.")
        };

    private static DotNetSvcUtilPreflightResult CreateSuccessResult(
        DotNetSvcUtilExecutionMode mode,
        string toolPath,
        ProcessExecutionResult executionResult,
        string workingDirectory,
        string diagnosticMessage)
        => new()
        {
            ToolFound = true,
            ToolPath = toolPath,
            ToolExecutionMode = mode,
            WorkingDirectory = workingDirectory,
            VersionOutput = SelectOutput(executionResult),
            DiagnosticMessage = diagnosticMessage
        };

    private static string SelectOutput(ProcessExecutionResult executionResult)
        => string.IsNullOrWhiteSpace(executionResult.StandardOutput)
            ? executionResult.StandardError
            : executionResult.StandardOutput;

    private LocalToolManifestInfo? FindLocalToolManifest(string workingDirectory)
    {
        LocalToolManifestInfo? manifestWithoutTool = null;
        var visitedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in EnumerateSearchRoots(workingDirectory))
        {
            foreach (var currentDirectory in EnumerateCurrentAndParents(directory))
            {
                if (!visitedDirectories.Add(currentDirectory))
                {
                    continue;
                }

                foreach (var manifestPath in EnumerateManifestPaths(currentDirectory))
                {
                    if (!_fileSystem.FileExists(manifestPath))
                    {
                        continue;
                    }

                    var manifest = ReadManifest(manifestPath);
                    if (manifest is null)
                    {
                        continue;
                    }

                    if (manifest.ContainsSvcUtil)
                    {
                        return manifest;
                    }

                    manifestWithoutTool ??= manifest;
                }
            }
        }

        return manifestWithoutTool;
    }

    private IEnumerable<string> EnumerateSearchRoots(string workingDirectory)
    {
        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            yield return workingDirectory;
        }

        var currentDirectory = _currentDirectoryProvider();
        if (!string.IsNullOrWhiteSpace(currentDirectory))
        {
            yield return currentDirectory;
        }

        if (!string.IsNullOrWhiteSpace(_applicationBaseDirectory))
        {
            yield return _applicationBaseDirectory;
        }
    }

    private static IEnumerable<string> EnumerateCurrentAndParents(string directory)
    {
        var current = new DirectoryInfo(directory);
        while (current is not null)
        {
            yield return current.FullName;
            current = current.Parent;
        }
    }

    private static IEnumerable<string> EnumerateManifestPaths(string directory)
    {
        yield return Path.Combine(directory, ".config", "dotnet-tools.json");
        yield return Path.Combine(directory, "dotnet-tools.json");
    }

    private LocalToolManifestInfo? ReadManifest(string manifestPath)
    {
        try
        {
            using var document = JsonDocument.Parse(_fileSystem.ReadAllText(manifestPath));
            var containsTool = document.RootElement.TryGetProperty("tools", out var toolsElement)
                && toolsElement.ValueKind == JsonValueKind.Object
                && toolsElement.TryGetProperty(ToolCommandName, out _);

            return new LocalToolManifestInfo(manifestPath, Path.GetDirectoryName(manifestPath)!, containsTool);
        }
        catch
        {
            return null;
        }
    }

    private string ResolveWorkingDirectory(string? preferredWorkingDirectory)
    {
        if (!string.IsNullOrWhiteSpace(preferredWorkingDirectory))
        {
            return Path.GetFullPath(preferredWorkingDirectory);
        }

        var currentDirectory = _currentDirectoryProvider();
        if (!string.IsNullOrWhiteSpace(currentDirectory))
        {
            return Path.GetFullPath(currentDirectory);
        }

        return Path.GetFullPath(_applicationBaseDirectory);
    }

    public sealed class DotNetSvcUtilResult
    {
        public bool Success { get; init; }

        public int ExitCode { get; init; }

        public string StandardOutput { get; init; } = string.Empty;

        public string StandardError { get; init; } = string.Empty;

        public string? ProxyFilePath { get; init; }

        public string ExecutedCommand { get; init; } = string.Empty;

        public string WorkingDirectory { get; init; } = string.Empty;

        public DotNetSvcUtilPreflightResult? PreflightResult { get; init; }
    }

    internal interface IProcessRunner
    {
        Task<ProcessExecutionResult> RunAsync(ProcessInvocation invocation, CancellationToken cancellationToken);
    }

    internal interface IFileSystem
    {
        bool FileExists(string path);

        string ReadAllText(string path);
    }

    internal sealed record ProcessInvocation(string FileName, IReadOnlyList<string> Arguments, string WorkingDirectory)
    {
        public string DisplayCommand => FormatCommand(FileName, Arguments);

        private static string FormatCommand(string fileName, IReadOnlyList<string> arguments)
            => string.Join(" ", [Quote(fileName), .. arguments.Select(Quote)]);

        private static string Quote(string value)
            => value.Any(static character => char.IsWhiteSpace(character) || character is '"' or '&')
                ? $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\""
                : value;
    }

    internal sealed record ProcessExecutionResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed record LocalToolManifestInfo(string ManifestPath, string WorkingDirectory, bool ContainsSvcUtil);

    private sealed class SystemProcessRunner : IProcessRunner
    {
        public async Task<ProcessExecutionResult> RunAsync(ProcessInvocation invocation, CancellationToken cancellationToken)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = invocation.FileName,
                    WorkingDirectory = invocation.WorkingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            foreach (var argument in invocation.Arguments)
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

            var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            return new ProcessExecutionResult(
                process.ExitCode,
                await standardOutputTask.ConfigureAwait(false),
                await standardErrorTask.ConfigureAwait(false));
        }
    }

    private sealed class SystemFileSystem : IFileSystem
    {
        public bool FileExists(string path)
            => File.Exists(path);

        public string ReadAllText(string path)
            => File.ReadAllText(path);
    }
}
