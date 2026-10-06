// Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
#if REACTIVE_SHIM
using ReactiveUI.Reactive;
using ReactiveUI.Binding.Reactive;
using ReactiveUI.Validation.Reactive.Abstractions;
using ReactiveUI.Validation.Reactive.Capabilities;
using ReactiveUI.Validation.Reactive.Contexts;
using ReactiveUI.Validation.Reactive.Extensions;
using ReactiveUI.Validation.Reactive.Formatters.Abstractions;
using ReactiveUI.Validation.Reactive.Helpers;
using ReactiveUI.Validation.Reactive.States;
using ReactiveUI.Validation.Reactive.ValidationBindings;
#else
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Validation.Abstractions;
using ReactiveUI.Validation.Capabilities;
using ReactiveUI.Validation.Contexts;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Formatters.Abstractions;
using ReactiveUI.Validation.Helpers;
using ReactiveUI.Validation.States;
using ReactiveUI.Validation.ValidationBindings;
#endif
namespace GeneratedValidation.StaticBindings;

internal sealed class Model : ReactiveObject, IValidatableViewModel
{
    public string Name { get; set; } = "";
    public string Other { get; set; } = "";
    public string NameField = "";
    public int ForbiddenReads { get; private set; }
    public string MetadataOnly
    {
        get
        {
            ForbiddenReads++;
            throw new InvalidOperationException("Metadata filtering read the selected value.");
        }
    }
    public ValidationHelper? Rule { get; set => this.RaiseAndSetIfChanged(ref field, value); }
    public IValidationContext ValidationContext { get; } = new ValidationContext();
}
internal sealed class View : ReactiveObject, IViewFor<Model>
{
    public Model? ViewModel { get; set => this.RaiseAndSetIfChanged(ref field, value); }
    object? IViewFor.ViewModel { get => ViewModel; set => ViewModel = (Model?)value; }
    public string Message { get; set; } = "";
    public string? NullableMessage { get; set; }
    public IList<string> NonNullableItems { get; set; } = new List<string>();
    public string OtherMessage { get; set; } = "";
    public string SetterOnly
    {
        get => throw new InvalidOperationException("A target binding read its selected leaf.");
        set => OtherMessage = value;
    }
    public int NormalizedWrites { get; private set; }
    public string NormalizedMessage
    {
        get => Message;
        set
        {
            if (++NormalizedWrites > 10) throw new InvalidOperationException("Normalized setter reconciliation loop.");
            Message = value.Trim();
        }
    }
    public string MessageField = "";
    public Output? Presentation { get; set; }
    public ConvertedOutput Converted { get; set; }
    public NullableConvertedOutput? NullableConverted { get; set; }
    public NullableConvertedOutput NonNullableConverted { get; set; } = new("");
    public int Index { get; set => this.RaiseAndSetIfChanged(ref field, value); }
    public int Writes0 { get; private set; }
    public int Writes1 { get; private set; }
    public int StateAssignments { get; private set; }
    public Action? OnStateWrite { get; set; }
    public WritableState State
    {
        get;
        set
        {
            StateAssignments++;
            if (StateAssignments > 10) throw new InvalidOperationException("Struct write-back notification loop.");
            field = value;
            var reenter = OnStateWrite;
            OnStateWrite = null;
            reenter?.Invoke();
            this.RaisePropertyChanged();
        }
    } = new("", 7);
    public string this[int index]
    {
        get => index == 0 ? Message : OtherMessage;
        set
        {
            if (index == 0)
            {
                Writes0++;
                Message = value;
            }
            else
            {
                Writes1++;
                OtherMessage = value;
            }
        }
    }
    public Panel? Panel
    {
        get;
        set
        {
            if (ReferenceEquals(field, value)) return;
            field = value;
            this.RaisePropertyChanged();
        }
    } = new();
}
internal sealed class Panel
{
    public string Message { get; set; } = "";
    public string? NullableMessage { get; set; }
    public IList<string> NonNullableItems { get; set; } = new List<string>();
    public override bool Equals(object? other) => other is Panel;
    public override int GetHashCode() => 0;
}
internal readonly record struct Output(string Text);
internal readonly record struct ConvertedOutput(string Text)
{
    public static int Conversions { get; private set; }
    public static implicit operator ConvertedOutput(string text) => FromString(text);
    public static ConvertedOutput FromString(string text)
    {
        Conversions++;
        return new ConvertedOutput(text);
    }
}
internal sealed class NullableConvertedOutput(string text)
{
    public string Text { get; } = text;
    public static implicit operator NullableConvertedOutput?(string value) => FromString(value);
    public static NullableConvertedOutput? FromString(string value) =>
        value.Length == 0 ? null : new NullableConvertedOutput(value);
}
internal record struct WritableState(string Error, int Kept);
internal sealed record RichState(bool IsValid, string Code, int Revision) : IValidationState
{
    public IValidationText Text => ValidationText.Create(Code);
}
internal sealed class Formatter : IValidationTextFormatter<Output?>
{
    public readonly List<IValidationText> Inputs = new();
    public Output? Format(IValidationText text)
    {
        Inputs.Add(text);
        return text.Count == 0 ? null : new Output("formatted:" + text.ToSingleLine());
    }
}
internal sealed class Current(IValidationState initial) : IObservable<IValidationState>
{
    private readonly List<IObserver<IValidationState>> _observers = new();
    private IValidationState _current = initial;
    public int Subscribers => _observers.Count;
    public IDisposable Subscribe(IObserver<IValidationState> observer)
    {
        _observers.Add(observer);
        observer.OnNext(_current);
        return new Cleanup(() => _observers.Remove(observer));
    }
    public void Set(IValidationState state)
    {
        _current = state;
        foreach (var observer in _observers.ToArray()) observer.OnNext(state);
    }
}
internal sealed class Cleanup(Action cleanup) : IDisposable
{
    public void Dispose() => cleanup();
}
internal static class StaticBindingsScenario
{
    private static readonly string[] NamePaths = [nameof(Model.Name)];
    private static readonly string[] NameAndOtherPaths = [nameof(Model.Name), nameof(Model.Other)];
    public static void Run()
    {
        if (!CheckInventory() || !CheckProjection()) throw new InvalidOperationException("Static binding scenario failed.");
    }
    private static bool CheckInventory()
    {
        var model = new Model();
        var view = new View { ViewModel = model };
        IValidationState first = new RichState(false, "name-required", 41);
        var states = new Current(first);
        using var rule = model.AddObservableRule(states, NamePaths);
        model.Rule = rule;
        var formatter = new Formatter();
        var bindings = new List<IDisposable>
        {
            ValidationBinding.ForProperty<View, Model, string, string>(view, x => x.Name, x => x.Message),
            ValidationBinding.ForProperty<View, Model, string, string>(view, x => x.Name, x => x.Message, null),
            ValidationBinding.ForProperty<View, Model, string, string>(view, x => x.Name, x => x.Message, null, false),
            ValidationBinding.ForProperty<View, Model, string, Output?>(view, x => x.Name, static (raw, formatted) => { }, formatter),
            ValidationBinding.ForProperty<View, Model, string, Output?>(view, x => x.Name, static (raw, formatted) => { }, formatter, false),
            ValidationBinding.ForValidationHelperProperty<View, Model, string>(view, x => x!.Rule, x => x.Message),
            ValidationBinding.ForValidationHelperProperty<View, Model, string>(view, x => x!.Rule, x => x.Message, null),
            ValidationBinding.ForValidationHelperProperty<View, Model, Output?>(view, x => x!.Rule, static (raw, formatted) => { }, formatter),
            ValidationBinding.ForViewModel<View, Model, string>(view, x => x.Message),
            ValidationBinding.ForViewModel<View, Model, string>(view, x => x.Message, null),
            ValidationBinding.ForViewModel<View, Model, Output?>(view, static formatted => { }, formatter),
        };
        Require(view.Message == "name-required", "static target initial state");
        Require(formatter.Inputs.Count >= 4, "custom formatter callbacks");
        foreach (var binding in bindings) binding.Dispose();
        return true;
    }

