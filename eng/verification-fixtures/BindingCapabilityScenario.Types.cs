// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
using ReactiveUI.Validation.Reactive.Collections;
using ReactiveUI.Validation.Reactive.Components.Abstractions;
using ReactiveUI.Validation.Reactive.ValidationBindings;
using ReactiveUI.Validation.Reactive.ValidationBindings.Abstractions;
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
using ReactiveUI.Validation.Collections;
using ReactiveUI.Validation.Components.Abstractions;
using ReactiveUI.Validation.ValidationBindings;
using ReactiveUI.Validation.ValidationBindings.Abstractions;
#endif

namespace BindingCapabilityInputs;

internal sealed class Model : ReactiveObject, IValidatableViewModel
{
    public string Name { get; set; } = "";
    public string Other { get; set; } = "";
    public string NameField = "";
    private ValidationHelper? PrivateRule => Rule;
    public void SetPrivateRule(ValidationHelper? helper)
    {
        Rule = helper;
        this.RaisePropertyChanged(nameof(PrivateRule));
    }
    public IValidationBinding BindPrivateHelper(View view, Action<IValidationState> onNext) =>
        view.BindValidationState(this, owner => owner.PrivateRule, static state => state, onNext);
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
    public SourceParent? Parent { get; set => this.RaiseAndSetIfChanged(ref field, value); }
    public IValidationContext ValidationContext { get; } = new ShadowContext();
    public ShadowContext? Selected
    {
        get;
        set
        {
            if (ReferenceEquals(field, value)) return;
            field = value;
            this.RaisePropertyChanged();
        }
    }
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
    public ValidationStoragePolicy StatePolicy { get; } = new();
    private WritableState _ownedState = new("", 7);
    public WritableState OwnedState
    {
        get => _ownedState;
        set => SetOwnedState(value, null);
    }
    public void SetOwnedState(WritableState value, object? origin)
    {
        if (++StateAssignments > 20) throw new InvalidOperationException("Peer struct write-back notification loop.");
        _ownedState = value;
        StatePolicy.RecordChange(origin);
        var reenter = OnStateWrite;
        OnStateWrite = null;
        reenter?.Invoke();
        this.RaisePropertyChanged(nameof(OwnedState));
    }
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public string AllowProperty { get; set => field = value ?? ""; } = "";
    public string AllowParameter
    {
        get;
        [param: System.Diagnostics.CodeAnalysis.AllowNull]
        set => field = value ?? "";
    } = "";
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public string AllowField = "";
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public string this[bool key]
    {
        get => AllowProperty;
        set => AllowProperty = value;
    }
    public string ParameterIndexerResult { get; private set; } = "";
    public string this[decimal key]
    {
        get => ParameterIndexerResult;
        [param: System.Diagnostics.CodeAnalysis.AllowNull]
        set => ParameterIndexerResult = value ?? "";
    }
    [System.Diagnostics.CodeAnalysis.AllowNull]
    private string PrivateAllow { get; set => field = value ?? ""; } = "";
    public string PrivateAllowResult => PrivateAllow;
    public IValidationBinding BindPrivateAllow(Model model) =>
        this.BindValidationState<View, Model, string?>(model, x => x.Rule,
            x => x.PrivateAllow, static state => state.IsValid ? null : state.Text.ToSingleLine());
    [System.Diagnostics.CodeAnalysis.DisallowNull]
    public string? DisallowProperty { get; set; } = "";
    public string? DisallowParameter
    {
        get;
        [param: System.Diagnostics.CodeAnalysis.DisallowNull]
        set;
    } = "";
    [System.Diagnostics.CodeAnalysis.DisallowNull]
    public string? DisallowField = "";
    [System.Diagnostics.CodeAnalysis.DisallowNull]
    public string? this[string key]
    {
        get => DisallowParameter;
        set => DisallowParameter = value;
    }
    public readonly record struct ValuePayload(int Number);
    [System.Diagnostics.CodeAnalysis.DisallowNull]
    public int? RequiredNumber { get; set; } = 0;
    [System.Diagnostics.CodeAnalysis.DisallowNull]
    private ValuePayload? RequiredPayload { get; set; } = new ValuePayload(0);
    public ValuePayload? PayloadResult => RequiredPayload;
    public void SetPayload(ValuePayload value) => RequiredPayload = value;
    public static Expression<Func<View, ValuePayload?>> PayloadExpression() => owner => owner.RequiredPayload;
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
    private static int _conversions;
    public static int Conversions => System.Threading.Volatile.Read(ref _conversions);
    public static implicit operator ConvertedOutput(string text) => FromString(text);
    public static ConvertedOutput FromString(string text)
    {
        System.Threading.Interlocked.Increment(ref _conversions);
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
internal sealed class SourceParent : ReactiveObject
{
    public int? Number { get; set => this.RaiseAndSetIfChanged(ref field, value); }
}
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

internal static partial class Outer<TOuter> where TOuter : class
{
    public sealed partial class Inner<T> : ReactiveObject, IViewFor<Model> where T : struct
    {
        private string PrivateMessage { get; set; } = "";
        public string Result => PrivateMessage;
        public Model? ViewModel { get; set => this.RaiseAndSetIfChanged(ref field, value); }
        object? IViewFor.ViewModel { get => ViewModel; set => ViewModel = (Model?)value; }
        public IDisposable Bind(Model model) => this.BindValidation(model, x => x.Name, x => x.PrivateMessage);
    }
}
internal sealed class ShadowContext : ValidationContext
{
    private new IObservable<IValidationState> ValidationStatusChange =>
        throw new InvalidOperationException("Selected context used a concrete hidden stream.", new InvalidOperationException(base.ToString()));
    public override bool Equals(object? other) => other is ShadowContext;
    public override int GetHashCode() => 0;
}
/// <summary>Records original expression argument evaluation without assuming callback execution.</summary>
internal sealed class EvaluationCounter
{
    private int _count;
    public int Count => System.Threading.Volatile.Read(ref _count);
    public void Increment() => System.Threading.Interlocked.Increment(ref _count);
}
