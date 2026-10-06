// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Validates private projection against the actual pinned producer packages.</summary>
public sealed class ProducerCompilerTests
{
    /// <summary>The ReactiveRoot fixture identity.</summary>
    private const string ReactiveRoot = "ReactiveUI.Reactive";

    /// <summary>The PrimitivesRoot fixture identity.</summary>
    private const string PrimitivesRoot = "ReactiveUI";

    /// <summary>The ReactiveBindingRoot fixture identity.</summary>
    private const string ReactiveBindingRoot = "ReactiveUI.Binding.Reactive";

    /// <summary>The BindingRoot fixture identity.</summary>
    private const string BindingRoot = "ReactiveUI.Binding";

    /// <summary>The ReactiveValidationRoot fixture identity.</summary>
    private const string ReactiveValidationRoot = "ReactiveUI.Validation.Reactive";

    /// <summary>The ValidationRoot fixture identity.</summary>
    private const string ValidationRoot = "ReactiveUI.Validation";

    /// <summary>The ModelName fixture identity.</summary>
    private const string ModelName = "Model";

    /// <summary>The CheckName fixture identity.</summary>
    private const string CheckName = "Check";

    /// <summary>The ContractMarker fixture identity.</summary>
    private const string ContractMarker = "ValidationProducerContractAttribute";

    /// <summary>The independent generic and observable declarations for full producer fidelity.</summary>
    private const string FullSupportSource = """
            public partial class Outer<T> where T : class
            {
                public partial class Nested : ReactiveObject
                {
                    [Reactive] private T? m_value;
                }
            }
            public sealed class Constant<T> : IObservable<T>
            {
                public IDisposable Subscribe(IObserver<T> observer) { observer.OnNext(default!); return new Empty(); }
            }
            public sealed class Empty : IDisposable { public void Dispose() { } }
        """;

    /// <summary>Executes generated notification and nullable field rules in either producer order.</summary>
    /// <param name="reactive">Whether to use System.Reactive.</param>
    /// <param name="validationFirst">Whether Validation precedes the real producers.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task GeneratedFieldsNotificationAndSafeMetadataRebind(bool reactive, bool validationFirst)
    {
        using var host = CapabilityCompilerHost.Create(reactive, validationFirst);
        var source = Source(reactive);
        var initial = await host.RunAsync(source);
        await Assert.That(initial.GeneratorDiagnostics).IsEmpty();
        await Assert.That(initial.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await initial.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(initial.ExecuteBoolean(ModelName, CheckName)).IsTrue();
        await Assert.That(initial.ValidationSources.Any(static generated => generated.Text.Contains(ContractMarker, StringComparison.Ordinal))).IsTrue();
        await Assert.That(initial.Compilation.SyntaxTrees.Any(static tree => tree.FilePath == "Validation.PrivateProducerProjection.cs")).IsFalse();
        await Assert.That(initial.ValidationSources.Any(static generated => generated.Text.Contains("throw null", StringComparison.Ordinal))).IsFalse();

        var changed = await host.RunAsync(source.Replace("_name;", "_name = \"ok\";", StringComparison.Ordinal));
        await Assert.That(changed.GeneratorDiagnostics).IsEmpty();
        await Assert.That(await changed.GetDispatchDiagnosticsAsync()).IsEmpty();
        var reverted = await host.RunAsync(source);
        await Assert.That(reverted.GeneratorDiagnostics).IsEmpty();
        await Assert.That(await reverted.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(reverted.ValidationSources.Select(static generated => generated.Sha256)).IsEquivalentTo(initial.ValidationSources.Select(static generated => generated.Sha256));
    }

    /// <summary>Checks the full declaration profile, including nonpublic, inherited and partial members.</summary>
    /// <param name="reactive">Whether to use System.Reactive.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task FullProducerDeclarationsMatchActualOutput(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var source = FullSource(reactive);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.Compilation.GetTypeByMetadataName("Parent")!.GetMembers("FetchCommand")).IsNotEmpty();
        await Assert.That(result.Compilation.GetTypeByMetadataName("Parent")!.GetMembers("KeepAsyncCommand")).IsNotEmpty();
        await Assert.That(result.ValidationSources.Any(static generated => generated.HintName == "ValidationProducerContracts.g.cs")).IsTrue();
    }

    /// <summary>Rejects the actual producer's namespace-only notification output for nested attributed classes.</summary>
    /// <param name="reactive">Whether to use System.Reactive.</param>
    /// <returns>The asynchronous final-contract verification.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task NestedGeneratedNotificationRejectsActualProducerDrift(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var root = reactive ? ReactiveValidationRoot : ValidationRoot;
        var result = await host.RunAsync($$"""
            using System;
            using ReactiveUI.SourceGenerators;
            using {{root}}.Abstractions;
            using {{root}}.Contexts;
            using {{root}}.Extensions;
            public partial class Outer
            {
                [IReactiveObject]
                public partial class Nested : IValidatableViewModel
                {
                    [Reactive] private string? _name;
                    public IValidationContext ValidationContext { get; } = new ValidationContext();
                    public IDisposable Register() => this.ValidationRule(value => value.Name, static value => value != null, "bad");
                }
            }
            """);
        var drift = (await result.GetDispatchDiagnosticsAsync()).Where(static diagnostic => diagnostic.Id == "RUVG008").ToArray();
        await Assert.That(drift).IsNotEmpty();
        await Assert.That(Array.Exists(drift, static diagnostic => diagnostic.GetMessage().Contains("top-level generated class", StringComparison.Ordinal))).IsTrue();
        await Assert.That(result.Compilation.GetTypeByMetadataName("Outer+Nested")!.AllInterfaces.Any(static type => type.ToDisplayString() == "ReactiveUI.IReactiveObject")).IsFalse();
    }

