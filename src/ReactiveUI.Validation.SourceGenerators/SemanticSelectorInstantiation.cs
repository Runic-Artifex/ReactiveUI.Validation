// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ReactiveUI.Validation.SourceGenerators;

/// <summary>Instantiates finite selector source in the original compilation's symbol universe.</summary>
[SuppressMessage("Performance", "PSH1100", Justification = "Bounded source specialization and semantic dispatch verification run only during compilation, not emitted runtime execution.")]
internal static class SemanticSelectorInstantiation
{
    /// <summary>Specializes a selected generic declaration without cloning its model or changing the call location.</summary>
    /// <param name="selectingModel">The selecting expression's semantic model.</param>
    /// <param name="selectedFactory">The actual constructed field, property or method.</param>
    /// <param name="definition">The proven finite source expression.</param>
    /// <param name="recoveredModel">The original or speculative defining model.</param>
    /// <param name="recoveredDefinition">The expression with its actual type arguments.</param>
    /// <param name="reason">The explicit typed alternative if specialization is not sound.</param>
    /// <returns>Whether the definition can be planned with its actual constructed types.</returns>
    internal static bool TryInstantiate(
        SemanticModel selectingModel,
        ISymbol selectedFactory,
        ExpressionSyntax definition,
        out SemanticModel recoveredModel,
        out ExpressionSyntax recoveredDefinition,
        out string reason)
    {
        recoveredModel = selectingModel.Compilation.GetSemanticModel(definition.SyntaxTree);
        recoveredDefinition = definition;
        reason = string.Empty;
        var arguments = FactoryArguments(selectedFactory);
        if (arguments.Count == 0)
        {
            return true;
        }

        var contract = selectedFactory switch
        {
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            IMethodSymbol factory => factory.ReturnType,
            _ => null,
        };
        if (contract is null)
        {
            reason = "Supply a typed ValidationSelector factory for an unavailable constructed selector contract.";
            return false;
        }

        var rewriter = new InstantiationRewriter(recoveredModel, arguments, definition);
        var annotation = new SyntaxAnnotation();
        var specialized = ((ExpressionSyntax)rewriter.Visit(definition)!).WithAdditionalAnnotations(annotation);
        var cast = SyntaxFactory.CastExpression(
            SyntaxFactory.ParseTypeName(GeneratorHelpers.TypeName(contract)),
            SyntaxFactory.ParenthesizedExpression(specialized));
        if (!TryBindDefinition(recoveredModel, definition, cast, out var speculative, out var scope) || speculative is null)
        {
            reason = "Supply a typed ValidationSelector factory for an unavailable constructed initializer context.";
            return false;
        }

        specialized = (ExpressionSyntax)scope.GetAnnotatedNodes(annotation).Single();
        if (!rewriter.PreservesDispatch(speculative, specialized))
        {
            reason = "Supply a typed ValidationSelector factory when constructed source cannot retain its exact lexical type or member dispatch.";
            return false;
        }

        recoveredModel = speculative;
        recoveredDefinition = specialized;
        return true;
    }

    /// <summary>Binds the specialized expression in its original initializer, arrow or single-return scope.</summary>
    /// <param name="model">The original defining model.</param>
    /// <param name="definition">The original definition.</param>
    /// <param name="cast">The specialized typed expression.</param>
    /// <param name="speculative">The recovered semantic model.</param>
    /// <param name="scope">The exact syntax passed to speculative binding.</param>
    /// <returns>Whether the original lexical scope supports speculative binding.</returns>
    private static bool TryBindDefinition(SemanticModel model, ExpressionSyntax definition, ExpressionSyntax cast, out SemanticModel? speculative, out SyntaxNode scope)
    {
        if (definition.Parent is EqualsValueClauseSyntax initializer)
        {
            var clause = SyntaxFactory.EqualsValueClause(cast);
            scope = clause;
            return model.TryGetSpeculativeSemanticModel(initializer.SpanStart, clause, out speculative);
        }

        if (definition.Parent is ArrowExpressionClauseSyntax arrow)
        {
            var clause = SyntaxFactory.ArrowExpressionClause(cast);
            scope = clause;
            return model.TryGetSpeculativeSemanticModel(arrow.SpanStart, clause, out speculative);
        }

        if (definition.Parent is ReturnStatementSyntax statement)
        {
            var returned = SyntaxFactory.ReturnStatement(cast);
            scope = returned;
            return model.TryGetSpeculativeSemanticModel(statement.SpanStart, returned, out speculative);
        }

        scope = cast;
        speculative = null;
        return false;
    }

