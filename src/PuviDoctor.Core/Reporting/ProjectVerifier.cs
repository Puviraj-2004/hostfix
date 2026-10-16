using System.Text.Json;
using PuviDoctor.Core.Abstractions;

namespace PuviDoctor.Core.Reporting;

public sealed record VerificationStep(string Name, bool Passed, string? Detail);
public sealed record VerificationReport(IReadOnlyList<VerificationStep> Steps)
{
    public bool Passed => Steps.All(step => step.Passed);
}

/// <summary>Runs only declared project validation commands from the target project directory.</summary>
public sealed class ProjectVerifier(IFileSystem fileSystem, IProcessRunner processRunner)
{
    public async Task<VerificationReport> VerifyAsync(
        string projectRoot,
        Action<string>? onStepStarted = null,
        Action<VerificationStep>? onStepCompleted = null,
        CancellationToken cancellationToken = default)
    {
        var steps = new List<VerificationStep>();
        var packageJson = Path.Combine(projectRoot, "package.json");
        if (fileSystem.FileExists(packageJson))
        {
            var scripts = await GetScriptsAsync(packageJson, cancellationToken);
            if (fileSystem.FileExists(Path.Combine(projectRoot, "package-lock.json")))
                steps.Add(await RunAsync("Dependencies (npm ci)", "npm", ["ci"], projectRoot, onStepStarted, onStepCompleted, cancellationToken));
            foreach (var script in new[] { "build", "lint", "typecheck", "test" }.Where(scripts.Contains))
                steps.Add(await RunAsync($"{char.ToUpperInvariant(script[0])}{script[1..]}", "npm", ["run", script], projectRoot, onStepStarted, onStepCompleted, cancellationToken));
        }

        var dotnetProject = fileSystem.FileExists(Path.Combine(projectRoot, "global.json")) ||
            fileSystem.GetFiles(projectRoot, "*.sln").Any() || fileSystem.GetFiles(projectRoot, "*.slnx").Any() || fileSystem.GetFiles(projectRoot, "*.csproj").Any();
        if (dotnetProject)
        {
            steps.Add(await RunAsync("Dependencies (dotnet restore)", "dotnet", ["restore"], projectRoot, onStepStarted, onStepCompleted, cancellationToken));
            steps.Add(await RunAsync("Build", "dotnet", ["build", "--no-restore"], projectRoot, onStepStarted, onStepCompleted, cancellationToken));
            steps.Add(await RunAsync("Tests", "dotnet", ["test", "--no-build", "--no-restore"], projectRoot, onStepStarted, onStepCompleted, cancellationToken));
        }

        return new VerificationReport(steps);
    }

    private async Task<HashSet<string>> GetScriptsAsync(string packageJson, CancellationToken cancellationToken)
    {
        try
        {
            using var doc = JsonDocument.Parse(await fileSystem.ReadAllTextAsync(packageJson, cancellationToken));
            return doc.RootElement.TryGetProperty("scripts", out var scripts) && scripts.ValueKind == JsonValueKind.Object
                ? scripts.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase)
                : new(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException) { return new(StringComparer.OrdinalIgnoreCase); }
    }

    private async Task<VerificationStep> RunAsync(
        string name,
        string command,
        IReadOnlyList<string> arguments,
        string directory,
        Action<string>? onStepStarted,
        Action<VerificationStep>? onStepCompleted,
        CancellationToken cancellationToken)
    {
        onStepStarted?.Invoke(name);
        VerificationStep step;
        try
        {
            var output = await processRunner.ExecuteInDirectoryAsync(command, arguments, directory, cancellationToken);
            if (output.ExitCode == 0) step = new(name, true, null);
            else
            {
                var detail = string.IsNullOrWhiteSpace(output.StandardError) ? output.StandardOutput : output.StandardError;
                step = new(name, false, Trim(detail));
            }
        }
        catch (Exception ex) { step = new(name, false, ex.Message); }

        onStepCompleted?.Invoke(step);
        return step;
    }

    private static string Trim(string value) => value.Length <= 600 ? value.Trim() : value[..600].Trim() + "...";
}
