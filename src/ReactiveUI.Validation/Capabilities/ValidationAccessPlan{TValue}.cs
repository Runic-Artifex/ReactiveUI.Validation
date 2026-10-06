// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>A bound typed getter and notification plan. Each observation owns independent registrations.</summary>
/// <typeparam name="TValue">The selected value type.</typeparam>
[System.Diagnostics.DebuggerDisplay("ValidationAccessPlan")]
public sealed class ValidationAccessPlan<TValue>
{
    /// <summary>The bound _read contract.</summary>
    private readonly Func<ValidationRead<TValue>>? _read;

    /// <summary>The bound _dependencies contract.</summary>
    private readonly ValidationDependency[] _dependencies = [];

    /// <summary>The optional independently owned observation factory.</summary>
    private readonly Func<ValidationAccessPlan<TValue>>? _createObservation;

    /// <summary>The bound _readPaths contract.</summary>
    private readonly Func<IReadOnlyList<ValidationPath>>? _readPaths;

    /// <summary>The separately snapshotted metadata-only notification contracts.</summary>
    private readonly ValidationDependency[]? _metadataDependencies;

    /// <summary>Initializes a new instance of the <see cref="ValidationAccessPlan{TValue}"/> class.</summary>
    /// <param name="read">Reads value and metadata atomically on the owner thread.</param>
    /// <param name="dependencies">The typed registration contracts.</param>
    /// <param name="options">Explicit missing-parent and equality policies.</param>
    public ValidationAccessPlan(
        Func<ValidationRead<TValue>> read,
        IEnumerable<ValidationDependency> dependencies,
        ValidationObservationOptions<TValue> options)
    {
        ArgumentExceptionHelper.ThrowIfNull(read);
        ArgumentExceptionHelper.ThrowIfNull(dependencies);
        ArgumentExceptionHelper.ThrowIfNull(options);
        _read = read;
        var copy = new List<ValidationDependency>();
        foreach (var dependency in dependencies)
        {
            ArgumentExceptionHelper.ThrowIfNull(dependency);
            copy.Add(dependency);
        }

        _dependencies = [.. copy];
        foreach (var dependency in _dependencies)
        {
            ArgumentExceptionHelper.ThrowIfNull(dependency);
        }

        Options = options;
    }

    /// <summary>Initializes a new instance of the <see cref="ValidationAccessPlan{TValue}"/> class.</summary>
    /// <param name="read">Reads the selected value and paths.</param>
    /// <param name="dependencies">Typed owner notifications.</param>
    /// <param name="options">Missing-owner and equality policies.</param>
    /// <param name="readPaths">Reads current identities without accessing the selected leaf value.</param>
    public ValidationAccessPlan(
        Func<ValidationRead<TValue>> read,
        IEnumerable<ValidationDependency> dependencies,
        ValidationObservationOptions<TValue> options,
        Func<IReadOnlyList<ValidationPath>> readPaths)
        : this(read, dependencies, options)
    {
        ArgumentExceptionHelper.ThrowIfNull(readPaths);
        _readPaths = readPaths;
    }

    /// <summary>Initializes a new instance of the <see cref="ValidationAccessPlan{TValue}"/> class.</summary>
    /// <param name="read">Reads the selected value and current identities.</param>
    /// <param name="dependencies">The value notification contracts.</param>
    /// <param name="options">The explicit observation policies.</param>
    /// <param name="readPaths">Reads identities without accessing the selected leaf.</param>
    /// <param name="metadataDependencies">Metadata-only owner getters and notification contracts.</param>
    public ValidationAccessPlan(
        Func<ValidationRead<TValue>> read,
        IEnumerable<ValidationDependency> dependencies,
        ValidationObservationOptions<TValue> options,
        Func<IReadOnlyList<ValidationPath>> readPaths,
        IEnumerable<ValidationDependency> metadataDependencies)
        : this(read, dependencies, options, readPaths)
    {
        ArgumentExceptionHelper.ThrowIfNull(metadataDependencies);
        List<ValidationDependency> snapshot = [];
        foreach (var dependency in metadataDependencies)
        {
            ArgumentExceptionHelper.ThrowIfNull(dependency);
            snapshot.Add(dependency);
        }

        _metadataDependencies = [.. snapshot];
    }

    /// <summary>Initializes a new instance of the <see cref="ValidationAccessPlan{TValue}"/> class with independent snapshot caches.</summary>
    /// <param name="createObservation">Creates a fresh inner plan for each read or independent observation subscription.</param>
    /// <param name="options">The policies shared by this outer plan and each inner plan.</param>
    public ValidationAccessPlan(Func<ValidationAccessPlan<TValue>> createObservation, ValidationObservationOptions<TValue> options)
    {
        ArgumentExceptionHelper.ThrowIfNull(createObservation);
        ArgumentExceptionHelper.ThrowIfNull(options);
        _createObservation = createObservation;
        Options = options;
    }

    /// <summary>Gets the explicit observation policies.</summary>
    public ValidationObservationOptions<TValue> Options { get; }

