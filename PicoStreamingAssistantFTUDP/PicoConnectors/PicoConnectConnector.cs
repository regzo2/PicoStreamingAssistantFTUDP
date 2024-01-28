using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Pico4SAFTExtTrackingModule.PicoConnectors;

/**
 * Connector class for PICO Connect
 **/
public sealed class PicoConnectConnector : PicoConnector
{
    private ILogger Logger;

    public PicoConnectConnector(ILogger Logger)
    {
        this.Logger = Logger;
    }

    public bool Connect()
    {
        Logger.LogInformation("Active Connections");
        Logger.LogInformation("");

        Logger.LogInformation(" Proto Local Address Foreign Address State PID");
        foreach (TcpRow tcpRow in ManagedIpHelper.GetExtendedTcpTable(true))
        {
            Logger.LogInformation(" {0,-7}{1,-23}{2, -23}{3,-14}{4}", "TCP", tcpRow.LocalEndPoint, tcpRow.RemoteEndPoint, tcpRow.State, tcpRow.ProcessId);

            Process process = Process.GetProcessById(tcpRow.ProcessId);
            if (process.ProcessName != "System")
            {
                foreach (ProcessModule processModule in process.Modules)
                {
                    Logger.LogInformation(" {0}", processModule.FileName);
                }

                Logger.LogInformation(" [{0}]", Path.GetFileName(process.MainModule.FileName));
            }
            else
            {
                Logger.LogInformation(" -- unknown component(s) --");
                Logger.LogInformation(" [{0}]", "System");
            }

            Logger.LogInformation("");
        }
        return false;
    }

    public unsafe float* GetBlendShapes()
    {
        return null;
    }

    public string GetProcessName()
    {
        return "PICO Connect";
    }

    void PicoConnector.Teardown()
    {
        
    }
}
