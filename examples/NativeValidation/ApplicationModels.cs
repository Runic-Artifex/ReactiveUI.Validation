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
    private readonly List<IObserver<T>> _observers = [];
    public T Value { get; private set; } = initial;
    public int Subscribers => _observers.Count;
    public IDisposable Subscribe(IObserver<T> observer)
    {
        _observers.Add(observer);
        observer.OnNext(Value);
        return new Cleanup(() => _observers.Remove(observer));
    }
    public void Set(T value)
    {
        Value = value;
        foreach (var observer in _observers.ToArray()) observer.OnNext(value);
    }
}

// A portable application-side observable boundary. Generated properties or a
// Binding interceptor can provide the same stream; no library reflection occurs.
internal sealed class PropertyValues<T>(INotifyPropertyChanged owner, string name, Func<T> read) : IObservable<T>
{
    public IDisposable Subscribe(IObserver<T> observer)
    {
        PropertyChangedEventHandler handler = (_, args) =>
        {
            if (args.PropertyName == name || string.IsNullOrEmpty(args.PropertyName)) observer.OnNext(read());
        };
        owner.PropertyChanged += handler;
        observer.OnNext(read());
        return new Cleanup(() => owner.PropertyChanged -= handler);
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
// Application-owned nested observation. Missing Address emits null; replacement
// detaches the previous INPC subscription before subscribing to the new address.
internal sealed class NestedPostcodes(Customer customer) : IObservable<string?>
{
    public IDisposable Subscribe(IObserver<string?> observer)
    {
        IDisposable? inner = null;
        void Replace(Address? address)
        {
            inner?.Dispose();
            inner = null;
            if (address is null) observer.OnNext(null);
            else inner = new PropertyValues<string?>(address, nameof(Address.Postcode), () => address.Postcode).Subscribe(observer);
        }
        var outer = new PropertyValues<Address?>(customer, nameof(Customer.Address), () => customer.Address).Subscribe(new Observer<Address?>(Replace));
        return new Cleanup(() => { outer.Dispose(); inner?.Dispose(); });
    }
}

// The outer stream reports both editor model and helper replacement; selector
// delegates themselves deliberately perform no hidden observation.
internal sealed class HelperSelections(Editor editor) : IObservable<ValidationHelper?>
{
    public IDisposable Subscribe(IObserver<ValidationHelper?> observer)
    {
        IDisposable? inner = null;
        void Replace(Customer? customer)
        {
            inner?.Dispose();
            inner = null;
            if (customer is null) observer.OnNext(null);
            else inner = new PropertyValues<ValidationHelper?>(customer, nameof(Customer.AddressRule), () => customer.AddressRule).Subscribe(observer);
        }
        var outer = new PropertyValues<Customer?>(editor, nameof(Editor.ViewModel), () => editor.ViewModel).Subscribe(new Observer<Customer?>(Replace));
        return new Cleanup(() => { outer.Dispose(); inner?.Dispose(); });
    }
}
#endif
