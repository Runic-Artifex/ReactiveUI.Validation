// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using ReactiveUI.Validation.SourceGenerators;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Contains final compiler, generator, analyzer, and executable behavior evidence.</summary>
/// <param name="Compilation">The final compilation after the combined generator round.</param>
/// <param name="GeneratorDiagnostics">The generator diagnostics.</param>
/// <param name="GeneratedSources">Every source emitted by every participating producer.</param>
/// <param name="CompilationDiagnostics">Final compiler diagnostics after actual packaged suppressors.</param>
/// <param name="AnalyzerOptions">The original run's retained analyzer input and configuration snapshot.</param>
internal sealed record CapabilityCompilation(
    CSharpCompilation Compilation,
    ImmutableArray<Diagnostic> GeneratorDiagnostics,
    ImmutableArray<CapabilityGeneratedSource> GeneratedSources,
    ImmutableArray<Diagnostic> CompilationDiagnostics,
    AnalyzerOptions? AnalyzerOptions = null)
{
    /// <summary>Gets raw compiler diagnostics before packaged suppressors and SDK reference-unification defaults.</summary>
    internal ImmutableArray<Diagnostic> RawCompilationDiagnostics => Compilation
        .WithOptions(Compilation.Options.WithSpecificDiagnosticOptions(ImmutableDictionary<string, ReportDiagnostic>.Empty)).GetDiagnostics();

    /// <summary>Gets every Validation-owned emitted file.</summary>
    internal ImmutableArray<CapabilityGeneratedSource> ValidationSources => [.. GeneratedSources.Where(static source => source.Generator.Contains("ValidationGenerator", StringComparison.Ordinal))];

    /// <summary>Runs the final normal-call dispatch analyzer on the complete combined output.</summary>
    /// <returns>The final dispatch diagnostics.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Task<ImmutableArray<Diagnostic>> GetDispatchDiagnosticsAsync() => Compilation
        .WithAnalyzers([new ValidationDispatchAnalyzer()], AnalyzerOptions ?? new AnalyzerOptions([])).GetAnalyzerDiagnosticsAsync();

    /// <summary>Executes a fixed trusted fixture in its own process-lifetime managed consumer context.</summary>
    /// <param name="typeName">The fixture entry point type.</param>
    /// <param name="methodName">The public static parameterless Boolean entry point.</param>
    /// <returns>The real consumer behavior result.</returns>
    /// <exception cref="InvalidOperationException">The fixture fails emission or lacks its entry point.</exception>
    [SuppressMessage("Security", "SES1402", Justification = "Only trusted fixture sources and pinned runtime references are emitted and loaded by this test-only host.")]
    internal bool ExecuteBoolean(string typeName, string methodName)
    {
        using var stream = new MemoryStream();
        var emit = Compilation.Emit(stream);
        if (!emit.Success)
        {
            throw new InvalidOperationException(string.Join("\n", emit.Diagnostics));
        }

        var loader = new ExecutionLoadContext();
        stream.Position = 0;
        var assembly = loader.LoadFromStream(stream);
        var method = assembly.GetType(typeName, throwOnError: true)!.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Fixture entry point is absent.");
        return (bool)method.Invoke(null, null)!;
    }

    /// <summary>Isolates same-name edited consumers while sharing trusted runtime identities until process exit.</summary>
    private sealed class ExecutionLoadContext() : AssemblyLoadContext(isCollectible: false)
    {
        /// <inheritdoc />
        protected override Assembly? Load(AssemblyName assemblyName) => Default.LoadFromAssemblyName(assemblyName);
    }
}
