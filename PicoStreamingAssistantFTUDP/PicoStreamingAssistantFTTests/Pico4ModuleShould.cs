using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using Pico4SAFTExtTrackingModule.BlendshapeScaler;
using Pico4SAFTExtTrackingModule.PicoConnectors;
using VRCFaceTracking.Core.Library;
using VRCFaceTracking.Core.Params.Expressions;
using System.Diagnostics;
using VRCFaceTracking;

namespace Pico4SAFTExtTrackingModule;

[TestClass]
public class Pico4ModuleShould
{
    [TestMethod]
    public void PublishItsIdentityBeforeTheTrackingServiceConnects()
    {
        Mock<IPicoConnector> connector = new Mock<IPicoConnector>();
        Pico4SAFTExtTrackingModule module = new Pico4SAFTExtTrackingModule(connector.Object, GetScalerMock().Object)
        {
            Logger = NullLogger.Instance,
            ModuleInformation = new ModuleMetadata(),
        };

        var initialized = module.Initialize(true, true);

        Assert.AreEqual((true, true), initialized);
        Assert.AreEqual("Pico 4 Pro / Enterprise", module.ModuleInformation.Name);
        Assert.AreEqual(1, module.ModuleInformation.StaticImages.Count);
        connector.Verify(c => c.Connect(), Times.Never);
    }

    private static Mock<IBlendshapeScaler> GetScalerMock()
    {
        Mock<IBlendshapeScaler> logger = new Mock<IBlendshapeScaler>();
        logger.Setup(m => m.EyeExpressionShapeScale(It.IsAny<float>(), It.IsAny<EyeExpressions>()))
            .Returns((float val, EyeExpressions _) => { return val; });
        logger.Setup(m => m.UnifiedExpressionShapeScale(It.IsAny<float>(), It.IsAny<UnifiedExpressions>()))
            .Returns((float val, UnifiedExpressions _) => { return val; });
        return logger;
    }

    [TestMethod]
    public unsafe void ApplyScalingToShapes()
    {
        int numberOfEyeParamsSet = 6,
            numberOfFaceParamsSet = 58;

        IPicoConnector pxrFTInfoMock = new IPicoConnectorMock();
        Mock<IBlendshapeScaler> scalerMock = GetScalerMock();
        Pico4SAFTExtTrackingModule uut = new Pico4SAFTExtTrackingModule(pxrFTInfoMock, scalerMock.Object);
        // simulate the `Setup` has been called
        uut.Status = ModuleState.Active;
        uut.trackingState = (true, true);

        // act
        uut.Update();

        // assert
        scalerMock.Verify(m => m.EyeExpressionShapeScale(It.IsAny<float>(), It.IsAny<EyeExpressions>()), Times.Exactly(numberOfEyeParamsSet));
        scalerMock.Verify(m => m.EyeExpressionShapeScale(It.IsAny<float>(), EyeExpressions.EyeYGazeRight), Times.Once);
        scalerMock.Verify(m => m.UnifiedExpressionShapeScale(It.IsAny<float>(), It.IsAny<UnifiedExpressions>()), Times.Exactly(numberOfFaceParamsSet));
    }



    [TestMethod]
    public unsafe void BackOffAfterTrackingUpdateThrows()
    {
        Mock<IPicoConnector> connector = new Mock<IPicoConnector>();
        connector.Setup(m => m.GetBlendShapes()).Throws<InvalidOperationException>();
        Pico4SAFTExtTrackingModule uut = new Pico4SAFTExtTrackingModule(connector.Object, GetScalerMock().Object)
        {
            Status = ModuleState.Active,
            Logger = NullLogger.Instance,
        };

        uut.Update();
        var retryWait = Stopwatch.StartNew();
        uut.Update();
        retryWait.Stop();

        connector.Verify(m => m.GetBlendShapes(), Times.Once);
        Assert.IsTrue(retryWait.ElapsedMilliseconds >= 80, "A failed update must not make the host loop spin.");
    }

    private class IPicoConnectorMock : IPicoConnector
    {
        private PxrFTInfo getBlendShapesReturn;

        public IPicoConnectorMock()
        {
            this.getBlendShapesReturn = new PxrFTInfo();
        }

        public IPicoConnectorMock(PxrFTInfo getBlendShapesReturn)
        {
            this.getBlendShapesReturn = getBlendShapesReturn;
        }

        public bool Connect()
        {
            throw new NotImplementedException();
        }

        public unsafe float* GetBlendShapes()
        {
            fixed (PxrFTInfo* pData = &this.getBlendShapesReturn)
                return pData->blendShapeWeight;
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
