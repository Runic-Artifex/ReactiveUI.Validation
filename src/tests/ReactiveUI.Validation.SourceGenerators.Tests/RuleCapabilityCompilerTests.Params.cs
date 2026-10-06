// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Separates expanded-params expression construction from the authored Native AOT rule route.</summary>
public sealed partial class RuleCapabilityCompilerTests
{
    /// <summary>The existing four predicate and message/context selector forms.</summary>
    private const int PredicateRuleForms = 4;

    /// <summary>The ordinary inline params syntax selected by the normal typed overload.</summary>
    private const string InlineParamsRule = """
        using var spread = model.ValidationRule(x => x[x.Key,1,2],value => value == "2/1,2","params");
        """;

    /// <summary>The explicit retained expression API constructs the original expression-tree params array.</summary>
    private const string ExpressionParamsRule = """
        using var spread = model.ValidationRule((Expression<Func<Model,string?>>)(x => x[x.Key,1,2]),value => value == "2/1,2","params");
        """;

    /// <summary>The retained expression route reads a qualified static array without expression-tree array construction.</summary>
    private const string PreconstructedExpressionParamsRule = """
        using var spread = model.ValidationRule((Expression<Func<Model,string?>>)(x => x[x.Key,Model.Tail]),value => value == "2/1,2","params");
        """;

    /// <summary>The authored typed params route executes an ordinary C# array while preserving the index contract.</summary>
    private const string AuthoredParamsRule = """
            var paramsSelector = new ValidationSelector<Model,string?>(source => new ValidationAccessPlan<string?>(
                () => { var key = source.Key; var value = source[key,1,2]; return ValidationRead<string?>.Present(value,
                    new[] { ValidationPath.Structural("Item[]",(key,1,2),
                        System.Collections.Generic.EqualityComparer<(int,int,int)>.Default) }); },
                new[] { ValidationDependency.PropertyChanged(() => source,nameof(Key)),
                    ValidationDependency.PropertyChanged(() => source,"Item[]") },
                ValidationObservationOptions<string?>.Default));
            using var spread = ValidationRuntime.RegisterRule(model,context,paramsSelector,
                value => value == "2/1,2" ? ValidationState.Valid : new ValidationState(false,"params"));
        """;

