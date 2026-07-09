using WcfNetTcpClientGenerator.Core;

namespace WcfNetTcpClientGenerator.Tests;

[TestFixture]
public sealed class DotNetSvcUtilRunnerTests
{
    [Test]
    public async Task DetectsGlobalDotNetSvcUtilCommand()
    {
        var processRunner = new FakeProcessRunner(invocation =>
        {
            return invocation.FileName == "dotnet-svcutil"
                ? new DotNetSvcUtilRunner.ProcessExecutionResult(0, "help output", string.Empty)
                : new DotNetSvcUtilRunner.ProcessExecutionResult(-1, string.Empty, "unexpected");
        });

        var runner = CreateRunner(processRunner, new FakeFileSystem());
        var result = await runner.CheckAvailabilityAsync(configuredToolPath: null, @"C:\repo", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ToolFound, Is.True);
            Assert.That(result.ToolExecutionMode, Is.EqualTo(DotNetSvcUtilExecutionMode.GlobalCommand));
            Assert.That(result.ToolPath, Is.EqualTo("dotnet-svcutil"));
            Assert.That(result.VersionOutput, Does.Contain("help output"));
        });
    }

    [Test]
    public async Task DetectsWindowsGlobalToolPath()
    {
        const string userProfile = @"C:\Users\Tester";
        var windowsGlobalToolPath = Path.Combine(userProfile, ".dotnet", "tools", "dotnet-svcutil.exe");
        var fileSystem = new FakeFileSystem([windowsGlobalToolPath]);
        var processRunner = new FakeProcessRunner(invocation =>
        {
            if (invocation.FileName == "dotnet-svcutil")
            {
                return new DotNetSvcUtilRunner.ProcessExecutionResult(-1, string.Empty, "not on PATH");
            }

            if (string.Equals(invocation.FileName, windowsGlobalToolPath, StringComparison.OrdinalIgnoreCase))
            {
                return new DotNetSvcUtilRunner.ProcessExecutionResult(0, "windows tool help", string.Empty);
            }

            return new DotNetSvcUtilRunner.ProcessExecutionResult(-1, string.Empty, "unexpected");
        });

        var runner = CreateRunner(processRunner, fileSystem, userProfileDirectory: userProfile);
        var result = await runner.CheckAvailabilityAsync(configuredToolPath: null, @"C:\repo", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ToolFound, Is.True);
            Assert.That(result.ToolExecutionMode, Is.EqualTo(DotNetSvcUtilExecutionMode.GlobalWindowsPath));
            Assert.That(result.ToolPath, Is.EqualTo(windowsGlobalToolPath));
        });
    }

    [Test]
    public async Task DetectsLocalToolManifest()
    {
        const string manifestPath = @"C:\repo\.config\dotnet-tools.json";
        var fileSystem = new FakeFileSystem(
            [manifestPath],
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [manifestPath] = """
                    {
                      "version": 1,
                      "isRoot": true,
                      "tools": {
                        "dotnet-svcutil": {
                          "version": "8.0.0",
                          "commands": [ "dotnet-svcutil" ]
                        }
                      }
                    }
                    """
            });

        var processRunner = new FakeProcessRunner(invocation =>
        {
            if (invocation.FileName == "dotnet-svcutil")
            {
                return new DotNetSvcUtilRunner.ProcessExecutionResult(-1, string.Empty, "not on PATH");
            }

            if (invocation.FileName == "dotnet" && invocation.Arguments.SequenceEqual(["tool", "restore"]))
            {
                return new DotNetSvcUtilRunner.ProcessExecutionResult(0, "restore ok", string.Empty);
            }

            if (invocation.FileName == "dotnet"
                && invocation.Arguments.SequenceEqual(["tool", "run", "dotnet-svcutil", "--", "--help"]))
            {
                return new DotNetSvcUtilRunner.ProcessExecutionResult(0, "local help", string.Empty);
            }

            return new DotNetSvcUtilRunner.ProcessExecutionResult(-1, string.Empty, "unexpected");
        });

        var runner = CreateRunner(
            processRunner,
            fileSystem,
            currentDirectory: @"C:\repo\src\app\bin\Debug",
            applicationBaseDirectory: @"C:\repo\src\app\bin\Debug\net10.0-windows");

        var result = await runner.CheckAvailabilityAsync(configuredToolPath: null, workingDirectory: null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ToolFound, Is.True);
            Assert.That(result.ToolExecutionMode, Is.EqualTo(DotNetSvcUtilExecutionMode.LocalTool));
            Assert.That(result.ToolPath, Is.EqualTo(manifestPath));
            Assert.That(processRunner.Invocations.Any(static invocation => invocation.FileName == "dotnet" && invocation.Arguments.SequenceEqual(["tool", "restore"])), Is.True);
        });
    }

    [Test]
    public async Task DoesNotRunToolRestoreWithoutManifest()
    {
        var processRunner = new FakeProcessRunner(_ => new DotNetSvcUtilRunner.ProcessExecutionResult(-1, string.Empty, "missing"));
        var runner = CreateRunner(processRunner, new FakeFileSystem(), currentDirectory: @"C:\isolated");

        var result = await runner.CheckAvailabilityAsync(configuredToolPath: null, workingDirectory: @"C:\isolated", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ToolFound, Is.False);
            Assert.That(processRunner.Invocations.Any(static invocation => invocation.FileName == "dotnet" && invocation.Arguments.SequenceEqual(["tool", "restore"])), Is.False);
        });
    }

    [Test]
    public async Task ReturnsImprovedErrorMessageWhenToolIsNotFound()
    {
        var processRunner = new FakeProcessRunner(_ => new DotNetSvcUtilRunner.ProcessExecutionResult(-1, string.Empty, "missing"));
        var runner = CreateRunner(processRunner, new FakeFileSystem(), currentDirectory: @"C:\isolated");

        var result = await runner.CheckAvailabilityAsync(configuredToolPath: null, workingDirectory: @"C:\isolated", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ToolFound, Is.False);
            Assert.That(result.DiagnosticMessage, Does.Contain("dotnet-svcutil could not be located from the generator process"));
            Assert.That(result.DiagnosticMessage, Does.Not.Contain("Run 'dotnet tool restore'"));
        });
    }

    [Test]
    public async Task BuildsExpectedArgumentsForNetTcpMexEndpoint()
    {
        var fileSystem = new FakeFileSystem();
        var outputDirectory = Path.Combine(Path.GetTempPath(), "WcfNetTcpClientGenerator.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var outputFilePath = Path.Combine(outputDirectory, "GeneratedProxy.cs");
            var processRunner = new FakeProcessRunner(invocation =>
            {
                if (invocation.FileName == "dotnet-svcutil" && invocation.Arguments.SequenceEqual(["--help"]))
                {
                    return new DotNetSvcUtilRunner.ProcessExecutionResult(0, "help", string.Empty);
                }

                if (invocation.FileName == "dotnet-svcutil")
                {
                    fileSystem.AddFile(outputFilePath, "// proxy");
                    return new DotNetSvcUtilRunner.ProcessExecutionResult(0, "generated", string.Empty);
                }

                return new DotNetSvcUtilRunner.ProcessExecutionResult(-1, string.Empty, "unexpected");
            });

            var runner = CreateRunner(processRunner, fileSystem);
            var result = await runner.GenerateProxyAsync(
                ["net.tcp://server:808/MyService/mex"],
                outputDirectory,
                outputFilePath,
                "GeneratedNamespace",
                configuredToolPath: null,
                CancellationToken.None);

            var generationInvocation = processRunner.Invocations.Last();

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(generationInvocation.FileName, Is.EqualTo("dotnet-svcutil"));
                Assert.That(generationInvocation.Arguments, Does.Contain("net.tcp://server:808/MyService/mex"));
                Assert.That(generationInvocation.Arguments, Does.Contain("-n"));
                Assert.That(generationInvocation.Arguments, Does.Contain("*,GeneratedNamespace"));
                Assert.That(generationInvocation.Arguments, Does.Contain("-o"));
                Assert.That(generationInvocation.Arguments, Does.Contain("GeneratedProxy.cs"));
            });
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    [Test]
    public async Task CapturesStdoutAndStderrFromFailedProxyGeneration()
    {
        var processRunner = new FakeProcessRunner(invocation =>
        {
            if (invocation.FileName == "dotnet-svcutil" && invocation.Arguments.SequenceEqual(["--help"]))
            {
                return new DotNetSvcUtilRunner.ProcessExecutionResult(0, "help", string.Empty);
            }

            if (invocation.FileName == "dotnet-svcutil")
            {
                return new DotNetSvcUtilRunner.ProcessExecutionResult(1, "proxy stdout", "proxy stderr");
            }

            return new DotNetSvcUtilRunner.ProcessExecutionResult(-1, string.Empty, "unexpected");
        });

        var runner = CreateRunner(processRunner, new FakeFileSystem());
        var proxyGenerator = new ProxyCodeGenerator(runner);
        var outputDirectory = Path.Combine(Path.GetTempPath(), "WcfNetTcpClientGenerator.Tests", Guid.NewGuid().ToString("N"));

        try
        {
            var result = await proxyGenerator.GenerateAsync(
                ["net.tcp://server:808/MyService/mex"],
                outputDirectory,
                "Generated.Namespace",
                configuredToolPath: null,
                CancellationToken.None);

            var diagnostic = result.Diagnostics.Single();
            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(diagnostic.Message, Does.Contain("proxy stdout"));
                Assert.That(diagnostic.Message, Does.Contain("proxy stderr"));
                Assert.That(diagnostic.Message, Does.Contain("Command:"));
                Assert.That(diagnostic.Message, Does.Contain("Exit code: 1"));
            });
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    private static DotNetSvcUtilRunner CreateRunner(
        FakeProcessRunner processRunner,
        FakeFileSystem fileSystem,
        string currentDirectory = @"C:\repo",
        string applicationBaseDirectory = @"C:\repo\bin",
        string userProfileDirectory = @"C:\Users\Tester")
        => new(
            processRunner,
            fileSystem,
            () => currentDirectory,
            applicationBaseDirectory,
            userProfileDirectory);

    private sealed class FakeProcessRunner : DotNetSvcUtilRunner.IProcessRunner
    {
        private readonly Func<DotNetSvcUtilRunner.ProcessInvocation, DotNetSvcUtilRunner.ProcessExecutionResult> _handler;

        public FakeProcessRunner(Func<DotNetSvcUtilRunner.ProcessInvocation, DotNetSvcUtilRunner.ProcessExecutionResult> handler)
        {
            _handler = handler;
        }

        public List<DotNetSvcUtilRunner.ProcessInvocation> Invocations { get; } = [];

        public Task<DotNetSvcUtilRunner.ProcessExecutionResult> RunAsync(
            DotNetSvcUtilRunner.ProcessInvocation invocation,
            CancellationToken cancellationToken)
        {
            Invocations.Add(invocation);
            return Task.FromResult(_handler(invocation));
        }
    }

    private sealed class FakeFileSystem : DotNetSvcUtilRunner.IFileSystem
    {
        private readonly HashSet<string> _files;
        private readonly Dictionary<string, string> _fileContents;

        public FakeFileSystem()
            : this([], new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
        {
        }

        public FakeFileSystem(IEnumerable<string> files)
            : this(files, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
        {
        }

        public FakeFileSystem(IEnumerable<string> files, Dictionary<string, string> fileContents)
        {
            _files = files
                .Select(static path => Path.GetFullPath(path))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _fileContents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in fileContents)
            {
                _fileContents[Path.GetFullPath(pair.Key)] = pair.Value;
                _files.Add(Path.GetFullPath(pair.Key));
            }
        }

        public bool FileExists(string path)
            => _files.Contains(Path.GetFullPath(path)) || File.Exists(path);

        public string ReadAllText(string path)
        {
            var fullPath = Path.GetFullPath(path);
            if (_fileContents.TryGetValue(fullPath, out var content))
            {
                return content;
            }

            return File.ReadAllText(path);
        }

        public void AddFile(string path, string contents)
        {
            var fullPath = Path.GetFullPath(path);
            _files.Add(fullPath);
            _fileContents[fullPath] = contents;
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, contents);
        }
    }
}
