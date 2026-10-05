using System.ComponentModel;

// Ordinary application models: every observed property sends INPC notifications.
internal sealed class Customer : ReactiveValidationObject
{
    public string Email { get; set => this.RaiseAndSetIfChanged(ref field, value); } = "";
    public StructValues StructInfo { get; set; }
    public PlainValues Metadata { get; } = new();
    public string Confirmation { get; set => this.RaiseAndSetIfChanged(ref field, value); } = "";
    public Address? Address { get; set => this.RaiseAndSetIfChanged(ref field, value); }
    public IValidationContext? SelectedContext { get; set => this.RaiseAndSetIfChanged(ref field, value); }
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
    private string _message = "";
    public List<string> Messages { get; } = [];
    public string Message { get => _message; set { _message = value; Messages.Add(value); } }
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
    public PlainPanel PlainPanel { get; } = new();
    public int Writes { get; private set; }
    // These ordinary setters record actual generated assignments.
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


internal sealed class Panel : ReactiveObject
{
    public string Message { get; set; } = "";
    public Presentation? Status { get; set; }
    public override bool Equals(object? other) => other is Panel;
    public override int GetHashCode() => 0;
}

internal sealed class PlainPanel
{
    public Panel Child { get; set; } = new();
}

internal sealed class PlainValues
{
    public string Value { get; set; } = "";
}

internal struct StructValues : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
    public string Value { get; set; }
}
