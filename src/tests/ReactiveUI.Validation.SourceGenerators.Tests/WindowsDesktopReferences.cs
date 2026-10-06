// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Resolves the explicitly restored WinForms reference pack from NuGet's actual package folders.</summary>
internal static class WindowsDesktopReferences
{
    /// <summary>The locked SDK's Windows desktop reference-pack version.</summary>
    private const string PackVersion = "10.0.12";

    /// <summary>Reads the exact download identity and restored cache paths, including NuGet's default cache.</summary>
    /// <param name="assetsPath">An optional captured restore graph for a test-owned cache control.</param>
    /// <returns>The actual managed Windows desktop reference assemblies.</returns>
    /// <exception cref="InvalidOperationException">The exact pack was not explicitly restored.</exception>
    internal static ImmutableArray<MetadataReference> Read(string? assetsPath = null)
    {
        assetsPath ??= Path.Combine(AppContext.BaseDirectory, "CompilerHostInputs", "Platforms.assets.json");
        using var restored = JsonDocument.Parse(File.ReadAllText(assetsPath));
        var root = restored.RootElement;
        if (!root.GetProperty("project").GetProperty("frameworks").GetProperty("net10.0")
            .GetProperty("downloadDependencies").EnumerateArray().Any(static download =>
                download.GetProperty("name").GetString() == "Microsoft.WindowsDesktop.App.Ref"
                && download.GetProperty("version").GetString() == $"[{PackVersion}, {PackVersion}]"))
        {
            throw new InvalidOperationException("Restore PlatformProducerInputs with the pinned Microsoft.WindowsDesktop.App.Ref download before running WinForms compiler tests.");
        }

        var directory = root.GetProperty("packageFolders").EnumerateObject()
            .Select(static folder => Path.Combine(folder.Name, "microsoft.windowsdesktop.app.ref", PackVersion, "ref", "net10.0"))
            .FirstOrDefault(Directory.Exists)
            ?? throw new InvalidOperationException("The explicitly restored Windows desktop reference pack is absent from its recorded NuGet package folders.");
        var paths = CompilerTestReferences.ManagedFiles(directory);
        if (!paths.Any(static path => Path.GetFileName(path) == "System.Windows.Forms.dll"))
        {
            throw new InvalidOperationException("The restored Windows desktop pack has no managed WinForms reference assembly.");
        }

        return [.. paths.Select(static path => MetadataReference.CreateFromFile(path))];
    }
}
