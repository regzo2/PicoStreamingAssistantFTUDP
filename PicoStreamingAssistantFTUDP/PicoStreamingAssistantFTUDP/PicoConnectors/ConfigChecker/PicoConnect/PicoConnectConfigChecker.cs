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
        logger?.LogDebug("Reading PICO Connect settings from {ConfigPath}.", configLocation);
        try
        {
            string configContents = fileSystem.File.ReadAllText(configLocation);
            return JsonSerializer.Deserialize<Config>(configContents);
        }
        catch (JsonException ex)
        {
            logger?.LogError(ex, "PICO Connect settings JSON is invalid: {ConfigPath}.", configLocation);
            return null;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Could not read PICO Connect settings: {ConfigPath}.", configLocation);
            return null;
        }
    }

    public int GetTransferProtocolNumber(PicoPrograms program)
    {
        if (program != PicoPrograms.PicoConnect) throw new ArgumentException("PicoConnectConfigChecker class only checks for PICO Connect config files");

        if (picoConfig!.Value == null) return 0; // The read/parse failure was logged above.
        if (picoConfig.Value.lab == null)
        {
            logger.LogWarning("PICO Connect settings.json has no lab object; transfer protocol is unavailable.");
            return 0; // send default value
        }

        return picoConfig!.Value!.lab!.faceTrackingTransferProtocol;
    }
}
