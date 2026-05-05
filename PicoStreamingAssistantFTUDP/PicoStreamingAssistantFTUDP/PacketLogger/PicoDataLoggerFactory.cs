using System.Text;

using Pico4SAFTExtTrackingModule.PicoConnectors;

namespace Pico4SAFTExtTrackingModule.PacketLogger;

public sealed class PicoDataLoggerFactory
{
    public static PacketLogger<PxrFTInfo> Build(string path)
        => new(path, new PicoDataExtractor());
}

file sealed class PicoDataExtractor : IDataExtractor<PxrFTInfo>
{
    public void Clone(in PxrFTInfo obj, ref PxrFTInfo ret) => ret = obj; // ValueType always clone it

    public string GetCSVHeader(char delimiter)
    {
        StringBuilder sb = new();

        foreach (var shape in Enum.GetValues<BlendShapeIndex>())
        {
            sb.Append(Enum.GetName(shape));
            sb.Append(delimiter);
        }

        sb.Length--; // remove the last delimiter
        return sb.ToString();
    }

    public string ToCSV(in PxrFTInfo obj, char delimiter)
    {
        ReadOnlySpan<float> span = obj.blendShapeWeight;
        StringBuilder sb = new();

        for (int n = 0; n < span.Length; n++)
        {
            sb.Append(span[n]);
            sb.Append(delimiter);
        }

        sb.Length--; // remove the last delimiter
        return sb.ToString();
    }
}
