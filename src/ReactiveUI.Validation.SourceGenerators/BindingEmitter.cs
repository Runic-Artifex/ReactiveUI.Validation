// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ReactiveUI.Validation.SourceGenerators;

/// <summary>Emits statically observed validation bindings and direct assignments.</summary>
internal static class BindingEmitter
{
    /// <summary>Distance from the target chain end to the final property owner.</summary>
    private const int TargetParentOffset = 2;

    /// <summary>Explains why a familiar binding call needs a supported static selector.</summary>
    private static readonly DiagnosticDescriptor Unsupported = new(
        "RUVG006",
        "Validation binding cannot be generated",
        "{0} Use a typed source observable and BindObservableValidationState/BindObservablePropertyValidationState, or explicitly choose the corresponding Unsafe method",
        "ReactiveUI.Validation.Generation",
        DiagnosticSeverity.Error,
        true);

    /// <summary>Emits supported familiar binding call shapes.</summary>
    /// <param name="site">The resolved invocation.</param>
    /// <param name="source">The complete interceptor method.</param>
    /// <param name="diagnostic">An actionable error for unsupported binding shapes.</param>
    /// <returns>Whether the invocation belongs to the binding surface.</returns>
    internal static bool TryEmit(CallSite site, out string? source, out Diagnostic? diagnostic)
    {
        source = null;
        diagnostic = null;
        if (site.Method.Name is not ("BindValidationState" or "BindValidation" or "BindValidationContext"))
        {
            return false;
        }

        try
        {
            source = Emit(site);
        }
        catch (UnsupportedBindingException exception)
        {
            diagnostic = Diagnostic.Create(Unsupported, exception.Location ?? site.Invocation.GetLocation(), exception.Message);
        }

        return true;
    }

    /// <summary>Assembles a generated binding after validating its complete shape.</summary>
    /// <param name="site">The resolved invocation.</param>
    /// <returns>The complete interceptor method.</returns>
    private static string Emit(CallSite site)
    {
        var shape = CreateShape(site);
        var body = new StringBuilder();
        _ = body.AppendLine(site.Attribute).AppendLine(GeneratorHelpers.MethodHeader(site)).AppendLine("{");
        AppendGuards(body, site);
        AppendSources(body, shape);
        var converter = GetConverter(body, shape);
        var bindCall = GetBindCall(shape, converter);
        if (shape.NestedTarget is null)
        {
            _ = body.AppendLine($"    return {bindCall};");
        }
        else
        {
            AppendTargetBinding(body, shape, bindCall);
        }

        _ = body.AppendLine("}");
        return body.ToString();
    }

    /// <summary>Resolves overload semantics and observed source selectors.</summary>
    /// <param name="site">The resolved invocation.</param>
    /// <returns>The validated binding shape.</returns>
    /// <exception cref="UnsupportedBindingException">The call uses an unsupported binding shape.</exception>
    private static BindingShape CreateShape(CallSite site)
    {
        var shape = new BindingShape(site);
        var viewType = FindParameterType(site, "view");
        if (!viewType.IsReferenceType)
        {
            throw new UnsupportedBindingException(
                "The view must have a reference type ownership contract. A concrete value-type view would box notification owners "
                + "and assign captured copies; use an interface-typed boxed view or an explicit observable and callback.");
        }

        if (!Notifies(viewType))
        {
            throw new UnsupportedBindingException(
                "The view must implement System.ComponentModel.INotifyPropertyChanged so ViewModel replacements can be observed.");
        }

        var sourceName = shape.SelectedContext ? "contextProperty" : shape.HelperName;
        if (sourceName is not null)
        {
            shape.SelectedProperties = Parse(site, sourceName, true);
            for (var index = 0; index < shape.SelectedProperties.Count - 1; index++)
            {
                if (!shape.SelectedProperties[index].Type.IsReferenceType)
                {
                    throw new UnsupportedBindingException(
                        "Observed helper/context parents must be reference types; provide an explicit observable for value-type parent chains.",
                        site.GetArgument(sourceName)?.GetLocation());
                }
            }
        }

        var modelPropertyName = FindParameter(site, "modelProperty", "viewModelProperty");
        if (modelPropertyName is not null)
        {
            var names = new List<string>();
            foreach (var property in Parse(site, modelPropertyName, false))
            {
                names.Add(property.Name);
            }

            shape.Path = string.Join(".", names);
        }

        SetTarget(shape);
        return shape;
    }

