using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRCFaceTracking.Core.Params.Expressions;

namespace Pico4SAFTExtTrackingModule.BlendshapeScaler;

public interface IBlendshapeScaler
{
    float EyeExpressionShapeScale(float val, EyeExpressions type);
    float UnifiedExpressionShapeScale(float val, UnifiedExpressions type);

    List<EyeExpressions> GetUsedEyeExpressionShapes();
    List<UnifiedExpressions> GetUsedUnifiedExpressionShapes();
}
