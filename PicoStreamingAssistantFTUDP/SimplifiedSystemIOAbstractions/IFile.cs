namespace System.IO.Abstractions;

public interface IFile
{
    public string ReadAllText(string path);
}
