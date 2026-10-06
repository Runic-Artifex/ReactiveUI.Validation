// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Collections.Immutable;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Validates actual platform package and extracted compiler-input bytes.</summary>
/// <param name="References">The real platform framework metadata.</param>
/// <param name="Producers">The real platform generator assemblies.</param>
internal sealed record PlatformProducerInputs(ImmutableArray<MetadataReference> References, ImmutableArray<CapabilityProducerInput> Producers)
{
    /// <summary>The reviewed package identity property.</summary>
    private const string IdentityProperty = "identity";

    /// <summary>The reviewed package and selected asset path property.</summary>
    private const string PathProperty = "path";

    /// <summary>The signature-independent NuGet content hash property.</summary>
    private const string ContentHashProperty = "sha512";

    /// <summary>Reads and verifies pinned NuGet archives before selecting their compiler assets.</summary>
    /// <param name="inputDirectory">An explicit test-owned input directory, or the captured compiler inputs.</param>
    /// <returns>The verified platform input graph.</returns>
    /// <exception cref="InvalidOperationException">A restored graph or byte differs from its reviewed pin.</exception>
    internal static PlatformProducerInputs Read(string? inputDirectory = null)
    {
        inputDirectory ??= Path.Combine(AppContext.BaseDirectory, "CompilerHostInputs");
        using var restored = JsonDocument.Parse(File.ReadAllText(Path.Combine(inputDirectory, "Platforms.assets.json")));
        using var pinned = JsonDocument.Parse(File.ReadAllText(Path.Combine(inputDirectory, "Platforms.immutable-inputs.json")));
        var folders = restored.RootElement.GetProperty("packageFolders").EnumerateObject().Select(static folder => folder.Name).ToArray();
        var libraries = restored.RootElement.GetProperty("libraries");
        var references = ImmutableArray.CreateBuilder<MetadataReference>();
        var producers = ImmutableArray.CreateBuilder<CapabilityProducerInput>();
        foreach (var package in pinned.RootElement.GetProperty("packages").EnumerateArray())
        {
            VerifyRestoredPackage(package, restored.RootElement, libraries);
            var packagePath = package.GetProperty(PathProperty).GetString()!;
            var directory = folders.Select(folder => Path.Combine(folder, packagePath)).First(Directory.Exists);
            var archiveBytes = VerifyArchive(package, directory);
            using var archive = new ZipArchive(new MemoryStream(archiveBytes), ZipArchiveMode.Read);
            AddVerifiedAssets(package, directory, archive, references, producers);
        }

        return new(references.ToImmutable(), producers.ToImmutable());
    }

    /// <summary>Checks the restored library graph and the explicitly downloaded generator identity.</summary>
    /// <param name="package">The reviewed package pin.</param>
    /// <param name="restored">The restored asset document.</param>
    /// <param name="libraries">The restored package libraries.</param>
    /// <exception cref="InvalidOperationException">The restored package identity differs.</exception>
    private static void VerifyRestoredPackage(JsonElement package, JsonElement restored, JsonElement libraries)
    {
        var identity = package.GetProperty(IdentityProperty).GetString()!;
        var downloadOnly = identity.StartsWith("Microsoft.Maui.Controls.SourceGen/", StringComparison.Ordinal);
        if (downloadOnly && !restored.GetProperty("project").GetProperty("frameworks").GetProperty("net10.0")
            .GetProperty("downloadDependencies").EnumerateArray().Any(static download =>
                download.GetProperty("name").GetString() == "Microsoft.Maui.Controls.SourceGen"
                && download.GetProperty("version").GetString() == "[10.0.110, 10.0.110]"))
        {
            throw new InvalidOperationException("The restored graph does not select the reviewed MAUI generator download.");
        }

        if (!downloadOnly && (!libraries.TryGetProperty(identity, out var library)
            || library.GetProperty(PathProperty).GetString() != package.GetProperty(PathProperty).GetString()
            || library.GetProperty(ContentHashProperty).GetString() != package.GetProperty(ContentHashProperty).GetString()))
        {
            throw new InvalidOperationException($"Platform restored package differs from the reviewed pin: {identity}.");
        }
    }

