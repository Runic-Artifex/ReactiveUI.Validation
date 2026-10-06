// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Runs the same producer fixture subsequently used by the actual-package verification corpus.</summary>
public sealed class ProducerInteropCompilerTests
{
    /// <summary>Executes real field, partial, command, collection and OAPH implementations in either producer order.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <param name="validationFirst">Whether Validation precedes the actual producer generators.</param>
    /// <returns>The asynchronous compiler and runtime verification.</returns>
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task ActualProducerLifecycleUsesSharedPackageScenario(bool reactive, bool validationFirst)
    {
        using var host = CapabilityCompilerHost.Create(reactive, validationFirst);
        var result = await host.RunAsync(Source(reactive), path: "ProducerInteropScenario.cs");
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)
            .Select(static diagnostic => $"{diagnostic}\n{diagnostic.Location.SourceTree?.GetText().Lines[diagnostic.Location.GetLineSpan().StartLinePosition.Line]}")).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean("ProducerInteropEntry", "Check")).IsTrue();
        await Assert.That(result.ValidationSources.Any(static generated => generated.HintName == "ValidationProducerContracts.g.cs")).IsTrue();
        await Assert.That(result.Compilation.SyntaxTrees.Any(static tree => tree.FilePath == "Validation.PrivateProducerProjection.cs")).IsFalse();
    }

    /// <summary>Loads the unchanged shared fixture and initializes only its selected runtime graph.</summary>
    /// <param name="reactive">Whether the fixture selects System.Reactive.</param>
    /// <returns>The real caller source submitted to all actual generators.</returns>
    private static string Source(bool reactive) =>
        $$"""
            {{(reactive ? "#define REACTIVE_SHIM\n" : string.Empty)}}using {{(reactive ? "ReactiveUI.Reactive.Builder" : "ReactiveUI.Builder")}};
            {{File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ProducerInteropScenario.cs"))}}
            public static class ProducerInteropEntry
            {
                public static bool Check()
                {
                    RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
                    ProducerInteropScenario.Run();
                    return true;
                }
            }
        """;
}
