using Microsoft.Extensions.Logging;

using Pico4SAFTExtTrackingModule.PicoConnectors.ConfigChecker.PicoConnect;

namespace Pico4SAFTExtTrackingModule.PicoConnectors.ConfigChecker;

public sealed class ConfigChecker(ILogger logger) : IConfigChecker
{
    public readonly PicoConnectConfigChecker picoConnectConfigChecker = new(logger);

    public int GetTransferProtocolNumber(PicoPrograms program) => program switch
    {
        PicoPrograms.PicoConnect or PicoPrograms.BusinessStreaming => picoConnectConfigChecker.GetTransferProtocolNumber(program),
        _ => throw new NotImplementedException(),
    };
}