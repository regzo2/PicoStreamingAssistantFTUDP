using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Pico4SAFTExtTrackingModule.PicoConnectors.ProgramChecker;
using Pico4SAFTExtTrackingModule.PicoConnectors.ConfigChecker;

namespace Pico4SAFTExtTrackingModule.PicoConnectors;

public sealed class ConnectorFactory
{
    public static IPicoConnector? build(ILogger Logger, IProgramChecker programChecker, IConfigChecker configChecker)
    {
        bool using_sa = programChecker.Check(PicoPrograms.StreamingAssistant);
        bool using_pc = programChecker.Check(PicoPrograms.PicoConnect);
        bool using_bs = programChecker.Check(PicoPrograms.BusinessStreaming);

        if (using_sa) return new LegacyConnector(Logger, PicoPrograms.StreamingAssistant);
        else if (using_bs) return new LegacyConnector(Logger, PicoPrograms.BusinessStreaming);
        else if (using_pc)
        {
            try {
                Logger.LogInformation("Got PICO Connect; checking settings.json to choose what protocol to use...");
                if (configChecker.GetTransferProtocolNumber(PicoPrograms.PicoConnect) == 2) return new LegacyConnector(Logger, PicoPrograms.PicoConnect); // using legacy protocol
            } catch (Exception ex) {
                Logger.LogWarning("Exception while trying to get the config protocol number: " + ex.ToString);
            }
            return new PicoConnectConnector(Logger); // couldn't get / using latest protocol
        }

        return null; // none found
    }
}
