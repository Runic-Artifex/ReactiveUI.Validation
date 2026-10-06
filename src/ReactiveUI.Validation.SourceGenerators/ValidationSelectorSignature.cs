// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators;

/// <summary>Reads the selected typed or retained expression selector's exact semantic contract.</summary>
internal static class ValidationSelectorSignature
{
    /// <summary>The source and result generic slots in the exact Func selector contract.</summary>
    private const int SelectorArity = 2;

    /// <summary>Reads the original selector result slot before target assignment conversion.</summary>
    /// <param name="selectorType">The selected Func or Expression&lt;Func&gt; formal parameter.</param>
    /// <returns>The original selected result type, including its nullable annotation.</returns>
    /// <exception cref="InvalidOperationException">The shipping selector signature is unavailable.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ITypeSymbol ValueType(ITypeSymbol selectorType) => DelegateContract(selectorType).ReturnType;

    /// <summary>Preserves the original nullable selector input slot for registered source plans.</summary>
    /// <param name="selectorType">The selected Func or Expression&lt;Func&gt; formal parameter.</param>
    /// <returns>The original source parameter type, including its nullable annotation.</returns>
    /// <exception cref="InvalidOperationException">The shipping selector signature is unavailable.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ITypeSymbol SourceType(ITypeSymbol selectorType) => DelegateContract(selectorType).Parameters[0].Type;

    /// <summary>Separates retained expression selectors from ordinary delegates by semantic type identity.</summary>
    /// <param name="type">The selected formal parameter type.</param>
    /// <returns>Whether the parameter is an Expression&lt;Func&lt;TSource,TValue&gt;&gt; selector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsExpression(ITypeSymbol type) =>
        type is INamedTypeSymbol { MetadataName: "Expression`1", TypeArguments.Length: 1 } expression
        && expression.ContainingNamespace.ToDisplayString() == "System.Linq.Expressions"
        && IsDelegate(expression.TypeArguments[0]);

    /// <summary>Recognizes the actual one-source typed Func selector contract.</summary>
    /// <param name="type">The selected formal parameter type.</param>
    /// <returns>Whether the parameter is a Func&lt;TSource,TValue&gt; selector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsDelegate(ITypeSymbol type) =>
        type is INamedTypeSymbol { TypeKind: TypeKind.Delegate, MetadataName: "Func`2", TypeArguments.Length: SelectorArity }
        && type.ContainingNamespace.ToDisplayString() == "System";

    /// <summary>Unwraps only a retained expression wrapper while preserving the actual delegate signature.</summary>
    /// <param name="selectorType">The original formal parameter type.</param>
    /// <returns>The exact selector's invocation contract.</returns>
    /// <exception cref="InvalidOperationException">The shipping selector signature is unavailable.</exception>
    private static IMethodSymbol DelegateContract(ITypeSymbol selectorType)
    {
        if (IsExpression(selectorType))
        {
            selectorType = ((INamedTypeSymbol)selectorType).TypeArguments[0];
        }

        if (IsDelegate(selectorType) && selectorType is INamedTypeSymbol { DelegateInvokeMethod: { } invoke })
        {
            return invoke;
        }

        throw new InvalidOperationException("The selected Validation overload has no typed one-source selector contract.");
    }
}
