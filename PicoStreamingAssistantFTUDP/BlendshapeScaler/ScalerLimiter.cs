using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRCFaceTracking.Core.Params.Expressions;

namespace Pico4SAFTExtTrackingModule.BlendshapeScaler;

/**
 * As VRC will go crazy if the param goes above 1.0f or below -1.0f,
 * this class will truncate the output.
 **/
public class ScalerLimiter : IBlendshapeScaler
{
    public static readonly float UPPER_LIMIT = 0.99f;
    public static readonly float LOWER_LIMIT = -0.99f;

    private IBlendshapeScaler limiting;
    public ScalerLimiter(IBlendshapeScaler limiting)
    {
        this.limiting = limiting;
    }

    public static float Filter(float val)
    {
        if (val > UPPER_LIMIT) val = UPPER_LIMIT;
        else if (val < LOWER_LIMIT) val = LOWER_LIMIT;
        return val;
    }

    public float EyeExpressionShapeScale(float val, EyeExpressions type)
    {
        return Filter(this.limiting.EyeExpressionShapeScale(val, type));
    }

    public float UnifiedExpressionShapeScale(float val, UnifiedExpressions type)
    {
        return Filter(this.limiting.UnifiedExpressionShapeScale(val, type));
    }

    public List<EyeExpressions> GetUsedEyeExpressionShapes()
    {
        return this.limiting.GetUsedEyeExpressionShapes();
    }

    public List<UnifiedExpressions> GetUsedUnifiedExpressionShapes()
    {
        return this.limiting.GetUsedUnifiedExpressionShapes();
    }
}