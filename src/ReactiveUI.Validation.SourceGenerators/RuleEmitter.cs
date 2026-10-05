// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators;

/// <summary>Emits predicate rules through the safe observable registration API.</summary>
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
        var reason = "An inline property selector is required.";
        if (expression is null || !GeneratorHelpers.TrySelector(site, expression, out var selector, out reason))
        {
            diagnostic = Diagnostic.Create(
                ValidationGenerator.UnsupportedSelector,
                expression?.GetLocation() ?? site.Invocation.GetLocation(),
                $"{reason} Use an explicit observable or ValidationRuleUnsafe.");
            return true;
        }

        var model = $"@{site.Method.Parameters[0].Name}";
        var predicate = site.Method.Parameters.Single(static parameter => parameter.Name == "isPropertyValid");
        var valueType = ((INamedTypeSymbol)predicate.Type).TypeArguments[0];
        var context = site.Method.Parameters.Any(static parameter => parameter.Name == "context") ? "@context" : $"{model}.ValidationContext";
        var dynamicMessage = site.Method.Parameters.Single(static parameter => parameter.Name == "message").Type.SpecialType != SpecialType.System_String;
        var descriptorList = new List<string>();
        for (var index = 0; index < selector!.Properties.Length; index++)
        {
            var owner = selector.OwnerGetter(model, index);
            var name = GeneratorHelpers.Quote(selector.Properties[index].Name);
            descriptorList.Add($"new global::{site.NamespaceRoot}.Extensions.GeneratedValidationProperty(() => {owner}, {name})");
        }

        var descriptors = string.Join(", ", descriptorList);
        var messageCheck = dynamicMessage
            ? "global::System.ArgumentNullException.ThrowIfNull(@message);"
            : "global::System.ArgumentException.ThrowIfNullOrEmpty(@message);";
        var message = dynamicMessage ? "@message(value)" : "@message";
        source = $$"""
            {{site.Attribute}}
            {{GeneratorHelpers.MethodHeader(site)}}
            {
                global::System.ArgumentNullException.ThrowIfNull({{model}});
                global::System.ArgumentNullException.ThrowIfNull(@viewModelProperty);
                global::System.ArgumentNullException.ThrowIfNull(@isPropertyValid);
                {{messageCheck}}
                var capturedContext = {{context}};
                global::System.ArgumentNullException.ThrowIfNull(capturedContext);
                var values = global::{{site.NamespaceRoot}}.Extensions.GeneratedValidationObservation.Observe<{{GeneratorHelpers.TypeName(valueType)}}>(
                    () => {{selector.Getter(model)}}, {{descriptors}});
                return global::{{site.NamespaceRoot}}.Extensions.ObservableValidationRuleExtensions.AddObservableRule(
                    capturedContext, values,
                    value => @isPropertyValid(value) ? global::{{site.NamespaceRoot}}.States.ValidationState.Valid
                        : new global::{{site.NamespaceRoot}}.States.ValidationState(false, {{message}}),
                    new string[] { {{GeneratorHelpers.Quote(selector.Path)}} });
            }
            """;
        return true;
    }
}
