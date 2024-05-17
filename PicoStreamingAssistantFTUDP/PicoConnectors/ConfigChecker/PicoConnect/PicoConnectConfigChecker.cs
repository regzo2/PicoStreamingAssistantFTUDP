using Microsoft.Extensions.Logging;
using System.IO.Abstractions;
using Newtonsoft.Json.Linq;

namespace Pico4SAFTExtTrackingModule.PicoConnectors.ConfigChecker.PicoConnect;

public sealed class PicoConnectConfigChecker : IConfigChecker
{
    private readonly ILogger logger;
    private readonly IFileSystem fileSystem;

    public PicoConnectConfigChecker(ILogger logger, IFileSystem fileSystem)
    {
        this.logger = logger;
        this.fileSystem = fileSystem;
    }

    public PicoConnectConfigChecker(ILogger logger) : this(logger, new FileSystem()) { }
    public int GetTransferProtocolNumber(PicoPrograms program)
    {
        string configLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PICO Connect\\settings.json");

        if (program != PicoPrograms.PicoConnect) throw new ArgumentException("PicoConnectConfigChecker class only checks for PICO Connect config files");

        try
        {
            JObject config = JObject.Parse(fileSystem.File.ReadAllText(configLocation));
            JObject lab = (JObject)config["lab"];
            int protocolNumber = (int)lab["faceTrackingTransferProtocol"];
            return protocolNumber;
        }
        catch (Exception ex)
        {
            logger.LogError("Get pico transfer protocol number failed: " + ex);
            return 0;
        }
    }
}