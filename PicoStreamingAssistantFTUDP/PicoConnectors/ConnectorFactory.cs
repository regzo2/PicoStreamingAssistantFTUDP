using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Pico4SAFTExtTrackingModule.PicoConnectors.ProgramChecker;

namespace Pico4SAFTExtTrackingModule.PicoConnectors;

public sealed class ConnectorFactory
{
    public static PicoConnector? build(ILogger Logger, IProgramChecker programChecker)
    {
        bool using_sa = programChecker.Check(PicoPrograms.StreamingAssistant);
        bool using_pc = programChecker.Check(PicoPrograms.PicoConnect);
        bool using_bs = programChecker.Check(PicoPrograms.BusinessStreaming);

        if (using_sa) return new LegacyConnector(Logger, PicoPrograms.StreamingAssistant);
        else if (using_bs) return new LegacyConnector(Logger, PicoPrograms.BusinessStreaming);
        else if (using_pc)
        {
            // TODO check mergetype
            return new PicoConnectConnector(Logger);
        }

        return null; // none found
    }
}
