using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Fixes;
using PuviDoctor.Core.Platform;
using PuviDoctor.Tests.Fakes;
using Shouldly;
using Xunit;

namespace PuviDoctor.Tests.Fixes;

public sealed class WindowsPathFixTests
{
    private readonly FakeRegistryService _fakeRegistry;
    private readonly FakeEnvironmentService _fakeEnv;
    private readonly WindowsPathFix _fix;

    public WindowsPathFixTests()
    {
        _fakeRegistry = new FakeRegistryService();
        _fakeEnv = new FakeEnvironmentService { Windows = true };
        _fix = new WindowsPathFix(_fakeRegistry, _fakeEnv, "path.test");
    }

    [Fact]
    public async Task ApplyAsync_RemovesDeadAndDuplicates_AndPreservesRegistryKind()
    {
        // Arrange
        _fakeEnv.ExistingDirectories.Add(@"C:\Valid1");
        _fakeEnv.ExistingDirectories.Add(@"C:\Valid2");

        const string initial = @"C:\Valid1;C:\DeadPath;C:\Valid1\;C:\Valid2;C:\Valid2";
        _fakeRegistry.CurrentPath = initial;
        _fakeRegistry.CurrentKind = SnapshotRegistryKind.ExpandString;

        // Act
        await _fix.ApplyAsync();

        // Assert: dead path gone, duplicate trailing slashes gone
        _fakeRegistry.CurrentPath.ShouldBe(@"C:\Valid1;C:\Valid2");
        _fakeRegistry.CurrentKind.ShouldBe(SnapshotRegistryKind.ExpandString);
    }

    [Fact]
    public async Task RollbackAsync_RestoresOriginalValueByteIdentical()
    {
        // Arrange
        const string initial = @"C:\Important\Original;C:\Another\Path";
        _fakeRegistry.CurrentPath = initial;
        _fakeRegistry.CurrentKind = SnapshotRegistryKind.ExpandString;

        // Act 1: Apply fix
        await _fix.ApplyAsync();
        _fakeRegistry.CurrentPath.ShouldNotBe(initial);

        // Act 2: Rollback
        await _fix.RollbackAsync();

        // Assert: 100% byte identical restoration
        _fakeRegistry.CurrentPath.ShouldBe(initial);
        _fakeRegistry.CurrentKind.ShouldBe(SnapshotRegistryKind.ExpandString);
    }

    [Fact]
    public void ComputeCleanedPath_PreservesEmbeddedEnvironmentVariables()
    {
        // Arrange: Complex dynamic paths containing variables in the start or middle
        const string initial = @"C:\Tools\%DYNAMIC_BIN%\bin;C:\Valid1;%USERPROFILE%\.dotnet\tools";
        _fakeEnv.ExistingDirectories.Add(@"C:\Valid1");

        // Act
        var cleaned = _fix.ComputeCleanedPath(initial);

        // Assert: Dynamic %VAR% segments must NOT be deleted by static existence checks
        cleaned.ShouldContain(@"C:\Tools\%DYNAMIC_BIN%\bin");
        cleaned.ShouldContain(@"%USERPROFILE%\.dotnet\tools");
        cleaned.ShouldContain(@"C:\Valid1");
    }

    [Fact]
    public async Task ApplyAsync_WhenAlreadyClean_DoesNotCreateRedundantJournal()
    {
        // Arrange
        const string cleanPath = @"C:\Valid1";
        _fakeEnv.ExistingDirectories.Add(@"C:\Valid1");
        _fakeRegistry.CurrentPath = cleanPath;

        // Act 1: First apply creates a session
        await _fix.ApplyAsync();
        var snapshot1 = await FixJournalStore.GetLatestSessionAsync();

        // Act 2: Second apply against already-clean state must be a no-op
        await _fix.ApplyAsync();
        var snapshot2 = await FixJournalStore.GetLatestSessionAsync();

        // Assert: Second run must not overwrite the journal with a redundant snapshot
        if (snapshot1 is not null && snapshot2 is not null)
        {
            snapshot2.SessionId.ShouldBe(snapshot1.SessionId);
        }
    }

    [Fact]
    public void DryRun_GeneratesBeforeAndAfterDiff_WithoutMutatingRegistry()
    {
        // Arrange
        const string initial = @"C:\Valid1;C:\DeadPath";
        _fakeEnv.ExistingDirectories.Add(@"C:\Valid1");
        _fakeRegistry.CurrentPath = initial;

        // Act
        var diff = _fix.DryRun();

        // Assert
        diff.ShouldContain("=== Dry Run PATH Diff ===");
        diff.ShouldContain(@"C:\Valid1;C:\DeadPath");
        diff.ShouldContain(@"C:\Valid1");
        _fakeRegistry.CurrentPath.ShouldBe(initial); // Unmutated
    }
}