using Pico4SAFTExtTrackingModule.PicoConnectors;

namespace Pico4SAFTExtTrackingModule.PacketLogger;

public sealed class PicoDataLoggerHelper
{
    /// <summary>
    /// It will leave `PxrFTInfo` as all zeroes, except the `blendShapeWeight` (that will copy from the argument)
    /// </summary>
    /// <param name="blendshapes"></param>
    /// <returns></returns>
    public static PxrFTInfo FillPxrFTInfo(ReadOnlySpan<float> blendshapes)
    {
        PxrFTInfo r = default;
        for (int i = 0; i < Pxr.BLEND_SHAPE_NUMS; i++)
        {
            r.blendShapeWeight[i] = blendshapes[i];
        }
        return r;
    }
}