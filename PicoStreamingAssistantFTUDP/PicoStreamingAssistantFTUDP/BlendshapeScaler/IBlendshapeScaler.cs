using VRCFaceTracking.Core.Params.Expressions;

namespace Pico4SAFTExtTrackingModule.BlendshapeScaler;

public interface IBlendshapeScaler
{
    float EyeExpressionShapeScale(float val, EyeExpressions type);
    float UnifiedExpressionShapeScale(float val, UnifiedExpressions type);

    List<EyeExpressions> GetUsedEyeExpressionShapes();
    List<UnifiedExpressions> GetUsedUnifiedExpressionShapes();
}
