// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using ReactiveUI.Validation.SourceGenerators;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Exercises generated rules against both actual runtime assemblies.</summary>
public sealed class CompilerTests
{
    /// <summary>The original caller fixture path.</summary>
    private const string CallerPath = "Caller.cs";

    /// <summary>The emitted model fixture type name.</summary>
    private const string ModelTypeName = "Model";

    /// <summary>The trusted fixture entry method.</summary>
    private const string FixtureMethodName = "Check";

    /// <summary>The Primitives ReactiveUI namespace.</summary>
    private const string PrimitivesNamespace = "ReactiveUI";

    /// <summary>The System.Reactive ReactiveUI namespace.</summary>
    private const string ReactiveNamespace = "ReactiveUI.Reactive";

    /// <summary>The Primitives Validation namespace.</summary>
    private const string PrimitivesValidationNamespace = "ReactiveUI.Validation";

    /// <summary>The System.Reactive Validation namespace.</summary>
    private const string ReactiveValidationNamespace = "ReactiveUI.Validation.Reactive";

    /// <summary>The final dispatch diagnostic identifier.</summary>
    private const string MissingDispatchId = "RUVG005";

    /// <summary>Uses the shipping compiler language and interception namespace contract.</summary>
    private static readonly CSharpParseOptions ParseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp14)
        .WithFeatures(new Dictionary<string, string> { ["InterceptorsNamespaces"] = "ReactiveUI.Validation.Generated" });

    /// <summary>Compiles and executes every predicate shape for either runtime flavor.</summary>
    /// <param name="reactive">Whether to use the System.Reactive flavor.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    [SuppressMessage("Security", "SES1402", Justification = "Only hard-coded compiler test sources and trusted runtime references are emitted into this in-memory test assembly.")]
    public async Task AllPredicateShapesCompileAndExecute(bool reactive)
    {
        var root = reactive ? ReactiveValidationNamespace : PrimitivesValidationNamespace;
        var ui = reactive ? ReactiveNamespace : PrimitivesNamespace;
        var source = $$"""
            using System;
            using {{ui}};
            using {{root}}.Abstractions;
            using {{root}}.Contexts;
            using {{root}}.Extensions;
            public sealed class Model : ReactiveObject, IValidatableViewModel
            {
                public string? Name { get; set; }
                public Model? Address { get; set; }
                public IValidationContext ValidationContext { get; } = new ValidationContext();
                public static bool Check()
                {
                    var model = new Model();
                    using var one = model.ValidationRule(x => x.Name, x => x == "ok", "bad");
                    using var two = model.ValidationRule(message: x => "bad:" + x, viewModelProperty: x => x.Name, isPropertyValid: x => x == "ok");
                    using var destination = new ValidationContext();
                    using var three = model.ValidationRule(destination, x => x.Name, x => x == "ok", "bad");
                    using var four = model.ValidationRule(destination, x => x.Address!.Name, x => x == "ok", x => "bad:" + x);
                    using var five = ValidatableViewModelExtensions.ValidationRule<Model, string>(model, (x => x.Name), x => x == "ok", "bad");
                    return !one.IsValid && !two.IsValid && !three.IsValid && !four.IsValid && !five.IsValid;
                }
            }
            """;
        var result = Generate(source);
        await Assert.That(await result.Compilation.WithAnalyzers([new ValidationDispatchAnalyzer()]).GetAnalyzerDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.Diagnostics).IsEmpty();
        await Assert.That(result.Compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(result.Generated).Contains("ValidationRuntime.RegisterRule");
        await Assert.That(result.Generated).Contains("Address.Name");
        await Assert.That(result.Generated).DoesNotContain("WhenAnyValue");
        await using var stream = new MemoryStream();
        var emit = result.Compilation.Emit(stream);
        await Assert.That(string.Join("\n", emit.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))).IsEmpty();
        var assembly = System.Reflection.Assembly.Load(stream.ToArray());
        await Assert.That((bool)assembly.GetType(ModelTypeName)!.GetMethod(FixtureMethodName)!.Invoke(null, null)!).IsTrue();
    }

    /// <summary>Uses the constrained interface context even when the model shadows its name.</summary>
    /// <param name="reactive">Whether to use the System.Reactive flavor.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    [SuppressMessage("Security", "SES1402", Justification = "Only hard-coded compiler test sources and trusted runtime references are emitted into this in-memory test assembly.")]
    public async Task ExplicitInterfaceContextAndPrivateShadowAreSupported(bool reactive)
    {
        var root = reactive ? ReactiveValidationNamespace : PrimitivesValidationNamespace;
        var ui = reactive ? ReactiveNamespace : PrimitivesNamespace;
        var source = $$"""
            using System;
            using {{ui}};
            using {{root}}.Abstractions;
            using {{root}}.Contexts;
            using {{root}}.Extensions;
            public sealed class Model : ReactiveObject, IValidatableViewModel
            {
                private readonly IValidationContext _context;
                public Model(IValidationContext context) => _context = context;
                public string? Name { get; set; }
                public int ContextReads { get; private set; }
                private IValidationContext ValidationContext => throw new InvalidOperationException("private shadow");
                IValidationContext IValidatableViewModel.ValidationContext
                {
                    get
                    {
                        ContextReads++;
                        return _context;
                    }
                }
                public static bool Check()
                {
                    using var context = new ValidationContext();
                    var model = new Model(context);
                    using var one = model.ValidationRule(x => x.Name, x => x == "ok", "bad");
                    using var two = model.ValidationRule(x => x.Name, x => x == "ok", x => "bad:" + x);
                    return !one.IsValid && !two.IsValid && model.ContextReads == 2;
                }
            }
            """;
        var result = Generate(source);
        await Assert.That(result.Diagnostics).IsEmpty();
        await Assert.That(await result.Compilation.WithAnalyzers([new ValidationDispatchAnalyzer()]).GetAnalyzerDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.Compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning)).IsEmpty();
        await using var stream = new MemoryStream();
        var emit = result.Compilation.Emit(stream);
        await Assert.That(string.Join("\n", emit.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))).IsEmpty();
        var assembly = System.Reflection.Assembly.Load(stream.ToArray());
        await Assert.That((bool)assembly.GetType(ModelTypeName)!.GetMethod(FixtureMethodName)!.Invoke(null, null)!).IsTrue();
    }

    /// <summary>Executes finite stored selectors while keeping each closed generic rule current.</summary>
    /// <param name="reactive">Whether to use the System.Reactive flavor.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task FiniteStoredSelectorsCompileAndNotify(bool reactive)
    {
        var ui = reactive ? ReactiveNamespace : PrimitivesNamespace;
        var root = reactive ? ReactiveValidationNamespace : PrimitivesValidationNamespace;
        var source = $$"""
            using System;
            using System.Linq.Expressions;
            using {{ui}};
            using {{root}}.Abstractions;
            using {{root}}.Contexts;
            using {{root}}.Extensions;
            public sealed class Model : ReactiveObject, IValidatableViewModel
            {
                private static readonly Expression<Func<Model,string?>> FieldSelector = value => value.Name;
                private static Expression<Func<Model,string?>> PropertySelector => value => value.Name;
                private static Expression<Func<Model,string?>> FactorySelector() => value => value.Name;
                public string? Name { get; set => this.RaiseAndSetIfChanged(ref field, value); }
                public IValidationContext ValidationContext { get; } = new ValidationContext();
                public static bool Check()
                {
                    var model = new Model();
                    Expression<Func<Model,string?>> local = value => value.Name;
                    using var one = model.ValidationRule(local, value => value == "ok", "bad");
                    using var two = model.ValidationRule(FieldSelector, value => value == "ok", "bad");
                    using var three = model.ValidationRule(PropertySelector, value => value == "ok", "bad");
                    using var four = model.ValidationRule(FactorySelector(), value => value == "ok", "bad");
                    var initial = !one.IsValid && !two.IsValid && !three.IsValid && !four.IsValid;
                    model.Name = "ok";
                    var changed = one.IsValid && two.IsValid && three.IsValid && four.IsValid;
                    model.Name = "bad";
                    return initial && changed && !one.IsValid && !two.IsValid && !three.IsValid && !four.IsValid;
                }
            }
            """;
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(ModelTypeName, FixtureMethodName)).IsTrue();
    }

    /// <summary>Rejects selectors without declared finite provenance or change sources.</summary>
    /// <param name="selector">The unsupported selector source.</param>
    /// <param name="diagnosticId">The required error identifier.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments("selector", "RUVG001")]
    [Arguments("x => x.Name!.Trim()", "RUVG001")]
    [Arguments("x => x.Name![0]", "RUVG001")]
    [Arguments("x => x.Plain!.Name", "RUVG001")]
    public async Task UnsupportedSelectorsAreErrors(string selector, string diagnosticId)
    {
        var source = $$"""
            using System;
            using System.Linq.Expressions;
            using ReactiveUI;
            using ReactiveUI.Validation.Abstractions;
            using ReactiveUI.Validation.Contexts;
            using ReactiveUI.Validation.Extensions;
            public sealed class Plain { public string? Name { get; set; } }
            public sealed class Model : ReactiveObject, IValidatableViewModel
            {
                public string? Name { get; set; }
                public Plain? Plain { get; set; }
                public IValidationContext ValidationContext { get; } = new ValidationContext();
                public void Attach()
                {
                    Expression<Func<Model,string?>> selector = x => x.Name;
                    selector = DateTime.UtcNow.Ticks == 0 ? selector : x => x.Name;
                    this.ValidationRule({{selector}}, x => false, "bad");
                }
            }
            """;
        var result = Generate(source);
        await Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Id == diagnosticId && diagnostic.Severity == DiagnosticSeverity.Error)).IsTrue();
        await Assert.That(result.Generated).IsEmpty();
    }

    /// <summary>Preserves safe observable overloads and explicit reflection choices.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task ObservableAndUnsafeCallsAreNotIntercepted()
    {
        const string source = """
            using System;
            using ReactiveUI;
            using ReactiveUI.Validation.Abstractions;
            using ReactiveUI.Validation.Contexts;
            using ReactiveUI.Validation.Extensions;
            public sealed class Model : ReactiveObject, IValidatableViewModel
            {
                public string? Name { get; set; }
                public IValidationContext ValidationContext { get; } = new ValidationContext();
                public void Attach(IObservable<bool> states)
                {
                    this.ValidationRule(states, "bad");
                    this.ValidationRule(x => x.Name, states, "bad");
                    this.ValidationRuleUnsafe(x => x.Name, x => false, "bad");
                }
            }
            """;
        var result = Generate(source);
        await Assert.That(await result.Compilation.WithAnalyzers([new ValidationDispatchAnalyzer()]).GetAnalyzerDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.Diagnostics).IsEmpty();
        await Assert.That(result.Generated).IsEmpty();
        await Assert.That(result.Compilation.GetDiagnostics().Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsFalse();
    }

    /// <summary>Rejects a normal call when no generator ran.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [SuppressMessage("Security", "SES1402", Justification = "Only hard-coded compiler test sources and trusted runtime references are emitted into this in-memory test assembly.")]
    public async Task MissingGeneratorIsGuardedAndStubFailsLoudly()
    {
        const string source = """
            using ReactiveUI;
            using ReactiveUI.Validation.Abstractions;
            using ReactiveUI.Validation.Contexts;
            using ReactiveUI.Validation.Extensions;
            public sealed class Model : ReactiveObject, IValidatableViewModel
            {
                public string? Name { get; set; }
                public IValidationContext ValidationContext { get; } = new ValidationContext();
                public static void Attach() => new Model().ValidationRule(x => x.Name, x => false, "bad");
            }
            """;
        var result = Generate(source, includeValidationGenerator: false);
        var diagnostics = await result.Compilation.WithAnalyzers([new ValidationDispatchAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Single().Id).IsEqualTo(MissingDispatchId);
        await using var stream = new MemoryStream();
        await Assert.That(result.Compilation.Emit(stream).Success).IsTrue();
        var assembly = System.Reflection.Assembly.Load(stream.ToArray());
        Exception? failure = null;
        try
        {
            _ = assembly.GetType(ModelTypeName)!.GetMethod("Attach")!.Invoke(null, null);
        }
        catch (System.Reflection.TargetInvocationException exception)
        {
            failure = exception.InnerException;
        }

        await Assert.That(failure is InvalidOperationException).IsTrue();
        await Assert.That(failure!.Message).Contains("generator");
        await Assert.That(failure.Message).Contains("Unsafe");
    }

    /// <summary>Checks final dispatch after another generator creates an unresolved receiver.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task AnotherGeneratorCannotIntroduceUnguardedNormalCalls()
    {
        const string source = """
            using ReactiveUI;
            using ReactiveUI.Validation.Abstractions;
            using ReactiveUI.Validation.Contexts;
            using ReactiveUI.Validation.Extensions;
            public sealed class Model : ReactiveObject, IValidatableViewModel
            {
                public string? Name { get; set; }
                public IValidationContext ValidationContext { get; } = new ValidationContext();
                public static void Attach() => Factory.Model.ValidationRule(x => x.Name, x => false, "bad");
            }
            """;
        var result = Generate(source, new FactoryGenerator());
        await Assert.That(result.Compilation.GetDiagnostics().Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsFalse();
        var diagnostics = await result.Compilation.WithAnalyzers([new ValidationDispatchAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Any(static diagnostic => diagnostic.Id == MissingDispatchId)).IsTrue();
        await Assert.That(diagnostics.All(static diagnostic => diagnostic.Location.SourceTree!.FilePath == CallerPath)).IsTrue();
    }

    /// <summary>Preserves unrelated methods sharing the familiar name.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task LookalikeMethodsAreIgnored()
    {
        const string source = "public static class User { public static int ValidationRule() => 42; public static int Run() => ValidationRule(); }";
        var result = Generate(source);
        await Assert.That(result.Diagnostics).IsEmpty();
        await Assert.That(result.Generated).IsEmpty();
        await Assert.That(await result.Compilation.WithAnalyzers([new ValidationDispatchAnalyzer()]).GetAnalyzerDiagnosticsAsync()).IsEmpty();
    }

    /// <summary>Rejects a method group that bypasses direct call generation.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task NormalMethodGroupIsRejected()
    {
        const string source = """
            using System;
            using System.Linq.Expressions;
            using ReactiveUI;
            using ReactiveUI.Validation.Abstractions;
            using ReactiveUI.Validation.Contexts;
            using ReactiveUI.Validation.Extensions;
            using ReactiveUI.Validation.Helpers;
            public sealed class Model : ReactiveObject, IValidatableViewModel
            {
                public string? Name { get; set; }
                public IValidationContext ValidationContext { get; } = new ValidationContext();
                public static Func<Model, Expression<Func<Model, string?>>, Func<string?, bool>, string, ValidationHelper> Create()
                    => ValidatableViewModelExtensions.ValidationRule<Model, string>;
            }
            """;
        var result = Generate(source);
        var diagnostics = await result.Compilation.WithAnalyzers([new ValidationDispatchAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Any(static diagnostic => diagnostic.Id == "RUVG007")).IsTrue();
    }

    /// <summary>Accepts a declared partial property completed by another generator.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task DeclaredPartialPropertyIsSupported()
    {
        const string source = """
            using ReactiveUI;
            using ReactiveUI.Validation.Abstractions;
            using ReactiveUI.Validation.Contexts;
            using ReactiveUI.Validation.Extensions;
            public sealed partial class Model : ReactiveObject, IValidatableViewModel
            {
                public partial string? Name { get; set; }
                public IValidationContext ValidationContext { get; } = new ValidationContext();
                public static void Attach() => new Model().ValidationRule(x => x.Name, x => false, "bad");
            }
            """;
        var result = Generate(source, new PartialPropertyGenerator());
        await Assert.That(result.Diagnostics).IsEmpty();
        await Assert.That(result.Compilation.GetDiagnostics().Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsFalse();
        await Assert.That(await result.Compilation.WithAnalyzers([new ValidationDispatchAnalyzer()]).GetAnalyzerDiagnosticsAsync()).IsEmpty();
    }

    /// <summary>Executes a private model through its legal partial lexical host.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task PrivateModelInPartialHostCompilesAndNotifies()
    {
        const string source = """
            using ReactiveUI;
            using ReactiveUI.Validation.Abstractions;
            using ReactiveUI.Validation.Contexts;
            using ReactiveUI.Validation.Extensions;
            public static partial class Holder
            {
                public static bool Check() => Model.Check();
                private sealed partial class Model : ReactiveObject, IValidatableViewModel
                {
                    public string? Name { get; set => this.RaiseAndSetIfChanged(ref field, value); }
                    public IValidationContext ValidationContext { get; } = new ValidationContext();
                    public static bool Check()
                    {
                        var model = new Model();
                        using var rule = model.ValidationRule(x => x.Name, x => x == "ok", "bad");
                        var initial = !rule.IsValid;
                        model.Name = "ok";
                        var changed = rule.IsValid;
                        rule.Dispose();
                        model.Name = "detached";
                        return initial && changed && model.ValidationContext.Validations.Count == 0;
                    }
                }
            }
            """;
        using var host = CapabilityCompilerHost.Create(reactive: false);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean("Holder", FixtureMethodName)).IsTrue();
    }

    /// <summary>Executes an open generic partial host through its original API slots.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task GenericPartialContainingTypeCompilesAndNotifies()
    {
        const string source = """
            using ReactiveUI;
            using ReactiveUI.Validation.Abstractions;
            using ReactiveUI.Validation.Contexts;
            using ReactiveUI.Validation.Extensions;
            public partial class Outer<T>
            {
                public sealed partial class Model : ReactiveObject, IValidatableViewModel
                {
                    public string? Name { get; set => this.RaiseAndSetIfChanged(ref field, value); }
                    public IValidationContext ValidationContext { get; } = new ValidationContext();
                    public static bool Check()
                    {
                        var model = new Model();
                        using var rule = model.ValidationRule(x => x.Name, x => x == "ok", "bad");
                        var initial = !rule.IsValid;
                        model.Name = "ok";
                        return initial && rule.IsValid;
                    }
                }
            }
            public static class Fixture
            {
                public static bool Check() => Outer<string>.Model.Check() && Outer<int>.Model.Check();
            }
            """;
        using var host = CapabilityCompilerHost.Create(reactive: false);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean("Fixture", FixtureMethodName)).IsTrue();
    }

    /// <summary>Rejects missing final dispatch when a peer emits an undeclared late property.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task UndeclaredLatePropertyRequiresFinalDispatchContract()
    {
        const string source = """
            using ReactiveUI;
            using ReactiveUI.Validation.Abstractions;
            using ReactiveUI.Validation.Contexts;
            using ReactiveUI.Validation.Extensions;
            public sealed partial class Model : ReactiveObject, IValidatableViewModel
            {
                public IValidationContext ValidationContext { get; } = new ValidationContext();
                public static void Attach() => new Model().ValidationRule(x => x.Name, x => false, "bad");
            }
            """;
        var result = Generate(source, new OrdinaryPropertyGenerator());
        await Assert.That(result.Diagnostics).IsEmpty();
        await Assert.That(result.Compilation.GetDiagnostics().Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsFalse();
        var finalDiagnostics = await result.Compilation.WithAnalyzers([new ValidationDispatchAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(finalDiagnostics.Any(static diagnostic => diagnostic.Id == MissingDispatchId)).IsTrue();
        await Assert.That(finalDiagnostics.Single(static diagnostic => diagnostic.Id == MissingDispatchId).GetMessage()).Contains("Unsafe");
    }

    /// <summary>Rejects retaining borrowed ref-struct storage through a deferred snapshot callback.</summary>
    /// <param name="reactive">Whether the caller uses the System.Reactive flavor.</param>
    /// <returns>The asynchronous genuine compiler-negative assertions.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SnapshotStorageCannotEscapeIntoDeferredCallback(bool reactive)
    {
        var root = reactive ? ReactiveValidationNamespace : PrimitivesValidationNamespace;
        var source = $$"""
            using System;
            using {{root}}.Capabilities;
            public ref struct Packet(ReadOnlySpan<char> text)
            {
                public ReadOnlySpan<char> Text = text;
            }
            public static class Fixture
            {
                public static int Read()
                {
                    var packet = new Packet("borrowed");
                    Func<int> retained = () => ValidationSnapshot.Read(in packet,
                        static (scoped in Packet value) => value.Text.Length);
                    return retained();
                }
            }
            """;
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        var errors = result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        await Assert.That(errors.Length).IsEqualTo(1);
        await Assert.That(errors[0].Id).IsEqualTo("CS8175");
        await Assert.That(source.Substring(errors[0].Location.SourceSpan.Start, errors[0].Location.SourceSpan.Length)).IsEqualTo("packet");
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning)).IsEmpty();
    }

    /// <summary>Runs the generator over a real-package caller compilation.</summary>
    /// <param name="source">The original caller source.</param>
    /// <param name="extraGenerator">Another generator participating in the same compiler round.</param>
    /// <param name="includeValidationGenerator">Whether to run validation generation.</param>
    /// <returns>The final compilation and generation evidence.</returns>
    private static CompilationResult Generate(string source, IIncrementalGenerator? extraGenerator = null, bool includeValidationGenerator = true)
    {
        var references = CompilerTestReferences.CreateDefault();
        var compilation = CSharpCompilation.Create(
            $"GeneratedTest{Guid.NewGuid():N}",
            [CSharpSyntaxTree.ParseText(source, ParseOptions, CallerPath)],
            references,
            new(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var generators = new List<ISourceGenerator>();
        if (includeValidationGenerator)
        {
            generators.Add(new ValidationGenerator().AsSourceGenerator());
        }

        if (extraGenerator is not null)
        {
            generators.Add(extraGenerator.AsSourceGenerator());
        }

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generators, parseOptions: ParseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var generated, out var diagnostics);
        var result = driver.GetRunResult();
        return new(generated, diagnostics, string.Join("\n", result.GeneratedTrees.Select(static tree => tree.ToString())));
    }

    /// <summary>Creates a receiver absent from the original compilation.</summary>
    private sealed class FactoryGenerator : IIncrementalGenerator
    {
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Initialize(IncrementalGeneratorInitializationContext context) => context.RegisterSourceOutput(
            context.CompilationProvider,
            static (output, _) => output.AddSource("Factory.g.cs", "public static class Factory { public static Model Model { get; } = new(); }"));
    }

    /// <summary>Completes a property contract visible in the original source.</summary>
    private sealed class PartialPropertyGenerator : IIncrementalGenerator
    {
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Initialize(IncrementalGeneratorInitializationContext context) => context.RegisterSourceOutput(
            context.CompilationProvider,
            static (output, _) => output.AddSource("PartialProperty.g.cs", "#nullable enable\npublic sealed partial class Model { public partial string? Name { get => null; set { } } }"));
    }

    /// <summary>Adds an ordinary property absent from original source.</summary>
    private sealed class OrdinaryPropertyGenerator : IIncrementalGenerator
    {
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Initialize(IncrementalGeneratorInitializationContext context) => context.RegisterSourceOutput(
            context.CompilationProvider,
            static (output, _) => output.AddSource("OrdinaryProperty.g.cs", "#nullable enable\npublic sealed partial class Model { public string? Name { get; set; } }"));
    }

    /// <summary>Retains compiler and emission evidence.</summary>
    /// <param name="Compilation">The final generated compilation.</param>
    /// <param name="Diagnostics">The generator diagnostics.</param>
    /// <param name="Generated">The emitted source.</param>
    private sealed record CompilationResult(Compilation Compilation, ImmutableArray<Diagnostic> Diagnostics, string Generated);
}
