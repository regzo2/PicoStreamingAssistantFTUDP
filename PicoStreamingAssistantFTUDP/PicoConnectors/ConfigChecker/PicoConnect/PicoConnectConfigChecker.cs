using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pico4SAFTExtTrackingModule.PicoConnectors.ConfigChecker.PicoConnect;

public sealed class PicoConnectConfigChecker : IConfigChecker
{
    readonly IFileSystem fileSystem;

    public PicoConnectConfigChecker(IFileSystem fileSystem)
    {
        this.fileSystem = fileSystem;
    }

    public PicoConnectConfigChecker() : this(new FileSystem()) { }

    public int GetTransferProtocolNumber(PicoPrograms program)
    {
        if (program != PicoPrograms.PicoConnect) throw new ArgumentException("PicoConnectConfigChecker class only checks for PICO Connect config files");

        // TODO
        return 0;
    }
}