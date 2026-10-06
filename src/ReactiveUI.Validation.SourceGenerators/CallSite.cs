// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
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
    /// <param name="originalCompilation">The originalCompilation contract.</param>
    [SuppressMessage("Style", "SST1472", Justification = "This semantic call record retains separate original and projected compilations while keeping existing call constructors source compatible.")]
    internal CallSite(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        string namespaceRoot,
        string attribute,
        int id,
        string? failureReason = null,
        Compilation? originalCompilation = null)
    {
        Model = model;
        Invocation = invocation;
        Method = method;
        NamespaceRoot = namespaceRoot;
        Attribute = attribute;
        Id = id;
        FailureReason = failureReason;
        OriginalCompilation = originalCompilation ?? model.Compilation;
    }

    /// <summary>Gets the original semantic model.</summary>
    internal SemanticModel Model { get; }

    /// <summary>Gets the actual original source compilation before private semantic projection.</summary>
    internal Compilation OriginalCompilation { get; }

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

    /// <summary>Gets auxiliary lexical hosts and payload interfaces emitted outside the interceptor class.</summary>
    internal IList<GeneratedSourceFragment> AdditionalSources { get; } = new List<GeneratedSourceFragment>();

    /// <summary>Gets or sets whether any selected read/write needs a lexical partial host.</summary>
    internal bool RequiresLexicalAccess { get; set; }

    /// <summary>Gets or sets exact finite producer member proofs retained through private projection.</summary>
    internal IReadOnlyCollection<string> ProducerMemberKeys { get; set; } = Array.Empty<string>();

    /// <summary>Builds a member key using exact nested CLR owner metadata.</summary>
    /// <param name="member">The property, field or associated accessor.</param>
    /// <returns>The normalized declaring-owner and member key.</returns>
    internal static string MemberContractKey(ISymbol member)
    {
        member = member is IMethodSymbol { AssociatedSymbol: { } associated } ? associated : member;
        var owner = member.ContainingType?.OriginalDefinition;
        var names = new Stack<string>();
        for (var current = owner; current is not null; current = current.ContainingType)
        {
            names.Push(current.MetadataName);
        }

        var prefix = owner is null || owner.ContainingNamespace.IsGlobalNamespace ? string.Empty : $"{owner.ContainingNamespace.ToDisplayString()}.";
        return $"{prefix}{string.Join("+", names)}|{member.Name}";
    }

    /// <summary>Tests whether a predicted member has an exact finite producer proof.</summary>
    /// <param name="member">The selected source or accessor member.</param>
    /// <returns>Whether final producer contract validation is required for this exact member.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool HasProducerContract(ISymbol member)
    {
        var key = MemberContractKey(member);
        foreach (var candidate in ProducerMemberKeys)
        {
            if (string.Equals(candidate, key, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

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
