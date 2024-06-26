using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Pico4SAFTExtTrackingModule.PicoConnectors;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Abstractions.TestingHelpers;
using System.Text;
using VRCFaceTracking.Core.Params.Expressions;

namespace Pico4SAFTExtTrackingModule.BlendshapeScaler;

[TestClass]
public class FileBlendshapeScalerShould
{
    private static FileBlendshapeScaler GetScaler()
    {
        return GetScaler(null);
    }

    private static FileBlendshapeScaler GetScaler(List<string>? errors)
    {
        return GetScalerWithCustomScales(new Dictionary<EyeExpressions, float>(), new Dictionary<UnifiedExpressions, float>(), errors); // no modifiers
    }

    private static FileBlendshapeScaler GetScalerWithCustomScales(Dictionary<EyeExpressions, float> eyeScales, Dictionary<UnifiedExpressions, float> unifiedScales)
    {
        return GetScalerWithCustomScales(eyeScales, unifiedScales, null);
    }

    private static string GetJsonContents(Dictionary<EyeExpressions, float> eyeScales, Dictionary<UnifiedExpressions, float> unifiedScales)
    {
        StringBuilder sb = new StringBuilder();
        NumberFormatInfo nfi = new System.Globalization.NumberFormatInfo();
        nfi.NumberDecimalSeparator = ".";

        foreach (KeyValuePair<EyeExpressions, float> scale in eyeScales)
        {
            sb.Append("\"")
                .Append(scale.Key.ToString())
                .Append("\":")
                .Append(scale.Value.ToString(nfi))
                .Append(',');
        }
        
        foreach (KeyValuePair<UnifiedExpressions, float> scale in unifiedScales)
        {
            sb.Append("\"")
                .Append(scale.Key.ToString())
                .Append("\":")
                .Append(scale.Value.ToString(nfi))
                .Append(',');
        }

        if (sb.Length > 0)
        {
            // not empty; we have to remove the last ','
            sb.Length = sb.Length-1;
        }

        return "{\"scales\": {" + sb.ToString() + "}}";
    }

    private static FileBlendshapeScaler GetScalerWithCustomScales(Dictionary<EyeExpressions,float> eyeScales, Dictionary<UnifiedExpressions, float> unifiedScales, List<string>? errors)
    {
        MockFileSystem mockFileSysteme = new MockFileSystem();
        string filePath = "ModuleConfig.json";
        MockFileData mockFileData = new MockFileData(GetJsonContents(eyeScales, unifiedScales));
        mockFileSysteme.AddFile(filePath, mockFileData);
        Mock<ILogger> logger = (errors == null) ? new Mock<ILogger>() : PicoConnectConfigCheckerShould.GetLoggerMock(errors);

        return new FileBlendshapeScaler(logger.Object, mockFileSysteme, filePath);
    }

    [TestMethod]
    public void ReturnExpectedUsedShapes()
    {
        int numberOfEyeParamsSet = 6,
            numberOfFaceParamsSet = 58;

        FileBlendshapeScaler uut = GetScaler();

        Assert.AreEqual(numberOfEyeParamsSet, uut.GetUsedEyeExpressionShapes().Count);
        Assert.AreEqual(numberOfFaceParamsSet, uut.GetUsedUnifiedExpressionShapes().Count);
    }

    [TestMethod]
    public void GenerateJsonFileIfNonExistant()
    {
        MockFileSystem mockFileSysteme = new MockFileSystem();
        string filePath = "ModuleConfig.json";
        Mock<ILogger> logger = new Mock<ILogger>();

        FileBlendshapeScaler uut = new FileBlendshapeScaler(logger.Object, mockFileSysteme, filePath);

        // this should trigger the generaton of the file
        uut.EyeExpressionShapeScale(0f, EyeExpressions.EyeOpennessRight);

        Assert.IsTrue(mockFileSysteme.File.Exists(filePath));
        Console.WriteLine("Json contents:\n\n" + mockFileSysteme.File.ReadAllText(filePath));
    }

    [TestMethod]
    public void KeepWorkingIfInvalidFileProvided()
    {
        MockFileSystem mockFileSysteme = new MockFileSystem();
        string filePath = "ModuleConfig.json";
        MockFileData mockFileData = new MockFileData("{\"invalid-data\":null");
        mockFileSysteme.AddFile(filePath, mockFileData);
        Mock<ILogger> logger = new Mock<ILogger>();
        float setting = 0.4f;

        FileBlendshapeScaler uut = new FileBlendshapeScaler(logger.Object, mockFileSysteme, filePath);

        float got = uut.EyeExpressionShapeScale(setting, EyeExpressions.EyeOpennessRight);

        Assert.AreEqual(setting, got, 0.01); // even if the file is wrong, we expect a '*1'
    }

    [TestMethod]
    public void ScaleAffectedBlendshapeBySetValue()
    {
        Dictionary<EyeExpressions, float> eyeScales = new Dictionary<EyeExpressions, float>();
        Dictionary<UnifiedExpressions, float> unifiedScales = new Dictionary<UnifiedExpressions, float>();
        float setting = 0.4f;
        float scalingByFactor = 1.3f;
        eyeScales.Add(EyeExpressions.EyeOpennessRight, scalingByFactor);
        FileBlendshapeScaler uut = GetScalerWithCustomScales(eyeScales, unifiedScales);

        float got = uut.EyeExpressionShapeScale(setting, EyeExpressions.EyeOpennessRight);

        Assert.AreEqual(setting * scalingByFactor, got, 0.01);
    }


    [TestMethod]
    public void DefaultScaleTo1ToUnaffectedBlendshapes()
    {
        float setting = 0.4f;
        FileBlendshapeScaler uut = GetScaler();

        float got = uut.EyeExpressionShapeScale(setting, EyeExpressions.EyeOpennessRight);

        Assert.AreEqual(setting, got, 0.01);
    }
}