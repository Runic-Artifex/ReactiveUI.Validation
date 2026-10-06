// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
namespace ReactiveUI.Validation.SourceGenerators;

/// <summary>Preserves the intercepted API's generic arity and constraint contract.</summary>
[SuppressMessage("Performance", "PSH1100", Justification = "Bounded symbol display and original-arity planning occur only during compilation, not generated runtime dispatch.")]
internal static class GenericInterceptorEmitter
{
    /// <summary>Gets whether the call needs the original generic signature.</summary>
    /// <param name="site">The intercepted call.</param>
    /// <returns>Whether closed namespace signature generation is impossible.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool NeedsOriginalSignature(CallSite site) => site.Method.TypeArguments.Any(type => !GeneratorHelpers.IsAccessibleType(site.Model.Compilation, type));

    /// <summary>Tests whether only direct caller type parameters need original API slots.</summary>
    /// <param name="site">The original call.</param>
    /// <returns>Whether a specialized global signature can preserve the exact original arity.</returns>
    internal static bool CanSpecialize(CallSite site) => NeedsOriginalSignature(site) && site.Method.TypeArguments.All(type =>
        type is ITypeParameterSymbol || GeneratorHelpers.IsAccessibleType(site.Model.Compilation, type));

    /// <summary>Gets the type-position substitution for direct caller generic arguments.</summary>
    /// <param name="site">The original call.</param>
    /// <returns>The caller parameter names rebound to original API parameter positions.</returns>
    internal static IReadOnlyDictionary<string, string> DirectSubstitutions(CallSite site)
    {
        var result = new Dictionary<string, string>();
        for (var index = 0; index < site.Method.TypeArguments.Length; index++)
        {
            if (site.Method.TypeArguments[index] is ITypeParameterSymbol parameter && !result.ContainsKey(parameter.Name))
            {
                result.Add(parameter.Name, site.Method.OriginalDefinition.TypeParameters[index].Name);
            }
        }

        return result;
    }