    /// <summary>Validates target access and resolves direct versus observed-parent assignment.</summary>
    /// <param name="shape">The validated binding overload.</param>
    /// <exception cref="UnsupportedBindingException">The call uses an unsupported binding shape.</exception>
    private static void SetTarget(BindingShape shape)
    {
        var targetName = FindParameter(shape.Site, "viewProperty");
        if (targetName is null)
        {
            shape.Callback = shape.SelectedContext ? "@action" : "@onNext";
            return;
        }

        var target = Parse(shape.Site, targetName, false);
        var property = target[target.Count - 1];
        if (property.SetMethod is null || property.SetMethod.IsInitOnly || !Accessible(shape.Site, property.SetMethod))
        {
            throw new UnsupportedBindingException(
                "The target property must have an accessible ordinary setter; init-only and private setters cannot be assigned by generated code.",
                shape.Site.GetArgument(targetName)?.GetLocation());
        }

        ValidateTextTarget(shape, property, targetName);
        ValidateStateTarget(shape, property, targetName);
        if (target.Count > 1)
        {
            ValidateTargetParents(shape, target, targetName);
            shape.NestedTarget = target;
            shape.Callback = "onNext";
        }
        else
        {
            shape.Callback = $"value => @view.@{property.Name} = value";
        }

        shape.HasTarget = true;
    }

    /// <summary>Requires every observed target parent to expose declared replacement notifications.</summary>
    /// <param name="shape">The validated binding overload.</param>
    /// <param name="target">The target property chain.</param>
    /// <param name="targetName">The target selector parameter name.</param>
    /// <exception cref="UnsupportedBindingException">The call uses an unsupported binding shape.</exception>
    private static void ValidateTargetParents(BindingShape shape, List<IPropertySymbol> target, string targetName)
    {
        for (var index = 0; index < target.Count - 1; index++)
        {
            var owner = index == 0 ? FindParameterType(shape.Site, "view") : target[index - 1].Type;
            if (!Notifies(owner) || !target[index].Type.IsReferenceType)
            {
                throw new UnsupportedBindingException(
                    "Nested target parents must be reference types and every observed owner must implement INotifyPropertyChanged. "
                    + "Use the callback overload with an explicit target replacement stream.",
                    shape.Site.GetArgument(targetName)?.GetLocation());
            }
        }
    }

    /// <summary>Ensures legacy text assignment has a static implicit conversion.</summary>
    /// <param name="shape">The validated binding overload.</param>
    /// <param name="property">The resolved writable property.</param>
    /// <param name="targetName">The target selector parameter name.</param>
    /// <exception cref="UnsupportedBindingException">The call uses an unsupported binding shape.</exception>
    private static void ValidateTextTarget(BindingShape shape, IPropertySymbol property, string targetName)
    {
        if (!shape.Text)
        {
            return;
        }

        var stringType = shape.Site.Model.Compilation.GetSpecialType(SpecialType.System_String);
        if (!shape.Site.Model.Compilation.ClassifyConversion(stringType, property.Type).IsImplicit)
        {
            throw new UnsupportedBindingException(
                "Validation text targets must accept string implicitly. Use BindValidationState with a converter for other target types.",
                shape.Site.GetArgument(targetName)?.GetLocation());
        }
    }

    /// <summary>Rejects selector upcasts whose projected value cannot be assigned to the actual property.</summary>
    /// <param name="shape">The resolved state projection.</param>
    /// <param name="property">The actual writable target property.</param>
    /// <param name="targetName">The target selector parameter name.</param>
    /// <exception cref="UnsupportedBindingException">The projected output cannot be assigned statically.</exception>
    private static void ValidateStateTarget(BindingShape shape, IPropertySymbol property, string targetName)
    {
        if (shape.Text)
        {
            return;
        }

        var output = ProjectionType(shape.Site);
        if (output.TypeKind == TypeKind.Dynamic || !shape.Site.Model.Compilation.ClassifyConversion(output, property.Type).IsImplicit)
        {
            throw new UnsupportedBindingException(
                "The converter output must be statically assignable to the actual target property. "
                + "Avoid selector upcasts that hide a narrower property type, or use the callback overload with an explicit conversion.",
                shape.Site.GetArgument(targetName)?.GetLocation());
        }
    }

