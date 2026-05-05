using Microsoft.Extensions.Logging;

namespace Pico4SAFTExtTrackingModule.PicoConnectors;

/// <summary>
/// Connector class for PICO Connect
/// </summary>
/// <param name="logger"></param>
public sealed partial class PicoConnectConnector(ILogger logger) : IPicoConnector
{
    public bool Connect()
    {
        LogTodo();
        return false;
    }

    public ReadOnlySpan<float> GetBlendShapes()
    {
        return [];
    }

    public string GetProcessName()
    {
        return "PICO Connect";
    }

    void IPicoConnector.Teardown()
    {
    }


    [LoggerMessage(LogLevel.Information, """
        PICO Connect module is still under development.
        You may want to set `mergetype=2` in the PICO Connect config to use the old protocol. For more information check https://docs.vrcft.io/docs/hardware/pico4pe#pico-connect-beta-setup
        """)]
    private partial void LogTodo();
}
