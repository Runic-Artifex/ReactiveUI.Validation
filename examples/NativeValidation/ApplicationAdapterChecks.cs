#if SAFE_API
using System.ComponentModel;

// Assertions for these application-owned adapters, not a broader library
// scheduling/concurrency contract. Initial values are delivered synchronously.
internal static class ApplicationAdapterChecks
{
    public static void Run()
    {
        InitialFailureCleanup();
        NestedInitialReplacement();
        HelperInitialReplacement();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            var error = new InvalidOperationException(message);
            error.Data["failureId"] = "application-adapter-lifecycle";
            throw error;
        }
    }

    private static void ExpectFailure(Func<IDisposable> subscribe, Exception expected)
    {
        try
        {
            using var unexpected = subscribe();
        }
        catch (Exception actual)
        {
            Check(ReferenceEquals(actual, expected), "initial subscription must propagate the original exception");
            return;
        }
        Check(false, "expected initial subscription failure");
    }

    private static void InitialFailureCleanup()
    {
        var owner = new InstrumentedOwner();
        var expected = new InvalidOperationException("initial delivery failed");
        var properties = new PropertyValues<int>(owner, nameof(InstrumentedOwner.Value), () => owner.Value);
        int callbacks = 0;
        ExpectFailure(() => properties.Subscribe(new Observer<int>(_ => { callbacks++; throw expected; })), expected);
        Check(owner.Handlers == 0 && callbacks == 1, "initial property callback failure removes handler");
        owner.Update(1);
        Check(callbacks == 1, "failed property callback remains detached");
        var failedGetter = new PropertyValues<int>(owner, nameof(InstrumentedOwner.Value), () => throw expected);
        ExpectFailure(() => failedGetter.Subscribe(new Observer<int>(_ => callbacks++)), expected);
        Check(owner.Handlers == 0 && callbacks == 1, "initial getter failure removes handler without delivering a value");
        var current = new Current<int>(0);
        ExpectFailure(() => current.Subscribe(new Observer<int>(_ => { callbacks++; throw expected; })), expected);
        Check(current.Subscribers == 0, "initial current-value callback failure removes registration");
        current.Set(1);
        Check(callbacks == 2, "failed current-value callback remains detached");
        using var pendingOwner = new PendingSubscription();
        pendingOwner.Assign(properties.Subscribe(new Observer<int>(_ => pendingOwner.Dispose())));
        Check(owner.Handlers == 0, "owner disposed during initial notification rejects the returned token");
        pendingOwner.Dispose();
    }

    private static void NestedInitialReplacement()
    {
        using var customer = new Customer();
        var first = new Address { Postcode = "first" };
        var second = new Address { Postcode = "second" };
        customer.Address = first;
        var values = new List<string?>();
        using var subscription = new NestedPostcodes(customer).Subscribe(new Observer<string?>(value =>
        {
            values.Add(value);
            if (value == "first") customer.Address = second;
        }));
        Check(values.SequenceEqual(["first", "second"]), "initial address callback reentrantly selects replacement");
        first.Postcode = "stale";
        Check(values.Count == 2, "pending first-address token cannot overwrite replacement ownership");
        subscription.Dispose();
        subscription.Dispose();
        second.Postcode = "after-disposal";
        customer.Address = first;
        Check(values.Count == 2, "address adapter disposal detaches latest inner and outer handlers");

        first.Postcode = "null-parent";
        using var nullSubscription = new NestedPostcodes(customer).Subscribe(new Observer<string?>(value =>
        {
            values.Add(value);
            if (value == "null-parent") customer.Address = null;
        }));
        Check(values[^1] is null, "initial address callback can replace parent with null");
        int count = values.Count;
        first.Postcode = "stale-null-parent";
        Check(values.Count == count, "null replacement rejects pending first-address token");
        nullSubscription.Dispose();

        customer.Address = first;
        using var pendingOwner = new PendingSubscription();
        pendingOwner.Assign(new NestedPostcodes(customer).Subscribe(new Observer<string?>(_ => { values.Add("owner-disposed"); pendingOwner.Dispose(); })));
        count = values.Count;
        first.Postcode = "after-owner-disposal";
        customer.Address = second;
        Check(values.Count == count, "initial owner disposal detaches address adapter when Subscribe returns");

        var expected = new InvalidOperationException("nested initial callback");
        int failedCallbacks = 0;
        ExpectFailure(() => new NestedPostcodes(customer).Subscribe(new Observer<string?>(_ => { failedCallbacks++; throw expected; })), expected);
        second.Postcode = "after-failed-subscribe";
        customer.Address = first;
        Check(failedCallbacks == 1, "failed nested initial callback removes both subscriptions");
    }

    private static void HelperInitialReplacement()
    {
        using var firstContext = new ValidationContext();
        using var secondContext = new ValidationContext();
        using var firstHelper = new ValidationHelper(firstContext);
        using var secondHelper = new ValidationHelper(secondContext);
        using var first = new Customer { AddressRule = firstHelper };
        using var second = new Customer { AddressRule = secondHelper };
        var editor = new Editor { ViewModel = first };
        var values = new List<ValidationHelper?>();
        using var subscription = new HelperSelections(editor).Subscribe(new Observer<ValidationHelper?>(value =>
        {
            values.Add(value);
            if (ReferenceEquals(value, firstHelper)) editor.ViewModel = second;
        }));
        Check(values.Count == 2 && ReferenceEquals(values[0], firstHelper) && ReferenceEquals(values[1], secondHelper), "initial helper callback reentrantly selects replacement model");
        first.AddressRule = null;
        Check(values.Count == 2, "pending old-model token cannot overwrite replacement ownership");
        subscription.Dispose();
        subscription.Dispose();
        second.AddressRule = null;
        editor.ViewModel = first;
        Check(values.Count == 2, "helper adapter disposal detaches latest inner and outer handlers");

        first.AddressRule = firstHelper;
        using var nullSubscription = new HelperSelections(editor).Subscribe(new Observer<ValidationHelper?>(value =>
        {
            values.Add(value);
            if (ReferenceEquals(value, firstHelper)) editor.ViewModel = null;
        }));
        Check(values[^1] is null, "initial helper callback can replace model with null");
        int count = values.Count;
        first.AddressRule = secondHelper;
        Check(values.Count == count, "null model replacement rejects pending first-model token");
        nullSubscription.Dispose();

        editor.ViewModel = first;
        using var pendingOwner = new PendingSubscription();
        pendingOwner.Assign(new HelperSelections(editor).Subscribe(new Observer<ValidationHelper?>(_ => { values.Add(null); pendingOwner.Dispose(); })));
        count = values.Count;
        first.AddressRule = null;
        editor.ViewModel = second;
        Check(values.Count == count, "initial owner disposal detaches helper adapter when Subscribe returns");

        var expected = new InvalidOperationException("helper initial callback");
        int failedCallbacks = 0;
        ExpectFailure(() => new HelperSelections(editor).Subscribe(new Observer<ValidationHelper?>(_ => { failedCallbacks++; throw expected; })), expected);
        second.AddressRule = secondHelper;
        editor.ViewModel = first;
        Check(failedCallbacks == 1, "failed helper initial callback removes both subscriptions");
    }

    private sealed class InstrumentedOwner : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed;
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; Handlers++; }
            remove { _changed -= value; Handlers--; }
        }
        public int Handlers { get; private set; }
        public int Value { get; private set; }
        public void Update(int value)
        {
            Value = value;
            _changed?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
        }
    }
}
#endif
