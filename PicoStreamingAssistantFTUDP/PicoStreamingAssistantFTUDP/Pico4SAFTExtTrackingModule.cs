using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;

using Pico4SAFTExtTrackingModule.BlendshapeScaler;
using Pico4SAFTExtTrackingModule.PacketLogger;
using Pico4SAFTExtTrackingModule.PicoConnectors;
using Pico4SAFTExtTrackingModule.PicoConnectors.ConfigChecker;
using Pico4SAFTExtTrackingModule.PicoConnectors.ProgramChecker;

using VRCFaceTracking;
using VRCFaceTracking.Core.Library;
using VRCFaceTracking.Core.Params.Data;
using VRCFaceTracking.Core.Params.Expressions;

namespace Pico4SAFTExtTrackingModule;

public sealed partial class Pico4SAFTExtTrackingModule(IPicoConnector? connector, IBlendshapeScaler? scaler) : ExtTrackingModule, IDisposable
{

    private bool _disposedValue = false;
    private IPicoConnector? _connector = connector;
    private IBlendshapeScaler? _scaler = scaler;
    private PacketLogger<PxrFTInfo>? _logger = null;

    public static readonly string LoggerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VRCFaceTracking\\PICOLogs.csv");
    public (bool Eye, bool Expression) TrackingState = (false, false);

    public override (bool SupportsEye, bool SupportsExpression) Supported { get; } = (true, true);

    public Pico4SAFTExtTrackingModule() : this(null, null)
    {
    }

    [MemberNotNullWhen(true, nameof(_connector))]
    private bool StreamerValidity()
    {
        _connector = ConnectorFactory.Build(Logger, new ProcessRunningProgramChecker(), new ConfigChecker(Logger));
        if (_connector is null)
        {
            LogErrorNoProcess();
            return false;
        }

        LogProcessName(_connector.GetProcessName());
        return true;
    }

    public override (bool eyeSuccess, bool expressionSuccess) Initialize(bool eyeAvailable, bool expressionAvailable)
    {
        TrackingState = (eyeAvailable, expressionAvailable);
        if (!StreamerValidity() || (eyeAvailable, expressionAvailable) is (false, false))
        {
            LogWarningNoData();
            return (false, false);
        }

        LogInitializing(_connector.GetProcessName());
        /*while (!this.disposedValue && !*/
        _connector.Connect()/*) Thread.Sleep(4_000)*/;

        if (_disposedValue)
        {
            LogWarningNoConnection();
            return (false, false);
        }

        _scaler = new FileBlendshapeScalerFactory().Build(Logger);

#if FILE_LOG
        _logger = PicoDataLoggerFactory.Build(LoggerPath);
        LogFileLogPath(LoggerPath);
#endif


        ModuleInformation.Name = "Pico 4 Pro / Enterprise";

        if (typeof(Pico4SAFTExtTrackingModule).Assembly.GetManifestResourceStream("pico-hmd.png") is { } stream)
        {
            if (ModuleInformation.StaticImages is null)
                ModuleInformation.StaticImages = [stream];
            else
                ModuleInformation.StaticImages.Add(stream);
        }

        if (!TrackingState.Eye)
            LogEyeReady();
        if (!TrackingState.Expression)
            LogExpressionReady();

        return TrackingState;
    }

    private void UpdateEye(ReadOnlySpan<float> pxrShape, ref UnifiedSingleEyeData left, ref UnifiedSingleEyeData right)
    {
        Debug.Assert(_scaler is not null);

        // to be tested, not entirely sure how Pxr blink/squint will translate to Openness.
        left.Openness = _scaler.EyeExpressionShapeScale(1f - pxrShape[(int)BlendShapeIndex.EyeBlink_L], EyeExpressions.EyeOpennessLeft);
        right.Openness = _scaler.EyeExpressionShapeScale(1f - pxrShape[(int)BlendShapeIndex.EyeBlink_R], EyeExpressions.EyeOpennessRight);

        left.Gaze.x = _scaler.EyeExpressionShapeScale(pxrShape[(int)BlendShapeIndex.EyeLookIn_L] - pxrShape[(int)BlendShapeIndex.EyeLookOut_L], EyeExpressions.EyeXGazeLeft);
        left.Gaze.y = _scaler.EyeExpressionShapeScale(pxrShape[(int)BlendShapeIndex.EyeLookUp_L] - pxrShape[(int)BlendShapeIndex.EyeLookDown_L], EyeExpressions.EyeYGazeLeft);

        right.Gaze.x = _scaler.EyeExpressionShapeScale(pxrShape[(int)BlendShapeIndex.EyeLookOut_R] - pxrShape[(int)BlendShapeIndex.EyeLookIn_R], EyeExpressions.EyeXGazeRight);
        right.Gaze.y = _scaler.EyeExpressionShapeScale(pxrShape[(int)BlendShapeIndex.EyeLookUp_R] - pxrShape[(int)BlendShapeIndex.EyeLookDown_R], EyeExpressions.EyeXGazeLeft);
    }

