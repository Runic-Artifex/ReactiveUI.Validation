// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
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

/// <summary>Stores typed selector code separately from observation and assignment.</summary>
[SuppressMessage("Performance", "PSH1100", Justification = "This is compiler-time planning and source formatting over bounded immutable operations, not emitted subscription execution.")]
internal sealed class Selector
{
    /// <summary>The parameter marker in semantic expression templates.</summary>
    internal const string RootMarker = "__runic_selector_root";

    /// <summary>The typed direct getter expression.</summary>
    private readonly string _getter;

    /// <summary>The null owner guards in evaluation order.</summary>
    private readonly ImmutableArray<string> _guards;

    /// <summary>The branch-preserving atomic read lowering.</summary>
    private SemanticReadPlan? _readPlan;

    /// <summary>The exact structural metadata plans.</summary>
    private ImmutableArray<SelectorPath> _pathPlans = [];

    /// <summary>Initializes a new instance of the <see cref="Selector"/> class.</summary>
    /// <param name="properties">The readable properties in a simple path.</param>
    internal Selector(ImmutableArray<IPropertySymbol> properties)
        : this(properties, BuildGetter(properties), BuildGuards(properties), BuildDependencies(properties), [string.Join(".", properties.Select(static property => property.Name))], null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="Selector"/> class.</summary>
    /// <param name="properties">The simple property path, if present.</param>
    /// <param name="getter">The exact semantic getter template.</param>
    /// <param name="guards">The missing-owner guards.</param>
    /// <param name="dependencies">The declared observation graph.</param>
    /// <param name="paths">The complete root-relative dependency paths.</param>
    /// <param name="access">The writable lens, when available.</param>
    internal Selector(
        ImmutableArray<IPropertySymbol> properties,
        string getter,
        ImmutableArray<string> guards,
        ImmutableArray<SelectorDependency> dependencies,
        ImmutableArray<string> paths,
        AccessPlan? access)
    {
        Properties = properties;
        _getter = getter;
        _guards = guards;
        Dependencies = dependencies;
        Paths = paths;
        Access = access;
    }

    /// <summary>Gets the exact metadata plans for independent path snapshots.</summary>
    internal ImmutableArray<SelectorPath> PathPlans => _pathPlans;

    /// <summary>Gets whether indexed branch metadata can be acquired without evaluating a selected leaf.</summary>
    internal bool SupportsIndependentMetadata => _readPlan?.HasCustomConditionalOperator != true && _pathPlans.SelectMany(static path => path.Indices)
        .All(static index => CanReadIndexMetadata(index.Operation));

    /// <summary>Gets whether metadata identifies one path for every present indexed branch.</summary>
    internal bool HasSingleMetadataPath => _pathPlans.Length == 1 || (_readPlan?.IsSingleIndexedConditional == true
        && _pathPlans.All(static path => !path.Indices.IsEmpty));

    /// <summary>Gets the exact selector result type.</summary>
    internal ITypeSymbol? ValueType { get; private set; }

    /// <summary>Gets whether generated member access needs the original lexical scope.</summary>
    internal bool RequiresLexicalAccess { get; private set; }

    /// <summary>Gets the legacy simple property path, empty for computations.</summary>
    internal ImmutableArray<IPropertySymbol> Properties { get; }

    /// <summary>Gets the separate typed observation dependency graph.</summary>
    internal ImmutableArray<SelectorDependency> Dependencies { get; }

    /// <summary>Gets every full ordinal root-relative dependency path.</summary>
    internal ImmutableArray<string> Paths { get; }

    /// <summary>Gets the first complete path for single-path consumers.</summary>
    internal string Path => Paths.IsEmpty ? string.Empty : Paths[0];

    /// <summary>Gets the writable lens, without inventing inverse conversions.</summary>
    internal AccessPlan? Access { get; }

    /// <summary>Gets whether this plan preserves a simple property chain.</summary>
    internal bool IsPropertyPath => !Properties.IsEmpty && Dependencies.All(static dependency => dependency.Member is IPropertySymbol { IsIndexer: false });

    /// <summary>Substitutes the exact generated parameter token.</summary>
    /// <param name="template">The semantic expression template.</param>
    /// <param name="root">The replacement parameter.</param>
    /// <returns>The bound expression.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string ReplaceRoot(string template, string root) => ReplaceIdentifier(template, RootMarker, root);

    /// <summary>Substitutes an exact expression identifier, preserving strings and member names.</summary>
    /// <param name="template">The typed expression template.</param>
    /// <param name="identifier">The generated identifier token.</param>
    /// <param name="value">The replacement expression.</param>
    /// <returns>The rebound typed expression.</returns>
    internal static string ReplaceIdentifier(string template, string identifier, string value)
    {
        var syntax = SyntaxFactory.ParseExpression(template);
        var nodes = syntax.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
            .Where(node => node.Identifier.ValueText == identifier && !(node.Parent is MemberAccessExpressionSyntax member && member.Name == node));
        var expression = SyntaxFactory.ParseExpression(value);
        return syntax.ReplaceNodes(nodes, (_, _) => expression is IdentifierNameSyntax ? expression : SyntaxFactory.ParenthesizedExpression(expression))
            .NormalizeWhitespace().ToFullString();
    }

    /// <summary>Rebinds a generated identifier inside an entire lambda body.</summary>
    /// <param name="body">The lambda expression.</param>
    /// <param name="identifier">The generated identifier.</param>
    /// <param name="value">The bound expression.</param>
    /// <returns>The rebound lambda.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string ReplaceIdentifierBody(string body, string identifier, string value) => ReplaceIdentifier(body, identifier, value);

    /// <summary>Attaches the atomic semantic read and access placement contract.</summary>
    /// <param name="readPlan">The branch-preserving read plan.</param>
    /// <param name="valueType">The exact selector value type.</param>
    /// <param name="lexicalAccess">Whether ordinary namespace access is impossible.</param>
    /// <param name="pathPlans">The pathPlans contract.</param>
    internal void SetSemanticRead(SemanticReadPlan readPlan, ITypeSymbol valueType, bool lexicalAccess, ImmutableArray<SelectorPath> pathPlans)
    {
        _readPlan = readPlan;
        ValueType = valueType;
        RequiresLexicalAccess = lexicalAccess;
        _pathPlans = pathPlans;
    }

    /// <summary>Formats the complete null-aware getter.</summary>
    /// <param name="root">The root variable.</param>
    /// <returns>Typed access syntax retaining all original conversions.</returns>
    internal string Getter(string root)
    {
        var value = ReplaceRoot(_getter, root);
        return _guards.IsEmpty ? value : $"({MissingGuard(root)} ? default : {value})";
    }

    /// <summary>Formats the direct getter, suitable after a missing-owner check.</summary>
    /// <param name="root">The root variable.</param>
    /// <returns>The semantic expression without a default-value wrapper.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal string DirectGetter(string root) => ReplaceRoot(_getter, root);

    /// <summary>Formats the missing-owner condition separately from a null leaf.</summary>
    /// <param name="root">The root variable.</param>
    /// <returns>A Boolean C# expression.</returns>
    internal string MissingGuard(string root) => _guards.IsEmpty ? "false" : string.Join(" || ", _guards.Select(guard => ReplaceRoot(guard, root)));

    /// <summary>Formats a null-aware notification owner getter.</summary>
    /// <param name="root">The root variable.</param>
    /// <param name="segment">The dependency index.</param>
    /// <returns>The typed notification owner expression.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal string OwnerGetter(string root, int segment) => Dependencies[segment].OwnerGetter(root);

    /// <summary>Emits the atomic typed read delegate with complete structural metadata.</summary>
    /// <param name="root">The current source.</param>
    /// <param name="namespaceRoot">The matching flavor namespace.</param>
    /// <returns>A Func returning the exact ValidationRead value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal string EmitRead(string root, string namespaceRoot) => _readPlan!.EmitRead(root, namespaceRoot, this);

    /// <summary>Emits a bound typed observation plan.</summary>
    /// <param name="root">The current source.</param>
    /// <param name="namespaceRoot">The matching flavor namespace.</param>
    /// <param name="optionsExpression">Optional caller-selected identity and null policy.</param>
    /// <returns>The bound ValidationAccessPlan expression.</returns>
    internal string EmitAccessPlan(string root, string namespaceRoot, string? optionsExpression = null)
    {
        var type = GeneratorHelpers.TypeName(ValueType!);
        var options = optionsExpression ?? $"global::{namespaceRoot}.Capabilities.ValidationObservationOptions<{type}>.Default";
        var plan = $"global::{namespaceRoot}.Capabilities.ValidationAccessPlan<{type}>";
        var metadata = SupportsIndependentMetadata
            ? $", {EmitPathsGetter(root, namespaceRoot, true)}, new global::{namespaceRoot}.Capabilities.ValidationDependency[] {{ {EmitMetadataDependencies(root, namespaceRoot, true)} }}"
            : string.Empty;
        return $$"""
            ((global::System.Func<{{plan}}>)(() => {
                var __runic_options = {{options}};
                return new {{plan}}(() => {
                    {{EmitSnapshotDeclarations()}}
                    return new {{plan}}(
                        {{_readPlan!.EmitRead(root, namespaceRoot, this, true)}},
                        new global::{{namespaceRoot}}.Capabilities.ValidationDependency[] { {{EmitDependencies(root, namespaceRoot, true)}} },
                        __runic_options{{metadata}});
                }, __runic_options);
            }))()
            """;
    }

    /// <summary>Emits a metadata-only bound plan with no selected leaf reads.</summary>
    /// <param name="root">The current source.</param>
    /// <param name="namespaceRoot">The matching runtime namespace.</param>
    /// <returns>The bound typed path access plan.</returns>
    internal string EmitMetadataPlan(string root, string namespaceRoot)
    {
        var type = $"global::{namespaceRoot}.Capabilities.ValidationPath";
        var plan = $"global::{namespaceRoot}.Capabilities.ValidationAccessPlan<{type}>";
        var options = $"global::{namespaceRoot}.Capabilities.ValidationObservationOptions<{type}>.Default";
        return $$"""
            new {{plan}}(() => {
                {{EmitSnapshotDeclarations()}}
                return new {{plan}}(
                    {{_readPlan!.EmitMetadataRead(root, namespaceRoot, this, true)}},
                    new global::{{namespaceRoot}}.Capabilities.ValidationDependency[] { {{EmitMetadataDependencies(root, namespaceRoot, true)}} },
                    {{options}},
                    {{EmitPathsGetter(root, namespaceRoot, true)}});
            }, {{options}})
            """;
    }

    /// <summary>Emits independently null-aware typed notification dependencies.</summary>
    /// <param name="root">The current source.</param>
    /// <param name="namespaceRoot">The matching flavor namespace.</param>
    /// <param name="snapshot">Whether to use acquired receiver caches.</param>
    /// <returns>The comma-separated dependency expressions.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal string EmitDependencies(string root, string namespaceRoot, bool snapshot = false) => string.Join(", ", Dependencies.Select(dependency => dependency.Emit(root, namespaceRoot, snapshot)));

    /// <summary>Emits dependencies of path operands without acquiring selected value owners.</summary>
    /// <param name="root">The current source.</param>
    /// <param name="namespaceRoot">The matching flavor namespace.</param>
    /// <param name="snapshot">Whether to use per-observation receiver caches.</param>
    /// <returns>The comma-separated structural dependency expressions.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal string EmitMetadataDependencies(string root, string namespaceRoot, bool snapshot = false) => string.Join(", ", Dependencies
        .Where(static dependency => dependency.IsMetadata).Select(dependency => dependency.Emit(root, namespaceRoot, snapshot)));

