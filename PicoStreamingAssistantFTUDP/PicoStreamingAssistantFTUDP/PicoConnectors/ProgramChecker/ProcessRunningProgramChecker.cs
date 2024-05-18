using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pico4SAFTExtTrackingModule.PicoConnectors.ProgramChecker;

public sealed class ProcessRunningProgramChecker : IProgramChecker
{
    /**
     * Checks if the specified program is running on the machine
     * @param program "Streaming Assistant, Business Streaming or PICO Connect
     * @return If the process related to the program is running, or not
     **/
    public bool Check(PicoPrograms program)
    {
        string processName;
        switch (program)
        {
            case PicoPrograms.StreamingAssistant:
                processName = "Streaming Assistant";
                break;

            case PicoPrograms.BusinessStreaming:
                processName = "Business StreamingUW";
                break;

            case PicoPrograms.PicoConnect:
                processName = "PICO Connect";
                break;

            default:
                throw new ArgumentException("Unexpected program to check: " + program.ToString());
        }

        return Process.GetProcessesByName(processName).Length > 0;
    }
}