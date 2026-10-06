// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Creates finite structural matchers without expression execution or reflective member access.</summary>
public static class ValidationExpressionPattern
{
    /// <summary>Creates a matcher for one known expression shape and explicit argument positions.</summary>
    /// <typeparam name="TSource">The source API type.</typeparam>
    /// <typeparam name="TValue">The selected value type.</typeparam>
    /// <param name="exemplar">The known parameter, member, conversion, indexer and exact method-call shape.</param>
    /// <param name="bindings">The bounded literal or explicitly typed argument extractors.</param>
    /// <returns>A pure matcher retaining only its finite schema.</returns>
    /// <exception cref="ArgumentException">A binding is missing from the exemplar, duplicates a token or overlaps another binding.</exception>
    /// <remarks>
    /// Unbound reference constants, including compiler closures, match by object identity. Unknown nodes and unbound custom value constants fail closed.
    /// Exact registered method calls match their closed method identity, receiver and ordered argument schema.
    /// Matching does not execute any selected getters, methods or subscriptions.
    /// </remarks>
    public static ValidationExpressionMatcher<TSource, TValue> Create<TSource, TValue>(Expression<Func<TSource, TValue>> exemplar, params ValidationExpressionBinding[] bindings)
    {
        ArgumentExceptionHelper.ThrowIfNull(exemplar);
        ArgumentExceptionHelper.ThrowIfNull(bindings);
        var snapshot = (ValidationExpressionBinding[])bindings.Clone();
        foreach (var binding in snapshot)
        {
            ArgumentExceptionHelper.ThrowIfNull(binding);
        }

        var positions = new HashSet<Expression>(ReferenceEqualityComparer.Instance);
        var tokens = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var binding in snapshot)
        {
            ArgumentExceptionHelper.ThrowIfNull(binding);
            if (!positions.Add(binding.Exemplar) || !tokens.Add(binding.Token) || !Contains(exemplar.Body, binding.Exemplar))
            {
                throw new ArgumentException("Every binding must identify a distinct subtree in the exemplar.", nameof(bindings));
            }

            foreach (var other in snapshot)
            {
                if (!ReferenceEquals(binding, other) && Contains(binding.Exemplar, other.Exemplar))
                {
                    throw new ArgumentException("Argument bindings must not overlap.", nameof(bindings));
                }
            }
        }

        return Match;

