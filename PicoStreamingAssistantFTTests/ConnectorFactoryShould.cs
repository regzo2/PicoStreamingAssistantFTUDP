using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Pico4SAFTExtTrackingModule.PicoConnectors.ProgramChecker;

namespace Pico4SAFTExtTrackingModule.PicoConnectors;

[TestClass]
public class ConnectorFactoryShould
{
    [TestMethod]
    public void ReturnNullIfNoSupportedProcessIsRunning()
    {
        // arrange
        Mock<IProgramChecker> programCheckerMock = new Mock<IProgramChecker>();
        programCheckerMock.Setup(m => m.Check(It.IsAny<PicoPrograms>()))
                        .Returns(false);

        // act
        PicoConnector? got = ConnectorFactory.build(null, programCheckerMock.Object);

        // assert
        Assert.IsNull(got);
    }

    [TestMethod]
    public void ReturnPicoConnectConnectorIfPicoConnectIsRunning()
    {
        // arrange
        Mock<IProgramChecker> programCheckerMock = new Mock<IProgramChecker>();
        programCheckerMock.Setup(m => m.Check(It.IsAny<PicoPrograms>()))
                        .Returns(false);
        programCheckerMock.Setup(m => m.Check(PicoPrograms.PicoConnect))
                        .Returns(true);

        // act
        PicoConnector? got = ConnectorFactory.build(null, programCheckerMock.Object);

        // assert
        Assert.AreEqual(typeof(PicoConnectConnector), got?.GetType());
    }

    [TestMethod]
    public void ReturnLegacyConnectorIfStreamingAssistantIsRunning()
    {
        // arrange
        Mock<IProgramChecker> programCheckerMock = new Mock<IProgramChecker>();
        programCheckerMock.Setup(m => m.Check(It.IsAny<PicoPrograms>()))
                        .Returns(false);
        programCheckerMock.Setup(m => m.Check(PicoPrograms.StreamingAssistant))
                        .Returns(true);

        // act
        PicoConnector? got = ConnectorFactory.build(null, programCheckerMock.Object);

        // assert
        Assert.AreEqual(typeof(LegacyConnector), got?.GetType());
        Assert.AreEqual("Streaming Assistant", got?.GetProcessName());
    }

    [TestMethod]
    public void ReturnLegacyConnectorIfBusinessStreamingIsRunning()
    {
        // arrange
        Mock<IProgramChecker> programCheckerMock = new Mock<IProgramChecker>();
        programCheckerMock.Setup(m => m.Check(It.IsAny<PicoPrograms>()))
                        .Returns(false);
        programCheckerMock.Setup(m => m.Check(PicoPrograms.BusinessStreaming))
                        .Returns(true);

        // act
        PicoConnector? got = ConnectorFactory.build(null, programCheckerMock.Object);

        // assert
        Assert.AreEqual(typeof(LegacyConnector), got?.GetType());
        Assert.AreEqual("Business Streaming", got?.GetProcessName());
    }
}