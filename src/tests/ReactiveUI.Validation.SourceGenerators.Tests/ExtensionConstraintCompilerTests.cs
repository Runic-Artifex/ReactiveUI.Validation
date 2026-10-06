// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ReactiveUI.Validation.SourceGenerators;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Preserves logical extension constraints across implementation metadata and CLR shim normalization.</summary>
public sealed class ExtensionConstraintCompilerTests
{
    /// <summary>The unchanged original rule API generic arity.</summary>
    private const int RuleArity = 2;

    /// <summary>The real nullable-struct rule consumer.</summary>
    private const string NullableRuleSource = """
        using System.ComponentModel;
        using System.Linq;
        using {{ui}};
        using {{root}}.Abstractions;
        using {{root}}.Contexts;
        using {{root}}.Extensions;
        using {{root}}.Helpers;
        public partial class Outer<T> where T : struct
        {
            private sealed partial class Model : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
            {
                private T? Secret { get; set; }
                public new event PropertyChangedEventHandler? PropertyChanged;
                public IValidationContext ValidationContext { get; } = new ValidationContext();
                public ValidationHelper Attach() => this.ValidationRule(x => x.Secret, value => value.HasValue, "required");
                public void Set(T? value) { Secret = value; PropertyChanged?.Invoke(this, new(nameof(Secret))); }
            }
            public static bool Check(T current)
            {
                var model = new Model();
                using var rule = model.Attach();
                if (rule.IsValid) return false;
                model.Set(current);
                if (!rule.IsValid || !model.ValidationContext.GetIsValid()) return false;
                model.Set(null);
                if (rule.IsValid) return false;
                rule.Dispose();
                model.Set(current);
                return !model.ValidationContext.Validations.Items.Any();
            }
        }
        public static class Fixture { public static bool Check() => Outer<int>.Check(7); }
        """;

    /// <summary>The independently compiled extension API with an optional genuine constraint.</summary>
    private const string MetadataApi = """
        using System;
        namespace MetadataExtensions;
        public static class Extensions
        {
            extension<TSource>(TSource source) where TSource : class
            {
                public bool Probe<TValue>(Func<TSource,TValue?> selector, Func<TValue?,bool> predicate) {{constraint}}
                    => predicate(selector(source));
            }
        }
        """;

    /// <summary>The open consumer forces an original-arity interceptor declaration.</summary>
    private const string MetadataConsumer = """
        using MetadataExtensions;
        public partial class Owner<T> where T : struct
        {
            private sealed partial class Model
            {
                private T Value { get; set; }
                public bool Attach() => this.Probe(value => value.Value, static value => true);
            }
        }
        """;

