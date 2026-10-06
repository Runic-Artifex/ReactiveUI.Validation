// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.ValidationBindings;
#else
namespace ReactiveUI.Validation.ValidationBindings;
#endif

/// <summary>Owns typed validation assignments and their subscriptions.</summary>
internal sealed class ValidationStateBinding : IValidationBinding
{
    /// <summary>The active subscription.</summary>
    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Interlocked.Exchange transfers the subscription to the disposer atomically.")]
    private IDisposable? _subscription;

    /// <summary>Initializes a new instance of the <see cref="ValidationStateBinding"/> class.</summary>
    /// <param name="updates">The assignment stream.</param>
    internal ValidationStateBinding(IObservable<Unit> updates) => _subscription = SubscribeExtensions.Subscribe(updates);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Interlocked.Exchange(ref _subscription, null)?.Dispose();

    /// <summary>Assigns current values to the current property host, including replaced nested hosts.</summary>
    /// <typeparam name="TView">View type.</typeparam>
    /// <typeparam name="TOut">Presentation value type.</typeparam>
    /// <typeparam name="TTarget">Target instance type.</typeparam>
    /// <param name="values">Projected values.</param>
    /// <param name="target">Target view.</param>
    /// <param name="viewProperty">Target property.</param>
    /// <returns>The assignment subscription.</returns>
    [RequiresUnreferencedCode("Expression-based property assignment may reference trimmed members.")]
    internal static IValidationBinding BindToView<TView, TOut, TTarget>(IObservable<TOut> values, TTarget target, Expression<Func<TView, TOut>> viewProperty)
        where TTarget : class
    {
        var expression = Reflection.Rewrite(viewProperty.Body);
        var setter = Reflection.GetValueSetterOrThrow(expression.GetMemberInfo())!;
        var parent = expression.GetParent();
        var args = expression.GetArgumentsArray();
        var updates = parent?.NodeType == ExpressionType.Parameter
            ? values.Do(value => setter(target, value, args)).Select(static _ => Unit.Default)
            : values.CombineLatest(
                target.WhenAnyDynamic(parent, static change => change.Value),
                static (value, host) => (value, host))
                .Where(static update => update.host is not null)
                .Do(update => setter(update.host, update.value, args))
                .Select(static _ => Unit.Default);
        return new ValidationStateBinding(updates);
    }
}
