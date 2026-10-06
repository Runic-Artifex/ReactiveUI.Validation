// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators;

/// <summary>Normalizes binding overload semantics independently of selector syntax and access lowering.</summary>
internal sealed class BindingCallPlan
{
    /// <summary>Initializes a new instance of the <see cref="BindingCallPlan"/> class.</summary>
    /// <param name="site">The original semantic call.</param>
    private BindingCallPlan(CallSite site)
    {
        Site = site;
        NamespaceRoot = $"global::{site.NamespaceRoot}";
        ExtensionRoot = $"{NamespaceRoot}.Extensions.";
        CapabilityRoot = $"{NamespaceRoot}.Capabilities.";
        IsStaticFactory = site.Method.Name is "ForProperty" or "ForViewModel" or "ForValidationHelperProperty";
        SourceParameter = FindParameter("contextProperty", "helperProperty", "viewModelHelperProperty");
        PropertyParameter = FindParameter("modelProperty", "viewModelProperty");
        TargetParameter = FindParameter("viewProperty");
        CallbackParameter = FindParameter("onNext", "action");
        FormatterParameter = FindParameter("formatter");
        ConverterParameter = FindParameter("converter");
        StrictParameter = FindParameter("strict");
        ViewType = GetParameterType("view")!;
        ModelType = GetModelType();
        SourceKind = GetSourceKind();
        ProjectionKind = GetProjectionKind();
        OutputType = GetOutputType();
    }

    /// <summary>Gets the resolved semantic invocation.</summary>
    internal CallSite Site { get; }

    /// <summary>Gets the fully qualified runtime flavor namespace.</summary>
    internal string NamespaceRoot { get; }

    /// <summary>Gets the fully qualified extension namespace.</summary>
    internal string ExtensionRoot { get; }

    /// <summary>Gets the fully qualified capability namespace.</summary>
    internal string CapabilityRoot { get; }

    /// <summary>Gets a value indicating whether the original call is a static binding factory.</summary>
    internal bool IsStaticFactory { get; }

    /// <summary>Gets the selected helper/context selector parameter, or null for the default context.</summary>
    internal string? SourceParameter { get; }

    /// <summary>Gets the validation metadata selector parameter, or null for aggregate validation.</summary>
    internal string? PropertyParameter { get; }

    /// <summary>Gets the target selector parameter, or null for a callback.</summary>
    internal string? TargetParameter { get; }

    /// <summary>Gets the callback parameter, or null for a writable target.</summary>
    internal string? CallbackParameter { get; }

    /// <summary>Gets the optional formatter parameter.</summary>
    internal string? FormatterParameter { get; }

    /// <summary>Gets the optional complete-state projection parameter.</summary>
    internal string? ConverterParameter { get; }

    /// <summary>Gets the optional strict matching parameter.</summary>
    internal string? StrictParameter { get; }

    /// <summary>Gets the original view receiver type.</summary>
    internal ITypeSymbol ViewType { get; }

    /// <summary>Gets the original model type, including its generic ownership contract.</summary>
    internal ITypeSymbol ModelType { get; }

    /// <summary>Gets the projected output type before any target assignment conversion.</summary>
    internal ITypeSymbol OutputType { get; }

    /// <summary>Gets the selected validation source contract.</summary>
    internal BindingSourceKind SourceKind { get; }

    /// <summary>Gets the original formatter/converter/callback semantics.</summary>
    internal BindingProjectionKind ProjectionKind { get; }

    /// <summary>Gets a value indicating whether the binding observes property membership.</summary>
    internal bool IsProperty => PropertyParameter is not null;

    /// <summary>Gets the strict matching expression with the familiar exclusive default.</summary>
    internal string StrictExpression => StrictParameter is null ? "true" : $"@{StrictParameter}";

    /// <summary>Creates the common binding representation for an applicable overload.</summary>
    /// <param name="site">The original semantic invocation.</param>
    /// <returns>The overload representation, or null for an unrelated method.</returns>
    internal static BindingCallPlan? Create(CallSite site) =>
        site.Method.Name is "BindValidation" or "BindValidationState" or "BindValidationContext"
            or "ForProperty" or "ForViewModel" or "ForValidationHelperProperty" ? new(site) : null;

