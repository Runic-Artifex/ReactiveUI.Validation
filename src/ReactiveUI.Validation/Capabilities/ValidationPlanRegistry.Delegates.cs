// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Registers and resolves bounded typed delegate capabilities alongside expression capabilities.</summary>
public sealed partial class ValidationPlanRegistry
{
    /// <summary>Registers a descriptor for one delegate identity and exact closed types.</summary>
    /// <typeparam name="TSource">The source API type.</typeparam>
    /// <typeparam name="TValue">The selected value type.</typeparam>
    /// <param name="role">The delegate's purpose.</param>
    /// <param name="selection">The supplied typed delegate, or null for an implicit model or default-context selector.</param>
    /// <param name="selector">The typed descriptor.</param>
    /// <returns>The registration lease.</returns>
    /// <exception cref="ArgumentNullException">A non-implicit registration has no selection.</exception>
    public IDisposable RegisterDelegateSelector<TSource, TValue>(ValidationPlanRole role, Func<TSource, TValue>? selection, ValidationSelector<TSource, TValue> selector)
    {
        if (selection is null && role is not (ValidationPlanRole.Model or ValidationPlanRole.DefaultContext))
        {
            throw new ArgumentNullException(nameof(selection), "Only an implicit model or default-context selector may register a null delegate.");
        }

        ArgumentExceptionHelper.ThrowIfNull(selector);
        return Add(new DelegateSelectorEntry<TSource, TValue>(role, selection, selector));
    }

    /// <summary>Registers a writable descriptor for one delegate identity and exact closed types.</summary>
    /// <typeparam name="TSource">The target source API type.</typeparam>
    /// <typeparam name="TValue">The selection's selected property type.</typeparam>
    /// <typeparam name="TOut">The assigned output type.</typeparam>
    /// <param name="role">The delegate's purpose.</param>
    /// <param name="selection">The supplied typed delegate.</param>
    /// <param name="target">The typed descriptor.</param>
    /// <returns>The registration lease.</returns>
    public IDisposable RegisterDelegateTarget<TSource, TValue, TOut>(ValidationPlanRole role, Func<TSource, TValue> selection, ValidationTarget<TSource, TOut> target)
    {
        ArgumentExceptionHelper.ThrowIfNull(selection);
        ArgumentExceptionHelper.ThrowIfNull(target);
        return Add(new DelegateTargetEntry<TSource, TValue, TOut>(role, selection, target));
    }

    /// <summary>Registers one finite delegate contract for fresh invocation-local selectors.</summary>
    /// <typeparam name="TSource">The source API type.</typeparam>
    /// <typeparam name="TValue">The selected value type.</typeparam>
    /// <param name="role">The delegate's purpose.</param>
    /// <param name="operationIdentity">The finite schema identity.</param>
    /// <param name="matcher">The pure typed schema matcher.</param>
    /// <param name="factory">Creates the descriptor without acquiring subscriptions, after ambiguity checks.</param>
    /// <returns>The registration lease.</returns>
    public IDisposable RegisterDelegateSelectorPattern<TSource, TValue>(
        ValidationPlanRole role,
        string operationIdentity,
        ValidationDelegateMatcher<TSource, TValue> matcher,
        Func<ValidationArguments, ValidationSelector<TSource, TValue>> factory)
    {
        ArgumentExceptionHelper.ThrowIfNullOrEmpty(operationIdentity);
        ArgumentExceptionHelper.ThrowIfNull(matcher);
        ArgumentExceptionHelper.ThrowIfNull(factory);
        return Add(new DelegateSelectorEntry<TSource, TValue>(role, operationIdentity, matcher, factory));
    }

    /// <summary>Registers one finite delegate contract for fresh invocation-local targets.</summary>
    /// <typeparam name="TSource">The target source API type.</typeparam>
    /// <typeparam name="TValue">The selection's selected property type.</typeparam>
    /// <typeparam name="TOut">The assigned output type.</typeparam>
    /// <param name="role">The delegate's purpose.</param>
    /// <param name="operationIdentity">The finite schema identity.</param>
    /// <param name="matcher">The pure typed schema matcher.</param>
    /// <param name="factory">Creates the descriptor without acquiring subscriptions, after ambiguity checks.</param>
    /// <returns>The registration lease.</returns>
    public IDisposable RegisterDelegateTargetPattern<TSource, TValue, TOut>(
        ValidationPlanRole role,
        string operationIdentity,
        ValidationDelegateMatcher<TSource, TValue> matcher,
        Func<ValidationArguments, ValidationTarget<TSource, TOut>> factory)
    {
        ArgumentExceptionHelper.ThrowIfNullOrEmpty(operationIdentity);
        ArgumentExceptionHelper.ThrowIfNull(matcher);
        ArgumentExceptionHelper.ThrowIfNull(factory);
        return Add(new DelegateTargetEntry<TSource, TValue, TOut>(role, operationIdentity, matcher, factory));
    }

