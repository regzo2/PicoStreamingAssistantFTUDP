using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pico4SAFTExtTrackingModule.PacketLogger;

namespace Pico4SAFTExtTrackingModule;

[TestClass]
public class PacketLoggerShould
{
    [TestMethod, Timeout(5_000)]
    public unsafe void WaitForDataAndStopWhenDisposed()
    {
        string path = Path.Combine(Path.GetTempPath(), $"pico-packet-{Guid.NewGuid():N}.csv");
        var extractor = new SignalingExtractor();
        try
        {
            using (var logger = new PacketLogger<int>(path, extractor))
            {
                Assert.IsFalse(extractor.Written.Wait(100), "No packet should be written before data arrives.");
                int value = 42;
                logger.UpdateValue(&value);
                Assert.IsTrue(extractor.Written.Wait(2_000), "The writer should wake for a new packet.");
            }

            CollectionAssert.AreEqual(new[] { "value", "42" }, File.ReadAllLines(path));
        }
        finally
        {
            extractor.Written.Dispose();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private sealed class SignalingExtractor : DataExtractor<int>
    {
        public ManualResetEventSlim Written { get; } = new();

        public unsafe void Clone(int* source, int* destination) => *destination = *source;

        public unsafe string ToCSV(int* value, char delimiter)
        {
            Written.Set();
            return (*value).ToString();
        }

        public string GetCSVHeader(char delimiter) => "value";
    }
}
