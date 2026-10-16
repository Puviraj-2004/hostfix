using PuviDoctor.Core.Reporting;
using PuviDoctor.Tests.Fakes;
using Shouldly;
using Xunit;

namespace PuviDoctor.Tests.Reporting;

public sealed class ProjectDetectorTests
{
    [Fact]
    public async Task DetectAsync_NextProjectWithNpmLock_ReportsTechnologyRuntimeAndScripts()
    {
        var root = Path.Combine(Path.GetTempPath(), "hostfix-profile-next");
        var files = new FakeFileSystem();
        files.AddFile(Path.Combine(root, "package.json"), """
        {
          "engines": { "node": ">=20.9.0" },
          "dependencies": { "next": "15.0.0", "react": "19.0.0" },
          "scripts": { "build": "next build", "lint": "next lint", "dev": "next dev" }
        }
        """);
        files.AddFile(Path.Combine(root, "package-lock.json"), "{}");

        var profile = await new ProjectDetector(files).DetectAsync(root);

        profile.Types.ShouldContain("Next.js");
        profile.Types.ShouldContain("Node.js");
        profile.PackageManager.ShouldBe("npm");
        profile.RuntimeRequirements.ShouldContain("Node.js >=20.9.0");
        profile.AvailableCommands.ShouldContain("npm run build");
        profile.AvailableCommands.ShouldContain("npm run lint");
        profile.AvailableCommands.ShouldContain("npm run dev");
    }

    [Fact]
    public async Task DetectAsync_DotNetProjectWithGlobalJson_ReportsDotNetRuntime()
    {
        var root = Path.Combine(Path.GetTempPath(), "hostfix-profile-dotnet");
        var files = new FakeFileSystem();
        files.AddFile(Path.Combine(root, "global.json"), """{ "sdk": { "version": "9.0.100" } }""");
        files.AddFile(Path.Combine(root, "App.csproj"), "<Project />");

        var profile = await new ProjectDetector(files).DetectAsync(root);

        profile.Types.ShouldContain(".NET");
        profile.RuntimeRequirements.ShouldContain(".NET SDK 9.0.100");
    }

    [Fact]
    public async Task DetectAsync_NodeRuntimeFileWithoutPackageManifest_StillDetectsNode()
    {
        var root = Path.Combine(Path.GetTempPath(), "hostfix-profile-runtime");
        var files = new FakeFileSystem();
        files.AddFile(Path.Combine(root, ".nvmrc"), "20.18.0");

        var profile = await new ProjectDetector(files).DetectAsync(root);

        profile.Types.ShouldContain("Node.js");
        profile.RuntimeRequirements.ShouldContain("Node.js 20.18.0");
    }
}
