using System.CommandLine;
using PuviDoctor.Cli.Rendering;
using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Checks.Dependencies;
using PuviDoctor.Core.Checks.Hygiene;
using PuviDoctor.Core.Checks.Path;
using PuviDoctor.Core.Checks.Versions;
using PuviDoctor.Core.Models;
using PuviDoctor.Core.Platform;
using PuviDoctor.Core.Platform.Windows;
using PuviDoctor.Core.Reporting;
using Spectre.Console;

namespace PuviDoctor.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var envService = new SystemEnvironmentService();
        var processRunner = new SystemProcessRunner();
        var fileSystem = new SystemFileSystem();
        var registryService = new SystemRegistryService();
        var networkService = new SystemNetworkService();

        var pathChecks = new IHealthCheck[]
        {
            new DeadPathCheck(envService, registryService),
            new DuplicatePathCheck(envService, registryService),
            new ShadowingCheck(envService, processRunner)
        };

        var versionChecks = new IHealthCheck[]
        {
            new NodeVersionCheck(fileSystem, processRunner),
            new DotNetVersionCheck(fileSystem, processRunner),
            new GitVersionCheck(processRunner)
        };

        var dependencyChecks = new IHealthCheck[]
        {
            new LockfileCheck(fileSystem),
            new NpmrcCheck(fileSystem),
            new NpmAuditCheck(fileSystem, processRunner),
            new NuGetVulnsCheck(fileSystem, processRunner)
        };

        var hygieneChecks = new IHealthCheck[]
        {
            new EnvHygieneCheck(fileSystem),
            new PortCollisionCheck(networkService)
        };

        var allChecks = pathChecks
            .Concat(versionChecks)
            .Concat(dependencyChecks)
            .Concat(hygieneChecks)
            .ToArray();

        // CLI Options
        var pathOption = new Option<DirectoryInfo?>(
            aliases: ["--path", "-p"],
            description: "Target directory of the repo to evaluate",
            getDefaultValue: () => new DirectoryInfo(Directory.GetCurrentDirectory())
        );

        var fixOption = new Option<bool>(name: "--fix", description: "Apply safe fixes for identified issues");
        var yesOption = new Option<bool>(name: "--yes", description: "Automatically confirm all fixes without prompts");
        var dryRunOption = new Option<bool>(name: "--dry-run", description: "Print exact before/after diff of proposed fixes");
        var jsonOption = new Option<bool>(name: "--json", description: "Produce machine-readable JSON output (schema: 1)");

        var rootCommand = new RootCommand("puvi-doctor: Developer machine and project diagnostics CLI");
        rootCommand.AddGlobalOption(pathOption);
        rootCommand.AddOption(fixOption);
        rootCommand.AddOption(yesOption);
        rootCommand.AddOption(dryRunOption);
        rootCommand.AddOption(jsonOption);

        // Root action
        rootCommand.SetHandler(async (DirectoryInfo? targetDir, bool isFix, bool isYes, bool isDryRun, bool isJson) =>
        {
            var dir = targetDir?.FullName ?? Directory.GetCurrentDirectory();
            Environment.ExitCode = await ExecuteDoctorFlowAsync(dir, allChecks, isFix, isYes, isDryRun, isJson);
        }, pathOption, fixOption, yesOption, dryRunOption, jsonOption);

        // Subcommand: puvi-doctor rollback
        var rollbackCommand = new Command("rollback", "Undo the last applied fix session from backup journal");
        rollbackCommand.SetHandler(async () =>
        {
            Environment.ExitCode = await ExecuteRollbackAsync(registryService);
        });
        rootCommand.Add(rollbackCommand);

        // Subcommand: puvi-doctor path
        var pathCommand = new Command("path", "Run PATH sanity checks only");
        pathCommand.SetHandler(async (DirectoryInfo? targetDir) =>
        {
            var dir = targetDir?.FullName ?? Directory.GetCurrentDirectory();
            Environment.ExitCode = await ExecuteDoctorFlowAsync(dir, pathChecks, false, false, false, false);
        }, pathOption);
        rootCommand.Add(pathCommand);

        // Subcommand: puvi-doctor versions
        var versionsCommand = new Command("versions", "Run runtime version checks only (.NET, Node, Git)");
        versionsCommand.SetHandler(async (DirectoryInfo? targetDir) =>
        {
            var dir = targetDir?.FullName ?? Directory.GetCurrentDirectory();
            Environment.ExitCode = await ExecuteDoctorFlowAsync(dir, versionChecks, false, false, false, false);
        }, pathOption);
        rootCommand.Add(versionsCommand);

        // Subcommand: puvi-doctor deps
        var depsCommand = new Command("deps", "Run dependency vulnerability & lockfile triage only");
        depsCommand.SetHandler(async (DirectoryInfo? targetDir) =>
        {
            var dir = targetDir?.FullName ?? Directory.GetCurrentDirectory();
            Environment.ExitCode = await ExecuteDoctorFlowAsync(dir, dependencyChecks, false, false, false, false);
        }, pathOption);
        rootCommand.Add(depsCommand);

        // Subcommand: puvi-doctor report --md
        var reportCommand = new Command("report", "Generate exportable diagnostic reports");
        var mdOption = new Option<bool>(name: "--md", description: "Export report as markdown file (PUVI_DOCTOR_REPORT.md)");
        reportCommand.AddOption(mdOption);
        reportCommand.SetHandler(async (DirectoryInfo? targetDir, bool exportMd) =>
        {
            var dir = targetDir?.FullName ?? Directory.GetCurrentDirectory();
            Environment.ExitCode = await ExecuteReportCommandAsync(dir, allChecks, exportMd);
        }, pathOption, mdOption);
        rootCommand.Add(reportCommand);

        try
        {
            return await rootCommand.InvokeAsync(args);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"puvi-doctor internal failure: {ex.Message}");
            Console.ResetColor();
            return 3;
        }
    }

    private static async Task<int> ExecuteReportCommandAsync(string projectDirectory, IReadOnlyList<IHealthCheck> checks, bool exportMd)
    {
        var projectName = Path.GetFileName(projectDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrEmpty(projectName)) projectName = "workspace";

        var context = new CheckContext(projectDirectory);
        var runner = new CheckRunner(checks);
        var report = await runner.RunAllAsync(context);

        if (exportMd)
        {
            var mdContent = MarkdownRenderer.Render(report, projectName);
            var outputPath = Path.Combine(projectDirectory, "PUVI_DOCTOR_REPORT.md");
            await File.WriteAllTextAsync(outputPath, mdContent);
            AnsiConsole.MarkupLine($"[green bold][[SUCCESS]][/] Markdown handover report generated: [yellow]{outputPath}[/]");
            return 0;
        }

        ConsoleRenderer.RenderHeader(projectName, "0.4.0");
        ConsoleRenderer.RenderReport(report);
        return report.ExitCode;
    }

    private static async Task<int> ExecuteDoctorFlowAsync(
        string projectDirectory,
        IReadOnlyList<IHealthCheck> checks,
        bool isFix,
        bool isYes,
        bool isDryRun,
        bool isJson)
    {
        var projectName = Path.GetFileName(projectDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrEmpty(projectName)) projectName = "workspace";

        var context = new CheckContext(projectDirectory);
        var runner = new CheckRunner(checks);
        var report = await runner.RunAllAsync(context);

        if (isJson)
        {
            Console.WriteLine(JsonRenderer.Render(report, projectName));
            return report.ExitCode;
        }

        ConsoleRenderer.RenderHeader(projectName, "0.4.0");
        ConsoleRenderer.RenderReport(report);

        var fixableExecutions = report.Executions.Where(e => e.Result.Fix is not null).ToList();
        if (fixableExecutions.Count == 0) return report.ExitCode;

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold blue]{fixableExecutions.Count} auto-fixable issue(s) available.[/]");

        if (isDryRun)
        {
            AnsiConsole.MarkupLine("[bold yellow]=== Dry Run (No machine mutation will occur) ===[/]");
            foreach (var execution in fixableExecutions)
            {
                var fix = execution.Result.Fix!;
                AnsiConsole.MarkupLine($"[cyan bold]Check:[/] {execution.Check.Id}");
                AnsiConsole.MarkupLine($"[grey]{Markup.Escape(fix.Describe())}[/]");
                AnsiConsole.WriteLine(fix.DryRun());
            }
            return report.ExitCode;
        }

        if (isFix)
        {
            foreach (var execution in fixableExecutions)
            {
                var fix = execution.Result.Fix!;
                AnsiConsole.MarkupLine($"\n[bold yellow]Target:[/] {execution.Check.Id} - {fix.Describe()}");

                bool shouldApply = isYes || AnsiConsole.Confirm("Apply this fix safely (backup journal will be created)?", defaultValue: false);
                if (shouldApply)
                {
                    AnsiConsole.MarkupLine("[yellow]Applying fix and persisting rollback journal...[/]");
                    await fix.ApplyAsync();
                    AnsiConsole.MarkupLine("[green bold][[SUCCESS]][/] Fix applied. Rollback snapshot saved to ~/.puvi-doctor/sessions");
                }
                else
                {
                    AnsiConsole.MarkupLine("[grey]Skipped by user.[/]");
                }
            }
        }
        else
        {
            AnsiConsole.MarkupLine("[grey]Run with [yellow]--dry-run[/] to preview diff, or [green]--fix[/] to apply safely.[/]");
        }

        return report.ExitCode;
    }

    private static async Task<int> ExecuteRollbackAsync(IRegistryService registryService)
    {
        var latest = await FixJournalStore.GetLatestSessionAsync();
        if (latest is null || latest.BeforeUserPathSnapshot.RawValue is null)
        {
            AnsiConsole.MarkupLine("[red bold][[ERROR]][/] No prior fix session found to rollback.");
            return 1;
        }

        AnsiConsole.MarkupLine($"[bold yellow]Found rollback session:[/] {latest.SessionId} ({latest.TimestampUtc:yyyy-MM-dd HH:mm:ss} UTC)");
        AnsiConsole.MarkupLine($"[grey]Original Action:[/] {latest.Description}");
        AnsiConsole.MarkupLine($"[grey]Restoring Value:[/] {latest.BeforeUserPathSnapshot.RawValue}");

        var confirm = AnsiConsole.Confirm("Are you sure you want to rollback to this exact state?", defaultValue: true);
        if (!confirm)
        {
            AnsiConsole.MarkupLine("[grey]Rollback aborted.[/]");
            return 0;
        }

        registryService.SetUserPath(latest.BeforeUserPathSnapshot.RawValue, latest.BeforeUserPathSnapshot.Kind);
        AnsiConsole.MarkupLine("[green bold][[SUCCESS]][/] Machine state restored byte-for-byte to snapshot before session.");
        return 0;
    }
}