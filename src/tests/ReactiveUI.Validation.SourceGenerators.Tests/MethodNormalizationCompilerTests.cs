// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ReactiveUI.Validation.SourceGenerators;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Checks exact CLR normalization independently of shipping-call classification.</summary>
public sealed class MethodNormalizationCompilerTests
{
    /// <summary>The preserved shipping normal-call CLR catalog size.</summary>
    private const int NormalMethodCount = 29;

    /// <summary>The added typed selector counterparts, excluding the selector-free retained shape.</summary>
    private const int CallableMethodCount = 28;

    /// <summary>The normal extension-form catalog size, excluding static factories.</summary>
    private const int NormalExtensionCount = 18;

    /// <summary>The C#14 logical normal extensions; bindings use traditional extension methods.</summary>
    private const int LogicalExtensionCount = 4;

    /// <summary>The retained expression selector classification.</summary>
    private const string ExpressionForm = "Expression";

    /// <summary>The added callable selector classification.</summary>
    private const string CallableForm = "Func";

    /// <summary>Foreign same-name, nongeneric, and generic extension controls.</summary>
    private const string ExtensionSource = """
        using System;
        namespace ForeignExtensions;
        public static class ValidatableViewModelExtensions
        {
            extension(string source)
            {
                public int ValidationRule() => source.Length;
                public int ValidationRule(Func<int,bool> isPropertyValid) => isPropertyValid(source.Length) ? source.Length : 0;
                public string Copy(string value) => source + value;
                public T Identity<T>(T value) => value;
            }
        }
        """;

    /// <summary>All bound invocations must normalize even when they are unrelated to normal validation.</summary>
    private const string ConsumerSource = """
        using ForeignExtensions;
        public static class Consumer
        {
            public static bool Check() => "abc".ValidationRule() == 3
                && "abc".ValidationRule(static length => length > 0) == 3
                && "abc".Copy("d") == "abcd" && "abc".Identity(7) == 7;
        }
        """;

    /// <summary>The display format preserves each formal's nullable contract.</summary>
    private static readonly SymbolDisplayFormat ContractFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>The exact shipping normal entry-point owners in either runtime flavor.</summary>
    private static readonly string[] NormalOwners =
    [
        "Extensions.ValidatableViewModelExtensions", "Extensions.ValidationRuleContextExtensions", "Extensions.ViewForExtensions",
        "Extensions.ValidationContextBindingExtensions", "Extensions.ValidationStateBindingExtensions", "ValidationBindings.ValidationBinding",
    ];

    /// <summary>Normalizes zero-arity shims from both source and real implementation metadata without classifying foreign calls.</summary>
    /// <param name="metadata">Whether to import the extension through an emitted implementation assembly.</param>
    /// <returns>The asynchronous symbol and compilation assertions.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ZeroArityExtensionsNormalizeWithoutInventingGenericConstruction(bool metadata)
    {
        using var host = CapabilityCompilerHost.Create(false, includeValidation: false);
        var baseline = await host.RunAsync("public sealed class Empty { }");
        var options = (CSharpParseOptions)baseline.Compilation.SyntaxTrees.First().Options;
        var apiTree = CSharpSyntaxTree.ParseText(ExtensionSource, options);
        var api = baseline.Compilation.RemoveAllSyntaxTrees().WithAssemblyName($"ForeignExtensions{Guid.NewGuid():N}").AddSyntaxTrees(apiTree);
        var consumer = baseline.Compilation.RemoveAllSyntaxTrees().AddSyntaxTrees(CSharpSyntaxTree.ParseText(ConsumerSource, options));
        if (metadata)
        {
            await using var stream = new MemoryStream();
            var emitted = api.Emit(stream);
            await Assert.That(Problems(emitted.Diagnostics)).IsEmpty();
            await Assert.That(emitted.Success).IsTrue();
            consumer = consumer.AddReferences(MetadataReference.CreateFromImage(stream.ToArray()));
        }
        else
        {
            consumer = consumer.AddSyntaxTrees(apiTree);
        }

        await Assert.That(Problems(consumer.GetDiagnostics())).IsEmpty();
        var tree = consumer.SyntaxTrees.First();
        var model = consumer.GetSemanticModel(tree);
        foreach (var invocation in (await tree.GetRootAsync()).DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var resolved = (IMethodSymbol)model.GetSymbolInfo(invocation).Symbol!;
            await Assert.That(resolved.ContainingType.IsExtension).IsTrue();
            var normalized = ValidationCallClassifier.NormalizeMethod(resolved)!;
            await Assert.That(normalized).IsNotNull();
            await Assert.That(normalized.Name).IsEqualTo(resolved.Name);
            await Assert.That(normalized.Parameters.Length).IsEqualTo(resolved.Parameters.Length + 1);
            await Assert.That(normalized.IsGenericMethod).IsEqualTo(resolved.IsGenericMethod);
            await Assert.That(SymbolEqualityComparer.Default.Equals(normalized.ReturnType, resolved.ReturnType)).IsTrue();
            await Assert.That(ValidationCallClassifier.IsNormalMethod(normalized)).IsFalse();
        }
    }

