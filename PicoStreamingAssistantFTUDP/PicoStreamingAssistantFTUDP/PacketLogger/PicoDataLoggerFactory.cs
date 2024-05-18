using System.Text;
using Pico4SAFTExtTrackingModule.PicoConnectors;
using System.Runtime.CompilerServices;

namespace Pico4SAFTExtTrackingModule.PacketLogger;

public sealed class PicoDataLoggerFactory
{
    protected sealed class PicoDataExtractor : DataExtractor<PxrFTInfo>
    {
        public unsafe void Clone(PxrFTInfo *obj, PxrFTInfo* ret)
        {
            Unsafe.CopyBlock(ret, obj, (uint)sizeof(PxrFTInfo));
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

    public static unsafe PacketLogger<PxrFTInfo> build(string path)
    {
        return new PacketLogger<PxrFTInfo>(path, new PicoDataExtractor());
    }
}