    /// <inheritdoc/>
    public bool TryGetDelegateSelector<TSource, TValue>(
        ValidationPlanRequest request,
        Func<TSource, TValue>? selection,
        [NotNullWhen(true)] out ValidationSelector<TSource, TValue>? selector)
    {
        request.Validate();
        var matches = new List<(DelegateSelectorEntry<TSource, TValue> Entry, ValidationArguments? Arguments)>();
        foreach (var entry in Snapshot())
        {
            if (entry is not DelegateSelectorEntry<TSource, TValue> candidate)
            {
                continue;
            }

            if (!candidate.Match(request, selection, out var matched))
            {
                continue;
            }

            matches.Add((candidate, matched));
        }

        if (matches.Count > 1)
        {
            throw Ambiguous();
        }

        if (matches.Count == 0)
        {
            selector = null;
            return false;
        }

        var selected = matches[0];
        selector = selected.Entry.Create(selected.Arguments!);
        return true;
    }

    /// <inheritdoc/>
    public bool TryGetDelegateTarget<TSource, TValue, TOut>(
        ValidationPlanRequest request,
        Func<TSource, TValue>? selection,
        [NotNullWhen(true)] out ValidationTarget<TSource, TOut>? target)
    {
        request.Validate();
        var matches = new List<(DelegateTargetEntry<TSource, TValue, TOut> Entry, ValidationArguments? Arguments)>();
        foreach (var entry in Snapshot())
        {
            if (entry is not DelegateTargetEntry<TSource, TValue, TOut> candidate)
            {
                continue;
            }

            if (!candidate.Match(request, selection, out var matched))
            {
                continue;
            }

            matches.Add((candidate, matched));
        }

        if (matches.Count > 1)
        {
            throw Ambiguous();
        }

        if (matches.Count == 0)
        {
            target = null;
            return false;
        }

        var selected = matches[0];
        target = selected.Entry.Create(selected.Arguments!);
        return true;
    }

    /// <summary>Stores an exact closed selector registration.</summary>
    /// <typeparam name="TSource">The closed source type.</typeparam>
    /// <typeparam name="TValue">The closed value type.</typeparam>
    private sealed class DelegateSelectorEntry<TSource, TValue> : Entry
    {
        /// <summary>The supplied typed delegate identity for an instance registration.</summary>
        private readonly Func<TSource, TValue>? _selection;

        /// <summary>The descriptor for an exact delegate identity.</summary>
        private readonly ValidationSelector<TSource, TValue>? _selector;

        /// <summary>The explicitly registered pure delegate matcher.</summary>
        private readonly ValidationDelegateMatcher<TSource, TValue>? _matcher;

        /// <summary>The factory for invocation-local descriptors.</summary>
        private readonly Func<ValidationArguments, ValidationSelector<TSource, TValue>>? _factory;

        /// <summary>Initializes a new instance of the DelegateSelectorEntry class.</summary>
        /// <param name="role">The role contract.</param>
        /// <param name="selection">The selection contract.</param>
        /// <param name="selector">The selector contract.</param>
        internal DelegateSelectorEntry(
            ValidationPlanRole role,
            Func<TSource, TValue>? selection,
            ValidationSelector<TSource, TValue> selector)
            : base(role, null, null)
        {
            _selection = selection;
            _selector = selector;
        }

        /// <summary>Initializes a new instance of the DelegateSelectorEntry class.</summary>
        /// <param name="role">The role contract.</param>
        /// <param name="identity">The identity contract.</param>
        /// <param name="matcher">The matcher contract.</param>
        /// <param name="factory">The factory contract.</param>
        internal DelegateSelectorEntry(
            ValidationPlanRole role,
            string identity,
            ValidationDelegateMatcher<TSource, TValue> matcher,
            Func<ValidationArguments, ValidationSelector<TSource, TValue>> factory)
            : base(role, identity, null)
        {
            _matcher = matcher;
            _factory = factory;
        }

        /// <inheritdoc/>
        internal override bool Duplicate(Entry other) => other is DelegateSelectorEntry<TSource, TValue> candidate
            && Role == candidate.Role
            && (Identity is null
                ? candidate.Identity is null && Equals(_selection, candidate._selection)
                : candidate.Identity is not null && string.Equals(Identity, candidate.Identity, StringComparison.Ordinal));

