// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Components;
#else
namespace ReactiveUI.Validation.Components;
#endif

/// <summary>A direct typed component preserving complete states and current structural metadata.</summary>
/// <typeparam name="TSource">The borrowed source type.</typeparam>
/// <typeparam name="TValue">The selected value type.</typeparam>
[System.Diagnostics.DebuggerDisplay("SelectorValidation")]
public sealed class SelectorValidation<TSource, TValue> : IValidationPathComponent, IDisposable
{
    /// <summary>The current _states observation state.</summary>
    private readonly ReplaySignal<IValidationState> _states = new(1);

    /// <summary>The current _paths observation state.</summary>
    private readonly ReplaySignal<IReadOnlyList<ValidationPath>> _paths = new(1);

    /// <summary>The current _plan observation state.</summary>
    private ValidationAccessPlan<TValue>? _plan;

    /// <summary>The current _validate observation state.</summary>
    private Func<TValue, IValidationState>? _validate;

    /// <summary>The current _subscription observation state.</summary>
    private IDisposable? _subscription;

    /// <summary>The current _state observation state.</summary>
    private IValidationState? _state;

    /// <summary>The current _currentPaths observation state.</summary>
    private IReadOnlyList<ValidationPath> _currentPaths = [];

    /// <summary>The current _active observation state.</summary>
    private bool _active;

    /// <summary>The current _hasPaths observation state.</summary>
    private bool _hasPaths;

    /// <summary>The current _disposed observation state.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="SelectorValidation{TSource,TValue}"/> class.</summary>
    /// <param name="source">The borrowed root or owned-storage handle.</param>
    /// <param name="selector">The typed factory.</param>
    /// <param name="validate">Projects values into complete states without text-based filtering.</param>
    public SelectorValidation(TSource source, ValidationSelector<TSource, TValue> selector, Func<TValue, IValidationState> validate)
    {
        ArgumentExceptionHelper.ThrowIfNull(selector);
        ArgumentExceptionHelper.ThrowIfNull(validate);
        _plan = selector.Bind(source);
        _validate = validate;
    }

    /// <inheritdoc/>
    public IReadOnlyList<ValidationPath> ValidationPaths
    {
        get
        {
            Activate();
            return _currentPaths;
        }
    }

    /// <inheritdoc/>
    public IObservable<IReadOnlyList<ValidationPath>> ValidationPathsChanged
    {
        get
        {
            Activate();
            return _paths;
        }
    }

    /// <inheritdoc/>
    public int PropertyCount => ValidationPaths.Count;

    /// <inheritdoc/>
    public IEnumerable<string> Properties
    {
        get
        {
            HashSet<string> names = [];
            foreach (var path in ValidationPaths)
            {
                _ = names.Add(path.DisplayPath);
            }

            return names;
        }
    }

    /// <inheritdoc/>
    public bool IsValid
    {
        get
        {
            Activate();
            return _state?.IsValid ?? false;
        }
    }

    /// <inheritdoc/>
    public IValidationText? Text
    {
        get
        {
            Activate();
            return _state?.Text;
        }
    }

    /// <inheritdoc/>
    public IObservable<IValidationState> ValidationStatusChange
    {
        get
        {
            Activate();
            return _states;
        }
    }

    /// <inheritdoc/>
    public bool ContainsPropertyName(string propertyName, bool exclusively)
    {
        foreach (var path in ValidationPaths)
        {
            if (StringComparer.Ordinal.Equals(path.DisplayPath, propertyName))
            {
                return !exclusively || PropertyCount == 1;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public bool ContainsPath(ValidationPath path, bool exclusively)
    {
        ArgumentExceptionHelper.ThrowIfNull(path);
        if (path.IsLegacy)
        {
            return ContainsPropertyName(path.DisplayPath, exclusively);
        }

        var paths = ValidationPaths;
        foreach (var current in paths)
        {
            if (current.Matches(path))
            {
                return !exclusively || paths.Count == 1;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _plan = null;
        _validate = null;
        _currentPaths = [];
        _state = null;
        try
        {
            _subscription?.Dispose();
        }
        finally
        {
            _subscription = null;
            _states.Dispose();
            _paths.Dispose();
        }
    }

    /// <summary>Starts the single shared typed value observation.</summary>
    private void Activate()
    {
        if (_active || Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        _active = true;
        var token = _plan!.ObserveIncludingMissing().Subscribe(new Observer(this));
        if (Volatile.Read(ref _disposed) != 0)
        {
            token.Dispose();
        }
        else
        {
            _subscription = token;
        }
    }

    /// <summary>Projects atomic observations into complete states and metadata.</summary>
    /// <param name="owner">The borrowed component.</param>
    private sealed class Observer(SelectorValidation<TSource, TValue> owner) : IObserver<ValidationRead<TValue>>
    {
        /// <inheritdoc/>
        public void OnNext(ValidationRead<TValue> value)
        {
            if (Volatile.Read(ref owner._disposed) != 0)
            {
                return;
            }

            var (uniquePaths, changed) = UpdatePaths(value.Paths);
            if (Volatile.Read(ref owner._disposed) != 0)
            {
                return;
            }

            owner._hasPaths = true;
            owner._currentPaths = Array.AsReadOnly(uniquePaths);
            if (!value.HasOwner && owner._plan!.Options.MissingOwner == ValidationMissingOwnerPolicy.Suppress)
            {
                if (changed)
                {
                    owner._paths.OnNext(owner._currentPaths);
                }

                return;
            }

            var state = owner._validate!(value.Value) ?? throw new InvalidOperationException("The validation callback returned no state.");
            if (Volatile.Read(ref owner._disposed) != 0)
            {
                return;
            }

            owner._state = state;
            owner._states.OnNext(state);
            if (changed && Volatile.Read(ref owner._disposed) == 0)
            {
                owner._paths.OnNext(owner._currentPaths);
            }
        }

        /// <inheritdoc/>
        public void OnCompleted()
        {
        }

        /// <inheritdoc/>
        public void OnError(Exception error)
        {
            if (Volatile.Read(ref owner._disposed) != 0)
            {
                return;
            }

            owner._states.OnError(error);
            owner._paths.OnError(error);
        }

        /// <summary>Compares identity and presentation metadata without invoking selected values.</summary>
        /// <param name="left">The previous paths.</param>
        /// <param name="right">The current paths.</param>
        /// <returns>Whether complete metadata snapshots match.</returns>
        private static bool SamePaths(IReadOnlyList<ValidationPath> left, ValidationPath[] right)
        {
            if (left.Count != right.Length)
            {
                return false;
            }

            for (var index = 0; index < left.Count; index++)
            {
                if (!left[index].Equals(right[index]) || !StringComparer.Ordinal.Equals(left[index].DisplayPath, right[index].DisplayPath))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Copies current metadata and compares its identity and display name.</summary>
        /// <param name="paths">The current paths.</param>
        /// <returns>The unique snapshot and whether it changed.</returns>
        private (ValidationPath[] Paths, bool Changed) UpdatePaths(IReadOnlyList<ValidationPath> paths)
        {
            HashSet<ValidationPath> seen = [];
            List<ValidationPath> unique = [];
            foreach (var path in paths)
            {
                if (seen.Add(path))
                {
                    unique.Add(path);
                }
            }

            ValidationPath[] uniquePaths = [.. unique];
            return (uniquePaths, !owner._hasPaths || !SamePaths(owner._currentPaths, uniquePaths));
        }
    }
}
