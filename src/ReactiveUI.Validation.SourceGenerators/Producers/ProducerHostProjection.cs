// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ReactiveUI.Validation.SourceGenerators.Producers;

/// <summary>Describes real nongeneric WinForms host and declared Binding OAPH contracts.</summary>
internal static class ProducerHostProjection
{
    /// <summary>The legacy implementation namespace.</summary>
    private const string LegacyRoot = "ReactiveUI";

    /// <summary>The System.Reactive view API namespace.</summary>
    private const string ReactiveBindingRoot = "ReactiveUI.Binding.Reactive";

    /// <summary>The generated public setter.</summary>
    private const string Setter = "set { }";

    /// <summary>Registers the pinned host and helper declarations.</summary>
    /// <param name="context">The generator pipeline context.</param>
    /// <returns>The finite host and helper declarations.</returns>
    internal static IncrementalValueProvider<ImmutableArray<ProducerDeclaration>> Register(in IncrementalGeneratorInitializationContext context)
    {
        var view = context.SyntaxProvider.ForAttributeWithMetadataName(
            "ReactiveUI.SourceGenerators.WinForms.ViewModelControlHostAttribute",
            static (node, _) => node is ClassDeclarationSyntax,
            static (attribute, cancellation) => Host(attribute, false, cancellation)).Collect();
        var routed = context.SyntaxProvider.ForAttributeWithMetadataName(
            "ReactiveUI.SourceGenerators.WinForms.RoutedControlHostAttribute",
            static (node, _) => node is ClassDeclarationSyntax,
            static (attribute, cancellation) => Host(attribute, true, cancellation)).Collect();
        var lean = context.SyntaxProvider.ForAttributeWithMetadataName(
            "ReactiveUI.Binding.ObservableAsPropertyAttribute",
            static (node, _) => node is PropertyDeclarationSyntax,
            Oaph).Where(static item => item is not null).Select(static (item, _) => item!).Collect();
        var reactive = context.SyntaxProvider.ForAttributeWithMetadataName(
            "ReactiveUI.Binding.Reactive.ObservableAsPropertyAttribute",
            static (node, _) => node is PropertyDeclarationSyntax,
            Oaph).Where(static item => item is not null).Select(static (item, _) => item!).Collect();
        var leanInitial = context.SyntaxProvider.ForAttributeWithMetadataName(
            "ReactiveUI.Binding.ObservableAsPropertyAttribute",
            static (node, _) => node is PropertyDeclarationSyntax,
            OaphInitial).Where(static item => item is not null).Select(static (item, _) => item!).Collect();
        var reactiveInitial = context.SyntaxProvider.ForAttributeWithMetadataName(
            "ReactiveUI.Binding.Reactive.ObservableAsPropertyAttribute",
            static (node, _) => node is PropertyDeclarationSyntax,
            OaphInitial).Where(static item => item is not null).Select(static (item, _) => item!).Collect();
        var hosts = view.Combine(routed).Combine(lean).Combine(reactive).Select(static (data, _) =>
        {
            var builder = ImmutableArray.CreateBuilder<ProducerDeclaration>();
            foreach (var group in data.Left.Left.Left)
            {
                builder.AddRange(group);
            }

            foreach (var group in data.Left.Left.Right)
            {
                builder.AddRange(group);
            }

            builder.AddRange(data.Left.Right);
            builder.AddRange(data.Right);
            return builder.ToImmutable();
        });
        return hosts.Combine(leanInitial).Combine(reactiveInitial).Select(static (data, _) => data.Left.Left.AddRange(data.Left.Right).AddRange(data.Right));
    }

    /// <summary>Predicts host contracts without inventing a typed IViewFor implementation.</summary>
    /// <param name="context">The producer attribute context.</param>
    /// <param name="routed">Whether this is a routed host.</param>
    /// <param name="cancellation">The cancellation token.</param>
    /// <returns>The exact host declarations.</returns>
    private static ImmutableArray<ProducerDeclaration> Host(GeneratorAttributeSyntaxContext context, bool routed, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (context.TargetSymbol is not INamedTypeSymbol owner || !ProducerProjection.CanAugment(owner)
            || context.Attributes[0].ConstructorArguments.IsEmpty || context.Attributes[0].ConstructorArguments[0].Value is not string baseName)
        {
            return [];
        }

        var root = context.SemanticModel.Compilation.GetTypeByMetadataName("ReactiveUI.Reactive.ReactiveCommand") is null ? LegacyRoot : "ReactiveUI.Reactive";
        var viewRoot = ViewNamespace(context.SemanticModel.Compilation);
        var builder = ImmutableArray.CreateBuilder<ProducerDeclaration>();
        builder.Add(new(owner, string.Empty, string.Empty, "global::ReactiveUI.IReactiveObject", baseName, location: context.TargetNode.GetLocation()));
        if (!routed)
        {
            builder.Add(new(owner, string.Empty, string.Empty, $"global::{viewRoot}.IViewFor", location: context.TargetNode.GetLocation()));
        }

        Add("DefaultContent", "global::System.Windows.Forms.Control?", Setter);
        Add("ViewContractObservable", "global::System.IObservable<string>?", Setter);
        Add("ViewLocator", $"global::{viewRoot}.IViewLocator?", Setter);
        if (routed)
        {
            Add("Router", $"global::{root}.RoutingState?", Setter);
        }
        else
        {
            Add("ViewModel", "object?", Setter);
            Add("Content", "object?", "protected set { }");
            Add("CacheViews", "bool", Setter);
            Add("CurrentView", "global::System.Windows.Forms.Control?", string.Empty);
            builder.Add(new(owner, "DefaultCacheViewsEnabled", "public static bool DefaultCacheViewsEnabled { get { throw null; } set { } }", location: context.TargetNode.GetLocation()));
        }

        return builder.ToImmutable();

        void Add(string name, string type, string setter) => builder.Add(new(
            owner,
            name,
            $"public {type} {name} {{ get {{ throw null; }} {setter} }}",
            location: context.TargetNode.GetLocation()));
    }

