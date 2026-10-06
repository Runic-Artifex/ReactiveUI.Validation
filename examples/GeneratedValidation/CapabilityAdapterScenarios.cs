// Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
#if REACTIVE_SHIM
using ReactiveUI.Validation.Reactive.Capabilities;
#else
using ReactiveUI.Validation.Capabilities;
#endif

internal static class CapabilityAdapterScenarios
{
    internal static void LatestRequestsAndOwnerScheduling()
    {
        var inputs = new Current<string>("first");
        var scheduler = new CapabilityScheduler();
        var requests = new Dictionary<string, TaskCompletionSource<IValidationState>>();
        var tokens = new Dictionary<string, CancellationToken>();
        var states = new List<IValidationState>();
        var pending = new ValidationState(false, "checking");
        var stream = ValidationAsync.ForLatest(inputs, (value, token) =>
        {
            tokens.Add(value, token);
            var request = new TaskCompletionSource<IValidationState>();
            requests.Add(value, request);
            return request.Task;
        }, pending, scheduler);
        using var subscription = stream.Subscribe(new Observer<IValidationState>(states.Add));
        Check(ReferenceEquals(states[^1], pending), "Pending validation is synchronous domain state.");
        inputs.Set("second");
        Check(tokens["first"].IsCancellationRequested, "Changing input cancels the obsolete request.");
        requests["first"].SetResult(new ValidationState(false, "obsolete"));
        scheduler.Drain();
        Check(states.All(static state => state.Text!.ToSingleLine() != "obsolete"), "A late result cannot rejoin the current request.");
        requests["second"].SetResult(ValidationState.Valid);
        Check(ReferenceEquals(states[^1], pending), "Completion is deferred to the explicit model owner.");
        scheduler.Drain();
        Check(ReferenceEquals(states[^1], ValidationState.Valid), "Current completion reaches the model owner.");
        inputs.Set("third");
        subscription.Dispose();
        subscription.Dispose();
        Check(tokens["third"].IsCancellationRequested && inputs.Subscribers == 0, "Disposal cancels current work and releases borrowed input subscription.");
        var count = states.Count;
        requests["third"].SetResult(ValidationState.Valid);
        scheduler.Drain();
        Check(states.Count == count, "Disposed work cannot deliver stale completion.");
    }

    internal static void StableRowsAndSourceOwnership()
    {
        var first = new Row(1, false);
        var second = new Row(2, true);
        var replacement = new Row(1, true);
        var snapshots = new Current<IReadOnlyCollection<Row>>([]);
        var other = new Current<IReadOnlyCollection<Row>>([second]);
        var sources = new Current<IObservable<IReadOnlyCollection<Row>>?>(snapshots);
        var states = new List<IValidationState>();
        var stream = ValidationCollection.Observe(sources, static row => row.Key, static row => row.CreateLease(),
            static values => new ValidationState(values.Count > 0 && values.All(static state => state.IsValid), "rows"),
            new ValidationState(false, "loading"));
        using var subscription = stream.Subscribe(new Observer<IValidationState>(states.Add));
        Check(!states[^1].IsValid, "The initial empty collection follows the explicit aggregate policy.");
        snapshots.Set([first]);
        first.States.Set(ValidationState.Valid);
        snapshots.Set([second, first]);
        snapshots.Set([first, second]);
        Check(first.Created == 1 && second.Created == 1 && states[^1].IsValid, "Stable key and reference rows retain their owned helpers through reordering.");
        snapshots.Set([replacement, second]);
        Check(first.Cleaned == 1 && first.States.Subscribers == 0 && replacement.Created == 1, "Same-key replacement ends the old row lifetime.");
        var count = states.Count;
        first.States.Set(new ValidationState(false, "stale"));
        Check(states.Count == count, "An old row cannot update the current collection.");
        sources.Set(other);
        Check(snapshots.Subscribers == 0 && replacement.Cleaned == 1 && second.Cleaned == 1 && second.Created == 2,
            "Source replacement releases old snapshots and recreates only the new source's leases.");
        sources.Set(null);
        Check(other.Subscribers == 0 && second.Cleaned == 2 && !states[^1].IsValid, "A missing source releases ownership and restores the supplied pending policy.");
        subscription.Dispose();
        Check(sources.Subscribers == 0, "Collection disposal ends outer subscription ownership.");
        first.States.Set(ValidationState.Valid);
        Check(first.States.Value.IsValid, "Borrowed row sources remain usable after owned leases end.");
    }

