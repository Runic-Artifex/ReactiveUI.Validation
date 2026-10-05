// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Extensions;
#else
namespace ReactiveUI.Validation.Extensions;
#endif

/// <summary>Projects validation through caller-created source streams and ordinary typed callbacks.</summary>
public static class ObservableValidationBindingExtensions
{
    /// <summary>Provides validation binding for explicit model, helper, or context selections.</summary>
    /// <typeparam name="TSource">The selected source type.</typeparam>
    /// <param name="sources">Emits the initial source and every replacement, including null.</param>
    extension<TSource>(IObservable<TSource?> sources)
        where TSource : class
    {
        /// <summary>Projects unchanged states from the latest selected source.</summary>
        /// <typeparam name="TOut">The callback value type.</typeparam>
        /// <param name="selectStates">Selects a helper, context, or other state stream without reflection.</param>
        /// <param name="converter">Projects complete states, preserving validity independent of text.</param>
        /// <param name="onNext">Assigns the typed result on the source notification thread.</param>
        /// <returns>An idempotently disposable binding that detaches replaced sources.</returns>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        /// <remarks>
        /// Null sources or selected streams emit <see cref="ValidationState.Valid"/>.
        /// The outer stream must report helper/context changes; delegates do not observe properties.
        /// Selected non-null streams must supply an initial state. Dispatch UI updates in the callback when required.
        /// </remarks>
        public IValidationBinding BindObservableValidationState<TOut>(Func<TSource, IObservable<IValidationState>?> selectStates, Func<IValidationState, TOut> converter, Action<TOut> onNext)
        {
            ArgumentExceptionHelper.ThrowIfNull(sources);
            ArgumentExceptionHelper.ThrowIfNull(selectStates);
            ArgumentExceptionHelper.ThrowIfNull(converter);
            ArgumentExceptionHelper.ThrowIfNull(onNext);
            var states = sources.Select(source => source is null ? null : selectStates(source))
                .Select(static stream => stream ?? Observable.Return(ValidationState.Valid))
                .SwitchTo();
            return new ValidationStateBinding(states.Do(state => onNext(converter(state))).Select(static _ => Unit.Default));
        }

        /// <summary>Projects all actual matching rule states from the latest selected context.</summary>
        /// <typeparam name="TOut">The callback value type.</typeparam>
        /// <param name="selectContext">Selects the current context without reflection.</param>
        /// <param name="propertyPath">The complete property path, matched ordinally.</param>
        /// <param name="converter">Projects matching states; treat the list as read-only.</param>
        /// <param name="onNext">Assigns the typed result on the source notification thread.</param>
        /// <param name="strict">If true, includes only rules exclusively associated with this one full path.</param>
        /// <returns>An idempotently disposable binding that detaches replaced contexts and rules.</returns>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        /// <exception cref="ArgumentException">The property path is malformed.</exception>
        /// <remarks>
        /// Null selections and contexts with no matching rules emit an empty list.
        /// Active rules must supply initial states; no synthetic valid state precedes them.
        /// The outer stream must report context replacement. No property observation or target assignment is discovered by the library.
        /// </remarks>
        public IValidationBinding BindObservablePropertyValidationState<TOut>(
            Func<TSource, IValidationContext?> selectContext,
            string propertyPath,
            Func<IList<IValidationState>, TOut> converter,
            Action<TOut> onNext,
            bool strict)
        {
            ArgumentExceptionHelper.ThrowIfNull(sources);
            ArgumentExceptionHelper.ThrowIfNull(selectContext);
            ObservablePropertyValidation.ValidatePath(propertyPath);
            ArgumentExceptionHelper.ThrowIfNull(converter);
            ArgumentExceptionHelper.ThrowIfNull(onNext);
            var states = sources.Select(source => source is null ? null : selectContext(source))
                .Select(context => context is null
                    ? Observable.Return<IList<IValidationState>>(Array.Empty<IValidationState>())
                    : context.Validations.Connect().ToCollection()
                        .StartWith(new List<IValidationComponent>(context.Validations.Items))
                        .Select(rules => ObserveRules(rules, propertyPath, strict)).SwitchTo())
                .SwitchTo();
            return new ValidationStateBinding(states.Do(state => onNext(converter(state))).Select(static _ => Unit.Default));
        }
    }

    /// <summary>Combines matching states without equality or text-based filtering.</summary>
    /// <param name="rules">The current membership snapshot.</param>
    /// <param name="propertyPath">The complete property path.</param>
    /// <param name="strict">Whether only exclusive rules match.</param>
    /// <returns>The raw latest states, or an empty list when no rules match.</returns>
    private static IObservable<IList<IValidationState>> ObserveRules(IReadOnlyCollection<IValidationComponent> rules, string propertyPath, bool strict)
    {
        List<IObservable<IValidationState>> streams = [];
        foreach (var rule in rules)
        {
            if (rule is IPropertyValidationComponent propertyRule && propertyRule.ContainsPropertyName(propertyPath, strict))
            {
                streams.Add(propertyRule.ValidationStatusChange);
            }
        }

        return streams.Count == 0 ? Observable.Return<IList<IValidationState>>(Array.Empty<IValidationState>()) : streams.CombineLatest();
    }
}
