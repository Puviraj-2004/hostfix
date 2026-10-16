using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Fixes;
using PuviDoctor.Core.Models;
using IOPath = System.IO.Path;

namespace PuviDoctor.Core.Checks.Hygiene;

public sealed class GitIgnoreCheck(IFileSystem fileSystem) : IHealthCheck
{
    public string Id => "hygiene.gitignore";

    public async Task<CheckResult> RunAsync(CheckContext context, CancellationToken cancellationToken = default)
    {
        var envExample = IOPath.Combine(context.ProjectRootDirectory, ".env.example");
        var packageJson = IOPath.Combine(context.ProjectRootDirectory, "package.json");
        if (!fileSystem.FileExists(envExample) && !fileSystem.FileExists(packageJson))
            return new(CheckStatus.Skip, "Git ignore check skipped: no supported project manifest found.");

        var path = IOPath.Combine(context.ProjectRootDirectory, ".gitignore");
        var before = fileSystem.FileExists(path) ? await fileSystem.ReadAllTextAsync(path, cancellationToken) : null;
        var required = new[] { ".env", ".env.local", ".hostfix/" };
        var missing = required.Where(entry => before is null || !before.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(entry, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (missing.Length == 0) return new(CheckStatus.Ok, "Sensitive local files and Hostfix repair journals are protected by .gitignore.");

        var after = (before ?? string.Empty).TrimEnd() + Environment.NewLine + string.Join(Environment.NewLine, missing) + Environment.NewLine;
        return new(CheckStatus.Warn, $".gitignore is missing {missing.Length} local protection rule(s).", "Hostfix can add safe ignore rules for local environment files and repair journals.", new ProjectFileFix(Id, path, before, after, "Protect local environment files and Hostfix repair journals in .gitignore."));
    }
}
