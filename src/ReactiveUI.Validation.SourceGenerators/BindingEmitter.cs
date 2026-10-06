// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ReactiveUI.Validation.SourceGenerators;

/// <summary>Lowers familiar extension and static binding calls through shared semantic access plans.</summary>
internal static class BindingEmitter
{
    /// <summary>Explains the typed observation or writable capability required by a binding.</summary>
    private static readonly DiagnosticDescriptor Unsupported = new(
        "RUVG006",
        "Validation binding cannot be generated",
        "{0} Use typed ValidationSelector/ValidationTarget, declare ValidationRuntimeDispatch for an explicitly registered selector, or choose the corresponding Unsafe method",
        "ReactiveUI.Validation.Generation",
        DiagnosticSeverity.Error,
        true);

    /// <summary>Emits an applicable binding call without a separate selector syntax parser.</summary>
    /// <param name="site">The original semantic invocation.</param>
    /// <param name="source">The generated method and hosted payload.</param>
    /// <param name="diagnostic">An actionable capability diagnostic.</param>
    /// <returns>Whether the call belongs to the binding surface.</returns>
    internal static bool TryEmit(CallSite site, out string? source, out Diagnostic? diagnostic)
    {
        source = null;
        diagnostic = null;
        var call = BindingCallPlan.Create(site);
        if (call is null)
        {
            return false;
        }

        if (!TryBuildBody(call, out var body, out var reason, out var location)
            || !HostedPlanEmitter.TryWrap(site, body, out source, out reason))
        {
            diagnostic = Diagnostic.Create(Unsupported, location ?? site.Invocation.GetLocation(), reason);
        }

        return true;
    }

    /// <summary>Builds a typed source, projection and target operation using the shared semantic backend.</summary>
    /// <param name="call">The normalized overload.</param>
    /// <param name="body">Statements suitable for ordinary or hosted interception.</param>
    /// <param name="reason">The missing capability explanation.</param>
    /// <param name="location">The offending original expression.</param>
    /// <returns>Whether the complete binding can be lowered.</returns>
    private static bool TryBuildBody(BindingCallPlan call, out string body, out string reason, out Location? location)
    {
        body = string.Empty;
        reason = string.Empty;
        location = null;
        var text = new StringBuilder();
        AppendGuards(text, call);
        if (!AppendSelection(text, call, out reason, out location))
        {
            return false;
        }

        if (call.IsProperty && !AppendMetadata(text, call, out reason, out location))
        {
            return false;
        }

        AppendStates(text, call);
        var values = AppendProjection(text, call);
        if (call.TargetParameter is null)
        {
            AppendCallback(text, call, values);
        }
        else if (!AppendTarget(text, call, values, out reason, out location))
        {
            return false;
        }

        body = text.ToString();
        return true;
    }

    /// <summary>Preserves the original argument validation before subscriptions or getters are acquired.</summary>
    /// <param name="body">The generated statements.</param>
    /// <param name="call">The normalized overload.</param>
    private static void AppendGuards(StringBuilder body, BindingCallPlan call)
    {
        foreach (var parameter in call.Site.Method.Parameters)
        {
            if (parameter.Type.IsReferenceType && parameter.Name != "viewModel"
                && (parameter.Name != "formatter" || call.TargetParameter is null))
            {
                _ = body.AppendLine($"global::System.ArgumentNullException.ThrowIfNull(@{parameter.Name});");
            }
        }
    }

    /// <summary>Creates a root-model selection so replacement consumes the observed model rather than a copied receiver.</summary>
    /// <param name="body">The generated statements.</param>
    /// <param name="call">The normalized overload.</param>
    /// <param name="reason">The source capability diagnostic.</param>
    /// <param name="location">The source selector location.</param>
    /// <returns>Whether the source can be observed.</returns>
    private static bool AppendSelection(StringBuilder body, BindingCallPlan call, out string reason, out Location? location)
    {
        reason = string.Empty;
        location = null;
        var modelType = GeneratorHelpers.TypeName(call.ModelType);
        var contextType = $"{call.NamespaceRoot}.Contexts.IValidationContext?";
        if (call.SourceParameter is null)
        {
            _ = body.AppendLine($"var selection = {call.CapabilityRoot}ValidationRuntime.DefaultContext<{modelType}>(@view);");
            return true;
        }

        var expression = call.Site.GetArgument(call.SourceParameter);
        location = expression?.GetLocation();
        var selectedType = call.SourceKind == BindingSourceKind.Helper
            ? $"{call.NamespaceRoot}.Helpers.ValidationHelper?"
            : contextType;
        if (expression is null || !GeneratorHelpers.TrySelector(call.Site, expression, out var selector, out reason))
        {
            return false;
        }

        var expressionRoot = GeneratorHelpers.TypeName(ValidationSelectorSignature.SourceType(call.GetParameterType(call.SourceParameter)!));
        var role = call.SourceKind == BindingSourceKind.Helper ? "Helper" : "Context";
        _ = body.AppendLine($"var generatedSelection = new {call.CapabilityRoot}ValidationSelector<{expressionRoot}, {selectedType}>(boundModel =>")
            .AppendLine($"    {selector!.EmitAccessPlan("boundModel", call.Site.NamespaceRoot, ReferenceOptions(call, selectedType))});")
            .AppendLine($"var selection = new {call.CapabilityRoot}ValidationSelector<{modelType}, {selectedType}>(boundModel =>")
            .AppendLine($"    {call.CapabilityRoot}ValidationRuntime.ResolveSelector<{expressionRoot}, {selectedType}>(")
            .AppendLine($"        boundModel, @view, @{call.SourceParameter}, {call.CapabilityRoot}ValidationPlanRole.{role}, global::System.String.Empty, generatedSelection).Bind(boundModel));");
        return true;
    }

