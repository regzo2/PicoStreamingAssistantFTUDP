using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.IO.Abstractions.TestingHelpers;

public sealed class MockFileData
{
    public string Contents { get; private set; }

    public MockFileData(string contents) {
        this.Contents = contents;
    }
}
