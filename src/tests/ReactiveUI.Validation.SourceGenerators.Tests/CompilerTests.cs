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
        var root = reactive ? "ReactiveUI.Validation.Reactive" : "ReactiveUI.Validation";
        var ui = reactive ? "ReactiveUI.Reactive" : "ReactiveUI";
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
        await Assert.That(result.Generated).Contains("GeneratedValidationObservation.Observe");
        await Assert.That(result.Generated).Contains("Address.Name");
        await Assert.That(result.Generated).DoesNotContain("WhenAnyValue");
        await using var stream = new MemoryStream();
        var emit = result.Compilation.Emit(stream);
        await Assert.That(string.Join("\n", emit.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))).IsEmpty();
        var assembly = System.Reflection.Assembly.Load(stream.ToArray());
        await Assert.That((bool)assembly.GetType("Model")!.GetMethod("Check")!.Invoke(null, null)!).IsTrue();
    }

    /// <summary>Rejects unsupported selectors at their source call sites.</summary>
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
            _ = assembly.GetType("Model")!.GetMethod("Attach")!.Invoke(null, null);
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

    /// <summary>Rejects closed call types that generated namespace code cannot access.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task InaccessibleModelTypeIsDiagnosed()
    {
        const string source = """
            using ReactiveUI;
            using ReactiveUI.Validation.Abstractions;
            using ReactiveUI.Validation.Contexts;
            using ReactiveUI.Validation.Extensions;
            public static class Holder
            {
                private sealed class Model : ReactiveObject, IValidatableViewModel
                {
                    public string? Name { get; set; }
                    public IValidationContext ValidationContext { get; } = new ValidationContext();
                    public void Attach() => this.ValidationRule(x => x.Name, x => false, "bad");
                }
            }
            """;
        var result = Generate(source);
        await Assert.That(result.Diagnostics.Single().Id).IsEqualTo("RUVG002");
        await Assert.That(result.Diagnostics.Single().Location.SourceTree!.FilePath).IsEqualTo(CallerPath);
    }

    /// <summary>Rejects open generic containing types before emitting namespace code.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task GenericContainingTypeIsDiagnosed()
    {
        const string source = """
            using ReactiveUI;
            using ReactiveUI.Validation.Abstractions;
            using ReactiveUI.Validation.Contexts;
            using ReactiveUI.Validation.Extensions;
            public class Outer<T>
            {
                public sealed class Model : ReactiveObject, IValidatableViewModel
                {
                    public string? Name { get; set; }
                    public IValidationContext ValidationContext { get; } = new ValidationContext();
                    public void Attach() => this.ValidationRule(x => x.Name, x => false, "bad");
                }
            }
            """;
        var result = Generate(source);
        await Assert.That(result.Diagnostics.Single().Id).IsEqualTo("RUVG002");
        await Assert.That(result.Generated).IsEmpty();
    }

    /// <summary>Rejects a selector whose property exists only in another generator's output.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task GeneratedOnlyPropertyRequiresDeclaredContract()
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
        await Assert.That(result.Diagnostics.Any(static diagnostic => diagnostic.Id == "RUVG001")).IsTrue();
        await Assert.That(result.Compilation.GetDiagnostics().Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsFalse();
        var finalDiagnostics = await result.Compilation.WithAnalyzers([new ValidationDispatchAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(finalDiagnostics.Any(static diagnostic => diagnostic.Id == MissingDispatchId)).IsTrue();
    }

    /// <summary>Runs the generator over a real-package caller compilation.</summary>
    /// <param name="source">The original caller source.</param>
    /// <param name="extraGenerator">Another generator participating in the same compiler round.</param>
    /// <param name="includeValidationGenerator">Whether to run validation generation.</param>
    /// <returns>The final compilation and generation evidence.</returns>
    private static CompilationResult Generate(string source, IIncrementalGenerator? extraGenerator = null, bool includeValidationGenerator = true)
    {
        var references = Directory.GetFiles(AppContext.BaseDirectory, "*.dll")
            .Concat(((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator))
            .Distinct().Select(static path => MetadataReference.CreateFromFile(path));
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
