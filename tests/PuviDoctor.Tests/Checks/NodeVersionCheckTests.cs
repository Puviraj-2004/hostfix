using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Checks.Versions;
using PuviDoctor.Core.Models;
using PuviDoctor.Tests.Fakes;
using Shouldly;
using Xunit;
using IOPath = System.IO.Path;

namespace PuviDoctor.Tests.Checks;

public sealed class NodeVersionCheckTests
{
    private readonly FakeFileSystem _fakeFs;
    private readonly FakeProcessRunner _fakeProcessRunner;
    private readonly NodeVersionCheck _check;
    private const string ProjectRoot = @"C:\MockProject";

    public NodeVersionCheckTests()
    {
        _fakeFs = new FakeFileSystem();
        _fakeProcessRunner = new FakeProcessRunner();
        _check = new NodeVersionCheck(_fakeFs, _fakeProcessRunner);
    }

    [Fact]
    public async Task RunAsync_WhenNoConfigExists_SkipsCheck()
    {
        // Arrange
        var context = new CheckContext(ProjectRoot);

        // Act
        var result = await _check.RunAsync(context);

        // Assert
        result.Status.ShouldBe(CheckStatus.Skip);
        result.Message.ShouldContain("skipped");
    }

    [Fact]
    public async Task RunAsync_WhenNvmrcMatchesInstalledVersion_ReturnsOk()
    {
        // Arrange
        _fakeFs.AddFile(IOPath.Combine(ProjectRoot, ".nvmrc"), "20.11.0");
        _fakeProcessRunner.RegisterOutput("node --version", 0, "v20.11.0");
        var context = new CheckContext(ProjectRoot);

        // Act
        var result = await _check.RunAsync(context);

        // Assert
        result.Status.ShouldBe(CheckStatus.Ok);
        result.Message.ShouldContain("satisfies '20.11.0' (from .nvmrc)");
    }

    [Fact]
    public async Task RunAsync_WhenNpmRangeMatchesInstalledVersion_ReturnsOk()
    {
        // Arrange
        var packageJson = """
        {
            "engines": {
                "node": ">=18.0.0 <22.0.0"
            }
        }
        """;
        _fakeFs.AddFile(IOPath.Combine(ProjectRoot, "package.json"), packageJson);
        _fakeProcessRunner.RegisterOutput("node --version", 0, "v20.11.1");
        var context = new CheckContext(ProjectRoot);

        // Act
        var result = await _check.RunAsync(context);

        // Assert
        result.Status.ShouldBe(CheckStatus.Ok);
        result.Message.ShouldContain("satisfies '>=18.0.0 <22.0.0'");
    }

    [Fact]
    public async Task RunAsync_WhenVersionMismatches_ReturnsFail()
    {
        // Arrange
        _fakeFs.AddFile(IOPath.Combine(ProjectRoot, ".nvmrc"), "22.0.0");
        _fakeProcessRunner.RegisterOutput("node --version", 0, "v20.11.0");
        var context = new CheckContext(ProjectRoot);

        // Act
        var result = await _check.RunAsync(context);

        // Assert
        result.Status.ShouldBe(CheckStatus.Fail);
        result.Message.ShouldContain("Installed v20.11.0, required '22.0.0'");
    }
}