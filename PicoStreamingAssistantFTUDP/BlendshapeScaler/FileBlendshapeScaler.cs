using System.Globalization;
using System.IO.Abstractions;
using System.Text;
using Microsoft.Extensions.Logging;
using VRCFaceTracking.Core.Params.Expressions;
using Newtonsoft.Json.Linq;

namespace Pico4SAFTExtTrackingModule.BlendshapeScaler;

public class FileBlendshapeScaler : IBlendshapeScaler
{
    private ILogger? Logger;
    private IFileSystem fileSystem;
    private string configPath;

    private Dictionary<EyeExpressions, float>? eyeScales;
    private Dictionary<UnifiedExpressions, float>? unifiedScales;

    public FileBlendshapeScaler(ILogger? logger, IFileSystem fileSystem, string configPath)
    {
        this.Logger = logger;
        this.fileSystem = fileSystem;
        this.configPath = configPath;

        this.eyeScales = null;
        this.unifiedScales = null;
    }

    public bool LoadConfigFile()
    {
        this.eyeScales = new Dictionary<EyeExpressions, float>();
        this.unifiedScales = new Dictionary<UnifiedExpressions, float>();

        if (!this.fileSystem.File.Exists(this.configPath))
        {
            bool createResult = this.CreateConfigFile();
            if (!createResult) return false; // failed
        }

        try
        {
            string stringifiedJson = this.fileSystem.File.ReadAllText(this.configPath);
            JObject scalesObject = JObject.Parse(stringifiedJson);
            JToken scales = scalesObject["scales"];
            foreach (JProperty jsonProperty in scales)
            {
                if (Logger != null) Logger.LogDebug($"Trying to parse {jsonProperty.Name}...");
                if (Enum.TryParse<EyeExpressions>(jsonProperty.Name, out EyeExpressions eyeExpression))
                {
                    if (Logger != null) Logger.LogDebug($"{jsonProperty.Name} matches as EyeExpression! Set its scaling to {jsonProperty.Value}");
                    this.eyeScales.Add(eyeExpression, jsonProperty.Value.ToObject<float>());
                }
                if (Enum.TryParse<UnifiedExpressions>(jsonProperty.Name, out UnifiedExpressions unifiedExpression))
                {
                    if (Logger != null) Logger.LogDebug($"{jsonProperty.Name} matches as UnifiedExpression! Set its scaling to {jsonProperty.Value}");
                    this.unifiedScales.Add(unifiedExpression, jsonProperty.Value.ToObject<float>());
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            if (Logger != null) Logger.LogError(ex.ToString());
            return false;
        }
    }

    private bool CreateConfigFile()
    {
        if (Logger != null) Logger.LogInformation("Generating blendshape scaling config file...");

        StringBuilder sb = new StringBuilder();
        NumberFormatInfo nfi = new System.Globalization.NumberFormatInfo();
        nfi.NumberDecimalSeparator = ".";

        sb.Append("{\n\t\"scales\": {\n");

        foreach (EyeExpressions ee in this.GetUsedEyeExpressionShapes())
        {
            sb.Append("\t\t\"")
                .Append(ee.ToString())
                .Append("\": 1.00,\n");
        }

        foreach (UnifiedExpressions ue in this.GetUsedUnifiedExpressionShapes())
        {
            sb.Append("\t\t\"")
                .Append(ue.ToString())
                .Append("\": 1.00,\n");
        }

        if (sb.Length > 0)
        {
            // not empty; we have to remove the last ','
            sb.Length = sb.Length - 2;
            sb.Append('\n');
        }

        sb.Append("\t}\n}");

        try
        {
            this.fileSystem.File.WriteAllText(this.configPath, sb.ToString());
            return true;
        }
        catch (Exception ex) {
            if (Logger != null) Logger.LogError(ex.ToString());
            return false;
        }
    }

    public float EyeExpressionShapeScale(float val, EyeExpressions type)
    {
        if (this.eyeScales == null)
        {
            bool loaded = LoadConfigFile();
            if (!loaded || this.eyeScales == null) return val; // couldn't load; expect a '*1' multiplier
        }

        float scale;
        if (!this.eyeScales.TryGetValue(type, out scale)) scale = 1.0f; // property not set
        return val * scale;
    }

    public float UnifiedExpressionShapeScale(float val, UnifiedExpressions type)
    {
        if (this.unifiedScales == null)
        {
            bool loaded = LoadConfigFile();
            if (!loaded || this.unifiedScales == null) return val; // couldn't load; expect a '*1' multiplier
        }

        float scale;
        if (!this.unifiedScales.TryGetValue(type, out scale)) scale = 1.0f; // property not set
        return val * scale;
    }

    public List<EyeExpressions> GetUsedEyeExpressionShapes()
    {
        return new List<EyeExpressions>()
        {
            EyeExpressions.EyeXGazeRight,
            EyeExpressions.EyeYGazeRight,
            EyeExpressions.EyeXGazeLeft,
            EyeExpressions.EyeYGazeLeft,
            EyeExpressions.EyeOpennessRight,
            EyeExpressions.EyeOpennessLeft
        };
    }

    public List<UnifiedExpressions> GetUsedUnifiedExpressionShapes()
    {
        return new List<UnifiedExpressions>()
        {
            UnifiedExpressions.BrowInnerUpLeft,
            UnifiedExpressions.BrowInnerUpRight,
            UnifiedExpressions.BrowOuterUpLeft,
            UnifiedExpressions.BrowOuterUpRight,
            UnifiedExpressions.BrowLowererLeft,
            UnifiedExpressions.BrowPinchLeft,
            UnifiedExpressions.BrowLowererRight,
            UnifiedExpressions.BrowPinchRight,
            UnifiedExpressions.EyeSquintLeft,
            UnifiedExpressions.EyeSquintRight,
            UnifiedExpressions.EyeWideLeft,
            UnifiedExpressions.EyeWideRight,
            UnifiedExpressions.JawOpen,
            UnifiedExpressions.JawLeft,
            UnifiedExpressions.JawRight,
            UnifiedExpressions.JawForward,
            UnifiedExpressions.MouthClosed,
            UnifiedExpressions.CheekPuffLeft,
            UnifiedExpressions.CheekPuffRight,
            UnifiedExpressions.CheekSquintLeft,
            UnifiedExpressions.CheekSquintRight,
            UnifiedExpressions.NoseSneerLeft,
            UnifiedExpressions.NoseSneerRight,
            UnifiedExpressions.MouthUpperUpLeft,
            UnifiedExpressions.MouthUpperUpRight,
            UnifiedExpressions.MouthLowerDownLeft,
            UnifiedExpressions.MouthLowerDownRight,
            UnifiedExpressions.MouthFrownLeft,
            UnifiedExpressions.MouthFrownRight,
            UnifiedExpressions.MouthDimpleLeft,
            UnifiedExpressions.MouthDimpleRight,
            UnifiedExpressions.MouthUpperLeft,
            UnifiedExpressions.MouthLowerLeft,
            UnifiedExpressions.MouthUpperRight,
            UnifiedExpressions.MouthLowerRight,
            UnifiedExpressions.MouthPressLeft,
            UnifiedExpressions.MouthPressRight,
            UnifiedExpressions.MouthRaiserLower,
            UnifiedExpressions.MouthRaiserUpper,
            UnifiedExpressions.MouthCornerPullLeft,
            UnifiedExpressions.MouthCornerSlantLeft,
            UnifiedExpressions.MouthCornerPullRight,
            UnifiedExpressions.MouthCornerSlantRight,
            UnifiedExpressions.MouthStretchLeft,
            UnifiedExpressions.MouthStretchRight,
            UnifiedExpressions.LipFunnelUpperLeft,
            UnifiedExpressions.LipFunnelUpperRight,
            UnifiedExpressions.LipFunnelLowerLeft,
            UnifiedExpressions.LipFunnelLowerRight,
            UnifiedExpressions.LipPuckerUpperLeft,
            UnifiedExpressions.LipPuckerUpperRight,
            UnifiedExpressions.LipPuckerLowerLeft,
            UnifiedExpressions.LipPuckerLowerRight,
            UnifiedExpressions.LipSuckUpperLeft,
            UnifiedExpressions.LipSuckUpperRight,
            UnifiedExpressions.LipSuckLowerLeft,
            UnifiedExpressions.LipSuckLowerRight,
            UnifiedExpressions.TongueOut
        };
    }
}