// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Selects actual managed compiler inputs without admitting native runtime libraries.</summary>
internal static class CompilerTestReferences
{
    /// <summary>Gets the current host's path identity comparison.</summary>
    private static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>Gets the current host's path identity comparer.</summary>
    private static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>Gets the host-selected framework assemblies, excluding application and opposite-flavor dependencies.</summary>
    /// <returns>The actual managed assemblies in the current runtime directory.</returns>
    internal static ImmutableArray<string> RuntimePaths()
    {
        var directory = Path.TrimEndingDirectorySeparator(RuntimeEnvironment.GetRuntimeDirectory());
        return [.. TrustedPaths().Where(path => Path.GetDirectoryName(path)!.Equals(directory, PathComparison))];
    }

    /// <summary>Creates the mixed reference graph used by original compiler fixtures.</summary>
    /// <returns>Managed host and output references, with no native PE inputs.</returns>
    internal static ImmutableArray<MetadataReference> CreateDefault() =>
        [.. TrustedPaths().Concat(ManagedFiles(AppContext.BaseDirectory)).Distinct(PathComparer)
            .Order(PathComparer).Select(static path => MetadataReference.CreateFromFile(path))];

    /// <summary>Selects only DLLs containing CLI assembly metadata.</summary>
    /// <param name="directory">The actual output or reference-pack directory.</param>
    /// <returns>The managed assembly paths in deterministic order.</returns>
    internal static ImmutableArray<string> ManagedFiles(string directory) =>
        [.. Directory.EnumerateFiles(directory, "*.dll").Where(IsManagedAssembly).Order(PathComparer)];

    /// <summary>Reads the runtime's managed assembly list rather than enumerating its native DLLs.</summary>
    /// <returns>The host-selected managed assembly paths.</returns>
    /// <exception cref="InvalidOperationException">The managed test host provides no trusted assembly list.</exception>
    private static string[] TrustedPaths() =>
        AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string paths
            ? paths.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            : throw new InvalidOperationException("The compiler tests require the managed host's trusted platform assembly list.");

    /// <summary>Distinguishes managed assemblies from native PE or ELF libraries without loading them.</summary>
    /// <param name="path">The candidate actual library.</param>
    /// <returns>Whether the file has CLI assembly metadata.</returns>
    private static bool IsManagedAssembly(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[4];
        stream.ReadExactly(header);
        if (header.SequenceEqual("\u007fELF"u8))
        {
            return false;
        }

        stream.Position = 0;
        using var reader = new PEReader(stream);
        return reader.HasMetadata && reader.GetMetadataReader().IsAssembly;
    }
}
