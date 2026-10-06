// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators;

/// <summary>One typed index argument expression and its exact original syntax identity.</summary>
/// <param name="operation">The original typed argument operation.</param>
/// <param name="expression">The root-parameterized argument expression.</param>
internal sealed class SelectorIndex(IOperation operation, string expression)
{
    /// <summary>Gets the original source argument.</summary>
    internal SyntaxNode Syntax => Operation.Syntax;

    /// <summary>Gets the exact argument type.</summary>
    internal ITypeSymbol Type => SemanticSelectorPlanner.OperandType(Operation);

    /// <summary>Gets the exact index evaluation operation.</summary>
    internal IOperation Operation { get; } = operation;

    /// <summary>Gets the root-parameterized expression.</summary>
    internal string Expression { get; } = expression;
}
