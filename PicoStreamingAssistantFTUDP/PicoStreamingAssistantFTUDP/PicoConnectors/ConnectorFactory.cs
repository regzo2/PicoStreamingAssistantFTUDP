using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Pico4SAFTExtTrackingModule.PicoConnectors.ProgramChecker;
using Pico4SAFTExtTrackingModule.PicoConnectors.ConfigChecker;
using Pico4SAFTExtTrackingModule.Diagnostics;

namespace Pico4SAFTExtTrackingModule.PicoConnectors;

public sealed class ConnectorFactory
{
    public static IPicoConnector? build(ILogger Logger, IProgramChecker programChecker, IConfigChecker configChecker)
    {
        var diagnostics = PicoDiagnostics.ForLogger(Logger);
        bool using_sa = programChecker.Check(PicoPrograms.StreamingAssistant);
        bool using_pc = programChecker.Check(PicoPrograms.PicoConnect);
        bool using_bs = programChecker.Check(PicoPrograms.BusinessStreaming);
        // These three booleans form a bounded state signature: changes must remain visible.
        diagnostics.Report(LogLevel.Debug, "StreamingProgramProbe", $"{using_sa}/{using_bs}/{using_pc}", () =>
            $"Streaming program probe: StreamingAssistant={using_sa}; " +
            $"BusinessStreaming={using_bs}; PICOConnect={using_pc}.");

        // Streaming Assistant and Business Streaming select the legacy decoder directly;
        // their settings and the actual packet format are not validated by this factory.
        if (using_sa) return new LegacyConnector(Logger, PicoPrograms.StreamingAssistant);
        else if (using_bs) return new LegacyConnector(Logger, PicoPrograms.BusinessStreaming);
        else if (using_pc)
        {
            try {
                Logger.LogDebug("Checking PICO Connect settings.json for the transfer protocol.");
                // For PICO Connect, the setup guide requires lab.faceTrackingTransferProtocol=2
                // to select legacy transfer. We then reuse the decoder/layout in LegacyConnector
                // and Pxr.cs; checking this setting does not negotiate or verify a packet version.
                // Other values or unreadable settings select PicoConnectConnector, whose Connect()
                // returns false and logs configuration instructions instead of receiving data.
                // https://docs.vrcft.io/docs/hardware/vr/pico/pico4pe
                int protocol = configChecker.GetTransferProtocolNumber(PicoPrograms.PicoConnect);
                // All unsupported values share a signature; a supported/unsupported
                // transition must be visible without letting arbitrary config integers
                // create an unbounded stream of new log signatures.
                diagnostics.Report(LogLevel.Information, "PicoConnectProtocol", protocol == 2 ? "legacy" : "unsupported", () =>
                    $"PICO Connect faceTrackingTransferProtocol={protocol}; selected decoder=" +
                    $"{(protocol == 2 ? nameof(LegacyConnector) : "unsupported protocol/settings")}.");
                if (protocol == 2) return new LegacyConnector(Logger, PicoPrograms.PicoConnect);
            } catch (Exception ex) {
                Logger.LogWarning(ex, "Could not read PICO Connect transfer protocol; connection retry in 5000ms.");
            }
            return new PicoConnectConnector(Logger); // Unsupported or unreadable protocol setting.
        }

        return null; // none found
    }
}
