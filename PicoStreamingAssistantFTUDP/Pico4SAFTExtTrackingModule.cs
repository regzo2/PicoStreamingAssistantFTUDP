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
using Pico4SAFTExtTrackingModule.BlendshapeScaler;

namespace Pico4SAFTExtTrackingModule;

public sealed class Pico4SAFTExtTrackingModule : ExtTrackingModule, IDisposable
{
    private bool disposedValue;
    private IPicoConnector? connector;
    private IBlendshapeScaler? scaler;
    public (bool, bool) trackingState = (false, false);

    private const bool FILE_LOG = false;
    public static readonly string LOGGER_PATH = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VRCFaceTracking\\PICOLogs.csv");
    private PacketLogger<PxrFTInfo>? logger;

    public override (bool SupportsEye, bool SupportsExpression) Supported { get; } = (true, true);

    public Pico4SAFTExtTrackingModule()
    {
        this.connector = null;
        this.scaler = null;
        this.logger = null;
        this.disposedValue = false;
    }

    public Pico4SAFTExtTrackingModule(IPicoConnector connector, IBlendshapeScaler scaler)
    {
        this.connector = connector;
        this.scaler = scaler;
        this.logger = null;
        this.disposedValue = false;
    }

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
        /*while (!this.disposedValue && !*/this.connector.Connect()/*) Thread.Sleep(4_000)*/;

        if (this.disposedValue)
        {
            Logger.LogWarning("Module failed to establish a connection.");
            return (false, false);
        }

        this.scaler = new FileBlendshapeScalerFactory().build(Logger);

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

    private unsafe void UpdateEye(float* pxrShape, UnifiedSingleEyeData* left, UnifiedSingleEyeData* right)
    {
        // to be tested, not entirely sure how Pxr blink/squint will translate to Openness.
        left->Openness = this.scaler!.EyeExpressionShapeScale(1f - pxrShape[(int)BlendShapeIndex.EyeBlink_L], EyeExpressions.EyeOpennessLeft);
        right->Openness = this.scaler!.EyeExpressionShapeScale(1f - pxrShape[(int)BlendShapeIndex.EyeBlink_R], EyeExpressions.EyeOpennessRight);

        left->Gaze.x = this.scaler!.EyeExpressionShapeScale(pxrShape[(int)BlendShapeIndex.EyeLookIn_L] - pxrShape[(int)BlendShapeIndex.EyeLookOut_L], EyeExpressions.EyeXGazeLeft);
        left->Gaze.y = this.scaler!.EyeExpressionShapeScale(pxrShape[(int)BlendShapeIndex.EyeLookUp_L] - pxrShape[(int)BlendShapeIndex.EyeLookDown_L], EyeExpressions.EyeYGazeLeft);

        right->Gaze.x = this.scaler!.EyeExpressionShapeScale(pxrShape[(int)BlendShapeIndex.EyeLookOut_R] - pxrShape[(int)BlendShapeIndex.EyeLookIn_R], EyeExpressions.EyeXGazeRight);
        right->Gaze.y = this.scaler!.EyeExpressionShapeScale(pxrShape[(int)BlendShapeIndex.EyeLookUp_R] - pxrShape[(int)BlendShapeIndex.EyeLookDown_R], EyeExpressions.EyeXGazeLeft);
    }

