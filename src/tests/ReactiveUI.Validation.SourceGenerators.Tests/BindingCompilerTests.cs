// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Exercises binding semantics through the actual compiler interceptor pipeline.</summary>
public sealed class BindingCompilerTests
{
    /// <summary>Names the trusted in-memory consumer's test entry point type.</summary>
    private const string FixtureTypeName = "Fixture";

    /// <summary>Names the trusted in-memory consumer's test entry point method.</summary>
    private const string FixtureMethodName = "Check";

    /// <summary>Consumer checks for rich-state identity, replacement, nulls, cached targets and disposal.</summary>
    private const string ReplacementBody = """
        RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
        var model = new Model();
        IValidationState initial = new RichState(false, "required", 7);
        var states = new Current(initial);
        using var rule = model.AddObservableRule(states, new[] { "Name" });
        model.Rule = rule;
        var view = new View { ViewModel = model };
        var original = view.Panel!;
        IValidationState? latest = null;
        using var raw = view.BindValidationState(
            converter: static state => state, helperProperty: x => x.Rule,
            onNext: state => latest = state, viewModel: model);
        using var rich = view.BindValidationState(model, x => x.Rule, x => x.Panel!.Status, Project);
        using var text = view.BindValidation(model, x => x.Name, x => x.Panel!.Message);
        Require(ReferenceEquals(latest, initial), "boxed custom state identity");
        Require(original.Status?.Revision == 7 && original.Message == "required", "actual initial state");
        view.Panel = new Panel();
        Require(view.Panel.Status?.Revision == 7, "equal-overriding replacement replays latest value");
        IValidationState changed = new RichState(false, "required", 8);
        states.Set(changed);
        Require(ReferenceEquals(latest, changed) && view.Panel.Status?.Revision == 8, "message-equal rich state update");
        Require(original.Status?.Revision == 7, "old target is detached");
        view.Panel = null;
        states.Set(new RichState(false, "later", 9));
        view.Panel = new Panel();
        Require(view.Panel.Status?.Revision == 9 && view.Panel.Message == "later", "null target caches latest projection");
        model.Rule = null;
        Require(view.Panel.Status is null && latest!.IsValid, "null helper clears rich target");
        model.Rule = rule;
        Require(view.Panel.Status?.Revision == 9, "replaced helper's current state");
        view.ViewModel = null;
        Require(view.Panel.Status is null && view.Panel.Message == "", "null model clears all targets");
        view.ViewModel = model;
        Require(view.Panel.Status?.Revision == 9, "model reattachment");
        rich.Dispose();
        text.Dispose();
        raw.Dispose();
        states.Set(new RichState(false, "disposed", 10));
        Require(view.Panel.Status?.Revision == 9 && view.Panel.Message == "later", "disposal stops all binding updates");
        view.Panel = new Panel();
        Require(view.Panel.Status is null && view.Panel.Message == "", "disposed parent observation stays detached");
        return true;
        """;

