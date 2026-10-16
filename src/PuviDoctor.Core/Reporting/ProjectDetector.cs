using System.Text.Json;
using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Models;

namespace PuviDoctor.Core.Reporting;

/// <summary>
/// Detects project technologies and runnable scripts from project-owned files only.
/// It never inspects global machine configuration.
/// </summary>
public sealed class ProjectDetector
{
    private readonly IFileSystem _fileSystem;

    public ProjectDetector(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public async Task<ProjectProfile> DetectAsync(string projectRoot, CancellationToken cancellationToken = default)
    {
        var types = new List<string>();
        var runtimes = new List<string>();
        var commands = new List<string>();
        string? packageManager = null;

        var packageJsonPath = Path.Combine(projectRoot, "package.json");
        if (_fileSystem.FileExists(packageJsonPath))
        {
            types.Add("Node.js");
            packageManager = DetectNodePackageManager(projectRoot);

            try
            {
                using var document = JsonDocument.Parse(await _fileSystem.ReadAllTextAsync(packageJsonPath, cancellationToken));
                var root = document.RootElement;

                if (HasPackage(root, "next")) types.Insert(0, "Next.js");
                else if (HasPackage(root, "vite")) types.Insert(0, "Vite");
                else if (HasPackage(root, "react")) types.Insert(0, "React");

                if (root.TryGetProperty("engines", out var engines) &&
                    engines.TryGetProperty("node", out var node) &&
                    !string.IsNullOrWhiteSpace(node.GetString()))
                {
                    runtimes.Add($"Node.js {node.GetString()!.Trim()}");
                }

                if (root.TryGetProperty("scripts", out var scripts) && scripts.ValueKind == JsonValueKind.Object)
                {
                    foreach (var name in new[] { "build", "test", "lint", "typecheck", "dev", "start" })
                    {
                        if (scripts.TryGetProperty(name, out _)) commands.Add($"{packageManager ?? "npm"} run {name}");
                    }
                }
            }
            catch (JsonException)
            {
                // The health checks will report project configuration failures separately.
            }
        }

        // A runtime file is itself evidence of a Node.js project, even when the
        // manifest lives in a nested workspace package.
        foreach (var runtimeFile in new[] { ".nvmrc", ".node-version" })
        {
            var runtimePath = Path.Combine(projectRoot, runtimeFile);
            if (_fileSystem.FileExists(runtimePath))
            {
                types.Add("Node.js");
                var version = (await _fileSystem.ReadAllTextAsync(runtimePath, cancellationToken)).Trim();
                if (!string.IsNullOrWhiteSpace(version) && !runtimes.Any(r => r.StartsWith("Node.js ", StringComparison.OrdinalIgnoreCase)))
                {
                    runtimes.Add($"Node.js {version}");
                }
                break;
            }
        }

        if (_fileSystem.FileExists(Path.Combine(projectRoot, "global.json")) ||
            _fileSystem.GetFiles(projectRoot, "*.csproj").Any() ||
            _fileSystem.GetFiles(projectRoot, "*.sln").Any() ||
            _fileSystem.GetFiles(projectRoot, "*.slnx").Any())
        {
            types.Add(".NET");
            var globalJsonPath = Path.Combine(projectRoot, "global.json");
            if (_fileSystem.FileExists(globalJsonPath))
            {
                try
                {
                    using var document = JsonDocument.Parse(await _fileSystem.ReadAllTextAsync(globalJsonPath, cancellationToken));
                    if (document.RootElement.TryGetProperty("sdk", out var sdk) &&
                        sdk.TryGetProperty("version", out var version) && !string.IsNullOrWhiteSpace(version.GetString()))
                    {
                        runtimes.Add($".NET SDK {version.GetString()!.Trim()}");
                    }
                }
                catch (JsonException) { }
            }
        }

        if (_fileSystem.FileExists(Path.Combine(projectRoot, "pyproject.toml")) ||
            _fileSystem.FileExists(Path.Combine(projectRoot, "requirements.txt")))
        {
            types.Add("Python");
            packageManager ??= "pip";
        }

        if (_fileSystem.FileExists(Path.Combine(projectRoot, "pom.xml")) ||
            _fileSystem.FileExists(Path.Combine(projectRoot, "build.gradle")) ||
            _fileSystem.FileExists(Path.Combine(projectRoot, "build.gradle.kts")))
        {
            types.Add("Java");
            packageManager ??= _fileSystem.FileExists(Path.Combine(projectRoot, "pom.xml")) ? "Maven" : "Gradle";
        }

        return new ProjectProfile(types.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), packageManager, runtimes, commands);
    }

    private string DetectNodePackageManager(string projectRoot)
    {
        if (_fileSystem.FileExists(Path.Combine(projectRoot, "pnpm-lock.yaml"))) return "pnpm";
        if (_fileSystem.FileExists(Path.Combine(projectRoot, "yarn.lock"))) return "yarn";
        if (_fileSystem.FileExists(Path.Combine(projectRoot, "package-lock.json"))) return "npm";
        return "npm";
    }

    private static bool HasPackage(JsonElement root, string packageName) =>
        HasPackageIn(root, "dependencies", packageName) || HasPackageIn(root, "devDependencies", packageName);

    private static bool HasPackageIn(JsonElement root, string section, string packageName) =>
        root.TryGetProperty(section, out var packages) && packages.ValueKind == JsonValueKind.Object && packages.TryGetProperty(packageName, out _);
}
