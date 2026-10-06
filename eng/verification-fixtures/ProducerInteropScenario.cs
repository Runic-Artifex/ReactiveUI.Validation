// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI.SourceGenerators;
#if REACTIVE_SHIM
using ReactiveUI.Reactive;
using ReactiveUI.Binding.Reactive;
using ReactiveUI.Validation.Reactive.Abstractions;
using ReactiveUI.Validation.Reactive.Contexts;
using ReactiveUI.Validation.Reactive.Extensions;
using ReactiveUI.Validation.Reactive.States;
using SchedulerApi = System.Reactive.Concurrency.IScheduler;
using Immediate = System.Reactive.Concurrency.ImmediateScheduler;
#else
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Validation.Abstractions;
using ReactiveUI.Validation.Contexts;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.States;
using SchedulerApi = ReactiveUI.Primitives.Concurrency.ISequencer;
using Immediate = ReactiveUI.Primitives.Concurrency.ImmediateSequencer;
#endif

/// <summary>Shared actual-package fixture for producer notification, commands, collections and OAPH lifetime.</summary>
internal static partial class ProducerInteropScenario
{
    /// <summary>Runs the same real-producer behavior in managed, trimmed and native consumers.</summary>
    internal static void Run()
    {
        using var model = new ProducerInteropModel();
        CheckFields(model);
        CheckCollections(model);
        CheckCommand(model);
        CheckAsyncCommands(model);
        CheckOutputProperty(model);
        model.Dispose();
        Check(model.CaptionSubscribers == 0 && model.CommandSubscribers == 0 && model.AsyncAdmissionSubscribers == 0,
            "Disposal releases actual helper and all command admission subscriptions.");
    }

    /// <summary>Checks field-generated and declared partial property updates and nullable leaf values.</summary>
    /// <param name="model">The actual producer-generated model.</param>
    private static void CheckFields(ProducerInteropModel model)
    {
        using var rule = model.ValidationRule(value => value.Name, static value => value == "valid", "producer-name");
        using var declared = model.ValidationRule(value => value.Declared, static value => value == "declared", "producer-declared");
        var nameState = new StateSink();
        var declaredState = new StateSink();
        using var nameSubscription = rule.ValidationChanged.Subscribe(nameState);
        using var declaredSubscription = declared.ValidationChanged.Subscribe(declaredState);
        Check(!nameState.Valid && !declaredState.Valid, "Generated nullable and declared partial selectors emit their initial invalid state.");
        model.Name = "valid";
        model.Declared = "declared";
        Check(nameState.Valid && declaredState.Valid, "Both actual producers notify Validation after assignment.");
        model.Name = null;
        Check(!nameState.Valid, "A present nullable leaf returning null is evaluated by the predicate.");
        rule.Dispose();
        var count = nameState.Count;
        model.Name = "valid";
        Check(nameState.Count == count, "Disposal detaches generated field rule observation.");
    }

    /// <summary>Checks actual collection member notifications, owner replacement, null and derived-list changes.</summary>
    /// <param name="model">The actual producer-generated model.</param>
    private static void CheckCollections(ProducerInteropModel model)
    {
        using var itemsRule = model.ValidationRule(value => value.Items!.Count, static value => value > 0, "producer-items");
        using var rowsRule = model.ValidationRule(value => value.Rows.Count, static value => value > 0, "producer-rows");
        var items = new StateSink();
        var rows = new StateSink();
        using var itemsSubscription = itemsRule.ValidationChanged.Subscribe(items);
        using var rowsSubscription = rowsRule.ValidationChanged.Subscribe(rows);
        Check(!items.Valid && !rows.Valid, "Actual generated collection and derived-list members begin empty.");
        var detached = model.Items!;
        detached.Add("first");
        model.AddRow("first");
        Check(items.Valid && rows.Valid, "Nested collection owners notify count rules.");
        model.Items = null;
        Check(!items.Valid, "A missing generated collection owner invalidates its rule.");
        var count = items.Count;
        detached.Add("detached");
        Check(items.Count == count, "Detached collections do not invalidate the current generated selector.");
        model.Items = new ObservableCollection<string> { "replacement" };
        Check(items.Valid, "Replacing a generated collection replays its current count.");
        model.ClearRows();
        Check(!rows.Valid, "Actual derived-list mutation continues to notify Validation.");
    }

