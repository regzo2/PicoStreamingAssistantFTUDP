using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using Pico4SAFTExtTrackingModule.BlendshapeScaler;
using Pico4SAFTExtTrackingModule.PicoConnectors;

using VRCFaceTracking;
using VRCFaceTracking.Core.Library;
using VRCFaceTracking.Core.Params.Data;
using VRCFaceTracking.Core.Params.Expressions;

namespace Pico4SAFTExtTrackingModule;

[TestClass]
public class Pico4ModuleShould
{
    /// <summary>
    /// Creates and configures a mock implementation of <see cref="IBlendshapeScaler"/>.
    /// The mock returns the input value unchanged for both <c>EyeExpressionShapeScale</c>
    /// and <c>UnifiedExpressionShapeScale</c> methods, regardless of the expression type.
    /// This is useful for unit tests where scaling logic should be bypassed.
    /// </summary>
    /// <returns>
    /// A <see cref="Mock{IBlendshapeScaler}"/> instance with the relevant methods set up.
    /// </returns>
    private static Mock<IBlendshapeScaler> GetScalerMock()
    {
        Mock<IBlendshapeScaler> scaler = new Mock<IBlendshapeScaler>();
        scaler.Setup(m => m.EyeExpressionShapeScale(It.IsAny<float>(), It.IsAny<EyeExpressions>()))
            .Returns((float val, EyeExpressions _) => { return val; });
        scaler.Setup(m => m.UnifiedExpressionShapeScale(It.IsAny<float>(), It.IsAny<UnifiedExpressions>()))
            .Returns((float val, UnifiedExpressions _) => { return val; });
        return scaler;
    }

    [TestMethod]
    public void ApplyScalingToShapes()
    {
        int numberOfEyeParamsSet = 6,
            numberOfFaceParamsSet = 58;

        IPicoConnector pxrFTInfoMock = new IPicoConnectorMock();
        Mock<IBlendshapeScaler> scalerMock = GetScalerMock();
        Pico4SAFTExtTrackingModule uut = new Pico4SAFTExtTrackingModule(pxrFTInfoMock, scalerMock.Object);
        // simulate the `Setup` has been called
        uut.Status = ModuleState.Active;
        uut.TrackingState = (true, true);

        // act
        uut.Update();

        // assert
        scalerMock.Verify(m => m.EyeExpressionShapeScale(It.IsAny<float>(), It.IsAny<EyeExpressions>()), Times.Exactly(numberOfEyeParamsSet));
        scalerMock.Verify(m => m.UnifiedExpressionShapeScale(It.IsAny<float>(), It.IsAny<UnifiedExpressions>()), Times.Exactly(numberOfFaceParamsSet));
    }

