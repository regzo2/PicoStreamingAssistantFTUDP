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

    private ILogger Logger;
    private UdpClient? udpClient;
    private IPEndPoint? endPoint;
    private PxrFTInfo data;

    private string processName;

    public LegacyConnector(ILogger Logger, PicoPrograms program_using)
    {
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
        int retry = 0;

    ReInitialize:
        try
        {
            udpClient = new UdpClient(PORT_NUMBER);
            endPoint = new IPEndPoint(IPAddress.Parse(IP_ADDRESS), PORT_NUMBER);
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
                    ReceivePxrData(pData);
            }
            Logger.LogDebug("{} handshake success.", this.processName);
        }
        catch (SocketException ex) when (ex.ErrorCode is 10048)
        {
            if (retry >= 3) return false;
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
        catch (Exception e)
        {
            Logger.LogWarning("{exception}", e);
            return false;
        }

        udpClient.Client.ReceiveTimeout = 5000;

        return true;
    }

    public unsafe float* GetBlendShapes()
    {
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
        Logger.LogInformation("Disposing of PxrFaceTracking UDP Client.");
        if (udpClient is not null) udpClient.Client.Blocking = false;
        udpClient?.Dispose();
        udpClient = null;
        endPoint = null;
    }

    private unsafe bool ReceivePxrData(PxrFTInfo* pData)
    {
        if (udpClient == null || endPoint == null) return false;

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

    public string GetProcessName()
    {
        return this.processName;
    }
}
