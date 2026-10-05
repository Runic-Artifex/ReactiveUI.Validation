// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE in the project root.

namespace Runic.Validation.Benchmarks;

internal static class Correctness
{
    internal static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

internal sealed class BenchmarkViewModel : ReactiveObject, IValidatableViewModel, IDisposable
{
    public string Name
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "valid";

    public IValidationContext ValidationContext { get; } = new ValidationContext(ImmediateScheduler.Instance);

    public void Dispose() => ValidationContext.Dispose();
}

// A controlled component isolates aggregate context cost from property observation and rule construction.
internal sealed class MutableComponent : IValidationComponent, IDisposable
{
    private readonly ReplaySignal<IValidationState> _states = new(1);

    internal MutableComponent(bool valid)
    {
        IsValid = valid;
        Text = valid ? ValidationText.None : ValidationText.Create("invalid-a");
        _states.OnNext(new ValidationState(IsValid, Text));
    }

    public bool IsValid { get; private set; }

    public IValidationText Text { get; private set; }

    public IObservable<IValidationState> ValidationStatusChange => _states;

    internal void Set(bool valid, string message)
    {
        IsValid = valid;
        Text = valid ? ValidationText.None : ValidationText.Create(message);
        _states.OnNext(new ValidationState(IsValid, Text));
    }

    public void Dispose() => _states.Dispose();
}

internal sealed class StateObserver : IObserver<IValidationState>
{
    internal int Notifications { get; private set; }

    internal IValidationState? Latest { get; private set; }

    public void OnNext(IValidationState value)
    {
        Notifications++;
        Latest = value;
    }

    public void OnCompleted()
    {
    }

    public void OnError(Exception error) => throw new InvalidOperationException("Validation observable failed.", error);
}
