using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

using Microsoft.Extensions.Logging;

using VRCFaceTracking;
using VRCFaceTracking.Core.Library;
using VRCFaceTracking.Core.Params.Data;
using VRCFaceTracking.Core.Params.Expressions;

namespace Pico4SAFTExtTrackingModule;

public sealed class Pico4SAFTExtTrackingModule : ExtTrackingModule, IDisposable
{
    private const string IP_ADDRESS = "127.0.0.1";
    private const int PORT_NUMBER = 29763; // Temporary port as of current Pico 4 SA app.
    private const bool hasHeader = false;
    private const int pxrHeaderSize = sizeof(ushort);
    private const int PacketIndex = hasHeader ? pxrHeaderSize : 0;
    private static readonly unsafe int pxrFtInfoSize = sizeof(PxrFTInfo);
    private static readonly int PacketSize = hasHeader ? pxrHeaderSize + pxrFtInfoSize : pxrFtInfoSize;
    private bool disposedValue;
    private UdpClient? udpClient;
    private IPEndPoint? endPoint;
    private PxrFTInfo data;

    private const bool FILE_LOG = true;
    public static readonly string LOGGER_PATH = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PICOLogs.csv");
    private Logger<PxrFTInfo> logger;

    public override (bool SupportsEye, bool SupportsExpression) Supported { get; } = (true, true);

    private bool StreamerValidity()
    {
        if (Process.GetProcessesByName("Streaming Assistant").Length is 0)
        {
            Logger.LogError("\"Streaming Assistant\" process was not found. Please run the Streaming Assistant before VRCFaceTracking.");
            return false;
        }
        return true;
    }

    public override (bool eyeSuccess, bool expressionSuccess) Initialize(bool eyeAvailable, bool expressionAvailable)
    {
        int retry = 0;
        if (!StreamerValidity())
            return (false, false);
        Logger.LogInformation("Initializing Streaming Assistant data stream.");
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

            Logger.LogInformation("Waiting for Streaming Assistant data stream.");
            unsafe
            {
                fixed (PxrFTInfo* pData = &data)
                    ReceivePxrData(pData);
            }
            Logger.LogDebug("Streaming Assistant handshake success.");

            if (FILE_LOG)
            {
                this.logger = PicoDataLoggerFactory.build(LOGGER_PATH);
                Logger.LogDebug("Using {} path for PICO logs.", LOGGER_PATH);
            }
        }
        catch (SocketException ex) when (ex.ErrorCode is 10048)
        {
            if (retry >= 3)
                return (false, false);
            retry++;
            // Magic
            // Close the pico_et_ft_bt_bridge.exe process and reinitialize it.
            // It will listen to UDP port 29763 before pico_et_ft_bt_bridge.exe runs.
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
            Logger.LogWarning("Module failed to establish a connection.");
            Logger.LogInformation("Tip: Did you forget to run Streaming Assistant?");
            Logger.LogWarning("{exception}", e);
            Teardown(); // Closes UDP client and any other objects
            return (false, false);
        }

        ModuleInformation.Name = "Pico 4 Pro / Enterprise";

        var stream = typeof(Pico4SAFTExtTrackingModule).Assembly.GetManifestResourceStream("Pico4SAFTExtTrackingModule.Assets.pico-hmd.png");
        ModuleInformation.StaticImages = stream is not null ? new List<Stream> { stream } : ModuleInformation.StaticImages;

        udpClient.Client.ReceiveTimeout = 5000;

