using Microsoft.Extensions.Logging;
using System.IO.Abstractions;
using System.Text.Json;

namespace Pico4SAFTExtTrackingModule.PicoConnectors.ConfigChecker.PicoConnect;

public sealed class PicoConnectConfigChecker : IConfigChecker
{
    private readonly Lazy<Config> picoConfig;
    private readonly ILogger logger;

    public PicoConnectConfigChecker(ILogger logger, IFileSystem fileSystem)
    {
        this.logger = logger;
        this.picoConfig = new Lazy<Config>(() => GetConfig(fileSystem, logger));
    }

    public PicoConnectConfigChecker(ILogger logger) : this(logger, new FileSystem()) { }

    private static Config GetConfig(IFileSystem fileSystem, ILogger? logger = null)
    {
        string configLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PICO Connect\\settings.json");
        try
        {
            string configContents = fileSystem.File.ReadAllText(configLocation);
            return JsonSerializer.Deserialize<Config>(configContents);
        }
        catch (Exception ex)
        {
            logger?.LogError("Pico Connect Config deserialize failed: " + ex.ToString());
            return null;
        }
    }

    public int GetTransferProtocolNumber(PicoPrograms program)
    {
        if (program != PicoPrograms.PicoConnect) throw new ArgumentException("PicoConnectConfigChecker class only checks for PICO Connect config files");
        if (picoConfig == null) return 0; // couldn't get; send default value

        if (picoConfig!.Value == null || picoConfig!.Value.lab == null)
        {
            logger.LogError("Couldn't get the value of `faceTrackingTransferProtocol` on the setting.json file");
            return 0; // send default value
        }

        return picoConfig!.Value!.lab!.faceTrackingTransferProtocol;
    }
}