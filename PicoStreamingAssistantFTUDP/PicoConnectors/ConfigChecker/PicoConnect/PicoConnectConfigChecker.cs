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

        if (picoConfig.Value == null) return 0; // couldn't get; send default value (0)
        return picoConfig!.Value.lab.faceTrackingTransferProtocol;
    }
}