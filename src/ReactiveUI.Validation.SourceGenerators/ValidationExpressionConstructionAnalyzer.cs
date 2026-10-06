// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ReactiveUI.Validation.SourceGenerators;

/// <summary>Explains dynamic-code construction of retained expression arguments without hiding linker diagnostics.</summary>
internal static class ValidationExpressionConstructionAnalyzer
{
    /// <summary>Explains a real original argument-construction boundary separately from generated getter behavior.</summary>
    internal static readonly DiagnosticDescriptor DynamicConstruction = new(
        "RUVG011",
        "Validation expression argument requires dynamic construction",
        "This retained Expression selector constructs an array or calls a RequiresDynamicCode factory before interception. "
        + "Use the normal Func overload with the same inline lambda, a typed ValidationSelector/ValidationTarget, or a preconstructed array "
        + "for an expression params argument. The interceptor cannot remove original argument construction; NativeAOT IL3050 remains applicable.",
        "ReactiveUI.Validation.Generation",
        DiagnosticSeverity.Warning,
        true);

    /// <summary>Reports known construction on normal dispatch calls; observable metadata calls retain their own caller IL analysis.</summary>
    /// <param name="context">The final analyzer context.</param>
    /// <param name="invocation">The original invocation after all producer output is available.</param>
    internal static void Analyze(in SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        if (!IsAotContext(context.Options.AnalyzerConfigOptionsProvider.GlobalOptions)
            || context.SemanticModel.GetOperation(invocation, context.CancellationToken) is not IInvocationOperation call)
        {
            return;
        }

        foreach (var argument in call.Arguments)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (argument.Parameter is null || !ValidationCallClassifier.IsSelectorParameter(argument.Parameter)
                || !ValidationSelectorSignature.IsExpression(argument.Parameter.Type)
                || argument.Value.Syntax is not ExpressionSyntax expression)
            {
                continue;
            }

            if (HasDynamicFactory(argument.Value, context)
                || HasExpressionArray(context, expression))
            {
                context.ReportDiagnostic(Diagnostic.Create(DynamicConstruction, expression.GetLocation()));
            }
        }
    }

    /// <summary>Uses compiler-visible AOT intent rather than guessing from target frameworks or runtime availability.</summary>
    /// <param name="options">The actual compiler's global configuration.</param>
    /// <returns>Whether dynamic-code analysis was requested.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAotContext(AnalyzerConfigOptions options) =>
        Enabled(options, "build_property.PublishAot") || Enabled(options, "build_property.IsAotCompatible") || Enabled(options, "build_property.EnableAotAnalyzer");

    /// <summary>Reads an actual MSBuild boolean without interpreting missing or malformed values as enabled.</summary>
    /// <param name="options">The actual compiler's global configuration.</param>
    /// <param name="name">The compiler-visible property name.</param>
    /// <returns>Whether the property explicitly requests analysis.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Enabled(AnalyzerConfigOptions options, string name) =>
        options.TryGetValue(name, out var value) && bool.TryParse(value, out var enabled) && enabled;

    /// <summary>Examines only an actual finite expression body, including synthesized expanded-params arrays.</summary>
    /// <param name="context">The final analyzer context.</param>
    /// <param name="expression">The actual expression argument.</param>
    /// <returns>Whether expression-tree construction needs NewArrayInit or NewArrayBounds.</returns>
    private static bool HasExpressionArray(in SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
    {
        if (!SemanticSelectorPlanner.TryResolveLambda(context.SemanticModel, expression, out var model, out var lambda, out _)
            || model.GetOperation(lambda.Body, context.CancellationToken) is not { } body)
        {
            return false;
        }

        return HasArray(body, context);
    }

    /// <summary>Checks the real bound operations so preconstructed arrays and ordinary delegate arrays stay unaffected.</summary>
    /// <param name="operation">The actual expression-body operation.</param>
    /// <param name="context">The final analyzer context.</param>
    /// <returns>Whether a nested operation constructs an expression array.</returns>
    private static bool HasArray(IOperation operation, in SyntaxNodeAnalysisContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        if (operation is IArrayCreationOperation)
        {
            return true;
        }

        foreach (var child in operation.ChildOperations)
        {
            if (HasArray(child, context))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Checks executed argument factories without treating a method merely represented inside a tree as construction.</summary>
    /// <param name="operation">The original argument evaluation.</param>
    /// <param name="context">The final analyzer context.</param>
    /// <returns>Whether an executed factory carries a real RequiresDynamicCode contract.</returns>
    private static bool HasDynamicFactory(IOperation operation, in SyntaxNodeAnalysisContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        if (operation is IAnonymousFunctionOperation)
        {
            return false;
        }

        var method = operation switch
        {
            IInvocationOperation call => call.TargetMethod,
            IObjectCreationOperation creation => creation.Constructor,
            IPropertyReferenceOperation property => property.Property.GetMethod,
            IConversionOperation conversion => conversion.OperatorMethod,
            _ => null,
        };
        if (method is not null && RequiresDynamicCode(method, context.Compilation))
        {
            return true;
        }

        foreach (var child in operation.ChildOperations)
        {
            if (HasDynamicFactory(child, context))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Compares the actual framework attribute symbol instead of accepting a similarly named attribute.</summary>
    /// <param name="method">The executed factory.</param>
    /// <param name="compilation">The final compiler symbol universe.</param>
    /// <returns>Whether the factory declares dynamic-code requirements.</returns>
    private static bool RequiresDynamicCode(IMethodSymbol method, Compilation compilation)
    {
        var expected = compilation.GetTypeByMetadataName("System.Diagnostics.CodeAnalysis.RequiresDynamicCodeAttribute");
        if (expected is null)
        {
            return false;
        }

        foreach (var attribute in method.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, expected))
            {
                return true;
            }
        }

        return false;
    }
}
