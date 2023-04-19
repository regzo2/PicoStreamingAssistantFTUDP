using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using static Pxr;

class MainApp
{
    const string IP_ADDRESS = "127.0.0.1";
    const int PORT_NUMBER = 29763; // Temporary port as of current Pico 4 SA app.

    static void Main(string[] args)
    {
        UdpClient udpClient = new UdpClient(PORT_NUMBER);
        IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(IP_ADDRESS), PORT_NUMBER);

        while (true)
        {
            try
            {
                byte[] receiveBytes = udpClient.Receive(ref endPoint);

                IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(PxrFTInfo)));
                Marshal.Copy(receiveBytes, 0, ptr, Marshal.SizeOf(typeof(PxrFTInfo)));
                PxrFTInfo data = (PxrFTInfo)Marshal.PtrToStructure(ptr, typeof(PxrFTInfo));

                Marshal.FreeHGlobal(ptr);

                Console.WriteLine("Received data: ");
                Console.WriteLine($"  timestamp: {data.timestamp}");
                Console.WriteLine("  blendShapeWeight:");
                for (int i = 0; i < BLEND_SHAPE_NUMS; i++)
                {
                    Console.WriteLine($"    {i}: {data.blendShapeWeight[i]}");
                }

                Console.WriteLine("  videoInputValid:");
                for (int i = 0; i < 10; i++)
                {
                    Console.WriteLine($"    {i}: {data.videoInputValid[i]}");
                }

                Console.WriteLine($"  laughingProb: {data.laughingProb}");
                Console.WriteLine("  emotionProb:");
                for (int i = 0; i < 10; i++)
                {
                    Console.WriteLine($"    {i}: {data.emotionProb[i]}");
                }

                Console.WriteLine("  reserved:");
                for (int i = 0; i < 128; i++)
                {
                    Console.WriteLine($"    {i}: {data.reserved[i]}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}