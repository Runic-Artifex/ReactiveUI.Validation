// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
namespace ReactiveUI.Validation.SourceGenerators;

/// <summary>Plans typed operations, dependencies and finite stored-selector provenance.</summary>
[SuppressMessage("Performance", "PSH1100", Justification = "This is compiler-time planning and source formatting over bounded immutable operations, not emitted subscription execution.")]
internal static class SemanticSelectorPlanner
{
    /// <summary>Distance from the leaf to its owning segment.</summary>
    private const int ParentOffset = 2;

    /// <summary>Recovers proven finite selector source without building or evaluating its dependency graph.</summary>
    /// <param name="model">The selecting semantic model.</param>
    /// <param name="expression">The original selector argument.</param>
    /// <param name="resolvedModel">The defining or constructed semantic model.</param>
    /// <param name="lambda">The exact proven lambda source.</param>
    /// <param name="reason">The required typed capability route on failure.</param>
    /// <returns>Whether immutable selector provenance is proven.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryResolveLambda(SemanticModel model, ExpressionSyntax expression, out SemanticModel resolvedModel, out LambdaExpressionSyntax lambda, out string reason) =>
        TryLambda(model, expression, new(SymbolEqualityComparer.Default), out resolvedModel, out lambda, out reason);

    /// <summary>Reads a selector without discarding conversions or changing member dispatch.</summary>
    /// <param name="site">The original call site.</param>
    /// <param name="expression">The expression or finite stored selector.</param>
    /// <param name="selector">The semantic plan.</param>
    /// <param name="reason">The explicit typed alternative required on failure.</param>
    /// <param name="requireNotification">Whether ordinary observable lowering is required.</param>
    /// <param name="metadataOnly">Whether selected value operations are never emitted.</param>
    /// <returns>Whether the automatic plan is sound.</returns>
    [SuppressMessage("Style", "SST1442", Justification = "The semantic shape checks independently validate lambda, root, metadata and storage contracts.")]
    internal static bool TryCreate(CallSite site, ExpressionSyntax expression, out Selector? selector, out string reason, bool requireNotification, bool metadataOnly = false)
    {
        selector = null;
        if (!TryLambda(site.Model, expression, new(SymbolEqualityComparer.Default), out var model, out var lambda, out reason))
        {
            return false;
        }

        var parameterSyntax = lambda switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Parameter,
            ParenthesizedLambdaExpressionSyntax parenthesized when parenthesized.ParameterList.Parameters.Count == 1 => parenthesized.ParameterList.Parameters[0],
            _ => null,
        };
        if (parameterSyntax is null || lambda.Body is not ExpressionSyntax body
            || model.GetDeclaredSymbol(parameterSyntax) is not IParameterSymbol parameter
            || ReadOperation(model, body) is not { } operation)
        {
            reason = "Use a single-parameter expression lambda, or a supplied typed ValidationSelector factory for statement bodies.";
            return false;
        }

        if (!CheckRootTransport(site, expression, model, parameter, out reason))
        {
            return false;
        }

        var graph = new DependencyWalker(site, model, parameter, requireNotification, metadataOnly, HasRootVariance(site, expression, parameter));
        graph.Visit(operation);
        graph.BindOwners();
        if (graph.Failure is not null)
        {
            reason = graph.Failure;
            return false;
        }

        var properties = SimpleProperties(operation, parameter);
        var paths = graph.Paths.Where(path => !graph.Paths.Exists(other => other.Length > path.Length
            && (other.StartsWith($"{path}.", StringComparison.Ordinal) || other.StartsWith($"{path}[", StringComparison.Ordinal))))
            .ToImmutableArray();
        selector = new(properties, Rewrite(model, body, parameter), graph.Guards.ToImmutableArray(), graph.Dependencies.ToImmutableArray(), paths, null);
        var pathPlans = graph.PathPlans.Where(plan => paths.Contains(plan.DisplayPath)).ToImmutableArray();
        var contractType = SelectorReturnType(site.Model.GetTypeInfo(expression).ConvertedType)
            ?? SelectorReturnType(model.GetTypeInfo(lambda).ConvertedType) ?? operation.Type!;
        selector.SetSemanticRead(
            new(model, parameter, operation, contractType, graph.ReadBridges),
            contractType,
            graph.RequiresLexicalAccess || graph.Dependencies.Exists(dependency => (!metadataOnly || dependency.IsMetadata)
            && !graph.ReadBridges.ContainsKey(dependency.Member) && (!model.Compilation.IsSymbolAccessibleWithin(
            dependency.Member is IPropertySymbol property ? property.GetMethod! : dependency.Member,
            model.Compilation.Assembly) || !GeneratorHelpers.IsAccessibleType(model.Compilation, dependency.Member.ContainingType))),
            pathPlans);
        if (!CheckMetadata(selector, metadataOnly, out reason))
        {
            selector = null;
            return false;
        }