    /// <summary>Rejects unavailable/drifting profile metadata and invalidates removed producer attributes.</summary>
    /// <param name="reactive">Whether to use System.Reactive.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ProfileDriftAndRemovedAttributesFailActionably(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var source = Source(reactive);
        var drift = await host.RunAsync(source, globalOptions: new Dictionary<string, string> { ["build_property.ReactiveUIValidationReactiveProducerVersion"] = "4.3.0" });
        await Assert.That(drift.GeneratorDiagnostics.Any(static diagnostic => diagnostic.Id == "RUVG009" && diagnostic.Severity == DiagnosticSeverity.Error)).IsTrue();
        var restored = await host.RunAsync(source);
        await Assert.That(restored.GeneratorDiagnostics).IsEmpty();
        var removed = await host.RunAsync(source.Replace("[Reactive] private string? _name;", "private string? _name;", StringComparison.Ordinal));
        await Assert.That(removed.CompilationDiagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsTrue();
        await Assert.That(removed.ValidationSources.Any(static generated => generated.Text.Contains("\"Name\",\"property", StringComparison.Ordinal))).IsFalse();
        var reverted = await host.RunAsync(source);
        await Assert.That(reverted.GeneratorDiagnostics).IsEmpty();
        await Assert.That(await reverted.GetDispatchDiagnosticsAsync()).IsEmpty();
    }

