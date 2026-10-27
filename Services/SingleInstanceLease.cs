namespace NetAdapterManager.Services;

internal sealed class SingleInstanceLease : IDisposable
{
    private readonly Mutex _mutex;
    internal bool Acquired { get; }

    internal SingleInstanceLease(string name)
    {
        _mutex = new Mutex(true, name, out var created);
        Acquired = created;
    }

    public void Dispose()
    {
        if (Acquired) _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
