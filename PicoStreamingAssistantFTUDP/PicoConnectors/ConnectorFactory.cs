using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Pico4SAFTExtTrackingModule.PicoConnectors;

public sealed class ConnectorFactory
{
    public static PicoConnector? build(ILogger Logger)
    {
        bool using_sa = Process.GetProcessesByName("Streaming Assistant").Length is 0;
        bool using_pc = Process.GetProcessesByName("PICO Connect").Length is 0;
        bool using_bs = Process.GetProcessesByName("Business StreamingUW").Length is 0;

        if (using_sa || using_bs) return new StreamingAssistantConnector(Logger, using_sa);
        else if (using_pc) return new PicoConnectConnector(Logger);

        return null; // none found
    }
}