    /// <summary>Checks actual generated command gating/execution and its untouched observable metadata overload.</summary>
    /// <param name="model">The actual producer-generated model.</param>
    private static void CheckCommand(ProducerInteropModel model)
    {
        using var metadata = model.ValidationRule(value => value.SubmitCommand, new ProducerCurrent<bool>(true), "producer-command-metadata");
        var command = (ICommand)model.SubmitCommand;
        Check(!command.CanExecute(null), "Actual command generation consumes its configured false can-execute source.");
        model.AllowSubmit(true);
        Check(command.CanExecute(null), "Actual command generation observes can-execute updates.");
        command.Execute(null);
        Check(model.Submitted == 1, "The actual generated command invokes the annotated method.");
        model.AllowSubmit(false);
        Check(!command.CanExecute(null), "The actual generated command gates later execution.");
    }

    /// <summary>Checks real Task/token and observable command admission, execution and cancellation ownership.</summary>
    /// <param name="model">The actual producer-generated model.</param>
    private static void CheckAsyncCommands(ProducerInteropModel model)
    {
        using var rule = model.ValidationRule(value => value.Name, static value => value == "ready", "producer-command-admission");
        using var admission = rule.ValidationChanged.Subscribe(new AdmissionSink(model));
        using var metadata = model.ValidationRule(value => value.FetchCommand, new ProducerCurrent<bool>(true), "producer-task-metadata");
        var taskCommand = (ICommand)model.FetchCommand;
        var observableCommand = (ICommand)model.WatchCommand;
        Check(!taskCommand.CanExecute(null) && !observableCommand.CanExecute(null), "Invalid validation synchronously denies both real generated commands.");
        model.Name = "ready";
        Check(!taskCommand.CanExecute(null), "Valid validation alone does not override the domain admission gate.");
        model.AllowAsync(true);
        Check(taskCommand.CanExecute(null) && observableCommand.CanExecute(null), "Validation and domain admission synchronously enable both actual commands.");
        CheckTaskExecution(model, taskCommand);
        CheckObservableExecution(model, observableCommand);
        model.Name = null;
        Check(!taskCommand.CanExecute(null) && !observableCommand.CanExecute(null), "A later invalid rule synchronously closes both command gates.");
        model.Name = "ready";
        model.AllowAsync(false);
        Check(!taskCommand.CanExecute(null) && !observableCommand.CanExecute(null), "Domain invalidation closes both gates while validation remains valid.");
    }

    /// <summary>Checks a generated Task result, cancellation token and a later execution after cancellation.</summary>
    /// <param name="model">The actual generated command owner.</param>
    /// <param name="command">The same command through its UI admission contract.</param>
    private static void CheckTaskExecution(ProducerInteropModel model, ICommand command)
    {
        using var admission = new ProducerBooleanSink();
        using var admissionSubscription = model.FetchCommand.CanExecute.Subscribe(admission);
        using var results = new ProducerResultSink<int>();
        using var published = model.FetchCommand.Subscribe(results);
        using var exceptions = new ProducerResultSink<Exception>();
        using var exceptionSubscription = model.FetchCommand.ThrownExceptions.Subscribe(exceptions);
        using var first = new ProducerResultSink<int>();
        using var execution = model.FetchCommand.Execute().Subscribe(first);
        Check(model.TaskStarts == 1 && !command.CanExecute(null), "The actual Task command starts once and denies overlapping UI admission.");
        model.CompleteTask(17);
        Check(first.WaitForCompletion() && first.Count == 1 && first.Value == 17 && first.Error is null,
            "The actual generated Task command publishes its completed result.");
        Check(admission.WaitForTrue() && results.Count == 1 && results.Value == 17 && command.CanExecute(null),
            "Completed execution restores admission and publishes the command result.");
        using var cancelled = new ProducerResultSink<int>();
        var cancelledExecution = model.FetchCommand.Execute().Subscribe(cancelled);
        var abandoned = model.PendingTask;
        var token = model.TaskToken;
        cancelledExecution.Dispose();
        Check(token.IsCancellationRequested, "Disposing the owned execution signals its real CancellationToken without assuming its Task has stopped.");
        using var current = new ProducerResultSink<int>();
        using var currentExecution = model.FetchCommand.Execute().Subscribe(current);
        Check(model.TaskStarts == 3 && !command.CanExecute(null), "Explicit execution can start fresh work while UI admission remains closed for in-flight work.");
        abandoned.SetResult(99);
        Check(cancelled.WaitForCompletion() && cancelled.Error is OperationCanceledException && cancelled.Count == 0 && current.Count == 0 && !command.CanExecute(null),
            "Cooperative Task cancellation terminates abandoned work without publishing its result or reopening admission around pending current work.");
        model.CompleteTask(23);
        Check(current.WaitForCompletion() && current.Value == 23 && current.Error is null, "A fresh Task command execution remains usable after cancellation.");
        Check(admission.WaitForTrue() && cancelled.Count == 0 && results.Count == 2 && results.Value == 23,
            "A late completion from a cancelled execution cannot replace the current published result.");
        Check(exceptions.Count == 1 && exceptions.Value is OperationCanceledException, "The actual command exposes cooperative cancellation through its observed exception contract.");
    }