    /// <summary>Reads the closed converter return type rather than any implicit selector upcast.</summary>
    /// <param name="site">The resolved state binding call.</param>
    /// <returns>The caller's typed presentation output.</returns>
    private static ITypeSymbol ProjectionType(CallSite site)
    {
        var converter = (INamedTypeSymbol)FindParameterType(site, "converter");
        return converter.TypeArguments[converter.TypeArguments.Length - 1];
    }

    /// <summary>Preserves null argument checks without invoking the expression-based stub.</summary>
    /// <param name="body">The generated method buffer.</param>
    /// <param name="site">The resolved invocation.</param>
    private static void AppendGuards(StringBuilder body, CallSite site)
    {
        foreach (var parameter in site.Method.Parameters)
        {
            if (parameter.Type.IsReferenceType && parameter.Name is not ("viewModel" or "formatter"))
            {
                _ = body.AppendLine($"    global::System.ArgumentNullException.ThrowIfNull(@{parameter.Name});");
            }
        }
    }

    /// <summary>Observes the current helper/context and every replacing owner with typed getters.</summary>
    /// <param name="body">The generated method buffer.</param>
    /// <param name="shape">The validated binding overload.</param>
    private static void AppendSources(StringBuilder body, BindingShape shape)
    {
        var descriptors = new List<string> { Descriptor(shape.ExtensionRoot, "@view", "ViewModel") };
        string sourceGetter;
        string sourceType;
        if (shape.SelectedProperties is { } properties)
        {
            var getter = new StringBuilder(shape.ModelGetter);
            foreach (var property in properties)
            {
                descriptors.Add(Descriptor(shape.ExtensionRoot, getter.ToString(), property.Name));
                _ = getter.Append("?.@").Append(property.Name);
            }

            sourceGetter = getter.ToString();
            sourceType = shape.SelectedContext
                ? $"{shape.NamespaceRoot}.Contexts.IValidationContext"
                : GeneratorHelpers.TypeName(properties[properties.Count - 1].Type.WithNullableAnnotation(NullableAnnotation.NotAnnotated));
        }
        else
        {
            descriptors.Add(Descriptor(shape.ExtensionRoot, shape.ModelGetter, "ValidationContext"));
            sourceGetter = $"(({shape.NamespaceRoot}.Abstractions.IValidatableViewModel?)({shape.ModelGetter}))?.ValidationContext";
            sourceType = $"{shape.NamespaceRoot}.Contexts.IValidationContext";
        }

        AppendObservation(body, shape.ExtensionRoot, "sources", sourceType, sourceGetter, descriptors);
    }

    /// <summary>Builds one direct property-notification descriptor.</summary>
    /// <param name="extensionRoot">The flavor-specific extension namespace.</param>
    /// <param name="owner">The typed owner getter expression.</param>
    /// <param name="propertyName">The observed declared property name.</param>
    /// <returns>The generated descriptor expression.</returns>
    private static string Descriptor(string extensionRoot, string owner, string propertyName) =>
        $"new {extensionRoot}GeneratedValidationProperty(() => (global::System.ComponentModel.INotifyPropertyChanged?)({owner}), {GeneratorHelpers.Quote(propertyName)})";

    /// <summary>Emits a reference-identity observation with null and replacement delivery.</summary>
    /// <param name="body">The generated method buffer.</param>
    /// <param name="extensionRoot">The flavor-specific extension namespace.</param>
    /// <param name="name">The semantic parameter or generated local name.</param>
    /// <param name="type">The declared value or owner type.</param>
    /// <param name="getter">The direct typed value getter.</param>
    /// <param name="descriptors">The ordered notification descriptors.</param>
    private static void AppendObservation(StringBuilder body, string extensionRoot, string name, string type, string getter, List<string> descriptors) =>
        _ = body.AppendLine($"    var {name} = {extensionRoot}GeneratedValidationObservation.ObserveReference<{type}>(")
            .AppendLine($"        () => {getter},")
            .AppendLine($"        {string.Join(",\n        ", descriptors)});");

    /// <summary>Preserves formatter resolution and the legacy first-nonempty-message projection.</summary>
    /// <param name="body">The generated method buffer.</param>
    /// <param name="shape">The validated binding overload.</param>
    /// <returns>The typed projection expression.</returns>
    private static string GetConverter(StringBuilder body, BindingShape shape)
    {
        if (shape.Text && shape.HasTarget)
        {
            var formatter = FindParameter(shape.Site, "formatter") is null ? "null" : "@formatter";
            _ = body.AppendLine($"    var resolvedFormatter = {shape.ExtensionRoot}GeneratedValidationBindingSupport.ResolveFormatter({formatter});");
            return shape.Path is null
                ? "state => resolvedFormatter.Format(state.Text)"
                : $"states => {shape.ExtensionRoot}GeneratedValidationBindingSupport.FirstNonEmptyMessage(states, resolvedFormatter)";
        }

        return shape.SelectedContext ? "static state => state" : "@converter";
    }

