using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Checks.Path;
using PuviDoctor.Core.Models;
using PuviDoctor.Tests.Fakes;
using Shouldly;
using Xunit;

namespace PuviDoctor.Tests.Checks;

public sealed class DeadPathCheckTests
{
    private readonly FakeEnvironmentService _fakeEnv;
    private readonly FakeRegistryService _fakeRegistry;
    private readonly DeadPathCheck _check;
    private readonly CheckContext _context;

    public DeadPathCheckTests()
    {
        _fakeEnv = new FakeEnvironmentService();
        _fakeRegistry = new FakeRegistryService();
        _check = new DeadPathCheck(_fakeEnv, _fakeRegistry);
        _context = new CheckContext(ProjectRootDirectory: Directory.GetCurrentDirectory());
    }

    [Fact]
    public async Task RunAsync_WhenAllPathsExist_ReturnsOk()
    {
        // Arrange
        _fakeEnv.ExistingDirectories.Add(@"C:\Tools\Bin");
        _fakeEnv.ExistingDirectories.Add(@"C:\Program Files\Nodejs");
        _fakeEnv.RawPath = @"C:\Tools\Bin;C:\Program Files\Nodejs";

        // Act
        var result = await _check.RunAsync(_context);

        // Assert
        result.Status.ShouldBe(CheckStatus.Ok);
        result.Message.ShouldContain("All PATH directories exist");
        result.Details.ShouldBeNull();
    }

    [Fact]
    public async Task RunAsync_WhenDeadPathsFound_ReturnsWarnWithDetails()
    {
        // Arrange
        _fakeEnv.ExistingDirectories.Add(@"C:\Valid\Path");
        _fakeEnv.RawPath = @"C:\Valid\Path;C:\Dead\Path1;C:\Dead\Path2";

        // Act
        var result = await _check.RunAsync(_context);

        // Assert
        result.Status.ShouldBe(CheckStatus.Warn);
        result.Message.ShouldBe("2 entries in PATH point to directories that do not exist.");
        result.Details.ShouldNotBeNull();
        result.Details!.ShouldContain(@"C:\Dead\Path1");
        result.Details!.ShouldContain(@"C:\Dead\Path2");
        result.Fix.ShouldNotBeNull();
    }

    [Fact]
    public async Task RunAsync_HandlesQuotedAndTrailingSpacesGracefully()
    {
        // Arrange
        _fakeEnv.ExistingDirectories.Add(@"C:\Program Files\Git\cmd");
        _fakeEnv.RawPath = @" ""C:\Program Files\Git\cmd"" ; C:\Dead\Directory ";

        // Act
        var result = await _check.RunAsync(_context);

        // Assert
        result.Status.ShouldBe(CheckStatus.Warn);
        result.Message.ShouldBe("1 entries in PATH point to directories that do not exist.");
        result.Details.ShouldNotBeNull();
        result.Details!.ShouldContain(@"C:\Dead\Directory");
    }

    [Fact]
    public async Task RunAsync_WhenPathIsNull_ReturnsWarn()
    {
        // Arrange
        _fakeEnv.RawPath = null;

        // Act
        var result = await _check.RunAsync(_context);

        // Assert
        result.Status.ShouldBe(CheckStatus.Warn);
        result.Message.ShouldContain("empty or unset");
    }
}