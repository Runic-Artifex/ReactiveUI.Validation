// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Adapts a borrowed platform or routed host to a typed view using explicit access and notification delegates.</summary>
/// <typeparam name="TOwner">The real platform or routed host type.</typeparam>
/// <typeparam name="TModel">The selected model type.</typeparam>
/// <remarks>The adapter owns only its notification registration. The host and models remain borrowed.</remarks>
[SuppressMessage("Concurrency", "PSH1306:Use atomic latch", Justification = "The explicit host provider and disposal run on the documented host owner thread; refresh drains reentry.")]
[System.Diagnostics.DebuggerDisplay("ValidationViewAdapter: {Owner}")]
public sealed class ValidationViewAdapter<TOwner, TModel> : ReactiveObject, IViewFor<TModel>, IDisposable
    where TOwner : class
    where TModel : class
{
    /// <summary>The explicit typed read operation.</summary>
    private readonly Func<TOwner, TModel?> _read;

    /// <summary>The explicit typed write operation.</summary>
    private readonly Action<TOwner, TModel?> _write;

    /// <summary>The owned host notification registration.</summary>
    private IDisposable? _registration;

    /// <summary>The model identity last observed by the provider.</summary>
    private TModel? _model;

    /// <summary>Whether the adapter has released its registration.</summary>
    private bool _disposed;

    /// <summary>Whether a provider refresh is in progress.</summary>
    private bool _refreshing;

    /// <summary>Whether a reentrant provider refresh is pending.</summary>
    private bool _pending;

    /// <summary>Initializes a new instance of the <see cref="ValidationViewAdapter{TOwner,TModel}"/> class.</summary>
    /// <param name="owner">The borrowed real host.</param>
    /// <param name="read">Reads the host's current model without reflection.</param>
    /// <param name="write">Writes the host's current model without reflection.</param>
    /// <param name="register">Registers provider callbacks on the host owner thread and returns the owned registration.</param>
    /// <exception cref="InvalidOperationException">The provider returns null.</exception>
    /// <remarks>
    /// The provider must notify every model replacement, including null, and clean up a failed registration before throwing.
    /// Synchronous initial callbacks are supported. Read, write, notifications and disposal run on the same host owner.
    /// Getters read current host storage; notifications compare model reference identity, including equal-valued replacements.
    /// </remarks>
    public ValidationViewAdapter(TOwner owner, Func<TOwner, TModel?> read, Action<TOwner, TModel?> write, Func<TOwner, Action, IDisposable> register)
    {
        ArgumentExceptionHelper.ThrowIfNull(owner);
        ArgumentExceptionHelper.ThrowIfNull(read);
        ArgumentExceptionHelper.ThrowIfNull(write);
        ArgumentExceptionHelper.ThrowIfNull(register);
        Owner = owner;
        _read = read;
        _write = write;
        _model = read(owner);
        try
        {
            var registration = register(owner, Refresh) ?? throw new InvalidOperationException("The host provider returned a null registration.");
            if (_disposed)
            {
                registration.Dispose();
            }
            else
            {
                _registration = registration;
                Refresh();
            }
        }
        catch (Exception error)
        {
            CleanupAfterFailure(error);
            throw;
        }
    }

    /// <summary>Gets the borrowed real host.</summary>
    public TOwner Owner { get; }

    /// <inheritdoc/>
    public TModel? ViewModel
    {
        get => _read(Owner);
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            try
            {
                _write(Owner, value);
                Refresh();
            }
            catch (Exception error)
            {
                CleanupAfterFailure(error);
                throw;
            }
        }
    }

    /// <inheritdoc/>
    object? IViewFor.ViewModel
    {
        get => ViewModel;
        set => ViewModel = (TModel?)value;
    }

    /// <summary>Detaches the host provider at most once without disposing the host or selected model.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var registration = _registration;
        _registration = null;
        _model = null;
        registration?.Dispose();
    }

    /// <summary>Drains provider notifications, preserving identity and rejecting post-disposal callbacks.</summary>
    private void Refresh()
    {
        if (_disposed)
        {
            return;
        }

        _pending = true;
        if (_refreshing)
        {
            return;
        }

        _refreshing = true;
        try
        {
            while (_pending && !_disposed)
            {
                _pending = false;
                var model = _read(Owner);
                if (_disposed || _pending || ReferenceEquals(model, _model))
                {
                    continue;
                }

                this.RaisePropertyChanging(nameof(ViewModel));
                if (_disposed || _pending)
                {
                    continue;
                }

                _model = model;
                this.RaisePropertyChanged(nameof(ViewModel));
            }
        }
        catch (Exception error)
        {
            CleanupAfterFailure(error);
            throw;
        }
        finally
        {
            _refreshing = false;
        }
    }

    /// <summary>Preserves a provider or callback failure before any owned cleanup failure.</summary>
    /// <param name="error">The original failure.</param>
    /// <exception cref="AggregateException">The operation and owned provider cleanup both fail.</exception>
    private void CleanupAfterFailure(Exception error)
    {
        try
        {
            Dispose();
        }
        catch (Exception cleanupError)
        {
            throw new AggregateException("View adapter and provider cleanup both failed.", error, cleanupError);
        }
    }
}
