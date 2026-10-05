using System.ComponentModel;

// Ordinary application models: every observed property sends INPC notifications.
internal sealed class Customer : ReactiveValidationObject
{
    public string Email { get; set => this.RaiseAndSetIfChanged(ref field, value); } = "";
    public string Confirmation { get; set => this.RaiseAndSetIfChanged(ref field, value); } = "";
    public Address? Address { get; set => this.RaiseAndSetIfChanged(ref field, value); }
    public ValidationHelper? AddressRule { get; set => this.RaiseAndSetIfChanged(ref field, value); }
}

internal sealed class Address : ReactiveObject
{
    public string Postcode { get; set => this.RaiseAndSetIfChanged(ref field, value); } = "";
}

internal enum Severity { None, Advisory, Blocking }
internal readonly record struct Presentation(Severity Severity, string Code, int Revision);

internal readonly record struct UniquenessState(bool IsValid, string Code, int Revision) : IValidationState
{
    public IValidationText Text => IsValid ? ValidationText.None : ValidationText.Create(Code);
}

internal sealed class Editor : ReactiveObject, IViewFor<Customer>
{
    private Presentation? _presentation;
    public Customer? ViewModel { get; set => this.RaiseAndSetIfChanged(ref field, value); }
    object? IViewFor.ViewModel { get => ViewModel; set => ViewModel = (Customer?)value; }
    public int Writes { get; private set; }
    // The baseline setter has no static writes or linker roots; this is a normal
    // property reached through a validation binding, rather than a native mock.
    public Presentation? Status { get => _presentation; set { _presentation = value; Writes++; } }
}

internal sealed class Current<T>(T initial) : IObservable<T>
{
    private readonly List<Registration> _observers = [];
    public T Value { get; private set; } = initial;
    public int Subscribers => _observers.Count;
    public IDisposable Subscribe(IObserver<T> observer)
    {
        var registration = new Registration(observer);
        var cleanup = new Cleanup(() => { registration.Active = false; _observers.Remove(registration); });
        _observers.Add(registration);
        try
        {
            observer.OnNext(Value);
            return cleanup;
        }
        catch
        {
            cleanup.Dispose();
            throw;
        }
    }
    public void Set(T value)
    {
        Value = value;
        foreach (var registration in _observers.ToArray())
        {
            if (registration.Active) registration.Observer.OnNext(value);
        }
    }

    private sealed class Registration(IObserver<T> observer)
    {
        public IObserver<T> Observer { get; } = observer;
        public bool Active { get; set; } = true;
    }
}

// A portable application-side observable boundary. Generated properties or a
// Binding interceptor can provide the same stream; no library reflection occurs.
internal sealed class PropertyValues<T>(INotifyPropertyChanged owner, string name, Func<T> read) : IObservable<T>
{
    public IDisposable Subscribe(IObserver<T> observer)
    {
        bool active = true;
        PropertyChangedEventHandler handler = (_, args) =>
        {
            if (active && (args.PropertyName == name || string.IsNullOrEmpty(args.PropertyName))) observer.OnNext(read());
        };
        var cleanup = new Cleanup(() => { active = false; owner.PropertyChanged -= handler; });
        owner.PropertyChanged += handler;
        try
        {
            observer.OnNext(read());
            return cleanup;
        }
        catch
        {
            cleanup.Dispose();
            throw;
        }
    }
}

internal sealed class Observer<T>(Action<T> next) : IObserver<T>
{
    public void OnNext(T value) => next(value);
    public void OnError(Exception error) => throw error;
    public void OnCompleted() { }
}

internal sealed class Cleanup(Action cleanup) : IDisposable
{
    private Action? _cleanup = cleanup;
    public void Dispose() => Interlocked.Exchange(ref _cleanup, null)?.Invoke();
}

#if SAFE_API
// Install this ownership slot before Subscribe: its initial notification can
// synchronously replace/dispose the owner before the produced token returns.
// All adapter notifications and mutations run on the serialized model owner.
internal sealed class PendingSubscription : IDisposable
{
    private IDisposable? _subscription;
    public bool IsDisposed { get; private set; }
    public void Assign(IDisposable subscription)
    {
        if (IsDisposed) subscription.Dispose();
        else _subscription = subscription;
    }
    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        var subscription = _subscription;
        _subscription = null;
        subscription?.Dispose();
    }
}

// Application-owned nested observation. Missing Address emits null; replacement
// detaches the previous INPC subscription before subscribing to the new address.
internal sealed class NestedPostcodes(Customer customer) : IObservable<string?>
{
    public IDisposable Subscribe(IObserver<string?> observer)
    {
        var outer = new PendingSubscription();
        PendingSubscription? inner = null;
        var cleanup = new Cleanup(() => { outer.Dispose(); inner?.Dispose(); });
        void Replace(Address? address)
        {
            if (outer.IsDisposed) return;
            var pending = new PendingSubscription();
            var previous = inner;
            inner = pending;
            previous?.Dispose();
            if (pending.IsDisposed || outer.IsDisposed) return;
            try
            {
                if (address is null) observer.OnNext(null);
                else pending.Assign(new PropertyValues<string?>(address, nameof(Address.Postcode), () => address.Postcode).Subscribe(new Observer<string?>(value =>
                {
                    if (!pending.IsDisposed && !outer.IsDisposed && ReferenceEquals(inner, pending)) observer.OnNext(value);
                })));
            }
            catch
            {
                cleanup.Dispose();
                throw;
            }
        }
        try
        {
            outer.Assign(new PropertyValues<Address?>(customer, nameof(Customer.Address), () => customer.Address).Subscribe(new Observer<Address?>(Replace)));
            return cleanup;
        }
        catch
        {
            cleanup.Dispose();
            throw;
        }
    }
}

// The outer stream reports both editor model and helper replacement; selector
// delegates themselves deliberately perform no hidden observation.
internal sealed class HelperSelections(Editor editor) : IObservable<ValidationHelper?>
{
    public IDisposable Subscribe(IObserver<ValidationHelper?> observer)
    {
        var outer = new PendingSubscription();
        PendingSubscription? inner = null;
        var cleanup = new Cleanup(() => { outer.Dispose(); inner?.Dispose(); });
        void Replace(Customer? customer)
        {
            if (outer.IsDisposed) return;
            var pending = new PendingSubscription();
            var previous = inner;
            inner = pending;
            previous?.Dispose();
            if (pending.IsDisposed || outer.IsDisposed) return;
            try
            {
                if (customer is null) observer.OnNext(null);
                else pending.Assign(new PropertyValues<ValidationHelper?>(customer, nameof(Customer.AddressRule), () => customer.AddressRule).Subscribe(new Observer<ValidationHelper?>(value =>
                {
                    if (!pending.IsDisposed && !outer.IsDisposed && ReferenceEquals(inner, pending)) observer.OnNext(value);
                })));
            }
            catch
            {
                cleanup.Dispose();
                throw;
            }
        }
        try
        {
            outer.Assign(new PropertyValues<Customer?>(editor, nameof(Editor.ViewModel), () => editor.ViewModel).Subscribe(new Observer<Customer?>(Replace)));
            return cleanup;
        }
        catch
        {
            cleanup.Dispose();
            throw;
        }
    }
}
#endif
