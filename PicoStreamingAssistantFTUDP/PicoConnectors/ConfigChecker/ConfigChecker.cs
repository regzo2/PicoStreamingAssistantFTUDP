using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pico4SAFTExtTrackingModule.PicoConnectors.ConfigChecker.PicoConnect;

namespace Pico4SAFTExtTrackingModule.PicoConnectors.ConfigChecker;

public sealed class ConfigChecker : IConfigChecker
{
    public readonly PicoConnectConfigChecker picoConnectConfigChecker;

    public ConfigChecker()
    {
        this.picoConnectConfigChecker = new PicoConnectConfigChecker();
    }

    public int GetTransferProtocolNumber(PicoPrograms program)
    {
        switch (program)
        {
            case PicoPrograms.PicoConnect:
                return this.picoConnectConfigChecker.GetTransferProtocolNumber(program);

            default:
                throw new NotImplementedException();
        }
    }
}