    /// <summary>Observes current identities without reading the selected leaf.</summary>
    /// <returns>The metadata stream.</returns>
    /// <exception cref="InvalidOperationException">No explicit metadata getter was supplied.</exception>
    public IObservable<IReadOnlyList<ValidationPath>> ObservePaths()
    {
        if (_createObservation is not null)
        {
            return Observable.Defer(() => FreshObservation().ObservePaths());
        }

        var readPaths = _readPaths ?? throw new InvalidOperationException("Metadata observation requires an explicit metadata-only getter.");
        var metadata = new ValidationAccessPlan<IReadOnlyList<ValidationPath>>(
            () =>
            {
                var paths = readPaths();
                return ValidationRead<IReadOnlyList<ValidationPath>>.Present(paths, paths);
            },
            _metadataDependencies ?? _dependencies,
            new(ValidationMissingOwnerPolicy.DefaultValue, null, EqualityComparer<IReadOnlyList<ValidationPath>>.Default, true));
        return metadata.Observe().Select(static read => read.Value);
    }

    /// <summary>Reads an unmodified snapshot without acquiring subscriptions.</summary>
    /// <returns>The current selection, including owner presence.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValidationRead<TValue> Read() => _createObservation is null ? _read!() : FreshObservation().Read();

    /// <summary>Creates a cold, synchronous observation that follows owner replacement.</summary>
    /// <returns>Value and metadata snapshots; disposal releases only owned registrations.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<ValidationRead<TValue>> Observe() => _createObservation is null
        ? new ValidationPlanObservable<TValue>(_read!, _dependencies, Options)
        : Observable.Defer(() => FreshObservation().Observe());

    /// <summary>Adds an independently owned invalidation adapter to the same atomic observation engine.</summary>
    /// <param name="invalidation">The additional source of current-owner refreshes.</param>
    /// <returns>The cold observation with its ordinary and after-read dependency phases preserved.</returns>
    internal IObservable<ValidationRead<TValue>> Observe(ValidationDependency invalidation)
    {
        ArgumentExceptionHelper.ThrowIfNull(invalidation);
        return _createObservation is null
            ? new ValidationPlanObservable<TValue>(_read!, [.. _dependencies, invalidation], Options)
            : Observable.Defer(() => FreshObservation().Observe(invalidation));
    }

    /// <summary>Retains missing snapshots for component metadata while leaving public value suppression intact.</summary>
    /// <returns>The component observation stream.</returns>
    internal IObservable<ValidationRead<TValue>> ObserveIncludingMissing()
    {
        if (_createObservation is not null)
        {
            return Observable.Defer(() => FreshObservation().ObserveIncludingMissing());
        }

        var options = Options.MissingOwner == ValidationMissingOwnerPolicy.Suppress
            ? new ValidationObservationOptions<TValue>(ValidationMissingOwnerPolicy.DefaultValue, null, Options.Comparer, Options.EmitEveryInvalidation)
            : Options;
        return new ValidationPlanObservable<TValue>(_read!, _dependencies, options);
    }

    /// <summary>Creates the single-property metadata plan without evaluating the selected value.</summary>
    /// <returns>The typed current path plan.</returns>
    /// <exception cref="InvalidOperationException">The metadata getter is absent or does not identify exactly one property.</exception>
    internal ValidationAccessPlan<ValidationPath> CreatePathPlan()
    {
        if (_createObservation is not null)
        {
            return new(() => FreshObservation().CreatePathPlan(), new(ValidationMissingOwnerPolicy.DefaultValue, null, EqualityComparer<ValidationPath>.Default, true));
        }

        var readPaths = _readPaths ?? throw new InvalidOperationException("Metadata observation requires an explicit metadata-only getter.");
        return new(
            () =>
            {
                var paths = readPaths();
                ArgumentExceptionHelper.ThrowIfNull(paths);
                if (paths.Count != 1)
                {
                    throw new InvalidOperationException("A property selector must identify exactly one current structural path.");
                }

                return ValidationRead<ValidationPath>.Present(paths[0], paths);
            },
            _metadataDependencies ?? _dependencies,
            new(ValidationMissingOwnerPolicy.DefaultValue, null, EqualityComparer<ValidationPath>.Default, true));
    }

    /// <summary>Preserves snapshot ownership while enforcing a role-specific identity comparer.</summary>
    /// <param name="comparer">The required identity comparer.</param>
    /// <returns>The same typed access with role-specific comparison.</returns>
    internal ValidationAccessPlan<TValue> WithComparer(IEqualityComparer<TValue> comparer)
    {
        var options = new ValidationObservationOptions<TValue>(Options.MissingOwner, Options.Fallback, comparer, Options.EmitEveryInvalidation);
        if (_createObservation is not null)
        {
            return new(() => FreshObservation().WithComparer(comparer), options);
        }

        return _readPaths is null
            ? new(_read!, _dependencies, options)
            : new(_read!, _dependencies, options, _readPaths, _metadataDependencies ?? _dependencies);
    }

    /// <summary>Creates independent snapshot state and verifies the declared policy contract.</summary>
    /// <returns>A fresh inner observation plan.</returns>
    /// <exception cref="InvalidOperationException">The factory returned null, itself, or different policies.</exception>
    private ValidationAccessPlan<TValue> FreshObservation()
    {
        var plan = _createObservation!() ?? throw new InvalidOperationException("The observation factory returned null.");
        if (ReferenceEquals(plan, this) || plan.Options.MissingOwner != Options.MissingOwner
            || !ReferenceEquals(plan.Options.Comparer, Options.Comparer) || !ReferenceEquals(plan.Options.Fallback, Options.Fallback)
            || plan.Options.EmitEveryInvalidation != Options.EmitEveryInvalidation)
        {
            throw new InvalidOperationException("The observation factory must return a distinct plan with the declared policies.");
        }

        return plan;
    }
}
