namespace Pico4SAFTExtTrackingModule.PicoConnectors;

public interface IPicoConnector
{
    string GetProcessName();

    bool Connect();

    ReadOnlySpan<float> GetBlendShapes();

    void Teardown();
}