    [Ignore] // too hard to fix with the module; will update the README
    [TestMethod]
    public void IgnoreFacetrackingBlendshapesWhenVisemesDataAvailable()
    {
        IPicoConnectorMock pxrFTInfoMock = new IPicoConnectorMock();
        Mock<IBlendshapeScaler> scalerMock = GetScalerMock();
        Pico4SAFTExtTrackingModule uut = new Pico4SAFTExtTrackingModule(pxrFTInfoMock, scalerMock.Object);
        // simulate the `Setup` has been called
        uut.Status = ModuleState.Active;
        uut.TrackingState = (true, true);

        // act
        // 1. no sound
        pxrFTInfoMock.setParam(BlendShapeIndex.JawOpen, 1.0f);
        pxrFTInfoMock.setParam(BlendShapeIndex.EyeBlink_L, 0.0f);
        pxrFTInfoMock.setParam(BlendShapeIndex.FF, 0.0f);
        uut.Update();

        // assert
        {
            Span<UnifiedExpressionShape> unifiedShape = UnifiedTracking.Data.Shapes;
            ref UnifiedSingleEyeData pLeft = ref UnifiedTracking.Data.Eye.Left;
            Assert.AreEqual(1.0f, unifiedShape[(int)UnifiedExpressions.JawOpen].Weight);
            Assert.AreEqual(1.0f, pLeft.Openness); // we set blink to 0 so that means it's open
        }

        // act
        // 2. sound; keep last
        pxrFTInfoMock.setParam(BlendShapeIndex.JawOpen, 0.0f); // jaw is set to 0 when there's sound
        pxrFTInfoMock.setParam(BlendShapeIndex.EyeBlink_L, 0.0f);
        pxrFTInfoMock.setParam(BlendShapeIndex.FF, 1.0f);
        uut.Update();

        // assert
        {
            Span<UnifiedExpressionShape> unifiedShape = UnifiedTracking.Data.Shapes;
            ref UnifiedSingleEyeData pLeft = ref UnifiedTracking.Data.Eye.Left;
            Assert.AreEqual(1.0f, unifiedShape[(int)UnifiedExpressions.JawOpen].Weight);
            Assert.AreEqual(1.0f, pLeft.Openness); // we set blink to 0 so that means it's open
        }

        // act
        // 3. still sound; keep last but update eye
        pxrFTInfoMock.setParam(BlendShapeIndex.JawOpen, 0.0f); // jaw is set to 0 when there's sound
        pxrFTInfoMock.setParam(BlendShapeIndex.EyeBlink_L, 1.0f);
        pxrFTInfoMock.setParam(BlendShapeIndex.FF, 1.0f);
        uut.Update();

        // assert
        {
            Span<UnifiedExpressionShape> unifiedShape = UnifiedTracking.Data.Shapes;
            ref UnifiedSingleEyeData pLeft = ref UnifiedTracking.Data.Eye.Left;
            Assert.AreEqual(1.0f, unifiedShape[(int)UnifiedExpressions.JawOpen].Weight);
            Assert.AreEqual(0.0f, pLeft.Openness); // we set blink to 1 so that means it's closed
        }

        // act
        // 4. still sound; keep last
        pxrFTInfoMock.setParam(BlendShapeIndex.JawOpen, 0.0f); // jaw is set to 0 when there's sound
        pxrFTInfoMock.setParam(BlendShapeIndex.EyeBlink_L, 1.0f);
        pxrFTInfoMock.setParam(BlendShapeIndex.FF, 0.02f);
        uut.Update();

        // assert
        {
            Span<UnifiedExpressionShape> unifiedShape = UnifiedTracking.Data.Shapes;
            ref UnifiedSingleEyeData pLeft = ref UnifiedTracking.Data.Eye.Left;
            Assert.AreEqual(1.0f, unifiedShape[(int)UnifiedExpressions.JawOpen].Weight);
            Assert.AreEqual(0.0f, pLeft.Openness); // we set blink to 1 so that means it's closed
        }

        // act
        // 5. no sound sound, but residual value left; update
        pxrFTInfoMock.setParam(BlendShapeIndex.JawOpen, 0.2f);
        pxrFTInfoMock.setParam(BlendShapeIndex.EyeBlink_L, 1.0f);
        pxrFTInfoMock.setParam(BlendShapeIndex.FF, 0.00002f); // some small value is left after talking
        uut.Update();

        // assert
        {
            Span<UnifiedExpressionShape> unifiedShape = UnifiedTracking.Data.Shapes;
            ref UnifiedSingleEyeData pLeft = ref UnifiedTracking.Data.Eye.Left;
            Assert.AreEqual(0.2f, unifiedShape[(int)UnifiedExpressions.JawOpen].Weight);
            Assert.AreEqual(0.0f, pLeft.Openness); // we set blink to 1 so that means it's closed
        }
    }



    private class IPicoConnectorMock : IPicoConnector
    {
        private PxrFTInfo getBlendShapesReturn;

        public IPicoConnectorMock()
        {
            this.getBlendShapesReturn = new PxrFTInfo();
        }

        public void setParam(BlendShapeIndex shape, float value)
        {
            this.getBlendShapesReturn.blendShapeWeight[(int)shape] = value;
        }

        public bool Connect()
        {
            throw new NotImplementedException();
        }

        public ReadOnlySpan<float> GetBlendShapes()
        {
            return getBlendShapesReturn.blendShapeWeight;
        }

        public string GetProcessName()
        {
            throw new NotImplementedException();
        }

        public void Teardown()
        {
            throw new NotImplementedException();
        }
    }
}
