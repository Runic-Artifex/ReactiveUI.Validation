// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace ReactiveUI.Validation.SourceGenerators;

/// <summary>Lowers one read atomically, preserving branch order and index argument values.</summary>
/// <param name="model">The model contract.</param>
/// <param name="parameter">The parameter contract.</param>
/// <param name="operation">The operation contract.</param>
/// <param name="contractType">The exact converted lambda return contract.</param>
/// <param name="readBridges">The exact selected legal getters.</param>
internal sealed class SemanticReadPlan(SemanticModel model, IParameterSymbol parameter, IOperation operation, ITypeSymbol contractType, IReadOnlyDictionary<ISymbol, string>? readBridges = null)
{
    /// <summary>Gets whether two indexed member alternatives identify exactly one active property.</summary>
    internal bool IsSingleIndexedConditional => operation is IConditionalOperation conditional
        && IsMember(conditional.WhenTrue) && IsMember(conditional.WhenFalse!);

    /// <summary>Gets whether branch activation needs the exact selected custom truth operator.</summary>
    internal bool HasCustomConditionalOperator => ContainsCustomConditional(operation);

    /// <summary>Emits a read delegate with a distinct missing-owner result.</summary>
    /// <param name="root">The current stable root.</param>
    /// <param name="namespaceRoot">The matching runtime namespace.</param>
    /// <param name="selector">The complete operation and structural metadata graph.</param>
    /// <param name="snapshotOwners">Whether the containing cold observation owns receiver slots.</param>
    /// <returns>The typed atomic read delegate.</returns>
    internal string EmitRead(string root, string namespaceRoot, Selector selector, bool snapshotOwners = false)
    {
        var valueType = GeneratorHelpers.TypeName(contractType);
        var paths = selector.EmitPaths(namespaceRoot);
        var builder = new ReadBuilder(model, parameter, namespaceRoot, valueType, readBridges, selector, snapshotOwners) { MissingPaths = paths };
        builder.TrackValuePaths();
        var value = builder.Evaluate(operation);
        if (builder.IsErased(value))
        {
            value = $"({valueType})({value})";
        }

        var presentPaths = selector.EmitPaths(namespaceRoot, builder.IndexValues, builder.IndexAcquired, builder.PathAcquired);
        var body = $"() => {{\n{builder.Prelude}{builder.ResetSnapshots}{builder.Body}return global::{namespaceRoot}.Capabilities.ValidationRead<{valueType}>.Present({value}, {presentPaths});\n}}";
        return Selector.ReplaceIdentifierBody(body, Selector.RootMarker, root);
    }

    /// <summary>Emits a path snapshot without evaluating selected final member values.</summary>
    /// <param name="root">The current source.</param>
    /// <param name="namespaceRoot">The matching flavor namespace.</param>
    /// <param name="selector">The structural metadata graph.</param>
    /// <param name="snapshotOwners">Whether to acquire per-observation receiver slots.</param>
    /// <returns>The independently typed metadata read delegate.</returns>
    internal string EmitMetadataRead(string root, string namespaceRoot, Selector selector, bool snapshotOwners = false)
    {
        var pathType = $"global::{namespaceRoot}.Capabilities.ValidationPath";
        var builder = new ReadBuilder(model, parameter, namespaceRoot, pathType, readBridges, selector, snapshotOwners) { MissingPaths = selector.EmitPaths(namespaceRoot) };
        foreach (var path in selector.PathPlans)
        {
            foreach (var index in path.Indices)
            {
                builder.EvaluateMetadataIndex(index.Operation);
            }
        }

        var paths = selector.EmitPaths(namespaceRoot, builder.IndexValues, builder.IndexAcquired);
        var readType = $"global::{namespaceRoot}.Capabilities.ValidationRead<{pathType}>";
        var result = $"(__runic_paths.Length == 0 ? default({pathType})! : __runic_paths[0])";
        var rootGuard = parameter.Type.IsReferenceType ? $"if ({Selector.RootMarker} is null) return {readType}.Missing({selector.EmitPaths(namespaceRoot)});" : string.Empty;
        var body = $"() => {{ {builder.Prelude}{builder.ResetSnapshots}{rootGuard}\n{builder.Body}var __runic_paths = {paths}; return {readType}.Present({result}, __runic_paths); }}";
        return Selector.ReplaceIdentifierBody(body, Selector.RootMarker, root);
    }

