using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Pico4SAFTExtTrackingModule;

public sealed class PicoDataLoggerFactory
{
    protected sealed class PicoDataExtractor : DataExtractor<PxrFTInfo>
    {
        public unsafe void Clone(PxrFTInfo *obj, PxrFTInfo* ret)
        {
            ret->timestamp = obj->timestamp;
            ret->laughingProb = obj->laughingProb;

            // TODO I'm sure there's an equivalent of `memcpy` for C# but I couldn't find it
            for (int n = 0; n < Pxr.BLEND_SHAPE_NUMS; n++) ret->blendShapeWeight[n] = obj->blendShapeWeight[n];
            for (int n = 0; n < 10; n++) ret->videoInputValid[n] = obj->videoInputValid[n];
            for (int n = 0; n < 10; n++) ret->emotionProb[n] = obj->emotionProb[n];
        }

        public string GetCSVHeader(char delimiter)
        {
            StringBuilder sb = new StringBuilder();

            foreach (BlendShapeIndex shape in Enum.GetValues(typeof(BlendShapeIndex)))
            {
                sb.Append(Enum.GetName(typeof(BlendShapeIndex), shape));
                sb.Append(delimiter);
            }

            sb.Length--; // remove the last delimiter
            return sb.ToString();
        }

        public unsafe string ToCSV(PxrFTInfo *obj, char delimiter)
        {
            StringBuilder sb = new StringBuilder();

            for (int n = 0; n < Pxr.BLEND_SHAPE_NUMS; n++)
            {
                sb.Append(obj->blendShapeWeight[n]);
                sb.Append(delimiter);
            }

            sb.Length--; // remove the last delimiter
            return sb.ToString();
        }
    }

    public static unsafe Logger<PxrFTInfo> build(string path)
    {
        return new Logger<PxrFTInfo>(path, new PicoDataExtractor());
    }
}