    private unsafe void UpdateEyeExpression(float* pxrShape, UnifiedExpressionShape* unifiedShape)
    {
        #region Brow Shapes
        unifiedShape[(int)UnifiedExpressions.BrowInnerUpLeft].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.BrowInnerUp], UnifiedExpressions.BrowInnerUpLeft);
        unifiedShape[(int)UnifiedExpressions.BrowInnerUpRight].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.BrowInnerUp], UnifiedExpressions.BrowInnerUpRight);
        unifiedShape[(int)UnifiedExpressions.BrowOuterUpLeft].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.BrowOuterUp_L], UnifiedExpressions.BrowOuterUpLeft);
        unifiedShape[(int)UnifiedExpressions.BrowOuterUpRight].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.BrowOuterUp_R], UnifiedExpressions.BrowOuterUpRight);
        unifiedShape[(int)UnifiedExpressions.BrowLowererLeft].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.BrowDown_L], UnifiedExpressions.BrowLowererLeft);
        unifiedShape[(int)UnifiedExpressions.BrowPinchLeft].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.BrowDown_L], UnifiedExpressions.BrowPinchLeft);
        unifiedShape[(int)UnifiedExpressions.BrowLowererRight].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.BrowDown_R], UnifiedExpressions.BrowLowererRight);
        unifiedShape[(int)UnifiedExpressions.BrowPinchRight].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.BrowDown_R], UnifiedExpressions.BrowPinchRight);
        #endregion
        #region Eye Shapes
        unifiedShape[(int)UnifiedExpressions.EyeSquintLeft].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.EyeSquint_L], UnifiedExpressions.EyeSquintLeft);
        unifiedShape[(int)UnifiedExpressions.EyeSquintRight].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.EyeSquint_R], UnifiedExpressions.EyeSquintRight);
        unifiedShape[(int)UnifiedExpressions.EyeWideLeft].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.EyeWide_L], UnifiedExpressions.EyeWideLeft);
        unifiedShape[(int)UnifiedExpressions.EyeWideRight].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.EyeWide_R], UnifiedExpressions.EyeWideRight);
        #endregion
    }

    private unsafe void UpdateExpression(float* pxrShape, UnifiedExpressionShape* unifiedShape)
    {
        // TODO: Map Viseme shapes onto face shapes.

        #region Jaw
        unifiedShape[(int)UnifiedExpressions.JawOpen].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.JawOpen], UnifiedExpressions.JawOpen);
        unifiedShape[(int)UnifiedExpressions.JawLeft].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.JawLeft], UnifiedExpressions.JawLeft);
        unifiedShape[(int)UnifiedExpressions.JawRight].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.JawRight], UnifiedExpressions.JawRight);
        unifiedShape[(int)UnifiedExpressions.JawForward].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.JawForward], UnifiedExpressions.JawForward);
        unifiedShape[(int)UnifiedExpressions.MouthClosed].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthClose], UnifiedExpressions.MouthClosed);
        #endregion
        #region Cheek
        unifiedShape[(int)UnifiedExpressions.CheekPuffLeft].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.CheekPuff], UnifiedExpressions.CheekPuffLeft);
        unifiedShape[(int)UnifiedExpressions.CheekPuffRight].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.CheekPuff], UnifiedExpressions.CheekPuffRight);
        unifiedShape[(int)UnifiedExpressions.CheekSquintLeft].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.CheekSquint_L], UnifiedExpressions.CheekSquintLeft);
        unifiedShape[(int)UnifiedExpressions.CheekSquintRight].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.CheekSquint_R], UnifiedExpressions.CheekSquintRight);
        #endregion
        #region Nose
        unifiedShape[(int)UnifiedExpressions.NoseSneerLeft].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.NoseSneer_L], UnifiedExpressions.NoseSneerLeft);
        unifiedShape[(int)UnifiedExpressions.NoseSneerRight].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.NoseSneer_R], UnifiedExpressions.NoseSneerRight);
        #endregion
        #region Mouth
        unifiedShape[(int)UnifiedExpressions.MouthUpperUpLeft].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthUpperUp_L], UnifiedExpressions.MouthUpperUpLeft);
        unifiedShape[(int)UnifiedExpressions.MouthUpperUpRight].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthUpperUp_R], UnifiedExpressions.MouthUpperUpRight);
        unifiedShape[(int)UnifiedExpressions.MouthLowerDownLeft].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthLowerDown_L], UnifiedExpressions.MouthLowerDownLeft);
        unifiedShape[(int)UnifiedExpressions.MouthLowerDownRight].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthLowerDown_R], UnifiedExpressions.MouthLowerDownRight);
        unifiedShape[(int)UnifiedExpressions.MouthFrownLeft].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthFrown_L], UnifiedExpressions.MouthFrownLeft);
        unifiedShape[(int)UnifiedExpressions.MouthFrownRight].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthFrown_R], UnifiedExpressions.MouthFrownRight);
        unifiedShape[(int)UnifiedExpressions.MouthDimpleLeft].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthDimple_L], UnifiedExpressions.MouthDimpleLeft);
        unifiedShape[(int)UnifiedExpressions.MouthDimpleRight].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthDimple_R], UnifiedExpressions.MouthDimpleRight);
        unifiedShape[(int)UnifiedExpressions.MouthUpperLeft].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthLeft], UnifiedExpressions.MouthUpperLeft);
        unifiedShape[(int)UnifiedExpressions.MouthLowerLeft].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthLeft], UnifiedExpressions.MouthLowerLeft);
        unifiedShape[(int)UnifiedExpressions.MouthUpperRight].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthRight], UnifiedExpressions.MouthUpperRight);
        unifiedShape[(int)UnifiedExpressions.MouthLowerRight].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthRight], UnifiedExpressions.MouthLowerRight);
        unifiedShape[(int)UnifiedExpressions.MouthPressLeft].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthPress_L], UnifiedExpressions.MouthPressLeft);
        unifiedShape[(int)UnifiedExpressions.MouthPressRight].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthPress_R], UnifiedExpressions.MouthPressRight);
        unifiedShape[(int)UnifiedExpressions.MouthRaiserLower].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthShrugLower], UnifiedExpressions.MouthRaiserLower);
        unifiedShape[(int)UnifiedExpressions.MouthRaiserUpper].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthShrugUpper], UnifiedExpressions.MouthRaiserUpper);
        unifiedShape[(int)UnifiedExpressions.MouthCornerPullLeft].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthSmile_L], UnifiedExpressions.MouthCornerPullLeft);
        unifiedShape[(int)UnifiedExpressions.MouthCornerSlantLeft].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthSmile_L], UnifiedExpressions.MouthCornerSlantLeft);
        unifiedShape[(int)UnifiedExpressions.MouthCornerPullRight].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthSmile_R], UnifiedExpressions.MouthCornerPullRight);
        unifiedShape[(int)UnifiedExpressions.MouthCornerSlantRight].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthSmile_R], UnifiedExpressions.MouthCornerSlantRight);
        unifiedShape[(int)UnifiedExpressions.MouthStretchLeft].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthStretch_L], UnifiedExpressions.MouthStretchLeft);
        unifiedShape[(int)UnifiedExpressions.MouthStretchRight].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthStretch_R], UnifiedExpressions.MouthStretchRight);
        #endregion
        #region Lip
        unifiedShape[(int)UnifiedExpressions.LipFunnelUpperLeft].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthFunnel], UnifiedExpressions.LipFunnelUpperLeft);
        unifiedShape[(int)UnifiedExpressions.LipFunnelUpperRight].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthFunnel], UnifiedExpressions.LipFunnelUpperRight);
        unifiedShape[(int)UnifiedExpressions.LipFunnelLowerLeft].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthFunnel], UnifiedExpressions.LipFunnelLowerLeft);
        unifiedShape[(int)UnifiedExpressions.LipFunnelLowerRight].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthFunnel], UnifiedExpressions.LipFunnelLowerRight);
        unifiedShape[(int)UnifiedExpressions.LipPuckerUpperLeft].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthPucker], UnifiedExpressions.LipPuckerUpperLeft);
        unifiedShape[(int)UnifiedExpressions.LipPuckerUpperRight].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthPucker], UnifiedExpressions.LipPuckerUpperRight);
        unifiedShape[(int)UnifiedExpressions.LipPuckerLowerLeft].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthPucker], UnifiedExpressions.LipPuckerLowerLeft);
        unifiedShape[(int)UnifiedExpressions.LipPuckerLowerRight].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthPucker], UnifiedExpressions.LipPuckerLowerRight);
        unifiedShape[(int)UnifiedExpressions.LipSuckUpperLeft].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthRollUpper], UnifiedExpressions.LipSuckUpperLeft);
        unifiedShape[(int)UnifiedExpressions.LipSuckUpperRight].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthRollUpper], UnifiedExpressions.LipSuckUpperRight);
        unifiedShape[(int)UnifiedExpressions.LipSuckLowerLeft].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthRollLower], UnifiedExpressions.LipSuckLowerLeft);
        unifiedShape[(int)UnifiedExpressions.LipSuckLowerRight].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthRollLower], UnifiedExpressions.LipSuckLowerRight);
        #endregion
        #region Tongue
        unifiedShape[(int)UnifiedExpressions.TongueOut].Weight = this.scaler!.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.TongueOut], UnifiedExpressions.TongueOut);
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