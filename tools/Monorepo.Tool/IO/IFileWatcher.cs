namespace Monorepo.Tool.IO;

public interface IFileWatcher : IDisposable
{
    event Action<string>? FileChanged;
    void Start();
}