    /// <summary>Formats every complete path in the selector.</summary>
    /// <param name="namespaceRoot">The matching runtime namespace.</param>
    /// <param name="values">The values contract.</param>
    /// <param name="acquired">The guards proving index operands were actually evaluated.</param>
    /// <param name="active">The guards proving a selected value path executed in the active branch.</param>
    /// <returns>The typed path array.</returns>
    internal string EmitPaths(
        string namespaceRoot,
        IReadOnlyDictionary<IOperation, string>? values = null,
        IReadOnlyDictionary<IOperation, string>? acquired = null,
        IReadOnlyDictionary<SelectorPath, string>? active = null)
    {
        var type = $"global::{namespaceRoot}.Capabilities.ValidationPath";
        var paths = _pathPlans.Where(path => path.Indices.IsEmpty || (values is not null && path.Indices.All(index => values.ContainsKey(index.Operation))))
            .Select(path =>
            {
                var expression = path.Emit(namespaceRoot, path.Indices.Select(index => values![index.Operation]).ToImmutableArray());
                var guards = path.Indices.IsEmpty || acquired is null ? [] : path.Indices.Select(index => acquired[index.Operation]).ToList();
                if (active is not null)
                {
                    guards.Add(active[path]);
                }

                return guards.Count == 0 ? expression : $"({string.Join(" && ", guards)} ? {expression} : null)";
            });
        return acquired is null && active is null
            ? $"new {type}[] {{ {string.Join(", ", paths)} }}"
            : $"global::System.Linq.Enumerable.ToArray(global::System.Linq.Enumerable.OfType<{type}>(new {type}?[] {{ {string.Join(", ", paths)} }}))";
    }

