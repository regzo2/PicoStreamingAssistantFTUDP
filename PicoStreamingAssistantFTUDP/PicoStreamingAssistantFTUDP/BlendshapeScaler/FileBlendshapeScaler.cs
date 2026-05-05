using System.IO.Abstractions;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using VRCFaceTracking.Core.Params.Expressions;

namespace Pico4SAFTExtTrackingModule.BlendshapeScaler;

public sealed partial class FileBlendshapeScaler(ILogger logger, IFileSystem fileSystem, string configPath) : IBlendshapeScaler
{
    private Dictionary<EyeExpressions, float>? _eyeScales = null;
    private Dictionary<UnifiedExpressions, float>? _unifiedScales = null;

    public bool LoadConfigFile()
    {
        _eyeScales = [];
        _unifiedScales = [];

        if (!fileSystem.File.Exists(configPath) && !CreateConfigFile())
            return false; // failed

        try
        {
            string stringifiedJson = fileSystem.File.ReadAllText(configPath);
            JsonElement scales = JsonDocument.Parse(stringifiedJson).RootElement.GetProperty("scales");
            foreach (var jsonProperty in scales.EnumerateObject())
            {
                LogDebugTryToParse(jsonProperty.Name);
                if (Enum.TryParse<EyeExpressions>(jsonProperty.Name, out var eyeExpression))
                {
                    LogDebugMatchEyeExpression(jsonProperty.Name, jsonProperty.Value);
                    _eyeScales.Add(eyeExpression, jsonProperty.Value.GetSingle());
                }
                if (Enum.TryParse<UnifiedExpressions>(jsonProperty.Name, out var unifiedExpression))
                {
                    LogDebugMatchUnifiedExpression(jsonProperty.Name, jsonProperty.Value);
                    _unifiedScales.Add(unifiedExpression, jsonProperty.Value.GetSingle());
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            LogError(ex);
            return false;
        }
    }

    private bool CreateConfigFile()
    {
        LogCreateConfigFile();

        using StringWriter writer = new()
        {
            NewLine = "\r\n  ",
        };
        writer.WriteLine('{');
        writer.NewLine = "\r\n    ";
        writer.WriteLine("\"scales\": {");

        foreach (var ee in GetUsedEyeExpressionShapes())
        {
            writer.Write('"');
            writer.Write(ee);
            writer.Write('"');
            writer.WriteLine(": 1.00,");
        }

        foreach (var ue in GetUsedUnifiedExpressionShapes())
        {
            writer.Write('"');
            writer.Write(ue);
            writer.Write('"');
            writer.WriteLine(": 1.00,");
        }

        if (writer.GetStringBuilder() is { Length: > 0 } sb)
        {
            // not empty; we have to remove the last ','
            sb.Length -= writer.NewLine.Length + 1;
        }

        writer.NewLine = "\r\n  ";
        writer.WriteLine();

        writer.NewLine = "\r\n";
        writer.WriteLine('}');
        writer.WriteLine('}');

        try
        {
            fileSystem.File.WriteAllText(configPath, writer.ToString());
            return true;
        }
        catch (Exception ex)
        {
            LogError(ex);
            return false;
        }
    }

    public float EyeExpressionShapeScale(float val, EyeExpressions type)
    {
        if (_eyeScales == null)
        {
            bool loaded = LoadConfigFile();
            if (!loaded || _eyeScales == null)
                return val; // couldn't load; expect a '*1' multiplier
        }

        if (!_eyeScales.TryGetValue(type, out float scale))
            scale = 1.0f; // property not set
        return val * scale;
    }

    public float UnifiedExpressionShapeScale(float val, UnifiedExpressions type)
    {
        if (_unifiedScales == null)
        {
            bool loaded = LoadConfigFile();
            if (!loaded || _unifiedScales == null)
                return val; // couldn't load; expect a '*1' multiplier
        }

        if (!_unifiedScales.TryGetValue(type, out float scale))
            scale = 1.0f; // property not set

        return val * scale;
    }

    public List<EyeExpressions> GetUsedEyeExpressionShapes() =>
    [
        EyeExpressions.EyeXGazeRight,
        EyeExpressions.EyeYGazeRight,
        EyeExpressions.EyeXGazeLeft,
        EyeExpressions.EyeYGazeLeft,
        EyeExpressions.EyeOpennessRight,
        EyeExpressions.EyeOpennessLeft
    ];

    public List<UnifiedExpressions> GetUsedUnifiedExpressionShapes() =>
    [
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
    ];


    [LoggerMessage(LogLevel.Debug, "Trying to parse {property}...")]
    private partial void LogDebugTryToParse(string property);

    [LoggerMessage(LogLevel.Debug, "{property} matches as EyeExpression! Set its scaling to {value}")]
    private partial void LogDebugMatchEyeExpression(string property, JsonElement value);

    [LoggerMessage(LogLevel.Debug, "{property} matches as UnifiedExpression! Set its scaling to {value}")]
    private partial void LogDebugMatchUnifiedExpression(string property, JsonElement value);

    [LoggerMessage(LogLevel.Error, "FileBlendshapeScaler.LoadConfigFile: Unexpected exceptions")]
    private partial void LogError(Exception exception);


    [LoggerMessage(LogLevel.Information, "Generating blendshape scaling config file...")]
    private partial void LogCreateConfigFile();
}