    /// <summary>Checks actual observable-result command values, terminal release and detached callbacks.</summary>
    /// <param name="model">The actual generated command owner.</param>
    /// <param name="command">The same command through its UI admission contract.</param>
    private static void CheckObservableExecution(ProducerInteropModel model, ICommand command)
    {
        using var results = new ProducerResultSink<int>();
        using var published = model.WatchCommand.Subscribe(results);
        using var first = new ProducerResultSink<int>();
        using var execution = model.WatchCommand.Execute().Subscribe(first);
        Check(model.WatchStarts == 1 && model.WatchSubscribers == 1 && !command.CanExecute(null), "The real observable command owns one live source subscription while executing.");
        model.EmitWatch(31);
        Check(first.Count == 1 && first.Value == 31 && results.Value == 31, "The generated observable-result command forwards actual source values.");
        model.CompleteWatch();
        Check(first.WaitForCompletion() && model.WatchSubscribers == 0 && command.CanExecute(null), "Source completion releases ownership and restores command admission.");
        using var cancelled = new ProducerResultSink<int>();
        var cancelledExecution = model.WatchCommand.Execute().Subscribe(cancelled);
        cancelledExecution.Dispose();
        Check(model.WatchSubscribers == 0 && command.CanExecute(null), "Disposing the owned observable execution detaches its source and restores admission.");
        model.EmitWatch(101);
        model.CompleteWatch();
        Check(cancelled.Count == 0 && results.Count == 1 && results.Value == 31, "Later source emissions cannot reach an unsubscribed observable execution.");
    }

    /// <summary>Checks declared partial OAPH initialization, source updates, nullable values and rule disposal.</summary>
    /// <param name="model">The actual producer-generated model.</param>
    private static void CheckOutputProperty(ProducerInteropModel model)
    {
        using var rule = model.ValidationRule(value => value.Caption, static value => value == "updated", "producer-caption");
        var state = new StateSink();
        using var subscription = rule.ValidationChanged.Subscribe(state);
        Check(model.Caption == "initial" && !state.Valid && model.CaptionSubscribers == 1, "Actual partial OAPH initialization is visible to the original selector.");
        model.SetCaption("updated");
        Check(model.Caption == "updated" && state.Valid, "The typed OAPH callback notifies its actual generated partial property.");
        model.SetCaption(null);
        Check(model.Caption is null && !state.Valid, "Nullable OAPH values retain their actual producer contract.");
        rule.Dispose();
        var count = state.Count;
        model.SetCaption("updated");
        Check(state.Count == count, "Rule disposal releases generated partial property observation.");
    }

    /// <summary>Requires the actual producer/runtime contract.</summary>
    /// <param name="condition">Whether the contract holds.</param>
    /// <param name="message">The contract that failed.</param>
    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>Captures the most recently observed actual rule state.</summary>
    private sealed class StateSink : IObserver<IValidationState>
    {
        /// <summary>Gets the latest validity.</summary>
        internal bool Valid { get; private set; }

        /// <summary>Gets the number of observed state updates.</summary>
        internal int Count { get; private set; }

