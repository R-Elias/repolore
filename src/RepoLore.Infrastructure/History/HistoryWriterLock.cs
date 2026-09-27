namespace RepoLore.Infrastructure.History;

public sealed class WriterLockException : Exception
{
    public WriterLockException(string message, Exception inner) : base(message, inner) { }
}

public sealed class WriterLock : IDisposable
{
    private readonly FileStream _stream;

    private WriterLock(FileStream stream) { _stream = stream; }

    public static WriterLock Acquire(string historyDirectory)
    {
        Directory.CreateDirectory(historyDirectory);
        var path = Path.Combine(historyDirectory, "write.lock");
        try
        {
            var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return new WriterLock(stream);
        }
        catch (IOException ex)
        {
            throw new WriterLockException("another RepoLore writer is active; retry later", ex);
        }
    }

    public void Dispose() => _stream.Dispose();
}
