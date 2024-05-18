using Microsoft.Extensions.Logging;
using Pico4SAFTExtTrackingModule.PacketLogger;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using VRCFaceTracking.Core.Params.Data;
using VRCFaceTracking;

namespace Pico4SAFTExtTrackingModule.PicoConnectors;

/**
 * Connector class for Streaming Assitant & Business Streaming.
 * Also used for PICO Connect on `mergetype=2`
 **/
public sealed class LegacyConnector : IPicoConnector
{
    private const string IP_ADDRESS = "127.0.0.1";
    private const int PORT_NUMBER = 29765;

    private static readonly unsafe int pxrHeaderSize = sizeof(TrackingDataHeader);
    private readonly int PacketIndex = pxrHeaderSize;
    private static readonly unsafe int pxrFtInfoSize = sizeof(PxrFTInfo);
    private static readonly int PacketSize = pxrHeaderSize + pxrFtInfoSize;

    private bool disposedValue, connecting;
    private object socketLock;
    private ILogger Logger;
    private UdpClient? udpClient;
    private IPEndPoint? endPoint;
    private PxrFTInfo data;
    private Thread? tryReinitializeThread;

    private string processName;

    public LegacyConnector(ILogger Logger, PicoPrograms program_using)
    {
        this.disposedValue = false;
        this.connecting = false;
        this.socketLock = new object();

        this.tryReinitializeThread = null;
        this.Logger = Logger;

        switch (program_using)
        {
            case PicoPrograms.StreamingAssistant:
                this.processName = "Streaming Assistant";
                break;

            case PicoPrograms.BusinessStreaming:
                this.processName = "Business Streaming";
                break;

            case PicoPrograms.PicoConnect:
                this.processName = "PICO Connect";
                break;

            default:
                // shouldn't reach this
                Logger.LogWarning("Couldn't find the name for program " + program_using.ToString());
                this.processName = "[?]";
                break;
        }
    }

    public bool Connect()
    {
        lock (this.socketLock)
        {
            this.disposedValue = false;
            this.connecting = true;
        }

        bool result;
        int retry = 0;

    ReInitialize:
        try
        {
            lock (this.socketLock)
            {
                udpClient = new UdpClient(PORT_NUMBER);
                endPoint = new IPEndPoint(IPAddress.Parse(IP_ADDRESS), PORT_NUMBER);
            }
            // Since Streaming Assistant is already running,
            // this module is indeed needed,
            // so the timeout failure is unnecessary.
            // udpClient.Client.ReceiveTimeout = 15000; // Initialization timeout.

            Logger.LogDebug("Host end-point: {endPoint}", endPoint);
            Logger.LogDebug("Initialization Timeout: {timeout}ms", udpClient.Client.ReceiveTimeout);
            Logger.LogDebug("Client established: attempting to receive PxrFTInfo.");

            Logger.LogInformation("Waiting for {} data stream.", this.processName);
            unsafe
            {
                fixed (PxrFTInfo* pData = &data)
                {
                    result = ReceivePxrData(pData);
                }
            }

            if (result)
            {
                Logger.LogInformation("{} handshake success.", this.processName);

                udpClient.Client.ReceiveTimeout = 5000;
            }
        }
        catch (SocketException ex) when (ex.ErrorCode is 10048)
        {
            if (retry >= 3) result = false;
            else {
                retry++;
                // Magic
                // Close the pico_et_ft_bt_bridge.exe process and reinitialize it.
                // It will listen to UDP port 29763 before pico_et_ft_bt_bridge.exe runs.
                // Note: exclusively to simplify older versions of the FT bridge,
                // the bridge now works without any need for process killing.
                Process proc = new()
                {
                    StartInfo = {
                        FileName = "taskkill.exe",
                        ArgumentList = {
                            "/f",
                            "/t",
                            "/im",
                            "pico_et_ft_bt_bridge.exe"
                        },
                        CreateNoWindow = true
                    }
                };
                proc.Start();
                proc.WaitForExit();
                goto ReInitialize;
            }
        }
        catch (Exception e)
        {
            Logger.LogWarning("{exception}", e);
            result = false;
        }

        lock (this.socketLock)
        {
            this.connecting = false;
        }
        return result;
    }

    public unsafe float* GetBlendShapes()
    {
        lock (this.socketLock)
        {
            if (this.connecting) return null;
        }

        fixed (PxrFTInfo* pData = &data)
            if (ReceivePxrData(pData))
            {
                float* pxrShape = pData->blendShapeWeight;
                return pxrShape;
            }

        return null;
    }

    public void Teardown()
    {
        lock (this.socketLock)
        {
            bool needsTeardown = (!this.disposedValue);
            if (!needsTeardown) return;
            this.disposedValue = true;
        }

        Logger.LogInformation("Disposing of PxrFaceTracking UDP Client.");
        lock (this.socketLock)
        {
            if (udpClient is not null)
            {
                udpClient.Client.Blocking = false;
                udpClient.Client.Shutdown(SocketShutdown.Receive);
                udpClient.Client.Close();
            }
            udpClient?.Dispose();
        }

        this.tryReinitializeThread?.Join();

        lock (this.socketLock)
        {
            udpClient = null;
            endPoint = null;
        }
    }

    private unsafe bool ReceivePxrData(PxrFTInfo* pData)
    {
        if (this.IsDisposed()) return false;

        try
        {
            fixed (byte* ptr = udpClient!.Receive(ref endPoint))
            {
                if (ptr == null) return false;

                TrackingDataHeader tdh;
                Buffer.MemoryCopy(ptr, &tdh, pxrHeaderSize, pxrHeaderSize);
                if (tdh.tracking_type != 2) return false; // not facetracking packet

                Buffer.MemoryCopy(ptr + PacketIndex, pData, pxrFtInfoSize, pxrFtInfoSize);
            }
            return true;
        }
        catch (SocketException ex) when (ex.ErrorCode is 10060)
        {
            // socket time out
            Logger.LogDebug("Data was not sent within the timeout. {msg}", ex.Message);
            Logger.LogInformation("Data was not sent within the timeout (is headset hibernated?), reinitialize...");

            // try to reinitialize
            this.Teardown();
            lock(this.socketLock) {
                this.tryReinitializeThread = new Thread(new ThreadStart(() => {
                    bool connected;
                    do {
                        connected = this.Connect();
                        if (!connected) Thread.Sleep(200); // try again; we have to set a low number because VRCFT won't call `Teardown()` until all the updates are done
                    } while (!this.IsDisposed() && !connected);
                }));
                this.tryReinitializeThread.Start();
            }

            return false; // got byte failed
        }
        catch (SocketException ex) when (ex.ErrorCode is 10004)
        {
            // `Teardown()` called
            Logger.LogInformation("Socket closed");
            return false; // got byte failed
        }
    }

    public bool IsDisposed()
    {
        lock (this.socketLock)
        {
            return this.disposedValue;
        }
    }

    public string GetProcessName()
    {
        return this.processName;
    }
}