    /// <summary>Emits one target-key read into the same per-bind receiver snapshot.</summary>
    /// <param name="operand">The exact index operand.</param>
    /// <param name="root">The stable current source.</param>
    /// <param name="namespaceRoot">The runtime flavor.</param>
    /// <param name="selector">The bound dependency graph.</param>
    /// <returns>The typed operand read delegate.</returns>
    internal string EmitOperand(IOperation operand, string root, string namespaceRoot, Selector selector)
    {
        var type = GeneratorHelpers.TypeName(SemanticSelectorPlanner.OperandType(operand));
        var paths = $"new global::{namespaceRoot}.Capabilities.ValidationPath[] {{ }}";
        var builder = new ReadBuilder(model, parameter, namespaceRoot, type, readBridges, selector, true) { MissingPaths = paths };
        var value = builder.Evaluate(operand);
        if (builder.IsErased(value))
        {
            value = $"({type})({value})";
        }

        return Selector.ReplaceRoot($"() => {{ {builder.Prelude}{builder.Body}return global::{namespaceRoot}.Capabilities.ValidationRead<{type}>.Present({value}, {paths}); }}", root);
    }

    /// <summary>Detects custom branch activation without interpreting its truth operator.</summary>
    /// <param name="value">The semantic operation.</param>
    /// <returns>Whether independent metadata would need a selected leaf value.</returns>
    private static bool ContainsCustomConditional(IOperation value)
    {
        if (value is IBinaryOperation { OperatorMethod: not null, OperatorKind: BinaryOperatorKind.ConditionalAnd or BinaryOperatorKind.ConditionalOr })
        {
            return true;
        }

        foreach (var child in value.ChildOperations)
        {
            if (ContainsCustomConditional(child))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests one selected member branch without treating a computation as a single property.</summary>
    /// <param name="value">The branch expression.</param>
    /// <returns>Whether the branch selects ordinary member storage.</returns>
    private static bool IsMember(IOperation value)
    {
        while (value is IConversionOperation or IParenthesizedOperation)
        {
            value = value is IConversionOperation conversion ? conversion.Operand : ((IParenthesizedOperation)value).Operand;
        }

        return value is IPropertyReferenceOperation or IFieldReferenceOperation;
    }

    /// <summary>Builds evaluation statements without eagerly reading inactive branches.</summary>
    /// <param name="model">The selector semantic model.</param>
    /// <param name="parameter">The selected root.</param>
    /// <param name="namespaceRoot">The matching runtime namespace.</param>
    /// <param name="valueType">The final selector value type.</param>
    /// <param name="readBridges">The selected static getter invocations.</param>
    /// <param name="selector">The per-observation dependency graph.</param>
    /// <param name="snapshotOwners">Whether to cache the actual acquired receivers.</param>
    private sealed class ReadBuilder(
        SemanticModel model,
        IParameterSymbol parameter,
        string namespaceRoot,
        string valueType,
        IReadOnlyDictionary<ISymbol, string>? readBridges,
        Selector selector,
        bool snapshotOwners)
    {
        /// <summary>The statement transition between two conditional branches.</summary>
        private const string BranchTransition = ";\n} else {";

        /// <summary>The source prefix for Boolean acquisition receipts.</summary>
        private const string BoolDeclaration = "bool ";

        /// <summary>The source suffix beginning a missing-reference branch.</summary>
        private const string NullBranch = " is null) {";

        /// <summary>The stored inaccessible reference results transported through proven object signatures.</summary>
        private readonly HashSet<string> _erasedValues = [];

        /// <summary>The metadata operands acquired once within their active branch.</summary>
        private readonly Dictionary<IOperation, string> _metadataValues = [];

        /// <summary>The next evaluation-local identifier.</summary>
        private int _next;

        /// <summary>The evaluated receiver for the current conditional-access branch.</summary>
        private string? _conditionalOwner;

        /// <summary>Gets declarations that remain in scope after every conditional branch.</summary>
        internal StringBuilder Prelude { get; } = new();

        /// <summary>Gets receiver resets for this independent read.</summary>
        internal string ResetSnapshots => snapshotOwners ? selector.EmitSnapshotResets() : string.Empty;

        /// <summary>Gets or sets an exceptional exit from a lazy custom-operator operand.</summary>
        internal string? MissingOwnerStatement { get; set; }

        /// <summary>Gets or sets the known structural identities for an unresolved owner.</summary>
        internal string MissingPaths { get; set; } = string.Empty;

        /// <summary>Gets the evaluation statement buffer.</summary>
        internal StringBuilder Body { get; } = new();

        /// <summary>Gets the current index values acquired in evaluation order.</summary>
        internal Dictionary<IOperation, string> IndexValues { get; } = new();

        /// <summary>Gets guards proving each index identity was acquired on the active evaluation path.</summary>
        internal Dictionary<IOperation, string> IndexAcquired { get; } = new();

        /// <summary>Gets the selected leaf activation guards for this read.</summary>
        internal Dictionary<SelectorPath, string> PathAcquired { get; } = new();

        /// <summary>Declares branch-local selected-path receipts in the enclosing read scope.</summary>
        internal void TrackValuePaths()
        {
            foreach (var path in selector.PathPlans)
            {
                var receipt = $"__runic_path{PathAcquired.Count}";
                PathAcquired.Add(path, receipt);
                _ = Prelude.Append(BoolDeclaration).Append(receipt).AppendLine(" = false;");
            }
        }

        /// <summary>Tests whether a proven erased reference read needs its accessible contract cast.</summary>
        /// <param name="value">The evaluated local.</param>
        /// <returns>Whether its generated signature transports object.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsErased(string value) => _erasedValues.Contains(value);

        /// <summary>Evaluates a semantic expression in its original branch and order.</summary>
        /// <param name="operation">The selected operation.</param>
        /// <returns>A stable typed local or constant expression.</returns>
        internal string Evaluate(IOperation operation)
        {
            var value = EvaluateCore(operation);
            if (snapshotOwners)
            {
                foreach (var dependency in selector.Dependencies.Where(dependency => dependency.SnapshotOperation is { } owner && owner.Kind == operation.Kind && owner.Syntax == operation.Syntax))
                {
                    _ = Body.Append(dependency.PropertySnapshot).Append(" = ((object?)(").Append(value).AppendLine(")) as global::System.ComponentModel.INotifyPropertyChanged;");
                    if (dependency.IsCollection)
                    {
                        _ = Body.Append(dependency.CollectionSnapshot).Append(" = ((object?)(").Append(value).AppendLine(")) as global::System.Collections.Specialized.INotifyCollectionChanged;");
                    }
                }
            }

            foreach (var path in PathAcquired)
            {
                if (path.Key.SelectedOperations.Exists(selected => selected.Kind == operation.Kind && selected.Syntax == operation.Syntax))
                {
                    _ = Body.Append(path.Value).AppendLine(" = true;");
                }
            }

            return value;
        }

        /// <summary>Acquires a structural operand only in its original active branch.</summary>
        /// <param name="operation">The exact index operation.</param>
        internal void EvaluateMetadataIndex(IOperation operation)
        {
            var guards = MetadataGuards(operation);
            foreach (var guard in guards)
            {
                var condition = MetadataValue(guard.Condition);
                var test = guard.Negate ? $"!({condition})" : condition;
                _ = Body.Append("if (").Append(guard.Null ? $"{condition} is not null" : test).AppendLine(") {");
            }

            SnapshotIndex(operation, MetadataValue(operation));
            foreach (var guard in guards)
            {
                _ = Body.AppendLine("}");
            }
        }

        /// <summary>Reads branch activation operations without acquiring their values.</summary>
        /// <param name="operation">The exact structural operand.</param>
        /// <returns>The outer-to-inner activation tests.</returns>
        private static List<(IOperation Condition, bool Negate, bool Null)> MetadataGuards(IOperation operation)
        {
            var guards = new List<(IOperation Condition, bool Negate, bool Null)>();
            for (var child = operation; child.Parent is { } parent; child = parent)
            {
                if (parent is IConditionalOperation conditional && !ReferenceEquals(child, conditional.Condition))
                {
                    guards.Insert(0, (conditional.Condition, ReferenceEquals(child, conditional.WhenFalse), false));
                }
                else if (parent is IBinaryOperation { OperatorMethod: null, OperatorKind: BinaryOperatorKind.ConditionalAnd or BinaryOperatorKind.ConditionalOr } binary
                    && ReferenceEquals(child, binary.RightOperand))
                {
                    guards.Insert(0, (binary.LeftOperand, binary.OperatorKind == BinaryOperatorKind.ConditionalOr, false));
                }
                else if (parent is IConditionalAccessOperation access && ReferenceEquals(child, access.WhenNotNull))
                {
                    guards.Insert(0, (access.Operation, false, true));
                }
            }

            return guards;
        }

        /// <summary>Infers member nullability while statically erasing an explicitly object-bound dynamic read.</summary>
        /// <param name="operation">The exact acquired operation.</param>
        /// <param name="erased">Whether a selected bridge transports an inaccessible reference.</param>
        /// <returns>The generated local type or inference keyword.</returns>
        private static string ResultType(IOperation operation, bool erased)
        {
            if (operation.Type!.TypeKind == TypeKind.Dynamic)
            {
                return "object";
            }

            return erased || operation is IParameterReferenceOperation or IPropertyReferenceOperation or IFieldReferenceOperation
                || operation.Type is INamedTypeSymbol { IsAnonymousType: true }
                ? "var"
                : GeneratorHelpers.TypeName(SemanticSelectorPlanner.OperandType(operation));
        }

        /// <summary>Evaluates the original operation without reevaluating observation receivers.</summary>
        /// <param name="operation">The semantic operation.</param>
        /// <returns>The acquired typed value.</returns>
        private string EvaluateCore(IOperation operation) => operation switch
        {
            IParameterReferenceOperation root => Root(root),
            INameOfOperation name => SemanticSelectorPlanner.FormatConstant(name.Type, name.ConstantValue.Value),
            ILiteralOperation literal => SemanticSelectorPlanner.FormatConstant(SemanticSelectorPlanner.OperandType(literal), literal.ConstantValue.Value),
            IDefaultValueOperation defaultValue => $"default({GeneratorHelpers.TypeName(SemanticSelectorPlanner.OperandType(defaultValue))})",
            IArrayCreationOperation { IsImplicit: true, Type: IArrayTypeSymbol array } creation => ImplicitArray(creation, array),
            IParenthesizedOperation parentheses => Evaluate(parentheses.Operand),
            IConversionOperation conversion => Convert(conversion),
            IConditionalOperation conditional => Conditional(conditional),
            ICoalesceOperation coalesce => Coalesce(coalesce),
            IConditionalAccessOperation conditionalAccess => ConditionalAccess(conditionalAccess),
            IConditionalAccessInstanceOperation => _conditionalOwner!,
            IBinaryOperation { OperatorKind: BinaryOperatorKind.ConditionalAnd or BinaryOperatorKind.ConditionalOr } binary => ShortCircuit(binary),
            IPropertyReferenceOperation property => Member(property, property.Instance, property.Arguments.Select(static argument => argument.Value)),
            IFieldReferenceOperation field => Member(field, field.Instance, []),
            _ => Composite(operation)
        };

        /// <summary>Acquires and proves the original reference root before conversions or dependent member reads.</summary>
        /// <param name="operation">The original declared parameter access.</param>
        /// <returns>The stable typed source local with an established owner-presence proof.</returns>
        private string Root(IParameterReferenceOperation operation)
        {
            var value = Store(operation, SemanticSelectorPlanner.RootExpression(model, operation.Parameter, allowMissing: true));
            if (operation.Type!.IsReferenceType)
            {
                _ = Body.Append("if (").Append(value).AppendLine(NullBranch);
                AppendMissingExit();
                _ = Body.AppendLine("}");
            }

            return value;
        }

        /// <summary>Stores one metadata operand in a scope-safe acquired slot.</summary>
        /// <param name="operation">The structural condition or index operand.</param>
        /// <returns>The acquired typed local.</returns>
        private string MetadataValue(IOperation operation)
        {
            if (!_metadataValues.TryGetValue(operation, out var result))
            {
                result = $"__runic_metadata{_metadataValues.Count}";
                _metadataValues.Add(operation, result);
                _ = Prelude.Append(GeneratorHelpers.TypeName(SemanticSelectorPlanner.OperandType(operation))).Append(' ').Append(result).AppendLine(" = default!;")
                    .Append(BoolDeclaration).Append(result).AppendLine("_acquired = false;");
            }

            _ = Body.Append("if (!").Append(result).AppendLine("_acquired) {");
            var value = Evaluate(operation);
            _ = Body.Append(result).Append(" = ").Append(value).AppendLine(";")
                .Append(result).AppendLine("_acquired = true;\n}");
            return result;
        }

        /// <summary>Copies an acquired index into a typed slot visible after branch completion.</summary>
        /// <param name="operation">The selected key operation.</param>
        /// <param name="value">The already evaluated key.</param>
        private void SnapshotIndex(IOperation operation, string value)
        {
            if (!IndexValues.TryGetValue(operation, out var result))
            {
                result = $"__runic_index{IndexValues.Count}";
                IndexValues.Add(operation, result);
                IndexAcquired.Add(operation, $"{result}_acquired");
                _ = Prelude.Append(GeneratorHelpers.TypeName(SemanticSelectorPlanner.OperandType(operation))).Append(' ').Append(result).AppendLine(" = default!;")
                    .Append(BoolDeclaration).Append(result).AppendLine("_acquired = false;");
            }

            _ = Body.Append(result).Append(" = ").Append(value).AppendLine(";")
                .Append(result).AppendLine("_acquired = true;");
        }

        /// <summary>Evaluates a typed conversion rather than stripping its dispatch.</summary>
        /// <param name="operation">The conversion.</param>
        /// <returns>The converted local.</returns>
        private string Convert(IConversionOperation operation)
        {
            var operand = Evaluate(operation.Operand);
            var type = GeneratorHelpers.TypeName(operation.Type!);
            var converted = operation.IsImplicit && !IsErased(operand)
                && (operation.OperatorMethod is not null || !SemanticSelectorPlanner.HasEnumContract(operation.Type))
                ? operand
                : $"({type})({operand})";
            var expression = operation.IsTryCast ? $"({operand}) as {type}" : converted;
            return Store(operation, operation.IsChecked ? $"checked({expression})" : expression);
        }

        /// <summary>Builds compiler-supplied params arrays from acquired values without pasting their parent access syntax.</summary>
        /// <param name="operation">The implicit params-array operation.</param>
        /// <param name="array">The exact array contract.</param>
        /// <returns>The stored typed array value.</returns>
        private string ImplicitArray(IArrayCreationOperation operation, IArrayTypeSymbol array)
        {
            var values = new List<string>();
            if (operation.Initializer is not null)
            {
                foreach (var value in operation.Initializer.ElementValues)
                {
                    values.Add(Evaluate(value));
                }
            }

            return Store(operation, $"new {GeneratorHelpers.TypeName(array.ElementType)}[] {{ {string.Join(", ", values)} }}");
        }

        /// <summary>Evaluates only the selected conditional branch.</summary>
        /// <param name="operation">The conditional operation.</param>
        /// <returns>The selected result local.</returns>
        private string Conditional(IConditionalOperation operation)
        {
            var condition = Evaluate(operation.Condition);
            var result = Declare(operation.Type!);
            _ = Body.Append("if (").Append(condition).AppendLine(") {");
            var whenTrue = Evaluate(operation.WhenTrue);
            _ = Body.Append(result).Append(" = ").Append(whenTrue).AppendLine(BranchTransition);
            var whenFalse = Evaluate(operation.WhenFalse!);
            _ = Body.Append(result).Append(" = ").Append(whenFalse).AppendLine(";\n}");
            return result;
        }

        /// <summary>Evaluates a coalesce fallback only when the selected left value is null.</summary>
        /// <param name="operation">The coalesce operation.</param>
        /// <returns>The coalesced result.</returns>
        private string Coalesce(ICoalesceOperation operation)
        {
            var left = Evaluate(operation.Value);
            var result = Declare(operation.Type!);
            _ = Body.Append("if (").Append(left).AppendLine(NullBranch);
            var whenNull = Evaluate(operation.WhenNull);
            _ = Body.Append(result).Append(" = ").Append(whenNull).AppendLine(BranchTransition);
            var present = operation.Value.Type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } ? $"{left}.Value" : left;
            _ = Body.Append(result).Append(" = ").Append(present).AppendLine(";\n}");
            return result;
        }

        /// <summary>Returns a null leaf for a conditional receiver and evaluates only its present branch.</summary>
        /// <param name="operation">The conditional access.</param>
        /// <returns>The nullable branch result.</returns>
        private string ConditionalAccess(IConditionalAccessOperation operation)
        {
            var receiver = Evaluate(operation.Operation);
            var result = Declare(operation.Type!);
            _ = Body.Append("if (").Append(receiver).AppendLine(NullBranch)
                .Append(result).AppendLine(" = default;\n} else {");
            var previous = _conditionalOwner;
            _conditionalOwner = operation.Operation.Type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T }
                ? $"{receiver}.Value"
                : receiver;
            var present = Evaluate(operation.WhenNotNull);
            _conditionalOwner = previous;
            _ = Body.Append(result).Append(" = ").Append(present).AppendLine(";\n}");
            return result;
        }

        /// <summary>Preserves built-in Boolean short-circuit evaluation.</summary>
        /// <param name="operation">The binary operation.</param>
        /// <returns>The Boolean result.</returns>
        private string ShortCircuit(IBinaryOperation operation)
        {
            if (operation.OperatorMethod is not null)
            {
                return CustomShortCircuit(operation);
            }

            var left = Evaluate(operation.LeftOperand);
            var result = Declare(operation.Type!);
            var test = operation.OperatorKind == BinaryOperatorKind.ConditionalAnd ? left : $"!({left})";
            _ = Body.Append("if (").Append(test).AppendLine(") {");
            var right = Evaluate(operation.RightOperand);
            _ = Body.Append(result).Append(" = ").Append(right).AppendLine(BranchTransition);
            _ = Body.Append(result).Append(" = ").Append(left).AppendLine(";\n}");
            return result;
        }

        /// <summary>Lets C# execute the exact custom truth operator before acquiring its lazy right operand.</summary>
        /// <param name="operation">The bound user-defined conditional operator.</param>
        /// <returns>The correctly short-circuited result.</returns>
        private string CustomShortCircuit(IBinaryOperation operation)
        {
            var left = Evaluate(operation.LeftOperand);
            var result = Declare(operation.Type!);
            var marker = $"{result}_missing";
            var thunk = $"{result}_right";
            var right = new ReadBuilder(model, parameter, namespaceRoot, valueType, readBridges, selector, snapshotOwners)
            {
                MissingPaths = MissingPaths,
                MissingOwnerStatement = $"{marker} = new global::System.InvalidOperationException(\"Missing validation owner.\"); throw {marker};",
            };
            right._next = _next;
            foreach (var cached in IndexValues)
            {
                right.IndexValues.Add(cached.Key, cached.Value);
                right.IndexAcquired.Add(cached.Key, IndexAcquired[cached.Key]);
            }

            foreach (var path in PathAcquired)
            {
                right.PathAcquired.Add(path.Key, path.Value);
            }

            var value = right.Evaluate(operation.RightOperand);
            _next = right._next;
            foreach (var index in right.IndexValues)
            {
                if (IndexValues.ContainsKey(index.Key))
                {
                    continue;
                }

                IndexValues.Add(index.Key, index.Value);
                IndexAcquired.Add(index.Key, right.IndexAcquired[index.Key]);
            }

            _ = Prelude.Append(right.Prelude);
            var replacements = new Dictionary<SyntaxNode, string> { [operation.LeftOperand.Syntax] = left, [operation.RightOperand.Syntax] = $"{thunk}()" };
            _ = Body.Append("global::System.InvalidOperationException? ").Append(marker).AppendLine(" = null;")
                .Append("global::System.Func<").Append(GeneratorHelpers.TypeName(operation.RightOperand.Type!)).Append("> ").Append(thunk).AppendLine(" = () => {")
                .Append(right.Body).Append("return ").Append(value).AppendLine(";\n};")
                .Append("try { ").Append(result).Append(" = ").Append(Rebind(operation, replacements)).AppendLine("; }")
                .Append("catch (global::System.InvalidOperationException __runic_error) when (global::System.Object.ReferenceEquals(__runic_error, ")
                .Append(marker).AppendLine(")) {");
            AppendMissingExit();
            _ = Body.AppendLine("}");
            return result;
        }

        /// <summary>Exits this read or lazy operand without applying an operator to fabricated missing values.</summary>
        private void AppendMissingExit()
        {
            if (MissingOwnerStatement is not null)
            {
                _ = Body.AppendLine(MissingOwnerStatement);
            }
            else
            {
                _ = Body.Append("return global::").Append(namespaceRoot).Append(".Capabilities.ValidationRead<").Append(valueType)
                    .Append(">.Missing(").Append(MissingPaths).AppendLine(");");
            }
        }

        /// <summary>Reads an owner and index arguments once, in source evaluation order.</summary>
        /// <param name="operation">The storage operation.</param>
        /// <param name="instance">The optional storage owner.</param>
        /// <param name="arguments">The index arguments.</param>
        /// <returns>The stored member value.</returns>
        /// <exception cref="System.InvalidOperationException">The operation has an unsupported conditional storage kind.</exception>
        private string Member(IOperation operation, IOperation? instance, IEnumerable<IOperation> arguments)
        {
            var replacements = new Dictionary<SyntaxNode, string>();
            string? conditionalOwner = null;
            if (instance is not null)
            {
                var owner = Evaluate(instance);
                conditionalOwner = instance is IConditionalAccessInstanceOperation ? owner : null;
                if (instance.Type!.IsReferenceType)
                {
                    _ = Body.Append("if (").Append(owner).AppendLine(NullBranch);
                    AppendMissingExit();
                    _ = Body.AppendLine("}");
                }

                replacements[instance.Syntax] = owner;
            }

            foreach (var argument in arguments)
            {
                var value = Evaluate(argument);
                if (argument.Syntax != operation.Syntax)
                {
                    replacements[argument.Syntax] = value;
                }

                SnapshotIndex(argument, value);
            }

            if (conditionalOwner is not null)
            {
                return Store(operation, ConditionalMemberAccess(operation, conditionalOwner));
            }

            var expression = Rebind(operation, replacements);
            var contextual = operation switch
            {
                IBinaryOperation binary => binary.IsChecked ? $"checked({expression})" : $"unchecked({expression})",
                IUnaryOperation unary => unary.IsChecked ? $"checked({expression})" : $"unchecked({expression})",
                _ => expression,
            };
            return Store(operation, contextual);
        }

        /// <summary>Reads a present conditional owner through its exact member or statically proven bridge.</summary>
        /// <param name="operation">The selected storage operation.</param>
        /// <param name="owner">The acquired present receiver.</param>
        /// <returns>The exact getter invocation.</returns>
        /// <exception cref="System.InvalidOperationException">The operation has an unsupported conditional storage kind.</exception>
        private string ConditionalMemberAccess(IOperation operation, string owner)
        {
            var member = operation switch
            {
                IPropertyReferenceOperation property => (ISymbol)property.Property,
                IFieldReferenceOperation field => field.Field,
                _ => null,
            };
            if (member is not null && readBridges is not null && readBridges.TryGetValue(member, out var bridge))
            {
                bridge = Selector.ReplaceIdentifier(bridge, AccessBridgeEmitter.OwnerPlaceholder, owner);
                if (operation is IPropertyReferenceOperation indexed)
                {
                    foreach (var argument in indexed.Arguments)
                    {
                        bridge = Selector.ReplaceIdentifier(bridge, AccessBridgeEmitter.IndexPlaceholder(argument.Parameter!.Ordinal), IndexValues[argument.Value]);
                    }
                }

                return bridge;
            }

            return operation switch
            {
                IPropertyReferenceOperation { Property.IsIndexer: true } indexed => $"{owner}[{string.Join(", ", indexed.Arguments.Select(argument => IndexValues[argument.Value]))}]",
                IPropertyReferenceOperation property => $"{owner}.@{property.Property.Name}",
                IFieldReferenceOperation field => $"{owner}.@{field.Field.Name}",
                _ => throw new System.InvalidOperationException("Conditional storage must be a property, field or indexer."),
            };
        }

        /// <summary>Evaluates ordinary operator operands in source order.</summary>
        /// <param name="operation">The expression.</param>
        /// <returns>The stored expression result.</returns>
        private string Composite(IOperation operation)
        {
            var replacements = new Dictionary<SyntaxNode, string>();
            foreach (var child in operation.ChildOperations)
            {
                replacements[child.Syntax] = Evaluate(child);
            }

            var expression = Rebind(operation, replacements);
            var contextual = operation switch
            {
                IBinaryOperation binary => binary.IsChecked ? $"checked({expression})" : $"unchecked({expression})",
                IUnaryOperation unary => unary.IsChecked ? $"checked({expression})" : $"unchecked({expression})",
                _ => expression,
            };
            return Store(operation, contextual);
        }

        /// <summary>Rebinds exact child syntax, preserving the original operation dispatch.</summary>
        /// <param name="operation">The original operation.</param>
        /// <param name="replacements">The evaluated child locals.</param>
        /// <returns>The fully qualified compound expression.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string Rebind(IOperation operation, Dictionary<SyntaxNode, string> replacements) =>
            SemanticSelectorPlanner.RewriteBound(model, operation.Syntax, parameter, replacements, readBridges, IndexValues);

        /// <summary>Stores a typed evaluated expression.</summary>
        /// <param name="operation">The expression operation.</param>
        /// <param name="expression">The lowered expression.</param>
        /// <returns>The new local.</returns>
        private string Store(IOperation operation, string expression)
        {
            var result = $"__runic_read{_next}";
            _next++;
            var member = operation switch
            {
                IPropertyReferenceOperation property => (ISymbol)property.Property,
                IFieldReferenceOperation field => field.Field,
                _ => null,
            };
            var erased = operation.Type is { IsReferenceType: true } && !GeneratorHelpers.IsAccessibleType(model.Compilation, operation.Type)
                && member is not null && readBridges?.ContainsKey(member) == true;
            var type = ResultType(operation, erased);
            if (erased)
            {
                _ = _erasedValues.Add(result);
            }

            if (operation.Syntax.Parent is Microsoft.CodeAnalysis.CSharp.Syntax.PostfixUnaryExpressionSyntax
                { RawKind: (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.SuppressNullableWarningExpression })
            {
                expression = $"(({expression}))!";
            }

            _ = Body.Append(type).Append(' ').Append(result).Append(" = ").Append(expression).AppendLine(";");
            return result;
        }

        /// <summary>Declares a branch result without evaluating either branch.</summary>
        /// <param name="type">The expression result type.</param>
        /// <returns>The local name.</returns>
        private string Declare(ITypeSymbol type)
        {
            var result = $"__runic_read{_next}";
            _next++;
            _ = Body.Append(GeneratorHelpers.TypeName(type)).Append(' ').Append(result).AppendLine(";");
            return result;
        }
    }
}
