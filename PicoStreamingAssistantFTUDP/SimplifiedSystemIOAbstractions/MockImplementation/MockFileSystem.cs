namespace System.IO.Abstractions.TestingHelpers;

public sealed class MockFileSystem : IFileSystem, IFile
{
    public IFile File => this;

    private readonly Dictionary<string, string> _contents;

    public MockFileSystem()
    {
        this._contents = new Dictionary<string, string>();
    }

    public void AddFile(string path, MockFileData file)
    {
        this._contents.Add(path, file.Contents);
    }

    public string ReadAllText(string path)
    {
        string text;
        bool found = this._contents.TryGetValue(path, out text);
        if (!found) throw new FileNotFoundException("Couldn't find any file on " + path);

        return text;
    }
}