    /// <summary>Chooses the runtime stream-only API and preserves complete state objects and strictness.</summary>
    /// <param name="shape">The validated binding overload.</param>
    /// <param name="converter">The complete-state projection expression.</param>
    /// <returns>The direct runtime binding call.</returns>
    private static string GetBindCall(BindingShape shape, string converter)
    {
        if (shape.Path is null)
        {
            var select = shape.HelperName is not null ? "static source => source.ValidationChanged" : "static source => source.ValidationStatusChange";
            return $"{shape.ExtensionRoot}ObservableValidationBindingExtensions.BindObservableValidationState(sources, {select}, {converter}, {shape.Callback})";
        }

        var strict = FindParameter(shape.Site, "strict") is null ? "true" : "@strict";
        return $"{shape.ExtensionRoot}ObservableValidationBindingExtensions.BindObservablePropertyValidationState"
            + $"(sources, static source => source, {GeneratorHelpers.Quote(shape.Path)}, {converter}, {shape.Callback}, {strict})";
    }

    /// <summary>Wires target-parent replacements to cached projected values and a direct final setter.</summary>
    /// <param name="body">The generated method buffer.</param>
    /// <param name="shape">The validated binding overload.</param>
    /// <param name="bindCall">The typed runtime binding invocation.</param>
    private static void AppendTargetBinding(StringBuilder body, BindingShape shape, string bindCall)
    {
        var target = shape.NestedTarget!;
        var descriptors = new List<string>();
        var getter = new StringBuilder("@view");
        for (var index = 0; index < target.Count - 1; index++)
        {
            descriptors.Add(Descriptor(shape.ExtensionRoot, getter.ToString(), target[index].Name));
            _ = getter.Append(index == 0 ? ".@" : "?.@").Append(target[index].Name);
        }

        var final = target[target.Count - 1];
        var parent = target[target.Count - TargetParentOffset];
        var parentType = GeneratorHelpers.TypeName(parent.Type.WithNullableAnnotation(NullableAnnotation.NotAnnotated));
        var outputType = shape.Text ? "string" : GeneratorHelpers.TypeName(ProjectionType(shape.Site));
        AppendObservation(body, shape.ExtensionRoot, "targets", parentType, getter.ToString(), descriptors);
        _ = body.AppendLine($"    return {shape.ExtensionRoot}GeneratedValidationBindingSupport.BindToTarget<{parentType}, {outputType}>(")
            .AppendLine($"        targets, onNext => {bindCall}, static (target, value) => target.@{final.Name} = value);");
    }

    /// <summary>Finds one supported semantic parameter name without positional syntax assumptions.</summary>
    /// <param name="site">The resolved invocation.</param>
    /// <param name="names">The supported semantic parameter names.</param>
    /// <returns>The matching semantic name, or null.</returns>
    private static string? FindParameter(CallSite site, params string[] names)
    {
        foreach (var parameter in site.Method.Parameters)
        {
            foreach (var name in names)
            {
                if (parameter.Name == name)
                {
                    return name;
                }
            }
        }

        return null;
    }

    /// <summary>Reads the closed parameter type from the resolved method.</summary>
    /// <param name="site">The resolved invocation.</param>
    /// <param name="name">The semantic parameter or generated local name.</param>
    /// <returns>The closed method parameter type.</returns>
    /// <exception cref="InvalidOperationException">The call uses an unsupported binding shape.</exception>
    private static ITypeSymbol FindParameterType(CallSite site, string name)
    {
        foreach (var parameter in site.Method.Parameters)
        {
            if (parameter.Name == name)
            {
                return parameter.Type;
            }
        }

        throw new InvalidOperationException($"Binding overload is missing its required '{name}' parameter.");
    }