        /// <inheritdoc />
        public void OnNext(IValidationState value) { Valid = value.IsValid; Count++; }

        /// <inheritdoc />
        public void OnError(Exception error) => throw error;

        /// <inheritdoc />
        public void OnCompleted() { }
    }

    /// <summary>Feeds current actual validation into the separate synchronous domain admission source.</summary>
    /// <param name="model">The actual generated command owner.</param>
    private sealed class AdmissionSink(ProducerInteropModel model) : IObserver<IValidationState>
    {
        /// <inheritdoc />
        public void OnNext(IValidationState value) => model.SetValidationAdmission(value.IsValid);

        /// <inheritdoc />
        public void OnError(Exception error) => throw error;

        /// <inheritdoc />
        public void OnCompleted() { }
    }

}

/// <summary>Requests implementations from the real pinned peer generators.</summary>
[IReactiveObject]
internal sealed partial class ProducerInteropModel : IValidatableViewModel, IDisposable
{
    /// <summary>The generated nullable field property.</summary>
    [Reactive]
    private string? _name;

    /// <summary>The generated replaceable collection property.</summary>
    [ReactiveCollection]
    private ObservableCollection<string>? _items = new();

    /// <summary>The actual generated read-only derived collection property.</summary>
    [BindableDerivedList]
    private readonly ReadOnlyObservableCollection<string> _rows;

    /// <summary>The stable underlying derived collection storage.</summary>
    private readonly ObservableCollection<string> _rowStorage = new();

    /// <summary>The command's typed can-execute input.</summary>
    private readonly ProducerCurrent<bool> _canSubmit = new(false);

    /// <summary>The validation and domain admission conjunction used by both async commands.</summary>
    private readonly ProducerCurrent<bool> _canRun = new(false);

    /// <summary>The controlled actual observable command source.</summary>
    private readonly ProducerCommandResults _watch = new();

    /// <summary>The latest validation admission state.</summary>
    private bool _validationAllowed;

    /// <summary>The independent domain admission state.</summary>
    private bool _domainAllowed;

    /// <summary>The output helper's typed source.</summary>
    private readonly ProducerCurrent<string?> _captions = new("initial");

    /// <summary>The typed result factory retains the annotated observable-returning method contract.</summary>
    private readonly Func<IObservable<int>> _watchSource;

    /// <summary>Initializes the actual generated output helper and derived-list backing storage.</summary>
    public ProducerInteropModel()
    {
        _watchSource = () => _watch;
        _rows = new(_rowStorage);
        _captionHelper = new(_captions, _ => this.RaisePropertyChanged(nameof(Caption)), "initial", false, Immediate.Instance);
    }

    /// <inheritdoc />
    public IValidationContext ValidationContext { get; } = new ValidationContext();

    /// <summary>Gets or sets a declared partial property implemented by the real Reactive producer.</summary>
    [Reactive]
    public partial string? Declared { get; set; }

    /// <summary>Gets the declared partial property implemented by the real Binding producer.</summary>
    [ObservableAsProperty(InitialValue = "\"initial\"")]
    public partial string? Caption { get; }

    /// <summary>Gets the actual annotated command invocation count.</summary>
    public int Submitted { get; private set; }

    /// <summary>Gets the actual output-helper source subscription count.</summary>
    public int CaptionSubscribers => _captions.Subscribers;

    /// <summary>Gets the actual command can-execute source subscription count.</summary>
    public int CommandSubscribers => _canSubmit.Subscribers;

    /// <summary>Gets the number of actual async command admission subscriptions.</summary>
    public int AsyncAdmissionSubscribers => _canRun.Subscribers;

    /// <summary>Gets the number of annotated Task method invocations.</summary>
    public int TaskStarts { get; private set; }

    /// <summary>Gets the current controlled Task result completion.</summary>
    public TaskCompletionSource<int> PendingTask { get; private set; } = new();

    /// <summary>Gets the CancellationToken supplied by the actual generated command factory.</summary>
    public CancellationToken TaskToken { get; private set; }

    /// <summary>Gets the number of annotated observable method invocations.</summary>
    public int WatchStarts { get; private set; }

    /// <summary>Gets the number of owned observable-result subscriptions.</summary>
    public int WatchSubscribers => _watch.Subscribers;