    internal static void RemovingPendingRowCancelsRequest()
    {
        var row = new Row(1, false);
        var snapshots = new Current<IReadOnlyCollection<Row>>([row]);
        var sources = new Current<IObservable<IReadOnlyCollection<Row>>?>(snapshots);
        var scheduler = new CapabilityScheduler();
        var request = new TaskCompletionSource<IValidationState>();
        CancellationToken cancellation = default;
        var states = new List<IValidationState>();
        var stream = ValidationCollection.Observe(sources, static value => value.Key,
            value => new ValidationRowLease(ValidationAsync.ForLatest(value.States, (_, token) =>
            {
                cancellation = token;
                return request.Task;
            }, new ValidationState(false, "checking"), scheduler)),
            static values => new ValidationState(values.All(static state => state.IsValid), "rows"), ValidationState.Valid);
        using var subscription = stream.Subscribe(new Observer<IValidationState>(states.Add));
        snapshots.Set([]);
        Check(cancellation.IsCancellationRequested && row.States.Subscribers == 0, "Removing a pending row cancels its request and detaches input.");
        var count = states.Count;
        request.SetResult(new ValidationState(false, "late"));
        scheduler.Drain();
        Check(states.Count == count, "A removed asynchronous row cannot rejoin the aggregate.");
    }

    internal static void DomainAndPresentationRemainIndependent()
    {
        using var context = new ValidationContext();
        var source = new Current<IValidationState>(ValidationState.Valid);
        using var rule = context.AddObservableRule(source, ["Name"]);
        var scheduler = new CapabilityScheduler();
        var shown = new List<bool>();
        using var presentation = ValidationScheduling.PresentOn(context.Valid, scheduler).Subscribe(new Observer<bool>(shown.Add));
        scheduler.Drain();
        source.Set(new ValidationState(false, "required"));
        Check(!context.GetIsValid() && shown[^1], "Domain admission is synchronous while presentation waits for its owner.");
        presentation.Dispose();
        scheduler.Drain();
        Check(shown[^1], "Disposal drops queued presentation without altering domain state.");

        var values = new Current<int>(0);
        var trace = new List<string>();
        using var serial = ValidationScheduling.SerializeInputs(values, scheduler).Subscribe(new Observer<int>(value =>
        {
            trace.Add($"begin{value}");
            if (value == 1) values.Set(2);
            trace.Add($"end{value}");
        }));
        scheduler.Drain();
        trace.Clear();
        values.Set(1);
        Check(trace.Count == 0, "Input delivery is explicitly queued.");
        scheduler.Drain();
        Check(trace.SequenceEqual(["begin1", "end1", "begin2", "end2"]), "Reentrant inputs are serialized without overlapping callbacks.");
    }

    internal static void NullableOutputAndStackSnapshots()
    {
        using var context = new ValidationContext();
        var states = new Current<IValidationState>(ValidationState.Valid);
        using var helper = context.AddObservableRule(states, ["Name"]);
        var sources = new Current<ValidationHelper?>(helper);
        var text = new List<string?>();
        var number = new List<int?>();
        using var output = ValidationOutput<string?>.FromStates(sources, static value => value.ValidationChanged,
            static state => state.IsValid ? null : state.Text!.ToSingleLine(), text.Add);
        var contexts = new Current<IValidationContext?>(context);
        using var property = ValidationOutput<int?>.FromPropertyStates(contexts, static value => value, "Name",
            static values => values.All(static state => state.IsValid) ? null : 1, number.Add);
        Check(text[^1] is null && number[^1] is null, "Output-only factories infer sources and preserve nullable reference/value outputs.");
        states.Set(new ValidationState(false, "required"));
        Check(text[^1] == "required" && number[^1] == 1, "Typed property and full-state factories preserve current validation behavior.");
        var span = "runic".AsSpan();
        var length = ValidationSnapshot.Read(in span, static (in ReadOnlySpan<char> value) => value.Length);
        var validation = ValidationSnapshot.Validate(in span, static (in ReadOnlySpan<char> value) => new ValidationState(!value.IsEmpty, "required"));
        Check(length == 5 && validation.IsValid, "A stack-only borrow is read synchronously without lifetime escape.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Row(int key, bool valid)
    {
        internal int Key { get; } = key;
        internal Current<IValidationState> States { get; } = new(new ValidationState(valid, "row"));
        internal int Created { get; private set; }
        internal int Cleaned { get; private set; }
        internal ValidationRowLease CreateLease()
        {
            Created++;
            return new(States, new Cleanup(() => Cleaned++));
        }
    }
}
