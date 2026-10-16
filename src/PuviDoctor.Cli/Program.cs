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
            new TrackedEnvironmentFileCheck(fileSystem, processRunner),
            new GitIgnoreCheck(fileSystem),
            new PortCollisionCheck(networkService)
        };

        var projectChecks = versionChecks
            .Concat(dependencyChecks)
            .Concat(hygieneChecks)
            .ToArray();

        var pathOption = new Option<DirectoryInfo?>(
            aliases: ["--path", "-p"],
            description: "Target directory of the repo to evaluate",
            getDefaultValue: () => new DirectoryInfo(Directory.GetCurrentDirectory())
        );

        var fixOption = new Option<bool>(name: "--fix", description: "Apply safe fixes for identified issues");
        var yesOption = new Option<bool>(name: "--yes", description: "Automatically confirm all fixes without prompts");
        var dryRunOption = new Option<bool>(name: "--dry-run", description: "Print exact before/after diff of proposed fixes");
        var jsonOption = new Option<bool>(name: "--json", description: "Produce machine-readable JSON output (schema: 1)");

        var rootCommand = new RootCommand("hostfix: Project health and repair CLI");
        rootCommand.AddGlobalOption(pathOption);
        rootCommand.AddOption(fixOption);
        rootCommand.AddOption(yesOption);
        rootCommand.AddOption(dryRunOption);
        rootCommand.AddOption(jsonOption);

        rootCommand.SetHandler(async (DirectoryInfo? targetDir, bool isFix, bool isYes, bool isDryRun, bool isJson) =>
        {
            var dir = targetDir?.FullName ?? Directory.GetCurrentDirectory();
            Environment.ExitCode = await ExecuteDoctorFlowAsync(dir, projectChecks, fileSystem, isFix, isYes, isDryRun, isJson, showProjectProfile: true, machineMode: false);
        }, pathOption, fixOption, yesOption, dryRunOption, jsonOption);

        var rollbackCommand = new Command("rollback", "Undo the last applied fix session from backup journal");
        rollbackCommand.SetHandler(async (DirectoryInfo? targetDir) =>
        {
            var dir = targetDir?.FullName ?? Directory.GetCurrentDirectory();
            Environment.ExitCode = await ExecuteRollbackAsync(dir, registryService);
        }, pathOption);
        rootCommand.Add(rollbackCommand);

        var machineCommand = new Command("machine", "Optional machine diagnostics and safe User PATH cleanup");
        machineCommand.AddAlias("path"); // Backward compatible name for v1 users.
        var machineFixOption = new Option<bool>(name: "--fix", description: "Apply safe User PATH fixes");
        var machineYesOption = new Option<bool>(name: "--yes", description: "Automatically confirm all safe User PATH fixes");
        var machineDryRunOption = new Option<bool>(name: "--dry-run", description: "Preview exact User PATH changes without modifying the machine");
        var machineJsonOption = new Option<bool>(name: "--json", description: "Produce machine-readable JSON output (schema: 1)");
        machineCommand.AddOption(machineFixOption);
        machineCommand.AddOption(machineYesOption);
        machineCommand.AddOption(machineDryRunOption);
        machineCommand.AddOption(machineJsonOption);
        machineCommand.SetHandler(async (DirectoryInfo? targetDir, bool isFix, bool isYes, bool isDryRun, bool isJson) =>
        {
            var dir = targetDir?.FullName ?? Directory.GetCurrentDirectory();
            Environment.ExitCode = await ExecuteDoctorFlowAsync(dir, pathChecks, fileSystem, isFix, isYes, isDryRun, isJson, showProjectProfile: false, machineMode: true);
        }, pathOption, machineFixOption, machineYesOption, machineDryRunOption, machineJsonOption);
        rootCommand.Add(machineCommand);

        var repairCommand = new Command("repair", "Apply every safe, project-owned repair and re-check the project");
        var repairDryRunOption = new Option<bool>(name: "--dry-run", description: "Preview every project file change without modifying files");
        var deepRepairOption = new Option<bool>(name: "--deep", description: "Create an isolated Git repair branch, attempt safe dependency updates, then verify it");
        repairCommand.AddOption(repairDryRunOption);
        repairCommand.AddOption(deepRepairOption);
        repairCommand.SetHandler(async (DirectoryInfo? targetDir, bool isDryRun, bool deep) =>
        {
            var dir = targetDir?.FullName ?? Directory.GetCurrentDirectory();
            if (deep)
            {
                var result = await new DeepRepairService(fileSystem, processRunner).RunAsync(dir);
                AnsiConsole.MarkupLine(result.Started ? "[bold blue]DEEP REPAIR[/]" : "[bold red]DEEP REPAIR BLOCKED[/]");
                AnsiConsole.MarkupLine(Markup.Escape(result.Message));
                if (result.BranchName is not null) AnsiConsole.MarkupLine($"Branch: [yellow]{Markup.Escape(result.BranchName)}[/]");
                if (result.Verification is not null) ConsoleRenderer.RenderVerification(result.Verification);
                Environment.ExitCode = result.Started && result.Verification?.Passed == true ? 0 : 2;
                return;
            }
            Environment.ExitCode = await ExecuteDoctorFlowAsync(dir, projectChecks, fileSystem, true, true, isDryRun, false, showProjectProfile: true, machineMode: false);
        }, pathOption, repairDryRunOption, deepRepairOption);
        rootCommand.Add(repairCommand);

        var verifyCommand = new Command("verify", "Restore dependencies and prove the detected build, lint, typecheck, and test commands pass");
        verifyCommand.SetHandler(async (DirectoryInfo? targetDir) =>
        {
            var dir = targetDir?.FullName ?? Directory.GetCurrentDirectory();
            var name = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            ConsoleRenderer.RenderHeader(string.IsNullOrEmpty(name) ? "workspace" : name, AppInfo.Version);
            ConsoleRenderer.RenderProjectProfile(await new ProjectDetector(fileSystem).DetectAsync(dir));
            var verification = await new ProjectVerifier(fileSystem, processRunner).VerifyAsync(dir);
            ConsoleRenderer.RenderVerification(verification);
            Environment.ExitCode = verification.Passed ? 0 : 2;
        }, pathOption);
        rootCommand.Add(verifyCommand);

        var versionsCommand = new Command("versions", "Run runtime version checks only (.NET, Node, Git)");
        versionsCommand.SetHandler(async (DirectoryInfo? targetDir) =>
        {
            var dir = targetDir?.FullName ?? Directory.GetCurrentDirectory();
            Environment.ExitCode = await ExecuteDoctorFlowAsync(dir, versionChecks, fileSystem, false, false, false, false, showProjectProfile: true, machineMode: false);
        }, pathOption);
        rootCommand.Add(versionsCommand);

        var depsCommand = new Command("deps", "Run dependency vulnerability & lockfile triage only");
        depsCommand.SetHandler(async (DirectoryInfo? targetDir) =>
        {
            var dir = targetDir?.FullName ?? Directory.GetCurrentDirectory();
            Environment.ExitCode = await ExecuteDoctorFlowAsync(dir, dependencyChecks, fileSystem, false, false, false, false, showProjectProfile: true, machineMode: false);
        }, pathOption);
        rootCommand.Add(depsCommand);

        var reportCommand = new Command("report", "Generate exportable diagnostic reports");
        var mdOption = new Option<bool>(name: "--md", description: "Export report as markdown file (HOSTFIX_REPORT.md)");
        reportCommand.AddOption(mdOption);
        reportCommand.SetHandler(async (DirectoryInfo? targetDir, bool exportMd) =>
        {
            var dir = targetDir?.FullName ?? Directory.GetCurrentDirectory();
            Environment.ExitCode = await ExecuteReportCommandAsync(dir, projectChecks, exportMd);
        }, pathOption, mdOption);
        rootCommand.Add(reportCommand);

        var ciCommand = new Command("ci", "CI mode: evaluates against .hostfix.baseline.json; fails only on new regressions");
        var generateBaselineOption = new Option<bool>(name: "--generate-baseline", description: "Record current issues as accepted baseline");
        ciCommand.AddOption(generateBaselineOption);
        ciCommand.SetHandler(async (DirectoryInfo? targetDir, bool genBaseline) =>
        {
            var dir = targetDir?.FullName ?? Directory.GetCurrentDirectory();
            Environment.ExitCode = await ExecuteCiCommandAsync(dir, projectChecks, fileSystem, processRunner, genBaseline);
        }, pathOption, generateBaselineOption);
        rootCommand.Add(ciCommand);

        var onboardCommand = new Command("onboard", "One-command developer setup: runs diagnostics, applies safe fixes, restores dependencies");
        onboardCommand.SetHandler(async (DirectoryInfo? targetDir) =>
        {
            var dir = targetDir?.FullName ?? Directory.GetCurrentDirectory();
            Environment.ExitCode = await ExecuteOnboardCommandAsync(dir, projectChecks, processRunner, fileSystem);
        }, pathOption);
        rootCommand.Add(onboardCommand);

        try
        {
            return await rootCommand.InvokeAsync(args);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"hostfix internal failure: {ex.Message}");
            Console.ResetColor();
            return 3;
        }
    }

    private static async Task<int> ExecuteCiCommandAsync(
        string projectDirectory,
        IReadOnlyList<IHealthCheck> checks,
        IFileSystem fileSystem,
        IProcessRunner processRunner,
        bool generateBaseline)
    {
        var context = new CheckContext(projectDirectory);
        var runner = new CheckRunner(checks);
        var report = await runner.RunAllAsync(context);

        if (generateBaseline)
        {
            await BaselineStore.SaveBaselineAsync(projectDirectory, report, fileSystem);
            AnsiConsole.MarkupLine("[green bold][[SUCCESS]][/] Generated accepted baseline at [yellow].hostfix.baseline.json[/]");
            return 0;
        }

        var baseline = await BaselineStore.LoadBaselineAsync(projectDirectory, fileSystem);
        if (baseline is null)
        {
            AnsiConsole.MarkupLine("[yellow bold][[CI]][/] No baseline found. Evaluating in strict mode.");
            ConsoleRenderer.RenderReport(report);
            return report.ExitCode;
        }

        var newRegressions = report.Executions
            .Where(e => e.Result.Status is CheckStatus.Warn or CheckStatus.Fail)
            .Where(e => !baseline.AcceptedFindings.Any(b => b.CheckId == e.Check.Id && b.Message == e.Result.Message))
            .ToList();

        if (newRegressions.Count == 0)
        {
            var verification = await new ProjectVerifier(fileSystem, processRunner).VerifyAsync(projectDirectory);
            if (!verification.Passed)
            {
                AnsiConsole.MarkupLine("[red bold][[CI FAIL]][/] Project verification failed.");
                ConsoleRenderer.RenderVerification(verification);
                return 2;
            }
            AnsiConsole.MarkupLine($"[green bold][[CI PASS]][/] All findings match accepted baseline ({baseline.AcceptedFindings.Count} accepted items) and project verification passed.");
            return 0;
        }

        AnsiConsole.MarkupLine($"[red bold][[CI FAIL]][/] {newRegressions.Count} new regression(s) introduced beyond baseline!");
        foreach (var reg in newRegressions)
        {
            AnsiConsole.MarkupLine($"  [red bold]Regression:[/] {Markup.Escape($"[{reg.Check.Id}] {reg.Result.Message}")}");
        }

        return 2;
    }

    private static async Task<int> ExecuteOnboardCommandAsync(
        string projectDirectory,
        IReadOnlyList<IHealthCheck> checks,
        IProcessRunner processRunner,
        IFileSystem fileSystem)
    {
        AnsiConsole.MarkupLine("[bold blue]hostfix onboard:[/] Preparing developer workspace...");
        AnsiConsole.WriteLine();

        var context = new CheckContext(projectDirectory);
        var runner = new CheckRunner(checks);
        var report = await runner.RunAllAsync(context);

        ConsoleRenderer.RenderReport(report);

        if (fileSystem.FileExists(Path.Combine(projectDirectory, "package.json")))
        {
            AnsiConsole.MarkupLine("\n[yellow]Restoring npm packages via 'npm ci'...[/]");
            var npmResult = await processRunner.ExecuteAsync("npm", ["ci"]);
            if (npmResult.ExitCode == 0)
            {
                AnsiConsole.MarkupLine("[green bold][[OK]][/] npm packages installed cleanly.");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]npm ci failed; attempting 'npm install'...[/]");
                await processRunner.ExecuteAsync("npm", ["install"]);
            }
        }

        if (fileSystem.FileExists(Path.Combine(projectDirectory, "PuviDoctor.slnx")) ||
            fileSystem.FileExists(Path.Combine(projectDirectory, "PuviDoctor.sln")) ||
            Directory.GetFiles(projectDirectory, "*.csproj").Length > 0)
        {
            AnsiConsole.MarkupLine("[yellow]Restoring .NET packages via 'dotnet restore'...[/]");
            var dotnetResult = await processRunner.ExecuteAsync("dotnet", ["restore"]);
            if (dotnetResult.ExitCode == 0)
            {
                AnsiConsole.MarkupLine("[green bold][[OK]][/] .NET packages restored cleanly.");
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(report.ExitCode == 0
            ? "[green bold]=== MACHINE READY: DAY 1 WORKSPACE READY TO BUILD & RUN ===[/]"
            : "[yellow bold]=== WORKSPACE SETUP COMPLETE WITH NOTICES: Review issues above before starting ===[/]");

        return report.ExitCode;
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
            var outputPath = Path.Combine(projectDirectory, "HOSTFIX_REPORT.md");
            await File.WriteAllTextAsync(outputPath, mdContent);
            AnsiConsole.MarkupLine($"[green bold][[SUCCESS]][/] Markdown handover report generated: [yellow]{outputPath}[/]");
            return 0;
        }

        ConsoleRenderer.RenderHeader(projectName, AppInfo.Version);
        ConsoleRenderer.RenderReport(report);
        return report.ExitCode;
    }

    private static async Task<int> ExecuteDoctorFlowAsync(
        string projectDirectory,
        IReadOnlyList<IHealthCheck> checks,
        IFileSystem fileSystem,
        bool isFix,
        bool isYes,
        bool isDryRun,
        bool isJson,
        bool showProjectProfile,
        bool machineMode)
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

        ConsoleRenderer.RenderHeader(projectName, AppInfo.Version);
        if (showProjectProfile)
        {
            var profile = await new ProjectDetector(fileSystem).DetectAsync(projectDirectory);
            ConsoleRenderer.RenderProjectProfile(profile);
        }
        if (machineMode) ConsoleRenderer.RenderMachineReport(report);
        else ConsoleRenderer.RenderReport(report);

        // Deduplicate fixes by their concrete Type to prevent double-applying the same operation
        var fixableExecutions = report.Executions
            .Where(e => e.Result.Fix is not null && e.Result.Fix.CanApply());
        if (machineMode)
        {
            fixableExecutions = fixableExecutions.GroupBy(e => e.Result.Fix!.GetType()).Select(g => g.First());
        }
        var fixableList = fixableExecutions.ToList();

        if (fixableList.Count == 0)
        {
            if (report.Executions.Any(e => e.Result.Fix is not null))
            {
                AnsiConsole.MarkupLine(machineMode
                    ? "[grey]No safe User PATH changes are available. Remaining PATH findings may require manual System PATH administration.[/]"
                    : "[grey]No safe automatic project repairs are available for these findings.[/]");
            }
            return report.ExitCode;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold blue]{fixableList.Count} safe repair(s) available.[/]");

        if (isDryRun)
        {
            AnsiConsole.MarkupLine(machineMode
                ? "[bold yellow]=== Dry Run (No machine mutation will occur) ===[/]"
                : "[bold yellow]=== Dry Run (No project files will be modified) ===[/]");
            foreach (var execution in fixableList)
            {
                var fix = execution.Result.Fix!;
                AnsiConsole.MarkupLine($"[cyan bold]Check:[/] {Markup.Escape(execution.Check.Id)}");
                AnsiConsole.MarkupLine($"[grey]{Markup.Escape(fix.Describe())}[/]");
                AnsiConsole.WriteLine(fix.DryRun());
            }
            return report.ExitCode;
        }

        if (isFix)
        {
            if (!machineMode)
            {
                var projectFixes = fixableList.Select(e => e.Result.Fix).OfType<PuviDoctor.Core.Fixes.ProjectFileFix>().ToList();
                if (projectFixes.Count > 0)
                {
                    await PuviDoctor.Core.Fixes.ProjectRepairService.ApplyAsync(projectDirectory, projectFixes);
                    AnsiConsole.MarkupLine("[green bold][[REPAIRED]][/] Safe project repairs applied. Rechecking project state...");
                    var verifiedReport = await new CheckRunner(checks).RunAllAsync(context);
                    ConsoleRenderer.RenderReport(verifiedReport);
                    return verifiedReport.ExitCode;
                }
            }

            foreach (var execution in fixableList)
            {
                var fix = execution.Result.Fix!;
                AnsiConsole.MarkupLine($"\n[bold yellow]Target:[/] {Markup.Escape(execution.Check.Id)} - {Markup.Escape(fix.Describe())}");

                bool shouldApply = isYes || AnsiConsole.Confirm("Apply this fix safely (backup journal will be created)?", defaultValue: false);
                if (shouldApply)
                {
                    AnsiConsole.MarkupLine("[yellow]Applying fix and persisting rollback journal...[/]");
                    await fix.ApplyAsync();
                    AnsiConsole.MarkupLine("[green bold][[SUCCESS]][/] Fix applied. Rollback snapshot saved to ~/.hostfix/sessions");
                }
                else
                {
                    AnsiConsole.MarkupLine("[grey]Skipped by user.[/]");
                }
            }
        }
        else
        {
            AnsiConsole.MarkupLine(machineMode
                ? "[grey]Run with [yellow]machine --dry-run[/] to preview diff, or [green]machine --fix[/] to apply safely.[/]"
                : "[grey]Run [green]hostfix repair[/] to apply every safe project repair, or [yellow]hostfix repair --dry-run[/] to preview changes.[/]");
        }

        return report.ExitCode;
    }

    private static async Task<int> ExecuteRollbackAsync(string projectDirectory, IRegistryService registryService)
    {
        if (await PuviDoctor.Core.Fixes.ProjectRepairService.RollbackLatestAsync(projectDirectory))
        {
            AnsiConsole.MarkupLine("[green bold][[ROLLED BACK]][/] Restored the latest project repair session.");
            return 0;
        }

        var latest = await FixJournalStore.GetLatestSessionAsync();
        if (latest is null || latest.BeforeUserPathSnapshot.RawValue is null)
        {
            AnsiConsole.MarkupLine("[red bold][[ERROR]][/] No prior fix session found to rollback.");
            return 1;
        }

        AnsiConsole.MarkupLine($"[bold yellow]Found rollback session:[/] {latest.SessionId} ({latest.TimestampUtc:yyyy-MM-dd HH:mm:ss} UTC)");
        AnsiConsole.MarkupLine($"[grey]Original Action:[/] {Markup.Escape(latest.Description)}");
        AnsiConsole.MarkupLine($"[grey]Restoring Value:[/] {Markup.Escape(latest.BeforeUserPathSnapshot.RawValue)}");

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
