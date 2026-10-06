// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>A bounded, explicitly owned catalog of closed typed validation plans.</summary>
/// <remarks>
/// Registrations retain descriptors until their lease or registry is disposed. Owner associations use weak reference identity, including stable interface boxes.
/// Each lookup uses a registration snapshot; callback reentry changes later lookups. Returned descriptors remain caller-owned after unregistration.
/// </remarks>
[DebuggerDisplay("Capacity = {_capacity}, Disposed = {_disposed}")]
public sealed partial class ValidationPlanRegistry : IValidationPlanProvider, IValidationDelegatePlanProvider, IDisposable
{
    /// <summary>Weak reference-identity owner associations.</summary>
    private static readonly ConditionalWeakTable<object, Attachment> _attached = new();

    /// <summary>Serializes weak owner associations.</summary>
    private static readonly Lock _attachmentGate = new();

    /// <summary>Serializes catalog lifetime and registration changes.</summary>
    private readonly Lock _gate = new();

    /// <summary>The configured maximum live entry count.</summary>
    private readonly int _capacity;

    /// <summary>The finite entries retained by this owner.</summary>
    private readonly List<Entry> _entries = [];

    /// <summary>The weak association leases owned by this catalog.</summary>
    private readonly List<AttachmentLease> _attachments = [];

    /// <summary>Whether this catalog has released its registrations.</summary>
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="ValidationPlanRegistry"/> class.</summary>
    /// <param name="capacity">The maximum live registrations and, separately, live owner associations.</param>
    public ValidationPlanRegistry(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
    }