    /// <summary>Consumer types with explicit notification, custom states and ordinary static setters.</summary>
    private const string ConsumerTemplate = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using {{ui}};
        using {{ui}}.Builder;
        using {{binding}};
        using {{root}}.Abstractions;
        using {{root}}.Collections;
        using {{root}}.Components.Abstractions;
        using {{root}}.Contexts;
        using {{root}}.Extensions;
        using {{root}}.Helpers;
        using {{root}}.States;
        public readonly record struct Presentation(int Revision);
        public readonly record struct RichState(bool IsValid, string Code, int Revision) : IValidationState
        {
            public IValidationText Text => ValidationText.Create(Code);
        }
        public sealed class Model : ReactiveObject, IValidatableViewModel
        {
            public string Name { get; set; } = "";
            public ValidationHelper? Rule { get; set => this.RaiseAndSetIfChanged(ref field, value); }
            public IValidationContext ValidationContext { get; } = new ValidationContext();
            public IValidationContext? Selected { get; set => this.RaiseAndSetIfChanged(ref field, value); }
            public PrivateContext? HiddenContext { get; set => this.RaiseAndSetIfChanged(ref field, value); }
            public ExplicitContext? ExplicitContext { get; set => this.RaiseAndSetIfChanged(ref field, value); }
        }
        public sealed class View : ReactiveObject, IViewFor<Model>
        {
            public Model? ViewModel { get; set => this.RaiseAndSetIfChanged(ref field, value); }
            object? IViewFor.ViewModel { get => ViewModel; set => ViewModel = (Model?)value; }
            public Presentation? Status { get; set; }
            public bool Valid { get; set; }
            public string Message { get; set; } = "";
            public string Initial { get; init; } = "";
            public Plain? Plain { get; set; }
            public Panel? Panel
            {
                get;
                set
                {
                    if (ReferenceEquals(field, value)) return;
                    field = value;
                    this.RaisePropertyChanged();
                }
            } = new();
        }
        public interface INotifyingView : IViewFor<Model>, System.ComponentModel.INotifyPropertyChanged
        {
            string? Message { get; set; }
        }
        public struct StructView : INotifyingView
        {
            public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
            public Model? ViewModel
            {
                get;
                set
                {
                    field = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ViewModel)));
                }
            }
            object? IViewFor.ViewModel { get => ViewModel; set => ViewModel = (Model?)value; }
            public string? Message { get; set; }
        }
        public sealed class Plain { public Panel? Panel { get; set; } }
        public sealed class Panel
        {
            public Presentation? Status { get; set; }
            public string Message { get; set; } = "";
            public override bool Equals(object? other) => other is Panel;
            public override int GetHashCode() => 1;
        }
        public sealed class Current(IValidationState initial) : IObservable<IValidationState>
        {
            private readonly List<IObserver<IValidationState>> _observers = new();
            private IValidationState _current = initial;
            public IDisposable Subscribe(IObserver<IValidationState> observer)
            {
                _observers.Add(observer);
                observer.OnNext(_current);
                return new Cleanup(() => _observers.Remove(observer));
            }
            public void Set(IValidationState value)
            {
                _current = value;
                foreach (var observer in _observers.ToArray()) observer.OnNext(value);
            }
        }
        public sealed class PrivateContext : ValidationContext
        {
            private new IObservable<IValidationState> ValidationStatusChange => throw new InvalidOperationException("private shadow");
        }
        public sealed class ExplicitContext : ValidationContext, IValidationContext, IValidationComponent
        {
            public new IObservable<IValidationState> ValidationStatusChange { get; } = new Current(new RichState(false, "wrong", 99));
            IObservable<IValidationState> IValidationComponent.ValidationStatusChange => base.ValidationStatusChange;
        }
        public sealed class Cleanup(Action dispose) : IDisposable { public void Dispose() => dispose(); }
        public static class Fixture
        {
            public static Presentation? Project(IValidationState state) => state.IsValid ? null : new Presentation(((RichState)state).Revision);
            public static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
            public static bool Check()
            {
                {{body}}
            }
        }
        """;

    /// <summary>Uses the supported compiler and explicitly opts into the generator's interceptor namespace.</summary>
    private static readonly CSharpParseOptions ParseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp14)
        .WithFeatures(new Dictionary<string, string> { ["InterceptorsNamespaces"] = "ReactiveUI.Validation.Generated" });

    /// <summary>Compiles every familiar binding overload in both runtime flavors.</summary>
    /// <param name="reactive">Whether the fixture uses the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task AllBindingOverloadsCompileWithoutReflectedDispatch(bool reactive)
    {
        const string body = """
            var model = new Model();
            var view = new View { ViewModel = model };
            view.BindValidation(model, x => x.Name, x => x.Message);
            view.BindValidation(model, x => x.Name, x => x.Message, null);
            view.BindValidation(model, x => x.Message);
            view.BindValidation(model, x => x.Message, null);
            view.BindValidation(model, x => x!.Rule, x => x.Message);
            view.BindValidation(model, x => x!.Rule, x => x.Message, null);
            view.BindValidationState(model, x => x.Rule, x => x.Status, Project);
            view.BindValidationState(model, x => x.Rule, Project, static value => { });
            view.BindValidationState(model, x => x.Name, x => x.Valid, static states => states.All(s => s.IsValid), true);
            view.BindValidationState(model, x => x.Name, static states => states.Count, static count => { }, false);
            view.BindValidationContext(model, x => x.Selected, x => x.Message);
            view.BindValidationContext(model, x => x.Selected, x => x.Name, x => x.Message, null, false);
            view.BindValidationContext(model, x => x.Selected, static state => { });
            view.BindValidationContext(model, x => x.Selected, x => x.Name, static states => { }, true);
            view.BindValidationState(model, x => x.Rule, x => x.Panel!.Status, Project);
            view.BindValidation(model, x => x.Name, x => x.Panel!.Message);
            return true;
            """;
        var result = Generate(Source(reactive, body));
        await Assert.That(result.Diagnostics).IsEmpty();
        await Assert.That(Errors(result.Compilation)).IsEmpty();
        await Assert.That(Warnings(result.Compilation)).IsEmpty();
        await Assert.That(await DispatchErrors(result.Compilation)).IsEmpty();
        await Assert.That(result.Generated).Contains("ObserveReference");
        await Assert.That(result.Generated).Contains("BindToTarget");
        await Assert.That(result.Generated).DoesNotContain("WhenAnyValue");
        await Assert.That(result.Generated).DoesNotContain("Unsafe");
        await Assert.That(result.Generated).DoesNotContain("Reflection");
    }

    /// <summary>Executes replacement, raw-state, null and nested-target replay behavior in both flavors.</summary>
    /// <param name="reactive">Whether the fixture uses the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    [SuppressMessage(
        "Security",
        "SES1402",
        Justification = "The test loads only a consumer compiled in memory from this repository's fixed trusted fixture to exercise generated dispatch.")]
    public async Task GeneratedBindingsPreserveStatesAndReplayReplacedTargets(bool reactive)
    {
        var result = Generate(Source(reactive, ReplacementBody));
        await Assert.That(result.Diagnostics).IsEmpty();
        await Assert.That(Errors(result.Compilation)).IsEmpty();
        await Assert.That(Warnings(result.Compilation)).IsEmpty();
        await Assert.That(await DispatchErrors(result.Compilation)).IsEmpty();
        await using var stream = new MemoryStream();
        var emitted = result.Compilation.Emit(stream);
        await Assert.That(string.Join("\n", emitted.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))).IsEmpty();
        var assembly = System.Reflection.Assembly.Load(stream.ToArray());
        var actual = (bool)assembly.GetType(FixtureTypeName)!.GetMethod(FixtureMethodName)!.Invoke(null, null)!;
        await Assert.That(actual).IsTrue();
    }

    /// <summary>Uses the selector's context interface contract despite private shadows and misleading concrete streams.</summary>
    /// <param name="reactive">Whether the fixture uses the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    [SuppressMessage(
        "Security",
        "SES1402",
        Justification = "The test loads only a consumer compiled in memory from this repository's fixed trusted fixture to exercise generated dispatch.")]
    public async Task SelectedContextsUseInterfaceDispatch(bool reactive)
    {
        const string body = """
            RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
            var model = new Model();
            var view = new View { ViewModel = model };
            using var hidden = new PrivateContext();
            using var hiddenRule = hidden.AddObservableRule(new Current(new RichState(false, "hidden", 1)), new[] { "Name" });
            model.HiddenContext = hidden;
            IValidationState? aggregate = null;
            using var hiddenText = view.BindValidationContext(model, x => x.HiddenContext, x => x.Message);
            using var hiddenAction = view.BindValidationContext(model, x => x.HiddenContext, state => aggregate = state);
            Require(view.Message == "hidden" && aggregate?.IsValid == false, "private shadow does not affect interface aggregate");
            hiddenText.Dispose();
            hiddenAction.Dispose();
            using var selected = new ExplicitContext();
            using var selectedRule = selected.AddObservableRule(new Current(new RichState(false, "correct", 2)), new[] { "Name" });
            model.ExplicitContext = selected;
            using var selectedText = view.BindValidationContext(model, x => x.ExplicitContext, x => x.Message);
            using var selectedAction = view.BindValidationContext(model, x => x.ExplicitContext, state => aggregate = state);
            Require(view.Message == "correct" && aggregate?.Text.ToSingleLine() == "correct", "explicit interface wins over misleading concrete stream");
            model.ExplicitContext = null;
            Require(view.Message == "" && aggregate!.IsValid, "null concrete context clears interface bindings");
            model.ExplicitContext = selected;
            Require(view.Message == "correct", "concrete context reattachment follows interface contract");
            return true;
            """;
        var result = Generate(Source(reactive, body));
        await Assert.That(result.Diagnostics).IsEmpty();
        await Assert.That(Errors(result.Compilation)).IsEmpty();
        await Assert.That(Warnings(result.Compilation)).IsEmpty();
        await Assert.That(await DispatchErrors(result.Compilation)).IsEmpty();
        await Assert.That(result.Generated).Contains($"ObserveReference<global::{(reactive ? "ReactiveUI.Validation.Reactive" : "ReactiveUI.Validation")}.Contexts.IValidationContext>");
        await using var stream = new MemoryStream();
        var emitted = result.Compilation.Emit(stream);
        await Assert.That(string.Join("\n", emitted.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))).IsEmpty();
        var assembly = System.Reflection.Assembly.Load(stream.ToArray());
        await Assert.That((bool)assembly.GetType(FixtureTypeName)!.GetMethod(FixtureMethodName)!.Invoke(null, null)!).IsTrue();
    }

    /// <summary>Rejects concrete struct receivers whose observation and setter ownership would use copies.</summary>
    /// <param name="reactive">Whether the fixture uses the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ConcreteStructViewsAreRejectedBeforeEmission(bool reactive)
    {
        const string body = """
            var model = new Model();
            var view = new StructView { ViewModel = model };
            view.BindValidation(model, x => x.Name, x => x.Message);
            return true;
            """;
        var result = Generate(Source(reactive, body));
        var diagnostic = result.Diagnostics.Single(static diagnostic => diagnostic.Id == "RUVG006");
        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostic.GetMessage()).Contains("reference type ownership contract");
        await Assert.That(diagnostic.GetMessage()).Contains("interface-typed boxed view");
        await Assert.That(result.Generated).IsEmpty();
        await Assert.That(Errors(result.Compilation)).IsEmpty();
        await Assert.That(Warnings(result.Compilation)).IsEmpty();
    }

    /// <summary>Supports a boxed struct through a stable notifying interface reference.</summary>
    /// <param name="reactive">Whether the fixture uses the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    [SuppressMessage(
        "Security",
        "SES1402",
        Justification = "The test loads only a consumer compiled in memory from this repository's fixed trusted fixture to verify reference ownership.")]
    public async Task InterfaceTypedBoxedViewsRetainReferenceOwnership(bool reactive)
    {
        const string body = """
            RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
            var model = new Model();
            using var rule = model.AddObservableRule(new Current(new RichState(false, "required", 1)), new[] { "Name" });
            INotifyingView view = new StructView { ViewModel = model };
            using var binding = view.BindValidation(model, x => x.Name, x => x.Message);
            Require(view.Message == "required", "setter mutates the stable boxed view");
            view.ViewModel = null;
            Require(view.Message == "", "boxed view sends replacement notifications");
            return true;
            """;
        var result = Generate(Source(reactive, body));
        await Assert.That(result.Diagnostics).IsEmpty();
        await Assert.That(Errors(result.Compilation)).IsEmpty();
        await Assert.That(Warnings(result.Compilation)).IsEmpty();
        await Assert.That(await DispatchErrors(result.Compilation)).IsEmpty();
        await using var stream = new MemoryStream();
        var emitted = result.Compilation.Emit(stream);
        await Assert.That(string.Join("\n", emitted.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))).IsEmpty();
        var assembly = System.Reflection.Assembly.Load(stream.ToArray());
        await Assert.That((bool)assembly.GetType(FixtureTypeName)!.GetMethod(FixtureMethodName)!.Invoke(null, null)!).IsTrue();
    }

    /// <summary>Reports unsupported binding inputs at the selector instead of emitting invalid C#.</summary>
    /// <param name="reactive">Whether the fixture uses the System.Reactive flavor.</param>
    /// <param name="call">The unsupported invocation.</param>
    /// <param name="selector">The exact rejected source span.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false, "view.BindValidationState(model, helper, Project, static value => { });", "helper")]
    [Arguments(false, "view.BindValidationState(model, x => x.Rule, target, Project);", "target")]
    [Arguments(false, "view.BindValidationState(model, x => x.Rule, x => x.Plain!.Panel!.Status, Project);", "x => x.Plain!.Panel!.Status")]
    [Arguments(false, "view.BindValidationState<View, Model, object>(model, x => x.Rule, x => x.Message, static state => (object)\"x\");", "x => x.Message")]
    [Arguments(false, "view.BindValidationState(model, x => x.Rule, x => x.Initial, static state => \"x\");", "x => x.Initial")]
    [Arguments(true, "view.BindValidationState(model, helper, Project, static value => { });", "helper")]
    [Arguments(true, "view.BindValidationState(model, x => x.Rule, target, Project);", "target")]
    [Arguments(true, "view.BindValidationState(model, x => x.Rule, x => x.Plain!.Panel!.Status, Project);", "x => x.Plain!.Panel!.Status")]
    [Arguments(true, "view.BindValidationState<View, Model, object>(model, x => x.Rule, x => x.Message, static state => (object)\"x\");", "x => x.Message")]
    [Arguments(true, "view.BindValidationState(model, x => x.Rule, x => x.Initial, static state => \"x\");", "x => x.Initial")]
    public async Task UnsupportedBindingsProduceActionableSelectorDiagnostics(bool reactive, string call, string selector)
    {
        var body = $$"""
            var model = new Model();
            var view = new View { ViewModel = model };
            System.Linq.Expressions.Expression<Func<Model,ValidationHelper?>> helper = x => x.Rule;
            System.Linq.Expressions.Expression<Func<View,Presentation?>> target = x => x.Status;
            {{call}}
            return true;
            """;
        var source = Source(reactive, body);
        var result = Generate(source);
        var diagnostic = result.Diagnostics.Single(static diagnostic => diagnostic.Id == "RUVG006");
        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(source.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length)).IsEqualTo(selector);
        await Assert.That(diagnostic.GetMessage()).Contains("Unsafe");
        await Assert.That(result.Generated).IsEmpty();
    }

    /// <summary>Builds ordinary notifying application types whose getters and setters are statically accessible.</summary>
    /// <param name="reactive">Whether the fixture uses the System.Reactive flavor.</param>
    /// <param name="body">The application check body.</param>
    /// <returns>The complete consumer source.</returns>
    private static string Source(bool reactive, string body)
    {
        var root = reactive ? "ReactiveUI.Validation.Reactive" : "ReactiveUI.Validation";
        var ui = reactive ? "ReactiveUI.Reactive" : "ReactiveUI";
        var binding = reactive ? "ReactiveUI.Binding.Reactive" : "ReactiveUI.Binding";
        return ConsumerTemplate.Replace("{{ui}}", ui).Replace("{{binding}}", binding)
            .Replace("{{root}}", root).Replace("{{body}}", body);
    }

    /// <summary>Runs the installed generator against producer DLL references and returns the resulting compilation.</summary>
    /// <param name="source">The consumer source.</param>
    /// <returns>The generated source and semantic compilation.</returns>
    private static CompilationResult Generate(string source)
    {
        var references = Directory.GetFiles(AppContext.BaseDirectory, "*.dll")
            .Concat(((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator))
            .Distinct().Select(static path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            $"GeneratedBindingTest{Guid.NewGuid():N}",
            [CSharpSyntaxTree.ParseText(source, ParseOptions, "BindingCaller.cs")],
            references,
            new(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new ValidationGenerator().AsSourceGenerator()], parseOptions: ParseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var generated, out var diagnostics);
        return new(generated, diagnostics, string.Join("\n", driver.GetRunResult().GeneratedTrees.Select(static tree => tree.ToString())));
    }

    /// <summary>Returns semantic errors after the generated source has entered the compilation.</summary>
    /// <param name="compilation">The completed consumer compilation.</param>
    /// <returns>The error messages.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Errors(Compilation compilation) =>
        string.Join("\n", compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));

    /// <summary>Returns semantic warnings so nullable generic mismatches cannot escape into strict consumers.</summary>
    /// <param name="compilation">The completed consumer compilation.</param>
    /// <returns>The warning messages.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Warnings(Compilation compilation) =>
        string.Join("\n", compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning));

    /// <summary>Checks final dispatch with the actual analyzer after generated members enter the compilation.</summary>
    /// <param name="compilation">The completed consumer compilation.</param>
    /// <returns>The dispatch analyzer errors.</returns>
    private static async Task<string> DispatchErrors(Compilation compilation)
    {
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new ValidationDispatchAnalyzer());
        var diagnostics = await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
        return string.Join("\n", diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

    /// <summary>Captures source generation and its completed semantic compilation.</summary>
    /// <param name="Compilation">The post-generation consumer compilation.</param>
    /// <param name="Diagnostics">The source-generator diagnostics.</param>
    /// <param name="Generated">The complete generated interceptor source.</param>
    private sealed record CompilationResult(Compilation Compilation, ImmutableArray<Diagnostic> Diagnostics, string Generated);
}
