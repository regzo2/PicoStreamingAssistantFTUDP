using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Pico4SAFTExtTrackingModule.PicoConnectors;

/**
 * Connector class for PICO Connect
 **/
public sealed class PicoConnectConnector : PicoConnector
{
    private ILogger Logger;

    public PicoConnectConnector(ILogger Logger)
    {
        this.Logger = Logger;
    }

    public bool Connect()
    {
        Logger.LogInformation("PICO Connect module is still under development.");
        Logger.LogInformation("You may want to set `mergetype=2` in the PICO Connect config to use the old protocol. For more information check https://docs.vrcft.io/docs/hardware/pico4pe#pico-connect-beta-setup");
        return false;
    }

    public unsafe float* GetBlendShapes()
    {
        return null;
    }

    public string GetProcessName()
    {
        return "PICO Connect";
    }

    void PicoConnector.Teardown()
    {
        
    }
}
