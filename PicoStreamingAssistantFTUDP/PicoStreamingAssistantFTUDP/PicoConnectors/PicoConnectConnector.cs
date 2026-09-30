using Microsoft.Extensions.Logging;

using Pico4SAFTExtTrackingModule.Diagnostics;

namespace Pico4SAFTExtTrackingModule.PicoConnectors;

/**
 * Connector class for PICO Connect
 **/
public sealed class PicoConnectConnector : IPicoConnector
{
    private readonly PicoDiagnostics diagnostics;

    public PicoConnectConnector(ILogger Logger)
    {
        diagnostics = PicoDiagnostics.ForLogger(Logger);
    }

    public bool Connect()
    {
        diagnostics.Report(LogLevel.Warning, "UnsupportedProtocol", "", () =>
            "PICO Connect face tracking cannot start with the current protocol or unreadable settings. " +
            "Open %APPDATA%\\PICO Connect\\settings.json, set lab.faceTrackingTransferProtocol to 2, save the file, " +
            "then restart PICO Connect and VRCFaceTracking. Connection retry in 5000ms. " +
            "See https://docs.vrcft.io/docs/hardware/vr/pico/pico4pe");
        diagnostics.Tick();
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

    void IPicoConnector.Teardown()
    {
        
    }
}
