using System.Text;
using PuviDoctor.Core.Models;

namespace PuviDoctor.Cli.Rendering;

/// <summary>
/// Generates GitHub-flavored Markdown health reports for client handovers and repository documentation.
/// </summary>
public static class MarkdownRenderer
{
    public static string Render(HealthReport report, string projectName)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"# Workspace Health Report (hostfix): {projectName}");
        sb.AppendLine();
        sb.AppendLine($"- **Generated At:** {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"- **Hostfix Version:** v1.0.0");
        sb.AppendLine($"- **Health Score:** `{report.Score}/{report.MaxScore}`");
        sb.AppendLine($"- **Exit Code:** `{report.ExitCode}`");
        sb.AppendLine();

        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine("| Status | Count |");
        sb.AppendLine("| :--- | :--- |");
        sb.AppendLine($"| Passed (OK) | {report.Executions.Count(e => e.Result.Status == CheckStatus.Ok)} |");
        sb.AppendLine($"| Warnings | {report.Executions.Count(e => e.Result.Status == CheckStatus.Warn)} |");
        sb.AppendLine($"| Failures | {report.Executions.Count(e => e.Result.Status == CheckStatus.Fail)} |");
        sb.AppendLine($"| Skipped | {report.Executions.Count(e => e.Result.Status == CheckStatus.Skip)} |");
        sb.AppendLine();

        sb.AppendLine("## Detailed Findings");
        sb.AppendLine();
        sb.AppendLine("| Check ID | Status | Message | Suggested Fix |");
        sb.AppendLine("| :--- | :--- | :--- | :--- |");

        foreach (var exec in report.Executions)
        {
            var statusBadge = exec.Result.Status switch
            {
                CheckStatus.Ok => "PASS",
                CheckStatus.Warn => "WARN",
                CheckStatus.Fail => "FAIL",
                CheckStatus.Skip => "SKIP",
                _ => "INFO"
            };

            var cleanMessage = EscapeMarkdown(exec.Result.Message);
            var fixText = exec.Result.Fix != null
                ? $"`{EscapeMarkdown(exec.Result.Fix.Describe())}`"
                : exec.Result.Details != null
                    ? EscapeMarkdown(exec.Result.Details.Replace(Environment.NewLine, " "))
                    : "None";

            sb.AppendLine($"| `{exec.Check.Id}` | **{statusBadge}** | {cleanMessage} | {fixText} |");
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("*Generated automatically by [hostfix](https://github.com/Puviraj-2004/hostfix)*");

        return sb.ToString();
    }

    private static string EscapeMarkdown(string text)
    {
        return text.Replace("|", "\\|").Trim();
    }
}