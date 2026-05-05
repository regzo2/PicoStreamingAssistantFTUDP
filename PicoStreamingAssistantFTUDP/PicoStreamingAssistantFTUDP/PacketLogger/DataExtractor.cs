namespace Pico4SAFTExtTrackingModule.PacketLogger;

public interface IDataExtractor<T> where T : struct
{
    void Clone(in T obj, ref T ret);
    string ToCSV(in T obj, char delimiter);
    string GetCSVHeader(char delimiter);
}
