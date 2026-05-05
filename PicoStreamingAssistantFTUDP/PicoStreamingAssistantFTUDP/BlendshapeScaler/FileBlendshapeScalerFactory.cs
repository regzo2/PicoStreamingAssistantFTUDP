using System.IO.Abstractions;

using Microsoft.Extensions.Logging;

namespace Pico4SAFTExtTrackingModule.BlendshapeScaler;

public sealed partial class FileBlendshapeScalerFactory
{
    private static class ModuleConfigPath
    {
        public const string FILENAME = "PicoModuleConfig.json";
        public static string Directory => Path.GetDirectoryName(typeof(ModuleConfigPath).Assembly.Location) ?? AppContext.BaseDirectory;
        public static string FullPath => Path.Combine(Directory, FILENAME);
    }

    public IBlendshapeScaler Build(ILogger Logger)
    {
        FileBlendshapeScaler scaler = new(Logger, new FileSystem(), ModuleConfigPath.FullPath);
        ScalerLimiter scalerLimiter = new(scaler);

        if (!scaler.LoadConfigFile())
            LogWarningLoadConfigFile(Logger);

        return scalerLimiter;
    }

    [LoggerMessage(LogLevel.Warning, "Failed to load/create module config file.")]
    private static partial void LogWarningLoadConfigFile(ILogger Logger);
}