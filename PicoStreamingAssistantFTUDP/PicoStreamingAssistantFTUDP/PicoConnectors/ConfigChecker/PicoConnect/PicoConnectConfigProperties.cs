namespace Pico4SAFTExtTrackingModule.PicoConnectors.ConfigChecker.PicoConnect;

public class Config
{
    public Lab lab { get; set; }
}

public class Lab
{
    public int faceTrackingTransferProtocol { get; set; }

    // PicoConnectConfigChecker reads lab.* from %APPDATA%\PICO Connect\settings.json.
    // This DTO omits lab.faceTrackingMode: connector selection only reads the transfer
    // protocol, and the module does not validate the mode or issue a mode-specific warning.
    // The VRCFT setup guide recommends faceTrackingMode=1 (image-driven) to avoid tracking
    // interruptions during microphone input. Protocol 2 alone does not check this requirement.
    // https://docs.vrcft.io/docs/hardware/vr/pico/pico4pe
}
