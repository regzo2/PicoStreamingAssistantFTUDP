using System.IO.Abstractions;
using System.Text.Json;

using Microsoft.Extensions.Logging;

namespace Pico4SAFTExtTrackingModule.PicoConnectors.ConfigChecker.PicoConnect;

public sealed partial class PicoConnectConfigChecker(ILogger logger, IFileSystem fileSystem) : IConfigChecker
{
    public PicoConnectConfigChecker(ILogger logger) : this(logger, new FileSystem()) { }

    private static string GetProgramFsBasePath(PicoPrograms program) => program switch
    {
        PicoPrograms.BusinessStreaming => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Business Streaming"),
        PicoPrograms.PicoConnect => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PICO Connect"),
        _ => throw new NotSupportedException("PicoConnectConfigChecker class only checks for PICO Connect or Business Streaming 2.0 config files"),
    };

    private Config? GetConfig(PicoPrograms program)
    {
        string path = Path.Combine(GetProgramFsBasePath(program), "settings.json");
        LogConfigPath(path);
        try
        {
            string content = fileSystem.File.ReadAllText(path);
            return JsonSerializer.Deserialize<Config>(content);
        }
        catch (Exception ex)
        {
            LogErrorGetConfig(ex);
            return null;
        }
    }

    public int GetTransferProtocolNumber(PicoPrograms program)
    {
        if (program is not PicoPrograms.PicoConnect and not PicoPrograms.BusinessStreaming)
            throw new ArgumentException("PicoConnectConfigChecker class only checks for PICO Connect or Business Streaming 2.0 config files");

        //if (picoConfig == null || picoConfig!.lab == null)
        if (GetConfig(program) is not { Lab: not null } picoConfig)
        {
            LogErrorGetConfigFailed();
            return 0; // send default value
        }

        return picoConfig.Lab.FaceTrackingTransferProtocol;
    }

    [LoggerMessage(LogLevel.Information, "Expecting PICO settings file at '{path}'")]
    private partial void LogConfigPath(string path);

    [LoggerMessage(LogLevel.Error, "Pico Config deserialize failed.")]
    private partial void LogErrorGetConfig(Exception exception);

    [LoggerMessage(LogLevel.Error, "Couldn't get the value of `faceTrackingTransferProtocol` on the setting.json file")]
    private partial void LogErrorGetConfigFailed();
}