    /// <summary>Emits metadata without reading the selected final property or field.</summary>
    /// <param name="root">The current root.</param>
    /// <param name="namespaceRoot">The runtime namespace.</param>
    /// <returns>The typed metadata read delegate.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal string EmitMetadataRead(string root, string namespaceRoot) => _readPlan!.EmitMetadataRead(root, namespaceRoot, this);

    /// <summary>Declares reference notification slots inside the cold observation factory.</summary>
    /// <returns>The typed cache declarations.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal string EmitSnapshotDeclarations() => string.Join("\n", Dependencies.Where(static dependency => dependency.SnapshotOrdinal >= 0)
        .Select(static dependency => $"global::System.ComponentModel.INotifyPropertyChanged? {dependency.PropertySnapshot} = null;"
            + (dependency.IsCollection ? $" global::System.Collections.Specialized.INotifyCollectionChanged? {dependency.CollectionSnapshot} = null;" : string.Empty)));

    /// <summary>Clears receiver slots before one independent read.</summary>
    /// <returns>The ordered reference-cache resets.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal string EmitSnapshotResets() => string.Join("\n", Dependencies.Where(static dependency => dependency.SnapshotOrdinal >= 0)
        .Select(static dependency => $"{dependency.PropertySnapshot} = null;" + (dependency.IsCollection ? $" {dependency.CollectionSnapshot} = null;" : string.Empty))) + "\n";

    /// <summary>Acquires one target index operand with its exact observation receivers.</summary>
    /// <param name="operand">The selected key operation.</param>
    /// <param name="root">The stable source.</param>
    /// <param name="namespaceRoot">The runtime flavor.</param>
    /// <returns>The typed atomic operand read.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal string EmitOperandRead(IOperation operand, string root, string namespaceRoot) => _readPlan!.EmitOperand(operand, root, namespaceRoot, this);

    /// <summary>Checks whether metadata activation depends on the selected coalesce leaf.</summary>
    /// <param name="operation">The structural index operation.</param>
    /// <returns>Whether an independent metadata callback can preserve evaluation order.</returns>
    private static bool CanReadIndexMetadata(IOperation operation)
    {
        for (var child = operation; child.Parent is { } parent; child = parent)
        {
            if (parent is ICoalesceOperation coalesce && ReferenceEquals(child, coalesce.WhenNull))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Creates the legacy property getter.</summary>
    /// <param name="properties">The property chain.</param>
    /// <returns>The direct expression.</returns>
    private static string BuildGetter(ImmutableArray<IPropertySymbol> properties) => RootMarker + string.Concat(properties.Select(static property => $".@{property.Name}"));

    /// <summary>Creates the legacy null-owner guards.</summary>
    /// <param name="properties">The property chain.</param>
    /// <returns>The ordered guards.</returns>
    private static ImmutableArray<string> BuildGuards(ImmutableArray<IPropertySymbol> properties)
    {
        var result = ImmutableArray.CreateBuilder<string>();
        for (var index = 1; index < properties.Length; index++)
        {
            if (properties[index - 1].Type.IsReferenceType)
            {
                result.Add($"{BuildGetter(properties.Take(index).ToImmutableArray())} is null");
            }
        }

        return result.ToImmutable();
    }

    /// <summary>Creates the legacy dependency graph.</summary>
    /// <param name="properties">The property chain.</param>
    /// <returns>The property dependencies.</returns>
    private static ImmutableArray<SelectorDependency> BuildDependencies(ImmutableArray<IPropertySymbol> properties)
    {
        var result = ImmutableArray.CreateBuilder<SelectorDependency>();
        for (var index = 0; index < properties.Length; index++)
        {
            var owners = properties.Take(index).ToImmutableArray();
            result.Add(new(properties[index], BuildGetter(owners), BuildGuards(owners), properties[index].Name, properties[index].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), false));
        }

        return result.ToImmutable();
    }

    /// <summary>Emits the explicit delegate invocation used by metadata-only observation.</summary>
    /// <param name="root">The current source.</param>
    /// <param name="namespaceRoot">The runtime namespace.</param>
    /// <param name="snapshot">Whether to cache metadata operand receivers.</param>
    /// <returns>The leaf-free metadata factory.</returns>
    private string EmitPathsGetter(string root, string namespaceRoot, bool snapshot = false)
    {
        var path = $"global::{namespaceRoot}.Capabilities.ValidationPath";
        var read = $"global::{namespaceRoot}.Capabilities.ValidationRead<{path}>";
        return $"() => ((global::System.Func<{read}>)({_readPlan!.EmitMetadataRead(root, namespaceRoot, this, snapshot)}))().Paths";
    }
}
