using System.Threading.Channels;

namespace Pico4SAFTExtTrackingModule.PacketLogger;

public sealed class PacketLogger<T> : IDisposable where T : struct
{
    private const char CSV_DELIMITER = ';';

    private readonly Thread _thread;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private readonly string _filePath;

    private readonly IDataExtractor<T> _dataExtractor;
    private readonly Channel<T> _channel = Channel.CreateUnbounded<T>();

    public PacketLogger(string filePath, IDataExtractor<T> dataExtractor)
    {
        _dataExtractor = dataExtractor;

        _filePath = filePath;

        _thread = new(ThreadMethod);
        _thread.Start();
    }

    public void UpdateValue(in T obj)
    {
        _channel.Writer.TryWrite(obj);
    }

    private void ThreadMethod()
    {
        using var writer = File.CreateText(_filePath);
        writer.WriteLine(_dataExtractor.GetCSVHeader(CSV_DELIMITER)); // TODO add timestamp

        while (!_cancellationTokenSource.IsCancellationRequested)
        {
            if (!_channel.Reader.TryRead(out var value))
            {
                Thread.Yield();
                continue;
            }

            writer.WriteLine(_dataExtractor.ToCSV(value, CSV_DELIMITER));
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _thread.Join();
    }
}
