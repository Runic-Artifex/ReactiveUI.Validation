// Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
using System.ComponentModel;
using System.Linq.Expressions;
#if REACTIVE_SHIM
using ReactiveUI.Reactive;
using ReactiveUI.Validation.Reactive.Abstractions;
using ReactiveUI.Validation.Reactive.Capabilities;
using ReactiveUI.Validation.Reactive.Contexts;
using ReactiveUI.Validation.Reactive.Extensions;
using ReactiveUI.Validation.Reactive.Helpers;
#else
using ReactiveUI;
using ReactiveUI.Validation.Abstractions;
using ReactiveUI.Validation.Capabilities;
using ReactiveUI.Validation.Contexts;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
#endif

namespace GeneratedValidation.Precompiled;

/// <summary>A separately compiled caller deliberately using finite typed runtime plans.</summary>
[ValidationRuntimeDispatch]
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

    /// <summary>Registers one finite capture schema; lookup binds each current source and capture.</summary>
    public static IDisposable Register(ValidationPlanRegistry registry, string operationIdentity = "peer.read.capture")
    {
        var exemplar = FreshExpression(new PeerCapture());
        var capture = new ValidationArgument<PeerCapture>();
        var position = (ConstantExpression)((MethodCallExpression)exemplar.Body).Arguments[0];
        var matcher = ValidationExpressionPattern.Create(exemplar, ValidationExpressionBinding.Constant(position, capture));
        return registry.RegisterSelectorPattern(ValidationPlanRole.RuleValue, operationIdentity, matcher,
            arguments => Selector(arguments.Get(capture)));
    }

    /// <summary>Calls the ordinary Func API from a separately compiled body.</summary>
    public static ValidationHelper DelegateRule(PeerModel model, PeerDelegateSelection selection, string expected, string message) =>
        model.ValidationRule(selection.Fresh(), value => value == expected, message);

    /// <summary>Returns the ordinary Func API as an actual callable method group.</summary>
    public static Func<PeerModel, Func<PeerModel, string?>, Func<string?, bool>, string, ValidationHelper> DelegateCallable() =>
        ValidatableViewModelExtensions.ValidationRule<PeerModel, string>;

    /// <summary>Registers a known delegate identity and supplied capture without executing metadata.</summary>
    public static IDisposable RegisterDelegate(ValidationPlanRegistry registry, PeerDelegateSelection selection, string operationIdentity) =>
        registry.RegisterDelegateSelectorPattern(
            ValidationPlanRole.RuleValue, operationIdentity, ValidationDelegatePattern.Create(selection.Fresh()),
            _ => Selector(selection.Capture));

    private static ValidationSelector<PeerModel, string?> Selector(PeerCapture capture) => new(source => new(
        () => ValidationRead<string?>.Present(source.Read(capture), Paths(source, capture)),
        [ValidationDependency.PropertyChanged(() => source, "Item[]"), ValidationDependency.PropertyChanged(() => capture, nameof(PeerCapture.Index))],
        new(ValidationMissingOwnerPolicy.DefaultValue, null, EqualityComparer<string?>.Default, true),
        () => Paths(source, capture)));

    private static IReadOnlyList<ValidationPath> Paths(PeerModel source, PeerCapture capture) =>
        [ValidationPath.Structural($"Values[{capture.Index}]", (source, capture.Index), EqualityComparer<(PeerModel, int)>.Default)];
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

/// <summary>A known delegate owner whose fresh method groups have equal identities.</summary>
public sealed class PeerDelegateSelection(PeerCapture capture)
{
    public PeerCapture Capture { get; } = capture;

    public int MetadataReads { get; private set; }

    public Func<PeerModel, string?> Fresh() => new(Read);

    private string? Read(PeerModel source)
    {
        MetadataReads++;
        return source.Read(Capture);
    }
}
