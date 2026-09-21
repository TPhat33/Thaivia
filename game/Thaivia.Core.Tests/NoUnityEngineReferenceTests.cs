using System.IO;
using System.Linq;
using Xunit;

namespace Thaivia.Core.Tests;

/// <summary>
/// Belt-and-suspenders check on top of the two structural guarantees
/// (Thaivia.Core.csproj cannot resolve UnityEngine at all; the asmdef
/// sets noEngineReferences): scans the actual committed source tree so a
/// future edit that somehow slips a `using UnityEngine;` into
/// Assets/Scripts/Core is caught by `dotnet test`, not only discovered
/// when a human eventually opens the Unity Editor.
/// </summary>
public class NoUnityEngineReferenceTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new DirectoryNotFoundException("Could not locate repo root (AGENTS.md) above " + AppContext.BaseDirectory);
        }

        return dir.FullName;
    }

    [Fact]
    public void CoreSourceTree_ContainsNoUnityEngineUsing()
    {
        var repoRoot = FindRepoRoot();
        var coreDir = Path.Combine(repoRoot, "game", "Assets", "Scripts", "Core");
        Assert.True(Directory.Exists(coreDir), $"expected {coreDir} to exist");

        // Doc comments in this project legitimately mention "UnityEngine"
        // by name in prose (explaining why it is NOT referenced), so this
        // checks specifically for an actual `using UnityEngine...;`
        // directive -- the one construct that would create a real compile
        // dependency -- not the bare word. (A fully-qualified
        // `UnityEngine.Foo` reference with no `using` would also fail to
        // resolve, since there is no UnityEngine assembly anywhere in
        // this project's dependency graph -- `dotnet build` already
        // proves that on every run.)
        var usingDirective = new System.Text.RegularExpressions.Regex(@"(?m)^\s*using\s+UnityEngine(\.\w+)*\s*;");

        var offenders = Directory.EnumerateFiles(coreDir, "*.cs", SearchOption.AllDirectories)
            .Select(path => (path, text: File.ReadAllText(path)))
            .Where(f => usingDirective.IsMatch(f.text))
            .Select(f => f.path)
            .ToList();

        Assert.True(offenders.Count == 0, "Files under Assets/Scripts/Core referencing UnityEngine: " + string.Join(", ", offenders));
    }

    [Fact]
    public void RuntimeSourceTree_IsMarkedAsUncompiledUnityCode()
    {
        var repoRoot = FindRepoRoot();
        var runtimeDir = Path.Combine(repoRoot, "game", "Assets", "Scripts", "Runtime");
        if (!Directory.Exists(runtimeDir))
        {
            return; // nothing written yet is fine; this test guards files that DO exist.
        }

        foreach (var path in Directory.EnumerateFiles(runtimeDir, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(path);
            Assert.Contains("UNCOMPILED", text.ToUpperInvariant());
        }
    }
}