    /// <summary>Gets the typed command execution scheduler.</summary>
    private static SchedulerApi CommandScheduler => Immediate.Instance;

    /// <summary>Gets the typed source referenced by the actual command attribute.</summary>
    private IObservable<bool> CanSubmit => _canSubmit;

    /// <summary>Gets the actual combined admission source configured by both async command attributes.</summary>
    private IObservable<bool> CanRun => _canRun;

    /// <summary>Releases actual producer-created infrastructure.</summary>
    public void Dispose()
    {
        _captionHelper?.Dispose();
        SubmitCommand.Dispose();
        FetchCommand.Dispose();
        WatchCommand.Dispose();
    }

    /// <summary>Changes the actual output helper's value.</summary>
    /// <param name="value">The nullable current value.</param>
    internal void SetCaption(string? value) => _captions.Set(value);

    /// <summary>Changes the actual command's can-execute value.</summary>
    /// <param name="allowed">Whether command execution is available.</param>
    internal void AllowSubmit(bool allowed) => _canSubmit.Set(allowed);

    /// <summary>Updates the independent synchronous domain admission gate.</summary>
    /// <param name="allowed">Whether the domain permits command execution.</param>
    internal void AllowAsync(bool allowed) { _domainAllowed = allowed; _canRun.Set(_domainAllowed && _validationAllowed); }

    /// <summary>Updates admission from the actual rule's current state.</summary>
    /// <param name="allowed">Whether the rule is currently valid.</param>
    internal void SetValidationAdmission(bool allowed) { _validationAllowed = allowed; _canRun.Set(_domainAllowed && _validationAllowed); }

    /// <summary>Completes the current actual Task-returning command method.</summary>
    /// <param name="value">The command result.</param>
    internal void CompleteTask(int value) => PendingTask.SetResult(value);

    /// <summary>Emits an actual observable command source result.</summary>
    /// <param name="value">The command result.</param>
    internal void EmitWatch(int value) => _watch.Emit(value);

    /// <summary>Completes the actual observable command source.</summary>
    internal void CompleteWatch() => _watch.Complete();

    /// <summary>Mutates the stable collection wrapped by the generated derived-list property.</summary>
    /// <param name="value">The added row.</param>
    internal void AddRow(string value) => _rowStorage.Add(value);

    /// <summary>Removes all rows from the actual derived-list source.</summary>
    internal void ClearRows() => _rowStorage.Clear();

    /// <summary>The method implemented by the actual generated command.</summary>
    [ReactiveCommand(CanExecute = nameof(CanSubmit), OutputScheduler = nameof(CommandScheduler))]
    private void Submit() => Submitted++;

    /// <summary>The Task method consumed by the actual pinned command generator.</summary>
    /// <param name="cancellationToken">The real command execution cancellation token.</param>
    /// <returns>The controlled actual asynchronous command result.</returns>
    [ReactiveCommand(CanExecute = nameof(CanRun), OutputScheduler = nameof(CommandScheduler))]
    private async Task<int> FetchAsync(CancellationToken cancellationToken)
    {
        TaskStarts++;
        TaskToken = cancellationToken;
        PendingTask = new();
        var result = await PendingTask.Task.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    /// <summary>The observable method consumed by the actual pinned command generator.</summary>
    /// <returns>The controlled actual observable command result source.</returns>
    [ReactiveCommand(CanExecute = nameof(CanRun), OutputScheduler = nameof(CommandScheduler))]
    private IObservable<int> Watch() { WatchStarts++; return _watchSource(); }
}


/// <summary>A deterministic typed replay source with observable subscription ownership.</summary>
/// <typeparam name="T">The exact current value type.</typeparam>
internal sealed class ProducerCurrent<T>(T value) : IObservable<T>
{
    /// <summary>The live typed observers.</summary>
    private readonly List<IObserver<T>> _observers = [];

    /// <summary>The current replay value.</summary>
    private T _value = value;

    /// <summary>Gets the number of owned subscriptions.</summary>
    internal int Subscribers => _observers.Count;

    /// <inheritdoc />
    public IDisposable Subscribe(IObserver<T> observer)
    {
        _observers.Add(observer);
        observer.OnNext(_value);
        return new ProducerSubscription<T>(_observers, observer);
    }

