// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators;

/// <summary>A typed writable lens with explicit outward value-owner write-back.</summary>
[SuppressMessage("Performance", "PSH1100", Justification = "This is compiler-time planning and source formatting over bounded immutable operations, not emitted subscription execution.")]
internal sealed class AccessPlan
{
    /// <summary>Distance from the chain end to the final member owner.</summary>
    private const int ParentOffset = 2;

    /// <summary>The default synchronous owner-local prefix.</summary>
    private const string OwnerPrefix = "__runic_owner";

    /// <summary>The writable path from its stable reference-owned root.</summary>
    private readonly ImmutableArray<AccessStep> _steps;

    /// <summary>The original lambda root contract before any delegate contravariance.</summary>
    private readonly string _rootExpression;

    /// <summary>Initializes a new instance of the <see cref="AccessPlan"/> class.</summary>
    /// <param name="valueType">The actual storage type before any selector conversion.</param>
    /// <param name="steps">The writable path.</param>
    /// <param name="rootExpression">The original typed source parameter or exact opaque bridge transport.</param>
    internal AccessPlan(ITypeSymbol valueType, ImmutableArray<AccessStep> steps, string? rootExpression = null)
    {
        ValueType = valueType;
        _steps = steps;
        _rootExpression = rootExpression ?? Selector.RootMarker;
    }

    /// <summary>Gets the actual writable storage type.</summary>
    internal ITypeSymbol ValueType { get; }

    /// <summary>Gets the exact terminal property, field or indexer for its independent setter input contract.</summary>
    internal ISymbol StorageMember => _steps[_steps.Length - 1].Member;

    /// <summary>Gets or sets gets the current replacing-owner dependency graph.</summary>
    internal ImmutableArray<SelectorDependency> Dependencies { get; set; } = [];

    /// <summary>Gets or sets the exact target slot identity including current index arguments.</summary>
    internal SelectorPath? TargetPath { get; set; }

    /// <summary>Gets or sets the exact semantic plan used for target-key observation snapshots.</summary>
    internal Selector? ReadSelector { get; set; }

    /// <summary>Gets whether an enclosing struct must be copied back.</summary>
    internal bool RequiresCopyBack => CopyBackIndex >= 0;

    /// <summary>Gets the outermost contiguous struct storage written by the selected assignment.</summary>
    private int CopyBackIndex
    {
        get
        {
            var outer = -1;
            for (var index = _steps.Length - ParentOffset; index >= 0 && _steps[index].ValueType.IsValueType; index--)
            {
                outer = index;
            }

            return outer;
        }
    }

    /// <summary>Emits one synchronous assignment acquiring each owner and key exactly once.</summary>
    /// <param name="root">The current stable reference-owned root.</param>
    /// <param name="value">The output expression, implicitly assignable to ValueType.</param>
    /// <returns>A complete lambda body with null skip and struct copy-back.</returns>
    internal string EmitAssignment(string root, string value)
    {
        var body = new StringBuilder("{\n");
        Acquire(body, root, "return", OwnerPrefix, true);
        AppendWrite(body, root, value, OwnerPrefix);
        return body.Append('}').ToString();
    }

    /// <summary>Emits a bound target resolver retaining current owner identity and cached keys.</summary>
    /// <param name="root">The current stable root.</param>
    /// <param name="namespaceRoot">The runtime flavor namespace.</param>
    /// <param name="outputType">The actual converter output type syntax.</param>
    /// <returns>The complete ValidationWritePlan expression.</returns>
    internal string EmitTargetPlan(string root, string namespaceRoot, string outputType)
    {
        var access = $"global::{namespaceRoot}.Capabilities.ValidationTargetAccess<{outputType}>";
        var plan = $"global::{namespaceRoot}.Capabilities.ValidationWritePlan<{outputType}>";
        var body = new StringBuilder("new ").Append(plan).AppendLine("(() => {");
        _ = body.AppendLine(ReadSelector?.EmitSnapshotDeclarations()).Append("return new ").Append(plan).AppendLine("(() => {");
        foreach (var dependency in Dependencies.Where(static dependency => dependency.SnapshotOrdinal >= 0))
        {
            _ = body.Append(dependency.PropertySnapshot).AppendLine(" = null;");
            if (dependency.IsCollection)
            {
                _ = body.Append(dependency.CollectionSnapshot).AppendLine(" = null;");
            }
        }

        Acquire(body, root, $"return {access}.Missing()", OwnerPrefix, true, namespaceRoot);
        var identity = _steps.Length - 1;
        var keys = _steps.SelectMany((step, segment) => step.Indices.Select((_, argument) => AccessStep.IndexMarker(segment, argument))).ToImmutableArray();
        var slot = TargetPath is null ? string.Empty : $", {TargetPath.Emit(namespaceRoot, keys)}";
        _ = body.Append("return ").Append(access).Append(".Present((object)__runic_owner").Append(identity).Append(slot).AppendLine(", __runic_output => {");
        AppendWrite(body, root, "__runic_output", OwnerPrefix);
        _ = body.AppendLine("});").Append("}, new global::").Append(namespaceRoot).AppendLine(".Capabilities.ValidationDependency[] {");
        _ = body.Append(string.Join(", ", Dependencies.Select(dependency => dependency.Emit(root, namespaceRoot, true)))).AppendLine("});\n})");
        return body.ToString();
    }

