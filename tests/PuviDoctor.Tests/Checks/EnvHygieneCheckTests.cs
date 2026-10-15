using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Checks.Hygiene;
using PuviDoctor.Core.Models;
using PuviDoctor.Tests.Fakes;
using Shouldly;
using Xunit;
using IOPath = System.IO.Path;

namespace PuviDoctor.Tests.Checks;

public sealed class EnvHygieneCheckTests
{
    private readonly FakeFileSystem _fakeFs;
    private readonly EnvHygieneCheck _check;
    private const string ProjectRoot = @"C:\MockProject";
    private readonly CheckContext _context;

    public EnvHygieneCheckTests()
    {
        _fakeFs = new FakeFileSystem();
        _check = new EnvHygieneCheck(_fakeFs);
        _context = new CheckContext(ProjectRoot);
    }

    [Fact]
    public async Task RunAsync_WhenMissingKeys_FailsWithKeyNamesOnly()
    {
        // Arrange
        const string example = "DATABASE_URL=\nJWT_SECRET=\nAPI_KEY=";
        const string env = "DATABASE_URL=postgres://localhost\n# Missing others";

        _fakeFs.AddFile(IOPath.Combine(ProjectRoot, ".env.example"), example);
        _fakeFs.AddFile(IOPath.Combine(ProjectRoot, ".env"), env);

        // Act
        var result = await _check.RunAsync(_context);

        // Assert
        result.Status.ShouldBe(CheckStatus.Fail);
        result.Message.ShouldContain("missing 2 required key(s)");
        result.Details.ShouldNotBeNull();
        result.Details!.ShouldContain("JWT_SECRET");
        result.Details!.ShouldContain("API_KEY");
    }

    [Fact]
    public async Task RunAsync_GuaranteesZeroSecretValuesLeakedInOutput()
    {
        // Production Rule: Secret values must NEVER be emitted in messages or details
        const string plantedSecret = "SuperSecretSuperPrivateDatabasePassword12345!";

        _fakeFs.AddFile(IOPath.Combine(ProjectRoot, ".env.example"), "DB_PASS=");
        _fakeFs.AddFile(IOPath.Combine(ProjectRoot, ".env"), $"DB_PASS={plantedSecret}");

        // Act
        var result = await _check.RunAsync(_context);

        // Assert: Planted secret must NOT appear in output message or details
        result.Message.ShouldNotContain(plantedSecret);
        if (result.Details is not null)
        {
            result.Details.ShouldNotContain(plantedSecret);
        }
    }
}