using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Checks.Dependencies;
using PuviDoctor.Core.Models;
using PuviDoctor.Tests.Fakes;
using Shouldly;
using Xunit;
using IOPath = System.IO.Path;

namespace PuviDoctor.Tests.Checks;

public sealed class LockfileCheckTests
{
    private readonly FakeFileSystem _fakeFs;
    private readonly LockfileCheck _check;
    private const string ProjectRoot = @"C:\MockProject";
    private readonly CheckContext _context;

    public LockfileCheckTests()
    {
        _fakeFs = new FakeFileSystem();
        _check = new LockfileCheck(_fakeFs);
        _context = new CheckContext(ProjectRoot);
    }

    [Fact]
    public async Task RunAsync_WhenNoPackageJson_Skips()
    {
        // Act
        var result = await _check.RunAsync(_context);

        // Assert
        result.Status.ShouldBe(CheckStatus.Skip);
    }

    [Fact]
    public async Task RunAsync_WhenSingleNpmLockfilePresent_ReturnsOk()
    {
        // Arrange
        _fakeFs.AddFile(IOPath.Combine(ProjectRoot, "package.json"), "{}");
        _fakeFs.AddFile(IOPath.Combine(ProjectRoot, "package-lock.json"), "{}");

        // Act
        var result = await _check.RunAsync(_context);

        // Assert
        result.Status.ShouldBe(CheckStatus.Ok);
        result.Message.ShouldContain("npm (package-lock.json)");
    }

    [Fact]
    public async Task RunAsync_WhenMissingLockfile_ReturnsWarn()
    {
        // Arrange
        _fakeFs.AddFile(IOPath.Combine(ProjectRoot, "package.json"), "{}");

        // Act
        var result = await _check.RunAsync(_context);

        // Assert
        result.Status.ShouldBe(CheckStatus.Warn);
        result.Message.ShouldContain("package-lock.json missing");
    }

    [Fact]
    public async Task RunAsync_WhenConflictingLockfilesPresent_ReturnsFail()
    {
        // Arrange: Both npm and pnpm lockfiles present
        _fakeFs.AddFile(IOPath.Combine(ProjectRoot, "package.json"), "{}");
        _fakeFs.AddFile(IOPath.Combine(ProjectRoot, "package-lock.json"), "{}");
        _fakeFs.AddFile(IOPath.Combine(ProjectRoot, "pnpm-lock.yaml"), "");

        // Act
        var result = await _check.RunAsync(_context);

        result.Status.ShouldBe(CheckStatus.Fail);
                result.Message.ShouldContain("Conflicting lockfiles detected");
                result.Details.ShouldNotBeNull();
                result.Details!.ShouldContain("Keep only one package manager lockfile");
    }
}