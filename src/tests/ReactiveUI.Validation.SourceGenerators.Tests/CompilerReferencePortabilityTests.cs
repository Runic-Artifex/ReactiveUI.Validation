// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Exercises real native libraries and recorded NuGet cache paths on each compiler-test host.</summary>
public sealed class CompilerReferencePortabilityTests
{
    /// <summary>The package-folder property recorded by NuGet restore.</summary>
    private const string FoldersProperty = "packageFolders";

    /// <summary>Excludes actual native runtime bytes while retaining a real managed core assembly for compilation.</summary>
    /// <returns>The asynchronous reference and compiler assertions.</returns>
    [Test]
    public async Task NativeRuntimeLibrariesNeverBecomeCompilerReferences()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"CompilerReferences{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        try
        {
            var runtime = RuntimeEnvironment.GetRuntimeDirectory();
            File.Copy(Path.Combine(runtime, OperatingSystem.IsWindows() ? "coreclr.dll" : "libcoreclr.so"), Path.Combine(directory, "Native.dll"));
            var managed = Path.Combine(directory, "Managed.dll");
            File.Copy(typeof(object).Assembly.Location, managed);
            var selected = CompilerTestReferences.ManagedFiles(directory);
            await Assert.That(selected).IsEquivalentTo([managed]);
            var framework = CompilerTestReferences.RuntimePaths();
            await Assert.That(framework).Contains(typeof(object).Assembly.Location);
            await Assert.That(framework.Any(static path => Path.GetFileName(path).StartsWith("ReactiveUI", StringComparison.Ordinal))).IsFalse();
            var references = framework.Where(static path => path != typeof(object).Assembly.Location).Append(managed)
                .Select(static path => MetadataReference.CreateFromFile(path));
            var compilation = CSharpCompilation.Create(
                "PortableReferences",
                [CSharpSyntaxTree.ParseText("public class Probe { public object Create() => new object(); }")],
                references,
                new(OutputKind.DynamicallyLinkedLibrary));
            await Assert.That(compilation.GetDiagnostics()).IsEmpty();
            await using var binary = new MemoryStream();
            await Assert.That(compilation.Emit(binary).Success).IsTrue();
            await File.WriteAllTextAsync(Path.Combine(directory, "Broken.dll"), "This is a corrupt intended compiler assembly.");
            await Assert.That(() => CompilerTestReferences.ManagedFiles(directory)).Throws<BadImageFormatException>();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Uses exact restored package folders without consulting the NUGET_PACKAGES environment variable.</summary>
    /// <returns>The asynchronous actual WinForms metadata and wrong-download assertions.</returns>
    [Test]
    public async Task DesktopReferencesFollowRecordedRestoreFolders()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"DesktopReferences{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        try
        {
            var captured = Path.Combine(AppContext.BaseDirectory, "CompilerHostInputs", "Platforms.assets.json");
            var assets = JsonNode.Parse(await File.ReadAllTextAsync(captured))!;
            var folders = assets[FoldersProperty]!.AsObject();
            var recorded = folders.Select(static folder => folder.Key).ToArray();
            assets[FoldersProperty] = new JsonObject { [Path.Combine(directory, "missing-cache")] = new JsonObject() };
            foreach (var folder in recorded)
            {
                assets[FoldersProperty]!.AsObject().Add(folder, new JsonObject());
            }

            var path = Path.Combine(directory, "assets.json");
            await File.WriteAllTextAsync(path, assets.ToJsonString());
            var references = WindowsDesktopReferences.Read(path);
            var compilation = CSharpCompilation.Create("DesktopMetadata", references:
                CompilerTestReferences.RuntimePaths().Select(static item => MetadataReference.CreateFromFile(item)).Concat(references));
            await Assert.That(compilation.GetTypeByMetadataName("System.Windows.Forms.UserControl")).IsNotNull();
            await Assert.That(references.Cast<PortableExecutableReference>().All(reference =>
                Array.Exists(recorded, folder => reference.FilePath!.StartsWith(folder, StringComparison.Ordinal)))).IsTrue();
            var downloads = assets["project"]!["frameworks"]!["net10.0"]!["downloadDependencies"]!.AsArray();
            downloads.Single(static download => download!["name"]!.GetValue<string>() == "Microsoft.WindowsDesktop.App.Ref")!["version"] = "[9.0.0, 9.0.0]";
            await File.WriteAllTextAsync(path, assets.ToJsonString());
            await Assert.That(() => WindowsDesktopReferences.Read(path)).Throws<InvalidOperationException>();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
