using Microsoft.Extensions.Logging;

using Pico4SAFTExtTrackingModule.PicoConnectors.ConfigChecker;
using Pico4SAFTExtTrackingModule.PicoConnectors.ProgramChecker;

namespace Pico4SAFTExtTrackingModule.PicoConnectors;

public static partial class ConnectorFactory
{
    public static IPicoConnector? Build(ILogger logger, IProgramChecker programChecker, IConfigChecker configChecker)
    {
        if (programChecker.Check(PicoPrograms.PicoConnect))
        {
            logger.LogPicoConnect();
            try
            {
                return configChecker.GetTransferProtocolNumber(PicoPrograms.PicoConnect) switch
                {
                    2 => new LegacyConnector(logger, PicoPrograms.PicoConnect), // using legacy protocol
                    _ => new PicoConnectConnector(logger), // couldn't get / using latest protocol
                };
            }
            catch (Exception ex)
            {
                logger.LogException(ex);
            }
        }

        if (programChecker.Check(PicoPrograms.BusinessStreaming))
        {
            logger.LogBusinessStreaming();
            try
            {
                return configChecker.GetTransferProtocolNumber(PicoPrograms.BusinessStreaming) switch
                {
                    2 => new LegacyConnector(logger, PicoPrograms.BusinessStreaming), // using legacy protocol

                    // TODO is the protocol the same as PicoConnect? can we use the same connector (once it's implemented)?
                    _ => new PicoConnectConnector(logger),// couldn't get / using latest protocol
                };
            }
            catch (Exception ex)
            {
                logger.LogException(ex);
            }
        }

        if (programChecker.Check(PicoPrograms.BusinessStreamingV1))
            return new LegacyConnector(logger, PicoPrograms.BusinessStreamingV1);

        if (programChecker.Check(PicoPrograms.StreamingAssistant))
            return new LegacyConnector(logger, PicoPrograms.StreamingAssistant);

        return null; // none found
    }

    [LoggerMessage(LogLevel.Information, "Got new Business Streaming; checking settings.json to choose what protocol to use...")]
    private static partial void LogBusinessStreaming(this ILogger logger);
    [LoggerMessage(LogLevel.Information, "Got PICO Connect; checking settings.json to choose what protocol to use...")]
    private static partial void LogPicoConnect(this ILogger logger);
    [LoggerMessage(LogLevel.Warning, "Exception while trying to get the config protocol number.")]
    private static partial void LogException(this ILogger logger, Exception exception);
}