    /// <summary>Checks raw archive hashes separately from NuGet's signature-independent content hash.</summary>
    /// <param name="package">The reviewed package pin.</param>
    /// <param name="directory">The selected package cache directory.</param>
    /// <returns>The verified raw archive bytes.</returns>
    /// <exception cref="InvalidOperationException">The archive or NuGet content metadata differs.</exception>
    private static byte[] VerifyArchive(JsonElement package, string directory)
    {
        var split = package.GetProperty(PathProperty).GetString()!.Split('/');
        var archivePath = Path.Combine(directory, $"{split[0]}.{split[1]}.nupkg");
        var archiveBytes = File.ReadAllBytes(archivePath);
        VerifyHash(archiveBytes, package.GetProperty("sha256").GetString()!, archivePath);
        var identity = package.GetProperty(IdentityProperty).GetString()!;
        if (Convert.ToBase64String(SHA512.HashData(archiveBytes)) != package.GetProperty("archiveSha512").GetString())
        {
            throw new InvalidOperationException($"Platform archive differs from its reviewed raw SHA-512: {identity}.");
        }

        using var packageMetadata = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, ".nupkg.metadata")));
        if (packageMetadata.RootElement.GetProperty("contentHash").GetString() != package.GetProperty(ContentHashProperty).GetString())
        {
            throw new InvalidOperationException($"Platform package content metadata differs from its NuGet pin: {identity}.");
        }

        return archiveBytes;
    }

    /// <summary>Verifies extracted DLLs against both their reviewed hashes and their archive entries.</summary>
    /// <param name="package">The reviewed package pin.</param>
    /// <param name="directory">The selected cache directory.</param>
    /// <param name="archive">The verified package archive.</param>
    /// <param name="references">The selected framework metadata.</param>
    /// <param name="producers">The selected actual generators.</param>
    /// <exception cref="InvalidOperationException">A selected asset is absent or differs.</exception>
    private static void AddVerifiedAssets(
        JsonElement package,
        string directory,
        ZipArchive archive,
        ImmutableArray<MetadataReference>.Builder references,
        ImmutableArray<CapabilityProducerInput>.Builder producers)
    {
        foreach (var asset in package.GetProperty("assets").EnumerateArray())
        {
            var relativePath = asset.GetProperty(PathProperty).GetString()!;
            var expectedHash = asset.GetProperty("sha256").GetString()!;
            var path = Path.GetFullPath(Path.Combine(directory, relativePath));
            VerifyHash(File.ReadAllBytes(path), expectedHash, path);
            var entry = archive.GetEntry(relativePath)
                ?? throw new InvalidOperationException($"Pinned platform asset is absent from its archive: {relativePath}.");
            using var extracted = new MemoryStream();
            using (var entryStream = entry.Open())
            {
                entryStream.CopyTo(extracted);
            }

            VerifyHash(extracted.ToArray(), expectedHash, relativePath);
            if (asset.GetProperty("kind").GetString() == "generator")
            {
                producers.Add(new(package.GetProperty(IdentityProperty).GetString()!, path, expectedHash));
            }
            else
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
        }
    }

    /// <summary>Rejects mutable cache bytes independently of the archive and restored graph.</summary>
    /// <param name="bytes">The selected file bytes.</param>
    /// <param name="expected">The reviewed SHA-256 hash.</param>
    /// <param name="path">The diagnostic input identity.</param>
    /// <exception cref="InvalidOperationException">The byte hash differs.</exception>
    private static void VerifyHash(byte[] bytes, string expected, string path)
    {
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != expected)
        {
            throw new InvalidOperationException($"Platform compiler input bytes differ from the reviewed pin: {path}.");
        }
    }
}