    /// <summary>Extracts declared readable property metadata while checking observed owner contracts when needed.</summary>
    /// <param name="site">The resolved invocation.</param>
    /// <param name="parameterName">The selector parameter name.</param>
    /// <param name="requireNotification">Whether every observed owner must declare change notifications.</param>
    /// <returns>The full root-relative property chain.</returns>
    /// <exception cref="UnsupportedBindingException">The call uses an unsupported binding shape.</exception>
    private static List<IPropertySymbol> Parse(CallSite site, string parameterName, bool requireNotification)
    {
        var expression = site.GetArgument(parameterName);
        var lambda = expression is null ? null : Unwrap(expression) as LambdaExpressionSyntax;
        var parameter = LambdaParameter(lambda);
        if (parameter is null || lambda!.Body is not ExpressionSyntax body)
        {
            throw new UnsupportedBindingException(
                $"Selector '{parameterName}' must be an inline single-parameter property lambda. Stored selectors and statement bodies are runtime inputs.",
                expression?.GetLocation());
        }

        var properties = new List<IPropertySymbol>();
        var current = Unwrap(body);
        while (current is MemberAccessExpressionSyntax member)
        {
            properties.Insert(0, ReadProperty(site, member, parameterName, requireNotification));
            current = Unwrap(member.Expression);
        }

        var rootParameter = site.Model.GetDeclaredSymbol(parameter);
        if (properties.Count == 0 || current is not IdentifierNameSyntax identifier
            || !SymbolEqualityComparer.Default.Equals(site.Model.GetSymbolInfo(identifier).Symbol, rootParameter))
        {
            throw new UnsupportedBindingException(
                $"Selector '{parameterName}' must be rooted in its lambda parameter; calls, captured values, indexers and conversions are unsupported.",
                expression!.GetLocation());
        }

        return properties;
    }

    /// <summary>Reads one selector property, rejecting inaccessible or generated-only contracts.</summary>
    /// <param name="site">The resolved invocation.</param>
    /// <param name="member">The declared property access.</param>
    /// <param name="parameterName">The selector parameter name.</param>
    /// <param name="requireNotification">Whether every observed owner must declare change notifications.</param>
    /// <returns>The accessible declared property.</returns>
    /// <exception cref="UnsupportedBindingException">The call uses an unsupported binding shape.</exception>
    private static IPropertySymbol ReadProperty(CallSite site, MemberAccessExpressionSyntax member, string parameterName, bool requireNotification)
    {
        if (site.Model.GetSymbolInfo(member).Symbol is not IPropertySymbol property || property.IsStatic || property.IsIndexer
            || property.GetMethod is null || !Accessible(site, property.GetMethod)
            || !GeneratorHelpers.IsAccessibleType(site.Model.Compilation, property.Type))
        {
            throw new UnsupportedBindingException(
                $"Selector '{parameterName}' must contain readable accessible declared properties. Fields, indexers and properties existing only in another generator's output are unsupported.",
                member.GetLocation());
        }

        var owner = site.Model.GetTypeInfo(member.Expression).Type;
        if (requireNotification && (owner is null || !Notifies(owner)))
        {
            throw new UnsupportedBindingException(
                $"Property '{property.Name}' is owned by a type without INotifyPropertyChanged. Supply an observable that reports replacement.",
                member.GetLocation());
        }

        return property;
    }

    /// <summary>Returns the exact parameter declared by a one-parameter literal lambda.</summary>
    /// <param name="lambda">The inline selector lambda.</param>
    /// <returns>The lambda parameter, or null for unsupported syntax.</returns>
    private static ParameterSyntax? LambdaParameter(LambdaExpressionSyntax? lambda) => lambda switch
    {
        SimpleLambdaExpressionSyntax simple => simple.Parameter,
        ParenthesizedLambdaExpressionSyntax parenthesized when parenthesized.ParameterList.Parameters.Count == 1 => parenthesized.ParameterList.Parameters[0],
        _ => null,
    };