    /// <summary>Acquires the target parent chain without reading the selected leaf.</summary>
    /// <param name="body">The statement buffer.</param>
    /// <param name="root">The current root.</param>
    /// <param name="missing">The missing-target return statement.</param>
    /// <param name="prefix">The independent synchronous owner-local prefix.</param>
    /// <param name="cacheKeys">Whether this scope acquires the current index arguments.</param>
    /// <param name="namespaceRoot">The runtime flavor when observation snapshots are required.</param>
    private void Acquire(StringBuilder body, string root, string missing, string prefix, bool cacheKeys, string? namespaceRoot = null)
    {
        _ = body.Append("var ").Append(prefix).Append("0 = ").Append(Selector.ReplaceRoot(_rootExpression, root)).AppendLine(";");
        for (var index = 0; index < _steps.Length; index++)
        {
            var step = _steps[index];
            var owner = $"{prefix}{index}";
            if (step.OwnerType.IsReferenceType)
            {
                _ = body.Append("if (").Append(owner).Append(" is null) ").Append(missing).AppendLine(";");
            }

            if (namespaceRoot is not null)
            {
                AppendOwnerSnapshot(body, step, owner);
            }

            if (cacheKeys)
            {
                AppendIndices(body, root, missing, step, index, namespaceRoot);
            }

            if (index < _steps.Length - 1)
            {
                _ = body.Append("var ").Append(prefix).Append(index + 1).Append(" = ").Append(step.Access(owner, root)).AppendLine(";");
            }
        }
    }

    /// <summary>Records the exact acquired reference owner before target delivery.</summary>
    /// <param name="body">The resolution statement buffer.</param>
    /// <param name="step">The selected storage operation.</param>
    /// <param name="owner">The already acquired receiver.</param>
    private void AppendOwnerSnapshot(StringBuilder body, AccessStep step, string owner)
    {
        foreach (var dependency in Dependencies.Where(dependency => dependency.SnapshotOperation is { } receiver
            && step.OwnerOperation is { } selected && receiver.Kind == selected.Kind && receiver.Syntax == selected.Syntax))
        {
            _ = body.Append(dependency.PropertySnapshot).Append(" = ((object?)(").Append(owner).AppendLine(")) as global::System.ComponentModel.INotifyPropertyChanged;");
            if (dependency.IsCollection)
            {
                _ = body.Append(dependency.CollectionSnapshot).Append(" = ((object?)(").Append(owner).AppendLine(")) as global::System.Collections.Specialized.INotifyCollectionChanged;");
            }
        }
    }

    /// <summary>Acquires every typed key once while recording its notification owners.</summary>
    /// <param name="body">The resolution statement buffer.</param>
    /// <param name="root">The stable source.</param>
    /// <param name="missing">The unresolved-target return.</param>
    /// <param name="step">The selected storage operation.</param>
    /// <param name="segment">The storage ordinal.</param>
    /// <param name="namespaceRoot">The optional runtime flavor.</param>
    private void AppendIndices(StringBuilder body, string root, string missing, AccessStep step, int segment, string? namespaceRoot)
    {
        for (var argument = 0; argument < step.Indices.Length; argument++)
        {
            var key = AccessStep.IndexMarker(segment, argument);
            if (namespaceRoot is not null && ReadSelector is not null)
            {
                var type = GeneratorHelpers.TypeName(SemanticSelectorPlanner.OperandType(step.IndexOperations[argument]));
                var read = ReadSelector.EmitOperandRead(step.IndexOperations[argument], root, namespaceRoot);
                _ = body.Append("var ").Append(key).Append("_read = ((global::System.Func<global::").Append(namespaceRoot).Append(".Capabilities.ValidationRead<")
                    .Append(type).Append(">>)(").Append(read).AppendLine("))();")
                    .Append("if (!").Append(key).Append("_read.HasOwner) ").Append(missing).AppendLine(";")
                    .Append("var ").Append(key).Append(" = ").Append(key).AppendLine("_read.Value;");
            }
            else
            {
                _ = body.Append("var ").Append(key).Append(" = ")
                    .Append(Selector.ReplaceRoot(step.Indices[argument], root)).AppendLine(";");
            }
        }
    }

    /// <summary>Writes the selected slot and every contiguous enclosing struct storage.</summary>
    /// <param name="body">The statement buffer.</param>
    /// <param name="root">The stable root used for argument templates.</param>
    /// <param name="value">The output expression.</param>
    /// <param name="prefix">The synchronous owner-local prefix.</param>
    private void AppendWrite(StringBuilder body, string root, string value, string prefix)
    {
        var leaf = _steps.Length - 1;
        _ = body.AppendLine(_steps[leaf].Assignment($"{prefix}{leaf}", root, value));
        for (var index = _steps.Length - ParentOffset; index >= 0; index--)
        {
            if (!_steps[index].ValueType.IsValueType)
            {
                break;
            }

            _ = body.AppendLine(_steps[index].Assignment($"{prefix}{index}", root, $"{prefix}{index + 1}"));
        }
    }
}
