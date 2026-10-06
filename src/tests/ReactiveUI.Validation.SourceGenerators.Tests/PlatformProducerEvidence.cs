// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Retains bounded actual-producer snapshots with an invocation-specific success record.</summary>
internal static class PlatformProducerEvidence
{
    /// <summary>The shared evidence serialization format.</summary>
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    /// <summary>Writes original inputs, complete actual output, and selected byte identities after assertions pass.</summary>
    /// <param name="profile">The explicit test case and compiler/provider scope.</param>
    /// <param name="source">The original C# input.</param>
    /// <param name="markup">The original platform additional input.</param>
    /// <param name="host">The participating pinned producers.</param>
    /// <param name="result">The final combined generator result.</param>
    internal static void Write(string profile, string source, string markup, CapabilityCompilerHost host, CapabilityCompilation result)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "TestResults", "PlatformProducerEvidence", $"{profile}-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "Original.cs"), source);
        File.WriteAllText(Path.Combine(directory, "Original.xaml"), markup);
        for (var index = 0; index < result.GeneratedSources.Length; index++)
        {
            var output = result.GeneratedSources[index];
            File.WriteAllText(Path.Combine(directory, $"{index:D2}-{Path.GetFileName(output.HintName)}"), output.Text);
        }

        var record = new
        {
            completed = true,
            scope = "Actual platform producer and framework-metadata compiler proof",
            typedProviderAndAdapterCompiled = source.Contains("CreateAdapter", StringComparison.Ordinal),
            managedPeEmitted = true,
            uiHostExecuted = false,
            nativeHostExecuted = false,
            profile,
            roslynVersion = typeof(CSharpCompilation).Assembly.GetName().Version!.ToString(),
            sourceSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source))),
            xamlSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(markup))),
            producers = host.ProducerInputs,
            generated = result.GeneratedSources.Select(static output => new { output.Generator, output.HintName, output.Sha256 }),
        };
        File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(record, SerializerOptions));
    }
}
