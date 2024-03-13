namespace System.IO.Abstractions;

public interface IFileSystem
{
    public IFile File { get; }
}