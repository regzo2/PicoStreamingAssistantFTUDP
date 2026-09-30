using Microsoft.Extensions.Logging;
using Pico4SAFTExtTrackingModule.PacketLogger;
using System.Net;
using System.Net.Sockets;
using VRCFaceTracking.Core.Params.Data;
using VRCFaceTracking;
using Pico4SAFTExtTrackingModule.Diagnostics;
using Pico4SAFTExtTrackingModule.PicoConnectors.ProgramChecker;

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
    // Minimum accepted length for the legacy layout declared in Pxr.cs:
    //   16 (header) + 8 (uninterpreted timestamp) + 72 * 4 (weights) = 312 bytes.
    // Only weights are used. The 596-byte suffix of the full PxrFTInfo is not copied;
    // this decoder accepts an absent, partial or complete suffix. This acceptance rule
    // does not establish that PICO defines those fields as optional in its wire protocol.
    // PICO Connect legacy setup (configuration guidance, not the source of these sizes):
    // https://docs.vrcft.io/docs/hardware/vr/pico/pico4pe
    private const int BlendShapePayloadSize = sizeof(long) + Pxr.BLEND_SHAPE_NUMS * sizeof(float);
    private static readonly int MinimumPacketSize = pxrHeaderSize + BlendShapePayloadSize;
    private const int MaxPacketsPerUpdate = 1024;

    private bool disposedValue, connecting;
    private object socketLock;
    private ILogger Logger;
    private UdpClient? udpClient;
    private IPEndPoint? endPoint;
    private PxrFTInfo data;
    private readonly PicoDiagnostics diagnostics;
    private readonly PicoPrograms program;
    private readonly bool ownsDiagnostics;
    private ReceiveDiagnostics? receiveDiagnostics;

    private string processName;

    public LegacyConnector(ILogger Logger, PicoPrograms program_using)
    {
        this.disposedValue = false;
        this.connecting = false;
        this.socketLock = new object();

        diagnostics = PicoDiagnostics.ForLogger(Logger);
        ownsDiagnostics = !ReferenceEquals(Logger, diagnostics.Logger);
        this.Logger = diagnostics.Logger;
        program = program_using;

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
                this.Logger.LogWarning("Couldn't find the name for program {Program}", program_using);
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
        try
        {
            lock (this.socketLock)
            {
                // UdpClient(port) binds all IPv4 interfaces (0.0.0.0:29765).
                udpClient = new UdpClient(PORT_NUMBER);
                // Receive(ref endPoint) replaces this value with the actual sender;
                // initializing it to 127.0.0.1 does not restrict who can send packets.
                endPoint = new IPEndPoint(IPAddress.Parse(IP_ADDRESS), PORT_NUMBER);
                udpClient.Client.ReceiveTimeout = 5000;
            }

            receiveDiagnostics = new ReceiveDiagnostics(diagnostics,
                () => new ProcessRunningProgramChecker().Check(program));

            // Binding the port is enough to initialize. Face tracking data may
            // arrive later; Update() will receive it when the service is ready.
            result = true;
            Logger.LogInformation("UDP listener bound to {LocalEndpoint} for {Program}; receiveTimeout={Timeout}ms. " +
                "Waiting for valid face data; sender endpoints are not restricted.",
                udpClient.Client.LocalEndPoint, processName, udpClient.Client.ReceiveTimeout);
        }
        catch (SocketException ex) when (ex.ErrorCode is 10048)
        {
            Logger.LogWarning(ex, "PICO UDP port {Port} is in use; connection retry in 5000ms.", PORT_NUMBER);
            result = false;
        }
        catch (Exception e)
        {
            Logger.LogWarning(e, "Could not bind PICO UDP listener on port {Port}; retry in 5000ms.", PORT_NUMBER);
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

        receiveDiagnostics?.Statistics(final: true);
        Logger.LogInformation("Closing local PICO UDP listener for {Program}.", processName);
        lock (this.socketLock)
        {
            if (udpClient is not null)
            {
                udpClient.Client.Close();
            }
            udpClient?.Dispose();
        }

        lock (this.socketLock)
        {
            udpClient = null;
            endPoint = null;
        }
        if (ownsDiagnostics) diagnostics.Flush(forget: true);
    }

    private unsafe bool ReceivePxrData(PxrFTInfo* pData)
    {
        if (this.IsDisposed()) return false;

        int validInBatch = 0;
        int drained = 0;
        bool limitReached = false;
        bool receptionCompleted = false;
        try
        {
            // Update can run slower than the incoming UDP stream. Drain queued
            // packets so tracking reflects the newest valid sample, not old data.
            var received = false;
            for (var i = 0; i < MaxPacketsPerUpdate; i++)
            {
                if (i > 0 && udpClient!.Available == 0) break;
                byte[] packet = udpClient!.Receive(ref endPoint);
                drained++;
                receiveDiagnostics?.Packet(packet.Length);
                // Sender endpoints are not checked, so any sender that can reach the bound
                // port can supply a datagram. This length check bounds the memory copies;
                // the tracking_type check below does not authenticate the sender or validate
                // the other header fields, fragmentation flags or floating-point values.
                if (packet.Length < MinimumPacketSize)
                {
                    receiveDiagnostics?.ShortPacket(packet.Length, endPoint);
                    continue;
                }

                fixed (byte* ptr = packet)
                {
                    TrackingDataHeader tdh;
                    Buffer.MemoryCopy(ptr, &tdh, pxrHeaderSize, pxrHeaderSize);
                    if (tdh.tracking_type != 2)
                    {
                        receiveDiagnostics?.OtherType(tdh.tracking_type, packet.Length, endPoint);
                        continue;
                    }

                    Buffer.MemoryCopy(ptr + PacketIndex, pData, sizeof(PxrFTInfo), BlendShapePayloadSize);
                    received = true;
                    validInBatch++;
                    receiveDiagnostics?.ValidPacket(packet.Length, endPoint);
                }
            }
            limitReached = drained == MaxPacketsPerUpdate && udpClient!.Available > 0;
            receptionCompleted = true;
            return received;
        }
        catch (SocketException ex) when (ex.ErrorCode is 10060)
        {
            // No face data yet. Keep the UDP socket bound so later packets can
            // be consumed without restarting the connector.
            receiveDiagnostics?.Timeout();
            receptionCompleted = true;
            return false; // got byte failed
        }
        catch (SocketException ex) when (ex.ErrorCode is 10004)
        {
            // `Teardown()` called
            Logger.LogDebug("UDP receive interrupted (10004); local socket is closing.");
            return false; // got byte failed
        }
        finally
        {
            receiveDiagnostics?.EndBatch(validInBatch, limitReached, observeHealth: receptionCompleted);
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
