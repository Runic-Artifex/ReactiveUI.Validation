// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ReactiveUI.Validation.SourceGenerators;

/// <summary>Captures the original semantic validation call and compiler location.</summary>
internal sealed class CallSite
{
    /// <summary>Initializes a new instance of the <see cref="CallSite"/> class.</summary>
    /// <param name="model">The original semantic model.</param>
    /// <param name="invocation">The source invocation.</param>
    /// <param name="method">The constructed implementation symbol.</param>
    /// <param name="namespaceRoot">The matching runtime flavor namespace.</param>
    /// <param name="attribute">The encoded interception attribute.</param>
    /// <param name="id">The generated method identifier.</param>
    /// <param name="failureReason">A resolution failure, if any.</param>
    internal CallSite(SemanticModel model, InvocationExpressionSyntax invocation, IMethodSymbol method, string namespaceRoot, string attribute, int id, string? failureReason = null)
    {
        Model = model;
        Invocation = invocation;
        Method = method;
        NamespaceRoot = namespaceRoot;
        Attribute = attribute;
        Id = id;
        FailureReason = failureReason;
    }

    /// <summary>Gets the original semantic model.</summary>
    internal SemanticModel Model { get; }

    /// <summary>Gets the source invocation.</summary>
    internal InvocationExpressionSyntax Invocation { get; }

    /// <summary>Gets the constructed static implementation method.</summary>
    internal IMethodSymbol Method { get; }

    /// <summary>Gets the matching runtime flavor namespace.</summary>
    internal string NamespaceRoot { get; }

    /// <summary>Gets the encoded compiler interception attribute.</summary>
    internal string Attribute { get; }

    /// <summary>Gets the generated method identifier.</summary>
    internal int Id { get; }

    /// <summary>Gets a generation failure detected during semantic resolution.</summary>
    internal string? FailureReason { get; }

    /// <summary>Finds an argument by its semantic parameter, including named arguments.</summary>
    /// <param name="parameterName">The formal parameter name.</param>
    /// <returns>The original argument expression.</returns>
    internal ExpressionSyntax? GetArgument(string parameterName)
    {
        if (Model.GetOperation(Invocation) is not IInvocationOperation operation)
        {
            return null;
        }

        foreach (var argument in operation.Arguments)
        {
            if (argument.Parameter?.Name == parameterName)
            {
                return argument.Value.Syntax as ExpressionSyntax;
            }
        }

        return null;
    }
}
