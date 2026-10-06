// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Linq.Expressions;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>An explicit argument position and its typed extraction contract.</summary>
[DebuggerDisplay("Node = {Exemplar.NodeType}")]
public abstract class ValidationExpressionBinding
{
    /// <summary>Initializes a new instance of the ValidationExpressionBinding class.</summary>
    /// <param name="exemplar">The exemplar contract.</param>
    private protected ValidationExpressionBinding(Expression exemplar)
    {
        ArgumentExceptionHelper.ThrowIfNull(exemplar);
        Exemplar = exemplar;
    }

    /// <summary>Gets the exact subtree position in the finite schema.</summary>
    internal Expression Exemplar { get; }

    /// <summary>Gets the typed argument identity token.</summary>
    internal abstract object Token { get; }

    /// <summary>Creates a binding for a literal constant at a known argument position.</summary>
    /// <typeparam name="T">The exact constant type.</typeparam>
    /// <param name="exemplar">The constant node in the schema's exemplar.</param>
    /// <param name="argument">The typed payload token.</param>
    /// <returns>The finite argument binding.</returns>
    /// <exception cref="ArgumentException">The exemplar constant and token have different types.</exception>
    public static ValidationExpressionBinding Constant<T>(ConstantExpression exemplar, ValidationArgument<T> argument)
    {
        ArgumentExceptionHelper.ThrowIfNull(exemplar);
        ArgumentExceptionHelper.ThrowIfNull(argument);
        if (exemplar.Type != typeof(T))
        {
            throw new ArgumentException("The constant and argument token must have the same type.", nameof(exemplar));
        }

        return new Binding<T>(exemplar, argument, ExtractConstant<T>);
    }

    /// <summary>Creates a binding with a caller-authored typed extractor for a known subtree.</summary>
    /// <typeparam name="T">The extracted argument type.</typeparam>
    /// <param name="exemplar">The exact subtree position in the schema's exemplar.</param>
    /// <param name="argument">The typed payload token.</param>
    /// <param name="extractor">A pure matcher that checks all supported identities before typed access.</param>
    /// <returns>The finite argument binding.</returns>
    /// <remarks>The extractor must reject unknown closure types and members; it must not compile expressions or discover members reflectively.</remarks>
    public static ValidationExpressionBinding Extract<T>(Expression exemplar, ValidationArgument<T> argument, ValidationArgumentExtractor<T> extractor)
    {
        ArgumentExceptionHelper.ThrowIfNull(argument);
        ArgumentExceptionHelper.ThrowIfNull(extractor);
        return new Binding<T>(exemplar, argument, extractor);
    }

    /// <summary>Extracts one explicitly supported typed argument.</summary>
    /// <param name="expression">The expression contract.</param>
    /// <param name="arguments">The arguments contract.</param>
    /// <param name="result">The result contract.</param>
    /// <returns>The current typed result.</returns>
    internal abstract bool TryExtract(Expression expression, ValidationArguments arguments, out ValidationArguments result);

    /// <summary>Reads a typed constant without executing the expression.</summary>
    /// <typeparam name="T">The argument type.</typeparam>
    /// <param name="expression">The expression contract.</param>
    /// <param name="value">The value contract.</param>
    /// <returns>The current typed result.</returns>
    private static bool ExtractConstant<T>(Expression expression, out T value)
    {
        if (expression is ConstantExpression constant && constant.Type == typeof(T))
        {
            if (constant.Value is T typed)
            {
                value = typed;
                return true;
            }

            if (constant.Value is null && default(T) is null)
            {
                value = default!;
                return true;
            }
        }

        value = default!;
        return false;
    }

    /// <summary>Extracts a value using a finite typed subtree contract.</summary>
    /// <typeparam name="T">The argument type.</typeparam>
    /// <param name="exemplar">The exemplar contract.</param>
    /// <param name="argument">The argument contract.</param>
    /// <param name="extractor">The extractor contract.</param>
    private sealed class Binding<T>(Expression exemplar, ValidationArgument<T> argument, ValidationArgumentExtractor<T> extractor) : ValidationExpressionBinding(exemplar)
    {
        /// <inheritdoc/>
        internal override object Token => argument;

        /// <inheritdoc/>
        internal override bool TryExtract(Expression expression, ValidationArguments arguments, out ValidationArguments result)
        {
            if (expression.NodeType == Exemplar.NodeType && expression.Type == Exemplar.Type && extractor(expression, out var value))
            {
                result = argument.AddTo(arguments, value);
                return true;
            }

            result = arguments;
            return false;
        }
    }
}