    /// <summary>Collects actual arguments from every constructed declaring and method slot.</summary>
    /// <param name="factory">The actual selected factory.</param>
    /// <returns>The changed declaration slots by original symbol identity.</returns>
    private static Dictionary<ITypeParameterSymbol, ITypeSymbol> FactoryArguments(ISymbol factory)
    {
        var arguments = new Dictionary<ITypeParameterSymbol, ITypeSymbol>(SymbolEqualityComparer.Default);
        for (var owner = factory.ContainingType; owner is not null; owner = owner.ContainingType)
        {
            AddArguments(arguments, owner.OriginalDefinition.TypeParameters, owner.TypeArguments);
        }

        if (factory is IMethodSymbol method)
        {
            AddArguments(arguments, method.OriginalDefinition.TypeParameters, method.TypeArguments);
        }

        return arguments;
    }

    /// <summary>Records only changed declaration slots, retaining unchanged open lexical owners.</summary>
    /// <param name="arguments">The type substitution.</param>
    /// <param name="parameters">The original declaration slots.</param>
    /// <param name="actual">Their selected arguments.</param>
    private static void AddArguments(
        Dictionary<ITypeParameterSymbol, ITypeSymbol> arguments,
        System.Collections.Immutable.ImmutableArray<ITypeParameterSymbol> parameters,
        System.Collections.Immutable.ImmutableArray<ITypeSymbol> actual)
    {
        for (var index = 0; index < parameters.Length; index++)
        {
            if (!SymbolEqualityComparer.IncludeNullability.Equals(parameters[index], actual[index]))
            {
                arguments[parameters[index]] = actual[index];
            }
        }
    }

    /// <summary>Rewrites bound type slots and preserves the original physical member selection.</summary>
    /// <param name="model">The original definition's model.</param>
    /// <param name="arguments">The selected type arguments by declaration identity.</param>
    /// <param name="definition">The finite original selector expression.</param>
    private sealed class InstantiationRewriter(SemanticModel model, Dictionary<ITypeParameterSymbol, ITypeSymbol> arguments, ExpressionSyntax definition) : CSharpSyntaxRewriter
    {
        /// <summary>The exact original member associated with each rewritten access.</summary>
        private readonly Dictionary<SyntaxAnnotation, ISymbol> _members = [];

        /// <summary>The original operator, conversion and constant behind each specialized expression.</summary>
        private readonly Dictionary<SyntaxAnnotation, IOperation> _operations = [];

        /// <summary>The original contextual conversions associated with each source expression.</summary>
        private readonly Dictionary<SyntaxNode, List<IConversionOperation>> _implicitConversions = CollectConversions(model, definition);

