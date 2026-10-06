// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Checks the real packaged producer driver and its flavor-isolated references.</summary>
public sealed class CapabilityCompilerHostTests
{
    /// <summary>The fixed fixture entry type.</summary>
    private const string EntryType = "Model";

    /// <summary>The fixed fixture entry method.</summary>
    private const string EntryMethod = "Check";

    /// <summary>The pinned producer package count.</summary>
    private const int ProducerCount = 2;

    /// <summary>Runs actual Reactive property generation with both combined-driver orders.</summary>
    /// <param name="reactive">Whether to use the System.Reactive runtime.</param>
    /// <param name="validationFirst">Whether Validation appears before the packaged producers.</param>
    /// <returns>The asynchronous assertions.</returns>
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task ActualProducersCompileExecuteAndInvalidate(bool reactive, bool validationFirst)
    {
        using var host = CapabilityCompilerHost.Create(reactive, validationFirst);
        var root = reactive ? "ReactiveUI.Validation.Reactive" : "ReactiveUI.Validation";
        var ui = reactive ? "ReactiveUI.Reactive" : "ReactiveUI";
        var source = $$"""
            using System;
            using {{ui}};
            using ReactiveUI.SourceGenerators;
            using {{root}}.Abstractions;
            using {{root}}.Contexts;
            using {{root}}.Extensions;
            public sealed partial class Model : ReactiveObject, IValidatableViewModel
            {
                [Reactive] public partial string? Name { get; set; }
                public IValidationContext ValidationContext { get; } = new ValidationContext();
                public static bool Check()
                {
                    var model = new Model();
                    using var rule = model.ValidationRule(x => x.Name, x => x == "ok", "bad");
                    var initiallyInvalid = !rule.IsValid;
                    model.Name = "ok";
                    return initiallyInvalid && rule.IsValid;
                }
            }
            """;
        var first = await host.RunAsync(source);
        await Assert.That(string.Join("\n", first.GeneratorDiagnostics)).IsEmpty();
        await Assert.That(string.Join("\n", first.CompilationDiagnostics
            .Where(static diagnostic => diagnostic.Severity is Microsoft.CodeAnalysis.DiagnosticSeverity.Error or Microsoft.CodeAnalysis.DiagnosticSeverity.Warning))).IsEmpty();
        await Assert.That(await first.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(first.ExecuteBoolean(EntryType, EntryMethod)).IsTrue();
        await Assert.That(first.GeneratedSources.Any(static file => file.Text.Contains("partial string? Name", StringComparison.Ordinal))).IsTrue();
        await Assert.That(first.ValidationSources.IsEmpty).IsFalse();
        await Assert.That(host.ProducerInputs.Length).IsEqualTo(ProducerCount);
        await Assert.That(host.ReferencePaths.Any(static path => Path.GetFileName(path) == "System.Reactive.dll")).IsEqualTo(reactive);
        await Assert.That(host.ReferencePaths.Any(path => Path.GetFileName(path) == (reactive ? "ReactiveUI.Validation.dll" : "ReactiveUI.Validation.Reactive.dll"))).IsFalse();

        var edited = await host.RunAsync(source.Replace("Name", "Title", StringComparison.Ordinal));
        await Assert.That(string.Join("\n", edited.GeneratorDiagnostics)).IsEmpty();
        await Assert.That(string.Join("\n", edited.CompilationDiagnostics
            .Where(static diagnostic => diagnostic.Severity is Microsoft.CodeAnalysis.DiagnosticSeverity.Error or Microsoft.CodeAnalysis.DiagnosticSeverity.Warning))).IsEmpty();
        await Assert.That(edited.ExecuteBoolean(EntryType, EntryMethod)).IsTrue();
        await Assert.That(edited.GeneratedSources.Any(static file => file.Text.Contains("partial string? Title", StringComparison.Ordinal))).IsTrue();
        var reverted = await host.RunAsync(source);
        await Assert.That(reverted.ExecuteBoolean(EntryType, EntryMethod)).IsTrue();
        await Assert.That(string.Join("\n", reverted.GeneratedSources.Select(static file => file.Sha256))).IsEqualTo(string.Join("\n", first.GeneratedSources.Select(static file => file.Sha256)));
    }

    /// <summary>Uses the actual packaged forwarding suppressor for generated property attributes.</summary>
    /// <param name="reactive">Whether to use the System.Reactive runtime.</param>
    /// <returns>The asynchronous assertions.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ActualProducerSuppressorsForwardPropertyAttributes(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive, includeValidation: false);
        var ui = reactive ? "ReactiveUI.Reactive" : "ReactiveUI";
        var source = $$"""
            using System.Diagnostics.CodeAnalysis;
            using {{ui}};
            using ReactiveUI.SourceGenerators;
            public sealed partial class Model : ReactiveObject
            {
                [Reactive]
                [property: MaybeNull]
                [property: AllowNull]
                private string? _name;
                public static bool Check()
                {
                    var model = new Model { Name = "ok" };
                    return model.Name == "ok";
                }
            }
            """;
        var result = await host.RunAsync(source);
        await Assert.That(result.RawCompilationDiagnostics.Any(static diagnostic => diagnostic.Id == "CS0657")).IsTrue();
        await Assert.That(string.Join("\n", result.GeneratorDiagnostics)).IsEmpty();
        await Assert.That(string.Join("\n", result.CompilationDiagnostics
            .Where(static diagnostic => diagnostic.Severity is Microsoft.CodeAnalysis.DiagnosticSeverity.Error or Microsoft.CodeAnalysis.DiagnosticSeverity.Warning))).IsEmpty();
        await Assert.That(result.GeneratedSources.Any(static file => file.Text.Contains("MaybeNull", StringComparison.Ordinal)
            && file.Text.Contains("AllowNull", StringComparison.Ordinal))).IsTrue();
        await Assert.That(result.ExecuteBoolean(EntryType, EntryMethod)).IsTrue();
    }
}
