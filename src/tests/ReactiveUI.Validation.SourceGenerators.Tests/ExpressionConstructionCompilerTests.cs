// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Preserves the original expression-construction boundary separately from interceptor execution.</summary>
public sealed class ExpressionConstructionCompilerTests
{
    /// <summary>The expanded params construction control.</summary>
    private const string ExpandedKind = "expanded";

    /// <summary>The original caller path supplied to the compiler host.</summary>
    private const string CallerPath = "CapabilityCaller.cs";

    /// <summary>The actual shipping normal rule method.</summary>
    private const string RuleName = "ValidationRule";

    /// <summary>The trusted executable fixture type.</summary>
    private const string ModelName = "Model";

    /// <summary>The fixture's Boolean execution entry point.</summary>
    private const string CheckName = "Check";

    /// <summary>Explains expanded expression params arrays at their original normal argument in an AOT context.</summary>
    /// <param name="reactive">Whether to use the System.Reactive flavor.</param>
    /// <param name="factory">Whether an executed factory explicitly declares dynamic-code requirements.</param>
    /// <returns>The asynchronous compiler and final analyzer assertions.</returns>
    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task RetainedExpressionConstructionHasPreciseActionableDiagnostic(bool reactive, bool factory)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(Source(reactive, factory ? "factory" : ExpandedKind), globalOptions: AotOptions(true));
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        var diagnostics = await result.GetDispatchDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(1);
        var diagnostic = diagnostics[0];
        await Assert.That(diagnostic.Id).IsEqualTo("RUVG011");
        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Warning);
        await Assert.That(diagnostic.GetMessage()).Contains("Func");
        await Assert.That(diagnostic.GetMessage()).Contains("IL3050");
        var tree = result.Compilation.SyntaxTrees.First(static item => item.FilePath == CallerPath);
        var call = (await tree.GetRootAsync()).DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Single(static item => item.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: RuleName });
        await Assert.That(diagnostic.Location.SourceTree).IsEqualTo(tree);
        await Assert.That(diagnostic.Location.SourceSpan).IsEqualTo(call.ArgumentList.Arguments[0].Expression.Span);
        await Assert.That(result.ExecuteBoolean(ModelName, CheckName)).IsTrue();
    }

    /// <summary>Keeps preconstructed expression arrays and ordinary non-AOT expression calls usable.</summary>
    /// <param name="reactive">Whether to use the System.Reactive flavor.</param>
    /// <param name="aot">Whether the compiler received actual AOT intent.</param>
    /// <returns>The asynchronous generated and final analyzer assertions.</returns>
    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task SafeExpressionConstructionControlsHaveNoDiagnostic(bool reactive, bool aot)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(Source(reactive, aot ? "preconstructed" : ExpandedKind), globalOptions: AotOptions(aot));
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(ModelName, CheckName)).IsTrue();
    }

    /// <summary>Reports a directly cast retained expression at the original cast argument.</summary>
    /// <param name="reactive">Whether to use the System.Reactive flavor.</param>
    /// <returns>The asynchronous real compiler and final analyzer assertions.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ExplicitExpressionCastRetainsConstructionBoundary(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(Source(reactive, "cast"), globalOptions: AotOptions(true));
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        var diagnostics = await result.GetDispatchDiagnosticsAsync();
        await Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id)).IsEquivalentTo(["RUVG011"]);
        var tree = result.Compilation.SyntaxTrees.First(static item => item.FilePath == CallerPath);
        var call = (await tree.GetRootAsync()).DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Single(static item => item.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: RuleName });
        await Assert.That(diagnostics[0].Location.SourceSpan).IsEqualTo(call.ArgumentList.Arguments[0].Expression.Span);
        await Assert.That(result.ExecuteBoolean(ModelName, CheckName)).IsTrue();
    }

    /// <summary>Keeps expanded delegate params usable without expression-tree construction.</summary>
    /// <param name="reactive">Whether to use the System.Reactive flavor.</param>
    /// <returns>The asynchronous selected-signature, analyzer and execution assertions.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PriorityFuncArrayHasNoExpressionConstructionDiagnostic(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(Source(reactive, "func"), globalOptions: AotOptions(true));
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        var tree = result.Compilation.SyntaxTrees.First(static item => item.FilePath == CallerPath);
        var call = (await tree.GetRootAsync()).DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Single(static item => item.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: RuleName });
        var method = (IMethodSymbol)result.Compilation.GetSemanticModel(tree).GetSymbolInfo(call).Symbol!;
        await Assert.That(ValidationCallClassifier.SelectorForm(method)).IsEqualTo("Func");
        await Assert.That(result.ExecuteBoolean(ModelName, CheckName)).IsTrue();
    }

    /// <summary>Separates safe observable metadata dispatch from caller expression-construction safety.</summary>
    /// <param name="reactive">Whether to use the System.Reactive flavor.</param>
    /// <returns>The asynchronous compiler and final dispatch assertions; no NativeAOT safety claim.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SafeObservableMetadataIsOutsideNormalConstructionDiagnostic(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var source = Source(reactive, ExpandedKind).Replace(
            "model.ValidationRule(selector, static value => value == \"3/1,2\", \"construction\")",
            "model.ValidationRule(selector, new ValuesObservable(), \"construction\")",
            StringComparison.Ordinal)
            + "public sealed class ValuesObservable : IObservable<bool> { public IDisposable Subscribe(IObserver<bool> observer) => throw new NotImplementedException(); }";
        var result = await host.RunAsync(source, globalOptions: AotOptions(true));
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
    }

    /// <summary>Supplies the same compiler-visible booleans that the package props export to the final analyzer.</summary>
    /// <param name="enabled">Whether AOT analysis is requested.</param>
    /// <returns>The explicit global options for this invocation.</returns>
    private static Dictionary<string, string> AotOptions(bool enabled) => new() { ["build_property.PublishAot"] = enabled ? "true" : "false" };

    /// <summary>Builds an actual normal retained-expression call with a known construction receipt.</summary>
    /// <param name="reactive">Whether to use System.Reactive namespaces.</param>
    /// <param name="kind">The explicit expression construction control.</param>
    /// <returns>The original caller source processed by all real producers and Validation.</returns>
    private static string Source(bool reactive, string kind)
    {
        var root = reactive ? "ReactiveUI.Validation.Reactive" : "ReactiveUI.Validation";
        var declaration = kind is "factory" or "cast" or "func" ? string.Empty : $"Expression<Func<Model,string?>> selector = x => x[x.Key,{(kind == ExpandedKind ? "1,2" : "Model.Values")}];";
        var argument = kind switch
        {
            "factory" => "Factory()",
            "cast" => "(Expression<Func<Model,string?>>)(x => x[x.Key,1,2])",
            "func" => "x => x[x.Key,1,2]",
            _ => "selector",
        };
        return $$"""
            using System;
            using System.Linq.Expressions;
            using System.Diagnostics.CodeAnalysis;
            using {{(reactive ? "ReactiveUI.Reactive" : "ReactiveUI")}};
            using {{root}}.Abstractions;
            using {{root}}.Contexts;
            using {{root}}.Extensions;
            public sealed class Model : ReactiveObject, IValidatableViewModel
            {
                public static readonly int[] Values = [1,2];
                public int Key { get; } = 3;
                public string this[int first,params int[] rest] => first + "/" + string.Join(",",rest);
                public IValidationContext ValidationContext { get; } = new ValidationContext();
                [RequiresDynamicCode("The fixture declares an executed argument factory contract.")]
                public static Expression<Func<Model,string?>> Factory() => x => x[x.Key,Model.Values];
                public static bool Check()
                {
                    var model = new Model();
                    {{declaration}}
                    using var rule = model.ValidationRule({{argument}}, static value => value == "3/1,2", "construction");
                    return rule.IsValid;
                }
            }
            """;
    }
}
