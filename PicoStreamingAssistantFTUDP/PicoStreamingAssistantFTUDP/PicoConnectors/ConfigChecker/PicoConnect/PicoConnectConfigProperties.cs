using System.Text.Json.Serialization;

namespace Pico4SAFTExtTrackingModule.PicoConnectors.ConfigChecker.PicoConnect;

public sealed class Config
{
    [JsonPropertyName("lab")]
    public Lab? Lab { get; set; }
}

public sealed class Lab
{
    [JsonPropertyName("faceTrackingTransferProtocol")]
    public int FaceTrackingTransferProtocol { get; set; }
}