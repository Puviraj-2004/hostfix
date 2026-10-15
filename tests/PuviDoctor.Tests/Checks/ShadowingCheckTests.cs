using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Checks.Path;
using PuviDoctor.Core.Models;
using PuviDoctor.Tests.Fakes;
using Shouldly;
using Xunit;

namespace PuviDoctor.Tests.Checks;

public sealed class ShadowingCheckTests
{
    private readonly FakeEnvironmentService _fakeEnv;
    private readonly FakeProcessRunner _fakeProcessRunner;
    private readonly ShadowingCheck _check;
    private readonly CheckContext _context;

    public ShadowingCheckTests()
    {
        _fakeEnv = new FakeEnvironmentService { Windows = true };
        _fakeProcessRunner = new FakeProcessRunner();
        _check = new ShadowingCheck(_fakeEnv, _fakeProcessRunner);
        _context = new CheckContext(ProjectRootDirectory: Directory.GetCurrentDirectory());
    }

    [Fact]
    public async Task RunAsync_WhenNoShadowing_ReturnsOk()
    {
        // Arrange
        _fakeProcessRunner.RegisterOutput("where.exe node", 0, @"C:\Program Files\nodejs\node.exe");

        // Act
        var result = await _check.RunAsync(_context);

        // Assert
        result.Status.ShouldBe(CheckStatus.Ok);
        result.Message.ShouldContain("No tool shadowing detected");
    }

    [Fact]
    public async Task RunAsync_WhenMultipleNodeBinariesFound_ReturnsWarnWithDetails()
    {
        // Arrange: Mise shim vs System node
        var stdout = string.Join(Environment.NewLine, [
            @"C:\Users\User\.local\share\mise\shims\node.exe",
            @"C:\Program Files\nodejs\node.exe"
        ]);
        _fakeProcessRunner.RegisterOutput("where.exe node", 0, stdout);

        // Act
        var result = await _check.RunAsync(_context);

        // Assert
        result.Status.ShouldBe(CheckStatus.Warn);
        result.Message.ShouldBe("1 tool(s) have shadowing duplicates on PATH.");
        result.Details.ShouldNotBeNull();
        result.Details!.ShouldContain("Active winner: 'C:\\Users\\User\\.local\\share\\mise\\shims\\node.exe'");
        result.Details!.ShouldContain("Shadowed: 'C:\\Program Files\\nodejs\\node.exe'");
    }
}