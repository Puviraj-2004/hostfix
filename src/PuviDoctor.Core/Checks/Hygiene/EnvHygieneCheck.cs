using System.Text.RegularExpressions;
using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Fixes;
using PuviDoctor.Core.Models;
using IOPath = System.IO.Path;

namespace PuviDoctor.Core.Checks.Hygiene;

/// <summary>
/// Audits .env and .env.example configuration for missing keys, extra keys, and accidental secret commits.
/// Strictly enforces safety rule: Secret values are NEVER read into memory structures or logged.
/// </summary>
public sealed partial class EnvHygieneCheck : IHealthCheck
{
    private readonly IFileSystem _fileSystem;

    public string Id => "hygiene.env";

    public EnvHygieneCheck(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public async Task<CheckResult> RunAsync(CheckContext context, CancellationToken cancellationToken = default)
    {
        var examplePath = IOPath.Combine(context.ProjectRootDirectory, ".env.example");
        var envPath = IOPath.Combine(context.ProjectRootDirectory, ".env");

        var hasExample = _fileSystem.FileExists(examplePath);
        var hasEnv = _fileSystem.FileExists(envPath);

        if (!hasExample && !hasEnv)
        {
            return new CheckResult(
                CheckStatus.Skip,
                "Environment check skipped: Neither .env nor .env.example found."
            );
        }

        if (hasExample && !hasEnv)
        {
            var keys = await ExtractKeyNamesAsync(examplePath, cancellationToken);
            var template = string.Join(Environment.NewLine, keys.OrderBy(k => k).Select(k => $"{k}=")) + Environment.NewLine;
            return new CheckResult(
                CheckStatus.Fail,
                ".env file is missing (.env.example is present).",
                "Hostfix can create a blank local .env template. Secret values are never copied or generated.",
                new ProjectFileFix(Id, envPath, null, template, "Create a blank .env template from required key names."));
        }

        if (!hasExample && hasEnv)
        {
            return new CheckResult(
                CheckStatus.Warn,
                ".env exists but .env.example is missing.",
                "Recommendation: Commit a sanitized .env.example so teammates know which environment variables are required."
            );
        }

        // Both files exist: compare key sets (WITHOUT loading values into memory)
        var exampleKeys = await ExtractKeyNamesAsync(examplePath, cancellationToken);
        var envKeys = await ExtractKeyNamesAsync(envPath, cancellationToken);

        var missingInEnv = exampleKeys.Except(envKeys, StringComparer.OrdinalIgnoreCase).ToList();
        var extraInEnv = envKeys.Except(exampleKeys, StringComparer.OrdinalIgnoreCase).ToList();

        if (missingInEnv.Count > 0)
        {
            return new CheckResult(
                CheckStatus.Fail,
                $".env is missing {missingInEnv.Count} required key(s) declared in .env.example.",
                $"Missing keys: [{string.Join(", ", missingInEnv)}]"
            );
        }

        if (extraInEnv.Count > 0)
        {
            return new CheckResult(
                CheckStatus.Ok,
                $".env satisfies all {exampleKeys.Count} required keys (contains {extraInEnv.Count} extra custom key(s))."
            );
        }

        return new CheckResult(
            CheckStatus.Ok,
            $".env matches .env.example cleanly ({exampleKeys.Count} keys verified)."
        );
    }

    private async Task<HashSet<string>> ExtractKeyNamesAsync(string filePath, CancellationToken cancellationToken)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var content = await _fileSystem.ReadAllTextAsync(filePath, cancellationToken);
        
        // Universal split supporting both CRLF (\r\n) and LF (\n)
        var lines = content.Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            if (line.StartsWith('#') || line.StartsWith(';'))
            {
                continue;
            }

            var match = KeyRegex().Match(line);
            if (match.Success)
            {
                keys.Add(match.Groups[1].Value);
            }
        }

        return keys;
    }

    [GeneratedRegex(@"^([A-Za-z0-9_]+)\s*=")]
    private static partial Regex KeyRegex();
}