        bool Match(Expression<Func<TSource, TValue>> expression, [NotNullWhen(true)] out ValidationArguments? arguments)
        {
            ArgumentExceptionHelper.ThrowIfNull(expression);
            var payload = new ValidationArguments();
            if (Same(exemplar.Body, expression.Body, exemplar.Parameters[0], expression.Parameters[0], snapshot, ref payload))
            {
                arguments = payload;
                return true;
            }

            arguments = null;
            return false;
        }
    }

    /// <summary>Matches one known subtree and its explicit argument bindings.</summary>
    /// <param name="expected">The expected contract.</param>
    /// <param name="actual">The actual contract.</param>
    /// <param name="expectedParameter">The expected parameter contract.</param>
    /// <param name="actualParameter">The actual parameter contract.</param>
    /// <param name="bindings">The bindings contract.</param>
    /// <param name="payload">The payload contract.</param>
    /// <returns>The current typed result.</returns>
    private static bool Same(
        Expression expected,
        Expression actual,
        ParameterExpression expectedParameter,
        ParameterExpression actualParameter,
        ValidationExpressionBinding[] bindings,
        ref ValidationArguments payload)
    {
        if (expected.NodeType != actual.NodeType || expected.Type != actual.Type)
        {
            return false;
        }

        foreach (var binding in bindings)
        {
            if (ReferenceEquals(binding.Exemplar, expected))
            {
                return binding.TryExtract(actual, payload, out payload);
            }
        }

        return SameNode(expected, actual, expectedParameter, actualParameter, bindings, ref payload);
    }

    /// <summary>Matches only the supported finite node kinds.</summary>
    /// <param name="expected">The expected contract.</param>
    /// <param name="actual">The actual contract.</param>
    /// <param name="expectedParameter">The expected parameter contract.</param>
    /// <param name="actualParameter">The actual parameter contract.</param>
    /// <param name="bindings">The bindings contract.</param>
    /// <param name="payload">The payload contract.</param>
    /// <returns>The current typed result.</returns>
    private static bool SameNode(
        Expression expected,
        Expression actual,
        ParameterExpression expectedParameter,
        ParameterExpression actualParameter,
        ValidationExpressionBinding[] bindings,
        ref ValidationArguments payload) =>
        (expected, actual) switch
        {
            (ParameterExpression left, ParameterExpression right) => ReferenceEquals(left, expectedParameter) && ReferenceEquals(right, actualParameter),
            (MemberExpression left, MemberExpression right) => SameMember(left, right, expectedParameter, actualParameter, bindings, ref payload),
            (ConstantExpression left, ConstantExpression right) => SameConstant(left.Value, right.Value),
            (UnaryExpression left, UnaryExpression right) => SameConversion(left, right, expectedParameter, actualParameter, bindings, ref payload),
            (IndexExpression left, IndexExpression right) => SameIndex(left, right, expectedParameter, actualParameter, bindings, ref payload),
            (MethodCallExpression left, MethodCallExpression right) => SameCall(left, right, expectedParameter, actualParameter, bindings, ref payload),
            (BinaryExpression left, BinaryExpression right) => SameArray(left, right, expectedParameter, actualParameter, bindings, ref payload),
            _ => false,
        };

    /// <summary>Matches corresponding optional receiver subtrees.</summary>
    /// <param name="expected">The expected contract.</param>
    /// <param name="actual">The actual contract.</param>
    /// <param name="expectedParameter">The expected parameter contract.</param>
    /// <param name="actualParameter">The actual parameter contract.</param>
    /// <param name="bindings">The bindings contract.</param>
    /// <param name="payload">The payload contract.</param>
    /// <returns>The current typed result.</returns>
    private static bool SameOptional(
        Expression? expected,
        Expression? actual,
        ParameterExpression expectedParameter,
        ParameterExpression actualParameter,
        ValidationExpressionBinding[] bindings,
        ref ValidationArguments payload) => expected is null ? actual is null : actual is not null && Same(expected, actual, expectedParameter, actualParameter, bindings, ref payload);

    /// <summary>Matches the ordered indexer argument schema.</summary>
    /// <param name="expected">The expected contract.</param>
    /// <param name="actual">The actual contract.</param>
    /// <param name="expectedParameter">The expected parameter contract.</param>
    /// <param name="actualParameter">The actual parameter contract.</param>
    /// <param name="bindings">The bindings contract.</param>
    /// <param name="payload">The payload contract.</param>
    /// <returns>The current typed result.</returns>
    private static bool SameArguments(
        ReadOnlyCollection<Expression> expected,
        ReadOnlyCollection<Expression> actual,
        ParameterExpression expectedParameter,
        ParameterExpression actualParameter,
        ValidationExpressionBinding[] bindings,
        ref ValidationArguments payload)
    {
        if (expected.Count != actual.Count)
        {
            return false;
        }

        for (var index = 0; index < expected.Count; index++)
        {
            if (!Same(expected[index], actual[index], expectedParameter, actualParameter, bindings, ref payload))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Compares supported literal constants without invoking custom value equality.</summary>
    /// <param name="expected">The expected contract.</param>
    /// <param name="actual">The actual contract.</param>
    /// <returns>The current typed result.</returns>
    private static bool SameConstant(object? expected, object? actual) => ReferenceEquals(expected, actual) || (expected, actual) switch
    {
        (string left, string right) => string.Equals(left, right, StringComparison.Ordinal),
        (bool left, bool right) => left == right,
        (char left, char right) => left == right,
        (byte left, byte right) => left == right,
        (sbyte left, sbyte right) => left == right,
        (short left, short right) => left == right,
        (ushort left, ushort right) => left == right,
        (int left, int right) => left == right,
        (uint left, uint right) => left == right,
        (long left, long right) => left == right,
        (ulong left, ulong right) => left == right,
        (float left, float right) => BitConverter.SingleToInt32Bits(left) == BitConverter.SingleToInt32Bits(right),
        (double left, double right) => BitConverter.DoubleToInt64Bits(left) == BitConverter.DoubleToInt64Bits(right),
        (decimal left, decimal right) => left == right,
        _ => false,
    };

    /// <summary>Checks whether an exemplar contains an exact known subtree position.</summary>
    /// <param name="root">The root contract.</param>
    /// <param name="candidate">The candidate contract.</param>
    /// <returns>The current typed result.</returns>
    private static bool Contains(Expression root, Expression candidate)
    {
        if (ReferenceEquals(root, candidate))
        {
            return true;
        }

        return root switch
        {
            MemberExpression member => member.Expression is not null && Contains(member.Expression, candidate),
            UnaryExpression unary => Contains(unary.Operand, candidate),
            IndexExpression index => (index.Object is not null && Contains(index.Object, candidate)) || ContainsArgument(index.Arguments, candidate),
            MethodCallExpression call => (call.Object is not null && Contains(call.Object, candidate)) || ContainsArgument(call.Arguments, candidate),
            BinaryExpression binary => Contains(binary.Left, candidate) || Contains(binary.Right, candidate),
            _ => false,
        };
    }

    /// <summary>Checks the explicitly supported conversion node kinds.</summary>
    /// <param name="nodeType">The node type contract.</param>
    /// <returns>The current typed result.</returns>
    private static bool IsConversion(ExpressionType nodeType) => nodeType is ExpressionType.Convert or ExpressionType.ConvertChecked or ExpressionType.TypeAs;

    /// <summary>Checks known argument positions for an exact subtree.</summary>
    /// <param name="arguments">The arguments contract.</param>
    /// <param name="candidate">The candidate contract.</param>
    /// <returns>The current typed result.</returns>
    private static bool ContainsArgument(ReadOnlyCollection<Expression> arguments, Expression candidate)
    {
        foreach (var argument in arguments)
        {
            if (Contains(argument, candidate))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Matches the known MemberExpression subtree identity.</summary>
    /// <param name="left">The exemplar node.</param>
    /// <param name="right">The current node.</param>
    /// <param name="expectedParameter">The exemplar source parameter.</param>
    /// <param name="actualParameter">The current source parameter.</param>
    /// <param name="bindings">The finite argument contracts.</param>
    /// <param name="payload">The invocation-local argument payload.</param>
    /// <returns>Whether the subtree matches.</returns>
    private static bool SameMember(
        MemberExpression left,
        MemberExpression right,
        ParameterExpression expectedParameter,
        ParameterExpression actualParameter,
        ValidationExpressionBinding[] bindings,
        ref ValidationArguments payload) =>
        Equals(left.Member, right.Member) && SameOptional(left.Expression, right.Expression, expectedParameter, actualParameter, bindings, ref payload);

    /// <summary>Matches the known UnaryExpression subtree identity.</summary>
    /// <param name="left">The exemplar node.</param>
    /// <param name="right">The current node.</param>
    /// <param name="expectedParameter">The exemplar source parameter.</param>
    /// <param name="actualParameter">The current source parameter.</param>
    /// <param name="bindings">The finite argument contracts.</param>
    /// <param name="payload">The invocation-local argument payload.</param>
    /// <returns>Whether the subtree matches.</returns>
    private static bool SameConversion(
        UnaryExpression left,
        UnaryExpression right,
        ParameterExpression expectedParameter,
        ParameterExpression actualParameter,
        ValidationExpressionBinding[] bindings,
        ref ValidationArguments payload) =>
        IsConversion(left.NodeType) && Equals(left.Method, right.Method) && Same(left.Operand, right.Operand, expectedParameter, actualParameter, bindings, ref payload);

    /// <summary>Matches the known IndexExpression subtree identity.</summary>
    /// <param name="left">The exemplar node.</param>
    /// <param name="right">The current node.</param>
    /// <param name="expectedParameter">The exemplar source parameter.</param>
    /// <param name="actualParameter">The current source parameter.</param>
    /// <param name="bindings">The finite argument contracts.</param>
    /// <param name="payload">The invocation-local argument payload.</param>
    /// <returns>Whether the subtree matches.</returns>
    private static bool SameIndex(
        IndexExpression left,
        IndexExpression right,
        ParameterExpression expectedParameter,
        ParameterExpression actualParameter,
        ValidationExpressionBinding[] bindings,
        ref ValidationArguments payload) =>
        Equals(left.Indexer, right.Indexer)
            && SameOptional(left.Object, right.Object, expectedParameter, actualParameter, bindings, ref payload)
            && SameArguments(left.Arguments, right.Arguments, expectedParameter, actualParameter, bindings, ref payload);

    /// <summary>Matches the known MethodCallExpression subtree identity.</summary>
    /// <param name="left">The exemplar node.</param>
    /// <param name="right">The current node.</param>
    /// <param name="expectedParameter">The exemplar source parameter.</param>
    /// <param name="actualParameter">The current source parameter.</param>
    /// <param name="bindings">The finite argument contracts.</param>
    /// <param name="payload">The invocation-local argument payload.</param>
    /// <returns>Whether the subtree matches.</returns>
    private static bool SameCall(
        MethodCallExpression left,
        MethodCallExpression right,
        ParameterExpression expectedParameter,
        ParameterExpression actualParameter,
        ValidationExpressionBinding[] bindings,
        ref ValidationArguments payload) =>
        Equals(left.Method, right.Method)
            && SameOptional(left.Object, right.Object, expectedParameter, actualParameter, bindings, ref payload)
            && SameArguments(left.Arguments, right.Arguments, expectedParameter, actualParameter, bindings, ref payload);

    /// <summary>Matches the known BinaryExpression subtree identity.</summary>
    /// <param name="left">The exemplar node.</param>
    /// <param name="right">The current node.</param>
    /// <param name="expectedParameter">The exemplar source parameter.</param>
    /// <param name="actualParameter">The current source parameter.</param>
    /// <param name="bindings">The finite argument contracts.</param>
    /// <param name="payload">The invocation-local argument payload.</param>
    /// <returns>Whether the subtree matches.</returns>
    private static bool SameArray(
        BinaryExpression left,
        BinaryExpression right,
        ParameterExpression expectedParameter,
        ParameterExpression actualParameter,
        ValidationExpressionBinding[] bindings,
        ref ValidationArguments payload) =>
        left.NodeType == ExpressionType.ArrayIndex
            && Same(left.Left, right.Left, expectedParameter, actualParameter, bindings, ref payload)
            && Same(left.Right, right.Right, expectedParameter, actualParameter, bindings, ref payload);
}
