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
using Pico4SAFTExtTrackingModule.Diagnostics;

namespace Pico4SAFTExtTrackingModule;

public sealed class Pico4SAFTExtTrackingModule : ExtTrackingModule, IDisposable
{
    private bool disposedValue;
    private IPicoConnector? connector;
    private IBlendshapeScaler? scaler;
    private readonly Func<IPicoConnector?> connectorFactory;
    private readonly Func<DateTime> utcNow;
    private DateTime nextConnectorAttempt;
    private DateTime nextUpdateAttempt;
    private readonly PicoDiagnostics diagnostics;
    private ModuleState? lastStatus;
    private string? lastStreamer;
    private TimeSpan? updateFailureStarted;
    private long updateFailures;
    public (bool, bool) trackingState = (false, false);

    private const bool FILE_LOG = false;
    public static readonly string LOGGER_PATH = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VRCFaceTracking\\PICOLogs.csv");
    private PacketLogger<PxrFTInfo>? logger;

    public override (bool SupportsEye, bool SupportsExpression) Supported { get; } = (true, true);

    public Pico4SAFTExtTrackingModule() : this(null, null, null, null) { }

    public Pico4SAFTExtTrackingModule(IPicoConnector connector, IBlendshapeScaler scaler)
        : this(connector, scaler, null, null) { }

    internal Pico4SAFTExtTrackingModule(IPicoConnector? connector, IBlendshapeScaler? scaler,
        Func<IPicoConnector?>? connectorFactory, Func<DateTime>? utcNow)
    {
        this.connector = connector;
        this.scaler = scaler;
        this.utcNow = utcNow ?? (() => DateTime.UtcNow);
        this.diagnostics = new PicoDiagnostics(() => Logger, utcNow);
        this.connectorFactory = connectorFactory ?? (() => ConnectorFactory.build(diagnostics.Logger,
            new ProcessRunningProgramChecker(), new ConfigChecker(diagnostics.Logger)));
        this.logger = null;
        this.disposedValue = false;
    }

    private bool StreamerValidity()
    {
        this.connector = this.connectorFactory();
        if (this.connector == null)
        {
            diagnostics.Report(LogLevel.Information, "WaitingForStreamer", "", () =>
                "Streaming Assistant, Business Streaming, and PICO Connect were not found; probing again in 5000ms.");
            return false;
        }

        string streamer = this.connector.GetProcessName();
        if (streamer != lastStreamer)
        {
            diagnostics.Report(LogLevel.Information, "StreamerSelected", streamer, () =>
                $"Selected {streamer}; connector={this.connector.GetType().Name}.");
            lastStreamer = streamer;
        }
        return true;
    }

    private bool TryConnect()
    {
        if (this.utcNow() < this.nextConnectorAttempt)
        {
            Thread.Sleep(100);
            return false;
        }

        Exception? connectionError = null;
        bool streamerFound = false;
        diagnostics.BeginConnectionAttempt();
        try
        {
            if ((streamerFound = StreamerValidity()) && this.connector!.Connect())
            {
                // A successful bind must not delay recovery from a later receive failure.
                this.nextConnectorAttempt = default;
                diagnostics.Flush("ConnectionFailure");
                diagnostics.Flush("WaitingForStreamer");
                diagnostics.Report(LogLevel.Information, "ConnectorReady", "", () =>
                    "Connector ready; waiting for a valid face sample. Binding alone does not establish data reception.");
                return true;
            }
        }
        catch (Exception ex)
        {
            connectionError = ex;
        }

        if (connectionError != null)
            diagnostics.Report(LogLevel.Warning, "ConnectionFailure", "exception", () =>
                "Service discovery or connection threw; connector closed; retry in 5000ms.", connectionError);
        else if (streamerFound)
            diagnostics.Report(LogLevel.Warning, "ConnectionFailure", "not-ready", () =>
                "Connector not ready; retry service discovery/connection in 5000ms. See preceding configuration/bind details.");
        ResetConnector();
        this.nextConnectorAttempt = this.utcNow().AddSeconds(5);
        Thread.Sleep(100);
        return false;
    }

    private void ResetConnector()
    {
        var previousConnector = this.connector;
        this.connector = null;
        try
        {
            previousConnector?.Teardown();
        }
        catch (Exception teardownError)
        {
            diagnostics.Report(LogLevel.Warning, "ConnectorCleanupFailed", "", () =>
                "Could not close the PICO connector; recovery will continue.", teardownError);
        }
    }

    private unsafe bool TryGetBlendShapes(out float* shapes)
    {
        try
        {
            shapes = this.connector!.GetBlendShapes();
            return true;
        }
        catch (Exception ex)
        {
            shapes = null;
            BackOffUpdate(ex, "receive");
            return false;
        }
    }

    private void BackOffUpdate(Exception ex, string stage, string operation = "read")
    {
        bool rebuild = stage == "receive" && ex is SocketException or ObjectDisposedException;
        if (!rebuild) this.nextUpdateAttempt = this.utcNow().AddSeconds(1);
        updateFailureStarted ??= diagnostics.Elapsed;
        updateFailures++;
        diagnostics.PauseReception();
        diagnostics.Report(stage == "processing" ? LogLevel.Error : LogLevel.Warning,
            "UpdateFailure/" + stage, operation, () =>
                $"stage={stage}; operation={operation}; socketError={(ex is SocketException socket ? socket.ErrorCode.ToString() : "none")}; " +
                $"{(rebuild ? "rebuild local UDP listener" : "retain connector")}; update backoff=1000ms.", ex);
        if (rebuild)
        {
            // Record the original failure before cleanup, which may itself fail.
            ResetConnector();
            this.nextUpdateAttempt = this.utcNow().AddSeconds(1);
        }
    }

