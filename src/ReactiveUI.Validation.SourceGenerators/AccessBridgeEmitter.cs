// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators;

/// <summary>Emits legal or explicitly selected static access to actual declared storage.</summary>
internal static class AccessBridgeEmitter
{
    /// <summary>The owner expression placeholder in a returned invocation template.</summary>
    internal const string OwnerPlaceholder = "__RUNIC_OWNER__";

    /// <summary>The assigned value placeholder in a returned invocation template.</summary>
    internal const string ValuePlaceholder = "__RUNIC_VALUE__";

    /// <summary>The generated namespace prefix containing assembly-isolated static access bridges.</summary>
    private const string BridgeNamespacePrefix = "ReactiveUI.Validation.Generated";

    /// <summary>The source suffix for an owner parameter declaration.</summary>
    private const string OwnerParameter = " owner";

    /// <summary>The source suffix for an assigned value parameter declaration.</summary>
    private const string ValueParameter = " value";

    /// <summary>Formats a placeholder for an exact accessor index parameter.</summary>
    /// <param name="accessorParameterOrdinal">The index parameter's ordinal in the actual accessor signature.</param>
    /// <returns>The placeholder mapped by the planner to its already acquired key local.</returns>
    internal static string IndexPlaceholder(int accessorParameterOrdinal) => $"__RUNIC_INDEX{accessorParameterOrdinal}__";

    /// <summary>Identifies setter parameter input contracts that ordinary property assignment cannot express.</summary>
    /// <param name="member">The actual terminal storage member.</param>
    /// <returns>Whether assignment needs the exact setter method's nullable input signature.</returns>
    internal static bool RequiresWriteContractBridge(ISymbol member) => member is IPropertySymbol { SetMethod: not null } property
        && property.Type.NullableAnnotation != NullableAnnotation.Annotated
        && BindingWriteContract.InputType(property.Type, property).NullableAnnotation == NullableAnnotation.Annotated
        && !property.GetAttributes().Any(static attribute => attribute.AttributeClass?.ToDisplayString() == "System.Diagnostics.CodeAnalysis.AllowNullAttribute");

    /// <summary>Creates a bridge for legal ordinary access or an explicitly selected existing storage operation.</summary>
    /// <param name="site">The original intercepted call.</param>
    /// <param name="actualMember">The actual property, accessor method or field symbol.</param>
    /// <param name="write">Whether the operation assigns the existing storage.</param>
    /// <param name="invocationTemplate">The bridge invocation containing owner/value placeholders.</param>
    /// <param name="reason">The precise unsupported contract and supplied typed alternative.</param>
    /// <param name="throughType">The original receiver type used to prove protected-through-type access.</param>
    /// <param name="retainLexicalReadCast">Whether an erased return is restored to its original type in a legal lexical caller.</param>
    /// <returns>Whether the exact static access signature can be emitted.</returns>
    internal static bool TryEmit(CallSite site, ISymbol actualMember, bool write, out string invocationTemplate, out string reason, ITypeSymbol? throughType = null, bool retainLexicalReadCast = true)
    {
        invocationTemplate = string.Empty;
        var member = actualMember is IMethodSymbol { AssociatedSymbol: IPropertySymbol property } ? property : actualMember;
        if (!Selected(site, member, write) && !OrdinaryAccess(site, member, write, throughType))
        {
            const string alternative = "Supply a typed ValidationSelector getter or ValidationTarget/ValidationLens factory.";
            reason = $"Storage '{member.Name}' requires explicit GeneratedValidationAccess from the matching Validation runtime. {alternative}";
            return false;
        }

        if (!Existing(site, member))
        {
            reason = "A static compatibility bridge requires an actual declared member or an exact known producer contract; "
                + "unknown predictions and inferred getter-only backing fields are not storage contracts. Supply a typed factory after the producer contract is available.";
            return false;
        }

        if (!TryMember(member, write, out var owner, out var valueType, out var accessor, out reason))
        {
            return false;
        }

        var signature = new BridgeSignature(site.Model.Compilation, member, owner, valueType, accessor);
        var scope = signature.ClosedLexicalWrapper ? signature.LexicalHost : null;
        if (!TryConstraints(site.Model.Compilation, signature.BridgeParameters, signature.Substitutions, out var constraints, out reason, scope)
            || !SupportedSignature(site.Model.Compilation, signature, out reason))
        {
            return false;
        }

        var bridgeName = $"AccessBridge{site.Id}_{site.AdditionalSources.Count}";
        var assembly = ValidationGenerator.AssemblyIdentity(site.Model.Compilation.AssemblyName ?? "Application");
        var bridgeNamespace = $"{BridgeNamespacePrefix}.{assembly}.Accessors";
        invocationTemplate = Invocation(bridgeNamespace, bridgeName, signature, write, retainLexicalReadCast);
        site.AdditionalSources.Add(new($"{bridgeName}.g.cs", EmitSource(bridgeNamespace, bridgeName, signature, write, constraints)));
        reason = string.Empty;
        return true;
    }

