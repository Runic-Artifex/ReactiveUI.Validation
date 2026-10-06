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

internal sealed class Cleanup(Action cleanup) : IDisposable
{
    private Action? _cleanup = cleanup;
    public void Dispose() => Interlocked.Exchange(ref _cleanup, null)?.Invoke();
}
