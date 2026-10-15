using System.CommandLine;
using PuviDoctor.Cli.Rendering;
using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Checks.Path;
using PuviDoctor.Core.Checks.Versions;
using PuviDoctor.Core.Platform;
using PuviDoctor.Core.Reporting;

namespace PuviDoctor.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // 1. Setup production platform services
        var envService = new SystemEnvironmentService();
        var processRunner = new SystemProcessRunner();
        var fileSystem = new SystemFileSystem();

        // 2. Register all checks
        var pathChecks = new IHealthCheck[]
        {
            new DeadPathCheck(envService),
            new DuplicatePathCheck(envService),
            new ShadowingCheck(envService, processRunner)
        };

        var versionChecks = new IHealthCheck[]
        {
            new NodeVersionCheck(fileSystem, processRunner),
            new DotNetVersionCheck(fileSystem, processRunner),
            new GitVersionCheck(processRunner)
        };

        var allChecks = pathChecks.Concat(versionChecks).ToArray();

        // 3. Define System.CommandLine options and commands
        var pathOption = new Option<DirectoryInfo?>(
            aliases: ["--path", "-p"],
            description: "Target directory of the repo to evaluate",
            getDefaultValue: () => new DirectoryInfo(Directory.GetCurrentDirectory())
        );

        var rootCommand = new RootCommand("puvi-doctor: Developer machine and project diagnostics CLI");
        rootCommand.AddGlobalOption(pathOption);

        // Root action: Run all checks
        rootCommand.SetHandler(async (DirectoryInfo? targetDir) =>
        {
            var dir = targetDir?.FullName ?? Directory.GetCurrentDirectory();
            Environment.ExitCode = await ExecuteDoctorAsync(dir, allChecks);
        }, pathOption);

        // Subcommand: puvi-doctor path
        var pathCommand = new Command("path", "Run PATH sanity checks only (dead entries, duplicates, shadowing)");
        pathCommand.SetHandler(async (DirectoryInfo? targetDir) =>
        {
            var dir = targetDir?.FullName ?? Directory.GetCurrentDirectory();
            Environment.ExitCode = await ExecuteDoctorAsync(dir, pathChecks);
        }, pathOption);
        rootCommand.Add(pathCommand);

        // Subcommand: puvi-doctor versions
        var versionsCommand = new Command("versions", "Run runtime version checks only (.NET, Node, Git)");
        versionsCommand.SetHandler(async (DirectoryInfo? targetDir) =>
        {
            var dir = targetDir?.FullName ?? Directory.GetCurrentDirectory();
            Environment.ExitCode = await ExecuteDoctorAsync(dir, versionChecks);
        }, pathOption);
        rootCommand.Add(versionsCommand);

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

    private static async Task<int> ExecuteDoctorAsync(string projectDirectory, IReadOnlyList<IHealthCheck> checks)
    {
        var projectName = System.IO.Path.GetFileName(projectDirectory.TrimEnd(
            System.IO.Path.DirectorySeparatorChar,
            System.IO.Path.AltDirectorySeparatorChar));

        if (string.IsNullOrEmpty(projectName))
        {
            projectName = "workspace";
        }

        ConsoleRenderer.RenderHeader(projectName);

        var context = new CheckContext(projectDirectory);
        var runner = new CheckRunner(checks);

        var report = await runner.RunAllAsync(context);

        ConsoleRenderer.RenderReport(report);

        return report.ExitCode;
    }
}