    /// <summary>Preserves the frozen original catalog and verifies every prioritized callable counterpart.</summary>
    /// <param name="reactive">Whether to use the System.Reactive flavor.</param>
    /// <returns>The asynchronous shipping-catalog assertions.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CompleteShippingNormalCatalogKeepsExactClassification(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive, includeValidation: false);
        var result = await host.RunAsync("public sealed class Empty { }");
        var root = reactive ? "ReactiveUI.Validation.Reactive" : "ReactiveUI.Validation";
        var owners = NormalOwners.Select(name => result.Compilation.GetTypeByMetadataName($"{root}.{name}")!).ToArray();
        var normal = owners.SelectMany(static owner => owner.GetMembers().OfType<IMethodSymbol>()).Where(ValidationCallClassifier.IsNormalMethod).ToArray();
        var retained = normal.Where(static method => ValidationCallClassifier.SelectorForm(method) != CallableForm).ToArray();
        var callable = normal.Where(static method => ValidationCallClassifier.SelectorForm(method) == CallableForm).ToArray();
        await Assert.That(retained.Length).IsEqualTo(NormalMethodCount);
        await Assert.That(callable.Length).IsEqualTo(CallableMethodCount);
        await Assert.That(retained.Count(static method => ValidationCallClassifier.SelectorForm(method) == ExpressionForm)).IsEqualTo(CallableMethodCount);
        await Assert.That(retained.Count(static method => ValidationCallClassifier.SelectorForm(method) == "None")).IsEqualTo(1);
        await Assert.That(retained.Count(static method => method.IsExtensionMethod)).IsEqualTo(NormalExtensionCount);
        await Assert.That(callable.Count(static method => method.IsExtensionMethod)).IsEqualTo(NormalExtensionCount);
        await AssertRetainedIdentitiesAsync(retained, root);
        await AssertCallablePairsAsync(retained, callable);
        foreach (var method in normal)
        {
            await Assert.That(SymbolEqualityComparer.Default.Equals(ValidationCallClassifier.NormalizeMethod(method), method)).IsTrue();
        }

