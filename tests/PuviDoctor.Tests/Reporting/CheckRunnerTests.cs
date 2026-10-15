using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Models;
using PuviDoctor.Core.Reporting;
using Shouldly;
using Xunit;

namespace PuviDoctor.Tests.Reporting;

public sealed class CheckRunnerTests
{
    private sealed class StubCheck(string id, CheckStatus status) : IHealthCheck
    {
        public string Id => id;
        public Task<CheckResult> RunAsync(CheckContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CheckResult(status, $"Stub message for {id}"));
    }

    [Fact]
    public async Task RunAllAsync_WhenAllOk_ReturnsExitCode0AndScore10()
    {
        // Arrange
        var checks = new IHealthCheck[]
        {
            new StubCheck("check.1", CheckStatus.Ok),
            new StubCheck("check.2", CheckStatus.Ok)
        };
        var runner = new CheckRunner(checks);
        var context = new CheckContext(Directory.GetCurrentDirectory());

        // Act
        var report = await runner.RunAllAsync(context);

        // Assert
        report.ExitCode.ShouldBe(0);
        report.Score.ShouldBe(10);
    }

    [Fact]
    public async Task RunAllAsync_WhenWarningsOnly_ReturnsExitCode1()
    {
        // Arrange
        var checks = new IHealthCheck[]
        {
            new StubCheck("check.1", CheckStatus.Ok),
            new StubCheck("check.2", CheckStatus.Warn)
        };
        var runner = new CheckRunner(checks);
        var context = new CheckContext(Directory.GetCurrentDirectory());

        // Act
        var report = await runner.RunAllAsync(context);

        // Assert
        report.ExitCode.ShouldBe(1);
        report.Score.ShouldBe(5);
    }

    [Fact]
    public async Task RunAllAsync_WhenFailExists_ReturnsExitCode2()
    {
        // Arrange
        var checks = new IHealthCheck[]
        {
            new StubCheck("check.1", CheckStatus.Ok),
            new StubCheck("check.2", CheckStatus.Fail)
        };
        var runner = new CheckRunner(checks);
        var context = new CheckContext(Directory.GetCurrentDirectory());

        // Act
        var report = await runner.RunAllAsync(context);

        // Assert
        report.ExitCode.ShouldBe(2);
        report.Score.ShouldBe(5);
    }
}