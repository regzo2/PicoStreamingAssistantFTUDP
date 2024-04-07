namespace System.IO.Abstractions;

public interface IFile
{
    public string ReadAllText(string path);
    public bool Exists(string path);
    public void WriteAllText(string path, string? contents);
}