    /// <summary>Creates metadata-only access, which does not execute the selected value getter.</summary>
    /// <param name="body">The generated statements.</param>
    /// <param name="call">The normalized overload.</param>
    /// <param name="reason">The missing metadata capability.</param>
    /// <param name="location">The metadata selector location.</param>
    /// <returns>Whether complete path snapshots can be obtained.</returns>
    private static bool AppendMetadata(StringBuilder body, BindingCallPlan call, out string reason, out Location? location)
    {
        reason = string.Empty;
        var expression = call.Site.GetArgument(call.PropertyParameter!);
        location = expression?.GetLocation();
        var modelType = GeneratorHelpers.TypeName(call.ModelType);
        if (expression is null || !GeneratorHelpers.TryMetadataSelector(call.Site, expression, out var selector, out reason))
        {
            return false;
        }

        var memberType = GeneratorHelpers.TypeName(ValidationSelectorSignature.ValueType(call.GetParameterType(call.PropertyParameter!)!));
        _ = body.AppendLine($"var generatedPath = new {call.CapabilityRoot}ValidationSelector<{modelType}, {call.CapabilityRoot}ValidationPath>(boundModel =>")
            .AppendLine($"    {selector!.EmitMetadataPlan("boundModel", call.Site.NamespaceRoot)});")
            .AppendLine($"var propertyPath = new {call.CapabilityRoot}ValidationSelector<{modelType}, {call.CapabilityRoot}ValidationPath>(boundModel =>")
            .AppendLine($"    {call.CapabilityRoot}ValidationRuntime.ResolvePathSelector<{modelType}, {memberType}>(")
            .AppendLine($"        boundModel, @view, @{call.PropertyParameter}, {call.CapabilityRoot}ValidationPlanRole.Property, global::System.String.Empty, generatedPath).Bind(boundModel));");
        return true;
    }

    /// <summary>Preserves interface state dispatch, actual initial states and current view-model ownership.</summary>
    /// <param name="body">The generated statements.</param>
    /// <param name="call">The normalized overload.</param>
    private static void AppendStates(StringBuilder body, BindingCallPlan call)
    {
        var viewType = GeneratorHelpers.TypeName(call.ViewType);
        var modelType = GeneratorHelpers.TypeName(call.ModelType);
        var initial = $"{call.CapabilityRoot}ValidationInitialSequence.Actual";
        if (call.IsProperty)
        {
            _ = body.AppendLine($"var states = {call.CapabilityRoot}ValidationRuntime.ObserveViewProperty<{viewType}, {modelType}>(")
                .AppendLine($"    @view, selection, propertyPath, {call.StrictExpression}, {initial});");
            return;
        }

        var sourceType = call.SourceKind == BindingSourceKind.Helper
            ? $"{call.NamespaceRoot}.Helpers.ValidationHelper?"
            : $"{call.NamespaceRoot}.Contexts.IValidationContext?";
        var stream = call.SourceKind == BindingSourceKind.Helper
            ? "static selected => selected?.ValidationChanged"
            : "static selected => selected?.ValidationStatusChange";
        _ = body.AppendLine($"var states = {call.CapabilityRoot}ValidationRuntime.ObserveViewState<{viewType}, {modelType}, {sourceType}>(")
            .AppendLine($"    @view, selection, {stream}, {initial});");
    }

    /// <summary>Formats text or applies caller delegates without changing raw validation state identity.</summary>
    /// <param name="body">The generated statements.</param>
    /// <param name="call">The normalized overload.</param>
    /// <returns>The projected or unmodified observable local.</returns>
    private static string AppendProjection(StringBuilder body, BindingCallPlan call)
    {
        string converter;
        if (call.ProjectionKind == BindingProjectionKind.Text)
        {
            var formatter = call.FormatterParameter is null ? "null" : $"@{call.FormatterParameter}";
            _ = body.AppendLine($"var resolvedFormatter = {call.ExtensionRoot}GeneratedValidationBindingSupport.ResolveFormatter({formatter});");
            converter = call.IsProperty
                ? $"value => {call.ExtensionRoot}GeneratedValidationBindingSupport.FirstNonEmptyMessage(value, resolvedFormatter)"
                : "value => resolvedFormatter.Format(value.Text)";
        }
        else if (call.ProjectionKind == BindingProjectionKind.ConvertState)
        {
            converter = $"@{call.ConverterParameter}";
        }
        else if (call.ProjectionKind == BindingProjectionKind.FormattedModelState)
        {
            converter = $"value => @{call.FormatterParameter}.Format(value.Text)";
        }
        else
        {
            return "states";
        }

        _ = body.AppendLine($"var values = {call.CapabilityRoot}ValidationRuntime.Project(states, {converter});");
        return "values";
    }

