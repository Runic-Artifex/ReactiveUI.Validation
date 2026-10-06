// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators;

/// <summary>Separates a writable member's nullable input contract from its readable value type.</summary>
internal static class BindingWriteContract
{
    /// <summary>Applies the terminal member and setter value parameter's input annotations.</summary>
    /// <param name="storageType">The actual CLR storage type with its readable annotations.</param>
    /// <param name="member">The actual terminal property, indexer or field.</param>
    /// <returns>The writable input type without changing any nested nullable or getter contract.</returns>
    internal static ITypeSymbol InputType(ITypeSymbol storageType, ISymbol member)
    {
        if (storageType.IsValueType)
        {
            return storageType;
        }

        var parameter = ValueParameter(member);
        if (ForbidsNull(member))
        {
            return storageType.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
        }

        return (HasAttribute(member, "System.Diagnostics.CodeAnalysis.AllowNullAttribute")
            || HasAttribute(parameter, "System.Diagnostics.CodeAnalysis.AllowNullAttribute"))
            ? storageType.WithNullableAnnotation(NullableAnnotation.Annotated)
            : storageType;
    }

    /// <summary>Checks an input prohibition independently of nullable reference or CLR value types.</summary>
    /// <param name="member">The exact terminal storage member.</param>
    /// <returns>Whether null is forbidden by the member or setter value parameter.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool ForbidsNull(ISymbol member) =>
        HasAttribute(member, "System.Diagnostics.CodeAnalysis.DisallowNullAttribute")
            || HasAttribute(ValueParameter(member), "System.Diagnostics.CodeAnalysis.DisallowNullAttribute");

    /// <summary>Checks a nullable converted value against the independent input prohibition.</summary>
    /// <param name="source">The original projected value type.</param>
    /// <param name="member">The exact terminal storage member.</param>
    /// <param name="conversion">The selected user-defined conversion, if any.</param>
    /// <returns>Whether an explicit nullable-value policy is required.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool RequiresNullPolicy(ITypeSymbol source, ISymbol member, IMethodSymbol? conversion) =>
        ForbidsNull(member) && (conversion is null ? CanContainNull(source) : ConvertedValueCanContainNull(source, conversion));

    /// <summary>Recognizes nullable value storage and nullable or unconstrained projected references.</summary>
    /// <param name="type">The projected or converted value contract.</param>
    /// <returns>Whether this contract can carry null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CanContainNull(ITypeSymbol type) => IsNullableValue(type)
        || type.NullableAnnotation == NullableAnnotation.Annotated
        || type is ITypeParameterSymbol { HasNotNullConstraint: false, HasValueTypeConstraint: false, HasReferenceTypeConstraint: false };

    /// <summary>Retains null propagation through a lifted value conversion without assuming its operator is called.</summary>
    /// <param name="source">The actual projected input.</param>
    /// <param name="conversion">The selected operator.</param>
    /// <returns>Whether a lifted input or declared output can carry null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ConvertedValueCanContainNull(ITypeSymbol source, IMethodSymbol conversion) =>
        (IsNullableValue(source) && conversion.Parameters.Length == 1 && conversion.Parameters[0].Type.IsValueType
            && !IsNullableValue(conversion.Parameters[0].Type)) || ConversionCanReturnNull(conversion);

    /// <summary>Recognizes CLR Nullable storage without changing its underlying signature.</summary>
    /// <param name="type">The actual projected or storage type.</param>
    /// <returns>Whether the type is constructed Nullable.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsNullableValue(ITypeSymbol type) =>
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T };

    /// <summary>Preserves a selected operator's explicit nullable return postcondition.</summary>
    /// <param name="conversion">The selected operator.</param>
    /// <returns>Whether its declared result can be null.</returns>
    private static bool ConversionCanReturnNull(IMethodSymbol conversion)
    {
        var maybeNull = false;
        foreach (var attribute in conversion.GetReturnTypeAttributes())
        {
            var name = attribute.AttributeClass?.ToDisplayString();
            if (name == "System.Diagnostics.CodeAnalysis.NotNullAttribute")
            {
                return false;
            }

            maybeNull |= name == "System.Diagnostics.CodeAnalysis.MaybeNullAttribute";
        }

        return maybeNull || CanContainNull(conversion.ReturnType);
    }

    /// <summary>Finds the actual setter input without attributing index parameters to its value.</summary>
    /// <param name="member">The exact writable storage member.</param>
    /// <returns>The setter value parameter or null for a field.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IParameterSymbol? ValueParameter(ISymbol member) =>
        member is IPropertySymbol { SetMethod: { } setter }
            ? setter.Parameters[setter.Parameters.Length - 1]
            : null;

    /// <summary>Checks exact framework flow annotations without attributing unrelated same-name types.</summary>
    /// <param name="symbol">The selected member or setter parameter.</param>
    /// <param name="attributeName">The fully qualified annotation name.</param>
    /// <returns>Whether this exact symbol declares the input annotation.</returns>
    private static bool HasAttribute(ISymbol? symbol, string attributeName)
    {
        if (symbol is null)
        {
            return false;
        }

        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == attributeName)
            {
                return true;
            }
        }

        return false;
    }
}
