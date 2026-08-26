using System;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Saves;

namespace STS2Mobile.Steam;

// Diagnostic scaffolding, not part of the launcher's behaviour.
//
// SteamKit2CloudSaveStore fails VTable setup, and the device has now ruled out
// every explanation that could be checked from the outside: no member is
// missing, every runtime signature matches ours exactly, only one copy of each
// assembly is loaded, and the interface the mod compiled against is the same
// instance the game runs. These two types split what is left in half. Each
// implements the interfaces and nothing else, so:
//
//   both load          → the interfaces are fine; the fault is in the real
//                        class's own body — a field type, IDisposable, or a
//                        SteamKit2 type that cannot resolve
//   only SaveProbe     → the fault is among ICloudSaveStore's six members
//   neither loads      → the mod cannot implement these interfaces at all,
//                        which would point at the reference it compiled against
//
// Delete both once the cause is known.
internal sealed class SaveProbe : ISaveStore
{
    public string ReadFile(string path) => throw new NotImplementedException();

    public Task<string> ReadFileAsync(string path) => throw new NotImplementedException();

    public void WriteFile(string path, string content) => throw new NotImplementedException();

    public void WriteFile(string path, byte[] content) => throw new NotImplementedException();

    public Task WriteFileAsync(string path, string content) => throw new NotImplementedException();

    public Task WriteFileAsync(string path, byte[] content) => throw new NotImplementedException();

    public bool FileExists(string path) => throw new NotImplementedException();

    public bool DirectoryExists(string path) => throw new NotImplementedException();

    public void DeleteFile(string path) => throw new NotImplementedException();

    public void RenameFile(string source, string destination) => throw new NotImplementedException();

    public string[] GetFilesInDirectory(string path) => throw new NotImplementedException();

    public string[] GetDirectoriesInDirectory(string path) => throw new NotImplementedException();

    public void CreateDirectory(string path) => throw new NotImplementedException();

    public void DeleteDirectory(string path) => throw new NotImplementedException();

    public void DeleteTemporaryFiles(string path) => throw new NotImplementedException();

    public DateTimeOffset GetLastModifiedTime(string path) => throw new NotImplementedException();

    public int GetFileSize(string path) => throw new NotImplementedException();

    public void SetLastModifiedTime(string path, DateTimeOffset time) =>
        throw new NotImplementedException();

    public string GetFullPath(string filename) => throw new NotImplementedException();
}

internal sealed class CloudProbe : ICloudSaveStore, IDisposable
{
    // Mirrors the real class's shape rather than inheriting: all twenty-five
    // members declared here, plus IDisposable, because the previous CloudProbe
    // took ISaveStore's members from a base class and loaded fine while the real
    // class — which declares them all itself — did not.
    private readonly object _lock = new();

    public void Dispose() => GC.KeepAlive(_lock);

    public string ReadFile(string path) => throw new NotImplementedException();

    public Task<string> ReadFileAsync(string path) => throw new NotImplementedException();

    public void WriteFile(string path, string content) => throw new NotImplementedException();

    public void WriteFile(string path, byte[] content) => throw new NotImplementedException();

    public Task WriteFileAsync(string path, string content) => throw new NotImplementedException();

    public Task WriteFileAsync(string path, byte[] content) => throw new NotImplementedException();

    public bool FileExists(string path) => throw new NotImplementedException();

    public bool DirectoryExists(string path) => throw new NotImplementedException();

    public void DeleteFile(string path) => throw new NotImplementedException();

    public void RenameFile(string source, string destination) => throw new NotImplementedException();

    public string[] GetFilesInDirectory(string path) => throw new NotImplementedException();

    public string[] GetDirectoriesInDirectory(string path) => throw new NotImplementedException();

    public void CreateDirectory(string path) => throw new NotImplementedException();

    public void DeleteDirectory(string path) => throw new NotImplementedException();

    public void DeleteTemporaryFiles(string path) => throw new NotImplementedException();

    public DateTimeOffset GetLastModifiedTime(string path) => throw new NotImplementedException();

    public int GetFileSize(string path) => throw new NotImplementedException();

    public void SetLastModifiedTime(string path, DateTimeOffset time) =>
        throw new NotImplementedException();

    public string GetFullPath(string filename) => throw new NotImplementedException();

    public bool HasCloudFiles() => throw new NotImplementedException();

    public void ForgetFile(string path) => throw new NotImplementedException();

    public bool IsFilePersisted(string path) => throw new NotImplementedException();

    public void BeginSaveBatch() => throw new NotImplementedException();

    public void EndSaveBatch() => throw new NotImplementedException();

    public bool HasUserEnabledCloudSync() => throw new NotImplementedException();
}