    public override (bool eyeSuccess, bool expressionSuccess) Initialize(bool eyeAvailable, bool expressionAvailable)
    {
        try { return InitializeTracking(eyeAvailable, expressionAvailable); }
        catch (Exception ex)
        {
            diagnostics.Report(LogLevel.Error, "InitializationFailed", "", () =>
                "PICO module initialization failed; the host will skip this module.", ex);
            throw;
        }
    }

    private (bool eyeSuccess, bool expressionSuccess) InitializeTracking(bool eyeAvailable, bool expressionAvailable)
    {
        trackingState = (eyeAvailable, expressionAvailable);
        if (!eyeAvailable && !expressionAvailable)
        {
            diagnostics.Logger.LogWarning("Neither eye nor expression tracking is available; skipping initialization.");
            return (false, false);
        }

        diagnostics.Logger.LogInformation("PICO module loaded; version={Version}; eye={Eye}; expression={Expression}; " +
            "repeatWindow=30s; CSV={Csv}. Streaming service will be detected in the background.",
            typeof(Pico4SAFTExtTrackingModule).Assembly.GetName().Version, eyeAvailable, expressionAvailable, FILE_LOG);

        this.scaler ??= new FileBlendshapeScalerFactory().build(diagnostics.Logger);

        if (FILE_LOG)
        {
            this.logger = PicoDataLoggerFactory.build(LOGGER_PATH);
            diagnostics.Logger.LogInformation("Using {Path} for PICO CSV logs.", LOGGER_PATH);
        }

        ModuleInformation.Name = "Pico 4 Pro / Enterprise";

        var stream = typeof(Pico4SAFTExtTrackingModule).Assembly.GetManifestResourceStream("Pico4SAFTExtTrackingModule.Assets.pico-hmd.png");
        ModuleInformation.StaticImages = stream is not null ? new List<Stream> { stream } : ModuleInformation.StaticImages;

        if (!trackingState.Item1)
            diagnostics.Logger.LogInformation("Eye tracking already in use, disabling eye data.");
        if (!trackingState.Item2) 
            diagnostics.Logger.LogInformation("Expression tracking already in use, disabling expression data.");

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
        right->Gaze.y = this.scaler!.EyeExpressionShapeScale(pxrShape[(int)BlendShapeIndex.EyeLookUp_R] - pxrShape[(int)BlendShapeIndex.EyeLookDown_R], EyeExpressions.EyeYGazeRight);
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
        diagnostics.Tick();
        if (lastStatus != Status)
        {
            diagnostics.Report(LogLevel.Information, "ModuleState", Status.ToString(), () =>
                $"Module state changed to {Status}; {(Status == ModuleState.Active ? "updates enabled" : "reception deliberately paused")}.");
            lastStatus = Status;
        }
        if (Status != ModuleState.Active)
        {
            diagnostics.PauseReception();
            Thread.Sleep(100);
            return;
        }

        if (this.utcNow() < this.nextUpdateAttempt)
        {
            // The host calls Update continuously, so returning immediately would spin a CPU core.
            Thread.Sleep(100);
            return;
        }

        if (this.connector == null && !TryConnect())
            return;

        diagnostics.ResumeReception();

        unsafe
        {
            if (!TryGetBlendShapes(out float* pxrShape) || pxrShape == null)
                return;

            string operation = "processing";
            try
            {
                if (this.logger != null)
                {
                    operation = "csv";
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
                            operation = "eye-gaze/openness";
                            UpdateEye(pxrShape, pLeft, pRight);
                            operation = "eye-expressions";
                            UpdateEyeExpression(pxrShape, unifiedShape);
                        }
                    }

                    if (trackingState.Item2)
                    {
                        operation = "mouth-expressions";
                        UpdateExpression(pxrShape, unifiedShape);
                    }
                }
                if (updateFailureStarted is { } failedAt)
                {
                    diagnostics.Flush("UpdateFailure/");
                    diagnostics.Report(LogLevel.Information, "ProcessingResumed", "", () =>
                        $"Tracking processing resumed after {(diagnostics.Elapsed - failedAt).TotalSeconds:F3}s; " +
                        $"update failures={updateFailures}.");
                    updateFailureStarted = null;
                    updateFailures = 0;
                }
            }
            catch (Exception ex)
            {
                // Mapping and optional logging failures do not invalidate the UDP listener.
                BackOffUpdate(ex, "processing", operation);
            }
        }
    }

    public override void Teardown() => Dispose();

    private void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                try
                {
                    diagnostics.PauseReception();
                    diagnostics.Report(LogLevel.Information, "ModuleShutdown", "", () =>
                        $"Shutting down PICO module; unresolved update failures={updateFailures}.");
                    ResetConnector();
                    this.logger?.Dispose();
                    this.logger = null;
                }
                finally
                {
                    diagnostics.Flush(forget: true);
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
