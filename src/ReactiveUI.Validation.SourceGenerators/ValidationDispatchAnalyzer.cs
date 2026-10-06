// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using ReactiveUI.Validation.SourceGenerators.Producers;

namespace ReactiveUI.Validation.SourceGenerators;

/// <summary>Rejects normal validation stubs that remain unintercepted after all generators run.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ValidationDispatchAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Validates and formats the original compilation contract.</summary>
    private static readonly DiagnosticDescriptor MissingDispatch = new(
        "RUVG005",
        "Validation generation is missing",
        "This normal validation call has no generated interceptor. Retain the bundled analyzer and namespace props, declare selectors "
        + "in original source, supply typed capabilities, opt into explicitly registered selector dispatch, use an explicit observable, or choose Unsafe explicitly.",
        "ReactiveUI.Validation.Generation",
        DiagnosticSeverity.Error,
        true);

    /// <summary>Validates and formats the original compilation contract.</summary>
    private static readonly DiagnosticDescriptor IndirectCall = new(
        "RUVG007",
        "Indirect normal validation call is unsupported",
        "Normal validation methods require generated calls or explicit registered dispatch. Supply typed capabilities and ValidationRuntimeDispatch "
        + "for a finite registered delegate, use a typed observable API, or choose Unsafe explicitly.",
        "ReactiveUI.Validation.Generation",
        DiagnosticSeverity.Error,
        true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [MissingDispatch, IndirectCall, ProducerContractChecker.ContractDrift, ValidationExpressionConstructionAnalyzer.DynamicConstruction];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterOperationAction(AnalyzeMethodReference, OperationKind.MethodReference);
        context.RegisterCompilationAction(ProducerContractChecker.Analyze);
    }

    /// <summary>Validates and formats the original compilation contract.</summary>
    /// <param name="context">The context contract to inspect.</param>
    [SuppressMessage("Usage", "RSEXPERIMENTAL002", Justification = "The supported Roslyn 5.9 compiler exposes final encoded interceptor dispatch through this experimental API.")]
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !ValidationGenerator.IsNormalMethod(method))
        {
            return;
        }

        ValidationExpressionConstructionAnalyzer.Analyze(context, invocation);
        if (!ValidationRuntimeDispatchPolicy.IsOptedIn(context.SemanticModel, invocation.SpanStart)
            && context.SemanticModel.GetInterceptorMethod(invocation, context.CancellationToken) is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingDispatch, invocation.GetLocation()));
        }
    }

    /// <summary>Validates and formats the original compilation contract.</summary>
    /// <param name="context">The context contract to inspect.</param>
    private static void AnalyzeMethodReference(OperationAnalysisContext context)
    {
        var reference = (IMethodReferenceOperation)context.Operation;
        if (ValidationGenerator.IsNormalMethod(reference.Method)
            && reference.SemanticModel is { } model
            && !ValidationRuntimeDispatchPolicy.IsOptedIn(model, reference.Syntax.SpanStart))
        {
            context.ReportDiagnostic(Diagnostic.Create(IndirectCall, reference.Syntax.GetLocation()));
        }
    }
}
