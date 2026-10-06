// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators;

/// <summary>One typed owner, operation identity and notification contract.</summary>
internal sealed class SelectorDependency
{
    /// <summary>The missing-owner guards.</summary>
    private ImmutableArray<string> _guards;

    /// <summary>The direct owner expression.</summary>
    private string _owner;

    /// <summary>Initializes a new instance of the <see cref="SelectorDependency"/> class.</summary>
    /// <param name="member">The exact member symbol.</param>
    /// <param name="owner">The typed owner template.</param>
    /// <param name="guards">The owner guards.</param>
    /// <param name="name">The event member name.</param>
    /// <param name="identity">The structural operation identity.</param>
    /// <param name="collection">Whether collection changes invalidate this access.</param>
    /// <param name="metadata">Whether this dependency changes structural path operands.</param>
    internal SelectorDependency(ISymbol member, string owner, ImmutableArray<string> guards, string name, string identity, bool collection, bool metadata = false)
    {
        Member = member;
        _owner = owner;
        _guards = guards;
        Name = name;
        Identity = identity;
        IsCollection = collection;
        IsMetadata = metadata;
    }

    /// <summary>Gets the exact property or field being read.</summary>
    internal ISymbol Member { get; }

    /// <summary>Gets the notification name, including Item[] for indexers.</summary>
    internal string Name { get; }

    /// <summary>Gets the unambiguous member/conversion/index identity.</summary>
    internal string Identity { get; }

    /// <summary>Gets whether collection events invalidate an index access.</summary>
    internal bool IsCollection { get; }

    /// <summary>Gets whether changes refresh structural metadata without reading selected storage.</summary>
    internal bool IsMetadata { get; }

    /// <summary>Gets or sets the reference owner operation acquired by the atomic read.</summary>
    internal IOperation? SnapshotOperation { get; set; }

    /// <summary>Gets or sets the per-observation reference slot ordinal.</summary>
    internal int SnapshotOrdinal { get; set; } = -1;

    /// <summary>Gets the cached typed property-change receiver variable.</summary>
    internal string PropertySnapshot => $"__runic_dependency{SnapshotOrdinal}_property";

    /// <summary>Gets the cached typed collection-change receiver variable.</summary>
    internal string CollectionSnapshot => $"__runic_dependency{SnapshotOrdinal}_collection";

    /// <summary>Gets whether a field requires an explicit event or invalidation policy.</summary>
    internal bool IsField => Member is IFieldSymbol;

    /// <summary>Rebinds the source receiver after exact getter bridges have been selected.</summary>
    /// <param name="owner">The same semantic owner with legal selected access invocations.</param>
    /// <param name="guards">The same ordered source guards with legal getter access.</param>
    internal void RebindOwner(string owner, ImmutableArray<string> guards)
    {
        _owner = owner;
        _guards = guards;
    }

    /// <summary>Emits a typed event adapter retaining current owner identity.</summary>
    /// <param name="root">The current root.</param>
    /// <param name="namespaceRoot">The runtime flavor namespace.</param>
    /// <param name="snapshot">Whether to use per-observation acquired receiver slots.</param>
    /// <returns>The dependency factory expression.</returns>
    internal string Emit(string root, string namespaceRoot, bool snapshot = false)
    {
        var owner = OwnerGetter(root);
        var dependency = $"global::{namespaceRoot}.Capabilities.ValidationDependency";
        var cached = snapshot && SnapshotOrdinal >= 0;
        var propertyOwner = cached ? PropertySnapshot : $"((object?)({owner})) as global::System.ComponentModel.INotifyPropertyChanged";
        var collectionOwner = cached ? CollectionSnapshot : $"((object?)({owner})) as global::System.Collections.Specialized.INotifyCollectionChanged";
        var property = $"{dependency}.PropertyChanged(() => {propertyOwner}, {GeneratorHelpers.Quote(Name)})";
        var collection = $"{dependency}.CollectionChanged(() => {collectionOwner})";
        if (cached)
        {
            property = $"{dependency}.AfterRead({property})";
            collection = $"{dependency}.AfterRead({collection})";
        }

        return IsCollection ? $"{property}, {collection}" : property;
    }

    /// <summary>Formats a null-aware owner getter without boxing struct copies.</summary>
    /// <param name="root">The current reference-owned root.</param>
    /// <returns>The owner expression.</returns>
    internal string OwnerGetter(string root)
    {
        var owner = Selector.ReplaceRoot(_owner, root);
        return _guards.IsEmpty ? owner : $"({string.Join(" || ", _guards.Select(guard => Selector.ReplaceRoot(guard, root)))} ? null : {owner})";
    }
}
