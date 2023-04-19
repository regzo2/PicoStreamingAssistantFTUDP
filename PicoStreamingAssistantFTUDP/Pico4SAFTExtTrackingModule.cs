using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using VRCFaceTracking;
using static Pxr;
using static System.Runtime.InteropServices.JavaScript.JSType;

class Pico4SAFTExtTrackingModule : ExtTrackingModule
{
    const string IP_ADDRESS = "127.0.0.1";
    const int PORT_NUMBER = 29763; // Temporary port as of current Pico 4 SA app.

    private static UdpClient udpClient = new UdpClient(PORT_NUMBER);
    private static IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(IP_ADDRESS), PORT_NUMBER);
    private static byte[] receiveBytes = new byte[2048];
    private static PxrFTInfo data = new();

    public override (bool SupportsEye, bool SupportsExpression) Supported => (true, true);

    public override (bool eyeSuccess, bool expressionSuccess) Initialize(bool eyeAvailable, bool expressionAvailable)
    {
        udpClient = new UdpClient(PORT_NUMBER);
        endPoint = new IPEndPoint(IPAddress.Parse(IP_ADDRESS), PORT_NUMBER);

        return RetrieveStreamData(ref receiveBytes);
    }

    public static (bool, bool) RetrieveStreamData( ref byte[] bytes)
    {
        try
        {
            receiveBytes = udpClient.Receive(ref endPoint);

            IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(PxrFTInfo)));
            Marshal.Copy(receiveBytes, 0, ptr, Marshal.SizeOf(typeof(PxrFTInfo)));
            data = (PxrFTInfo)Marshal.PtrToStructure(ptr, typeof(PxrFTInfo));

            Marshal.FreeHGlobal(ptr);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }

        return (true, true);
    }

    public override void Update()
    {
        RetrieveStreamData(ref receiveBytes);

        Logger.LogInformation("JawOpen: ", data.blendShapeWeight[(int)BlendShapeIndex.JawOpen].ToString());
    }

    public override void Teardown()
    {
        udpClient.Dispose();
    }

}