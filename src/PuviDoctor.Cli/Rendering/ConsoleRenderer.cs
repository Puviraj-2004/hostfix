using PuviDoctor.Core.Models;
using Spectre.Console;

namespace PuviDoctor.Cli.Rendering;

/// <summary>
/// Renders the health report to the console using Spectre.Console with structured markup.
/// Matches the specification output contract.
/// </summary>
public static class ConsoleRenderer
{
    public static void RenderHeader(string projectName, string version = "1.0.0")
    {
        AnsiConsole.MarkupLine($"[bold blue]hostfix v{version}[/] | project: [yellow]{Markup.Escape(projectName)}[/]");
        AnsiConsole.WriteLine();
    }
    public static void RenderReport(HealthReport report)
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