    /// <summary>Gets a formal parameter type without relying on positional source arguments.</summary>
    /// <param name="name">The formal parameter name.</param>
    /// <returns>The declared parameter type, or null when absent.</returns>
    internal ITypeSymbol? GetParameterType(string name)
    {
        foreach (var parameter in Site.Method.Parameters)
        {
            if (parameter.Name == name)
            {
                return parameter.Type;
            }
        }

        return null;
    }

    /// <summary>Finds one formal parameter using the legacy and modern semantic names.</summary>
    /// <param name="names">The recognized parameter names.</param>
    /// <returns>The applicable parameter name, or null.</returns>
    private string? FindParameter(params string[] names)
    {
        foreach (var parameter in Site.Method.Parameters)
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

    /// <summary>Reads model identity from an explicit argument, expression input or view contract.</summary>
    /// <returns>The original model type.</returns>
    /// <exception cref="InvalidOperationException">The classified binding has no model contract.</exception>
    private ITypeSymbol GetModelType()
    {
        if (GetParameterType("viewModel") is { } model)
        {
            return model.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
        }

        foreach (var parameter in Site.Method.TypeParameters)
        {
            if (parameter.Name == "TViewModel")
            {
                return Site.Method.TypeArguments[parameter.Ordinal];
            }
        }

        foreach (var contract in ViewType.AllInterfaces)
        {
            if (contract.Name == "IViewFor" && contract.TypeArguments.Length == 1)
            {
                return contract.TypeArguments[0];
            }
        }

        throw new InvalidOperationException("The validation binding overload has no typed view-model contract.");
    }

    /// <summary>Separates formatting callbacks from full-state converters and raw context delivery.</summary>
    /// <returns>The projection contract.</returns>
    private BindingProjectionKind GetProjectionKind()
    {
        if (ConverterParameter is not null)
        {
            return BindingProjectionKind.ConvertState;
        }

        if (TargetParameter is not null)
        {
            return BindingProjectionKind.Text;
        }

        if (!IsStaticFactory)
        {
            return BindingProjectionKind.RawState;
        }

        if (IsProperty)
        {
            return BindingProjectionKind.FormattedPropertyStates;
        }

        return SourceKind == BindingSourceKind.Helper
            ? BindingProjectionKind.FormattedHelperState
            : BindingProjectionKind.FormattedModelState;
    }

    /// <summary>Identifies default-context, selected-context and helper source semantics.</summary>
    /// <returns>The selected source contract.</returns>
    private BindingSourceKind GetSourceKind()
    {
        if (SourceParameter == "contextProperty")
        {
            return BindingSourceKind.SelectedContext;
        }

        return SourceParameter is not null ? BindingSourceKind.Helper : BindingSourceKind.Context;
    }

    /// <summary>Reads formatter/converter delegate return types without substituting the target leaf type.</summary>
    /// <returns>The projection output type.</returns>
    /// <exception cref="InvalidOperationException">The classified binding lacks a typed projection.</exception>
    private ITypeSymbol GetOutputType()
    {
        if (ProjectionKind == BindingProjectionKind.Text)
        {
            return Site.Model.Compilation.GetSpecialType(SpecialType.System_String);
        }

        if (ConverterParameter is not null && GetParameterType(ConverterParameter) is INamedTypeSymbol converter)
        {
            return converter.DelegateInvokeMethod!.ReturnType;
        }

        if (FormatterParameter is not null && GetParameterType(FormatterParameter) is INamedTypeSymbol formatter)
        {
            return formatter.TypeArguments[0];
        }

        if (CallbackParameter is not null && GetParameterType(CallbackParameter) is INamedTypeSymbol callback)
        {
            return callback.DelegateInvokeMethod!.Parameters[0].Type;
        }

        throw new InvalidOperationException("The validation binding overload has no typed projection contract.");
    }
}