    /// <summary>Compiles the real generated WinForms hosts without constructing controls on Linux.</summary>
    /// <param name="reactive">Whether to use System.Reactive.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task WinFormsHostsPreserveActualNongenericViewContracts(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var validation = reactive ? ReactiveValidationRoot : ValidationRoot;
        var binding = reactive ? ReactiveBindingRoot : BindingRoot;
        var references = WindowsDesktopReferences.Read();
        var source = $$"""
            using System;
            using System.ComponentModel;
            using ReactiveUI.SourceGenerators.WinForms;
            using {{validation}}.Abstractions;
            using {{validation}}.Contexts;
            using {{validation}}.Extensions;
            [ViewModelControlHost("global::System.Windows.Forms.UserControl")]
            public partial class ViewHost : IValidatableViewModel
            {
                private IContainer? components = new Container();
                private void InitializeComponent() { }
                public IValidationContext ValidationContext { get; } = new ValidationContext();
                public IDisposable Register() => this.ValidationRule(x => x.CacheViews, x => x, "bad");
            }
            [RoutedControlHost("global::System.Windows.Forms.UserControl")]
            public partial class RoutedHost : IValidatableViewModel
            {
                private IContainer? components = new Container();
                private void InitializeComponent() { }
                public IValidationContext ValidationContext { get; } = new ValidationContext();
                public IDisposable Register() => this.ValidationRule(x => x.ViewContractObservable, x => x != null, "bad");
            }
            """;
        var result = await host.RunAsync(source, additionalReferences: references);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        var view = result.Compilation.GetTypeByMetadataName("ViewHost")!;
        await Assert.That(view.AllInterfaces.Any(type => type.ToDisplayString() == $"{binding}.IViewFor")).IsTrue();
        await Assert.That(view.AllInterfaces.Any(static type => type.Name == "IViewFor" && type.Arity == 1)).IsFalse();
        await Assert.That(result.Compilation.GetTypeByMetadataName("RoutedHost")!.AllInterfaces.Any(static type => type.Name == "IViewFor")).IsFalse();
    }

    /// <summary>Supports conditional receivers while preserving null short-circuiting.</summary>
    /// <param name="reactive">Whether to use System.Reactive.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ConditionalReceiverUsesOriginalInterceptionLocation(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var source = Source(reactive).Replace("using var rule = model.ValidationRule", "using var rule = model?.ValidationRule", StringComparison.Ordinal)
            .Replace("rule.ValidationChanged", "rule!.ValidationChanged", StringComparison.Ordinal)
            .Replace("model.ValidationRule", "model!.ValidationRule", StringComparison.Ordinal)
            .Replace("model.Name", "model!.Name", StringComparison.Ordinal)
            .Replace(
                "var sink = new Sink();",
                """
                Model? absent = null;
                using var skipped = absent?.ValidationRule(x => x.Name, x => throw new Exception(), "bad");
                if (skipped != null) return false;
                var sink = new Sink();
                """,
                StringComparison.Ordinal);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(ModelName, CheckName)).IsTrue();
    }

    /// <summary>Allows explicit declarations with prediction disabled without weakening final dispatch checks.</summary>
    /// <param name="reactive">Whether to use System.Reactive.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ExplicitProjectionOptOutPreservesDeclaredNormalCalls(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var ui = reactive ? ReactiveRoot : PrimitivesRoot;
        var source = Source(reactive).Replace("[Reactive] private string? _name;", "[Reactive] public partial string? Name { get; set; }", StringComparison.Ordinal)
            .Replace("[IReactiveObject]", string.Empty, StringComparison.Ordinal)
            .Replace("class Model : IValidatableViewModel", $"class Model : global::{ui}.ReactiveObject, IValidatableViewModel", StringComparison.Ordinal);
        var result = await host.RunAsync(source, globalOptions: new Dictionary<string, string>
        {
            ["build_property.ReactiveUIValidationProducerProjectionEnabled"] = "false",
            ["build_property.ReactiveUIValidationReactiveProducerVersion"] = "4.3.0",
        });
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ValidationSources.Any(static generated => generated.HintName == "ValidationProducerContracts.g.cs")).IsFalse();
        await Assert.That(result.ExecuteBoolean(ModelName, CheckName)).IsTrue();
    }

    /// <summary>Constructs the full pinned producer declaration inventory.</summary>
    /// <param name="reactive">Whether to use System.Reactive.</param>
    /// <returns>The real producer declarations submitted to the compiler.</returns>
    private static string FullSource(bool reactive)
    {
        var validation = reactive ? ReactiveValidationRoot : ValidationRoot;
        return $$"""
            using System;
            using System.Collections.ObjectModel;
            using System.Threading;
            using System.Threading.Tasks;
            using ReactiveUI.SourceGenerators;
            using {{(reactive ? ReactiveRoot : PrimitivesRoot)}};
            using {{validation}}.Extensions;
            public partial class Parent : ReactiveObject, global::{{validation}}.Abstractions.IValidatableViewModel
            {
                public global::{{validation}}.Contexts.IValidationContext ValidationContext { get; } = new global::{{validation}}.Contexts.ValidationContext();
                public IDisposable Register() => this.ValidationRule(x => x.Label, x => x != null, "bad");
                [Reactive(Inheritance = InheritanceModifier.Virtual)] private string? _label;
                [property: System.Diagnostics.CodeAnalysis.MaybeNull]
                [Reactive] private string _maybeNull = string.Empty;
                [property: System.Diagnostics.CodeAnalysis.AllowNull]
                [Reactive] private string _allowNull = string.Empty;
                [Reactive(SetModifier = AccessModifier.PrivateProtected)] private string? _privateProtected;
                [Reactive(SetModifier = AccessModifier.Protected)] private string? _protected;
                [Reactive(SetModifier = AccessModifier.Internal)] private string? _internal;
                [Reactive(SetModifier = AccessModifier.Private)] private string? _private;
                [Reactive(SetModifier = AccessModifier.InternalProtected)] private string? _internalProtected;
                [Reactive(SetModifier = AccessModifier.Init, UseRequired = true)] private string? _requiredName;
                [ReactiveCollection] private ObservableCollection<int>? _items;
                [BindableDerivedList(AccessModifier = PropertyAccessModifier.PrivateProtected)] private ReadOnlyObservableCollection<int>? _rows = null;
                [ReactiveCommand] private void Submit() { }
                [ReactiveCommand] private Task SaveAsync(CancellationToken cancellation) => Task.CompletedTask;
                [ReactiveCommand] private Task<string?> FetchAsync(int input, CancellationToken cancellation) => Task.FromResult<string?>(null);
                [ReactiveCommand] private ValueTask<int> KeepAsync() => new(1);
                [ReactiveCommand] private IObservable<string?> Watch() => new Constant<string?>();
                [{{(reactive ? ReactiveBindingRoot : BindingRoot)}}.ObservableAsProperty(UseProtected = true, ReadOnly = true)] public partial string? Caption { get; }
                [Reactive] public partial string? Declared { get; set; }
            }
            public partial class Child : Parent
            {
                [Reactive(Inheritance = InheritanceModifier.Override)] private string? _label;
                [Reactive(Inheritance = InheritanceModifier.New)] private string? _internal;
            }
            {{FullSupportSource}}
            """;
    }

    /// <summary>Constructs a generated-notification, command-metadata and field-observation consumer.</summary>
    /// <param name="reactive">Whether to use System.Reactive.</param>
    /// <returns>The actual producer consumer source.</returns>
    private static string Source(bool reactive)
    {
        var root = reactive ? ReactiveValidationRoot : ValidationRoot;
        return $$"""
            using System;
            using ReactiveUI.SourceGenerators;
            using {{(reactive ? ReactiveRoot : PrimitivesRoot)}}.Builder;
            using {{root}}.Abstractions;
            using {{root}}.Contexts;
            using {{root}}.Extensions;
            using {{root}}.States;
            [IReactiveObject]
            public partial class Model : IValidatableViewModel
            {
                [Reactive] private string? _name;
                [ReactiveCommand] private void Submit() { }
                public IValidationContext ValidationContext { get; } = new ValidationContext();
                public static bool Check()
                {
                    RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
                    var model = new Model();
                    using var rule = model.ValidationRule(x => x.Name, x => x == "ok", "bad");
                    using var metadata = model.ValidationRule(x => x.SubmitCommand, new Constant<bool>(), "metadata");
                    var sink = new Sink();
                    using var subscription = rule.ValidationChanged.Subscribe(sink);
                    if (sink.Current) return false;
                    model.Name = "ok";
                    if (!sink.Current) return false;
                    model.Name = null;
                    return !sink.Current;
                }
            }
            public sealed class Sink : IObserver<IValidationState>
            {
                public bool Current;
                public void OnNext(IValidationState state) => Current = state.IsValid;
                public void OnError(Exception error) => throw error;
                public void OnCompleted() { }
            }
            public sealed class Constant<T> : IObservable<T>
            {
                public IDisposable Subscribe(IObserver<T> observer) { observer.OnNext(default!); return new Empty(); }
            }
            public sealed class Empty : IDisposable { public void Dispose() { } }
            """;
    }
}
