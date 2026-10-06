// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators;

/// <summary>Shares exact shipping normal-call recognition with final analysis and package verification.</summary>
internal static class ValidationCallClassifier
{
    /// <summary>Validates and formats the original compilation contract.</summary>
    /// <param name="resolved">The resolved contract to inspect.</param>
    /// <returns>The checked semantic result.</returns>
    internal static IMethodSymbol? NormalizeMethod(IMethodSymbol resolved)
    {
        if (!resolved.ContainingType.IsExtension)
        {
            return resolved.ReducedFrom is null ? resolved : resolved.GetConstructedReducedFrom();
        }

        var arguments = resolved.ContainingType.TypeArguments.AddRange(resolved.TypeArguments);
        foreach (var member in resolved.ContainingType.ContainingType.GetMembers(resolved.Name))
        {
            if (member is not IMethodSymbol candidate || candidate.TypeParameters.Length != arguments.Length
                || candidate.Parameters.Length != resolved.Parameters.Length + 1)
            {
                continue;
            }

            var constructed = arguments.IsEmpty ? candidate : candidate.Construct(arguments.ToArray());
            if (MatchesParameters(resolved, constructed))
            {
                return constructed;
            }
        }

        return null;
    }

    /// <summary>Recognizes normal generator/runtime-dispatch entry points from the matching core assembly.</summary>
    /// <param name="method">The semantically resolved method.</param>
    /// <returns>Whether the method requires generated or explicitly registered dispatch.</returns>
    internal static bool IsNormalMethod(IMethodSymbol method) => RuntimeRoot(method) is not null && IsGeneratorOnly(method);

    /// <summary>Reports the actual selected normal selector form independently of converters and callbacks.</summary>
    /// <param name="method">The semantically selected shipping method or normalized shim.</param>
    /// <returns>Expression, Func, or None for the single selector-free normal shape or unrelated APIs.</returns>
    internal static string SelectorForm(IMethodSymbol method)
    {
        if (!IsNormalMethod(method))
        {
            return "None";
        }

        foreach (var parameter in method.Parameters)
        {
            if (!IsSelectorParameter(parameter))
            {
                continue;
            }

            if (ValidationSelectorSignature.IsExpression(parameter.Type))
            {
                return "Expression";
            }

            if (ValidationSelectorSignature.IsDelegate(parameter.Type))
            {
                return "Func";
            }
        }

        return "None";
    }

    /// <summary>Recognizes selector roles while excluding executable predicates, projections and callbacks.</summary>
    /// <param name="parameter">The exact shipping formal parameter.</param>
    /// <returns>Whether the formal parameter carries a semantic selector.</returns>
    internal static bool IsSelectorParameter(IParameterSymbol parameter) => parameter.Name is
        "viewModelProperty" or "modelProperty" or "viewProperty" or "contextProperty" or "helperProperty" or "viewModelHelperProperty";

    /// <summary>Recognizes only the two matching shipping namespace and assembly identities.</summary>
    /// <param name="method">The semantically resolved method.</param>
    /// <returns>The runtime namespace, or null for unrelated methods.</returns>
    internal static string? RuntimeRoot(IMethodSymbol method)
    {
        var root = method.ContainingAssembly.Name switch
        {
            "ReactiveUI.Validation" => "ReactiveUI.Validation",
            "ReactiveUI.Validation.Reactive" => "ReactiveUI.Validation.Reactive",
            _ => null,
        };
        var namespaceName = method.ContainingNamespace.ToDisplayString();
        return root is not null && (namespaceName == $"{root}.Extensions" || namespaceName == $"{root}.ValidationBindings") ? root : null;
    }

    /// <summary>Separates existing normal shapes from safe observable/metadata APIs and Unsafe variants.</summary>
    /// <param name="method">The semantically resolved method.</param>
    /// <returns>Whether the exact shipping owner and signature require dispatch.</returns>
    internal static bool IsGeneratorOnly(IMethodSymbol method)
    {
        var owner = method.ContainingType.IsExtension ? method.ContainingType.ContainingType : method.ContainingType;
        if (owner.ContainingType is not null || owner.Arity != 0)
        {
            return false;
        }

        return method.Name == "ValidationRule" ? IsRulePredicate(method, owner) : method.Name switch
        {
            "BindValidation" => owner.Name == "ViewForExtensions",
            "BindValidationContext" => owner.Name == "ValidationContextBindingExtensions",
            "BindValidationState" => owner.Name == "ValidationStateBindingExtensions",
            "ForProperty" or "ForViewModel" or "ForValidationHelperProperty" => owner.Name == "ValidationBinding",
            _ => false,
        };
    }

    /// <summary>Compares the extension block's substituted parameters with its CLR shim.</summary>
    /// <param name="resolved">The bound extension member.</param>
    /// <param name="constructed">The constructed shipping shim.</param>
    /// <returns>Whether every nonreceiver parameter matches.</returns>
    private static bool MatchesParameters(IMethodSymbol resolved, IMethodSymbol constructed)
    {
        for (var index = 0; index < resolved.Parameters.Length; index++)
        {
            if (!SymbolEqualityComparer.Default.Equals(constructed.Parameters[index + 1].Type, resolved.Parameters[index].Type))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Separates the four predicate rules from safe observable and metadata overloads.</summary>
    /// <param name="method">The exact shipping method.</param>
    /// <param name="owner">The actual top-level extension owner.</param>
    /// <returns>Whether the predicate rule requires normal dispatch.</returns>
    private static bool IsRulePredicate(IMethodSymbol method, INamedTypeSymbol owner)
    {
        if (owner.Name is not ("ValidatableViewModelExtensions" or "ValidationRuleContextExtensions"))
        {
            return false;
        }

        foreach (var parameter in method.Parameters)
        {
            if (parameter.Name == "isPropertyValid")
            {
                return true;
            }
        }

        return false;
    }
}
