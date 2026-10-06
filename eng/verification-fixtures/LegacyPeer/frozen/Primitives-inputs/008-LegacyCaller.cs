// Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
using System.ComponentModel;
using System.Linq.Expressions;
#if REACTIVE_SHIM
using ReactiveUI.Reactive;
using ReactiveUI.Validation.Reactive.Abstractions;
using ReactiveUI.Validation.Reactive.Contexts;
using ReactiveUI.Validation.Reactive.Extensions;
using ReactiveUI.Validation.Reactive.Helpers;
#else
using ReactiveUI;
using ReactiveUI.Validation.Abstractions;
using ReactiveUI.Validation.Contexts;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
#endif

namespace GeneratedValidation.LegacyPeer;

/// <summary>A separately compiled caller deliberately using finite typed runtime plans.</summary>
public static class PeerRules
{
    /// <summary>Creates a new expression whose explicitly typed capture belongs to this invocation.</summary>
    public static Expression<Func<PeerModel, string?>> FreshExpression(PeerCapture capture)
    {
        Expression<Func<PeerModel, string?>> knownCall = source => source.Read(default!);
        var call = (MethodCallExpression)knownCall.Body;
        return Expression.Lambda<Func<PeerModel, string?>>(
            Expression.Call(knownCall.Parameters[0], call.Method, Expression.Constant(capture)), knownCall.Parameters);
    }

    /// <summary>Provides a genuine unregistered compiler-closure shape as a fail-closed control.</summary>
    public static Expression<Func<PeerModel, string?>> ForeignExpression(PeerCapture capture) => source => source.Read(capture);

    /// <summary>Uses the unchanged normal predicate API from a precompiled assembly.</summary>
    public static ValidationHelper Rule(PeerModel model, PeerCapture capture, string expected, string message) =>
        model.ValidationRule(FreshExpression(capture), value => value == expected, message);

    /// <summary>Returns the unchanged normal API as a real callable method group.</summary>
    public static Func<PeerModel, Expression<Func<PeerModel, string?>>, Func<string?, bool>, string, ValidationHelper> Callable() =>
        ValidatableViewModelExtensions.ValidationRule<PeerModel, string>;


}

/// <summary>The current receiver supplied to the original API by a separately compiled caller.</summary>
public sealed class PeerModel : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
{
    private readonly string?[] _values;
    private PropertyChangedEventHandler? _changed;

    public PeerModel(string? first, string? second) => _values = [first, second];

    public IValidationContext ValidationContext { get; } = new ValidationContext();

    public int ListenerCount { get; private set; }

    public int Reads { get; private set; }

    public new event PropertyChangedEventHandler? PropertyChanged
    {
        add { _changed += value; ListenerCount++; }
        remove { _changed -= value; ListenerCount--; }
    }

    public string? Read(PeerCapture capture)
    {
        Reads++;
        return _values[capture.Index];
    }

    public void Set(int index, string? value)
    {
        _values[index] = value;
        _changed?.Invoke(this, new("Item[]"));
    }
}

/// <summary>A known typed live argument, never inspected through reflection.</summary>
public sealed class PeerCapture : INotifyPropertyChanged
{
    private PropertyChangedEventHandler? _changed;
    private int _index;

    public int ListenerCount { get; private set; }

    public int Index
    {
        get => _index;
        set { _index = value; _changed?.Invoke(this, new(nameof(Index))); }
    }

    public event PropertyChangedEventHandler? PropertyChanged
    {
        add { _changed += value; ListenerCount++; }
        remove { _changed -= value; ListenerCount--; }
    }
}