        return (true, true);
    }

    private unsafe void ReceivePxrData(PxrFTInfo* pData)
    {
        fixed (byte* ptr = udpClient!.Receive(ref endPoint))
            Buffer.MemoryCopy(ptr + PacketIndex, pData, PacketSize, PacketSize);
    }

    private static unsafe void UpdateEye(float* pxrShape, UnifiedSingleEyeData* left, UnifiedSingleEyeData* right)
    {
        // to be tested, not entirely sure how Pxr blink/squint will translate to Openness.
        left->Openness = 1f - pxrShape[(int)BlendShapeIndex.EyeBlink_L];
        right->Openness = 1f - pxrShape[(int)BlendShapeIndex.EyeBlink_R];

        left->Gaze.x = pxrShape[(int)BlendShapeIndex.EyeLookIn_L] - pxrShape[(int)BlendShapeIndex.EyeLookOut_L];
        left->Gaze.y = pxrShape[(int)BlendShapeIndex.EyeLookUp_L] - pxrShape[(int)BlendShapeIndex.EyeLookDown_L];

        right->Gaze.x = pxrShape[(int)BlendShapeIndex.EyeLookOut_R] - pxrShape[(int)BlendShapeIndex.EyeLookIn_R];
        right->Gaze.y = pxrShape[(int)BlendShapeIndex.EyeLookUp_R] - pxrShape[(int)BlendShapeIndex.EyeLookDown_R];
    }

    private static unsafe void UpdateEyeExpression(float* pxrShape, UnifiedExpressionShape* unifiedShape)
    {
        #region Brow Shapes
        unifiedShape[(int)UnifiedExpressions.BrowInnerUpLeft].Weight = pxrShape[(int)BlendShapeIndex.BrowInnerUp];
        unifiedShape[(int)UnifiedExpressions.BrowInnerUpRight].Weight = pxrShape[(int)BlendShapeIndex.BrowInnerUp];
        unifiedShape[(int)UnifiedExpressions.BrowOuterUpLeft].Weight = pxrShape[(int)BlendShapeIndex.BrowOuterUp_L];
        unifiedShape[(int)UnifiedExpressions.BrowOuterUpRight].Weight = pxrShape[(int)BlendShapeIndex.BrowOuterUp_R];
        unifiedShape[(int)UnifiedExpressions.BrowLowererLeft].Weight = pxrShape[(int)BlendShapeIndex.BrowDown_L];
        unifiedShape[(int)UnifiedExpressions.BrowPinchLeft].Weight = pxrShape[(int)BlendShapeIndex.BrowDown_L];
        unifiedShape[(int)UnifiedExpressions.BrowLowererRight].Weight = pxrShape[(int)BlendShapeIndex.BrowDown_R];
        unifiedShape[(int)UnifiedExpressions.BrowPinchRight].Weight = pxrShape[(int)BlendShapeIndex.BrowDown_R];
        #endregion
        #region Eye Shapes
        unifiedShape[(int)UnifiedExpressions.EyeSquintLeft].Weight = pxrShape[(int)BlendShapeIndex.EyeSquint_L];
        unifiedShape[(int)UnifiedExpressions.EyeSquintRight].Weight = pxrShape[(int)BlendShapeIndex.EyeSquint_R];
        unifiedShape[(int)UnifiedExpressions.EyeWideLeft].Weight = pxrShape[(int)BlendShapeIndex.EyeWide_L];
        unifiedShape[(int)UnifiedExpressions.EyeWideRight].Weight = pxrShape[(int)BlendShapeIndex.EyeWide_R];
        #endregion
    }

    private static unsafe void UpdateExpression(float* pxrShape, UnifiedExpressionShape* unifiedShape)
    {
        // TODO: Map Viseme shapes onto face shapes.

        #region Jaw
        unifiedShape[(int)UnifiedExpressions.JawOpen].Weight = pxrShape[(int)BlendShapeIndex.JawOpen];
        unifiedShape[(int)UnifiedExpressions.JawLeft].Weight = pxrShape[(int)BlendShapeIndex.JawLeft];
        unifiedShape[(int)UnifiedExpressions.JawRight].Weight = pxrShape[(int)BlendShapeIndex.JawRight];
        unifiedShape[(int)UnifiedExpressions.JawForward].Weight = pxrShape[(int)BlendShapeIndex.JawForward];
        unifiedShape[(int)UnifiedExpressions.MouthClosed].Weight = pxrShape[(int)BlendShapeIndex.MouthClose];
        #endregion
        #region Cheek
        unifiedShape[(int)UnifiedExpressions.CheekPuffLeft].Weight = pxrShape[(int)BlendShapeIndex.CheekPuff];
        unifiedShape[(int)UnifiedExpressions.CheekPuffRight].Weight = pxrShape[(int)BlendShapeIndex.CheekPuff];
        unifiedShape[(int)UnifiedExpressions.CheekSquintLeft].Weight = pxrShape[(int)BlendShapeIndex.CheekSquint_L];
        unifiedShape[(int)UnifiedExpressions.CheekSquintRight].Weight = pxrShape[(int)BlendShapeIndex.CheekSquint_R];
        #endregion
        #region Nose
        unifiedShape[(int)UnifiedExpressions.NoseSneerLeft].Weight = pxrShape[(int)BlendShapeIndex.NoseSneer_L];
        unifiedShape[(int)UnifiedExpressions.NoseSneerRight].Weight = pxrShape[(int)BlendShapeIndex.NoseSneer_R];
        #endregion
        #region Mouth
        unifiedShape[(int)UnifiedExpressions.MouthUpperUpLeft].Weight = pxrShape[(int)BlendShapeIndex.MouthUpperUp_L];
        unifiedShape[(int)UnifiedExpressions.MouthUpperUpRight].Weight = pxrShape[(int)BlendShapeIndex.MouthUpperUp_R];
        unifiedShape[(int)UnifiedExpressions.MouthLowerDownLeft].Weight = pxrShape[(int)BlendShapeIndex.MouthLowerDown_L];
        unifiedShape[(int)UnifiedExpressions.MouthLowerDownRight].Weight = pxrShape[(int)BlendShapeIndex.MouthLowerDown_R];
        unifiedShape[(int)UnifiedExpressions.MouthFrownLeft].Weight = pxrShape[(int)BlendShapeIndex.MouthFrown_L];
        unifiedShape[(int)UnifiedExpressions.MouthFrownRight].Weight = pxrShape[(int)BlendShapeIndex.MouthFrown_R];
        unifiedShape[(int)UnifiedExpressions.MouthDimpleLeft].Weight = pxrShape[(int)BlendShapeIndex.MouthDimple_L];
        unifiedShape[(int)UnifiedExpressions.MouthDimpleRight].Weight = pxrShape[(int)BlendShapeIndex.MouthDimple_R];
        unifiedShape[(int)UnifiedExpressions.MouthUpperLeft].Weight = pxrShape[(int)BlendShapeIndex.MouthLeft];
        unifiedShape[(int)UnifiedExpressions.MouthLowerLeft].Weight = pxrShape[(int)BlendShapeIndex.MouthLeft];
        unifiedShape[(int)UnifiedExpressions.MouthUpperRight].Weight = pxrShape[(int)BlendShapeIndex.MouthRight];
        unifiedShape[(int)UnifiedExpressions.MouthLowerRight].Weight = pxrShape[(int)BlendShapeIndex.MouthRight];
        unifiedShape[(int)UnifiedExpressions.MouthPressLeft].Weight = pxrShape[(int)BlendShapeIndex.MouthPress_L];
        unifiedShape[(int)UnifiedExpressions.MouthPressRight].Weight = pxrShape[(int)BlendShapeIndex.MouthPress_R];
        unifiedShape[(int)UnifiedExpressions.MouthRaiserLower].Weight = pxrShape[(int)BlendShapeIndex.MouthShrugLower];
        unifiedShape[(int)UnifiedExpressions.MouthRaiserUpper].Weight = pxrShape[(int)BlendShapeIndex.MouthShrugUpper];
        unifiedShape[(int)UnifiedExpressions.MouthCornerPullLeft].Weight = pxrShape[(int)BlendShapeIndex.MouthSmile_L];
        unifiedShape[(int)UnifiedExpressions.MouthCornerSlantLeft].Weight = pxrShape[(int)BlendShapeIndex.MouthSmile_L];
        unifiedShape[(int)UnifiedExpressions.MouthCornerPullRight].Weight = pxrShape[(int)BlendShapeIndex.MouthSmile_R];
        unifiedShape[(int)UnifiedExpressions.MouthCornerSlantRight].Weight = pxrShape[(int)BlendShapeIndex.MouthSmile_R];
        unifiedShape[(int)UnifiedExpressions.MouthStretchLeft].Weight = pxrShape[(int)BlendShapeIndex.MouthStretch_L];
        unifiedShape[(int)UnifiedExpressions.MouthStretchRight].Weight = pxrShape[(int)BlendShapeIndex.MouthStretch_R];
        #endregion
        #region Lip
        unifiedShape[(int)UnifiedExpressions.LipFunnelUpperLeft].Weight = pxrShape[(int)BlendShapeIndex.MouthFunnel];
        unifiedShape[(int)UnifiedExpressions.LipFunnelUpperRight].Weight = pxrShape[(int)BlendShapeIndex.MouthFunnel];
        unifiedShape[(int)UnifiedExpressions.LipFunnelLowerLeft].Weight = pxrShape[(int)BlendShapeIndex.MouthFunnel];
        unifiedShape[(int)UnifiedExpressions.LipFunnelLowerRight].Weight = pxrShape[(int)BlendShapeIndex.MouthFunnel];
        unifiedShape[(int)UnifiedExpressions.LipPuckerUpperLeft].Weight = pxrShape[(int)BlendShapeIndex.MouthPucker];
        unifiedShape[(int)UnifiedExpressions.LipPuckerUpperRight].Weight = pxrShape[(int)BlendShapeIndex.MouthPucker];
        unifiedShape[(int)UnifiedExpressions.LipPuckerLowerLeft].Weight = pxrShape[(int)BlendShapeIndex.MouthPucker];
        unifiedShape[(int)UnifiedExpressions.LipPuckerLowerRight].Weight = pxrShape[(int)BlendShapeIndex.MouthPucker];
        unifiedShape[(int)UnifiedExpressions.LipSuckUpperLeft].Weight = pxrShape[(int)BlendShapeIndex.MouthRollUpper];
        unifiedShape[(int)UnifiedExpressions.LipSuckUpperRight].Weight = pxrShape[(int)BlendShapeIndex.MouthRollUpper];
        unifiedShape[(int)UnifiedExpressions.LipSuckLowerLeft].Weight = pxrShape[(int)BlendShapeIndex.MouthRollLower];
        unifiedShape[(int)UnifiedExpressions.LipSuckLowerRight].Weight = pxrShape[(int)BlendShapeIndex.MouthRollLower];
        #endregion
        #region Tongue
        unifiedShape[(int)UnifiedExpressions.TongueOut].Weight = pxrShape[(int)BlendShapeIndex.TongueOut];
        #endregion
    }

    public override void Update()
    {
        if (Status != ModuleState.Active)
        {
            Thread.Sleep(100);
            return;
        }

        try
        {
            unsafe
            {
                fixed (PxrFTInfo* pData = &data)
                fixed (UnifiedExpressionShape* unifiedShape = UnifiedTracking.Data.Shapes)
                fixed (UnifiedSingleEyeData* pLeft = &UnifiedTracking.Data.Eye.Left)
                fixed (UnifiedSingleEyeData* pRight = &UnifiedTracking.Data.Eye.Right)
                {
                    ReceivePxrData(pData);

                    if (this.logger != null) this.logger.UpdateValue(pData);

                    float* pxrShape = pData->blendShapeWeight;
                    UpdateEye(pxrShape, pLeft, pRight);
                    UpdateEyeExpression(pxrShape, unifiedShape);
                    UpdateExpression(pxrShape, unifiedShape);
                }
            }
        }
        catch (SocketException ex) when (ex.ErrorCode is 10060)
        {
            if (!StreamerValidity())
                Logger.LogInformation("Streaming Assistant is currently not running. Please ensure Streaming Assistant is running to send tracking data.");
            Logger.LogDebug("Data was not sent within the timeout. {msg}", ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Unexpected exceptions: {exception}", ex);
        }
    }

    public override void Teardown() => Dispose();

    private void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                if (udpClient is not null)
                    udpClient.Client.Blocking = false;
                Logger.LogInformation("Disposing of PxrFaceTracking UDP Client.");
                udpClient?.Dispose();

                if (this.logger != null)
                {
                    this.logger.Dispose();
                    this.logger = null;
                }
            }

            disposedValue = true;
        }
    }

    // ~Pico4SAFTExtTrackingModule()
    // {
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}