// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators;

/// <summary>Emits predicate rules through typed access and observation plans.</summary>
internal static class RuleEmitter
{
    /// <summary>Produces a typed interceptor or an actionable error.</summary>
    /// <param name="site">The original validation call.</param>
    /// <param name="source">The generated method.</param>
    /// <param name="diagnostic">An unsupported-selector error.</param>
    /// <returns>Whether the rule overload was handled.</returns>
    internal static bool TryEmit(CallSite site, out string? source, out Diagnostic? diagnostic)
    {
        source = null;
        diagnostic = null;
        if (site.Attribute.Length == 0)
        {
            diagnostic = Diagnostic.Create(ValidationGenerator.UnsupportedLocation, site.Invocation.GetLocation());
            return true;
        }

        var expression = site.GetArgument("viewModelProperty");
        var reason = "A selector with a typed access plan is required.";
        if (expression is null || !GeneratorHelpers.TrySelector(site, expression, out var selector, out reason))
        {
            diagnostic = Diagnostic.Create(
                ValidationGenerator.UnsupportedSelector,
                expression?.GetLocation() ?? site.Invocation.GetLocation(),
                $"{reason} Supply a typed ValidationSelector or registered operation; an explicit observable is also supported.");
            return true;
        }

        var model = $"@{site.Method.Parameters[0].Name}";
        var predicate = site.Method.Parameters.Single(static parameter => parameter.Name == "isPropertyValid");
        var valueType = ((INamedTypeSymbol)predicate.Type).TypeArguments[0];
        var context = site.Method.Parameters.Any(static parameter => parameter.Name == "context")
            ? "@context"
            : $"((global::{site.NamespaceRoot}.Abstractions.IValidatableViewModel){model}).ValidationContext";
        var dynamicMessage = site.Method.Parameters.Single(static parameter => parameter.Name == "message").Type.SpecialType != SpecialType.System_String;
        var modelType = site.Method.Parameters[0].Type;
        var messageCheck = dynamicMessage
            ? "global::System.ArgumentNullException.ThrowIfNull(@message);"
            : "global::System.ArgumentException.ThrowIfNullOrEmpty(@message);";
        var message = dynamicMessage ? "@message(value)" : "@message";
        var body = $$"""
            global::System.ArgumentNullException.ThrowIfNull({{model}});
            global::System.ArgumentNullException.ThrowIfNull(@viewModelProperty);
            global::System.ArgumentNullException.ThrowIfNull(@isPropertyValid);
            {{messageCheck}}
            var capturedContext = {{context}};
            global::System.ArgumentNullException.ThrowIfNull(capturedContext);
            var selector = new global::{{site.NamespaceRoot}}.Capabilities.ValidationSelector<{{GeneratorHelpers.TypeName(modelType)}}, {{GeneratorHelpers.TypeName(valueType)}}>(
                source => {{selector!.EmitAccessPlan("source", site.NamespaceRoot)}});
            return global::{{site.NamespaceRoot}}.Capabilities.ValidationRuntime.RegisterRule(
                {{model}}, capturedContext, selector,
                value => @isPropertyValid(value) ? global::{{site.NamespaceRoot}}.States.ValidationState.Valid
                    : new global::{{site.NamespaceRoot}}.States.ValidationState(false, {{message}}));
            """;
        if (!HostedPlanEmitter.TryWrap(site, body, out source, out reason, selector!.RequiresLexicalAccess))
        {
            diagnostic = Diagnostic.Create(
                ValidationGenerator.UnsupportedSelector,
                expression.GetLocation(),
                $"{reason} Supply a typed ValidationSelector or registered operation from a location that can access the selected members.");
        }

        return true;
    }
}
