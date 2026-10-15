using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Fixes;
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
}