    /// <summary>Rejects unsupported erasure and inaccessible current generic arguments.</summary>
    /// <param name="compilation">The exact semantic compilation.</param>
    /// <param name="signature">The existing storage signature.</param>
    /// <param name="reason">The supplied typed alternative on failure.</param>
    /// <returns>Whether the exact signature can be emitted.</returns>
    private static bool SupportedSignature(Compilation compilation, BridgeSignature signature, out string reason)
    {
        if (!SupportedErasure(signature, out reason) || !SupportedIndexParameters(compilation, signature.ConstructedIndexParameters, out reason))
        {
            return false;
        }

        foreach (var argument in signature.Arguments)
        {
            var scope = signature.ClosedLexicalWrapper ? signature.LexicalHost : null;
            if (NameableWithin(compilation, argument, scope ?? (ISymbol)compilation.Assembly))
            {
                continue;
            }

            reason = "The declaring generic bridge must preserve every type generic argument and its VAR position. "
                + "Supply a typed lexical factory when a generic argument cannot be named by the generated bridge.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>Requires a real lexical type edge for erased generic signatures and rejects unsupported value erasure.</summary>
    /// <param name="signature">The actual member signature and legal host.</param>
    /// <param name="reason">The actionable signature failure.</param>
    /// <returns>Whether reference transport retains every intrinsic CLR type.</returns>
    private static bool SupportedErasure(BridgeSignature signature, out string reason)
    {
        if (signature.RequiresLexicalHost && signature.LexicalHost is null)
        {
            reason = "An erased generic accessor signature requires an accessible partial enclosing type for an exact typed accessor. "
                + "UnsafeAccessorType generic parameter strings are not preserved by the locked full trimmer. "
                + "Make the existing enclosing type partial, or supply a typed ValidationCell/ValidationLens replacement factory in its legal lexical scope.";
            return false;
        }

        if ((signature.OwnerErased && !signature.Owner.IsReferenceType) || (signature.ValueErased && !signature.ValueType.IsReferenceType))
        {
            reason = "UnsafeAccessorType supports inaccessible reference signatures only. "
                + "An inaccessible value type needs a typed lexical ValidationSelector/ValidationLens factory that owns storage and outward write-back.";
            return false;
        }

        if (signature.Member is IFieldSymbol && signature.ValueErased && signature.LexicalHost is null)
        {
            reason = "UnsafeAccessorType cannot describe an inaccessible byref field return. "
                + "Supply a typed lexical getter or ValidationLens replacement factory; no erased ref-object field bridge is valid.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>Requires nameable index signatures or supported reference-only erasure.</summary>
    /// <param name="compilation">The exact semantic compilation.</param>
    /// <param name="parameters">The actual accessor index parameters.</param>
    /// <param name="reason">The supplied typed alternative on failure.</param>
    /// <returns>Whether every index parameter can retain its CLR signature.</returns>
    private static bool SupportedIndexParameters(Compilation compilation, ImmutableArray<IParameterSymbol> parameters, out string reason)
    {
        foreach (var parameter in parameters)
        {
            if (Nameable(compilation, parameter.Type) || parameter.Type.IsReferenceType)
            {
                continue;
            }

            reason = "An inaccessible value-type index parameter cannot use UnsafeAccessorType reference erasure. "
                + "Supply a typed lexical ValidationTarget or ValidationLens factory that receives the current key.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>Writes a separate global bridge class with exact declaring generic constraints.</summary>
    /// <param name="bridgeNamespace">The assembly-specific access bridge namespace.</param>
    /// <param name="bridgeName">The call-specific bridge class name.</param>
    /// <param name="signature">The verified existing storage signature.</param>
    /// <param name="write">Whether to emit assignment.</param>
    /// <param name="constraints">The copied declaring generic constraints.</param>
    /// <returns>The complete bridge source.</returns>
    private static string EmitSource(string bridgeNamespace, string bridgeName, BridgeSignature signature, bool write, string constraints)
    {
        if (signature.LexicalHost is { } host)
        {
            var bridge = new StringBuilder("internal static class ").Append(bridgeName);
            if (!signature.ClosedLexicalWrapper)
            {
                _ = bridge.Append(signature.DeclarationParameters).Append(constraints);
            }

            _ = bridge.AppendLine("\n{");
            EmitLexicalOperation(bridge, signature, write, constraints);
            _ = bridge.AppendLine("}");
            return AccessBridgeLexicalHost.Wrap(host, bridge.ToString());
        }

        var source = new StringBuilder("// <auto-generated/>\n#nullable enable\nnamespace ").Append(bridgeNamespace).Append(";\ninternal static class ")
            .Append(bridgeName).Append(signature.DeclarationParameters).Append(constraints).AppendLine("\n{");
        if (signature.Member is IFieldSymbol)
        {
            EmitField(source, signature, write);
        }
        else
        {
            EmitProperty(source, signature, write);
        }

        _ = source.AppendLine("}");
        return source.ToString();
    }

    /// <summary>Emits a real typed intrinsic and a reference transport wrapper in the legal enclosing host.</summary>
    /// <param name="source">The nested bridge source.</param>
    /// <param name="signature">The exact declared storage signature.</param>
    /// <param name="write">Whether the operation writes storage.</param>
    /// <param name="constraints">The exact private core constraints.</param>
    private static void EmitLexicalOperation(StringBuilder source, BridgeSignature signature, bool write, string constraints)
    {
        if (signature.ClosedLexicalWrapper)
        {
            _ = source.Append("private static class AccessCore").Append(signature.DeclarationParameters).Append(constraints).AppendLine("\n{");
        }

        if (signature.Member is IFieldSymbol)
        {
            EmitLexicalField(source, signature, write);
        }
        else
        {
            EmitLexicalProperty(source, signature, write);
        }

        if (signature.ClosedLexicalWrapper)
        {
            _ = source.AppendLine("}");
        }

        EmitLexicalWrapper(source, signature, write);
    }

    /// <summary>Declares exact typed field access without erased byref or generic type-name strings.</summary>
    /// <param name="source">The nested bridge source.</param>
    /// <param name="signature">The verified named field signature.</param>
    /// <param name="write">Whether the wrapper writes the field.</param>
    private static void EmitLexicalField(StringBuilder source, BridgeSignature signature, bool write)
    {
        if (!write)
        {
            _ = source.Append(signature.ReadAttributes);
        }

        _ = source.Append("[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = ")
            .Append(GeneratorHelpers.Quote(signature.Member.MetadataName)).AppendLine(")]")
            .Append(signature.ClosedLexicalWrapper ? "internal static extern ref " : "private static extern ref ")
            .Append(write ? signature.TypedWriteSyntax : signature.TypedValueSyntax).Append(" Storage(")
            .Append(signature.OwnerModifier).Append(signature.TypedOwnerSyntax).AppendLine(" owner);");
    }

    /// <summary>Declares an exact typed accessor method with its original VAR positions and nullable flow.</summary>
    /// <param name="source">The nested bridge source.</param>
    /// <param name="signature">The verified property accessor signature.</param>
    /// <param name="write">Whether the actual setter is selected.</param>
    private static void EmitLexicalProperty(StringBuilder source, BridgeSignature signature, bool write)
    {
        _ = source.Append("[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = ")
            .Append(GeneratorHelpers.Quote(signature.Accessor!.MetadataName)).AppendLine(")]");
        if (!write)
        {
            _ = source.Append(signature.ReadAttributes);
        }

        _ = source.Append(signature.ClosedLexicalWrapper ? "internal static extern " : "private static extern ")
            .Append(write ? "void" : signature.TypedValueSyntax).Append(" Access(")
            .Append(signature.OwnerModifier).Append(signature.TypedOwnerSyntax).Append(OwnerParameter);
        foreach (var declaration in signature.TypedIndexDeclarations)
        {
            _ = source.Append(", ").Append(declaration);
        }

        if (write)
        {
            _ = source.Append(", ").Append(signature.WriteAttributes).Append(signature.TypedWriteSyntax).Append(ValueParameter);
        }

        _ = source.AppendLine(");");
    }

    /// <summary>Keeps inaccessible CLR types inside their lexical host and transports references outward.</summary>
    /// <param name="source">The nested bridge source.</param>
    /// <param name="signature">The exact typed and outward transport signature.</param>
    /// <param name="write">Whether to emit assignment.</param>
    private static void EmitLexicalWrapper(StringBuilder source, BridgeSignature signature, bool write)
    {
        if (!write)
        {
            _ = source.Append(signature.ReadAttributes);
        }

        _ = source.Append("internal static ").Append(write ? "void Write(" : $"{signature.ValueSyntax} Read(")
            .Append(signature.OwnerModifier).Append(signature.OwnerSyntax).Append(OwnerParameter);
        foreach (var declaration in signature.TransportIndexDeclarations)
        {
            _ = source.Append(", ").Append(declaration);
        }

        if (write)
        {
            _ = source.Append(", ").Append(signature.WriteAttributes).Append(signature.WriteValueSyntax).Append(ValueParameter);
        }

        EmitLexicalInvocation(source, signature, write);
    }

    /// <summary>Calls the typed intrinsic using already acquired owner, index and value operands.</summary>
    /// <param name="source">The nested operation wrapper.</param>
    /// <param name="signature">The verified transport and typed signature.</param>
    /// <param name="write">Whether the intrinsic assigns storage.</param>
    private static void EmitLexicalInvocation(StringBuilder source, BridgeSignature signature, bool write)
    {
        _ = source.Append(") => ").Append(signature.CoreCallPrefix).Append(signature.Member is IFieldSymbol ? "Storage(" : "Access(").Append(signature.TypedOwnerArgument);
        foreach (var argument in signature.TypedIndexArguments)
        {
            _ = source.Append(", ").Append(argument);
        }

        if (write && signature.Member is not IFieldSymbol)
        {
            _ = source.Append(", ").Append(signature.TypedWriteArgument);
        }

        _ = source.Append(')');
        if (write && signature.Member is IFieldSymbol)
        {
            _ = source.Append(" = ").Append(signature.TypedWriteArgument);
        }

        _ = source.AppendLine(";");
    }

    /// <summary>Writes a typed byref field declaration and a managed operation wrapper.</summary>
    /// <param name="source">The source builder.</param>
    /// <param name="signature">The verified exact field signature.</param>
    /// <param name="write">Whether to emit assignment.</param>
    private static void EmitField(StringBuilder source, BridgeSignature signature, bool write)
    {
        if (!write)
        {
            _ = source.Append(signature.ReadAttributes);
        }

        _ = source.Append("[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = ")
            .Append(GeneratorHelpers.Quote(signature.Member.MetadataName)).AppendLine(")]")
            .Append("private static extern ref ").Append(write ? signature.WriteValueSyntax : signature.ValueSyntax).Append(" Storage(").Append(signature.OwnerAttribute)
            .Append(signature.OwnerModifier).Append(signature.OwnerSyntax).AppendLine(" owner);");
        if (!write)
        {
            _ = source.Append(signature.ReadAttributes);
        }

        _ = source.Append("internal static ").Append(write ? "void" : signature.ValueSyntax).Append(write ? " Write(" : " Read(")
            .Append(signature.OwnerModifier).Append(signature.OwnerSyntax).Append(OwnerParameter);
        if (write)
        {
            _ = source.Append(", ").Append(signature.WriteAttributes).Append(signature.WriteValueSyntax).Append(" value) => Storage(").Append(signature.OwnerModifier).AppendLine("owner) = value;");
        }
        else
        {
            _ = source.Append(") => Storage(").Append(signature.OwnerModifier).AppendLine("owner);");
        }
    }

    /// <summary>Writes the exact existing accessor method signature.</summary>
    /// <param name="source">The source builder.</param>
    /// <param name="signature">The verified exact accessor signature.</param>
    /// <param name="write">Whether to emit the setter.</param>
    private static void EmitProperty(StringBuilder source, BridgeSignature signature, bool write)
    {
        _ = source.Append("[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = ")
            .Append(GeneratorHelpers.Quote(signature.Accessor!.MetadataName)).AppendLine(")]");
        if (!write)
        {
            _ = source.Append(signature.ReadAttributes);
        }

        if (!write && signature.ValueErased)
        {
            _ = source.Append("[return: ").Append(TypeAttributeBody(signature.OriginalValue, signature.Parameters)).AppendLine("]");
        }

        _ = source.Append("internal static extern ").Append(write ? "void" : signature.ValueSyntax).Append(write ? " Write(" : " Read(")
            .Append(signature.OwnerAttribute).Append(signature.OwnerModifier).Append(signature.OwnerSyntax).Append(OwnerParameter);
        foreach (var declaration in signature.IndexDeclarations)
        {
            _ = source.Append(", ").Append(declaration);
        }

        if (write)
        {
            _ = source.Append(", ").Append(signature.ValueAttribute).Append(signature.WriteAttributes).Append(signature.WriteValueSyntax).Append(ValueParameter);
        }

        _ = source.AppendLine(");");
    }

    /// <summary>Creates a lexical typed read or an erased reference transport between exact accessors.</summary>
    /// <param name="bridgeNamespace">The assembly-specific access bridge namespace.</param>
    /// <param name="bridgeName">The call-specific class name.</param>
    /// <param name="signature">The verified exact signature.</param>
    /// <param name="write">Whether to emit assignment.</param>
    /// <param name="retainLexicalReadCast">Whether the caller can retain the original inaccessible return type.</param>
    /// <returns>The invocation with syntax placeholders.</returns>
    private static string Invocation(string bridgeNamespace, string bridgeName, BridgeSignature signature, bool write, bool retainLexicalReadCast)
    {
        var method = write ? "Write" : "Read";
        var value = write ? $", {ValuePlaceholder}" : string.Empty;
        var indices = string.Concat(signature.IndexParameters.Select(static parameter => $", {IndexPlaceholder(parameter.Ordinal)}"));
        var owner = signature.LexicalHost is { } host ? GeneratorHelpers.TypeName(host) : $"global::{bridgeNamespace}";
        var arguments = signature.ClosedLexicalWrapper ? string.Empty : signature.CallArguments;
        var invocation = $"{owner}.{bridgeName}{arguments}.{method}({signature.OwnerModifier}{OwnerPlaceholder}{indices}{value})";
        return !write && signature.ValueErased && retainLexicalReadCast ? $"(({GeneratorHelpers.TypeName(signature.ValueType)}){invocation})" : invocation;
    }

    /// <summary>Requires explicit selection on the exact member or the selected accessor.</summary>
    /// <param name="site">The original call site.</param>
    /// <param name="member">The associated storage property or field.</param>
    /// <param name="write">Whether the setter is selected.</param>
    /// <returns>Whether the exact operation is explicitly marked.</returns>
    private static bool Selected(CallSite site, ISymbol member, bool write)
    {
        var attributeName = $"{site.NamespaceRoot}.Capabilities.GeneratedValidationAccessAttribute";
        var runtimeAssembly = site.Method.ContainingAssembly.Identity;
        IMethodSymbol? accessor = null;
        if (member is IPropertySymbol property)
        {
            accessor = write ? property.SetMethod : property.GetMethod;
        }

        return HasMarker(member, attributeName, runtimeAssembly)
            || (accessor is not null && HasMarker(accessor, attributeName, runtimeAssembly));
    }

    /// <summary>Requires the exact marker contract from the same runtime assembly as the intercepted method.</summary>
    /// <param name="symbol">The selected member or accessor.</param>
    /// <param name="attributeName">The matching runtime flavor's marker identity.</param>
    /// <param name="runtimeAssembly">The intercepted runtime's full assembly identity.</param>
    /// <returns>Whether the marker is the intended runtime contract.</returns>
    private static bool HasMarker(ISymbol symbol, string attributeName, AssemblyIdentity runtimeAssembly)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass is { } marker && marker.ToDisplayString() == attributeName
                && marker.ContainingAssembly.Identity.Equals(runtimeAssembly))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Preserves access already legal in the original caller without authorizing init or readonly mutation.</summary>
    /// <param name="site">The original invocation and lexical access scope.</param>
    /// <param name="member">The exact existing storage member.</param>
    /// <param name="write">Whether ordinary assignment is required.</param>
    /// <param name="throughType">The actual original receiver type.</param>
    /// <returns>Whether the accessor operation was already legal for the caller.</returns>
    private static bool OrdinaryAccess(CallSite site, ISymbol member, bool write, ITypeSymbol? throughType)
    {
        var operation = member switch
        {
            IPropertySymbol { SetMethod: { IsInitOnly: false } setter } when write => setter,
            IPropertySymbol { GetMethod: { } getter } when !write => getter,
            IFieldSymbol { IsReadOnly: false, IsConst: false } field when write => (ISymbol)field,
            IFieldSymbol field when !write => field,
            _ => null,
        };
        if (operation is null)
        {
            return false;
        }

        var within = site.Model.GetEnclosingSymbol(site.Invocation.SpanStart)?.ContainingType ?? (ISymbol)site.Model.Compilation.Assembly;
        return site.Model.Compilation.IsSymbolAccessibleWithin(operation, within, throughType ?? member.ContainingType);
    }

    /// <summary>Accepts actual source, referenced metadata or finite known producer contracts.</summary>
    /// <param name="site">The original call site.</param>
    /// <param name="member">The selected storage member.</param>
    /// <returns>Whether the declaration is actually present.</returns>
    private static bool Existing(CallSite site, ISymbol member)
    {
        if (member.DeclaringSyntaxReferences.IsEmpty || site.HasProducerContract(member))
        {
            return true;
        }

        foreach (var reference in member.DeclaringSyntaxReferences)
        {
            if (!site.OriginalCompilation.ContainsSyntaxTree(reference.SyntaxTree))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Checks only actual instance property accessors and named fields.</summary>
    /// <param name="member">The selected storage member.</param>
    /// <param name="write">Whether its setter is needed.</param>
    /// <param name="owner">The exact declaring type.</param>
    /// <param name="valueType">The actual stored value type.</param>
    /// <param name="accessor">The selected getter or setter for properties.</param>
    /// <param name="reason">The unsupported contract.</param>
    /// <returns>Whether the declared storage operation can be accessed.</returns>
    private static bool TryMember(ISymbol member, bool write, out INamedTypeSymbol owner, out ITypeSymbol valueType, out IMethodSymbol? accessor, out string reason)
    {
        owner = member.ContainingType!;
        valueType = null!;
        accessor = null;
        reason = "A static access bridge requires an existing nonstatic property accessor or nonconstant named field. Supply a typed getter/setter factory for other operations.";
        if (UnsupportedOwner(member, owner))
        {
            return false;
        }

        switch (member)
        {
            case IPropertySymbol property:
            {
                valueType = property.Type;
                if (!TryProperty(property, write, out accessor, out reason))
                {
                    return false;
                }

                break;
            }

            case IFieldSymbol { IsConst: false, IsFixedSizeBuffer: false } field:
            {
                valueType = field.Type;
                break;
            }

            default:
            {
                return false;
            }
        }

        if (UnsupportedValue(valueType))
        {
            reason = "Borrowed ref-like, pointer, dynamic and unresolved signatures cannot become an owned generated access bridge. "
                + "Supply a synchronous typed snapshot or stable reference-owned ValidationLens factory.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>Rejects owner shapes that cannot be represented by an instance storage bridge.</summary>
    /// <param name="member">The exact storage member.</param>
    /// <param name="owner">The exact declaring owner, if present.</param>
    /// <returns>Whether the owner is unsupported.</returns>
    private static bool UnsupportedOwner(ISymbol member, INamedTypeSymbol? owner) => owner is null || owner.IsRefLikeType
        || owner.TypeKind is TypeKind.Interface or TypeKind.Error || member.IsStatic;

    /// <summary>Rejects borrowed or unresolved values rather than converting them into owned storage.</summary>
    /// <param name="valueType">The exact signature value.</param>
    /// <returns>Whether the value is unsupported.</returns>
    private static bool UnsupportedValue(ITypeSymbol valueType) => valueType.IsRefLikeType || valueType is ITypeParameterSymbol { AllowsRefLikeType: true }
        || valueType.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer or TypeKind.Error or TypeKind.Dynamic;

    /// <summary>Requires an actual callable accessor, preserving the getter-only storage boundary.</summary>
    /// <param name="property">The exact property or indexer.</param>
    /// <param name="write">Whether the setter is required.</param>
    /// <param name="accessor">The exact accessor method.</param>
    /// <param name="reason">The actionable missing or borrowed accessor diagnostic.</param>
    /// <returns>Whether the selected accessor is supported.</returns>
    private static bool TryProperty(IPropertySymbol property, bool write, out IMethodSymbol? accessor, out string reason)
    {
        accessor = write ? property.SetMethod : property.GetMethod;
        if (property.ReturnsByRef || property.ReturnsByRefReadonly)
        {
            reason = "A borrowed byref property cannot become an owned generated access bridge. "
                + "Supply a typed lexical snapshot getter or stable reference-owned ValidationLens storage factory.";
            return false;
        }

        if (accessor is null || accessor.IsAbstract || accessor.IsStatic)
        {
            reason = "The property must declare the selected actual getter or setter. A getter-only property has no writable contract; "
                + "supply a typed immutable ValidationTarget replacement instead of inferred backing storage.";
            return false;
        }

        foreach (var parameter in property.Parameters)
        {
            if (parameter.RefKind == RefKind.None && !UnsupportedValue(parameter.Type))
            {
                continue;
            }

            reason = "Borrowed byref, ref-like or unresolved index parameters cannot become an owned static accessor bridge. "
                + "Supply a synchronous typed ValidationTarget or ValidationLens factory that acquires stable keys.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>Collects the current constructed arguments in the same CLR VAR order.</summary>
    /// <param name="owner">The current constructed declaring type.</param>
    /// <returns>The actual declaring type arguments.</returns>
    private static ImmutableArray<ITypeSymbol> DeclaringArguments(INamedTypeSymbol owner)
    {
        var containing = owner.ContainingType is null ? ImmutableArray<ITypeSymbol>.Empty : DeclaringArguments(owner.ContainingType);
        return containing.AddRange(owner.TypeArguments);
    }

    /// <summary>Copies every declaring generic constraint rather than moving VARs to method MVARs.</summary>
    /// <param name="compilation">The caller compilation.</param>
    /// <param name="parameters">The original declaring type parameters.</param>
    /// <param name="substitutions">The corresponding generated parameter names.</param>
    /// <param name="text">The exact generated constraint clauses.</param>
    /// <param name="reason">The inaccessible constraint failure.</param>
    /// <param name="scope">The private intrinsic's proven lexical scope, if present.</param>
    /// <returns>Whether all constraints can be preserved.</returns>
    private static bool TryConstraints(
        Compilation compilation,
        ImmutableArray<ITypeParameterSymbol> parameters,
        Dictionary<ITypeParameterSymbol, string> substitutions,
        out string text,
        out string reason,
        INamedTypeSymbol? scope = null)
    {
        var result = new StringBuilder();
        foreach (var parameter in parameters)
        {
            var constraints = PrimaryConstraints(parameter);

            foreach (var constraint in parameter.ConstraintTypes)
            {
                if (!NameableWithin(compilation, constraint, scope ?? (ISymbol)compilation.Assembly))
                {
                    text = string.Empty;
                    reason = "The exact declaring generic constraints cannot be named outside their lexical owner. "
                        + "Supply a typed lexical factory; an erased or relaxed generic constraint is not a compatible accessor signature.";
                    return false;
                }

                constraints.Add(TypeName(constraint, substitutions));
            }

            if (parameter.HasConstructorConstraint)
            {
                constraints.Add("new()");
            }

            if (parameter.AllowsRefLikeType)
            {
                constraints.Add("allows ref struct");
            }

            if (constraints.Count != 0)
            {
                _ = result.Append("\nwhere ").Append(substitutions[parameter]).Append(" : ").Append(string.Join(", ", constraints));
            }
        }

        text = result.ToString();
        reason = string.Empty;
        return true;
    }

    /// <summary>Copies the mutually exclusive primary declaring generic constraint.</summary>
    /// <param name="parameter">The original declaring generic parameter.</param>
    /// <returns>The exact primary constraint, if present.</returns>
    private static List<string> PrimaryConstraints(ITypeParameterSymbol parameter)
    {
        if (parameter.HasUnmanagedTypeConstraint)
        {
            return ["unmanaged"];
        }

        if (parameter.HasValueTypeConstraint)
        {
            return ["struct"];
        }

        if (parameter.HasReferenceTypeConstraint)
        {
            return [parameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated ? "class?" : "class"];
        }

        return parameter.HasNotNullConstraint ? ["notnull"] : [];
    }

    /// <summary>Checks generated naming without treating open type parameters as closed types.</summary>
    /// <param name="compilation">The caller compilation.</param>
    /// <param name="type">The signature type to name.</param>
    /// <returns>Whether generated C# can name the type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Nameable(Compilation compilation, ITypeSymbol type) => NameableWithin(compilation, type, compilation.Assembly);

    /// <summary>Checks the exact lexical naming scope without exposing private signatures in transport.</summary>
    /// <param name="compilation">The caller compilation.</param>
    /// <param name="type">The exact signature or constraint type.</param>
    /// <param name="scope">The actual assembly or legal lexical host.</param>
    /// <returns>Whether generated source at that scope can name the type.</returns>
    private static bool NameableWithin(Compilation compilation, ITypeSymbol type, ISymbol scope) => type switch
    {
        ITypeParameterSymbol => true,
        IArrayTypeSymbol array => NameableWithin(compilation, array.ElementType, scope),
        INamedTypeSymbol named => !named.IsFileLocal && compilation.IsSymbolAccessibleWithin(named.OriginalDefinition, scope)
            && (named.ContainingType is null || NameableWithin(compilation, named.ContainingType, scope)) && named.TypeArguments.All(argument => NameableWithin(compilation, argument, scope)),
        _ => type.TypeKind is not (TypeKind.Error or TypeKind.Dynamic or TypeKind.Pointer or TypeKind.FunctionPointer),
    };

    /// <summary>Formats source syntax with symbol-identity generic substitutions.</summary>
    /// <param name="type">The original signature type.</param>
    /// <param name="substitutions">The generated names by exact symbol identity.</param>
    /// <returns>The signature syntax.</returns>
    private static string TypeName(ITypeSymbol type, Dictionary<ITypeParameterSymbol, string> substitutions)
    {
        var format = SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);
        var source = new StringBuilder();
        var nullableReplacement = false;
        foreach (var part in type.ToDisplayParts(format))
        {
            var text = part.ToString();
            if (nullableReplacement && text == "?")
            {
                nullableReplacement = false;
                continue;
            }

            nullableReplacement = false;
            if (part.Symbol is ITypeParameterSymbol parameter && substitutions.TryGetValue(parameter, out var replacement))
            {
                text = replacement;
                nullableReplacement = replacement.EndsWith("?", StringComparison.Ordinal);
            }

            _ = source.Append(text);
        }

        return source.ToString();
    }

    /// <summary>Formats the unbracketed signature attribute for a parameter or return target.</summary>
    /// <param name="type">The erased reference signature.</param>
    /// <param name="parameters">The declaring VAR parameters.</param>
    /// <returns>The attribute type and exact constant metadata identity.</returns>
    private static string TypeAttributeBody(ITypeSymbol type, ImmutableArray<ITypeParameterSymbol> parameters) =>
        $"global::System.Runtime.CompilerServices.UnsafeAccessorType({GeneratorHelpers.Quote(MetadataTypeName(type, parameters))})";

    /// <summary>Formats reflection type syntax with declaring VAR positions and exact assemblies.</summary>
    /// <param name="type">The referenced signature type.</param>
    /// <param name="parameters">The declaring VAR parameters.</param>
    /// <param name="includeAssembly">Whether to include the outer type's assembly qualification.</param>
    /// <returns>The static metadata type identity.</returns>
    private static string MetadataTypeName(ITypeSymbol type, ImmutableArray<ITypeParameterSymbol> parameters, bool includeAssembly = true)
    {
        if (type is ITypeParameterSymbol parameter)
        {
            return $"!{parameters.IndexOf(parameter, 0, parameters.Length, SymbolEqualityComparer.Default)}";
        }

        if (type is IArrayTypeSymbol array)
        {
            var element = MetadataTypeName(array.ElementType, parameters, false);
            var assembly = SignatureAssembly(array.ElementType);
            var suffix = $"[{new string(',', array.Rank - 1)}]";
            return $"{element}{suffix}{(includeAssembly && assembly is not null ? $", {assembly}" : string.Empty)}";
        }

        var named = (INamedTypeSymbol)type;
        var names = new Stack<string>();
        for (var current = named; current is not null; current = current.ContainingType)
        {
            names.Push(current.MetadataName);
        }

        var prefix = named.ContainingNamespace.IsGlobalNamespace ? string.Empty : $"{named.ContainingNamespace.ToDisplayString()}.";
        var result = $"{prefix}{string.Join("+", names)}";
        var arguments = DeclaringArguments(named);
        if (!arguments.IsEmpty)
        {
            result = $"{result}[[{string.Join("],[", arguments.Select(argument => MetadataTypeName(argument, parameters)))}]]";
        }

        return $"{result}{(includeAssembly ? $", {named.ContainingAssembly.Identity.GetDisplayName()}" : string.Empty)}";
    }

    /// <summary>Resolves assembly qualification without moving an array suffix into assembly identity text.</summary>
    /// <param name="type">The array element or named signature type.</param>
    /// <returns>The exact semantic assembly identity, if the signature has one.</returns>
    private static string? SignatureAssembly(ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol array => SignatureAssembly(array.ElementType),
        INamedTypeSymbol named => named.ContainingAssembly.Identity.GetDisplayName(),
        _ => null,
    };

    /// <summary>Retains the exact semantic member and its source/metadata signature spellings.</summary>
    private sealed class BridgeSignature
    {
        /// <summary>The source spelling of a reference-only erased signature.</summary>
        private const string ErasedReferenceSyntax = "object";

        /// <summary>Initializes a new instance of the <see cref="BridgeSignature"/> class.</summary>
        /// <param name="compilation">The exact semantic compilation.</param>
        /// <param name="member">The actual storage member.</param>
        /// <param name="owner">The exact declaring owner.</param>
        /// <param name="valueType">The constructed value type.</param>
        /// <param name="accessor">The actual selected property accessor.</param>
        internal BridgeSignature(Compilation compilation, ISymbol member, INamedTypeSymbol owner, ITypeSymbol valueType, IMethodSymbol? accessor)
        {
            Member = member;
            Owner = owner;
            ValueType = valueType;
            Accessor = accessor;
            Parameters = DeclaringParameters(owner.OriginalDefinition);
            Arguments = DeclaringArguments(owner);
            OriginalValue = StorageDefinitionType(member, valueType);
            OwnerErased = !Nameable(compilation, owner.OriginalDefinition);
            ValueErased = !Nameable(compilation, valueType);
            IndexParameters = AccessorIndexParameters(accessor);
            RequiresLexicalHost = RequiresTypedLexicalSignature(compilation);
            LexicalHost = FindLexicalHost(compilation, owner);
            var hostCount = LexicalHost is null ? 0 : DeclaringParameters(LexicalHost.OriginalDefinition).Length;
            BridgeParameters = Parameters.RemoveRange(0, hostCount);
            Substitutions = CreateSubstitutions(hostCount);
            var bridgeArguments = Arguments.RemoveRange(0, hostCount);
            ClosedLexicalWrapper = LexicalHost is not null && bridgeArguments.All(argument => InHostScope(argument, hostCount));
            TransportSubstitutions = CreateTransportSubstitutions(hostCount);
            OwnerSyntax = OwnerErased ? ErasedReferenceSyntax : TypeName(owner.OriginalDefinition, TransportSubstitutions);
            ValueSyntax = ParameterTypeSyntax(ValueErased, OriginalValue);
            var writeValue = BindingWriteContract.InputType(OriginalValue, member);
            WriteValueSyntax = ParameterTypeSyntax(ValueErased, writeValue);
            WriteAttributes = InputAttributes(member);
            ReadAttributes = ReadPostconditions(member, accessor);
            IndexDeclarations = IndexParameters.Select(parameter => IndexDeclaration(compilation, parameter)).ToImmutableArray();
            TransportIndexDeclarations = IndexParameters.Select(parameter => IndexDeclaration(compilation, parameter, false)).ToImmutableArray();
            OwnerAttribute = ErasedAttribute(OwnerErased, owner.OriginalDefinition, Parameters);
            ValueAttribute = ErasedAttribute(ValueErased, OriginalValue, Parameters);
            OwnerModifier = owner.IsValueType ? "ref " : string.Empty;
            DeclarationParameters = BridgeParameters.IsEmpty ? string.Empty : $"<{string.Join(", ", BridgeParameters.Select(parameter => Substitutions[parameter]))}>";
            CallArguments = bridgeArguments.IsEmpty ? string.Empty : $"<{string.Join(", ", bridgeArguments.Select(GeneratorHelpers.TypeName))}>";
        }

        /// <summary>Gets the exact existing member.</summary>
        internal ISymbol Member { get; }

        /// <summary>Gets the exact constructed declaring owner.</summary>
        internal INamedTypeSymbol Owner { get; }

        /// <summary>Gets the exact constructed storage value.</summary>
        internal ITypeSymbol ValueType { get; }

        /// <summary>Gets the exact accessor method, if a property is selected.</summary>
        internal IMethodSymbol? Accessor { get; }

        /// <summary>Gets the definition's value signature preserving declaring VARs.</summary>
        internal ITypeSymbol OriginalValue { get; }

        /// <summary>Gets the declaring type parameters in CLR VAR order.</summary>
        internal ImmutableArray<ITypeParameterSymbol> Parameters { get; }

        /// <summary>Gets parameters declared by the bridge after the lexical host consumes its original VAR prefix.</summary>
        internal ImmutableArray<ITypeParameterSymbol> BridgeParameters { get; }

        /// <summary>Gets the legal enclosing partial type for typed generic-owner signatures.</summary>
        internal INamedTypeSymbol? LexicalHost { get; }

        /// <summary>Gets a value indicating whether an erased generic signature needs a real lexical type edge.</summary>
        internal bool RequiresLexicalHost { get; }

        /// <summary>Gets a value indicating whether exact closed suffix arguments stay behind a nongeneric transport wrapper.</summary>
        internal bool ClosedLexicalWrapper { get; }

        /// <summary>Gets the current declaring arguments in the same order.</summary>
        internal ImmutableArray<ITypeSymbol> Arguments { get; }

        /// <summary>Gets source syntax substitutions keyed by semantic type parameter identity.</summary>
        internal Dictionary<ITypeParameterSymbol, string> Substitutions { get; }

        /// <summary>Gets lexical host parameters and the actual closed suffix arguments used by transport wrappers.</summary>
        internal Dictionary<ITypeParameterSymbol, string> TransportSubstitutions { get; }

        /// <summary>Gets a value indicating whether the owner requires reference-only erasure.</summary>
        internal bool OwnerErased { get; }

        /// <summary>Gets a value indicating whether the value requires reference-only erasure.</summary>
        internal bool ValueErased { get; }

        /// <summary>Gets the generated owner parameter type.</summary>
        internal string OwnerSyntax { get; }

        /// <summary>Gets the generated value type.</summary>
        internal string ValueSyntax { get; }

        /// <summary>Gets the exact typed owner signature inside its legal lexical host.</summary>
        internal string TypedOwnerSyntax => TypeName(Owner.OriginalDefinition, Substitutions);

        /// <summary>Gets an exact owner argument without boxing nameable value-type storage.</summary>
        internal string TypedOwnerArgument => OwnerErased ? $"({TypeName(Owner.OriginalDefinition, TransportSubstitutions)})owner" : $"{OwnerModifier}owner";

        /// <summary>Gets the original typed return signature inside its legal lexical host.</summary>
        internal string TypedValueSyntax => TypeName(OriginalValue, Substitutions);

        /// <summary>Gets the typed setter input with effective annotations and unchanged CLR identity.</summary>
        internal string TypedWriteSyntax => TypeName(BindingWriteContract.InputType(OriginalValue, Member), Substitutions);

        /// <summary>Gets the assigned transport reference cast only where the lexical signature is inaccessible globally.</summary>
        internal string TypedWriteArgument => ValueErased ? $"({TypeName(BindingWriteContract.InputType(OriginalValue, Member), TransportSubstitutions)})value" : "value";

        /// <summary>Gets the closed private core invocation prefix without changing its declaring VAR signature.</summary>
        internal string CoreCallPrefix => ClosedLexicalWrapper ? $"AccessCore{LexicalCoreArguments}." : string.Empty;

        /// <summary>Gets private core arguments rebound by exact host-prefix identity inside the partial declaration.</summary>
        internal string LexicalCoreArguments
        {
            get
            {
                var arguments = Arguments.RemoveRange(0, Parameters.Length - BridgeParameters.Length);
                return arguments.IsEmpty ? string.Empty : $"<{string.Join(", ", arguments.Select(argument => TypeName(argument, Substitutions)))}>";
            }
        }

        /// <summary>Gets the effective writer input syntax without changing the CLR or getter signature.</summary>
        internal string WriteValueSyntax { get; }

        /// <summary>Gets input flow restrictions that must retain a nullable CLR value signature.</summary>
        internal string WriteAttributes { get; }

        /// <summary>Gets exact readable member and getter-return postconditions, independent of setter inputs.</summary>
        internal string ReadAttributes { get; }

        /// <summary>Gets the exact accessor index parameters in signature ordinal order.</summary>
        internal ImmutableArray<IParameterSymbol> IndexParameters { get; }

        /// <summary>Gets current constructed index types used to decide outward transport accessibility.</summary>
        internal ImmutableArray<IParameterSymbol> ConstructedIndexParameters => AccessorIndexParameters(Accessor, false);

        /// <summary>Gets their emitted typed declarations with exact input annotations and reference erasure attributes.</summary>
        internal ImmutableArray<string> IndexDeclarations { get; }

        /// <summary>Gets exact typed index parameter declarations inside the lexical intrinsic.</summary>
        internal IEnumerable<string> TypedIndexDeclarations => IndexParameters.Select(parameter =>
            $"{InputAttributes(parameter)}{TypeName(BindingWriteContract.InputType(parameter.Type, parameter), Substitutions)} index{parameter.Ordinal}");

        /// <summary>Gets transport parameters without UnsafeAccessorType on ordinary wrapper methods.</summary>
        internal ImmutableArray<string> TransportIndexDeclarations { get; }

        /// <summary>Gets the already acquired index values converted inside the legal lexical host.</summary>
        internal IEnumerable<string> TypedIndexArguments => IndexParameters.Select(parameter =>
            $"({TypeName(BindingWriteContract.InputType(parameter.Type, parameter), TransportSubstitutions)})index{parameter.Ordinal}");

        /// <summary>Gets the owner's exact metadata signature attribute, if erased.</summary>
        internal string OwnerAttribute { get; }

        /// <summary>Gets the value's exact metadata signature attribute, if erased.</summary>
        internal string ValueAttribute { get; }

        /// <summary>Gets the required owner modifier for value-type storage.</summary>
        internal string OwnerModifier { get; }

        /// <summary>Gets the generated declaring type parameter list.</summary>
        internal string DeclarationParameters { get; }

        /// <summary>Gets the current constructed bridge type argument list.</summary>
        internal string CallArguments { get; }

        /// <summary>Reads the definition storage type without replacing its declaring VARs.</summary>
        /// <param name="member">The selected actual storage member.</param>
        /// <param name="fallback">The exact nonstorage fallback type.</param>
        /// <returns>The original definition's CLR storage type.</returns>
        private static ITypeSymbol StorageDefinitionType(ISymbol member, ITypeSymbol fallback) => member switch
        {
            IPropertySymbol property => property.OriginalDefinition.Type,
            IFieldSymbol field => field.OriginalDefinition.Type,
            _ => fallback,
        };

        /// <summary>Separates actual indexed operands from the setter's final assigned value parameter.</summary>
        /// <param name="accessor">The selected original getter or setter.</param>
        /// <param name="definition">Whether to retain original declaring VAR types or current constructed transport types.</param>
        /// <returns>Only the exact accessor index parameters in their original ordinal order.</returns>
        private static ImmutableArray<IParameterSymbol> AccessorIndexParameters(IMethodSymbol? accessor, bool definition = true)
        {
            if (accessor is null)
            {
                return [];
            }

            var parameters = (definition ? accessor.OriginalDefinition : accessor).Parameters;
            return accessor.MethodKind == MethodKind.PropertySet ? parameters.RemoveAt(parameters.Length - 1) : parameters;
        }

        /// <summary>Collects declaring type parameters in CLR VAR order, including containing types.</summary>
        /// <param name="owner">The original declaring type definition.</param>
        /// <returns>All declaring parameters in metadata order.</returns>
        private static ImmutableArray<ITypeParameterSymbol> DeclaringParameters(INamedTypeSymbol owner)
        {
            var containing = owner.ContainingType is null ? ImmutableArray<ITypeParameterSymbol>.Empty : DeclaringParameters(owner.ContainingType);
            return containing.AddRange(owner.TypeParameters);
        }

        /// <summary>Formats a signature attribute only for an erased reference parameter.</summary>
        /// <param name="erased">Whether the parameter is erased.</param>
        /// <param name="type">The exact original reference signature.</param>
        /// <param name="parameters">The declaring VAR parameters.</param>
        /// <returns>The parameter attribute and trailing separator, if required.</returns>
        private static string ErasedAttribute(bool erased, ITypeSymbol type, ImmutableArray<ITypeParameterSymbol> parameters) =>
            erased ? $"[{TypeAttributeBody(type, parameters)}] " : string.Empty;

        /// <summary>Copies null input prohibitions without replacing Nullable value storage with its underlying type.</summary>
        /// <param name="symbol">The actual member or accessor index parameter.</param>
        /// <returns>The effective input flow attribute, if present.</returns>
        private static string InputAttributes(ISymbol symbol) => BindingWriteContract.ForbidsNull(symbol)
            ? "[global::System.Diagnostics.CodeAnalysis.DisallowNullAttribute] "
            : string.Empty;

        /// <summary>Copies declared read postconditions from the member and selected getter return.</summary>
        /// <param name="member">The exact readable field or property.</param>
        /// <param name="accessor">Its actual getter, if present.</param>
        /// <returns>The unique framework return attributes.</returns>
        private static string ReadPostconditions(ISymbol member, IMethodSymbol? accessor)
        {
            var source = new StringBuilder();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var attribute in member.GetAttributes())
            {
                AppendReadAttribute(source, names, attribute);
            }

            foreach (var attribute in accessor?.GetReturnTypeAttributes() ?? [])
            {
                AppendReadAttribute(source, names, attribute);
            }

            return source.ToString();
        }

        /// <summary>Formats only exact MaybeNull and NotNull output postconditions without duplicates.</summary>
        /// <param name="source">The attribute source builder.</param>
        /// <param name="names">The already emitted postcondition names.</param>
        /// <param name="attribute">The actual declared flow attribute.</param>
        private static void AppendReadAttribute(StringBuilder source, HashSet<string> names, AttributeData attribute)
        {
            var name = attribute.AttributeClass?.ToDisplayString();
            if (name is not ("System.Diagnostics.CodeAnalysis.MaybeNullAttribute" or "System.Diagnostics.CodeAnalysis.NotNullAttribute"))
            {
                return;
            }

            if (!names.Add(name))
            {
                return;
            }

            _ = source.Append("[return: global::").Append(name).AppendLine("]");
        }

        /// <summary>Finds a legal typed host only when generic owner erasure would hide the target from the full trimmer.</summary>
        /// <param name="compilation">The actual caller compilation.</param>
        /// <param name="owner">The exact declaring owner.</param>
        /// <returns>The constructed partial host, if required and available.</returns>
        private INamedTypeSymbol? FindLexicalHost(Compilation compilation, INamedTypeSymbol owner) => RequiresLexicalHost
            ? AccessBridgeLexicalHost.Find(compilation, owner)
            : null;

        /// <summary>Requires real typed intrinsic signatures for every erased declaring-generic position.</summary>
        /// <param name="compilation">The actual semantic compilation.</param>
        /// <returns>Whether a generic owner, return, value or index uses reference erasure.</returns>
        private bool RequiresTypedLexicalSignature(Compilation compilation) => !Parameters.IsEmpty
            && (OwnerErased || ValueErased || ConstructedIndexParameters.Any(parameter => !Nameable(compilation, parameter.Type)));

        /// <summary>Maps the host's original prefix and bridge suffix to their exact lexical generic positions.</summary>
        /// <param name="hostCount">The declaring VAR prefix already supplied by the host.</param>
        /// <returns>The exact symbol-identity substitution map.</returns>
        private Dictionary<ITypeParameterSymbol, string> CreateSubstitutions(int hostCount)
        {
            var substitutions = new Dictionary<ITypeParameterSymbol, string>(SymbolEqualityComparer.Default);
            for (var index = 0; index < Parameters.Length; index++)
            {
                var name = index < hostCount ? $"@{Parameters[index].Name}" : $"__T{index}";
                substitutions.Add(Parameters[index], name);
            }

            for (var index = 0; index < hostCount; index++)
            {
                if (Arguments[index] is ITypeParameterSymbol argument && !substitutions.ContainsKey(argument))
                {
                    substitutions.Add(argument, substitutions[Parameters[index]]);
                }
            }

            return substitutions;
        }

        /// <summary>Allows only concrete types or exact direct prefix parameters already bound by the lexical host.</summary>
        /// <param name="type">The current suffix argument.</param>
        /// <param name="hostCount">The prefix bound by the containing host.</param>
        /// <returns>Whether the suffix can close inside the host without inventing an external generic slot.</returns>
        private bool InHostScope(ITypeSymbol type, int hostCount)
        {
            if (type is ITypeParameterSymbol parameter)
            {
                for (var index = 0; index < hostCount; index++)
                {
                    if (SymbolEqualityComparer.Default.Equals(parameter, Parameters[index]) || SymbolEqualityComparer.Default.Equals(parameter, Arguments[index]))
                    {
                        return true;
                    }
                }

                return false;
            }

            return type switch
            {
                IArrayTypeSymbol array => InHostScope(array.ElementType, hostCount),
                INamedTypeSymbol named => (named.ContainingType is null || InHostScope(named.ContainingType, hostCount))
                    && named.TypeArguments.All(argument => InHostScope(argument, hostCount)),
                _ => true,
            };
        }

        /// <summary>Closes only the bridge suffix while retaining the host's exact lexical generic prefix.</summary>
        /// <param name="hostCount">The prefix supplied by the partial host.</param>
        /// <returns>The constructed wrapper signature substitutions.</returns>
        private Dictionary<ITypeParameterSymbol, string> CreateTransportSubstitutions(int hostCount)
        {
            var substitutions = new Dictionary<ITypeParameterSymbol, string>(Substitutions, SymbolEqualityComparer.Default);
            if (!ClosedLexicalWrapper)
            {
                return substitutions;
            }

            for (var index = hostCount; index < Parameters.Length; index++)
            {
                substitutions[Parameters[index]] = TypeName(Arguments[index], Substitutions);
            }

            return substitutions;
        }

        /// <summary>Formats a parameter's effective input type while preserving its original CLR identity.</summary>
        /// <param name="erased">Whether the parameter requires reference-only erasure.</param>
        /// <param name="type">Its effective input type.</param>
        /// <returns>The exact generated parameter type syntax.</returns>
        private string ParameterTypeSyntax(bool erased, ITypeSymbol type)
        {
            var erasedSyntax = type.NullableAnnotation == NullableAnnotation.Annotated ? $"{ErasedReferenceSyntax}?" : ErasedReferenceSyntax;
            return erased ? erasedSyntax : TypeName(type, TransportSubstitutions);
        }

        /// <summary>Formats one actual accessor index parameter without acquiring its runtime value.</summary>
        /// <param name="compilation">The exact caller compilation.</param>
        /// <param name="parameter">The original accessor parameter.</param>
        /// <param name="includeErasure">Whether this is an intrinsic parameter requiring its exact reference type attribute.</param>
        /// <returns>Its typed declaration in the original signature order.</returns>
        private string IndexDeclaration(Compilation compilation, IParameterSymbol parameter, bool includeErasure = true)
        {
            var erased = !Nameable(compilation, Accessor!.Parameters[parameter.Ordinal].Type);
            var input = BindingWriteContract.InputType(parameter.Type, parameter);
            var attribute = includeErasure ? ErasedAttribute(erased, parameter.Type, Parameters) : string.Empty;
            return $"{attribute}{InputAttributes(parameter)}{ParameterTypeSyntax(erased, input)} index{parameter.Ordinal}";
        }
    }
}
