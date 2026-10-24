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
        // Every write command starts here. Without this check, a file in place of the directory surfaces as the
        // runtime's own message, which names the absolute path and no remedy.
        if (File.Exists(historyDirectory))
            throw new IOException("_repolore/.history is a file, not a directory, so nothing can be written; move that file aside or delete it, then retry");
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
