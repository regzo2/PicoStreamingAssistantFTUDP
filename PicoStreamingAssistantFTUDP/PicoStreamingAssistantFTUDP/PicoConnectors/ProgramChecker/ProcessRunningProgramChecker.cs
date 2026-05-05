using System.Diagnostics;

namespace Pico4SAFTExtTrackingModule.PicoConnectors.ProgramChecker;

public sealed class ProcessRunningProgramChecker : IProgramChecker
{
    /// <summary>
    /// Checks if the specified program is running on the machine
    /// </summary>
    /// <param name="program">Streaming Assistant, Business Streaming or PICO Connect</param>
    /// <returns>If the process related to the program is running, or not</returns>
    /// <exception cref="ArgumentException"></exception>
    public bool Check(PicoPrograms program)
    {
        string processName = program switch
        {
            PicoPrograms.StreamingAssistant => "Streaming Assistant",
            PicoPrograms.BusinessStreamingV1 => "Business StreamingUW",
            PicoPrograms.BusinessStreaming => "Business Streaming",
            PicoPrograms.PicoConnect => "PICO Connect",
            _ => throw new ArgumentException($"Unexpected program to check: {program}"),
        };
        return Process.GetProcessesByName(processName).Length > 0;
    }
}