    /// <summary>Matches the producer's independent view API selection.</summary>
    /// <param name="compilation">The compiler input.</param>
    /// <returns>The selected view namespace.</returns>
    private static string ViewNamespace(Compilation compilation)
    {
        if (compilation.GetTypeByMetadataName("ReactiveUI.IViewFor`1") is not null)
        {
            return LegacyRoot;
        }

        var hasBinding = compilation.GetTypeByMetadataName("ReactiveUI.Binding.IViewFor`1") is not null;
        if (compilation.GetTypeByMetadataName("ReactiveUI.Binding.Reactive.IViewFor`1") is not null
            && (!hasBinding || compilation.GetTypeByMetadataName("ReactiveUI.Reactive.ReactiveCommand") is not null))
        {
            return ReactiveBindingRoot;
        }

        return hasBinding ? "ReactiveUI.Binding" : LegacyRoot;
    }

    /// <summary>Predicts Binding's private initial-value field when it stores a non-string expression.</summary>
    /// <param name="context">The OAPH attribute context.</param>
    /// <param name="cancellation">The cancellation token.</param>
    /// <returns>The initial-value field, or null when not generated.</returns>
    private static ProducerDeclaration? OaphInitial(GeneratorAttributeSyntaxContext context, CancellationToken cancellation)
    {
        if (Oaph(context, cancellation) is null || context.TargetSymbol is not IPropertySymbol property
            || property.Type.SpecialType == SpecialType.System_String
            || !context.Attributes[0].NamedArguments.Any(static argument => argument.Key == "InitialValue" && argument.Value.Value is string))
        {
            return null;
        }

        var name = $"_{char.ToLowerInvariant(property.Name[0])}{property.Name.Substring(1)}";
        return new(
            property.ContainingType,
            name,
            $"private readonly {ProducerProjection.TypeName(property.Type)} {name};",
            profile: "ReactiveUI.Binding.SourceGenerators/9.1.0",
            location: property.Locations[0]);
    }

    /// <summary>Leaves declared partial properties intact and predicts only Binding's helper field.</summary>
    /// <param name="context">The OAPH attribute context.</param>
    /// <param name="cancellation">The cancellation token.</param>
    /// <returns>The helper field, or null for an invalid declaration.</returns>
    private static ProducerDeclaration? Oaph(GeneratorAttributeSyntaxContext context, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (context.TargetSymbol is not IPropertySymbol { IsStatic: false } property
            || !ProducerProjection.CanAugment(property.ContainingType) || !IsOaphDeclaration(context.TargetNode))
        {
            return null;
        }

        var (useProtected, readOnly) = OaphOptions(context.Attributes[0]);
        var name = $"_{char.ToLowerInvariant(property.Name[0])}{property.Name.Substring(1)}Helper";
        var helperRoot = context.Attributes[0].AttributeClass?.ContainingNamespace.ToDisplayString() == ReactiveBindingRoot ? ReactiveBindingRoot : "ReactiveUI.Binding";
        var visibility = useProtected ? "protected" : "private";
        var modifier = readOnly ? " readonly" : string.Empty;
        var type = ProducerProjection.TypeName(property.Type);
        return new(
            property.ContainingType,
            name,
            $"{visibility}{modifier} global::{helperRoot}.ObservableAsPropertyHelper<{type}>? {name};",
            profile: "ReactiveUI.Binding.SourceGenerators/9.1.0",
            location: property.Locations[0]);
    }

    /// <summary>Reads the actual helper field access and storage options.</summary>
    /// <param name="attribute">The Binding OAPH attribute.</param>
    /// <returns>The protected-access and readonly-storage flags.</returns>
    private static (bool Protected, bool ReadOnly) OaphOptions(AttributeData attribute)
    {
        var useProtected = false;
        var readOnly = false;
        foreach (var argument in attribute.NamedArguments)
        {
            useProtected |= argument.Key == "UseProtected" && argument.Value.Value is true;
            readOnly |= argument.Key == "ReadOnly" && argument.Value.Value is true;
        }

        return (useProtected, readOnly);
    }

    /// <summary>Checks the declared partial get-only property contract.</summary>
    /// <param name="node">The original declaration.</param>
    /// <returns>Whether Binding can implement the declaration.</returns>
    private static bool IsOaphDeclaration(SyntaxNode node) =>
        node is PropertyDeclarationSyntax { AccessorList.Accessors.Count: 1 } syntax
        && syntax.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)
        && syntax.AccessorList.Accessors[0].Body is null && syntax.AccessorList.Accessors[0].ExpressionBody is null;
}