    /// <summary>Removes syntactic parentheses and null-forgiving operators, which do not alter runtime access.</summary>
    /// <param name="expression">The source expression.</param>
    /// <returns>The underlying runtime expression.</returns>
    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (true)
        {
            if (expression is ParenthesizedExpressionSyntax parenthesized)
            {
                expression = parenthesized.Expression;
            }
            else if (expression is PostfixUnaryExpressionSyntax postfix && postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression))
            {
                expression = postfix.Operand;
            }
            else
            {
                return expression;
            }
        }
    }

    /// <summary>Checks accessibility from the actual generated assembly-level host.</summary>
    /// <param name="site">The resolved invocation.</param>
    /// <param name="accessor">The property accessor.</param>
    /// <returns>Whether the generated assembly can access the accessor.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Accessible(CallSite site, IMethodSymbol accessor) =>
        site.Model.Compilation.IsSymbolAccessibleWithin(accessor, site.Model.Compilation.Assembly);

    /// <summary>Checks declared notification contracts including generic constraints.</summary>
    /// <param name="type">The declared value or owner type.</param>
    /// <returns>Whether the owner declares property change notifications.</returns>
    private static bool Notifies(ITypeSymbol type)
    {
        if (type.ToDisplayString() == "System.ComponentModel.INotifyPropertyChanged")
        {
            return true;
        }

        if (type is ITypeParameterSymbol parameter)
        {
            foreach (var constraint in parameter.ConstraintTypes)
            {
                if (Notifies(constraint))
                {
                    return true;
                }
            }
        }

        foreach (var contract in type.AllInterfaces)
        {
            if (contract.ToDisplayString() == "System.ComponentModel.INotifyPropertyChanged")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Holds validated overload semantics during interceptor emission.</summary>
    private sealed class BindingShape
    {
        /// <summary>Initializes a new instance of the <see cref="BindingShape"/> class.</summary>
        /// <param name="site">The resolved binding call.</param>
        internal BindingShape(CallSite site)
        {
            Site = site;
            NamespaceRoot = $"global::{site.NamespaceRoot}";
            ExtensionRoot = $"{NamespaceRoot}.Extensions.";
            Text = site.Method.Name != "BindValidationState";
            SelectedContext = site.Method.Name == "BindValidationContext";
            HelperName = FindParameter(site, "helperProperty", "viewModelHelperProperty");
            var bindingRoot = site.NamespaceRoot.EndsWith(".Reactive", StringComparison.Ordinal)
                ? "global::ReactiveUI.Binding.Reactive."
                : "global::ReactiveUI.Binding.";
            var model = GeneratorHelpers.TypeName(FindParameterType(site, "viewModel").WithNullableAnnotation(NullableAnnotation.NotAnnotated));
            ModelGetter = $"(({bindingRoot}IViewFor<{model}>)@view).ViewModel";
        }

        /// <summary>Gets the resolved invocation.</summary>
        internal CallSite Site { get; }

        /// <summary>Gets the flavor-specific namespace.</summary>
        internal string NamespaceRoot { get; }

        /// <summary>Gets the flavor-specific extension namespace.</summary>
        internal string ExtensionRoot { get; }

        /// <summary>Gets the current typed view-model getter.</summary>
        internal string ModelGetter { get; }

        /// <summary>Gets a value indicating whether the overload presents text.</summary>
        internal bool Text { get; }

        /// <summary>Gets a value indicating whether the overload explicitly selects a context.</summary>
        internal bool SelectedContext { get; }

        /// <summary>Gets the optional helper selector parameter name.</summary>
        internal string? HelperName { get; }

        /// <summary>Gets or sets the observed helper/context selector properties.</summary>
        internal List<IPropertySymbol>? SelectedProperties { get; set; }

        /// <summary>Gets or sets the complete metadata path.</summary>
        internal string? Path { get; set; }

        /// <summary>Gets or sets the statically typed assignment callback.</summary>
        internal string Callback { get; set; } = string.Empty;

        /// <summary>Gets or sets a value indicating whether there is a writable target.</summary>
        internal bool HasTarget { get; set; }

        /// <summary>Gets or sets the observed nested writable target chain.</summary>
        internal List<IPropertySymbol>? NestedTarget { get; set; }
    }

    /// <summary>Carries an unsupported source shape and its exact diagnostic location during validation.</summary>
    [SuppressMessage(
        "Design",
        "CA1032:Implement standard exception constructors",
        Justification = "This private compiler control-flow exception always carries a diagnostic and source location.")]
    [SuppressMessage(
        "Design",
        "CA1064:Exceptions should be public",
        Justification = "The private compiler control-flow exception never crosses the emitter boundary.")]
    [SuppressMessage(
        "Design",
        "SST1488:Exception constructors",
        Justification = "Compiler diagnostic control flow requires only the diagnostic message and source location.")]
    private sealed class UnsupportedBindingException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="UnsupportedBindingException"/> class.</summary>
        /// <param name="message">The actionable diagnostic text.</param>
        /// <param name="location">The rejected source expression.</param>
        internal UnsupportedBindingException(string message, Location? location = null)
            : base(message) => Location = location;

        /// <summary>Gets the rejected source expression.</summary>
        internal Location? Location { get; }
    }
}
