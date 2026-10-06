// Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using ReactiveUI.Validation.SourceGenerators;

internal static class SemanticInventory
{
private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat
    .WithMiscellaneousOptions(SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

private static readonly SymbolDisplayFormat SignatureFormat = TypeFormat
    .WithMemberOptions(SymbolDisplayMemberOptions.IncludeContainingType | SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeRef)
    .WithParameterOptions(SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName | SymbolDisplayParameterOptions.IncludeParamsRefOut)
    .WithGenericsOptions(SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeTypeConstraints);

internal static Dictionary<string, IMethodSymbol> Catalogue(CSharpCompilation compilation)
{
var shippingMethods = new Dictionary<string, IMethodSymbol>(StringComparer.Ordinal);
foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols.Where(static assembly => assembly.Name is "ReactiveUI.Validation" or "ReactiveUI.Validation.Reactive"))
{
    var root = assembly.Name;
    foreach (var metadataName in new[]
    {
        $"{root}.Extensions.ValidatableViewModelExtensions", $"{root}.Extensions.ValidationRuleContextExtensions",
        $"{root}.Extensions.ViewForExtensions", $"{root}.Extensions.ValidationContextBindingExtensions",
        $"{root}.Extensions.ValidationStateBindingExtensions", $"{root}.ValidationBindings.ValidationBinding",
    })
    {
        var type = assembly.GetTypeByMetadataName(metadataName) ?? throw new InvalidDataException($"Shipping method owner is missing: {metadataName}");
        foreach (var method in type.GetMembers().OfType<IMethodSymbol>().Where(ValidationCallClassifier.IsNormalMethod))
        {
            var id = method.GetDocumentationCommentId() ?? throw new InvalidDataException("A shipping CLR method has no exact documentation identity.");
            shippingMethods.Add(id, method);
        }
    }
}
if (shippingMethods.Count == 0)
{
    throw new InvalidDataException("No actual shipping normal method definitions were found.");
}
return shippingMethods;
}

internal static List<object> Dispatch(CompilerReplay replay)
{
var compilation = replay.Compilation;
var trees = replay.Trees;
var originalPaths = replay.OriginalPaths;
var dispatch = new List<object>();
foreach (var tree in trees.Where(tree => originalPaths.Contains(tree.FilePath, StringComparer.Ordinal)))
{
    var model = compilation.GetSemanticModel(tree);
    foreach (var invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
    {
        if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
        {
            continue;
        }

        IMethodSymbol normalized;
        try
        {
            normalized = ValidationCallClassifier.NormalizeMethod(method) ?? method;
        }
        catch (InvalidOperationException error)
        {
            throw new InvalidDataException($"Original consumer method normalization failed: {invocation.GetLocation().GetLineSpan()} "
                + $"{method.ToDisplayString(SignatureFormat)}; extension={method.ContainingType.IsExtension}; "
                + $"methodArity={method.Arity}; receiverArity={method.ContainingType.Arity}; invocation={invocation}", error);
        }
        var definition = normalized.OriginalDefinition;
        var name = definition.ContainingType.ContainingNamespace.ToDisplayString();
        if (!name.StartsWith("ReactiveUI.Validation", StringComparison.Ordinal)
            || definition.Name is not ("ValidationRule" or "BindValidation" or "BindValidationContext" or "BindValidationState" or "ForProperty" or "ForValidationHelperProperty" or "ForViewModel"))
        {
            continue;
        }

        var interceptor = FindInterceptor(model, invocation);
        var owner = definition.ContainingType.IsExtension ? definition.ContainingType.ContainingType.Name : definition.ContainingType.Name;
        var normalShippingCall = ValidationCallClassifier.IsNormalMethod(method);
        var runtimeSelected = normalShippingCall && ValidationRuntimeDispatchPolicy.IsOptedIn(model, invocation.SpanStart);
        var requiresGenerated = normalShippingCall && !runtimeSelected;
        if (requiresGenerated && interceptor is null)
        {
            throw new InvalidDataException($"Required normal consumer call has no final interceptor: {invocation.GetLocation().GetLineSpan()} {definition.ToDisplayString(SignatureFormat)}");
        }
        dispatch.Add(new
        {
            api = owner + "." + definition.Name,
            callable = false,
            mode = normalShippingCall ? runtimeSelected ? "RuntimeRegistered" : "Generated" : "RuntimeSafe",
            normalShippingCall,
            requiresGenerated,
            methodId = definition.GetDocumentationCommentId(),
            signature = definition.ToDisplayString(SignatureFormat),
            selectorForm = ValidationCallClassifier.SelectorForm(definition),
            returnType = TypeName(definition.ReturnType),
            arity = definition.Arity,
            inlineSelectors = AllSelectorsAreInline(model.GetOperation(invocation) as IInvocationOperation),
            parameters = definition.Parameters.Select(static parameter => new { name = parameter.Name, type = TypeName(parameter.Type), refKind = parameter.RefKind.ToString() }),
            typeParameters = definition.TypeParameters.Select(static parameter => parameter.Name),
            genericParameterContracts = GenericParameters(definition.TypeParameters),
            source = tree.FilePath,
            line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            interceptor = interceptor?.ToDisplayString(SignatureFormat),
            interceptorContainingType = interceptor?.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            interceptorTypeParameters = interceptor?.TypeParameters.Select(static parameter => parameter.Name),
        });
    }
    foreach (var node in tree.GetRoot().DescendantNodes())
    {
        if (model.GetOperation(node) is not IMethodReferenceOperation reference || reference.Syntax.Span != node.Span
            || !ValidationCallClassifier.IsNormalMethod(reference.Method))
        {
            continue;
        }

        if (!ValidationRuntimeDispatchPolicy.IsOptedIn(model, node.SpanStart))
        {
            throw new InvalidDataException($"A normal method group did not select finite runtime dispatch: {node.GetLocation().GetLineSpan()}");
        }

        var definition = (ValidationCallClassifier.NormalizeMethod(reference.Method) ?? reference.Method).OriginalDefinition;
        var owner = definition.ContainingType.IsExtension ? definition.ContainingType.ContainingType.Name : definition.ContainingType.Name;
        dispatch.Add(new
        {
            api = owner + "." + definition.Name,
            mode = "RuntimeRegistered",
            callable = true,
            normalShippingCall = true,
            requiresGenerated = false,
            methodId = definition.GetDocumentationCommentId(),
            signature = definition.ToDisplayString(SignatureFormat),
            selectorForm = ValidationCallClassifier.SelectorForm(definition),
            returnType = TypeName(definition.ReturnType),
            arity = definition.Arity,
            parameters = definition.Parameters.Select(static parameter => new { name = parameter.Name, type = TypeName(parameter.Type), refKind = parameter.RefKind.ToString() }),
            typeParameters = definition.TypeParameters.Select(static parameter => parameter.Name),
            genericParameterContracts = GenericParameters(definition.TypeParameters),
            source = tree.FilePath,
            line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            interceptor = (string?)null,
        });
    }

}

return dispatch;
}

internal static List<object> Declarations(CompilerReplay replay)
{
var compilation = replay.Compilation;
var trees = replay.Trees;
var ownPaths = replay.OwnPaths;
var generatedRoot = replay.GeneratedRoot;
var declarations = new List<object>();
foreach (var tree in trees.Where(tree => ownPaths.Contains(tree.FilePath, StringComparer.Ordinal)))
{
    var model = compilation.GetSemanticModel(tree);
    foreach (var declaration in tree.GetRoot().DescendantNodes().Where(static node =>
        node is MemberDeclarationSyntax || node is VariableDeclaratorSyntax { Parent.Parent: FieldDeclarationSyntax }))
    {
        var symbol = model.GetDeclaredSymbol(declaration);
        if (symbol is not (INamedTypeSymbol or IMethodSymbol or IPropertySymbol or IFieldSymbol or IEventSymbol))
        {
            continue;
        }

        declarations.Add(new
        {
            file = Path.GetRelativePath(generatedRoot, tree.FilePath).Replace('\\', '/'),
            kind = symbol.Kind.ToString(),
            name = symbol.Name,
            signature = symbol.ToDisplayString(SignatureFormat),
            containingType = symbol.ContainingType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            typeParameters = symbol is IMethodSymbol method ? method.TypeParameters.Select(static parameter => parameter.Name)
                : symbol is INamedTypeSymbol type ? type.TypeParameters.Select(static parameter => parameter.Name) : [],
            accessibility = symbol.DeclaredAccessibility.ToString(),
            isStatic = symbol.IsStatic,
            refKind = symbol is IMethodSymbol methodRef ? methodRef.RefKind.ToString()
                : symbol is IPropertySymbol propertyRef ? propertyRef.RefKind.ToString() : null,
            getter = symbol is IPropertySymbol propertyGetter ? propertyGetter.GetMethod?.DeclaredAccessibility.ToString() : null,
            setter = symbol is IPropertySymbol propertySetter ? propertySetter.SetMethod?.DeclaredAccessibility.ToString() : null,
            initOnly = symbol is IPropertySymbol propertyInit && propertyInit.SetMethod?.IsInitOnly is true,
            readOnly = symbol is IFieldSymbol field && field.IsReadOnly,
            constant = symbol is IFieldSymbol constantField && constantField.IsConst,
            constantValue = symbol is IFieldSymbol literalField && literalField.IsConst ? literalField.ConstantValue : null,
            parameters = symbol is IMethodSymbol declaredMethod ? declaredMethod.Parameters.Select(static parameter => new
            {
                name = parameter.Name,
                type = TypeName(parameter.Type),
                nullableAnnotation = parameter.NullableAnnotation.ToString(),
                refKind = parameter.RefKind.ToString(),
                scopedKind = parameter.ScopedKind.ToString(),
                attributes = AttributeContracts(parameter.GetAttributes()),
            }) : null,
            explicitInterfaces = symbol switch
            {
                IMethodSymbol explicitMethod => explicitMethod.ExplicitInterfaceImplementations.Select(Signature),
                IPropertySymbol explicitProperty => explicitProperty.ExplicitInterfaceImplementations.Select(Signature),
                IEventSymbol explicitEvent => explicitEvent.ExplicitInterfaceImplementations.Select(Signature),
                _ => [],
            },
            nullableAnnotation = symbol switch
            {
                IMethodSymbol nullableMethod => nullableMethod.ReturnNullableAnnotation.ToString(),
                IPropertySymbol property => property.NullableAnnotation.ToString(),
                IFieldSymbol memberField => memberField.NullableAnnotation.ToString(),
                IEventSymbol memberEvent => memberEvent.NullableAnnotation.ToString(),
                _ => null,
            },
            genericParameters = GenericParameters(symbol is IMethodSymbol genericMethod ? genericMethod.TypeParameters
                : symbol is INamedTypeSymbol genericType ? genericType.TypeParameters : []),
            attributes = symbol.GetAttributes().Select(static attribute => attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)),
            attributeContracts = AttributeContracts(symbol.GetAttributes()),
            returnAttributes = symbol is IMethodSymbol returnMethod ? AttributeContracts(returnMethod.GetReturnTypeAttributes()) : [],
            returnType = symbol is IMethodSymbol resultMethod ? TypeName(resultMethod.ReturnType) : null,
        });
    }
}

return declarations;
}

internal static string TypeName(ITypeSymbol type) => type.ToDisplayString(TypeFormat);

private static bool AllSelectorsAreInline(IInvocationOperation? invocation)
{
    if (invocation is null)
    {
        return false;
    }

    var selectors = invocation.Arguments.Where(static argument => argument.Parameter is { } parameter
        && ValidationCallClassifier.IsSelectorParameter(parameter)).ToArray();
    return selectors.Length != 0 && selectors.All(static argument => argument.Syntax is ArgumentSyntax syntax
        && IsInlineLambda(syntax.Expression));
}

private static bool IsInlineLambda(ExpressionSyntax expression)
{
    while (expression is ParenthesizedExpressionSyntax parentheses)
    {
        expression = parentheses.Expression;
    }

    return expression is LambdaExpressionSyntax or AnonymousMethodExpressionSyntax;
}

internal static IEnumerable<object> AttributeContracts(IEnumerable<AttributeData> attributes) => attributes.Select(static attribute => (object)new
{
    type = attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
    assembly = attribute.AttributeClass?.ContainingAssembly.Identity.ToString(),
    constructorArguments = attribute.ConstructorArguments.Select(static argument => new
    {
        type = argument.Type?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
        value = AttributeValue(argument),
    }),
    namedArguments = attribute.NamedArguments.Select(static argument => new
    {
        name = argument.Key,
        type = argument.Value.Type?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
        value = AttributeValue(argument.Value),
    }),
});

internal static object? AttributeValue(TypedConstant argument) => argument.Kind switch
{
    TypedConstantKind.Array => argument.IsNull ? null : argument.Values.Select(AttributeValue).ToArray(),
    TypedConstantKind.Type => argument.Value is ITypeSymbol type ? TypeName(type) : null,
    _ => argument.Value?.ToString(),
};

internal static IEnumerable<object> GenericParameters(IEnumerable<ITypeParameterSymbol> parameters) => parameters.Select(static parameter => (object)new
{
    name = parameter.Name,
    variance = parameter.Variance.ToString(),
    referenceConstraint = parameter.HasReferenceTypeConstraint,
    referenceConstraintNullable = parameter.ReferenceTypeConstraintNullableAnnotation.ToString(),
    valueConstraint = parameter.HasValueTypeConstraint,
    unmanagedConstraint = parameter.HasUnmanagedTypeConstraint,
    notNullConstraint = parameter.HasNotNullConstraint,
    constructorConstraint = parameter.HasConstructorConstraint,
    allowsRefLike = parameter.AllowsRefLikeType,
    constraintTypes = parameter.ConstraintTypes.Select(TypeName),
});

internal static string Signature(ISymbol symbol) => symbol.ToDisplayString(SignatureFormat);

[SuppressMessage("Usage", "RSEXPERIMENTAL002", Justification = "The pinned Roslyn5.9 exposes final actual dispatch through this API; semantic replay never runs a consumer or ships Roslyn with it.")]
static IMethodSymbol? FindInterceptor(SemanticModel model, InvocationExpressionSyntax invocation) => model.GetInterceptorMethod(invocation);
}
