// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ReactiveUI.Validation.SourceGenerators;

/// <summary>Validates semantic contracts and emits compiler-safe C# syntax.</summary>
internal static class GeneratorHelpers
{
    /// <summary>Formats a fully qualified type with nullable annotations.</summary>
    /// <param name="type">The type to format.</param>
    /// <returns>Compiler-safe type syntax.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string TypeName(ITypeSymbol type) => type.ToDisplayString(
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers));

    /// <summary>Escapes a C# string literal.</summary>
    /// <param name="value">The string to escape.</param>
    /// <returns>A quoted literal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string Quote(string value) => SymbolDisplay.FormatLiteral(value, true);

    /// <summary>Formats a concrete static interceptor signature.</summary>
    /// <param name="site">The intercepted call.</param>
    /// <returns>A method declaration without a body.</returns>
    internal static string MethodHeader(CallSite site)
    {
        var text = new StringBuilder($"internal static {TypeName(site.Method.ReturnType)} Intercept{site.Id}(");
        for (var index = 0; index < site.Method.Parameters.Length; index++)
        {
            var parameter = site.Method.Parameters[index];
            if (index > 0)
            {
                _ = text.Append(", ");
            }

            if (index == 0 && site.Method.IsExtensionMethod)
            {
                _ = text.Append("this ");
            }

            if (parameter.RefKind != RefKind.None)
            {
                _ = text.Append(parameter.RefKind.ToString().ToLowerInvariant()).Append(' ');
            }

            _ = text.Append(TypeName(parameter.Type)).Append(" @").Append(parameter.Name);
        }

        return text.Append(')').ToString();
    }

    /// <summary>Checks a type and all its constructed arguments for generated access.</summary>
    /// <param name="compilation">The caller compilation.</param>
    /// <param name="type">The referenced type.</param>
    /// <returns>Whether namespace-level generated code can use the type.</returns>
    internal static bool IsAccessibleType(Compilation compilation, ITypeSymbol type)
    {
        if (type is ITypeParameterSymbol || type.TypeKind is TypeKind.Error or TypeKind.Dynamic)
        {
            return false;
        }

        if (type is IArrayTypeSymbol array)
        {
            return IsAccessibleType(compilation, array.ElementType);
        }

        if (!compilation.IsSymbolAccessibleWithin(type, compilation.Assembly))
        {
            return false;
        }

        return type is not INamedTypeSymbol named || NamedArgumentsAccessible(compilation, named);
    }

    /// <summary>Reads a literal selector and validates its notification contracts.</summary>
    /// <param name="site">The original call.</param>
    /// <param name="expression">The selector argument.</param>
    /// <param name="selector">The validated path.</param>
    /// <param name="reason">The unsupported contract explanation.</param>
    /// <param name="requireNotification">Whether every owner must notify changes.</param>
    /// <returns>Whether the selector can be generated.</returns>
    internal static bool TrySelector(CallSite site, ExpressionSyntax expression, out Selector? selector, out string reason, bool requireNotification = true)
    {
        selector = null;
        var body = LambdaBody(expression);
        if (body is null)
        {
            reason = "Supply an inline property-selector lambda; runtime expression variables and statement bodies need an explicit observable.";
            return false;
        }

        var properties = ImmutableArray.CreateBuilder<IPropertySymbol>();
        body = Unwrap(body);
        while (body is MemberAccessExpressionSyntax member)
        {
            if (!TryProperty(site, member, out var property))
            {
                reason = "Every segment must be an accessible readable instance property. Generated-only members, indexers and private getters need an explicit observable.";
                return false;
            }

            properties.Insert(0, property!);
            body = Unwrap(member.Expression);
        }

        var parameter = body is IdentifierNameSyntax identifier ? site.Model.GetSymbolInfo(identifier).Symbol as IParameterSymbol : null;
        if (properties.Count == 0 || !IsSelectedParameter(site, expression, parameter))
        {
            reason = "Selectors must read a path from their lambda parameter. Calls, indexers, conversions and captured objects need an explicit observable.";
            return false;
        }

        if (requireNotification && !ValidateOwners(parameter!.Type, properties, out reason))
        {
            return false;
        }

        selector = new(properties.ToImmutable());
        reason = string.Empty;
        return true;
    }

    /// <summary>Checks enclosing generic types and all constructed type arguments.</summary>
    /// <param name="compilation">The caller compilation.</param>
    /// <param name="named">The constructed named type.</param>
    /// <returns>Whether every containing type and argument can be generated.</returns>
    private static bool NamedArgumentsAccessible(Compilation compilation, INamedTypeSymbol named)
    {
        if (named.ContainingType is not null && !IsAccessibleType(compilation, named.ContainingType))
        {
            return false;
        }

        foreach (var argument in named.TypeArguments)
        {
            if (!IsAccessibleType(compilation, argument))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Requires the selector's own parameter rather than a captured outer parameter.</summary>
    /// <param name="site">The original call.</param>
    /// <param name="expression">The selector syntax.</param>
    /// <param name="parameter">The path's root parameter symbol.</param>
    /// <returns>Whether the root belongs to this selector.</returns>
    private static bool IsSelectedParameter(CallSite site, ExpressionSyntax expression, IParameterSymbol? parameter)
    {
        var declared = Unwrap(expression) switch
        {
            SimpleLambdaExpressionSyntax simple => site.Model.GetDeclaredSymbol(simple.Parameter),
            ParenthesizedLambdaExpressionSyntax parenthesized => site.Model.GetDeclaredSymbol(parenthesized.ParameterList.Parameters[0]),
            _ => null,
        };
        return parameter is not null && SymbolEqualityComparer.Default.Equals(declared, parameter);
    }

    /// <summary>Reads a single-parameter expression-bodied literal lambda.</summary>
    /// <param name="expression)">The expression) contract to inspect.</param>
    /// <returns>The checked semantic result.</returns>
    private static ExpressionSyntax? LambdaBody(ExpressionSyntax expression) => Unwrap(expression) switch
    {
        SimpleLambdaExpressionSyntax simple => simple.Body as ExpressionSyntax,
        ParenthesizedLambdaExpressionSyntax parenthesized when parenthesized.ParameterList.Parameters.Count == 1 => parenthesized.Body as ExpressionSyntax,
        _ => null,
    };

    /// <summary>Validates and formats the original compilation contract.</summary>
    /// <param name="site">The site contract to inspect.</param>
    /// <param name="member">The member contract to inspect.</param>
    /// <param name="property">The property contract to inspect.</param>
    /// <returns>The checked semantic result.</returns>
    private static bool TryProperty(CallSite site, MemberAccessExpressionSyntax member, out IPropertySymbol? property)
    {
        property = site.Model.GetSymbolInfo(member).Symbol as IPropertySymbol;
        return property is { IsStatic: false, IsIndexer: false, GetMethod: not null }
            && site.Model.Compilation.IsSymbolAccessibleWithin(property.GetMethod, site.Model.Compilation.Assembly)
            && IsAccessibleType(site.Model.Compilation, property.Type);
    }

    /// <summary>Validates and formats the original compilation contract.</summary>
    /// <param name="root">The root contract to inspect.</param>
    /// <param name="properties">The properties contract to inspect.</param>
    /// <param name="reason">The reason contract to inspect.</param>
    /// <returns>The checked semantic result.</returns>
    private static bool ValidateOwners(ITypeSymbol root, ImmutableArray<IPropertySymbol>.Builder properties, out string reason)
    {
        for (var index = 0; index < properties.Count; index++)
        {
            var owner = index == 0 ? root : properties[index - 1].Type;
            if (index > 0 && !owner.IsReferenceType)
            {
                reason = "Observed intermediate parents must be reference types. Supply an explicit observable for value-type chains.";
                return false;
            }

            if (Notifies(owner))
            {
                continue;
            }

            reason = $"The owner of '{properties[index].Name}' must implement INotifyPropertyChanged. Supply an explicit observable for non-notifying nodes.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>Validates and formats the original compilation contract.</summary>
    /// <param name="owner">The owner contract to inspect.</param>
    /// <returns>The checked semantic result.</returns>
    private static bool Notifies(ITypeSymbol owner)
    {
        foreach (var contract in owner.AllInterfaces)
        {
            if (contract.ToDisplayString() == "System.ComponentModel.INotifyPropertyChanged")
            {
                return true;
            }
        }

        return owner.ToDisplayString() == "System.ComponentModel.INotifyPropertyChanged";
    }

    /// <summary>Validates and formats the original compilation contract.</summary>
    /// <param name="expression">The expression contract to inspect.</param>
    /// <returns>The checked semantic result.</returns>
    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (true)
        {
            if (expression is ParenthesizedExpressionSyntax parenthesized)
            {
                expression = parenthesized.Expression;
                continue;
            }

            if (expression is PostfixUnaryExpressionSyntax suppression && suppression.IsKind(SyntaxKind.SuppressNullableWarningExpression))
            {
                expression = suppression.Operand;
                continue;
            }

            return expression;
        }
    }
}
