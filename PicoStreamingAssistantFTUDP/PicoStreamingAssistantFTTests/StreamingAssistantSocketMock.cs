using System.Net.Sockets;
using System.Net;
using Pico4SAFTExtTrackingModule.PicoConnectors;
using System.Runtime.CompilerServices;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Pico4SAFTExtTrackingModule.Mocks;

public sealed class StreamingAssistantSocketMock
{
    private static readonly string IP = "127.0.0.1";
    private static readonly int SA_PORT = 29765;

    private static readonly unsafe int pxrHeaderSize = sizeof(TrackingDataHeader);
    private static readonly unsafe int pxrFtInfoSize = sizeof(PxrFTInfo);
    private static readonly int PacketSize = pxrHeaderSize + pxrFtInfoSize;

    private bool _disposed;
    private UdpClient? serverSocket;
    private byte[] sending;

    public StreamingAssistantSocketMock()
    {
        this.SetDefaultTrackingData();
    }

    private void SetDefaultTrackingData()
    {
        this.sending = new byte[PacketSize];


        // header
        TrackingDataHeader tdh = new TrackingDataHeader();
        tdh.tracking_type = 2; // this specifies it's ft/et

        IntPtr ptr = IntPtr.Zero;
        try
        {
            ptr = Marshal.AllocHGlobal(pxrHeaderSize);
            Marshal.StructureToPtr(tdh, ptr, true);
            Marshal.Copy(ptr, this.sending, 0, pxrHeaderSize);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }


        // data
        PxrFTInfo data = new PxrFTInfo();

        ptr = IntPtr.Zero;
        try
        {
            ptr = Marshal.AllocHGlobal(pxrFtInfoSize);
            Marshal.StructureToPtr(data, ptr, true);
            Marshal.Copy(ptr, this.sending, pxrHeaderSize, pxrFtInfoSize);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    public void Setup()
    {
        this._disposed = false;
        this.serverSocket = new UdpClient();
    }

    public void Run()
    {
        if (this.serverSocket == null) throw new Exception("You have to call `Setup()` before Run");
        if (this._disposed) throw new Exception("Socket closed; Run `Setup()` first");

        while (!this._disposed)
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(IP), SA_PORT);
            lock (this.sending)
            {
                this.serverSocket.Send(this.sending, PacketSize, ep);
            }

            Thread.Sleep(100);
        }
    }

    public void SetSendingData(PxrFTInfo info)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        if (this._disposed) return;

        this.serverSocket?.Client?.Shutdown(SocketShutdown.Receive);
        this.serverSocket?.Client?.Close();
        this.serverSocket?.Dispose();
        this._disposed = true;
    }
}
