using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Checks.Versions;
using PuviDoctor.Core.Models;
using PuviDoctor.Tests.Fakes;
using Shouldly;
using Xunit;

namespace PuviDoctor.Tests.Checks;

public sealed class GitVersionCheckTests
{
    private readonly FakeProcessRunner _fakeProcessRunner;
    private readonly GitVersionCheck _check;
    private readonly CheckContext _context;

    public GitVersionCheckTests()
    {
        _fakeProcessRunner = new FakeProcessRunner();
        _check = new GitVersionCheck(_fakeProcessRunner);
        _context = new CheckContext(Directory.GetCurrentDirectory());
    }

    [Fact]
    public async Task RunAsync_WhenGitMeetsMinimum_ReturnsOk()
    {
        // Arrange
        _fakeProcessRunner.RegisterOutput("git --version", 0, "git version 2.45.2.windows.1");

        // Act
        var result = await _check.RunAsync(_context);

        // Assert
        result.Status.ShouldBe(CheckStatus.Ok);
        result.Message.ShouldContain("Git 2.45.2 installed and up to date.");
    }

    [Fact]
    public async Task RunAsync_WhenGitIsOutdated_ReturnsFail()
    {
        // Arrange
        _fakeProcessRunner.RegisterOutput("git --version", 0, "git version 2.20.1");

        // Act
        var result = await _check.RunAsync(_context);

        // Assert
        result.Status.ShouldBe(CheckStatus.Fail);
        result.Message.ShouldContain("below minimum requirement");
        result.Details.ShouldNotBeNull();
        result.Details!.ShouldContain("winget upgrade Git.Git");
    }

    [Fact]
    public async Task RunAsync_WhenGitNotInstalled_ReturnsFail()
    {
        // Arrange
        _fakeProcessRunner.RegisterOutput("git --version", 1, "", "git: command not found");

        // Act
        var result = await _check.RunAsync(_context);

        // Assert
        result.Status.ShouldBe(CheckStatus.Fail);
        result.Message.ShouldContain("Git is not found on system PATH.");
    }
}