    /// <summary>Updates each current typed subscriber.</summary>
    /// <param name="current">The next current value.</param>
    internal void Set(T current)
    {
        _value = current;
        foreach (var observer in _observers.ToArray())
        {
            observer.OnNext(current);
        }
    }
}

/// <summary>Removes a single borrowed observer on disposal.</summary>
/// <typeparam name="T">The exact observed value type.</typeparam>
/// <param name="observers">The source's live observer collection.</param>
/// <param name="observer">The selected observer.</param>
internal sealed class ProducerSubscription<T>(List<IObserver<T>> observers, IObserver<T> observer) : IDisposable
{
    /// <inheritdoc />
    public void Dispose() => observers.Remove(observer);
}

/// <summary>Captures real asynchronous command notifications with a bounded terminal signal.</summary>
/// <typeparam name="T">The command result type.</typeparam>
internal sealed class ProducerResultSink<T> : IObserver<T>, IDisposable
{
    /// <summary>The signal for actual terminal notification rather than a timing delay.</summary>
    private readonly ManualResetEventSlim _completed = new();

    /// <summary>The number of actual result notifications.</summary>
    private int _count;

    /// <summary>Gets the most recently published command result.</summary>
    internal T? Value { get; private set; }

    /// <summary>Gets the actual result notification count.</summary>
    internal int Count => Volatile.Read(ref _count);

    /// <summary>Gets any actual command execution failure.</summary>
    internal Exception? Error { get; private set; }

    /// <inheritdoc />
    public void OnNext(T value) { Value = value; Interlocked.Increment(ref _count); }

    /// <inheritdoc />
    public void OnError(Exception error) { Error = error; _completed.Set(); }

    /// <inheritdoc />
    public void OnCompleted() => _completed.Set();

    /// <inheritdoc />
    public void Dispose() => _completed.Dispose();

    /// <summary>Waits only for an actual terminal callback with a finite test failure boundary.</summary>
    /// <returns>Whether the actual command terminated within the verification boundary.</returns>
    internal bool WaitForCompletion() => _completed.Wait(TimeSpan.FromSeconds(5));
}

/// <summary>Waits for the command's actual restored admission rather than guessing async cleanup timing.</summary>
internal sealed class ProducerBooleanSink : IObserver<bool>, IDisposable
{
    /// <summary>The current actual admission signal.</summary>
    private readonly ManualResetEventSlim _available = new();

    /// <inheritdoc />
    public void OnNext(bool value)
    {
        if (value)
        {
            _available.Set();
        }
        else
        {
            _available.Reset();
        }
    }

    /// <inheritdoc />
    public void OnError(Exception error) => throw error;

    /// <inheritdoc />
    public void OnCompleted() { }

    /// <inheritdoc />
    public void Dispose() => _available.Dispose();

    /// <summary>Waits for actual available admission with a finite test failure boundary.</summary>
    /// <returns>Whether the command reported available admission within the verification boundary.</returns>
    internal bool WaitForTrue() => _available.Wait(TimeSpan.FromSeconds(5));
}

/// <summary>A controlled nonreplaying observable returned by a real annotated command method.</summary>
internal sealed class ProducerCommandResults : IObservable<int>
{
    /// <summary>The currently owned result subscriptions.</summary>
    private readonly List<IObserver<int>> _observers = [];

    /// <summary>Gets the number of currently owned subscriptions.</summary>
    internal int Subscribers => _observers.Count;

    /// <inheritdoc />
    public IDisposable Subscribe(IObserver<int> observer)
    {
        _observers.Add(observer);
        return new ProducerSubscription<int>(_observers, observer);
    }

    /// <summary>Emits a current actual result to the subscribed command execution.</summary>
    /// <param name="value">The current command result.</param>
    internal void Emit(int value)
    {
        foreach (var observer in _observers.ToArray())
        {
            observer.OnNext(value);
        }
    }

    /// <summary>Terminates all current result subscriptions.</summary>
    internal void Complete()
    {
        var observers = _observers.ToArray();
        _observers.Clear();
        foreach (var observer in observers)
        {
            observer.OnCompleted();
        }
    }
}
