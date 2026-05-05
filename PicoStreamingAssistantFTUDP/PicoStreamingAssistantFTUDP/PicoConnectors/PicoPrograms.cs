namespace Pico4SAFTExtTrackingModule.PicoConnectors;

public enum PicoPrograms
{
    /// <summary>
    /// Streaming Assitant (SA) was the first program used to connect your PICO
    /// device to the computer.
    /// </summary>
    StreamingAssistant,

    /// <summary>
    /// PICO Connect is the successor of SA.
    /// Currently PicoConnect lacks of proper API to get facetracking data, so
    /// we force it to work like the old SA did and that way we can re-use the module.
    /// </summary>
    PicoConnect,

    /// <summary>
    /// Business Streaming (BS) is the successor of SA for business devices (PICO 4 enterprise).
    /// Due to a internal change since one BS version we keep this (BusinessStreamingV1)
    /// entry to refere to "old BS 1.X programs".
    /// Internally works similar to how SA did.
    /// </summary>
    BusinessStreamingV1,

    /// <summary>
    /// Business Streaming (BS) is the successor of SA for business devices (PICO 4 enterprise).
    /// Since 2.0, internally works similar to how PicoConnect do.
    /// </summary>
    BusinessStreaming
}