using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pico4SAFTExtTrackingModule.PacketLogger;

public sealed class PacketLogger<T> : IDisposable
{
    private const char CSV_DELIMITER = ';';

    private Thread thread;
    private bool threadEnabled;

    private string filePath;

    private DataExtractor<T> dataExtractor;
    private bool waiting;
    private T current;
    private object waitingCurrentLock = new object();

    public PacketLogger(string filePath, DataExtractor<T> dataExtractor)
    {
        this.dataExtractor = dataExtractor;
        this.waiting = true; // request first data

        this.filePath = filePath;

        this.threadEnabled = true;

        this.thread = new Thread(new ThreadStart(ThreadMethod));
        this.thread.Start();
    }

    public unsafe void UpdateValue(T* obj)
    {
        if (!this.IsWaiting()) return; // already got something

        fixed (T* ret = &this.current) {
            dataExtractor.Clone(obj, ret);
        }
        lock (this.waitingCurrentLock)
        {
            this.waiting = false; // got the new data
        }
    }

    protected unsafe void ThreadMethod()
    {
        using (StreamWriter writer = new StreamWriter(this.filePath))
        {
            writer.WriteLine(this.dataExtractor.GetCSVHeader(CSV_DELIMITER)); // TODO add timestamp

            while (this.threadEnabled)
            {
                if (this.IsWaiting()) continue; // no data; try again later

                fixed (T *curr = &this.current) {
                    writer.WriteLine(this.dataExtractor.ToCSV(curr, CSV_DELIMITER));
                }
                lock (this.waitingCurrentLock)
                {
                    this.waiting = true; // request a new data
                }
            }
        }
    }

    public void Dispose()
    {
        this.threadEnabled = false;
        this.thread.Join();
    }

    protected bool IsWaiting()
    {
        bool waiting;
        lock(this.waitingCurrentLock)
        {
            waiting = this.waiting;
        }
        return waiting;
    }
}
