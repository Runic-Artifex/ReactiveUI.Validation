// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ReactiveUI.Validation.SourceGenerators.Producers;

/// <summary>Builds never-emitted semantic declarations for the pinned producer contracts.</summary>
internal sealed class ProducerProjection
{
    /// <summary>Initializes a new instance of the <see cref="ProducerProjection"/> class.</summary>
    /// <param name="compilation">The private semantic compilation.</param>
    /// <param name="originalCompilation">The unchanged compiler input.</param>
    /// <param name="declarations">The exact predicted declarations.</param>
    private ProducerProjection(Compilation compilation, Compilation originalCompilation, ImmutableArray<ProducerDeclaration> declarations)
    {
        Compilation = compilation;
        OriginalCompilation = originalCompilation;
        Declarations = declarations;
        var keys = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        foreach (var declaration in declarations)
        {
            if (declaration.Name.Length > 0)
            {
                _ = keys.Add($"{MetadataName(declaration.Owner)}|{declaration.Name}");
            }
        }

        MemberKeys = keys.ToImmutable();
    }

    /// <summary>Gets the private compilation, retaining every original tree.</summary>
    internal Compilation Compilation { get; }

    /// <summary>Gets the compiler input before any private predictions.</summary>
    internal Compilation OriginalCompilation { get; }

    /// <summary>Gets the finite declaration identities with mandatory final verification.</summary>
    internal IReadOnlyCollection<string> MemberKeys { get; }

    /// <summary>Gets the producer contracts to validate after all generators finish.</summary>
    internal ImmutableArray<ProducerDeclaration> Declarations { get; }

    /// <summary>Registers incrementally extracted declarations and additional files.</summary>
    /// <param name="context">The generator pipeline context.</param>
    /// <returns>The private semantic compilation and finite contracts.</returns>
    internal static IncrementalValueProvider<ProducerProjection> Register(in IncrementalGeneratorInitializationContext context)
    {
        var reactive = Collect(in context, "ReactiveUI.SourceGenerators.ReactiveAttribute", ProducerMemberExtractor.ExtractReactive);
        var collections = Collect(in context, "ReactiveUI.SourceGenerators.ReactiveCollectionAttribute", ProducerMemberExtractor.ExtractReactiveCollection);
        var derived = Collect(in context, "ReactiveUI.SourceGenerators.BindableDerivedListAttribute", ProducerMemberExtractor.ExtractBindableDerivedList);
        var commands = Collect(in context, "ReactiveUI.SourceGenerators.ReactiveCommandAttribute", ProducerMemberExtractor.ExtractReactiveCommand);
        var objects = Collect(in context, "ReactiveUI.SourceGenerators.IReactiveObjectAttribute", ProducerMemberExtractor.ExtractIReactiveObject);
        var hosts = ProducerHostProjection.Register(in context);
        var xaml = ProducerXamlProjection.Register(in context);
        var declarations = reactive.Combine(collections).Combine(derived).Combine(commands).Combine(objects).Combine(hosts).Combine(xaml)
            .Select(static (data, _) => data.Left.Left.Left.Left.Left.Left
                .AddRange(data.Left.Left.Left.Left.Left.Right)
                .AddRange(data.Left.Left.Left.Left.Right)
                .AddRange(data.Left.Left.Left.Right)
                .AddRange(data.Left.Left.Right)
                .AddRange(data.Left.Right)
                .AddRange(data.Right));
        var tree = declarations.Select(WriteDeclarations)
            .Combine(context.ParseOptionsProvider)
            .Select(static (data, cancellation) => data.Left.Length == 0 ? null : CSharpSyntaxTree.ParseText(
                data.Left,
                data.Right as CSharpParseOptions,
                "Validation.PrivateProducerProjection.cs",
                cancellationToken: cancellation));
        return context.CompilationProvider.Combine(tree).Combine(declarations).Combine(context.AnalyzerConfigOptionsProvider)
            .Select(static (data, cancellation) =>
            {
                cancellation.ThrowIfCancellationRequested();
                var disabled = data.Right.GlobalOptions.TryGetValue("build_property.ReactiveUIValidationProducerProjectionEnabled", out var enabled)
                    && string.Equals(enabled, "false", StringComparison.OrdinalIgnoreCase);
                var original = data.Left.Left.Left;
                return new ProducerProjection(
                    disabled || data.Left.Left.Right is null ? original : original.AddSyntaxTrees(data.Left.Left.Right),
                    original,
                    disabled ? [] : data.Left.Right);
            });
    }

