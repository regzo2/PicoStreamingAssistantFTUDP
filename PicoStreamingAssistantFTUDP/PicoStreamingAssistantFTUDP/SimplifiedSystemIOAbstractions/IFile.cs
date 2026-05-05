namespace System.IO.Abstractions;

public interface IFile
{
    string ReadAllText(string path);
    bool Exists(string path);
    void WriteAllText(string path, string? contents);

    TextWriter CreateText(string path);
}
