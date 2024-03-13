using System.Net;
using System.Net.Sockets;

using Microsoft.Extensions.Logging;
using Pico4SAFTExtTrackingModule.PicoConnectors;
using Pico4SAFTExtTrackingModule.PacketLogger;
using VRCFaceTracking;
using VRCFaceTracking.Core.Library;
using VRCFaceTracking.Core.Params.Data;
using VRCFaceTracking.Core.Params.Expressions;
using Pico4SAFTExtTrackingModule.PicoConnectors.ProgramChecker;
using Pico4SAFTExtTrackingModule.PicoConnectors.ConfigChecker;

namespace Pico4SAFTExtTrackingModule;

public sealed class Pico4SAFTExtTrackingModule : ExtTrackingModule, IDisposable
{
    private bool disposedValue;
    private IPicoConnector? connector;
    private (bool, bool) trackingState = (false, false);

    private const bool FILE_LOG = false;
    public static readonly string LOGGER_PATH = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VRCFaceTracking\\PICOLogs.csv");
    private PacketLogger<PxrFTInfo>? logger;

    public override (bool SupportsEye, bool SupportsExpression) Supported { get; } = (true, true);

    private bool StreamerValidity()
    {
        this.connector = ConnectorFactory.build(Logger, new ProcessRunningProgramChecker(), new ConfigChecker(Logger));
        if (this.connector == null)
        {
            Logger.LogError("\"Streaming Assistant\", \"Streaming Assistant\" or \"PICO Connect\" process was not found. Please run the Streaming Assistant or PICO Connect before VRCFaceTracking.");
            return false;
        }

        Logger.LogInformation("Using {}.", this.connector.GetProcessName());
        return true;
    }

    public override (bool eyeSuccess, bool expressionSuccess) Initialize(bool eyeAvailable, bool expressionAvailable)
    {
        trackingState = (eyeAvailable, expressionAvailable);
        if (!StreamerValidity() || (!eyeAvailable && !expressionAvailable))
        {
            Logger.LogWarning("No data is usable, skipping initialization.");
            return (false, false);
        }

        Logger.LogInformation("Initializing {} data stream.", this.connector.GetProcessName());
        if (!this.connector.Connect())
        {
            Logger.LogWarning("Module failed to establish a connection.");
            Teardown(); // closes client and any other objects
            return (false, false);
        }

        if (FILE_LOG)
        {
            this.logger = PicoDataLoggerFactory.build(LOGGER_PATH);
            Logger.LogInformation("Using {} path for PICO logs.", LOGGER_PATH);
        }

        ModuleInformation.Name = "Pico 4 Pro / Enterprise";

        var stream = typeof(Pico4SAFTExtTrackingModule).Assembly.GetManifestResourceStream("Pico4SAFTExtTrackingModule.Assets.pico-hmd.png");
        ModuleInformation.StaticImages = stream is not null ? new List<Stream> { stream } : ModuleInformation.StaticImages;

        if (!trackingState.Item1)
            Logger.LogInformation("Eye tracking already in use, disabling eye data."); 
        if (!trackingState.Item2) 
            Logger.LogInformation("Expression Tracking already in use, disabling expression data.");

        return trackingState;
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
                float* pxrShape = this.connector.GetBlendShapes();
                if (pxrShape != null)
                {
                    if (this.logger != null)
                    {
                        // legacy; PacketLogger#UpdateValue needs a PxrFTInfo; but we don't want to send that outside from the PicoConnector
                        PxrFTInfo data = PicoDataLoggerHelper.FillPxrFTInfo(pxrShape);
                        this.logger.UpdateValue(&data);
                    }

                    fixed (UnifiedExpressionShape* unifiedShape = UnifiedTracking.Data.Shapes)
                    {
                        if (trackingState.Item1)
                        {
                            fixed (UnifiedSingleEyeData* pLeft = &UnifiedTracking.Data.Eye.Left)
                            fixed (UnifiedSingleEyeData* pRight = &UnifiedTracking.Data.Eye.Right)
                            {
                                UpdateEye(pxrShape, pLeft, pRight);
                                UpdateEyeExpression(pxrShape, unifiedShape);
                            }
                        }

                        if (trackingState.Item2) 
                            UpdateExpression(pxrShape, unifiedShape);
                    }
                }
            }
        }
        catch (SocketException ex) when (ex.ErrorCode is 10060)
        {
            if (!StreamerValidity())
                Logger.LogInformation("Streaming Assistant, Business Streaming or PICO Connect is currently not running. Please ensure one program is running to send tracking data.");
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
                if (this.connector != null)
                {
                    this.connector.Teardown();
                    this.connector = null;
                }

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