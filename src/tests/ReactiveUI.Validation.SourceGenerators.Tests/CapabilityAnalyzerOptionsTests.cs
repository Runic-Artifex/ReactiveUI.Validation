// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Checks that final analyzer inputs preserve the actual generator run configuration.</summary>
public sealed class CapabilityAnalyzerOptionsTests
{
    /// <summary>The actual AOT build property passed to generation and final analysis.</summary>
    private const string AotKey = "build_property.PublishAot";

    /// <summary>The additional-file metadata key whose snapshot must survive caller mutation.</summary>
    private const string FileKey = "build_metadata.AdditionalFiles.Marker";

    /// <summary>Preserves earlier per-run global and file inputs across caller mutations and driver edits.</summary>
    /// <param name="reactive">Whether to use the System.Reactive runtime.</param>
    /// <returns>The asynchronous assertions.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task EarlierCompilationKeepsItsAnalyzerConfiguration(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var globals = new Dictionary<string, string> { [AotKey] = "true" };
        var metadata = new Dictionary<string, string> { [FileKey] = "original" };
        var input = new ConfigurationInput();
        var files = new Dictionary<string, IReadOnlyDictionary<string, string>> { [input.Path] = metadata };
        const string source = "internal sealed class ConfigurationMarker { }";
        var first = await host.RunAsync(source, additionalTexts: [input], globalOptions: globals, additionalFileOptions: files);
        globals[AotKey] = "false";
        metadata[FileKey] = "edited";
        var second = await host.RunAsync(source, additionalTexts: [input], globalOptions: globals, additionalFileOptions: files);
        var firstOptions = first.AnalyzerOptions!;
        var secondOptions = second.AnalyzerOptions!;
        await Assert.That(firstOptions.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue(AotKey, out var firstAot)).IsTrue();
        await Assert.That(firstAot).IsEqualTo("true");
        await Assert.That(secondOptions.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue(AotKey, out var secondAot)).IsTrue();
        await Assert.That(secondAot).IsEqualTo("false");
        await Assert.That(firstOptions.AnalyzerConfigOptionsProvider.GetOptions(input).TryGetValue(FileKey, out var firstFile)).IsTrue();
        await Assert.That(firstFile).IsEqualTo("original");
        await Assert.That(secondOptions.AnalyzerConfigOptionsProvider.GetOptions(input).TryGetValue(FileKey, out var secondFile)).IsTrue();
        await Assert.That(secondFile).IsEqualTo("edited");
        await Assert.That(firstOptions.AdditionalFiles).Contains(input);
        await Assert.That(await first.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(await second.GetDispatchDiagnosticsAsync()).IsEmpty();
    }

    /// <summary>The immutable additional input retained by each analyzer run.</summary>
    private sealed class ConfigurationInput : AdditionalText
    {
        /// <inheritdoc />
        public override string Path => "Configuration.txt";

        /// <inheritdoc />
        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From("configuration");
    }
}
