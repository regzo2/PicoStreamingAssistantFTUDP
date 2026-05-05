using System.Text;

namespace System.IO.Abstractions.TestingHelpers;

public sealed class MockFileSystem : IFileSystem, IFile
{
    public IFile File => this;

    private readonly Dictionary<string, StringBuilder> _contents = [];

    public void AddFile(string path, MockFileData file)
        => _contents.Add(path, file.Contents);

    public string ReadAllText(string path)
    {
        if (!_contents.TryGetValue(path, out var text))
            throw new FileNotFoundException("Couldn't find any file on " + path);

        return text.ToString();
    }

    public bool Exists(string path)
        => _contents.ContainsKey(path);

    public void WriteAllText(string path, string? contents)
        => AddFile(path, new MockFileData(contents));

    public TextWriter CreateText(string path)
    {
        StringBuilder sb = new();
        AddFile(path, new MockFileData(sb));
        return new StringWriter(sb);
    }
}