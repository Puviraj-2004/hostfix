using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Checks.Versions;
using PuviDoctor.Core.Models;
using PuviDoctor.Tests.Fakes;
using Shouldly;
using Xunit;
using IOPath = System.IO.Path;

namespace PuviDoctor.Tests.Checks;

public sealed class DotNetVersionCheckTests
{
    private readonly FakeFileSystem _fakeFs;
    private readonly FakeProcessRunner _fakeProcessRunner;
    private readonly DotNetVersionCheck _check;
    private const string ProjectRoot = @"C:\DotNetProject";

    public DotNetVersionCheckTests()
    {
        _fakeFs = new FakeFileSystem();
        _fakeProcessRunner = new FakeProcessRunner();
        _check = new DotNetVersionCheck(_fakeFs, _fakeProcessRunner);
    }

    [Fact]
    public async Task RunAsync_WhenNoGlobalJson_SkipsCheck()
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
    public async Task RunAsync_WhenModernGlobalJsonMatches_ReturnsOk()
    {
        // Arrange: Modern sdk.version syntax
        var globalJson = """
        {
            "sdk": {
                "version": "9.0.100",
                "rollForward": "latestFeature"
            }
        }
        """;
        _fakeFs.AddFile(IOPath.Combine(ProjectRoot, "global.json"), globalJson);
        _fakeProcessRunner.RegisterOutput(
            "dotnet --list-sdks",
            0,
            @"9.0.306 [C:\Program Files\dotnet\sdk]"
        );
        var context = new CheckContext(ProjectRoot);

        // Act
        var result = await _check.RunAsync(context);

        // Assert
        result.Status.ShouldBe(CheckStatus.Ok);
        result.Message.ShouldContain("satisfies required '9.0.100'");
    }

    [Fact]
    public async Task RunAsync_WhenRollForwardLatestPatchSatisfies_ReturnsOk()
    {
        // Arrange
        var globalJson = """
        {
            "sdk": {
                "version": "9.0.100",
                "rollForward": "latestPatch"
            }
        }
        """;
        _fakeFs.AddFile(IOPath.Combine(ProjectRoot, "global.json"), globalJson);
        _fakeProcessRunner.RegisterOutput(
            "dotnet --list-sdks",
            0,
            @"9.0.102 [C:\Program Files\dotnet\sdk]"
        );
        var context = new CheckContext(ProjectRoot);

        // Act
        var result = await _check.RunAsync(context);

        // Assert
        result.Status.ShouldBe(CheckStatus.Ok);
        result.Message.ShouldContain("satisfies required '9.0.100'");
    }

    [Fact]
    public async Task RunAsync_WhenRequiredSdkMissing_ReturnsFailWithFixDetails()
    {
        // Arrange
        var globalJson = """
        {
            "sdk": {
                "version": "9.0.100",
                "rollForward": "latestPatch"
            }
        }
        """;
        _fakeFs.AddFile(IOPath.Combine(ProjectRoot, "global.json"), globalJson);
        _fakeProcessRunner.RegisterOutput(
            "dotnet --list-sdks",
            0,
            @"8.0.400 [C:\Program Files\dotnet\sdk]"
        );
        var context = new CheckContext(ProjectRoot);

        // Act
        var result = await _check.RunAsync(context);

        // Assert
        result.Status.ShouldBe(CheckStatus.Fail);
        result.Message.ShouldContain("Required '9.0.100' (rollForward: latestPatch) not satisfied");
        result.Details.ShouldNotBeNull();
        result.Details!.ShouldContain("winget install Microsoft.DotNet.SDK.9");
    }
}