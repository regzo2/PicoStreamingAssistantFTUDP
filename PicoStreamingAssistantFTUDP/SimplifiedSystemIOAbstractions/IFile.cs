using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.IO.Abstractions;

public interface IFile
{
    public string ReadAllText(string path);
}
