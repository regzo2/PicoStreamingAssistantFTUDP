using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pico4SAFTExtTrackingModule.PicoConnectors.ConfigChecker.PicoConnect;

namespace Pico4SAFTExtTrackingModule.PicoConnectors.ConfigChecker;

public sealed class ConfigChecker : IConfigChecker
{
    public readonly PicoConnectConfigChecker picoConnectConfigChecker;

    public ConfigChecker(ILogger logger)
    {
        this.picoConnectConfigChecker = new PicoConnectConfigChecker(logger);
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