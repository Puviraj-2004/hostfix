using PuviDoctor.Core.Models;
using PuviDoctor.Core.Reporting;
using Spectre.Console;

namespace PuviDoctor.Cli.Rendering;

/// <summary>
/// Renders the health report to the console using Spectre.Console with structured markup.
/// Matches the specification output contract.
/// </summary>
public static class ConsoleRenderer
{
    public static void RenderHeader(string projectName, string? version = null)
    {
        version ??= AppInfo.Version;
        AnsiConsole.MarkupLine($"[bold blue]hostfix v{version}[/] | project: [yellow]{Markup.Escape(projectName)}[/]");
        AnsiConsole.WriteLine();
    }

    public static void RenderProjectProfile(ProjectProfile profile)
    {
        AnsiConsole.MarkupLine("[bold underline]Project profile[/]");

        if (!profile.IsDetected)
        {
            AnsiConsole.MarkupLine("  [grey][[INFO]][/] No supported project manifest found in this folder.");
            AnsiConsole.WriteLine();
            return;
        }

        AnsiConsole.MarkupLine($"  [cyan]Detected:[/] {Markup.Escape(string.Join(" + ", profile.Types))}");
        if (!string.IsNullOrWhiteSpace(profile.PackageManager))
        {
            AnsiConsole.MarkupLine($"  [cyan]Package manager:[/] {Markup.Escape(profile.PackageManager)}");
        }

        if (profile.RuntimeRequirements.Count > 0)
        {
            AnsiConsole.MarkupLine($"  [cyan]Required runtime:[/] {Markup.Escape(string.Join(" | ", profile.RuntimeRequirements))}");
        }

        if (profile.AvailableCommands.Count > 0)
        {
            AnsiConsole.MarkupLine($"  [cyan]Detected commands:[/] {Markup.Escape(string.Join(", ", profile.AvailableCommands))}");
        }

        AnsiConsole.WriteLine();
    }
    public static void RenderReport(HealthReport report)
    {
        var failures = report.Executions.Where(e => e.Result.Status == CheckStatus.Fail).ToList();
        var fixNow = report.Executions.Where(e => e.Result.Status == CheckStatus.Warn && e.Result.Fix?.CanApply() == true).ToList();
        var review = report.Executions.Where(e => e.Result.Status == CheckStatus.Warn && e.Result.Fix?.CanApply() != true).ToList();
        var healthy = report.Executions.Where(e => e.Result.Status == CheckStatus.Ok).ToList();

        var status = failures.Count > 0 ? "BLOCKED" : fixNow.Count + review.Count > 0 ? "AT RISK" : "READY";
        var color = failures.Count > 0 ? "red" : fixNow.Count + review.Count > 0 ? "yellow" : "green";
        AnsiConsole.MarkupLine($"[bold {color}]PROJECT STATUS: {status}[/]");
        AnsiConsole.WriteLine();

        RenderPriorityGroup("BLOCKING", failures, "red");
        RenderPriorityGroup("FIX NOW", fixNow, "yellow");
        RenderPriorityGroup("REVIEW", review, "yellow");
        RenderPriorityGroup("HEALTHY", healthy, "green");
    }

    public static void RenderMachineReport(HealthReport report)
    {
        // Group checks by category (e.g. "Path" or "Version")
        var grouped = report.Executions.GroupBy(e => GetCategory(e.Check.Id));

        foreach (var group in grouped)
        {
            AnsiConsole.MarkupLine($"[bold underline]{group.Key}[/]");

            foreach (var execution in group)
            {
                RenderExecution(execution);
            }

            AnsiConsole.WriteLine();
        }

        // Summary footer
        var scoreColor = report.Score switch
        {
            >= 8 => "green",
            >= 5 => "yellow",
            _ => "red"
        };

        var passCount = report.Executions.Count(e => e.Result.Status == CheckStatus.Ok);
        var warnCount = report.Executions.Count(e => e.Result.Status == CheckStatus.Warn);
        var failCount = report.Executions.Count(e => e.Result.Status == CheckStatus.Fail);

        AnsiConsole.MarkupLine(
            $"Score: [{scoreColor} bold]{report.Score}/{report.MaxScore}[/] | " +
            $"[green]{passCount} passed[/], [yellow]{warnCount} warnings[/], [red]{failCount} failures[/] | " +
            $"exit code [bold]{report.ExitCode}[/]"
        );
    }

    public static void RenderVerification(VerificationReport report)
    {
        AnsiConsole.MarkupLine(report.Passed ? "[bold green]PROJECT STATUS: READY[/]" : "[bold red]PROJECT STATUS: BLOCKED[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold underline]VERIFICATION[/]");
        foreach (var step in report.Steps)
        {
            var tag = step.Passed ? "[[OK]]" : "[[FAIL]]";
            var color = step.Passed ? "green" : "red";
            AnsiConsole.MarkupLine($"  [{color} bold]{tag,-8}[/] {Markup.Escape(step.Name)}");
            if (!string.IsNullOrWhiteSpace(step.Detail)) AnsiConsole.MarkupLine($"         [grey]{Markup.Escape(step.Detail)}[/]");
        }
        if (report.Steps.Count == 0) AnsiConsole.MarkupLine("  [yellow][[REVIEW]][/] No supported build or verification commands were detected.");
    }

    private static void RenderPriorityGroup(string title, IReadOnlyList<CheckExecutionResult> executions, string color)
    {
        if (executions.Count == 0) return;
        AnsiConsole.MarkupLine($"[bold underline]{title}[/]");
        foreach (var execution in executions) RenderExecution(execution);
        AnsiConsole.WriteLine();
    }

    private static void RenderExecution(CheckExecutionResult execution)
    {
        var result = execution.Result;

        // Note: Spectre.Console requires double brackets [[ ]] to output literal '[' and ']'
        var (statusTag, color) = result.Status switch
        {
            CheckStatus.Ok => ("[[OK]]", "green"),
            CheckStatus.Warn => ("[[WARN]]", "yellow"),
            CheckStatus.Fail => ("[[FAIL]]", "red"),
            CheckStatus.Skip => ("[[SKIP]]", "grey"),
            _ => ("[[UNKNOWN]]", "white")
        };

        AnsiConsole.MarkupLine($"  [{color} bold]{statusTag,-8}[/] {Markup.Escape(result.Message)}");

        if (!string.IsNullOrWhiteSpace(result.Details))
        {
            var lines = result.Details.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                AnsiConsole.MarkupLine($"         [grey]{Markup.Escape(line)}[/]");
            }
        }
    }

    private static string GetCategory(string checkId)
    {
        var parts = checkId.Split('.');
        return parts.Length > 0 ? char.ToUpperInvariant(parts[0][0]) + parts[0][1..] : "General";
    }
}
