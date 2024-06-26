namespace System.IO.Abstractions.TestingHelpers;

public sealed class MockFileData
{
    public string? Contents { get; private set; }

    public MockFileData(string? contents) {
        this.Contents = contents;
    }
}
