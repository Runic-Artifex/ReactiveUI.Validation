// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
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

    /// <summary>Formats a payload using only the original interceptor generic slots.</summary>
    /// <param name="site">The call site.</param>
    /// <param name="type">The exact payload type.</param>
    /// <returns>The legal method-local type name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string MethodTypeName(CallSite site, ITypeSymbol type) => GenericInterceptorEmitter.PayloadType(site, type);

    /// <summary>Escapes a C# string literal.</summary>
    /// <param name="value">The string to escape.</param>
    /// <returns>A quoted literal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string Quote(string value) => SymbolDisplay.FormatLiteral(value, true);

    /// <summary>Formats a legal interceptor with the original API arity.</summary>
    /// <param name="site">The intercepted call.</param>
    /// <returns>A method declaration without a body.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string MethodHeader(CallSite site) => GenericInterceptorEmitter.MethodHeader(site);

    /// <summary>Checks a type and all constructed arguments for namespace access.</summary>
    /// <param name="compilation">The caller compilation.</param>
    /// <param name="type">The referenced type.</param>
    /// <returns>Whether namespace-level generated code can use the closed type.</returns>
    [SuppressMessage("Style", "SST1442", Justification = "This recursive type proof independently rejects open, anonymous, file-local and inaccessible shapes.")]
    internal static bool IsAccessibleType(Compilation compilation, ITypeSymbol type)
    {
        if (type is ITypeParameterSymbol || type.TypeKind is TypeKind.Error or TypeKind.Dynamic || type is INamedTypeSymbol { IsAnonymousType: true } or INamedTypeSymbol { IsFileLocal: true })
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

        return type is not INamedTypeSymbol named
            || ((named.ContainingType is null || IsAccessibleType(compilation, named.ContainingType)) && named.TypeArguments.All(argument => IsAccessibleType(compilation, argument)));
    }

    /// <summary>Plans a literal or statically proven stored semantic selector.</summary>
    /// <param name="site">The original call.</param>
    /// <param name="expression">The selector argument.</param>
    /// <param name="selector">The validated operation/dependency plan.</param>
    /// <param name="reason">The required explicit typed route.</param>
    /// <param name="requireNotification">Whether ordinary observable lowering is required.</param>
    /// <returns>Whether the automatic selector has a sound plan.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TrySelector(CallSite site, ExpressionSyntax expression, out Selector? selector, out string reason, bool requireNotification = true) =>
        SemanticSelectorPlanner.TryCreate(site, expression, out selector, out reason, requireNotification);

    /// <summary>Plans structural metadata using only the operations needed to acquire current keys.</summary>
    /// <param name="site">The original call.</param>
    /// <param name="expression">The property metadata selector.</param>
    /// <param name="selector">The metadata plan.</param>
    /// <param name="reason">The typed metadata factory alternative.</param>
    /// <returns>Whether structural metadata can be emitted without reading the selected leaf.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryMetadataSelector(CallSite site, ExpressionSyntax expression, out Selector? selector, out string reason) =>
        SemanticSelectorPlanner.TryCreate(site, expression, out selector, out reason, false, true);

    /// <summary>Plans legal typed storage assignment with outward struct write-back.</summary>
    /// <param name="site">The original call.</param>
    /// <param name="expression">The target selector.</param>
    /// <param name="access">The typed writable lens.</param>
    /// <param name="reason">The explicit replacement/storage alternative.</param>
    /// <param name="requireObservableStorageSafety">Whether notification replay needs proven origin-aware storage.</param>
    /// <returns>Whether ordinary typed assignment is legal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryAccessPlan(CallSite site, ExpressionSyntax expression, out AccessPlan? access, out string reason, bool requireObservableStorageSafety = true)
    {
        if (!SemanticSelectorPlanner.TryAccess(site, expression, out access, out reason))
        {
            return false;
        }

        if (requireObservableStorageSafety && access!.RequiresCopyBack)
        {
            reason = "Observable struct storage requires an authored origin-aware ValidationTarget or owned ValidationCell/ValidationLens; "
                + "opaque setters cannot distinguish peer writes, normalization and external replacement.";
            access = null;
            return false;
        }

        if (TryMetadataSelector(site, expression, out var selector, out _))
        {
            access!.Dependencies = selector!.Dependencies;
            access.TargetPath = selector.PathPlans.FirstOrDefault();
            access.ReadSelector = selector;
        }

        return true;
    }
}
