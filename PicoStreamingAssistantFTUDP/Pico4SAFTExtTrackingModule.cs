using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using VRCFaceTracking;
using VRCFaceTracking.Core.Params.Data;
using VRCFaceTracking.Core.Params.Expressions;
using VRCFaceTracking.Core.Types;
using VRCFaceTracking.Core.Library;
using static Pxr;

namespace Pico4SAFTExtTrackingModule;

public class Pico4SAFTExtTrackingModule : ExtTrackingModule
{
    const string IP_ADDRESS = "127.0.0.1";
    const int PORT_NUMBER = 29763; // Temporary port as of current Pico 4 SA app.

    private static UdpClient? udpClient;
    private static IPEndPoint? endPoint;

    private static byte[] receiveBytes = new byte[4096];
    private static PxrFTInfo data = new PxrFTInfo();

    private static readonly bool hasHeader = false;
    private static readonly int pxrHeaderSize = Marshal.SizeOf(typeof(UInt16));
    private static readonly int pxrFtInfoSize = Marshal.SizeOf(typeof(PxrFTInfo));

    public override (bool SupportsEye, bool SupportsExpression) Supported => (true, true);

    public override (bool eyeSuccess, bool expressionSuccess) Initialize(bool eyeAvailable, bool expressionAvailable)
    {
        ModuleInformation.Name = "Pico 4 Pro / Enterprise Streaming Assistant";

        var stream = GetType().Assembly.GetManifestResourceStream("Pico4SAFTExtTrackingModule.Assets.pico-hmd.png");
        ModuleInformation.StaticImages = stream != null ? new List<Stream> { stream } : ModuleInformation.StaticImages;

        Logger.LogInformation("Initializing UDP client. Accepting on port:" + PORT_NUMBER);
        try
        {
            udpClient = new UdpClient(PORT_NUMBER);
            endPoint = new IPEndPoint(IPAddress.Parse(IP_ADDRESS), PORT_NUMBER);
            Logger.LogInformation("Client success! Receiving PxrFTInfo.");
        }
        catch (Exception)
        {
            Logger.LogInformation("Client failed to create UDP instance.");
            return (false, false);
        }

        return (true, true);
    }

    private static int GetPacketSize() => 
        hasHeader ? pxrHeaderSize + pxrFtInfoSize : pxrFtInfoSize;
    private static int GetPacketIndex() =>
        hasHeader ? pxrHeaderSize : 0;

    private void RetrieveStreamData()
    {
        try
        {
            receiveBytes = udpClient?.Receive(ref endPoint);

            IntPtr ptr = Marshal.AllocHGlobal(GetPacketSize());
            Marshal.Copy(receiveBytes, GetPacketIndex(), ptr, GetPacketSize());
            data = (PxrFTInfo)Marshal.PtrToStructure(ptr, typeof(PxrFTInfo));

            Marshal.FreeHGlobal(ptr);
        }
        catch (Exception e)
        {
            Logger.LogInformation(e.ToString());
        }
    }

    private static void UpdateEye(ref float[] pxrShape, ref UnifiedEyeData unifiedEye)
    {
        // to be tested, not entirely sure how Pxr blink/squint will translate to Openness.
        unifiedEye.Left.Openness = 1f - pxrShape[(int)BlendShapeIndex.EyeBlink_L];
        unifiedEye.Right.Openness = 1f - pxrShape[(int)BlendShapeIndex.EyeBlink_R];

        unifiedEye.Left.Gaze = new Vector2(
            pxrShape[(int)BlendShapeIndex.EyeLookIn_L] - pxrShape[(int)BlendShapeIndex.EyeLookOut_L],
            pxrShape[(int)BlendShapeIndex.EyeLookUp_L] - pxrShape[(int)BlendShapeIndex.EyeLookDown_L]);
        unifiedEye.Right.Gaze = new Vector2(
            pxrShape[(int)BlendShapeIndex.EyeLookOut_R] - pxrShape[(int)BlendShapeIndex.EyeLookIn_R],
            pxrShape[(int)BlendShapeIndex.EyeLookUp_R] - pxrShape[(int)BlendShapeIndex.EyeLookDown_R]);
    }

    private static void UpdateEyeExpression(ref float[] pxrShape, ref UnifiedExpressionShape[] unifiedShape)
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

    private static void UpdateExpression(ref float[] pxrShape, ref UnifiedExpressionShape[] unifiedShape)
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
        if (Status == ModuleState.Active)
        {
            RetrieveStreamData();
            UpdateEye(ref data.blendShapeWeight, ref UnifiedTracking.Data.Eye);
            UpdateEyeExpression(ref data.blendShapeWeight, ref UnifiedTracking.Data.Shapes);
            UpdateExpression(ref data.blendShapeWeight, ref UnifiedTracking.Data.Shapes);
        }
    }

    public override void Teardown()
    {
        Logger.LogInformation("Disposing of PxrFaceTracking UDP Client.");
        udpClient?.Dispose();
    }
}