    /// <summary>Preserves reference nullability when rendering producer type contracts.</summary>
    /// <param name="type">The semantic type.</param>
    /// <returns>The fully qualified annotated type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string TypeName(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat
        .WithMiscellaneousOptions(SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier));

    /// <summary>Checks that the producer can reopen the complete containing chain.</summary>
    /// <param name="owner">The declaring type.</param>
    /// <returns>Whether all containing declarations are partial.</returns>
    internal static bool CanAugment(INamedTypeSymbol owner)
    {
        for (var current = owner; current is not null; current = current.ContainingType)
        {
            if (!IsPartial(current))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns the full metadata identity, including containing generic positions.</summary>
    /// <param name="owner">The declaring type.</param>
    /// <returns>The unique metadata name.</returns>
    internal static string MetadataName(INamedTypeSymbol owner)
    {
        var chain = new Stack<string>();
        for (var current = owner; current is not null; current = current.ContainingType)
        {
            chain.Push(current.MetadataName);
        }

        var prefix = owner.ContainingNamespace.IsGlobalNamespace ? string.Empty : $"{owner.ContainingNamespace.ToDisplayString()}.";
        return $"{prefix}{string.Join("+", chain)}";
    }

    /// <summary>Groups declaration identities in stable metadata and member order.</summary>
    /// <param name="declarations">The finite producer records.</param>
    /// <returns>The deterministically ordered declaring groups.</returns>
    internal static SortedDictionary<string, List<ProducerDeclaration>> GroupDeclarations(ImmutableArray<ProducerDeclaration> declarations)
    {
        var groups = new SortedDictionary<string, List<ProducerDeclaration>>(StringComparer.Ordinal);
        foreach (var declaration in declarations)
        {
            var key = MetadataName(declaration.Owner);
            if (!groups.TryGetValue(key, out var members))
            {
                members = [];
                groups.Add(key, members);
            }

            members.Add(declaration);
        }

        foreach (var group in groups.Values)
        {
            group.Sort(static (left, right) =>
            {
                var order = StringComparer.Ordinal.Compare(left.Name, right.Name);
                return order != 0 ? order : StringComparer.Ordinal.Compare(left.InterfaceName, right.InterfaceName);
            });
        }

        return groups;
    }

    /// <summary>Checks all source declaration parts without relying on peer output.</summary>
    /// <param name="type">The original declaring type.</param>
    /// <returns>Whether every source part is partial.</returns>
    private static bool IsPartial(INamedTypeSymbol type)
    {
        if (type.DeclaringSyntaxReferences.IsEmpty)
        {
            return false;
        }

        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax() is not TypeDeclarationSyntax declaration || !HasPartial(declaration))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Checks the original partial modifier.</summary>
    /// <param name="declaration">The original source declaration.</param>
    /// <returns>Whether the declaration is partial.</returns>
    private static bool HasPartial(TypeDeclarationSyntax declaration)
    {
        foreach (var modifier in declaration.Modifiers)
        {
            if (modifier.IsKind(SyntaxKind.PartialKeyword))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Writes private partial declarations without copying executable code.</summary>
    /// <param name="declarations">The finite producer declarations.</param>
    /// <param name="cancellation">The generator cancellation token.</param>
    /// <returns>The never-emitted declaration source.</returns>
    private static string WriteDeclarations(ImmutableArray<ProducerDeclaration> declarations, CancellationToken cancellation)
    {
        if (declarations.IsEmpty)
        {
            return string.Empty;
        }

        var output = new StringBuilder("#nullable enable\n");
        foreach (var group in GroupDeclarations(declarations).Values)
        {
            cancellation.ThrowIfCancellationRequested();
            var owner = group[0].Owner;
            if (!CanAugment(owner))
            {
                continue;
            }

            WriteGroup(output, owner, group, cancellation);
        }

        return output.ToString();
    }

    /// <summary>Writes one declaring chain and its finite generated members.</summary>
    /// <param name="output">The declaration buffer.</param>
    /// <param name="owner">The original declaring owner.</param>
    /// <param name="group">Its sorted member predictions.</param>
    /// <param name="cancellation">The cancellation token.</param>
    private static void WriteGroup(StringBuilder output, INamedTypeSymbol owner, List<ProducerDeclaration> group, CancellationToken cancellation)
    {
        var nested = new Stack<INamedTypeSymbol>();
        for (var current = owner; current is not null; current = current.ContainingType)
        {
            nested.Push(current);
        }

        var hasNamespace = !owner.ContainingNamespace.IsGlobalNamespace;
        if (hasNamespace)
        {
            _ = output.Append("namespace ").Append(owner.ContainingNamespace.ToDisplayString()).AppendLine(" {");
        }

        foreach (var type in nested)
        {
            WriteTypeHeader(output, type, cancellation);
            if (SymbolEqualityComparer.Default.Equals(type, owner))
            {
                WriteBases(output, group);
            }

            _ = output.AppendLine(" {");
        }

        foreach (var item in group)
        {
            _ = output.AppendLine(item.Declaration);
        }

        _ = output.AppendLine(new('}', nested.Count + (hasNamespace ? 1 : 0)));
    }

    /// <summary>Preserves the original partial type kind and generic positions.</summary>
    /// <param name="output">The declaration buffer.</param>
    /// <param name="type">The original partial type.</param>
    /// <param name="cancellation">The cancellation token.</param>
    private static void WriteTypeHeader(StringBuilder output, INamedTypeSymbol type, CancellationToken cancellation)
    {
        var declaration = (TypeDeclarationSyntax)type.DeclaringSyntaxReferences[0].GetSyntax(cancellation);
        _ = output.Append("partial ");
        if (declaration is RecordDeclarationSyntax record)
        {
            _ = output.Append(record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) ? "record struct " : "record class ");
        }
        else
        {
            _ = output.Append(declaration.Keyword.ValueText).Append(' ');
        }

        _ = output.Append('@').Append(type.Name);
        if (type.Arity == 0)
        {
            return;
        }

        var parameters = new List<string>();
        foreach (var parameter in type.TypeParameters)
        {
            parameters.Add($"@{parameter.Name}");
        }

        _ = output.Append('<').Append(string.Join(",", parameters)).Append('>');
    }

    /// <summary>Writes the producer base class before its implemented interfaces.</summary>
    /// <param name="output">The declaration buffer.</param>
    /// <param name="group">The declaring predictions.</param>
    private static void WriteBases(StringBuilder output, List<ProducerDeclaration> group)
    {
        var bases = new SortedSet<string>(StringComparer.Ordinal);
        var interfaces = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var declaration in group)
        {
            if (declaration.BaseType is { } baseType)
            {
                _ = bases.Add(baseType);
            }

            if (declaration.InterfaceName is { } implemented)
            {
                _ = interfaces.Add(implemented);
            }
        }

        var types = new List<string>(bases);
        types.AddRange(interfaces);
        if (types.Count > 0)
        {
            _ = output.Append(" : ").Append(string.Join(",", types));
        }
    }

    /// <summary>Collects one actual attributed source-member cohort.</summary>
    /// <param name="context">The generator pipeline.</param>
    /// <param name="metadataName">The producer attribute metadata name.</param>
    /// <param name="extract">The exact declaration extractor.</param>
    /// <returns>The incrementally collected declarations.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IncrementalValueProvider<ImmutableArray<ProducerDeclaration>> Collect(
        in IncrementalGeneratorInitializationContext context,
        string metadataName,
        Func<GeneratorAttributeSyntaxContext, CancellationToken, ProducerDeclaration?> extract) =>
        context.SyntaxProvider.ForAttributeWithMetadataName(
            metadataName,
            static (node, _) => node is VariableDeclaratorSyntax or MethodDeclarationSyntax or TypeDeclarationSyntax,
            extract).Where(static item => item is not null).Select(static (item, _) => item!).Collect();
}