    /// <summary>Retains managed interception coverage and proves the authored route removes expression-tree array construction.</summary>
    /// <param name="reactive">Whether to use the System.Reactive flavor.</param>
    /// <returns>The asynchronous compiler, emitted-metadata and runtime assertions.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task AuthoredParamsRulePreservesLifecycleWithoutExpressionArrayConstruction(bool reactive)
    {
        var automaticSource = ExpandFixture(IndexedBoundarySource, reactive).Replace(InlineParamsRule, ExpressionParamsRule, StringComparison.Ordinal);
        using var automaticHost = CapabilityCompilerHost.Create(reactive);
        var automatic = await automaticHost.RunAsync(automaticSource);
        await Assert.That(automatic.GeneratorDiagnostics).IsEmpty();
        await Assert.That(automatic.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(ReferencesExpressionArrayConstruction(automatic.Compilation)).IsTrue();
        await Assert.That(automatic.ExecuteBoolean(ModelEntryType, EntryMethod)).IsTrue();
        var authoredSource = automaticSource.Replace(ExpressionParamsRule, AuthoredParamsRule, StringComparison.Ordinal);
        using var authoredHost = CapabilityCompilerHost.Create(reactive);
        var authored = await authoredHost.RunAsync(authoredSource);
        await Assert.That(authored.GeneratorDiagnostics).IsEmpty();
        await Assert.That(authored.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await authored.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(ReferencesExpressionArrayConstruction(authored.Compilation)).IsFalse();
        await Assert.That(authored.ExecuteBoolean(ModelEntryType, EntryMethod)).IsTrue();
        await PreconstructedExpressionParamsPreservesLifecycleAsync(reactive);
    }

    /// <summary>Proves actual inline overload selection, emitted caller construction and the complete params lifecycle.</summary>
    /// <param name="reactive">Whether to use the System.Reactive flavor.</param>
    /// <returns>The asynchronous compiler, selected-symbol, metadata and runtime assertions.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task InlineParamsSelectsTypedNormalRuleWithoutExpressionArrayConstruction(bool reactive)
    {
        var source = ExpandFixture(IndexedBoundarySource, reactive);
        using var originalHost = CapabilityCompilerHost.Create(reactive, includeValidation: false);
        var original = await originalHost.RunAsync(source);
        await Assert.That(original.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await ParamsSelectorIsFuncAsync(original.Compilation)).IsTrue();
        await Assert.That(ReferencesExpressionArrayConstruction(original.Compilation)).IsFalse();
        using var generatedHost = CapabilityCompilerHost.Create(reactive);
        var generated = await generatedHost.RunAsync(source);
        await Assert.That(generated.GeneratorDiagnostics).IsEmpty();
        await Assert.That(generated.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await generated.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(await ParamsSelectorIsFuncAsync(generated.Compilation)).IsTrue();
        await Assert.That(ReferencesExpressionArrayConstruction(generated.Compilation)).IsFalse();
        await Assert.That(generated.ExecuteBoolean(ModelEntryType, EntryMethod)).IsTrue();
    }

    /// <summary>Verifies the retained expression API with qualified static params storage and exact lifecycle.</summary>
    /// <param name="reactive">Whether to use the System.Reactive flavor.</param>
    /// <returns>The asynchronous compiler, selected-signature, metadata and runtime assertions.</returns>
    private static async Task PreconstructedExpressionParamsPreservesLifecycleAsync(bool reactive)
    {
        var source = ExpandFixture(IndexedBoundarySource, reactive)
            .Replace("private int _key = 2;", "private int _key = 2; public static readonly int[] Tail = [1,2];", StringComparison.Ordinal)
            .Replace(InlineParamsRule, PreconstructedExpressionParamsRule, StringComparison.Ordinal);
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(await ParamsSelectorIsFuncAsync(result.Compilation)).IsFalse();
        await Assert.That(ReferencesExpressionArrayConstruction(result.Compilation)).IsFalse();
        await Assert.That(result.ExecuteBoolean(ModelEntryType, EntryMethod)).IsTrue();
    }

    /// <summary>Reads the compiler-selected signature for the original params call rather than its candidate set.</summary>
    /// <param name="compilation">The original or generated consumer compilation.</param>
    /// <returns>Whether the actual selected selector parameter is System.Func.</returns>
    /// <exception cref="InvalidOperationException">The params call has no selected method.</exception>
    private static async Task<bool> ParamsSelectorIsFuncAsync(Compilation compilation)
    {
        var tree = compilation.SyntaxTrees.First();
        var root = await tree.GetRootAsync();
        var model = compilation.GetSemanticModel(tree);
        var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().Single(node =>
            node.ArgumentList.Arguments.Count > 0
            && model.GetConstantValue(node.ArgumentList.Arguments.Last().Expression) is { HasValue: true, Value: "params" });
        var method = model.GetSymbolInfo(invocation).Symbol as IMethodSymbol
            ?? throw new InvalidOperationException("The params validation call has no selected method.");
        return method.Parameters.Single(static parameter => parameter.Name == "viewModelProperty").Type is INamedTypeSymbol selector
            && selector.MetadataName == "Func`2" && selector.ContainingNamespace.ToDisplayString() == "System";
    }

    /// <summary>Counts actual selected predicate signatures for all four message and context forms.</summary>
    /// <param name="compilation">The generated consumer compilation.</param>
    /// <param name="expression">Whether the retained expression API is expected.</param>
    /// <returns>The matching selected method count.</returns>
    private static async Task<int> CountSelectedRuleSelectorsAsync(Compilation compilation, bool expression)
    {
        var tree = compilation.SyntaxTrees.First();
        var root = await tree.GetRootAsync();
        var model = compilation.GetSemanticModel(tree);
        var expectedName = expression ? "Expression`1" : "Func`2";
        var expectedNamespace = expression ? "System.Linq.Expressions" : "System";
        return root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Select(node => model.GetSymbolInfo(node).Symbol).OfType<IMethodSymbol>()
            .Where(static method => method.Name == "ValidationRule")
            .Count(method => method.Parameters.Single(static parameter => parameter.Name == "viewModelProperty").Type is INamedTypeSymbol selector
                && selector.MetadataName == expectedName && selector.ContainingNamespace.ToDisplayString() == expectedNamespace);
    }

    /// <summary>Checks actual emitted member references, including compiler-generated argument construction.</summary>
    /// <param name="compilation">The generated consumer compilation.</param>
    /// <returns>Whether the actual assembly references Expression.NewArrayInit.</returns>
    /// <exception cref="InvalidOperationException">The consumer cannot be emitted for metadata inspection.</exception>
    private static bool ReferencesExpressionArrayConstruction(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics));
        }

        stream.Position = 0;
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        foreach (var handle in metadata.MemberReferences)
        {
            var member = metadata.GetMemberReference(handle);
            if (member.Parent.Kind is not HandleKind.TypeReference || metadata.GetString(member.Name) != "NewArrayInit")
            {
                continue;
            }

            var owner = metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
            if (metadata.GetString(owner.Namespace) != "System.Linq.Expressions" || metadata.GetString(owner.Name) != "Expression")
            {
                continue;
            }

            return true;
        }

        return false;
    }
}
