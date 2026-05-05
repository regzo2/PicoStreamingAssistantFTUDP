using VRCFaceTracking.Core.Params.Expressions;

namespace Pico4SAFTExtTrackingModule.BlendshapeScaler;

/// <summary>
/// As VRC will go crazy if the param goes above 1.0f or below -1.0f,
/// this class will truncate the output.
/// </summary>
/// <param name="limiting"></param>
public sealed class ScalerLimiter(IBlendshapeScaler limiting) : IBlendshapeScaler
{
    public static readonly float UPPER_LIMIT = 0.99f;
    public static readonly float LOWER_LIMIT = -0.99f;

    public static float Filter(float val)
        => float.Clamp(val, LOWER_LIMIT, UPPER_LIMIT);

    public float EyeExpressionShapeScale(float val, EyeExpressions type)
        => Filter(limiting.EyeExpressionShapeScale(val, type));

    public float UnifiedExpressionShapeScale(float val, UnifiedExpressions type)
        => Filter(limiting.UnifiedExpressionShapeScale(val, type));

    public List<EyeExpressions> GetUsedEyeExpressionShapes()
        => limiting.GetUsedEyeExpressionShapes();

    public List<UnifiedExpressions> GetUsedUnifiedExpressionShapes()
        => limiting.GetUsedUnifiedExpressionShapes();
}