    /// <summary>Preserves raw/formatter paired callback semantics, including the complete ordered FormatAll list.</summary>
    /// <param name="body">The generated statements.</param>
    /// <param name="call">The normalized overload.</param>
    /// <param name="values">The observable local.</param>
    private static void AppendCallback(StringBuilder body, BindingCallPlan call, string values)
    {
        var callback = call.ProjectionKind switch
        {
            BindingProjectionKind.FormattedPropertyStates => $"raw => @{call.CallbackParameter}(raw, {call.CapabilityRoot}ValidationRuntime.FormatAll(raw, @{call.FormatterParameter}))",
            BindingProjectionKind.FormattedHelperState => $"raw => @{call.CallbackParameter}(raw, @{call.FormatterParameter}.Format(raw.Text))",
            _ => $"@{call.CallbackParameter}",
        };

        _ = body.AppendLine($"return {call.CapabilityRoot}ValidationRuntime.Bind({values}, {callback});");
    }

    /// <summary>Creates a typed target handoff with reference identity, null caching and outward struct write-back.</summary>
    /// <param name="body">The generated statements.</param>
    /// <param name="call">The normalized overload.</param>
    /// <param name="values">The projected observable.</param>
    /// <param name="reason">The missing writable capability.</param>
    /// <param name="location">The target selector location.</param>
    /// <returns>Whether the output has a legal typed assignment route.</returns>
    private static bool AppendTarget(StringBuilder body, BindingCallPlan call, string values, out string reason, out Location? location)
    {
        reason = string.Empty;
        var expression = call.Site.GetArgument(call.TargetParameter!);
        location = expression?.GetLocation();
        var viewType = GeneratorHelpers.TypeName(call.ViewType);
        var outputType = GeneratorHelpers.TypeName(call.OutputType);
        if (expression is null || !GeneratorHelpers.TryAccessPlan(call.Site, expression, out var target, out reason))
        {
            return false;
        }

        var conversion = call.Site.Model.Compilation.ClassifyConversion(call.OutputType, target!.ValueType);
        if (!conversion.IsImplicit)
        {
            reason = "The projection output must be assignable to the actual writable storage. Supply a typed conversion target for this selector.";
            return false;
        }

        if (conversion.IsDynamic)
        {
            reason = "The target assignment needs a statically typed output conversion. Supply a typed converter or target instead of a dynamic assignment.";
            return false;
        }

        var inputType = BindingWriteContract.InputType(target.ValueType, target.StorageMember);
        if (BindingWriteContract.RequiresNullPolicy(call.OutputType, target.StorageMember, conversion.MethodSymbol)
            || BindingNullability.RequiresPolicy(call.OutputType, inputType, conversion.MethodSymbol))
        {
            reason = "The nullable projection cannot satisfy the actual writable storage contract. Supply a typed target with an explicit nullable-value policy.";
            return false;
        }

        var memberType = GeneratorHelpers.TypeName(ValidationSelectorSignature.ValueType(call.GetParameterType(call.TargetParameter!)!));
        _ = body.AppendLine($"var generatedTarget = new {call.CapabilityRoot}ValidationTarget<{viewType}, {outputType}>(boundView =>")
            .AppendLine($"    {target.EmitTargetPlan("boundView", call.Site.NamespaceRoot, outputType)});")
            .AppendLine($"var target = {call.CapabilityRoot}ValidationRuntime.ResolveTarget<{viewType}, {memberType}, {outputType}>(")
            .AppendLine($"    @view, @{call.TargetParameter}, global::System.String.Empty, generatedTarget);")
            .AppendLine($"return target.Bind(@view).Bind({values});");
        return true;
    }

    /// <summary>Preserves owner reference replacement independently of custom value equality.</summary>
    /// <param name="call">The matching runtime flavor.</param>
    /// <param name="type">The selected reference contract.</param>
    /// <returns>The typed source-selection policy.</returns>
    private static string ReferenceOptions(BindingCallPlan call, string type) =>
        $"new {call.CapabilityRoot}ValidationObservationOptions<{type}>("
        + $"{call.CapabilityRoot}ValidationMissingOwnerPolicy.DefaultValue, null, global::System.Collections.Generic.ReferenceEqualityComparer.Instance, false)";
}
