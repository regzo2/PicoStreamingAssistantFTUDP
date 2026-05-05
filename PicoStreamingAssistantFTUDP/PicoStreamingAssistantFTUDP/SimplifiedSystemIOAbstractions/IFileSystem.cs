namespace System.IO.Abstractions;

public interface IFileSystem
{
    IFile File { get; }
}