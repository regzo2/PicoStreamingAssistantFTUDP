using Pico4SAFTExtTrackingModule.PicoConnectors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pico4SAFTExtTrackingModule.PacketLogger;
public sealed class PicoDataLoggerHelper
{
    /**
     * It will leave `PxrFTInfo` as all zeroes, except the `blendShapeWeight` (that will copy from the argument)
     **/
    public static unsafe PxrFTInfo FillPxrFTInfo(float* blendshapes)
    {
        PxrFTInfo r = default;
        for (int i = 0; i < Pxr.BLEND_SHAPE_NUMS; i++)
        {
            r.blendShapeWeight[i] = blendshapes[i];
        }
        return r;
    }
}