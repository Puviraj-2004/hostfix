using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Models;
using PuviDoctor.Core.Platform;
using PuviDoctor.Tests.Fakes;
using Shouldly;
using Xunit;
using IOPath = System.IO.Path;

namespace PuviDoctor.Tests.Reporting;

public sealed class BaselineTests
{
    private readonly FakeFileSystem _fakeFs;
    private const string ProjectRoot = @"C:\MockProject";

    public BaselineTests()
    {
        _fakeFs = new FakeFileSystem();
    }

    [Fact]
    public async Task Baseline_WhenFindingMatchesAcceptedEntry_FiltersRegression()
    {
        // Arrange: Accepted baseline containing a known warning
        var baselineJson = """
        {
          "schemaVersion": 1,
          "createdAtUtc": "2026-10-15T00:00:00Z",
          "acceptedFindings": [
            {
              "checkId": "path.dead-entries",
              "status": "WARN",
              "message": "3 entries in PATH point to directories that do not exist."
            }
          ]
        }
        """;

        _fakeFs.AddFile(IOPath.Combine(ProjectRoot, ".puvi-doctor.baseline.json"), baselineJson);

        // Act
        var baseline = await BaselineStore.LoadBaselineAsync(ProjectRoot, _fakeFs);

        // Assert
        baseline.ShouldNotBeNull();
        baseline!.AcceptedFindings.Count.ShouldBe(1);
        baseline.AcceptedFindings[0].CheckId.ShouldBe("path.dead-entries");
    }
}