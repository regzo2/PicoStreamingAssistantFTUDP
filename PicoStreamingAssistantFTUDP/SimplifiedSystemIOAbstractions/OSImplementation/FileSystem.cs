namespace System.IO.Abstractions;
public sealed class FileSystem : IFileSystem, IFile
{
    public IFile File => this;

    public bool Exists(string path)
    {
        return System.IO.File.Exists(path);
    }

    public string ReadAllText(string path)
    {
        return System.IO.File.ReadAllText(path);
    }

    public void WriteAllText(string path, string? contents)
    {
        System.IO.File.WriteAllText(path, contents);
    }
}