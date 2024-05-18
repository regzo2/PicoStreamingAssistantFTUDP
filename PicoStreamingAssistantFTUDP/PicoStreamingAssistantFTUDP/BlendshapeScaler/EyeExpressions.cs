using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pico4SAFTExtTrackingModule.BlendshapeScaler;

public enum EyeExpressions
{
    #region Eye Gaze Expressions

    EyeXGazeRight,
    EyeYGazeRight,
    EyeXGazeLeft,
    EyeYGazeLeft,

    #endregion

    #region Eye Expressions

    EyeOpennessRight, // Open the right eyelid
    EyeOpennessLeft, // Open the left eyelid
    EyeDilationRight, // Dilates the right eye's pupil
    EyeDilationLeft // Dilates the left eye's pupil

    #endregion
}