    /// <summary>Checks the admitted normal rule before generation and executes nullable updates and disposal.</summary>
    /// <param name="reactive">Whether to use the System.Reactive flavor.</param>
    /// <returns>The asynchronous compiler and execution assertions.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task NullableStructRuleKeepsBoundExtensionContract(bool reactive)
    {
        var root = reactive ? "ReactiveUI.Validation.Reactive" : "ReactiveUI.Validation";
        var ui = reactive ? "ReactiveUI.Reactive" : "ReactiveUI";
        var source = NullableRuleSource.Replace("{{root}}", root, StringComparison.Ordinal).Replace("{{ui}}", ui, StringComparison.Ordinal);
        using var originalHost = CapabilityCompilerHost.Create(reactive, includeValidation: false);
        var original = await originalHost.RunAsync(source);
        await Assert.That(Problems(original.CompilationDiagnostics)).IsEmpty();
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(Problems(result.CompilationDiagnostics)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        var tree = result.Compilation.SyntaxTrees.Single(static tree => tree.FilePath.EndsWith("ValidationInterceptors.g.cs", StringComparison.Ordinal));
        var interceptor = (await tree.GetRootAsync()).DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Single(static method => method.Identifier.ValueText == "Intercept0");
        var method = result.Compilation.GetSemanticModel(interceptor.SyntaxTree).GetDeclaredSymbol(interceptor)!;
        await Assert.That(method.TypeParameters.Length).IsEqualTo(RuleArity);
        await Assert.That(method.TypeParameters[0].HasReferenceTypeConstraint).IsTrue();
        await Assert.That(method.TypeParameters[1].HasNotNullConstraint).IsFalse();
        await Assert.That(result.ExecuteBoolean("Fixture", "Check")).IsTrue();
    }

    /// <summary>Retains real constraints while normalizing both logical owner and method parameter positions.</summary>
    /// <param name="constrained">Whether the original extension explicitly requires notnull.</param>
    /// <returns>The asynchronous implementation-metadata assertions.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ImplementationMetadataKeepsGenuineConstraints(bool constrained)
    {
        using var host = CapabilityCompilerHost.Create(false, includeValidation: false);
        var baseline = await host.RunAsync("public sealed class Empty { }");
        var api = baseline.Compilation.RemoveAllSyntaxTrees().WithAssemblyName($"MetadataExtension{Guid.NewGuid():N}")
            .AddSyntaxTrees(CSharpSyntaxTree.ParseText(
                MetadataApi.Replace("{{constraint}}", constrained ? "where TValue : notnull" : string.Empty, StringComparison.Ordinal),
                (CSharpParseOptions)baseline.Compilation.SyntaxTrees.First().Options));
        await using var stream = new MemoryStream();
        var emitted = api.Emit(stream);
        await Assert.That(emitted.Success).IsTrue();
        var consumer = baseline.Compilation.RemoveAllSyntaxTrees().AddReferences(MetadataReference.CreateFromImage(stream.ToArray()))
            .AddSyntaxTrees(CSharpSyntaxTree.ParseText(MetadataConsumer, (CSharpParseOptions)baseline.Compilation.SyntaxTrees.First().Options));
        await Assert.That(Problems(consumer.GetDiagnostics())).IsEmpty();
        var tree = consumer.SyntaxTrees.Single();
        var invocation = (await tree.GetRootAsync()).DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        var model = consumer.GetSemanticModel(tree);
        var logical = (IMethodSymbol)model.GetSymbolInfo(invocation).Symbol!;
        var normalized = ValidationCallClassifier.NormalizeMethod(logical)!;
        await Assert.That(logical.ContainingType.IsExtension).IsTrue();
        await Assert.That(logical.OriginalDefinition.TypeParameters[0].HasNotNullConstraint).IsEqualTo(constrained);
        var site = new CallSite(model, invocation, normalized, "ReactiveUI.Validation", string.Empty, 0);
        var header = GenericInterceptorEmitter.MethodHeader(site);
        await Assert.That(header.Contains("where @TSource : class", StringComparison.Ordinal)).IsTrue();
        await Assert.That(header.Contains("where @TValue : notnull", StringComparison.Ordinal)).IsEqualTo(constrained);
        var declaration = CSharpSyntaxTree.ParseText($"internal static class Interceptor {{ {header} => @predicate(@selector(@source)); }}", (CSharpParseOptions)tree.Options);
        await Assert.That(Problems(consumer.AddSyntaxTrees(declaration).GetDiagnostics())).IsEmpty();
        var nullable = consumer.RemoveAllSyntaxTrees().AddSyntaxTrees(CSharpSyntaxTree.ParseText(
            MetadataConsumer.Replace("private T Value", "private T? Value", StringComparison.Ordinal),
            (CSharpParseOptions)tree.Options));
        await Assert.That(nullable.GetDiagnostics().Any(static diagnostic => diagnostic.Id == "CS8714")).IsEqualTo(constrained);
    }

    /// <summary>Formats every compiler warning and error for useful failure evidence.</summary>
    /// <param name="diagnostics">The actual compiler diagnostics.</param>
    /// <returns>The warning and error messages.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Problems(IEnumerable<Diagnostic> diagnostics) => string.Join("\n", diagnostics
        .Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error));
}