        /// <summary>Matches one closed delegate slot without invoking its descriptor factory.</summary>
        /// <param name="request">The request contract.</param>
        /// <param name="selection">The selection contract.</param>
        /// <param name="arguments">The arguments contract.</param>
        /// <returns>The current typed result.</returns>
        /// <exception cref="InvalidOperationException">A successful matcher returns no payload.</exception>
        internal bool Match(ValidationPlanRequest request, Func<TSource, TValue>? selection, out ValidationArguments? arguments)
        {
            arguments = null;
            if (!Compatible(request))
            {
                return false;
            }

            if (Identity is null)
            {
                return Equals(_selection, selection);
            }

            if (selection is null)
            {
                return false;
            }

            if (!_matcher!(selection, out arguments))
            {
                return false;
            }

            if (arguments is null)
            {
                throw new InvalidOperationException("A successful schema matcher must return a non-null argument payload.");
            }

            return true;
        }

        /// <summary>Creates the selected descriptor after all ambiguity checks.</summary>
        /// <param name="arguments">The arguments contract.</param>
        /// <returns>The current typed result.</returns>
        /// <exception cref="InvalidOperationException">The factory returns no descriptor.</exception>
        internal ValidationSelector<TSource, TValue> Create(ValidationArguments arguments) =>
            _selector ?? _factory!(arguments) ?? throw new InvalidOperationException("A selector schema factory returned null.");
    }

    /// <summary>Stores an exact closed target registration.</summary>
    /// <typeparam name="TSource">The closed source type.</typeparam>
    /// <typeparam name="TValue">The closed value type.</typeparam>
    /// <typeparam name="TOut">The closed out type.</typeparam>
    private sealed class DelegateTargetEntry<TSource, TValue, TOut> : Entry
    {
        /// <summary>The supplied typed delegate identity for an instance registration.</summary>
        private readonly Func<TSource, TValue>? _selection;

        /// <summary>The target descriptor for an exact delegate identity.</summary>
        private readonly ValidationTarget<TSource, TOut>? _target;

        /// <summary>The explicitly registered pure delegate matcher.</summary>
        private readonly ValidationDelegateMatcher<TSource, TValue>? _matcher;

        /// <summary>The factory for invocation-local descriptors.</summary>
        private readonly Func<ValidationArguments, ValidationTarget<TSource, TOut>>? _factory;

        /// <summary>Initializes a new instance of the DelegateTargetEntry class.</summary>
        /// <param name="role">The role contract.</param>
        /// <param name="selection">The selection contract.</param>
        /// <param name="target">The target contract.</param>
        internal DelegateTargetEntry(
            ValidationPlanRole role,
            Func<TSource, TValue> selection,
            ValidationTarget<TSource, TOut> target)
            : base(role, null, null)
        {
            _selection = selection;
            _target = target;
        }

        /// <summary>Initializes a new instance of the DelegateTargetEntry class.</summary>
        /// <param name="role">The role contract.</param>
        /// <param name="identity">The identity contract.</param>
        /// <param name="matcher">The matcher contract.</param>
        /// <param name="factory">The factory contract.</param>
        internal DelegateTargetEntry(
            ValidationPlanRole role,
            string identity,
            ValidationDelegateMatcher<TSource, TValue> matcher,
            Func<ValidationArguments, ValidationTarget<TSource, TOut>> factory)
            : base(role, identity, null)
        {
            _matcher = matcher;
            _factory = factory;
        }

        /// <inheritdoc/>
        internal override bool Duplicate(Entry other) => other is DelegateTargetEntry<TSource, TValue, TOut> candidate
            && Role == candidate.Role
            && (Identity is null
                ? candidate.Identity is null && Equals(_selection, candidate._selection)
                : candidate.Identity is not null && string.Equals(Identity, candidate.Identity, StringComparison.Ordinal));

        /// <summary>Matches one closed delegate slot without invoking its descriptor factory.</summary>
        /// <param name="request">The request contract.</param>
        /// <param name="selection">The selection contract.</param>
        /// <param name="arguments">The arguments contract.</param>
        /// <returns>The current typed result.</returns>
        /// <exception cref="InvalidOperationException">A successful matcher returns no payload.</exception>
        internal bool Match(ValidationPlanRequest request, Func<TSource, TValue>? selection, out ValidationArguments? arguments)
        {
            arguments = null;
            if (!Compatible(request))
            {
                return false;
            }

            if (Identity is null)
            {
                return Equals(_selection, selection);
            }

            if (selection is null)
            {
                return false;
            }

            if (!_matcher!(selection, out arguments))
            {
                return false;
            }

            if (arguments is null)
            {
                throw new InvalidOperationException("A successful schema matcher must return a non-null argument payload.");
            }

            return true;
        }

        /// <summary>Creates the selected descriptor after all ambiguity checks.</summary>
        /// <param name="arguments">The arguments contract.</param>
        /// <returns>The current typed result.</returns>
        /// <exception cref="InvalidOperationException">The factory returns no descriptor.</exception>
        internal ValidationTarget<TSource, TOut> Create(ValidationArguments arguments) =>
            _target ?? _factory!(arguments) ?? throw new InvalidOperationException("A target schema factory returned null.");
    }
}