    private static bool CheckProjection()
    {
        var model = new Model();
        var view = new View { ViewModel = model };
        IValidationState first = new RichState(false, "first", 7);
        IValidationState second = new RichState(false, "second", 8);
        var firstStates = new Current(first);
        var secondStates = new Current(second);
        using var firstRule = model.AddObservableRule(firstStates, NamePaths);
        using var secondRule = model.AddObservableRule(secondStates, NameAndOtherPaths);
        model.Rule = firstRule;
        var formatter = new Formatter();
        IList<IValidationState>? latestRaw = null;
        IList<Output?>? latestFormatted = null;
        IValidationState? latestHelper = null;
        Output? latestHelperOutput = null;
        using var property = ValidationBinding.ForProperty<View, Model, string, Output?>(
            formatter: formatter, strict: false, viewModelProperty: x => x.Name, view: view,
            action: (raw, formatted) => { latestRaw = raw; latestFormatted = formatted; });
        using var helper = ValidationBinding.ForValidationHelperProperty<View, Model, Output?>(
            view, x => x!.Rule, (raw, formatted) => { latestHelper = raw; latestHelperOutput = formatted; }, formatter);
        Require(latestRaw!.Count == 2 && ReferenceEquals(latestRaw[0], first) && ReferenceEquals(latestRaw[1], second), "raw state identity and rule order");
        Require(latestFormatted![0]?.Text == "formatted:first" && latestFormatted[1]?.Text == "formatted:second", "ordered FormatAll outputs");
        Require(ReferenceEquals(latestHelper, first) && latestHelperOutput?.Text == "formatted:first", "helper raw state identity");
        IValidationState changed = new RichState(false, "first", 9);
        firstStates.Set(changed);
        Require(ReferenceEquals(latestRaw![0], changed) && ReferenceEquals(latestHelper, changed), "same-message custom state update");
        model.Rule = null;
        Require(latestHelper!.IsValid && latestHelperOutput is null, "null helper passes valid text through custom formatter");
        view.ViewModel = null;
        Require(latestRaw!.Count == 0 && latestFormatted!.Count == 0, "missing model empty projection");
        view.ViewModel = model;
        Require(latestRaw!.Count == 2 && ReferenceEquals(latestRaw[0], changed), "reattached current rule states");
        helper.Dispose();
        property.Dispose();
        firstStates.Set(new RichState(false, "disposed", 10));
        Require(ReferenceEquals(latestRaw![0], changed), "disposed callbacks remain unchanged");
        return true;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