        site.RequiresLexicalAccess |= selector.RequiresLexicalAccess;
        reason = string.Empty;
        return true;
    }

    /// <summary>Reads the actual storage lens and proves every outward struct assignment.</summary>
    /// <param name="site">The source call.</param>
    /// <param name="expression">The target selector.</param>
    /// <param name="access">The typed writable plan.</param>
    /// <param name="reason">The supplied typed replacement alternative.</param>
    /// <returns>Whether ordinary assignment is legal.</returns>
    internal static bool TryAccess(CallSite site, ExpressionSyntax expression, out AccessPlan? access, out string reason)
    {
        access = null;
        if (!TryLambda(site.Model, expression, new(SymbolEqualityComparer.Default), out var model, out var lambda, out reason))
        {
            return false;
        }

        var syntax = lambda switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Parameter,
            ParenthesizedLambdaExpressionSyntax parenthesized when parenthesized.ParameterList.Parameters.Count == 1 => parenthesized.ParameterList.Parameters[0],
            _ => null,
        };
        if (syntax is null || lambda.Body is not ExpressionSyntax body || model.GetDeclaredSymbol(syntax) is not IParameterSymbol parameter || ReadOperation(model, body) is not { } operation)
        {
            reason = "Supply a typed ValidationTarget factory with an ordinary setter or immutable replacement callback.";
            return false;
        }

        return CheckRootTransport(site, expression, model, parameter, out reason)
            && TryAccess(site, model, parameter, operation, out access, out reason);
    }

    /// <summary>Formats an expression by semantic symbol identity rather than identifier spelling.</summary>
    /// <param name="model">The expression's semantic model.</param>
    /// <param name="syntax">The expression syntax.</param>
    /// <param name="parameter">The selected lambda parameter.</param>
    /// <param name="replacement">An optional owner operation to replace.</param>
    /// <param name="readBridges">Exact statically proven getter invocations.</param>
    /// <param name="retainRootType">Whether executable formatting preserves the original reference parameter type.</param>
    /// <returns>Fully qualified typed C# syntax.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string Rewrite(
        SemanticModel model,
        SyntaxNode syntax,
        IParameterSymbol parameter,
        SyntaxNode? replacement = null,
        IReadOnlyDictionary<ISymbol, string>? readBridges = null,
        bool retainRootType = true) =>
        new ExpressionRewriter(model, parameter, replacement, null, readBridges, retainRootType: retainRootType).Visit(syntax)!.NormalizeWhitespace().ToFullString();

    /// <summary>Rebinds evaluated child operations by original source identity.</summary>
    /// <param name="model">The original semantic model.</param>
    /// <param name="syntax">The original expression.</param>
    /// <param name="parameter">The selected parameter.</param>
    /// <param name="replacements">Exact source child nodes and their evaluated expressions.</param>
    /// <param name="readBridges">Exact statically proven getter invocations.</param>
    /// <param name="operandValues">Already acquired index operands keyed by semantic operation identity.</param>
    /// <returns>The original typed expression with bound children.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string RewriteBound(
        SemanticModel model,
        SyntaxNode syntax,
        IParameterSymbol parameter,
        IReadOnlyDictionary<SyntaxNode, string> replacements,
        IReadOnlyDictionary<ISymbol, string>? readBridges = null,
        IReadOnlyDictionary<IOperation, string>? operandValues = null) =>
        new ExpressionRewriter(model, parameter, null, replacements, readBridges, operandValues).Visit(syntax)!.NormalizeWhitespace().ToFullString();

    /// <summary>Preserves the exact enum and literal contract of explicit or compiler-supplied constant operands.</summary>
    /// <param name="type">The original constant type, or null for a null literal.</param>
    /// <param name="value">The compiler constant value.</param>
    /// <returns>The typed C# constant expression.</returns>
    internal static string FormatConstant(ITypeSymbol? type, object? value)
    {
        var literal = SymbolDisplay.FormatPrimitive(value!, true, false) ?? "null";
        return HasEnumContract(type) ? $"(({GeneratorHelpers.TypeName(type!)})({literal}))" : literal;
    }

    /// <summary>Identifies enum contracts whose compiler-supplied primitive constants require typed casts.</summary>
    /// <param name="type">The original argument or conversion target type.</param>
    /// <returns>Whether the target is an enum or a nullable enum.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool HasEnumContract(ITypeSymbol? type) => type is { TypeKind: TypeKind.Enum }
        || (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
        && nullable.TypeArguments[0].TypeKind == TypeKind.Enum);

    /// <summary>Uses the actual parameter contract for compiler-supplied optional defaults.</summary>
    /// <param name="operation">The original index operand operation.</param>
    /// <returns>The declared optional input type or the ordinary operand type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ITypeSymbol OperandType(IOperation operation) =>
        operation.Parent is IArgumentOperation { ArgumentKind: ArgumentKind.DefaultValue, Parameter: { } parameter }
            ? parameter.Type
            : operation.Type!;

    /// <summary>Preserves the original named reference parameter's dispatch across delegate contravariance.</summary>
    /// <param name="model">The original lambda semantic model.</param>
    /// <param name="parameter">The lambda's declared source parameter.</param>
    /// <param name="allowMissing">Whether the expression acquires an unproven nullable root before its owner guard.</param>
    /// <returns>The statically typed original root, or opaque transport for exact inaccessible-member bridges.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string RootExpression(SemanticModel model, IParameterSymbol parameter, bool allowMissing = false) =>
        parameter.Type is INamedTypeSymbol { IsReferenceType: true } type && RootTypeNameable(model.Compilation, type)
            ? $"(({RootCastType(type, allowMissing)}){Selector.RootMarker})"
            : Selector.RootMarker;

    /// <summary>Formats the original root's declared type at its presence-proof boundary.</summary>
    /// <param name="type">The original named reference root.</param>
    /// <param name="allowMissing">Whether acquisition precedes the owner-presence guard.</param>
    /// <returns>The exact root type with its current nullable flow contract.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string RootCastType(ITypeSymbol type, bool allowMissing) => GeneratorHelpers.TypeName(type.WithNullableAnnotation(
        allowMissing ? NullableAnnotation.Annotated : NullableAnnotation.NotAnnotated));

    /// <summary>Allows legal lexical type parameters while rejecting unnameable constructed root arguments.</summary>
    /// <param name="compilation">The original caller compilation.</param>
    /// <param name="type">The original declared root or one of its constructed arguments.</param>
    /// <returns>Whether the type can be spelled in its generated legal host.</returns>
    private static bool RootTypeNameable(Compilation compilation, ITypeSymbol type) => type switch
    {
        ITypeParameterSymbol => true,
        IArrayTypeSymbol array => RootTypeNameable(compilation, array.ElementType),
        INamedTypeSymbol named => !named.IsAnonymousType && !named.IsFileLocal
            && compilation.IsSymbolAccessibleWithin(named.OriginalDefinition, compilation.Assembly)
            && (named.ContainingType is null || RootTypeNameable(compilation, named.ContainingType))
            && named.TypeArguments.All(argument => RootTypeNameable(compilation, argument)),
        _ => GeneratorHelpers.IsAccessibleType(compilation, type),
    };

    /// <summary>Formats an exact operand instead of treating an implicit default's parent syntax as its value.</summary>
    /// <param name="model">The defining semantic model.</param>
    /// <param name="operation">The original operand operation.</param>
    /// <param name="parameter">The selected lambda parameter.</param>
    /// <param name="retainRootType">Whether this is executable syntax rather than root-relative display metadata.</param>
    /// <returns>The root-parameterized typed operand.</returns>
    private static string OperandExpression(SemanticModel model, IOperation operation, IParameterSymbol parameter, bool retainRootType = true) => operation switch
    {
        ILiteralOperation literal => FormatConstant(OperandType(literal), literal.ConstantValue.Value),
        IDefaultValueOperation defaultValue => $"default({GeneratorHelpers.TypeName(OperandType(defaultValue))})",
        IParenthesizedOperation parentheses => OperandExpression(model, parentheses.Operand, parameter, retainRootType),
        IConversionOperation conversion => ConvertedOperand(model, conversion, parameter, retainRootType),
        IArrayCreationOperation { IsImplicit: true, Type: IArrayTypeSymbol array } creation => ImplicitArrayOperand(model, creation, array, parameter, retainRootType),
        _ => Rewrite(model, operation.Syntax, parameter, retainRootType: retainRootType),
    };

    /// <summary>Preserves compiler-supplied params arrays without using their parent access syntax.</summary>
    /// <param name="model">The defining semantic model.</param>
    /// <param name="operation">The original array operation.</param>
    /// <param name="array">The exact array contract.</param>
    /// <param name="parameter">The original lambda parameter.</param>
    /// <param name="retainRootType">Whether this operand is executable typed syntax.</param>
    /// <returns>The typed array of original operand expressions.</returns>
    private static string ImplicitArrayOperand(SemanticModel model, IArrayCreationOperation operation, IArrayTypeSymbol array, IParameterSymbol parameter, bool retainRootType)
    {
        var values = new List<string>();
        if (operation.Initializer is not null)
        {
            foreach (var value in operation.Initializer.ElementValues)
            {
                values.Add(OperandExpression(model, value, parameter, retainRootType));
            }
        }

        return $"new {GeneratorHelpers.TypeName(array.ElementType)}[] {{ {string.Join(", ", values)} }}";
    }

    /// <summary>Acquires a converted key before its getter and setter reuse, including implicit user conversion.</summary>
    /// <param name="model">The defining semantic model.</param>
    /// <param name="conversion">The original converted index operand.</param>
    /// <param name="parameter">The original lambda parameter.</param>
    /// <param name="retainRootType">Whether this operand is executable typed syntax.</param>
    /// <returns>The exact typed key conversion.</returns>
    private static string ConvertedOperand(SemanticModel model, IConversionOperation conversion, IParameterSymbol parameter, bool retainRootType)
    {
        var value = OperandExpression(model, conversion.Operand, parameter, retainRootType);
        var converted = conversion.IsTryCast
            ? $"({value}) as {GeneratorHelpers.TypeName(conversion.Type!)}"
            : $"({GeneratorHelpers.TypeName(conversion.Type!)})({value})";
        return conversion.IsChecked ? $"checked({converted})" : converted;
    }

    /// <summary>Checks an independent property's metadata contract without dropping ambiguous paths.</summary>
    /// <param name="selector">The complete operation graph.</param>
    /// <param name="metadataOnly">Whether value reads must never execute.</param>
    /// <param name="reason">The actual typed callback alternative.</param>
    /// <returns>Whether automatic metadata is valid.</returns>
    private static bool CheckMetadata(Selector selector, bool metadataOnly, out string reason)
    {
        reason = string.Empty;
        if (metadataOnly && !selector.SupportsIndependentMetadata)
        {
            reason = "Branch metadata requiring selected leaf values needs an authored ValidationSelector readPaths callback; "
                + "normal metadata never executes selected leaf getters.";
            return false;
        }

        if (metadataOnly && !selector.HasSingleMetadataPath)
        {
            reason = "A property binding must identify exactly one current path. Supply an authored typed metadata selector for multi-property computations.";
            return false;
        }

        return true;
    }

    /// <summary>Reads the selected callable contract, including its legal covariant return conversion.</summary>
    /// <param name="contract">The converted expression or delegate contract.</param>
    /// <returns>The declared callable return type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ITypeSymbol? SelectorReturnType(ITypeSymbol? contract) => SelectorCallable(contract)?.DelegateInvokeMethod?.ReturnType;

    /// <summary>Reads the delegate contract carried directly or inside an expression tree.</summary>
    /// <param name="contract">The actual selected argument contract.</param>
    /// <returns>The delegate contract, when present.</returns>
    private static INamedTypeSymbol? SelectorCallable(ITypeSymbol? contract)
    {
        var type = contract as INamedTypeSymbol;
        if (type?.TypeKind != TypeKind.Delegate && type?.TypeArguments.Length == 1)
        {
            type = type.TypeArguments[0] as INamedTypeSymbol;
        }

        return type;
    }

    /// <summary>Prevents opaque contravariant roots from rebinding a different member slot.</summary>
    /// <param name="site">The selected normal API call.</param>
    /// <param name="expression">The original selector argument.</param>
    /// <param name="model">The original defining semantic model.</param>
    /// <param name="parameter">The recovered delegate's declared source parameter.</param>
    /// <param name="reason">The explicit legal-context descriptor alternative.</param>
    /// <returns>Whether the exact original root can be transported automatically.</returns>
    private static bool CheckRootTransport(CallSite site, ExpressionSyntax expression, SemanticModel model, IParameterSymbol parameter, out string reason)
    {
        if (parameter.Type is INamedTypeSymbol { IsReferenceType: true } && !RootTypeNameable(model.Compilation, parameter.Type)
            && HasRootVariance(site, expression, parameter))
        {
            reason = "This contravariant selector's original root contains inaccessible or file-local types. "
                + "Supply an inferred typed ValidationSelector/ValidationTarget factory in the legal caller context to retain its exact member dispatch.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>Identifies a legal input variance conversion without inferring dispatch from the runtime receiver.</summary>
    /// <param name="site">The selected normal API call.</param>
    /// <param name="expression">The original selector argument.</param>
    /// <param name="parameter">The recovered delegate's declared source parameter.</param>
    /// <returns>Whether the selected argument receives a different static source type.</returns>
    private static bool HasRootVariance(CallSite site, ExpressionSyntax expression, IParameterSymbol parameter)
    {
        var selected = SelectorCallable(site.Model.GetTypeInfo(expression).ConvertedType)?.DelegateInvokeMethod?.Parameters[0].Type;
        return selected is not null && !SymbolEqualityComparer.Default.Equals(selected, parameter.Type);
    }

    /// <summary>Retains the legacy direct-property chain independently from richer operation metadata.</summary>
    /// <param name="operation">The complete selected value operation.</param>
    /// <param name="parameter">The original declared source parameter.</param>
    /// <returns>The simple property chain, or an empty array for richer computations.</returns>
    private static ImmutableArray<IPropertySymbol> SimpleProperties(IOperation operation, IParameterSymbol parameter)
    {
        var members = new List<IOperation>();
        return TryMemberChain(operation, parameter, members) && members.TrueForAll(static member => member is IPropertyReferenceOperation { Property.IsIndexer: false })
            ? members.Select(static member => ((IPropertyReferenceOperation)member).Property).ToImmutableArray()
            : ImmutableArray<IPropertySymbol>.Empty;
    }

    /// <summary>Reads an operation while retaining checked arithmetic on the underlying operation.</summary>
    /// <param name="model">The defining semantic model.</param>
    /// <param name="expression">The selector body.</param>
    /// <returns>The exact typed operation.</returns>
    private static IOperation? ReadOperation(SemanticModel model, ExpressionSyntax expression) => model.GetOperation(expression)
        ?? (expression is CheckedExpressionSyntax checkedExpression ? model.GetOperation(checkedExpression.Expression) : null);

    /// <summary>Proves the actual writable path rooted in stable reference storage.</summary>
    /// <param name="site">The source site.</param>
    /// <param name="model">The selector semantic model.</param>
    /// <param name="parameter">The root parameter.</param>
    /// <param name="operation">The leaf operation.</param>
    /// <param name="access">The completed lens.</param>
    /// <param name="reason">The failure alternative.</param>
    /// <returns>Whether the lens is legal.</returns>
    [SuppressMessage("Style", "SST1442", Justification = "Each storage kind and receiver conversion requires a distinct legal access proof.")]
    [SuppressMessage("Style", "SST1523", Justification = "Storage access and all outward write-back proofs share one ordered semantic chain.")]
    [SuppressMessage("Style", "SST1443", Justification = "The chain walk checks per-segment storage access, owner conversion and index argument identity together.")]
    private static bool TryAccess(CallSite site, SemanticModel model, IParameterSymbol parameter, IOperation operation, out AccessPlan? access, out string reason)
    {
        access = null;
        if (!parameter.Type.IsReferenceType)
        {
            reason = "A by-value struct root is an unowned copy. Supply ValidationCell storage and a typed ValidationLens write-back factory.";
            return false;
        }
        while (operation is IConversionOperation { OperatorMethod: null } conversion
            && (conversion.IsImplicit || conversion.Conversion.IsIdentity || conversion.Conversion.IsReference))
        {
            operation = conversion.Operand;
        }

        var chain = new List<IOperation>();
        if (operation is IConversionOperation || !TryMemberChain(operation, parameter, chain) || chain.Count == 0)
        {
            reason = "The target must be actual property, field or indexer storage. Supply a typed ValidationTarget for explicit inverse conversion or replacement.";
            return false;
        }

        var steps = ImmutableArray.CreateBuilder<AccessStep>();
        for (var index = 0; index < chain.Count; index++)
        {
            var member = chain[index];
            var symbol = Member(member)!;
            var receiver = Instance(member)!;
            var storage = member.Type!;
            var needsWrite = index == chain.Count - 1 || (storage.IsValueType && chain.Skip(index + 1).Take(chain.Count - index - ParentOffset).All(static owner => owner.Type!.IsValueType));
            var ordinaryWrite = Writable(site, symbol, receiver.Type!);
            var requiresContractBridge = needsWrite && AccessBridgeEmitter.RequiresWriteContractBridge(symbol);
            var writeAccessor = symbol is IPropertySymbol selectedProperty ? selectedProperty.SetMethod : symbol;
            var readAccessor = symbol is IPropertySymbol readableProperty ? readableProperty.GetMethod : symbol;
            string? writeBridge = null;
            string? readBridge = null;
            if (needsWrite && (requiresContractBridge || !ordinaryWrite
                || (writeAccessor is not null && !site.Model.Compilation.IsSymbolAccessibleWithin(writeAccessor, site.Model.Compilation.Assembly))))
            {
                if (AccessBridgeEmitter.TryEmit(site, symbol, true, out var bridge, out var bridgeReason, receiver.Type))
                {
                    writeBridge = bridge;
                }
                else if (!ordinaryWrite || requiresContractBridge)
                {
                    reason = bridgeReason;
                    return false;
                }
                else
                {
                    site.RequiresLexicalAccess = true;
                }
            }

            if (index < chain.Count - 1 && readAccessor is not null && !site.Model.Compilation.IsSymbolAccessibleWithin(readAccessor, site.Model.Compilation.Assembly))
            {
                if (AccessBridgeEmitter.TryEmit(site, symbol, false, out var bridge, out _, receiver.Type, retainLexicalReadCast: false))
                {
                    readBridge = bridge;
                }
                else
                {
                    site.RequiresLexicalAccess = true;
                }
            }

            var replaced = StripConversions(receiver);
            if (receiver is IConversionOperation && receiver.Type!.IsValueType)
            {
                reason = "Unboxing or converting a value owner creates temporary storage. Supply a typed ValidationCell/ValidationLens factory for owned write-back.";
                return false;
            }

            var replacements = new Dictionary<SyntaxNode, string> { [replaced.Syntax] = AccessStep.OwnerMarker };
            var operands = new Dictionary<IOperation, string>();
            var indices = ImmutableArray.CreateBuilder<string>();
            if (member is IPropertyReferenceOperation indexed)
            {
                for (var argument = 0; argument < indexed.Arguments.Length; argument++)
                {
                    var value = indexed.Arguments[argument].Value;
                    if (value.Syntax != member.Syntax)
                    {
                        replacements[value.Syntax] = AccessStep.IndexMarker(index, argument);
                    }

                    indices.Add(OperandExpression(model, value, parameter));
                    operands[value] = AccessStep.IndexMarker(index, argument);
                    var marker = AccessBridgeEmitter.IndexPlaceholder(indexed.Arguments[argument].Parameter!.Ordinal);
                    writeBridge = writeBridge is null ? null : Selector.ReplaceIdentifier(writeBridge, marker, AccessStep.IndexMarker(index, argument));
                    readBridge = readBridge is null ? null : Selector.ReplaceIdentifier(readBridge, marker, AccessStep.IndexMarker(index, argument));
                }
            }

            steps.Add(new(symbol, receiver.Type!, storage, RewriteBound(model, member.Syntax, parameter, replacements, operandValues: operands), indices.ToImmutable())
            {
                ReadBridge = readBridge,
                WriteBridge = writeBridge,
                OwnerOperation = receiver,
                IndexOperations = member is IPropertyReferenceOperation keys ? keys.Arguments.Select(static argument => argument.Value).ToImmutableArray() : [],
            });
        }

        access = new(operation.Type!, steps.ToImmutable(), RootExpression(model, parameter, allowMissing: true));
        reason = string.Empty;
        return true;
    }

    /// <summary>Checks ordinary writable storage in the caller's legal lexical context.</summary>
    /// <param name="site">The original site.</param>
    /// <param name="member">The selected storage member.</param>
    /// <param name="throughType">The actual receiver type, including protected access restrictions.</param>
    /// <returns>Whether ordinary assignment is legal.</returns>
    private static bool Writable(CallSite site, ISymbol member, ITypeSymbol throughType)
    {
        var within = site.Model.GetEnclosingSymbol(site.Invocation.SpanStart)?.ContainingType ?? (ISymbol)site.Model.Compilation.Assembly;
        return member switch
        {
            IPropertySymbol { SetMethod: { IsInitOnly: false } setter } => site.Model.Compilation.IsSymbolAccessibleWithin(setter, within, throughType),
            IFieldSymbol { IsConst: false, IsReadOnly: false } field => site.Model.Compilation.IsSymbolAccessibleWithin(field, within, throughType),
            _ => false,
        };
    }

    /// <summary>Recovers a finite immutable selector definition, never caching runtime closures.</summary>
    /// <param name="model">The current semantic model.</param>
    /// <param name="expression">The selector expression.</param>
    /// <param name="seen">The bounded cycle guard.</param>
    /// <param name="resolvedModel">The defining semantic model.</param>
    /// <param name="lambda">The finite lambda definition.</param>
    /// <param name="reason">The required descriptor route.</param>
    /// <returns>Whether immutable provenance is proven.</returns>
    private static bool TryLambda(SemanticModel model, ExpressionSyntax expression, HashSet<ISymbol> seen, out SemanticModel resolvedModel, out LambdaExpressionSyntax lambda, out string reason)
    {
        expression = UnwrapLambdaCast(model, expression);
        resolvedModel = model;
        lambda = null!;
        reason = string.Empty;
        if (expression is LambdaExpressionSyntax literal)
        {
            lambda = literal;
            return true;
        }

        var symbol = model.GetSymbolInfo(expression).Symbol;
        if (symbol is null || seen.Count >= 32 || !seen.Add(symbol))
        {
            reason = "Runtime selection needs an explicitly registered typed selector catalog or supplied ValidationSelector factory.";
            return false;
        }

        if (HasPolymorphicDispatch(symbol))
        {
            reason = "Polymorphic static selector dispatch requires a supplied typed ValidationSelector factory or explicit scoped catalog registration.";
            return false;
        }

        if (IdentityArgument(model, expression, symbol) is { } argument)
        {
            return TryLambda(model, argument, seen, out resolvedModel, out lambda, out reason);
        }

        var definition = FiniteDefinition(model, expression, symbol);
        if (definition is null)
        {
            reason = "Mutable selector variables, parameters, factories and live captures require a typed ValidationSelector factory or explicit scoped catalog registration.";
            return false;
        }

        return SemanticSelectorInstantiation.TryInstantiate(model, symbol, definition, out resolvedModel, out var instantiated, out reason)
            && TryLambda(resolvedModel, instantiated, seen, out resolvedModel, out lambda, out reason);
    }

    /// <summary>Recovers a source-proven expression identity forwarder without executing or interpreting its body.</summary>
    /// <param name="model">The selecting semantic model.</param>
    /// <param name="expression">The actual invocation argument.</param>
    /// <param name="symbol">The selected factory method.</param>
    /// <returns>The exact current caller argument when the source body returns only that parameter.</returns>
    private static ExpressionSyntax? IdentityArgument(SemanticModel model, ExpressionSyntax expression, ISymbol symbol)
    {
        if (symbol is not IMethodSymbol { IsStatic: true } method || expression is not InvocationExpressionSyntax invocation
            || method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax { ExpressionBody.Expression: { } body })
        {
            return null;
        }

        var definingModel = model.Compilation.GetSemanticModel(body.SyntaxTree);
        return definingModel.GetOperation(body) is IParameterReferenceOperation returned && returned.Parameter.RefKind == RefKind.None
            && model.GetOperation(invocation) is IInvocationOperation call
            ? call.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == returned.Parameter.Ordinal)?.Value.Syntax as ExpressionSyntax
            : null;
    }

    /// <summary>Reads a source definition only when its storage and declaration fix the final selector.</summary>
    /// <param name="model">The selecting semantic model.</param>
    /// <param name="expression">The original argument expression.</param>
    /// <param name="symbol">The selected storage or factory member.</param>
    /// <returns>The fixed expression body, or null for runtime-selected storage.</returns>
    private static ExpressionSyntax? FiniteDefinition(SemanticModel model, ExpressionSyntax expression, ISymbol symbol)
    {
        var declaration = symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
        switch (symbol)
        {
            case ILocalSymbol when declaration is VariableDeclaratorSyntax { Initializer.Value: { } initial } variable:
                {
                    return ImmutableLocalDefinition(model, variable, symbol, initial);
                }

            case IFieldSymbol { IsReadOnly: true, IsStatic: true } field when declaration is VariableDeclaratorSyntax { Initializer.Value: { } initial }
                && !HasStaticInitializationWrites(model.Compilation, field):
                {
                    return initial;
                }

            case IPropertySymbol { IsStatic: true, SetMethod: null } when declaration is PropertyDeclarationSyntax property:
                {
                    return property.ExpressionBody?.Expression ?? SingleReturn(property.AccessorList?.Accessors.SingleOrDefault(
                        static accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration)));
                }

            case IMethodSymbol { IsStatic: true, Parameters.Length: 0 } when expression is InvocationExpressionSyntax
                && declaration is MethodDeclarationSyntax method:
                {
                    return method.ExpressionBody?.Expression ?? SingleReturn(method.Body);
                }
        }

        return null;
    }

    /// <summary>Proves that a local retains the exact selected initializer throughout its scope.</summary>
    /// <param name="model">The original selecting model.</param>
    /// <param name="variable">The selector storage declaration.</param>
    /// <param name="symbol">The exact local storage identity.</param>
    /// <param name="initial">The declared selector initializer.</param>
    /// <returns>The initializer when no writes or borrowed escapes can replace it.</returns>
    private static ExpressionSyntax? ImmutableLocalDefinition(SemanticModel model, VariableDeclaratorSyntax variable, ISymbol symbol, ExpressionSyntax initial) =>
        variable.FirstAncestorOrSelf<BlockSyntax>() is { } scope && !HasWrites(model, scope, symbol) ? initial : null;

    /// <summary>Recovers a proven single-return factory or getter without interpreting control flow.</summary>
    /// <param name="declaration">The exact getter or block declaration.</param>
    /// <returns>The returned expression when no other statements can change its result.</returns>
    private static ExpressionSyntax? SingleReturn(SyntaxNode? declaration) => declaration switch
    {
        AccessorDeclarationSyntax accessor => accessor.ExpressionBody?.Expression ?? SingleReturn(accessor.Body),
        BlockSyntax { Statements.Count: 1 } block when block.Statements[0] is ReturnStatementSyntax statement => statement.Expression,
        _ => null,
    };

    /// <summary>Rejects a static interface contract whose runtime implementation can replace its source body.</summary>
    /// <param name="symbol">The selected factory member.</param>
    /// <returns>Whether the source declaration does not fix actual dispatch.</returns>
    private static bool HasPolymorphicDispatch(ISymbol symbol) => symbol switch
    {
        IPropertySymbol { IsStatic: true, IsSealed: false } property => property.IsVirtual || property.IsAbstract,
        IMethodSymbol { IsStatic: true, IsSealed: false } method => method.IsVirtual || method.IsAbstract,
        _ => false,
    };

    /// <summary>Checks every source static constructor before trusting a readonly field initializer.</summary>
    /// <param name="compilation">The selector's compilation.</param>
    /// <param name="field">The selected readonly field.</param>
    /// <returns>Whether initialization can replace or expose the declared selector.</returns>
    private static bool HasStaticInitializationWrites(Compilation compilation, IFieldSymbol field)
    {
        foreach (var constructor in field.ContainingType.GetMembers().OfType<IMethodSymbol>().Where(static method => method.MethodKind == MethodKind.StaticConstructor))
        {
            foreach (var reference in constructor.DeclaringSyntaxReferences)
            {
                var declaration = reference.GetSyntax();
                if (HasWrites(compilation.GetSemanticModel(declaration.SyntaxTree), declaration, field))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Rejects assignment, increment and byref escape of an otherwise finite local.</summary>
    /// <param name="model">The original semantic model.</param>
    /// <param name="scope">The complete local scope.</param>
    /// <param name="symbol">The selector local.</param>
    /// <returns>Whether the local can be changed or escaped.</returns>
    private static bool HasWrites(SemanticModel model, SyntaxNode scope, ISymbol symbol)
    {
        foreach (var identifier in scope.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (!SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(identifier).Symbol?.OriginalDefinition, symbol.OriginalDefinition))
            {
                continue;
            }

            if (identifier.Ancestors().TakeWhile(node => node != scope).Any(node =>
                (node is AssignmentExpressionSyntax assignment && assignment.Left.Span.Contains(identifier.Span))
                || node is RefExpressionSyntax
                || (node is InvocationExpressionSyntax invocation && model.GetOperation(invocation) is IInvocationOperation call
                    && call.Arguments.Any(argument => argument.Parameter is { RefKind: not RefKind.None } && argument.Value.Syntax.Span.Contains(identifier.Span)))
                || (node is PrefixUnaryExpressionSyntax prefix && prefix.Kind() is SyntaxKind.PreIncrementExpression or SyntaxKind.PreDecrementExpression)
                || (node is PostfixUnaryExpressionSyntax postfix && postfix.Kind() is SyntaxKind.PostIncrementExpression or SyntaxKind.PostDecrementExpression)
                || node is ArgumentSyntax { RefKindKeyword.RawKind: not 0 }))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reads an ordered storage chain while preserving receiver conversions.</summary>
    /// <param name="operation">The storage operation.</param>
    /// <param name="parameter">The selected root.</param>
    /// <param name="members">The ordered storage operations.</param>
    /// <returns>Whether the chain terminates at the selected root.</returns>
    private static bool TryMemberChain(IOperation operation, IParameterSymbol parameter, List<IOperation> members)
    {
        operation = StripConversions(operation);
        if (operation is IParameterReferenceOperation root)
        {
            return SymbolEqualityComparer.Default.Equals(root.Parameter, parameter);
        }

        if (operation is IConditionalAccessInstanceOperation)
        {
            for (var parent = operation.Parent; parent is not null; parent = parent.Parent)
            {
                if (parent is IConditionalAccessOperation conditional)
                {
                    return TryMemberChain(conditional.Operation, parameter, members);
                }
            }

            return false;
        }

        if (Member(operation) is null || Instance(operation) is not { } instance || !TryMemberChain(instance, parameter, members))
        {
            return false;
        }

        members.Add(operation);
        return true;
    }

    /// <summary>Reads the storage symbol for a semantic access.</summary>
    /// <param name="operation">The operation.</param>
    /// <returns>The exact symbol.</returns>
    private static ISymbol? Member(IOperation operation) => operation switch
    {
        IPropertyReferenceOperation property => property.Property,
        IFieldReferenceOperation field => field.Field,
        _ => null,
    };

    /// <summary>Reads the typed receiver for a storage access.</summary>
    /// <param name="operation">The operation.</param>
    /// <returns>The receiver operation.</returns>
    private static IOperation? Instance(IOperation operation) => operation switch
    {
        IPropertyReferenceOperation property => property.Instance,
        IFieldReferenceOperation field => field.Instance,
        _ => null,
    };

    /// <summary>Removes operation wrappers only while examining dependency ownership.</summary>
    /// <param name="operation">The operation.</param>
    /// <returns>The storage/root operation.</returns>
    private static IOperation StripConversions(IOperation operation)
    {
        while (operation is IConversionOperation or IParenthesizedOperation)
        {
            operation = operation is IConversionOperation conversion ? conversion.Operand : ((IParenthesizedOperation)operation).Operand;
        }

        return operation;
    }

    /// <summary>Removes syntax wrappers with no runtime operation.</summary>
    /// <param name="expression">The selector expression.</param>
    /// <returns>The underlying expression.</returns>
    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax or PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression })
        {
            expression = expression is ParenthesizedExpressionSyntax parenthesized ? parenthesized.Expression : ((PostfixUnaryExpressionSyntax)expression).Operand;
        }

        return expression;
    }

    /// <summary>Admits only built-in casts of literal lambdas to the normal Func or expression contracts.</summary>
    /// <param name="model">The original selecting semantic model.</param>
    /// <param name="expression">The wrapped selector syntax.</param>
    /// <returns>The original lambda, retaining its semantic converted contract, or the untouched expression.</returns>
    private static ExpressionSyntax UnwrapLambdaCast(SemanticModel model, ExpressionSyntax expression)
    {
        expression = Unwrap(expression);
        if (expression is not CastExpressionSyntax cast || Unwrap(cast.Expression) is not LambdaExpressionSyntax converted
            || !IsSelectorContract(model.GetTypeInfo(cast).Type))
        {
            return expression;
        }

        return model.GetOperation(cast) is IDelegateCreationOperation or IConversionOperation { OperatorMethod: null, Conversion.IsUserDefined: false }
            ? converted
            : expression;
    }

    /// <summary>Identifies the exact normal selector contracts without erasing custom conversion behavior.</summary>
    /// <param name="type">The declared cast destination.</param>
    /// <returns>Whether it is Func&lt;TSource, TValue&gt; or Expression of that exact delegate shape.</returns>
    private static bool IsSelectorContract(ITypeSymbol? type)
    {
        if (type is INamedTypeSymbol { MetadataName: "Expression`1" } expression
            && expression.ContainingNamespace.ToDisplayString() == "System.Linq.Expressions")
        {
            type = expression.TypeArguments[0];
        }

        return type is INamedTypeSymbol { MetadataName: "Func`2" } function
            && function.ContainingNamespace.ToDisplayString() == "System";
    }

    /// <summary>Builds exact dependency operations independently of the selector value.</summary>
    /// <param name="site">The site contract.</param>
    /// <param name="model">The model contract.</param>
    /// <param name="parameter">The parameter contract.</param>
    /// <param name="requireNotification">The requireNotification contract.</param>
    /// <param name="metadataOnly">Whether only key operands acquire value access bridges.</param>
    /// <param name="structuralRoot">Whether input variance requires the exact original declaring-member identity.</param>
    private sealed class DependencyWalker(
        CallSite site,
        SemanticModel model,
        IParameterSymbol parameter,
        bool requireNotification,
        bool metadataOnly,
        bool structuralRoot) : OperationWalker
    {
        /// <summary>The exact source receivers behind emitted dependency callbacks.</summary>
        private readonly Dictionary<SelectorDependency, IOperation> _owners = [];

        /// <summary>The current index-argument observation depth.</summary>
        private int _indexDepth;

        /// <summary>Gets the first missing capability.</summary>
        internal string? Failure { get; private set; }

        /// <summary>Gets whether nondependency operations require their original lexical context.</summary>
        internal bool RequiresLexicalAccess { get; private set; }

        /// <summary>Gets the owner guards in evaluation order.</summary>
        internal List<string> Guards { get; } = parameter.Type.IsReferenceType ? [$"{Selector.RootMarker} is null"] : [];

        /// <summary>Gets typed observation owners.</summary>
        internal List<SelectorDependency> Dependencies { get; } = [];

        /// <summary>Gets full root-relative storage paths.</summary>
        internal List<string> Paths { get; } = [];

        /// <summary>Gets structural metadata separately from event dependencies.</summary>
        internal List<SelectorPath> PathPlans { get; } = [];

        /// <summary>Gets exact selected getter invocations, never inferred backing storage.</summary>
        internal Dictionary<ISymbol, string> ReadBridges { get; } = new(SymbolEqualityComparer.Default);

        /// <inheritdoc />
        public override void Visit(IOperation? operation)
        {
            if (operation?.Type?.TypeKind == TypeKind.Dynamic && (!metadataOnly || _indexDepth > 0)
                && operation.Parent is not IConversionOperation { OperatorMethod: null, Type.SpecialType: SpecialType.System_Object, Conversion.IsIdentity: true })
            {
                Failure = "Dynamic dispatch needs an authored typed ValidationSelector converter and dependencies, or an explicit static cast through object; "
                    + "normal generated reads do not emit DLR calls.";
                return;
            }

            base.Visit(operation);
        }

        /// <inheritdoc />
        public override void VisitConditional(IConditionalOperation operation)
        {
            _indexDepth++;
            Visit(operation.Condition);
            _indexDepth--;
            Visit(operation.WhenTrue);
            Visit(operation.WhenFalse);
        }

        /// <inheritdoc />
        public override void VisitPropertyReference(IPropertyReferenceOperation operation)
        {
            Add(operation, operation.Property, operation.Instance);
            Visit(operation.Instance);
            foreach (var argument in operation.Arguments)
            {
                _indexDepth++;
                Visit(argument.Value);
                _indexDepth--;
            }
        }

        /// <inheritdoc />
        public override void VisitFieldReference(IFieldReferenceOperation operation)
        {
            if (operation.Field.IsStatic && operation.Field.IsReadOnly && (!metadataOnly || _indexDepth > 0)
                && !model.Compilation.IsSymbolAccessibleWithin(operation.Field, model.Compilation.Assembly))
            {
                RequiresLexicalAccess = true;
            }

            if (!operation.Field.IsConst && !(operation.Field.IsStatic && operation.Field.IsReadOnly))
            {
                Add(operation, operation.Field, operation.Instance);
            }

            base.VisitFieldReference(operation);
        }

        /// <inheritdoc />
        public override void VisitParameterReference(IParameterReferenceOperation operation)
        {
            if (!SymbolEqualityComparer.Default.Equals(operation.Parameter, parameter))
            {
                Failure = "Live captured parameters require a supplied typed ValidationSelector factory and declared dependencies; an interceptor cannot recover caller closure storage.";
            }

            base.VisitParameterReference(operation);
        }

        /// <inheritdoc />
        public override void VisitLocalReference(ILocalReferenceOperation operation)
        {
            if (!operation.Local.IsConst)
            {
                Failure = "Live captured locals require a supplied typed ValidationSelector factory and declared dependencies; finite selector provenance does not freeze capture values.";
            }

            base.VisitLocalReference(operation);
        }

        /// <inheritdoc />
        public override void VisitInstanceReference(IInstanceReferenceOperation operation)
        {
            Failure = "A captured enclosing instance needs a supplied typed ValidationSelector factory with current receiver and explicit dependencies.";
            base.VisitInstanceReference(operation);
        }

        /// <inheritdoc />
        public override void VisitInvocation(IInvocationOperation operation)
        {
            Failure = "Opaque method effects need declared dependencies or an observable through a supplied typed ValidationSelector factory.";
            base.VisitInvocation(operation);
        }

        /// <summary>Binds changing receivers to the atomic read snapshot rather than evaluating them again.</summary>
        internal void BindOwners()
        {
            var ordinal = 0;
            foreach (var item in _owners)
            {
                var receiver = item.Value;
                while (receiver is IParenthesizedOperation or IConversionOperation { OperatorMethod: null, Conversion.IsIdentity: true }
                    or IConversionOperation { OperatorMethod: null, Conversion.IsReference: true })
                {
                    receiver = receiver is IParenthesizedOperation parentheses ? parentheses.Operand : ((IConversionOperation)receiver).Operand;
                }

                if (receiver is IParameterReferenceOperation root && SymbolEqualityComparer.Default.Equals(root.Parameter, parameter))
                {
                    item.Key.RebindOwner(Selector.RootMarker, []);
                }
                else
                {
                    item.Key.SnapshotOperation = item.Value;
                    item.Key.SnapshotOrdinal = ordinal;
                    ordinal++;
                }
            }
        }

        /// <summary>Reads the source receiver represented by a conditional access placeholder.</summary>
        /// <param name="instance">The member receiver.</param>
        /// <returns>The exact original source operation.</returns>
        private static IOperation ConditionalReceiver(IOperation instance)
        {
            if (instance is IConditionalAccessInstanceOperation)
            {
                for (var parent = instance.Parent; parent is not null; parent = parent.Parent)
                {
                    if (parent is IConditionalAccessOperation conditional)
                    {
                        return conditional.Operation;
                    }
                }
            }

            return instance;
        }

        /// <summary>Tests declared provider capability, including constrained type parameters.</summary>
        /// <param name="type">The owner type.</param>
        /// <param name="contract">The fully qualified interface name.</param>
        /// <returns>Whether the type implements the capability.</returns>
        private static bool Notifies(ITypeSymbol type, string contract) => type.ToDisplayString() == contract || type.AllInterfaces.Any(item => item.ToDisplayString() == contract)
            || (type is ITypeParameterSymbol parameter && parameter.ConstraintTypes.Any(constraint => Notifies(constraint, contract)));

        /// <summary>Proves reference conversion identity without assuming user-defined conversions retain providers.</summary>
        /// <param name="owner">The exact converted receiver.</param>
        /// <param name="contract">The required notification interface.</param>
        /// <returns>Whether the same reference has the declared provider.</returns>
        private static bool NotifiesNotificationOwner(IOperation owner, string contract) => Notifies(owner.Type!, contract)
            || (owner is IConversionOperation { OperatorMethod: null, Conversion.IsReference: true } conversion && NotifiesNotificationOwner(conversion.Operand, contract))
            || (owner is IParenthesizedOperation parentheses && NotifiesNotificationOwner(parentheses.Operand, contract));

        /// <summary>Adds a root-relative member and its exact notification owner.</summary>
        /// <param name="operation">The storage access.</param>
        /// <param name="member">The exact member.</param>
        /// <param name="instance">The typed owner.</param>
        [SuppressMessage("Style", "SST1442", Justification = "Dependency classification separately proves storage identity, read access, owner lifetime and provider capability.")]
        [SuppressMessage("Style", "SST1443", Justification = "The rooted chain walk retains ordered null guards and exact dynamic-index identity.")]
        [SuppressMessage("Style", "SST1523", Justification = "The operation identity and owner contract must be built from the same semantic chain.")]
        private void Add(IOperation operation, ISymbol member, IOperation? instance)
        {
            var chain = new List<IOperation>();
            if (instance is null || !TryMemberChain(operation, parameter, chain))
            {
                Failure = "Static mutable or captured-root access requires a supplied typed selector factory with explicit invalidation dependencies.";
                return;
            }

            var within = site.Model.GetEnclosingSymbol(site.Invocation.SpanStart)?.ContainingType ?? (ISymbol)site.Model.Compilation.Assembly;
            var readable = member is IPropertySymbol property ? property.GetMethod : member;
            if (readable is null || !model.Compilation.IsSymbolAccessibleWithin(readable, within))
            {
                Failure = $"Member '{member.Name}' is inaccessible in the original caller. Supply a typed accessor from a legal model or caller context.";
                return;
            }

            if ((!metadataOnly || _indexDepth > 0) && !model.Compilation.IsSymbolAccessibleWithin(readable, model.Compilation.Assembly)
                && !ReadBridges.ContainsKey(member) && AccessBridgeEmitter.TryEmit(site, member, false, out var bridge, out _, instance.Type))
            {
                var value = SyntaxFactory.ParseExpression(bridge);
                while (value is ParenthesizedExpressionSyntax parentheses)
                {
                    value = parentheses.Expression;
                }

                ReadBridges.Add(member, value is CastExpressionSyntax cast ? cast.Expression.ToString() : bridge);
            }

            var path = string.Join(".", chain.Select(item => item is IPropertyReferenceOperation { Property.IsIndexer: true } indexed
                ? $"[{string.Join(",", indexed.Arguments.Select(argument => OperandExpression(model, argument.Value, parameter, false).Replace($"{Selector.RootMarker}.", string.Empty)))}]"
                : Member(item)!.Name)).Replace(".[", "[");
            if (_indexDepth == 0 && (!Paths.Contains(path) || chain.OfType<IPropertyReferenceOperation>().Any(static property => !property.Arguments.IsEmpty)))
            {
                Paths.Add(path);
                var indices = chain.OfType<IPropertyReferenceOperation>().SelectMany(static item => item.Arguments)
                    .Select(argument => new SelectorIndex(argument.Value, OperandExpression(model, argument.Value, parameter))).ToImmutableArray();
                var structural = structuralRoot || !indices.IsEmpty || chain.Exists(static item => item is IFieldReferenceOperation)
                    || chain.Exists(static item => Instance(item) is IConversionOperation { IsImplicit: false });
                var identityParts = chain.Select(static item => Member(item)!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    + (Instance(item) is IConversionOperation conversion ? $"|conversion:{GeneratorHelpers.TypeName(conversion.Type!)}" : string.Empty));
                var plan = new SelectorPath(path, string.Join("/", identityParts), indices, structural);
                plan.SelectedOperations.Add(operation);
                PathPlans.Add(plan);
            }
            else if (_indexDepth == 0)
            {
                PathPlans.First(plan => plan.DisplayPath == path).SelectedOperations.Add(operation);
            }

            var actualInstance = ConditionalReceiver(instance);
            var owner = Rewrite(model, actualInstance.Syntax, parameter);
            var ownerGuards = parameter.Type.IsReferenceType ? new List<string> { $"{Selector.RootMarker} is null" } : [];
            foreach (var item in chain.Take(chain.Count - 1))
            {
                if (item.Type?.IsReferenceType != true)
                {
                    continue;
                }

                var guard = $"{Rewrite(model, item.Syntax, parameter)} is null";
                ownerGuards.Add(guard);
                if (!Guards.Contains(guard))
                {
                    Guards.Add(guard);
                }
            }

            // A struct member is observed through its nearest reference-owned replacing storage.
            // Subscribing to its boxed value would observe a temporary copy.
            if (!actualInstance.Type!.IsReferenceType)
            {
                return;
            }

            var collection = member is IPropertySymbol { IsIndexer: true };
            if (requireNotification && !NotifiesNotificationOwner(actualInstance, "System.ComponentModel.INotifyPropertyChanged")
                && !(collection && NotifiesNotificationOwner(actualInstance, "System.Collections.Specialized.INotifyCollectionChanged")))
            {
                Failure = $"Owner of '{member.Name}' has no declared change source. Supply a typed notification provider, manual invalidation or snapshot ValidationSelector factory.";
                return;
            }

            var name = collection ? "Item[]" : member.Name;
            var identity = $"{member.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}|{Rewrite(model, operation.Syntax, parameter)}";
            if (!Dependencies.Exists(dependency => dependency.Identity == identity))
            {
                var dependency = new SelectorDependency(member, owner, ownerGuards.ToImmutableArray(), name, identity, collection, _indexDepth > 0);
                Dependencies.Insert(0, dependency);
                _owners.Add(dependency, actualInstance);
            }
        }
    }

    /// <summary>Rewrites only semantic root, constants and type names; casts and operators survive.</summary>
    /// <param name="model">The model contract.</param>
    /// <param name="parameter">The parameter contract.</param>
    /// <param name="replacement">The replacement contract.</param>
    /// <param name="replacements">The replacements contract.</param>
    /// <param name="readBridges">The exact selected legal getters.</param>
    /// <param name="operandValues">The acquired index operands keyed by operation identity.</param>
    /// <param name="retainRootType">Whether executable formatting preserves the original reference parameter type.</param>
    private sealed class ExpressionRewriter(
        SemanticModel model,
        IParameterSymbol parameter,
        SyntaxNode? replacement,
        IReadOnlyDictionary<SyntaxNode, string>? replacements = null,
        IReadOnlyDictionary<ISymbol, string>? readBridges = null,
        IReadOnlyDictionary<IOperation, string>? operandValues = null,
        bool retainRootType = true) : CSharpSyntaxRewriter
    {
        /// <inheritdoc />
        public override SyntaxNode? Visit(SyntaxNode? node)
        {
            if (node is not null && replacements is not null && replacements.TryGetValue(node, out var value))
            {
                var expression = SyntaxFactory.ParseExpression(value);
                return (expression is IdentifierNameSyntax ? expression : SyntaxFactory.ParenthesizedExpression(expression)).WithTriviaFrom(node);
            }

            if (node is TypeSyntax type && model.GetSymbolInfo(type).Symbol is INamedTypeSymbol declared)
            {
                return SyntaxFactory.ParseTypeName(GeneratorHelpers.TypeName(model.GetTypeInfo(type).Type ?? declared)).WithTriviaFrom(node);
            }

            return IsOwnerReplacement(node)
                ? SyntaxFactory.IdentifierName(AccessStep.OwnerMarker).WithTriviaFrom(node!)
                : base.Visit(node);
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            var symbol = model.GetSymbolInfo(node).Symbol;
            if (SymbolEqualityComparer.Default.Equals(symbol, parameter))
            {
                return SyntaxFactory.ParseExpression(retainRootType ? RootExpression(model, parameter) : Selector.RootMarker).WithTriviaFrom(node);
            }

            if (symbol is ILocalSymbol { IsConst: true } local)
            {
                return SyntaxFactory.ParseExpression(FormatConstant(local.Type, local.ConstantValue)).WithTriviaFrom(node);
            }

            if (symbol is IFieldSymbol { IsStatic: true } field)
            {
                return SyntaxFactory.ParseExpression(StaticField(field)).WithTriviaFrom(node);
            }

            return symbol is INamedTypeSymbol type ? SyntaxFactory.ParseName(GeneratorHelpers.TypeName(type)).WithTriviaFrom(node) : base.VisitIdentifierName(node);
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitCastExpression(CastExpressionSyntax node)
        {
            var type = model.GetTypeInfo(node.Type).Type;
            return node.WithExpression((ExpressionSyntax)Visit(node.Expression)!)
                .WithType(type is null ? node.Type : SyntaxFactory.ParseTypeName(GeneratorHelpers.TypeName(type)));
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            var symbol = model.GetSymbolInfo(node).Symbol;
            if (symbol is INamedTypeSymbol type)
            {
                return SyntaxFactory.ParseExpression(GeneratorHelpers.TypeName(type)).WithTriviaFrom(node);
            }

            if (symbol is not null && readBridges is not null && readBridges.TryGetValue(symbol, out var bridge))
            {
                return SyntaxFactory.ParseExpression(Selector.ReplaceIdentifier(bridge, AccessBridgeEmitter.OwnerPlaceholder, Visit(node.Expression)!.ToString())).WithTriviaFrom(node);
            }

            if (symbol is IFieldSymbol { IsStatic: true } field)
            {
                return SyntaxFactory.ParseExpression(StaticField(field)).WithTriviaFrom(node);
            }

            var rewritten = (MemberAccessExpressionSyntax)base.VisitMemberAccessExpression(node)!;
            return retainRootType && symbol?.ContainingType is { } declared && model.GetTypeInfo(node.Expression).Type is ITypeParameterSymbol
                ? rewritten.WithExpression(SyntaxFactory.ParseExpression($"(({GeneratorHelpers.TypeName(declared)})(object)({rewritten.Expression}))"))
                : rewritten;
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitElementAccessExpression(ElementAccessExpressionSyntax node)
        {
            if (model.GetOperation(node) is not IPropertyReferenceOperation indexed)
            {
                return base.VisitElementAccessExpression(node);
            }

            if (readBridges is not null && readBridges.TryGetValue(indexed.Property, out var bridge))
            {
                bridge = Selector.ReplaceIdentifier(bridge, AccessBridgeEmitter.OwnerPlaceholder, Visit(node.Expression)!.ToString());
                foreach (var argument in indexed.Arguments)
                {
                    var value = IndexValue(argument);
                    bridge = Selector.ReplaceIdentifier(bridge, AccessBridgeEmitter.IndexPlaceholder(argument.Parameter!.Ordinal), value);
                }

                return SyntaxFactory.ParseExpression(bridge).WithTriviaFrom(node);
            }

            var rewritten = (ElementAccessExpressionSyntax)base.VisitElementAccessExpression(node)!;
            if (operandValues is not null)
            {
                var arguments = indexed.Arguments.Select(argument => SyntaxFactory.Argument(SyntaxFactory.ParseExpression(IndexValue(argument)))
                    .WithNameColon(SyntaxFactory.NameColon(SyntaxFactory.IdentifierName($"@{argument.Parameter!.Name}"))));
                rewritten = rewritten.WithArgumentList(SyntaxFactory.BracketedArgumentList(SyntaxFactory.SeparatedList(arguments)));
            }

            return retainRootType && model.GetTypeInfo(node.Expression).Type is ITypeParameterSymbol
                ? rewritten.WithExpression(SyntaxFactory.ParseExpression($"(({GeneratorHelpers.TypeName(indexed.Property.ContainingType)})(object)({rewritten.Expression}))"))
                : rewritten;
        }

        /// <summary>Preserves the exact static field owner while retaining the original typed constant contract.</summary>
        /// <param name="field">The semantically selected field.</param>
        /// <returns>The qualified field read or typed constant.</returns>
        private static string StaticField(IFieldSymbol field) => field.IsConst
            ? FormatConstant(field.Type, field.ConstantValue)
            : $"{GeneratorHelpers.TypeName(field.ContainingType)}.@{field.Name}";

        /// <summary>Uses acquired argument operations, including defaults and implicit params arrays, without reading parent syntax.</summary>
        /// <param name="argument">The exact index argument.</param>
        /// <returns>The acquired local or statically typed original argument.</returns>
        private string IndexValue(IArgumentOperation argument)
        {
            if (operandValues is not null && operandValues.TryGetValue(argument.Value, out var value))
            {
                return value;
            }

            return argument.IsImplicit
                ? OperandExpression(model, argument.Value, parameter)
                : Visit(argument.Value.Syntax)!.ToString();
        }

        /// <summary>Matches the exact semantic owner syntax without rewriting unrelated text or type names.</summary>
        /// <param name="node">The current source syntax.</param>
        /// <returns>Whether this syntax is the selected owner replacement.</returns>
        private bool IsOwnerReplacement(SyntaxNode? node) => node is not null && replacement is not null
            && node.SyntaxTree == replacement.SyntaxTree && node.Span == replacement.Span;
    }
}