    /// <summary>Attaches this catalog to an owner by stable object identity.</summary>
    /// <typeparam name="TOwner">The owner's reference or interface API type.</typeparam>
    /// <param name="owner">The borrowed owner; interface boxes must be retained by the caller.</param>
    /// <returns>A lease that removes only this exact association.</returns>
    /// <exception cref="InvalidOperationException">The owner is already associated or the association capacity is exhausted.</exception>
    public IDisposable Attach<TOwner>(TOwner owner)
        where TOwner : class
    {
        ArgumentExceptionHelper.ThrowIfNull(owner);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _ = _attachments.RemoveAll(static lease => !lease.IsAlive);
            if (_attachments.Count == _capacity)
            {
                throw new InvalidOperationException("The registry's owner association capacity has been reached.");
            }

            lock (_attachmentGate)
            {
                if (_attached.TryGetValue(owner, out _))
                {
                    throw new InvalidOperationException("The owner already has a validation plan provider attached.");
                }

                var attachment = new Attachment(this);
                var lease = new AttachmentLease(owner, attachment);
                _attached.Add(owner, attachment);
                _attachments.Add(lease);
                return lease;
            }
        }
    }

    /// <summary>Registers a descriptor for one expression instance and exact closed types.</summary>
    /// <typeparam name="TSource">The source API type.</typeparam>
    /// <typeparam name="TValue">The selected value type.</typeparam>
    /// <param name="role">The expression's purpose.</param>
    /// <param name="expression">The exact expression instance, or null for an implicit model or default-context selector.</param>
    /// <param name="selector">The typed descriptor.</param>
    /// <returns>The registration lease.</returns>
    /// <exception cref="ArgumentNullException">A non-implicit registration has no expression.</exception>
    public IDisposable RegisterSelector<TSource, TValue>(ValidationPlanRole role, Expression<Func<TSource, TValue>>? expression, ValidationSelector<TSource, TValue> selector)
    {
        if (expression is null && role is not (ValidationPlanRole.Model or ValidationPlanRole.DefaultContext))
        {
            throw new ArgumentNullException(nameof(expression), "Only an implicit model or default-context selector may register a null expression.");
        }

        ArgumentExceptionHelper.ThrowIfNull(selector);
        return Add(new SelectorEntry<TSource, TValue>(role, expression, selector));
    }

    /// <summary>Registers a writable descriptor for one expression instance and exact closed types.</summary>
    /// <typeparam name="TSource">The target source API type.</typeparam>
    /// <typeparam name="TValue">The expression's selected property type.</typeparam>
    /// <typeparam name="TOut">The assigned output type.</typeparam>
    /// <param name="role">The expression's purpose.</param>
    /// <param name="expression">The exact expression instance.</param>
    /// <param name="target">The typed descriptor.</param>
    /// <returns>The registration lease.</returns>
    public IDisposable RegisterTarget<TSource, TValue, TOut>(ValidationPlanRole role, Expression<Func<TSource, TValue>> expression, ValidationTarget<TSource, TOut> target)
    {
        ArgumentExceptionHelper.ThrowIfNull(expression);
        ArgumentExceptionHelper.ThrowIfNull(target);
        return Add(new TargetEntry<TSource, TValue, TOut>(role, expression, target));
    }

    /// <summary>Registers one finite expression schema for fresh invocation-local selectors.</summary>
    /// <typeparam name="TSource">The source API type.</typeparam>
    /// <typeparam name="TValue">The selected value type.</typeparam>
    /// <param name="role">The expression's purpose.</param>
    /// <param name="operationIdentity">The finite schema identity.</param>
    /// <param name="matcher">The pure typed schema matcher.</param>
    /// <param name="factory">Creates the descriptor without acquiring subscriptions, after ambiguity checks.</param>
    /// <returns>The registration lease.</returns>
    public IDisposable RegisterSelectorPattern<TSource, TValue>(
        ValidationPlanRole role,
        string operationIdentity,
        ValidationExpressionMatcher<TSource, TValue> matcher,
        Func<ValidationArguments, ValidationSelector<TSource, TValue>> factory)
    {
        ArgumentExceptionHelper.ThrowIfNullOrEmpty(operationIdentity);
        ArgumentExceptionHelper.ThrowIfNull(matcher);
        ArgumentExceptionHelper.ThrowIfNull(factory);
        return Add(new SelectorEntry<TSource, TValue>(role, operationIdentity, matcher, factory));
    }

    /// <summary>Registers one finite expression schema for fresh invocation-local targets.</summary>
    /// <typeparam name="TSource">The target source API type.</typeparam>
    /// <typeparam name="TValue">The expression's selected property type.</typeparam>
    /// <typeparam name="TOut">The assigned output type.</typeparam>
    /// <param name="role">The expression's purpose.</param>
    /// <param name="operationIdentity">The finite schema identity.</param>
    /// <param name="matcher">The pure typed schema matcher.</param>
    /// <param name="factory">Creates the descriptor without acquiring subscriptions, after ambiguity checks.</param>
    /// <returns>The registration lease.</returns>
    public IDisposable RegisterTargetPattern<TSource, TValue, TOut>(
        ValidationPlanRole role,
        string operationIdentity,
        ValidationExpressionMatcher<TSource, TValue> matcher,
        Func<ValidationArguments, ValidationTarget<TSource, TOut>> factory)
    {
        ArgumentExceptionHelper.ThrowIfNullOrEmpty(operationIdentity);
        ArgumentExceptionHelper.ThrowIfNull(matcher);
        ArgumentExceptionHelper.ThrowIfNull(factory);
        return Add(new TargetEntry<TSource, TValue, TOut>(role, operationIdentity, matcher, factory));
    }

    /// <inheritdoc/>
    public bool TryGetSelector<TSource, TValue>(
        ValidationPlanRequest request,
        Expression<Func<TSource, TValue>>? expression,
        [NotNullWhen(true)] out ValidationSelector<TSource, TValue>? selector)
    {
        request.Validate();
        var matches = new List<(SelectorEntry<TSource, TValue> Entry, ValidationArguments? Arguments)>();
        foreach (var entry in Snapshot())
        {
            if (entry is not SelectorEntry<TSource, TValue> candidate)
            {
                continue;
            }

            if (!candidate.Match(request, expression, out var matched))
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
    public bool TryGetTarget<TSource, TValue, TOut>(
        ValidationPlanRequest request,
        Expression<Func<TSource, TValue>>? expression,
        [NotNullWhen(true)] out ValidationTarget<TSource, TOut>? target)
    {
        request.Validate();
        var matches = new List<(TargetEntry<TSource, TValue, TOut> Entry, ValidationArguments? Arguments)>();
        foreach (var entry in Snapshot())
        {
            if (entry is not TargetEntry<TSource, TValue, TOut> candidate)
            {
                continue;
            }

            if (!candidate.Match(request, expression, out var matched))
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

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _entries.Clear();
            foreach (var lease in _attachments)
            {
                lease.Dispose();
            }

            _attachments.Clear();
        }
    }

    /// <summary>Finds the explicitly attached sealed registry by owner reference identity.</summary>
    /// <param name="owner">The owner contract.</param>
    /// <returns>The current typed result.</returns>
    internal static ValidationPlanRegistry? TryGetAttached(object owner)
    {
        ArgumentExceptionHelper.ThrowIfNull(owner);
        lock (_attachmentGate)
        {
            return _attached.TryGetValue(owner, out var attachment) ? attachment.Registry : null;
        }
    }

    /// <summary>Creates the actionable overlapping-schema failure.</summary>
    /// <returns>The current typed result.</returns>
    private static InvalidOperationException Ambiguous() => new("Multiple validation schemas matched the same role and closed types. Remove the overlap or supply a unique operation identity.");

    /// <summary>Registers one entry after capacity and duplicate checks.</summary>
    /// <param name="entry">The entry contract.</param>
    /// <returns>The current typed result.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The entry has an undefined role.</exception>
    /// <exception cref="InvalidOperationException">An identity is duplicated or capacity is exhausted.</exception>
    private RegistrationLease Add(Entry entry)
    {
        if (!Enum.IsDefined(entry.Role))
        {
            throw new ArgumentOutOfRangeException(nameof(entry));
        }

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            foreach (var existing in _entries)
            {
                if (entry.Duplicate(existing))
                {
                    throw new InvalidOperationException("The same expression or schema identity is already registered for these exact types and role.");
                }
            }

            if (_entries.Count == _capacity)
            {
                throw new InvalidOperationException("The registry's registration capacity has been reached.");
            }

            _entries.Add(entry);
            return new(this, entry);
        }
    }

    /// <summary>Captures the finite registration set without invoking caller callbacks.</summary>
    /// <returns>The current typed result.</returns>
    private Entry[] Snapshot()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _entries.ToArray();
        }
    }

    /// <summary>Releases one registration without disposing returned descriptors.</summary>
    /// <param name="entry">The entry contract.</param>
    private void Remove(Entry entry)
    {
        lock (_gate)
        {
            _ = _entries.Remove(entry);
        }
    }

    /// <summary>Associates a weak-table owner with its concrete registry.</summary>
    /// <param name="registry">The sealed registry contract.</param>
    private sealed class Attachment(ValidationPlanRegistry registry)
    {
        /// <summary>Gets the concrete registry attached to the weak owner identity.</summary>
        internal ValidationPlanRegistry Registry { get; } = registry;
    }

    /// <summary>Owns one weak identity association.</summary>
    /// <param name="owner">The owner contract.</param>
    /// <param name="attachment">The attachment contract.</param>
    private sealed class AttachmentLease(object owner, Attachment attachment) : IDisposable
    {
        /// <summary>The weak identity of the borrowed association owner.</summary>
        private readonly WeakReference<object> _owner = new(owner);

        /// <summary>The currently owned weak-table association.</summary>
        private Attachment? _attachment = attachment;

        /// <summary>Gets whether this association still has a live owner and has not been disposed.</summary>
        internal bool IsAlive => _attachment is not null && _owner.TryGetTarget(out _);

        /// <inheritdoc/>
        public void Dispose()
        {
            lock (_attachmentGate)
            {
                if (_attachment is null)
                {
                    return;
                }

                if (_owner.TryGetTarget(out var owner) && _attached.TryGetValue(owner, out var current) && ReferenceEquals(current, _attachment))
                {
                    _ = _attached.Remove(owner);
                }

                _attachment = null;
            }
        }
    }

    /// <summary>Owns unregistration without owning the borrowed catalog.</summary>
    /// <param name="registry">The registry contract.</param>
    /// <param name="entry">The entry contract.</param>
    private sealed class RegistrationLease(ValidationPlanRegistry registry, Entry entry) : IDisposable
    {
        /// <summary>Serializes registration lease disposal.</summary>
        private readonly Lock _leaseGate = new();

        /// <summary>The weak reference to the borrowed catalog.</summary>
        private WeakReference<ValidationPlanRegistry>? _registry = new(registry);

        /// <summary>The weak reference to the registration released by this lease.</summary>
        private WeakReference<Entry>? _entry = new(entry);

        /// <inheritdoc/>
        public void Dispose()
        {
            lock (_leaseGate)
            {
                var registry = _registry;
                var entry = _entry;
                _registry = null;
                _entry = null;
                if (registry is not null && registry.TryGetTarget(out var catalog) && entry is not null && entry.TryGetTarget(out var registered))
                {
                    catalog.Remove(registered);
                }
            }
        }
    }

    /// <summary>Stores a finite registration identity.</summary>
    /// <param name="role">The role contract.</param>
    /// <param name="identity">The identity contract.</param>
    /// <param name="expression">The expression contract.</param>
    private abstract class Entry(ValidationPlanRole role, string? identity, Expression? expression)
    {
        /// <summary>Gets the registered expression role.</summary>
        internal ValidationPlanRole Role { get; } = role;

        /// <summary>Gets the schema discriminator, or null for an instance registration.</summary>
        internal string? Identity { get; } = identity;

        /// <summary>Gets the exact registered expression instance, when supplied.</summary>
        internal Expression? Expression { get; } = expression;

        /// <summary>Checks for an already registered exact identity.</summary>
        /// <param name="other">The other contract.</param>
        /// <returns>The current typed result.</returns>
        internal abstract bool Duplicate(Entry other);

        /// <summary>Compares a role and exact expression or schema identity.</summary>
        /// <param name="other">The other contract.</param>
        /// <returns>The current typed result.</returns>
        protected bool SameIdentity(Entry other) =>
            Role == other.Role
            && (Identity is null ? other.Identity is null
            && ReferenceEquals(Expression, other.Expression) : other.Identity is not null
            && string.Equals(Identity, other.Identity, StringComparison.Ordinal));

        /// <summary>Checks the requested role and optional schema discriminator.</summary>
        /// <param name="request">The request contract.</param>
        /// <returns>The current typed result.</returns>
        protected bool Compatible(ValidationPlanRequest request) =>
            Role == request.Role && (Identity is null || request.OperationIdentity!.Length == 0 || string.Equals(Identity, request.OperationIdentity, StringComparison.Ordinal));
    }

    /// <summary>Stores an exact closed selector registration.</summary>
    /// <typeparam name="TSource">The closed source type.</typeparam>
    /// <typeparam name="TValue">The closed value type.</typeparam>
    private sealed class SelectorEntry<TSource, TValue> : Entry
    {
        /// <summary>The descriptor for an exact expression instance.</summary>
        private readonly ValidationSelector<TSource, TValue>? _selector;

        /// <summary>The explicitly registered pure expression matcher.</summary>
        private readonly ValidationExpressionMatcher<TSource, TValue>? _matcher;

        /// <summary>The factory for invocation-local descriptors.</summary>
        private readonly Func<ValidationArguments, ValidationSelector<TSource, TValue>>? _factory;

        /// <summary>Initializes a new instance of the SelectorEntry class.</summary>
        /// <param name="role">The role contract.</param>
        /// <param name="expression">The expression contract.</param>
        /// <param name="selector">The selector contract.</param>
        internal SelectorEntry(
            ValidationPlanRole role,
            Expression<Func<TSource, TValue>>? expression,
            ValidationSelector<TSource, TValue> selector)
            : base(role, null, expression) => _selector = selector;

        /// <summary>Initializes a new instance of the SelectorEntry class.</summary>
        /// <param name="role">The role contract.</param>
        /// <param name="identity">The identity contract.</param>
        /// <param name="matcher">The matcher contract.</param>
        /// <param name="factory">The factory contract.</param>
        internal SelectorEntry(
            ValidationPlanRole role,
            string identity,
            ValidationExpressionMatcher<TSource, TValue> matcher,
            Func<ValidationArguments, ValidationSelector<TSource, TValue>> factory)
            : base(role, identity, null)
        {
            _matcher = matcher;
            _factory = factory;
        }

        /// <inheritdoc/>
        internal override bool Duplicate(Entry other) => other is SelectorEntry<TSource, TValue> && SameIdentity(other);

        /// <summary>Matches one closed expression slot without invoking its descriptor factory.</summary>
        /// <param name="request">The request contract.</param>
        /// <param name="expression">The expression contract.</param>
        /// <param name="arguments">The arguments contract.</param>
        /// <returns>The current typed result.</returns>
        /// <exception cref="InvalidOperationException">A successful matcher returns no payload.</exception>
        internal bool Match(ValidationPlanRequest request, Expression<Func<TSource, TValue>>? expression, out ValidationArguments? arguments)
        {
            arguments = null;
            if (!Compatible(request))
            {
                return false;
            }

            if (Identity is null)
            {
                return ReferenceEquals(Expression, expression);
            }

            if (expression is null)
            {
                return false;
            }

            if (!_matcher!(expression, out arguments))
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
    private sealed class TargetEntry<TSource, TValue, TOut> : Entry
    {
        /// <summary>The target descriptor for an exact expression instance.</summary>
        private readonly ValidationTarget<TSource, TOut>? _target;

        /// <summary>The explicitly registered pure expression matcher.</summary>
        private readonly ValidationExpressionMatcher<TSource, TValue>? _matcher;

        /// <summary>The factory for invocation-local descriptors.</summary>
        private readonly Func<ValidationArguments, ValidationTarget<TSource, TOut>>? _factory;

        /// <summary>Initializes a new instance of the TargetEntry class.</summary>
        /// <param name="role">The role contract.</param>
        /// <param name="expression">The expression contract.</param>
        /// <param name="target">The target contract.</param>
        internal TargetEntry(
            ValidationPlanRole role,
            Expression<Func<TSource, TValue>> expression,
            ValidationTarget<TSource, TOut> target)
            : base(role, null, expression) => _target = target;

        /// <summary>Initializes a new instance of the TargetEntry class.</summary>
        /// <param name="role">The role contract.</param>
        /// <param name="identity">The identity contract.</param>
        /// <param name="matcher">The matcher contract.</param>
        /// <param name="factory">The factory contract.</param>
        internal TargetEntry(
            ValidationPlanRole role,
            string identity,
            ValidationExpressionMatcher<TSource, TValue> matcher,
            Func<ValidationArguments, ValidationTarget<TSource, TOut>> factory)
            : base(role, identity, null)
        {
            _matcher = matcher;
            _factory = factory;
        }

        /// <inheritdoc/>
        internal override bool Duplicate(Entry other) => other is TargetEntry<TSource, TValue, TOut> && SameIdentity(other);

        /// <summary>Matches one closed expression slot without invoking its descriptor factory.</summary>
        /// <param name="request">The request contract.</param>
        /// <param name="expression">The expression contract.</param>
        /// <param name="arguments">The arguments contract.</param>
        /// <returns>The current typed result.</returns>
        /// <exception cref="InvalidOperationException">A successful matcher returns no payload.</exception>
        internal bool Match(ValidationPlanRequest request, Expression<Func<TSource, TValue>>? expression, out ValidationArguments? arguments)
        {
            arguments = null;
            if (!Compatible(request))
            {
                return false;
            }

            if (Identity is null)
            {
                return ReferenceEquals(Expression, expression);
            }

            if (expression is null)
            {
                return false;
            }

            if (!_matcher!(expression, out arguments))
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
