using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pico4SAFTExtTrackingModule.PacketLogger;

public interface DataExtractor<T>
{
    unsafe void Clone(T *obj, T *ret);
    unsafe string ToCSV(T *obj, char delimiter);
    string GetCSVHeader(char delimiter);
}
