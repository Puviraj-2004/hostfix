using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Checks.Path;
using PuviDoctor.Core.Models;
using PuviDoctor.Tests.Fakes;
using Shouldly;
using Xunit;

namespace PuviDoctor.Tests.Checks;

public sealed class DuplicatePathCheckTests
{
    private readonly FakeEnvironmentService _fakeEnv;
    private readonly FakeRegistryService _fakeRegistry;
    private readonly DuplicatePathCheck _check;
    private readonly CheckContext _context;

    public DuplicatePathCheckTests()
    {
        _fakeEnv = new FakeEnvironmentService();
        _fakeRegistry = new FakeRegistryService();
        _check = new DuplicatePathCheck(_fakeEnv, _fakeRegistry);
        _context = new CheckContext(ProjectRootDirectory: Directory.GetCurrentDirectory());
    }

    [Fact]
    public async Task RunAsync_WhenNoDuplicates_ReturnsOk()
    {
        // Arrange
        _fakeEnv.RawPath = @"C:\Tools;C:\Bin;C:\Node";

        // Act
        var result = await _check.RunAsync(_context);

        // Assert
        result.Status.ShouldBe(CheckStatus.Ok);
        result.Message.ShouldContain("No duplicate PATH entries found");
        result.Details.ShouldBeNull();
    }

    [Fact]
    public async Task RunAsync_WhenCaseDiffersOnWindows_DetectsDuplicate()
    {
        // Arrange
        _fakeEnv.Windows = true;
        _fakeEnv.RawPath = @"C:\Program Files\Node;c:\program files\node";

        // Act
        var result = await _check.RunAsync(_context);

        // Assert
        result.Status.ShouldBe(CheckStatus.Warn);
        result.Message.ShouldBe("1 duplicate entries found in PATH.");
        result.Details.ShouldNotBeNull();
        result.Details!.ShouldContain(@"c:\program files\node");
        result.Fix.ShouldNotBeNull();
    }

    [Fact]
    public async Task RunAsync_WhenTrailingSlashDiffers_DetectsDuplicate()
    {
        // Arrange
        _fakeEnv.Windows = true;
        _fakeEnv.RawPath = @"C:\Tools\Bin\;C:\Tools\Bin";

        // Act
        var result = await _check.RunAsync(_context);

        // Assert
        result.Status.ShouldBe(CheckStatus.Warn);
        result.Message.ShouldBe("1 duplicate entries found in PATH.");
        result.Details.ShouldNotBeNull();
        result.Details!.ShouldContain(@"C:\Tools\Bin");
    }

    [Fact]
    public async Task RunAsync_WhenOnUnix_CaseMatters()
    {
        // Arrange
        _fakeEnv.Windows = false;
        _fakeEnv.RawPath = "/usr/local/bin:/USR/LOCAL/BIN";

        // Act
        var result = await _check.RunAsync(_context);

        // Assert
        result.Status.ShouldBe(CheckStatus.Ok);
    }
}