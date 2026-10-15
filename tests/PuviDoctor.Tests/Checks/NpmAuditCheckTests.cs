using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Checks.Dependencies;
using PuviDoctor.Core.Models;
using PuviDoctor.Tests.Fakes;
using Shouldly;
using Xunit;
using IOPath = System.IO.Path;

namespace PuviDoctor.Tests.Checks;

public sealed class NpmAuditCheckTests
{
    private readonly FakeFileSystem _fakeFs;
    private readonly FakeProcessRunner _fakeProcessRunner;
    private readonly NpmAuditCheck _check;
    private const string ProjectRoot = @"C:\MockProject";
    private readonly CheckContext _context;

    public NpmAuditCheckTests()
    {
        _fakeFs = new FakeFileSystem();
        _fakeProcessRunner = new FakeProcessRunner();
        _check = new NpmAuditCheck(_fakeFs, _fakeProcessRunner);
        _context = new CheckContext(ProjectRoot);
    }

    [Fact]
    public async Task RunAsync_WhenNoLockfile_SkipsCheck()
    {
        // Act
        var result = await _check.RunAsync(_context);

        // Assert
        result.Status.ShouldBe(CheckStatus.Skip);
        result.Message.ShouldContain("skipped");
    }

    [Fact]
    public async Task RunAsync_WhenZeroVulnerabilities_ReturnsOk()
    {
        // Arrange
        _fakeFs.AddFile(IOPath.Combine(ProjectRoot, "package.json"), "{}");
        _fakeFs.AddFile(IOPath.Combine(ProjectRoot, "package-lock.json"), "{}");

        var auditJson = """
        {
            "metadata": {
                "vulnerabilities": { "info": 0, "low": 0, "moderate": 0, "high": 0, "critical": 0, "total": 0 }
            }
        }
        """;
        _fakeProcessRunner.RegisterOutput("npm audit --json", 0, auditJson);

        // Act
        var result = await _check.RunAsync(_context);

        // Assert
        result.Status.ShouldBe(CheckStatus.Ok);
        result.Message.ShouldContain("0 vulnerabilities found");
    }

    [Fact]
    public async Task RunAsync_WhenOnlyDevVulnerabilitiesExist_ReturnsWarnNotFail()
    {
        // Arrange: 5 total vulnerabilities, but 0 in production
        _fakeFs.AddFile(IOPath.Combine(ProjectRoot, "package.json"), "{}");
        _fakeFs.AddFile(IOPath.Combine(ProjectRoot, "package-lock.json"), "{}");

        var fullJson = """
        {
            "metadata": {
                "vulnerabilities": { "low": 2, "moderate": 2, "high": 1, "critical": 0, "total": 5 }
            }
        }
        """;
        var prodJson = """
        {
            "metadata": {
                "vulnerabilities": { "low": 0, "moderate": 0, "high": 0, "critical": 0, "total": 0 }
            }
        }
        """;

        _fakeProcessRunner.RegisterOutput("npm audit --json", 1, fullJson);
        _fakeProcessRunner.RegisterOutput("npm audit --json --omit=dev", 0, prodJson);

        // Act
        var result = await _check.RunAsync(_context);

        // Assert: Dev-only must NOT fail the build; it must warn and clarify dev-only split
        result.Status.ShouldBe(CheckStatus.Warn);
        result.Message.ShouldBe("npm audit: 5 vulnerabilities (0 production, 5 dev-only).");
        result.Details.ShouldNotBeNull();
        result.Details!.ShouldContain("Production: 0 critical, 0 high");
    }

    [Fact]
    public async Task RunAsync_WhenProductionCriticalExists_ReturnsFail()
    {
        // Arrange: 1 critical vulnerability reaching production
        _fakeFs.AddFile(IOPath.Combine(ProjectRoot, "package.json"), "{}");
        _fakeFs.AddFile(IOPath.Combine(ProjectRoot, "package-lock.json"), "{}");

        var fullJson = """
        {
            "metadata": {
                "vulnerabilities": { "low": 0, "moderate": 0, "high": 0, "critical": 1, "total": 1 }
            }
        }
        """;
        var prodJson = """
        {
            "metadata": {
                "vulnerabilities": { "low": 0, "moderate": 0, "high": 0, "critical": 1, "total": 1 }
            }
        }
        """;

        _fakeProcessRunner.RegisterOutput("npm audit --json", 1, fullJson);
        _fakeProcessRunner.RegisterOutput("npm audit --json --omit=dev", 1, prodJson);

        // Act
        var result = await _check.RunAsync(_context);

        // Assert: Production high/critical must fail
        result.Status.ShouldBe(CheckStatus.Fail);
        result.Message.ShouldBe("npm audit: 1 vulnerabilities (1 production, 0 dev-only).");
        result.Details.ShouldNotBeNull();
        result.Details!.ShouldContain("Production: 1 critical");
    }
}