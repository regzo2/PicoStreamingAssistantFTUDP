using System.Text;

namespace System.IO.Abstractions.TestingHelpers;

public sealed class MockFileData(StringBuilder builder)
{
    public StringBuilder Contents { get; private set; } = builder;

    public MockFileData(string? contents) : this(new StringBuilder(contents))
    {
    }
}