    /// <summary>Checks whether a legal global interceptor signature exists.</summary>
    /// <param name="site">The call site.</param>
    /// <param name="reason">The explicit descriptor route if a signature is impossible.</param>
    /// <returns>Whether the original API can be intercepted.</returns>
    internal static bool CanGenerate(CallSite site, out string reason)
    {
        var method = SignatureMethod(site);
        foreach (var parameter in method.Parameters)
        {
            if (CanNameOriginalType(site.Model.Compilation, parameter.Type))
            {
                continue;
            }

            reason = "This interceptor signature contains inaccessible concrete storage. Supply an inferred typed selector/target factory in the legal caller context.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>Formats a global static interceptor with only original method parameters.</summary>
    /// <param name="site">The original call.</param>
    /// <returns>The method declaration including exact generic constraints.</returns>
    [SuppressMessage("Style", "SST1442", Justification = "The signature independently retains original enclosing arity, method arity, byref modifiers and constraint positions.")]
    internal static string MethodHeader(CallSite site)
    {
        var method = SignatureMethod(site);
        var text = new StringBuilder($"internal static {SignatureType(site, method.ReturnType)} Intercept{site.Id}");
        var enclosing = EnclosingParameters(site.Method.OriginalDefinition);
        if (NeedsOriginalSignature(site) && (method.IsGenericMethod || !enclosing.IsEmpty))
        {
            var names = enclosing.Select(static (_, ordinal) => $"__RunicApiOwner{ordinal}").Concat(method.TypeParameters.Select(static type => $"@{type.Name}"));
            _ = text.Append('<').Append(string.Join(", ", names)).Append('>');
        }

        _ = text.Append('(');
        for (var index = 0; index < method.Parameters.Length; index++)
        {
            var parameter = method.Parameters[index];
            if (index > 0)
            {
                _ = text.Append(", ");
            }

            if (index == 0 && method.IsExtensionMethod)
            {
                _ = text.Append("this ");
            }

            _ = text.Append(parameter.RefKind switch
            {
                RefKind.Ref => "ref ",
                RefKind.Out => "out ",
                RefKind.In => "in ",
                RefKind.RefReadOnlyParameter => "ref readonly ",
                _ => string.Empty,
            });
            _ = text.Append(SignatureType(site, parameter.Type)).Append(" @").Append(parameter.Name);
        }

        _ = text.Append(')');
        if (NeedsOriginalSignature(site))
        {
            AppendConstraints(text, site, enclosing);
        }

        return text.ToString();
    }

    /// <summary>Gets the exact method symbol used for the generated signature.</summary>
    /// <param name="site">The call site.</param>
    /// <returns>The constructed or original API method.</returns>
    internal static IMethodSymbol SignatureMethod(CallSite site) => NeedsOriginalSignature(site) && !CanSpecialize(site) ? site.Method.OriginalDefinition : site.Method;

    /// <summary>Maps exact original API argument symbols to signature type slots.</summary>
    /// <param name="site">The call site.</param>
    /// <param name="type">The payload type.</param>
    /// <returns>The original slot name or concrete type name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string PayloadType(CallSite site, ITypeSymbol type) => SignatureType(site, type);

    /// <summary>Formats an exact original API payload slot by its ordinal.</summary>
    /// <param name="site">The original call.</param>
    /// <param name="ordinal">The original API type-parameter ordinal.</param>
    /// <returns>The exact original slot, preserving coincident constructed argument roles.</returns>
    internal static string PayloadSlot(CallSite site, int ordinal) => NeedsOriginalSignature(site)
        ? $"@{site.Method.OriginalDefinition.TypeParameters[ordinal].Name}"
        : GeneratorHelpers.TypeName(site.Method.TypeArguments[ordinal]);

    /// <summary>Formats type/method constraints without changing their positions.</summary>
    /// <param name="parameter">The original type parameter.</param>
    /// <param name="name">The optional original enclosing-slot declaration name.</param>
    /// <param name="slots">The original containing-type slot identities.</param>
    /// <param name="includeNotNull">Whether to retain the warning-only original method restriction.</param>
    /// <returns>The complete where clause.</returns>
    internal static string Constraints(ITypeParameterSymbol parameter, string? name = null, IReadOnlyDictionary<ITypeParameterSymbol, string>? slots = null, bool includeNotNull = true)
    {
        var constraints = new List<string>();
        AppendStorageConstraint(constraints, parameter, includeNotNull);

        constraints.AddRange(parameter.ConstraintTypes.Select(type => slots is null ? GeneratorHelpers.TypeName(type) : SlotTypeName(type, slots)));
        if (parameter.HasConstructorConstraint)
        {
            constraints.Add("new()");
        }

        if (parameter.AllowsRefLikeType)
        {
            constraints.Add("allows ref struct");
        }

        return constraints.Count == 0 ? string.Empty : $" where @{(name ?? parameter.Name).TrimStart('@')} : {string.Join(", ", constraints)}";
    }

    /// <summary>Copies CLR storage restrictions and optional warning-only nullability.</summary>
    /// <param name="constraints">The emitted clauses.</param>
    /// <param name="parameter">The original API slot.</param>
    /// <param name="includeNotNull">Whether the declaration retains the original method warning contract.</param>
    private static void AppendStorageConstraint(List<string> constraints, ITypeParameterSymbol parameter, bool includeNotNull)
    {
        if (parameter.HasUnmanagedTypeConstraint)
        {
            constraints.Add("unmanaged");
        }
        else if (parameter.HasValueTypeConstraint)
        {
            constraints.Add("struct");
        }
        else if (parameter.HasReferenceTypeConstraint)
        {
            constraints.Add(parameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated ? "class?" : "class");
        }
        else if (includeNotNull && parameter.HasNotNullConstraint)
        {
            constraints.Add("notnull");
        }
    }

    /// <summary>Retains original declarations while specializing dependent constraint payload slots.</summary>
    /// <param name="text">The method declaration buffer.</param>
    /// <param name="site">The original API call.</param>
    /// <param name="enclosing">The original containing-type parameters.</param>
    private static void AppendConstraints(StringBuilder text, CallSite site, ImmutableArray<ITypeParameterSymbol> enclosing)
    {
        var slots = CanSpecialize(site) ? SpecializedSlots(site) : EnclosingSlots(site);
        var declared = EnclosingSlots(site);
        foreach (var parameter in enclosing)
        {
            _ = text.Append(Constraints(parameter, declared[parameter], slots));
        }

        var original = site.Method.OriginalDefinition.TypeParameters;
        var logical = LogicalParameters(site);
        for (var index = 0; index < original.Length; index++)
        {
            slots[logical[index]] = slots.TryGetValue(original[index], out var substitution) ? substitution : $"@{original[index].Name}";
        }

        for (var index = 0; index < original.Length; index++)
        {
            _ = text.Append(Constraints(logical[index], original[index].Name, slots));
        }
    }

    /// <summary>Preserves the bound C# extension contract instead of synthesized shim metadata restrictions.</summary>
    /// <param name="site">The original semantic call.</param>
    /// <returns>The logical constraints in normalized CLR generic-slot order.</returns>
    private static ImmutableArray<ITypeParameterSymbol> LogicalParameters(CallSite site)
    {
        if (site.Model.GetSymbolInfo(site.Invocation).Symbol is IMethodSymbol { ContainingType.IsExtension: true } resolved)
        {
            var definition = resolved.OriginalDefinition;
            var parameters = definition.ContainingType.TypeParameters.AddRange(definition.TypeParameters);
            if (parameters.Length == site.Method.OriginalDefinition.TypeParameters.Length)
            {
                return parameters;
            }
        }

        return site.Method.OriginalDefinition.TypeParameters;
    }

    /// <summary>Formats specialized known arguments while keeping direct caller parameters in original slots.</summary>
    /// <param name="site">The original call.</param>
    /// <param name="type">The exact payload type.</param>
    /// <returns>The legal global signature type.</returns>
    private static string SignatureType(CallSite site, ITypeSymbol type) => CanSpecialize(site)
        ? TypeParameterSyntax.ReplaceType(GeneratorHelpers.TypeName(type), DirectSubstitutions(site))
        : SlotTypeName(type, EnclosingSlots(site));

    /// <summary>Reads the original API's containing generic arity in CLR order.</summary>
    /// <param name="method">The original method.</param>
    /// <returns>The original containing type parameters.</returns>
    private static ImmutableArray<ITypeParameterSymbol> EnclosingParameters(IMethodSymbol method)
    {
        var types = new Stack<INamedTypeSymbol>();
        for (var type = method.ContainingType; type is not null; type = type.ContainingType)
        {
            types.Push(type);
        }

        return types.SelectMany(static type => type.TypeParameters).ToImmutableArray();
    }

    /// <summary>Names original enclosing slots without introducing decomposition parameters.</summary>
    /// <param name="site">The original API call.</param>
    /// <returns>The semantic type-parameter map.</returns>
    private static Dictionary<ITypeParameterSymbol, string> EnclosingSlots(CallSite site)
    {
        var result = new Dictionary<ITypeParameterSymbol, string>(SymbolEqualityComparer.Default);
        var parameters = EnclosingParameters(site.Method.OriginalDefinition);
        for (var index = 0; index < parameters.Length; index++)
        {
            result.Add(parameters[index], $"@__RunicApiOwner{index}");
        }

        return result;
    }

    /// <summary>Substitutes exact closed argument positions in dependent original API constraints.</summary>
    /// <param name="site">The specialized original API call.</param>
    /// <returns>The role-preserving original symbol map.</returns>
    private static Dictionary<ITypeParameterSymbol, string> SpecializedSlots(CallSite site)
    {
        var result = EnclosingSlots(site);
        var direct = DirectSubstitutions(site);
        for (var index = 0; index < site.Method.TypeArguments.Length; index++)
        {
            var argument = site.Method.TypeArguments[index];
            result.Add(site.Method.OriginalDefinition.TypeParameters[index], argument is ITypeParameterSymbol parameter
                ? $"@{direct[parameter.Name]}"
                : GeneratorHelpers.TypeName(argument));
        }

        return result;
    }

    /// <summary>Formats original type slots by symbol identity, preserving coincident names and values.</summary>
    /// <param name="type">The exact payload or constraint type.</param>
    /// <param name="slots">The original slot identity map.</param>
    /// <returns>The signature-safe type syntax.</returns>
    private static string SlotTypeName(ITypeSymbol type, IReadOnlyDictionary<ITypeParameterSymbol, string> slots)
    {
        var format = SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);
        return string.Concat(type.ToDisplayParts(format).Select(part => part.Symbol is ITypeParameterSymbol parameter && slots.TryGetValue(parameter, out var name) ? name : part.ToString()));
    }

    /// <summary>Checks the original API signature, allowing its own open parameters.</summary>
    /// <param name="compilation">The compilation.</param>
    /// <param name="type">The API payload type.</param>
    /// <returns>Whether a global method can name the type.</returns>
    private static bool CanNameOriginalType(Compilation compilation, ITypeSymbol type) => type switch
    {
        ITypeParameterSymbol => true,
        IArrayTypeSymbol array => CanNameOriginalType(compilation, array.ElementType),
        INamedTypeSymbol named => compilation.IsSymbolAccessibleWithin(named.OriginalDefinition, compilation.Assembly)
            && named.TypeArguments.All(argument => CanNameOriginalType(compilation, argument))
            && (named.ContainingType is null || CanNameOriginalType(compilation, named.ContainingType)),
        _ => GeneratorHelpers.IsAccessibleType(compilation, type),
    };
}