        var logical = owners.SelectMany(static owner => owner.GetTypeMembers().Where(static type => type.IsExtension))
            .SelectMany(static type => type.GetMembers().OfType<IMethodSymbol>()).Where(ValidationCallClassifier.IsNormalMethod).ToArray();
        await Assert.That(logical.Count(static method => ValidationCallClassifier.SelectorForm(method) == ExpressionForm)).IsEqualTo(LogicalExtensionCount);
        await Assert.That(logical.Count(static method => ValidationCallClassifier.SelectorForm(method) == CallableForm)).IsEqualTo(LogicalExtensionCount);
        await Assert.That(logical.Length).IsEqualTo(LogicalExtensionCount + LogicalExtensionCount);
        foreach (var method in logical)
        {
            var normalized = ValidationCallClassifier.NormalizeMethod(method)!;
            await Assert.That(normalized).IsNotNull();
            await Assert.That(ValidationCallClassifier.IsNormalMethod(normalized)).IsTrue();
            await Assert.That(normal.Contains(normalized.OriginalDefinition, SymbolEqualityComparer.Default)).IsTrue();
        }
    }

    /// <summary>Compares retained metadata against the independently frozen original shipping IDs.</summary>
    /// <param name="retained">The preserved expression and selector-free methods.</param>
    /// <param name="root">The matching runtime namespace.</param>
    /// <returns>The asynchronous exact identity assertions.</returns>
    private static async Task AssertRetainedIdentitiesAsync(IMethodSymbol[] retained, string root)
    {
        var text = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "original-normal-api.json"));
        using var baseline = JsonDocument.Parse(text);
        var expected = baseline.RootElement.GetProperty("originalNormalMethodIds").EnumerateArray().Select(static id => id.GetString()!).ToArray();
        await Assert.That(expected.Length).IsEqualTo(NormalMethodCount);
        var actual = retained.Select(method => method.GetDocumentationCommentId()!.Replace(root, "{ROOT}", StringComparison.Ordinal));
        await Assert.That(actual).IsEquivalentTo(expected);
    }

    /// <summary>Checks one-to-one selector roles, complete formal signatures, priorities and generic constraints.</summary>
    /// <param name="retained">The preserved original catalog.</param>
    /// <param name="callable">The added typed selector catalog.</param>
    /// <returns>The asynchronous counterpart assertions.</returns>
    private static async Task AssertCallablePairsAsync(IMethodSymbol[] retained, IMethodSymbol[] callable)
    {
        var expressions = retained.Where(static method => ValidationCallClassifier.SelectorForm(method) == ExpressionForm)
            .ToDictionary(PairSignature, StringComparer.Ordinal);
        await Assert.That(callable.Select(PairSignature).Distinct().Count()).IsEqualTo(CallableMethodCount);
        foreach (var method in callable)
        {
            var original = expressions[PairSignature(method)];
            await Assert.That(Priority(method)).IsEqualTo(1);
            await Assert.That(Priority(original)).IsEqualTo(0);
            await Assert.That(method.IsExtensionMethod).IsEqualTo(original.IsExtensionMethod);
            foreach (var pair in method.TypeParameters.Zip(original.TypeParameters))
            {
                await Assert.That(ConstraintSignature(pair.First)).IsEqualTo(ConstraintSignature(pair.Second));
            }
        }
    }

    /// <summary>Erases only the selector wrapper while retaining roles, slots, outputs and optional inputs.</summary>
    /// <param name="method">The exact original or callable CLR method.</param>
    /// <returns>The complete counterpart identity.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string PairSignature(IMethodSymbol method) =>
        $"{method.ContainingType.ToDisplayString()}.{method.Name}`{method.Arity}:{method.ReturnType.ToDisplayString(ContractFormat)}"
        + $"({string.Join(",", method.Parameters.Select(ParameterSignature))})";

    /// <summary>Retains every formal role and unwraps only actual expression selectors.</summary>
    /// <param name="parameter">The shipping formal parameter.</param>
    /// <returns>The exact signature contract shared by its two selector forms.</returns>
    private static string ParameterSignature(IParameterSymbol parameter)
    {
        var type = parameter.Type;
        if (ValidationCallClassifier.IsSelectorParameter(parameter) && ValidationSelectorSignature.IsExpression(type))
        {
            type = ((INamedTypeSymbol)type).TypeArguments[0];
        }

        return $"{parameter.Name}:{parameter.RefKind}:{type.ToDisplayString(ContractFormat)}:{parameter.IsOptional}"
            + $":{(parameter.HasExplicitDefaultValue ? parameter.ExplicitDefaultValue : string.Empty)}";
    }

    /// <summary>Preserves each original generic slot's storage and nullable restrictions.</summary>
    /// <param name="parameter">The original or counterpart generic slot.</param>
    /// <returns>The complete constraint contract.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string ConstraintSignature(ITypeParameterSymbol parameter) =>
        $"{parameter.Ordinal}:{parameter.HasConstructorConstraint}:{parameter.HasReferenceTypeConstraint}:{parameter.ReferenceTypeConstraintNullableAnnotation}"
        + $":{parameter.HasValueTypeConstraint}:{parameter.HasUnmanagedTypeConstraint}:{parameter.HasNotNullConstraint}:{parameter.AllowsRefLikeType}"
        + $":{string.Join(",", parameter.ConstraintTypes.Select(static type => type.ToDisplayString(ContractFormat)))}";

    /// <summary>Reads the actual emitted priority, including the retained default of zero.</summary>
    /// <param name="method">The shipping CLR method.</param>
    /// <returns>The emitted overload selection priority.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Priority(IMethodSymbol method) => method.GetAttributes()
        .Where(static attribute => attribute.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.OverloadResolutionPriorityAttribute")
        .Select(static attribute => (int)attribute.ConstructorArguments[0].Value!).DefaultIfEmpty(0).Single();

    /// <summary>Formats every compiler warning and error.</summary>
    /// <param name="diagnostics">The actual compiler diagnostics.</param>
    /// <returns>The warning and error text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Problems(IEnumerable<Diagnostic> diagnostics) => string.Join("\n", diagnostics
        .Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error));
}