    private void UpdateEyeExpression(ReadOnlySpan<float> pxrShape, Span<UnifiedExpressionShape> unifiedShape)
    {
        Debug.Assert(_scaler is not null);

        #region Brow Shapes
        unifiedShape[(int)UnifiedExpressions.BrowInnerUpLeft].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.BrowInnerUp], UnifiedExpressions.BrowInnerUpLeft);
        unifiedShape[(int)UnifiedExpressions.BrowInnerUpRight].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.BrowInnerUp], UnifiedExpressions.BrowInnerUpRight);
        unifiedShape[(int)UnifiedExpressions.BrowOuterUpLeft].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.BrowOuterUp_L], UnifiedExpressions.BrowOuterUpLeft);
        unifiedShape[(int)UnifiedExpressions.BrowOuterUpRight].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.BrowOuterUp_R], UnifiedExpressions.BrowOuterUpRight);
        unifiedShape[(int)UnifiedExpressions.BrowLowererLeft].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.BrowDown_L], UnifiedExpressions.BrowLowererLeft);
        unifiedShape[(int)UnifiedExpressions.BrowPinchLeft].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.BrowDown_L], UnifiedExpressions.BrowPinchLeft);
        unifiedShape[(int)UnifiedExpressions.BrowLowererRight].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.BrowDown_R], UnifiedExpressions.BrowLowererRight);
        unifiedShape[(int)UnifiedExpressions.BrowPinchRight].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.BrowDown_R], UnifiedExpressions.BrowPinchRight);
        #endregion
        #region Eye Shapes
        unifiedShape[(int)UnifiedExpressions.EyeSquintLeft].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.EyeSquint_L], UnifiedExpressions.EyeSquintLeft);
        unifiedShape[(int)UnifiedExpressions.EyeSquintRight].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.EyeSquint_R], UnifiedExpressions.EyeSquintRight);
        unifiedShape[(int)UnifiedExpressions.EyeWideLeft].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.EyeWide_L], UnifiedExpressions.EyeWideLeft);
        unifiedShape[(int)UnifiedExpressions.EyeWideRight].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.EyeWide_R], UnifiedExpressions.EyeWideRight);
        #endregion
    }

    private void UpdateExpression(ReadOnlySpan<float> pxrShape, Span<UnifiedExpressionShape> unifiedShape)
    {
        // TODO: Map Viseme shapes onto face shapes.
        Debug.Assert(_scaler is not null);

        #region Jaw
        unifiedShape[(int)UnifiedExpressions.JawOpen].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.JawOpen], UnifiedExpressions.JawOpen);
        unifiedShape[(int)UnifiedExpressions.JawLeft].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.JawLeft], UnifiedExpressions.JawLeft);
        unifiedShape[(int)UnifiedExpressions.JawRight].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.JawRight], UnifiedExpressions.JawRight);
        unifiedShape[(int)UnifiedExpressions.JawForward].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.JawForward], UnifiedExpressions.JawForward);
        unifiedShape[(int)UnifiedExpressions.MouthClosed].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthClose], UnifiedExpressions.MouthClosed);
        #endregion
        #region Cheek
        unifiedShape[(int)UnifiedExpressions.CheekPuffLeft].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.CheekPuff], UnifiedExpressions.CheekPuffLeft);
        unifiedShape[(int)UnifiedExpressions.CheekPuffRight].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.CheekPuff], UnifiedExpressions.CheekPuffRight);
        unifiedShape[(int)UnifiedExpressions.CheekSquintLeft].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.CheekSquint_L], UnifiedExpressions.CheekSquintLeft);
        unifiedShape[(int)UnifiedExpressions.CheekSquintRight].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.CheekSquint_R], UnifiedExpressions.CheekSquintRight);
        #endregion
        #region Nose
        unifiedShape[(int)UnifiedExpressions.NoseSneerLeft].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.NoseSneer_L], UnifiedExpressions.NoseSneerLeft);
        unifiedShape[(int)UnifiedExpressions.NoseSneerRight].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.NoseSneer_R], UnifiedExpressions.NoseSneerRight);
        #endregion
        #region Mouth
        unifiedShape[(int)UnifiedExpressions.MouthUpperUpLeft].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthUpperUp_L], UnifiedExpressions.MouthUpperUpLeft);
        unifiedShape[(int)UnifiedExpressions.MouthUpperUpRight].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthUpperUp_R], UnifiedExpressions.MouthUpperUpRight);
        unifiedShape[(int)UnifiedExpressions.MouthLowerDownLeft].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthLowerDown_L], UnifiedExpressions.MouthLowerDownLeft);
        unifiedShape[(int)UnifiedExpressions.MouthLowerDownRight].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthLowerDown_R], UnifiedExpressions.MouthLowerDownRight);
        unifiedShape[(int)UnifiedExpressions.MouthFrownLeft].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthFrown_L], UnifiedExpressions.MouthFrownLeft);
        unifiedShape[(int)UnifiedExpressions.MouthFrownRight].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthFrown_R], UnifiedExpressions.MouthFrownRight);
        unifiedShape[(int)UnifiedExpressions.MouthDimpleLeft].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthDimple_L], UnifiedExpressions.MouthDimpleLeft);
        unifiedShape[(int)UnifiedExpressions.MouthDimpleRight].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthDimple_R], UnifiedExpressions.MouthDimpleRight);
        unifiedShape[(int)UnifiedExpressions.MouthUpperLeft].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthLeft], UnifiedExpressions.MouthUpperLeft);
        unifiedShape[(int)UnifiedExpressions.MouthLowerLeft].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthLeft], UnifiedExpressions.MouthLowerLeft);
        unifiedShape[(int)UnifiedExpressions.MouthUpperRight].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthRight], UnifiedExpressions.MouthUpperRight);
        unifiedShape[(int)UnifiedExpressions.MouthLowerRight].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthRight], UnifiedExpressions.MouthLowerRight);
        unifiedShape[(int)UnifiedExpressions.MouthPressLeft].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthPress_L], UnifiedExpressions.MouthPressLeft);
        unifiedShape[(int)UnifiedExpressions.MouthPressRight].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthPress_R], UnifiedExpressions.MouthPressRight);
        unifiedShape[(int)UnifiedExpressions.MouthRaiserLower].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthShrugLower], UnifiedExpressions.MouthRaiserLower);
        unifiedShape[(int)UnifiedExpressions.MouthRaiserUpper].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthShrugUpper], UnifiedExpressions.MouthRaiserUpper);
        unifiedShape[(int)UnifiedExpressions.MouthCornerPullLeft].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthSmile_L], UnifiedExpressions.MouthCornerPullLeft);
        unifiedShape[(int)UnifiedExpressions.MouthCornerSlantLeft].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthSmile_L], UnifiedExpressions.MouthCornerSlantLeft);
        unifiedShape[(int)UnifiedExpressions.MouthCornerPullRight].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthSmile_R], UnifiedExpressions.MouthCornerPullRight);
        unifiedShape[(int)UnifiedExpressions.MouthCornerSlantRight].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthSmile_R], UnifiedExpressions.MouthCornerSlantRight);
        unifiedShape[(int)UnifiedExpressions.MouthStretchLeft].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthStretch_L], UnifiedExpressions.MouthStretchLeft);
        unifiedShape[(int)UnifiedExpressions.MouthStretchRight].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthStretch_R], UnifiedExpressions.MouthStretchRight);
        #endregion
        #region Lip
        unifiedShape[(int)UnifiedExpressions.LipFunnelUpperLeft].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthFunnel], UnifiedExpressions.LipFunnelUpperLeft);
        unifiedShape[(int)UnifiedExpressions.LipFunnelUpperRight].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthFunnel], UnifiedExpressions.LipFunnelUpperRight);
        unifiedShape[(int)UnifiedExpressions.LipFunnelLowerLeft].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthFunnel], UnifiedExpressions.LipFunnelLowerLeft);
        unifiedShape[(int)UnifiedExpressions.LipFunnelLowerRight].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthFunnel], UnifiedExpressions.LipFunnelLowerRight);
        unifiedShape[(int)UnifiedExpressions.LipPuckerUpperLeft].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthPucker], UnifiedExpressions.LipPuckerUpperLeft);
        unifiedShape[(int)UnifiedExpressions.LipPuckerUpperRight].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthPucker], UnifiedExpressions.LipPuckerUpperRight);
        unifiedShape[(int)UnifiedExpressions.LipPuckerLowerLeft].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthPucker], UnifiedExpressions.LipPuckerLowerLeft);
        unifiedShape[(int)UnifiedExpressions.LipPuckerLowerRight].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthPucker], UnifiedExpressions.LipPuckerLowerRight);
        unifiedShape[(int)UnifiedExpressions.LipSuckUpperLeft].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthRollUpper], UnifiedExpressions.LipSuckUpperLeft);
        unifiedShape[(int)UnifiedExpressions.LipSuckUpperRight].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthRollUpper], UnifiedExpressions.LipSuckUpperRight);
        unifiedShape[(int)UnifiedExpressions.LipSuckLowerLeft].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthRollLower], UnifiedExpressions.LipSuckLowerLeft);
        unifiedShape[(int)UnifiedExpressions.LipSuckLowerRight].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.MouthRollLower], UnifiedExpressions.LipSuckLowerRight);
        #endregion
        #region Tongue
        unifiedShape[(int)UnifiedExpressions.TongueOut].Weight = _scaler.UnifiedExpressionShapeScale(pxrShape[(int)BlendShapeIndex.TongueOut], UnifiedExpressions.TongueOut);
        #endregion
    }

    public override void Update()
    {
        Debug.Assert(_connector is not null);
        if (Status != ModuleState.Active)
        {
            Thread.Sleep(100);
            return;
        }

        try
        {
            ReadOnlySpan<float> pxrShape = _connector.GetBlendShapes();
            if (pxrShape.IsEmpty)
                return;

            if (_logger != null)
            {
                // legacy; PacketLogger#UpdateValue needs a PxrFTInfo; but we don't want to send that outside from the PicoConnector
                PxrFTInfo data = PicoDataLoggerHelper.FillPxrFTInfo(pxrShape);
                _logger.UpdateValue(data);
            }

            Span<UnifiedExpressionShape> unifiedShape = UnifiedTracking.Data.Shapes;

            if (TrackingState.Eye)
            {
                ref var pLeft = ref UnifiedTracking.Data.Eye.Left;
                ref var pRight = ref UnifiedTracking.Data.Eye.Right;
                UpdateEye(pxrShape, ref pLeft, ref pRight);
                UpdateEyeExpression(pxrShape, unifiedShape);
            }

            if (TrackingState.Expression)
                UpdateExpression(pxrShape, unifiedShape);
        }
        catch (Exception ex)
        {
            LogException(ex);
        }
    }

    public override void Teardown() => Dispose();

    private void Dispose(bool disposing)
    {
        if (_disposedValue)
            return;

        if (disposing)
        {
            _connector?.Teardown();
            _connector = null;
            _logger?.Dispose();
            _logger = null;
        }

        _disposedValue = true;
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

    [LoggerMessage(LogLevel.Information, "Using {process}")]
    private partial void LogProcessName(string process);

    [LoggerMessage(LogLevel.Information, "Initializing {process} data stream.")]
    private partial void LogInitializing(string process);

    [LoggerMessage(LogLevel.Information, "Using {path} path for PICO logs.")]
    private partial void LogFileLogPath(string path);

    [LoggerMessage(LogLevel.Information, "Eye tracking already in use, disabling eye data.")]
    private partial void LogEyeReady();

    [LoggerMessage(LogLevel.Information, "Expression Tracking already in use, disabling expression data.")]
    private partial void LogExpressionReady();

    [LoggerMessage(LogLevel.Warning, "No data is usable, skipping initialization.")]
    private partial void LogWarningNoData();

    [LoggerMessage(LogLevel.Warning, "Module failed to establish a connection.")]
    private partial void LogWarningNoConnection();

    [LoggerMessage(LogLevel.Warning, "Unexpected exceptions")]
    private partial void LogException(Exception exception);

    [LoggerMessage(
        LogLevel.Error,
        "\"Streaming Assistant\", \"Business Streaming\" or \"PICO Connect\" process was not found. " +
        "Please run the Streaming Assistant or PICO Connect before VRCFaceTracking.")]
    private partial void LogErrorNoProcess();
}