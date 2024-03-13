namespace System.IO.Abstractions;
public sealed class FileSystem : IFileSystem, IFile
{
    public IFile File => this;

    public string ReadAllText(string path)
    {
        return System.IO.File.ReadAllText(path);
    }
}