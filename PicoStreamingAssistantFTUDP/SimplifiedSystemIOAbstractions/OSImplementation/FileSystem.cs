using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.IO.Abstractions;
public sealed class FileSystem : IFileSystem, IFile
{
    public IFile File => this;

    public string ReadAllText(string path)
    {
        return File.ReadAllText(path);
    }
}