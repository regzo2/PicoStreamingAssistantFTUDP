namespace System.IO.Abstractions;

public sealed class FileSystem : IFileSystem, IFile
{
    public IFile File => this;

    public TextWriter CreateText(string path)
        => System.IO.File.CreateText(path);

    public bool Exists(string path)
        => System.IO.File.Exists(path);

    public string ReadAllText(string path)
        => System.IO.File.ReadAllText(path);

    public void WriteAllText(string path, string? contents)
        => System.IO.File.WriteAllText(path, contents);
}