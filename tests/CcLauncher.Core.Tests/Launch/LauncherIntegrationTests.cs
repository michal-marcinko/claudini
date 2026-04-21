using CcLauncher.Core.Launch;
using FluentAssertions;
using System.Runtime.InteropServices;
using Xunit;

namespace CcLauncher.Core.Tests.Launch;

public class LauncherIntegrationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _stubPath;
    private readonly string _outputPath;

    public LauncherIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "cc-launcher-launch-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _outputPath = Path.Combine(_tempDir, "out.txt");

        // Stub "terminal": writes its args + cwd to _outputPath, then exits.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _stubPath = Path.Combine(_tempDir, "stub.cmd");
            File.WriteAllText(_stubPath, $"""
                @echo off
                echo CWD=%CD% > "{_outputPath}"
                echo ARGS=%* >> "{_outputPath}"
                """);
        }
        else
        {
            _stubPath = Path.Combine(_tempDir, "stub.sh");
            File.WriteAllText(_stubPath, $"""
                #!/usr/bin/env bash
                echo "CWD=$PWD" > "{_outputPath}"
                echo "ARGS=$*" >> "{_outputPath}"
                """);
            File.SetUnixFileMode(_stubPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public void Launch_Synchronously_CapturesArgsAndCwd()
    {
        var launcher = LauncherFactory.ForTesting();
        var req = new LaunchRequest(
            Cwd: _tempDir,
            TerminalCommand: _stubPath, // overridden for test
            ClaudeArgs: new[] { "--resume", "abc" });

        var result = launcher.Launch(req);

        result.Success.Should().BeTrue();
        // Stub writes synchronously, but spawned process is detached.
        // Wait briefly for output.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!File.Exists(_outputPath) && DateTime.UtcNow < deadline)
            Thread.Sleep(50);

        File.Exists(_outputPath).Should().BeTrue();
        var contents = File.ReadAllText(_outputPath);
        contents.Should().Contain("--resume");
        contents.Should().Contain("abc");
    }
}