        /// <inheritdoc />
        public override SyntaxNode? Visit(SyntaxNode? node)
        {
            if (node is ExpressionSyntax constant && SourceOperation(constant) is INameOfOperation { ConstantValue.HasValue: true } name)
            {
                return TrackOperation(SyntaxFactory.ParseExpression(SemanticSelectorPlanner.FormatConstant(name.Type, name.ConstantValue.Value)).WithTriviaFrom(node), name);
            }

            if (node is TypeSyntax typeSyntax && model.GetSymbolInfo(typeSyntax).Symbol is ITypeSymbol type)
            {
                return SyntaxFactory.ParseTypeName(GeneratorHelpers.TypeName(Substitute(type))).WithTriviaFrom(node);
            }

            var rewritten = base.Visit(node);
            return node is ExpressionSyntax original && rewritten is ExpressionSyntax expression
                ? BindOperations(original, expression)
                : rewritten;
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            var rewritten = (BinaryExpressionSyntax)base.VisitBinaryExpression(node)!;
            if (SourceOperation(node) is not IBinaryOperation operation)
            {
                return rewritten;
            }

            if (operation.OperatorMethod is { } method)
            {
                return rewritten.WithLeft(Cast(rewritten.Left, OperandType(method.Parameters[0].Type, operation.LeftOperand.Type, operation.IsLifted)))
                    .WithRight(Cast(rewritten.Right, OperandType(method.Parameters[1].Type, operation.RightOperand.Type, operation.IsLifted)));
            }

            if (UsesGenericReferenceEquality(operation))
            {
                var reference = model.Compilation.GetSpecialType(SpecialType.System_Object);
                return rewritten.WithLeft(Cast(rewritten.Left, reference)).WithRight(Cast(rewritten.Right, reference));
            }

            return rewritten;
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
        {
            var rewritten = (PrefixUnaryExpressionSyntax)base.VisitPrefixUnaryExpression(node)!;
            return SourceOperation(node) is IUnaryOperation { OperatorMethod: { } method } operation
                ? rewritten.WithOperand(Cast(rewritten.Operand, OperandType(method.Parameters[0].Type, operation.Operand.Type, operation.IsLifted)))
                : rewritten;
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitCastExpression(CastExpressionSyntax node)
        {
            var rewritten = (CastExpressionSyntax)base.VisitCastExpression(node)!;
            return SourceOperation(node) is IConversionOperation { OperatorMethod: { } method } operation
                ? rewritten.WithExpression(Cast(rewritten.Expression, OperandType(method.Parameters[0].Type, operation.Operand.Type, false)))
                : rewritten;
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            var symbol = model.GetSymbolInfo(node).Symbol;
            return symbol is IFieldSymbol { IsStatic: true } or IPropertySymbol { IsStatic: true }
                && node.Parent is not MemberAccessExpressionSyntax
                ? Track(SyntaxFactory.ParseExpression($"{GeneratorHelpers.TypeName(Substitute(symbol.ContainingType!))}.@{symbol.Name}").WithTriviaFrom(node), symbol)
                : base.VisitIdentifierName(node);
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            var symbol = model.GetSymbolInfo(node).Symbol;
            var rewritten = (MemberAccessExpressionSyntax)base.VisitMemberAccessExpression(node)!;
            if (symbol is IFieldSymbol or IPropertySymbol)
            {
                if (symbol.IsStatic)
                {
                    rewritten = rewritten.WithExpression(SyntaxFactory.ParseExpression(GeneratorHelpers.TypeName(Substitute(symbol.ContainingType!))));
                }
                else if (model.GetTypeInfo(node.Expression).Type is ITypeParameterSymbol && symbol.ContainingType is { IsReferenceType: true } declared)
                {
                    rewritten = rewritten.WithExpression(SyntaxFactory.ParenthesizedExpression(SyntaxFactory.CastExpression(
                        SyntaxFactory.ParseTypeName(GeneratorHelpers.TypeName(Substitute(declared))),
                        SyntaxFactory.ParenthesizedExpression(rewritten.Expression))));
                }

                return Track(rewritten, symbol);
            }

            return rewritten;
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitElementAccessExpression(ElementAccessExpressionSyntax node)
        {
            var rewritten = (ExpressionSyntax)base.VisitElementAccessExpression(node)!;
            return model.GetSymbolInfo(node).Symbol is IPropertySymbol property ? Track(rewritten, property) : rewritten;
        }

        /// <summary>Checks every specialized access still selects its original declaration.</summary>
        /// <param name="specializedModel">The original-compilation speculative model.</param>
        /// <param name="expression">The specialized expression.</param>
        /// <returns>Whether member identity and body binding are preserved.</returns>
        internal bool PreservesDispatch(SemanticModel specializedModel, ExpressionSyntax expression)
        {
            foreach (var member in _members)
            {
                var access = expression.GetAnnotatedNodes(member.Key).Single();
                var selected = specializedModel.GetSymbolInfo(access).Symbol;
                if (!SymbolEqualityComparer.Default.Equals(member.Value.OriginalDefinition, selected?.OriginalDefinition))
                {
                    return false;
                }
            }

            return PreservesOperations(specializedModel, expression) && !HasInvalidLambdas(specializedModel, expression);
        }

        /// <summary>Checks that the specialized lambda bodies bind completely.</summary>
        /// <param name="model">The specialized semantic model.</param>
        /// <param name="expression">The specialized expression.</param>
        /// <returns>Whether any specialized body contains an invalid operation.</returns>
        private static bool HasInvalidLambdas(SemanticModel model, ExpressionSyntax expression)
        {
            foreach (var lambda in expression.DescendantNodesAndSelf().OfType<LambdaExpressionSyntax>())
            {
                if (lambda.Body is ExpressionSyntax body && model.GetOperation(body) is { } operation
                    && HasInvalidOperation(operation))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Identifies generic reference comparisons that closing to string must not reinterpret.</summary>
        /// <param name="operation">The original binary operation.</param>
        /// <returns>Whether the comparison requires reference operands.</returns>
        private static bool UsesGenericReferenceEquality(IBinaryOperation operation) =>
            operation.OperatorKind is BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals
            && (operation.LeftOperand.Type is ITypeParameterSymbol || operation.RightOperand.Type is ITypeParameterSymbol)
            && operation.LeftOperand.Type?.IsReferenceType == true && operation.RightOperand.Type?.IsReferenceType == true;

        /// <summary>Finds a failed binding without discarding nested conversions.</summary>
        /// <param name="operation">The specialized operation.</param>
        /// <returns>Whether any operation is invalid.</returns>
        private static bool HasInvalidOperation(IOperation operation) => operation.Kind == OperationKind.Invalid
            || operation.ChildOperations.Any(HasInvalidOperation);

        /// <summary>Reads explicit and implicit operator dispatch, including the absence of a user operator.</summary>
        /// <param name="operation">The original or specialized operation.</param>
        /// <returns>The selected operator method, or null for a built-in operation.</returns>
        private static IMethodSymbol? Operator(IOperation operation) => operation switch
        {
            IBinaryOperation binary => binary.OperatorMethod,
            IUnaryOperation unary => unary.OperatorMethod,
            IConversionOperation conversion => conversion.OperatorMethod,
            _ => null,
        };

        /// <summary>Checks original dispatch, nullable lifting and checked arithmetic after specialization.</summary>
        /// <param name="original">The declaration's operation.</param>
        /// <param name="rebound">The specialized operation.</param>
        /// <returns>Whether executable operator semantics were retained.</returns>
        private static bool SameSemantics(IOperation original, IOperation rebound) =>
            SymbolEqualityComparer.Default.Equals(Operator(original)?.OriginalDefinition, Operator(rebound)?.OriginalDefinition)
            && SameOperationFlags(original, rebound);

        /// <summary>Checks nullable lifting and checked flags separately from selected operator identity.</summary>
        /// <param name="original">The declaration's operation.</param>
        /// <param name="rebound">The specialized operation.</param>
        /// <returns>Whether operation flags and kinds are retained.</returns>
        private static bool SameOperationFlags(IOperation original, IOperation rebound) => original switch
        {
            IBinaryOperation binary => SameBinaryFlags(binary, rebound),
            IUnaryOperation unary => SameUnaryFlags(unary, rebound),
            IConversionOperation conversion => rebound is IConversionOperation selected && conversion.IsChecked == selected.IsChecked,
            _ => true,
        };

        /// <summary>Checks the original binary operator kind and its checked and nullable lifting flags.</summary>
        /// <param name="original">The declaration's binary operation.</param>
        /// <param name="rebound">The specialized operation.</param>
        /// <returns>Whether binary flags were retained.</returns>
        private static bool SameBinaryFlags(IBinaryOperation original, IOperation rebound) => rebound is IBinaryOperation selected
            && original.IsChecked == selected.IsChecked && original.IsLifted == selected.IsLifted && original.OperatorKind == selected.OperatorKind;

        /// <summary>Checks the original unary operator kind and its checked and nullable lifting flags.</summary>
        /// <param name="original">The declaration's unary operation.</param>
        /// <param name="rebound">The specialized operation.</param>
        /// <returns>Whether unary flags were retained.</returns>
        private static bool SameUnaryFlags(IUnaryOperation original, IOperation rebound) => rebound is IUnaryOperation selected
            && original.IsChecked == selected.IsChecked && original.IsLifted == selected.IsLifted && original.OperatorKind == selected.OperatorKind;

        /// <summary>Records conversions in inner-to-outer order at their original expression syntax.</summary>
        /// <param name="semantic">The original selector's semantic model.</param>
        /// <param name="source">The finite source definition.</param>
        /// <returns>The contextual conversion map.</returns>
        private static Dictionary<SyntaxNode, List<IConversionOperation>> CollectConversions(SemanticModel semantic, ExpressionSyntax source)
        {
            var conversions = new Dictionary<SyntaxNode, List<IConversionOperation>>();
            foreach (var lambda in source.DescendantNodesAndSelf().OfType<LambdaExpressionSyntax>())
            {
                var operation = lambda.Body is ExpressionSyntax body ? semantic.GetOperation(body) : null;
                if (operation is null)
                {
                    continue;
                }

                while (operation is not IAnonymousFunctionOperation && operation.Parent is { } parent && lambda.Span.Contains(parent.Syntax.Span))
                {
                    operation = parent;
                }

                Collect(operation, conversions);
            }

            return conversions;
        }

        /// <summary>Retains contextual conversions before reconstructing their typed syntax.</summary>
        /// <param name="operation">The original operation tree.</param>
        /// <param name="conversions">The accumulated expression conversions.</param>
        private static void Collect(IOperation operation, Dictionary<SyntaxNode, List<IConversionOperation>> conversions)
        {
            foreach (var child in operation.ChildOperations)
            {
                Collect(child, conversions);
            }

            if (operation is not IConversionOperation { IsImplicit: true } conversion || conversion.Syntax is not ExpressionSyntax)
            {
                return;
            }

            if (!conversions.TryGetValue(conversion.Syntax, out var values))
            {
                values = [];
                conversions.Add(conversion.Syntax, values);
            }

            if (!values.Contains(conversion))
            {
                values.Add(conversion);
            }
        }

        /// <summary>Applies an ordinary typed cast without changing surrounding checked context.</summary>
        /// <param name="expression">The original rewritten operand.</param>
        /// <param name="type">The pinned actual contract.</param>
        /// <returns>The parenthesized typed operand.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ParenthesizedExpressionSyntax Cast(ExpressionSyntax expression, ITypeSymbol type) => SyntaxFactory.ParenthesizedExpression(
            SyntaxFactory.CastExpression(SyntaxFactory.ParseTypeName(GeneratorHelpers.TypeName(type)), SyntaxFactory.ParenthesizedExpression(expression)));

        /// <summary>Leaves a whole lambda-body cast directly bindable without removing operand parentheses.</summary>
        /// <param name="original">The original source expression.</param>
        /// <param name="expression">The reconstructed expression.</param>
        /// <returns>The directly bindable lambda body or the unchanged nested expression.</returns>
        private static ExpressionSyntax LambdaBodySyntax(ExpressionSyntax original, ExpressionSyntax expression) =>
            original.Parent is LambdaExpressionSyntax && expression is ParenthesizedExpressionSyntax body
                ? body.Expression
                : expression;

        /// <summary>Checks annotated operator and constant semantics independently of member access.</summary>
        /// <param name="specializedModel">The specialized semantic model.</param>
        /// <param name="expression">The specialized expression.</param>
        /// <returns>Whether every original operator and constant was retained.</returns>
        private bool PreservesOperations(SemanticModel specializedModel, ExpressionSyntax expression)
        {
            foreach (var original in _operations)
            {
                var syntax = expression.GetAnnotatedNodes(original.Key).Single();
                var rebound = specializedModel.GetOperation(syntax);
                if (rebound is null || !SameSemantics(original.Value, rebound)
                    || (original.Value.ConstantValue.HasValue && (!rebound.ConstantValue.HasValue || !Equals(original.Value.ConstantValue.Value, rebound.ConstantValue.Value))))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Retains original operation proofs and restores contextual conversions in evaluation order.</summary>
        /// <param name="original">The original source expression.</param>
        /// <param name="expression">The rewritten expression.</param>
        /// <returns>The expression with its original contextual conversion contracts.</returns>
        private ExpressionSyntax BindOperations(ExpressionSyntax original, ExpressionSyntax expression)
        {
            var operation = SourceOperation(original);
            if (operation is IBinaryOperation or IUnaryOperation or IConversionOperation || operation?.ConstantValue.HasValue == true)
            {
                expression = TrackOperation(expression, operation!);
            }

            if (!_implicitConversions.TryGetValue(original, out var conversions))
            {
                return expression;
            }

            foreach (var conversion in conversions)
            {
                var operand = conversion.OperatorMethod is { } method ? Cast(expression, OperandType(method.Parameters[0].Type, conversion.Operand.Type, true)) : expression;
                expression = TrackOperation(Cast(operand, Substitute(conversion.Type!)), conversion);
            }

            return LambdaBodySyntax(original, expression);
        }

        /// <summary>Reads the source operation before contextual conversions that are reconstructed separately.</summary>
        /// <param name="syntax">The original expression.</param>
        /// <returns>The explicitly authored operation.</returns>
        private IOperation? SourceOperation(ExpressionSyntax syntax)
        {
            var operation = model.GetOperation(syntax);
            while (operation is IConversionOperation { IsImplicit: true } conversion)
            {
                operation = conversion.Operand;
            }

            return operation;
        }

        /// <summary>Preserves nullable lifting while pinning an operator's actual formal operand contract.</summary>
        /// <param name="formal">The original operator parameter type.</param>
        /// <param name="operand">The original operand type.</param>
        /// <param name="lifted">Whether nullable lifting is permitted.</param>
        /// <returns>The specialized operand type.</returns>
        private ITypeSymbol OperandType(ITypeSymbol formal, ITypeSymbol? operand, bool lifted)
        {
            var type = Substitute(formal);
            return lifted && type.IsValueType && type.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T
                && operand is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T }
                ? model.Compilation.GetSpecialType(SpecialType.System_Nullable_T).Construct(type)
                : type;
        }

        /// <summary>Retains the original operator or constant proof on specialized syntax.</summary>
        /// <param name="expression">The specialized expression.</param>
        /// <param name="operation">Its original semantic operation.</param>
        /// <returns>The annotated specialized expression.</returns>
        private ExpressionSyntax TrackOperation(ExpressionSyntax expression, IOperation operation)
        {
            if (expression is ParenthesizedExpressionSyntax parenthesized)
            {
                return parenthesized.WithExpression(TrackOperation(parenthesized.Expression, operation));
            }

            var annotation = new SyntaxAnnotation();
            _operations.Add(annotation, operation);
            return expression.WithAdditionalAnnotations(annotation);
        }

        /// <summary>Attaches a source member identity to the rewritten access.</summary>
        /// <param name="expression">The specialized access.</param>
        /// <param name="member">The original bound member.</param>
        /// <returns>The annotated specialized access.</returns>
        private ExpressionSyntax Track(ExpressionSyntax expression, ISymbol member)
        {
            var annotation = new SyntaxAnnotation();
            _members.Add(annotation, member);
            return expression.WithAdditionalAnnotations(annotation);
        }

        /// <summary>Substitutes actual arguments through nested types, arrays and nullable contracts.</summary>
        /// <param name="type">The original semantic type.</param>
        /// <returns>The type constructed with the selected declaration slots.</returns>
        private ITypeSymbol Substitute(ITypeSymbol type)
        {
            if (type is ITypeParameterSymbol parameter && arguments.TryGetValue(parameter, out var actual))
            {
                return type.NullableAnnotation == NullableAnnotation.Annotated ? actual.WithNullableAnnotation(NullableAnnotation.Annotated) : actual;
            }

            if (type is IArrayTypeSymbol array)
            {
                return model.Compilation.CreateArrayTypeSymbol(Substitute(array.ElementType), array.Rank).WithNullableAnnotation(type.NullableAnnotation);
            }

            if (type is IPointerTypeSymbol pointer)
            {
                return model.Compilation.CreatePointerTypeSymbol(Substitute(pointer.PointedAtType));
            }

            if (type is INamedTypeSymbol named)
            {
                var declaration = named.ContainingType is null
                    ? named.OriginalDefinition
                    : ((INamedTypeSymbol)Substitute(named.ContainingType)).GetTypeMembers(named.Name, named.Arity).Single();
                return (named.Arity == 0 ? declaration : declaration.Construct(named.TypeArguments.Select(Substitute).ToArray())).WithNullableAnnotation(type.NullableAnnotation);
            }

            return type;
        }
    }
}
