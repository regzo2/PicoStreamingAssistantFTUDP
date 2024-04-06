using Microsoft.Extensions.Logging;
using System.IO.Abstractions;
using System.Reflection;

namespace Pico4SAFTExtTrackingModule.BlendshapeScaler;

public class FileBlendshapeScalerFactory
{
    private static class ModuleConfigPath
    {
        public const string Filename = "PicoModuleConfig.json";
        public static string Directory => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        public static string FullPath => Path.Combine(ModuleConfigPath.Directory, ModuleConfigPath.Filename);
    }

    public IBlendshapeScaler build(ILogger Logger)
    {
        return new FileBlendshapeScaler(Logger, new FileSystem(), ModuleConfigPath.FullPath);
    }
}