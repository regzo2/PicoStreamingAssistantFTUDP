using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Pico4SAFTExtTrackingModule.PicoConnectors.ConfigChecker;
using Pico4SAFTExtTrackingModule.PicoConnectors.ProgramChecker;

namespace Pico4SAFTExtTrackingModule.PicoConnectors;

[TestClass]
public class ConnectorFactoryShould
{
    [TestMethod]
    public void ReturnNullIfNoSupportedProcessIsRunning()
    {
        // arrange
        Mock<ILogger> logger = new Mock<ILogger>();
        Mock<IProgramChecker> programCheckerMock = new Mock<IProgramChecker>();
        programCheckerMock.Setup(m => m.Check(It.IsAny<PicoPrograms>()))
                        .Returns(false);
        Mock<IConfigChecker> configCheckerMock = new Mock<IConfigChecker>();
        configCheckerMock.Setup(m => m.GetTransferProtocolNumber(It.IsAny<PicoPrograms>()))
                        .Returns(0);

        // act
        IPicoConnector? got = ConnectorFactory.build(logger.Object, programCheckerMock.Object, configCheckerMock.Object);

        // assert
        Assert.IsNull(got);
    }

    [TestMethod]
    public void ReturnPicoConnectConnectorIfPicoConnectIsRunning()
    {
        // arrange
        Mock<ILogger> logger = new Mock<ILogger>();
        Mock<IProgramChecker> programCheckerMock = new Mock<IProgramChecker>();
        programCheckerMock.Setup(m => m.Check(It.IsAny<PicoPrograms>()))
                        .Returns(false);
        programCheckerMock.Setup(m => m.Check(PicoPrograms.PicoConnect))
                        .Returns(true);
        Mock<IConfigChecker> configCheckerMock = new Mock<IConfigChecker>();
        configCheckerMock.Setup(m => m.GetTransferProtocolNumber(It.IsAny<PicoPrograms>()))
                        .Returns(0);

        // act
        IPicoConnector? got = ConnectorFactory.build(logger.Object, programCheckerMock.Object, configCheckerMock.Object);

        // assert
        Assert.AreEqual(typeof(PicoConnectConnector), got?.GetType());
    }

    [TestMethod]
    public void ReturnLegacyConnectorIfPicoConnectIsRunningAndIsUsingOldTransferProtocol()
    {
        // arrange
        Mock<ILogger> logger = new Mock<ILogger>();
        Mock<IProgramChecker> programCheckerMock = new Mock<IProgramChecker>();
        programCheckerMock.Setup(m => m.Check(It.IsAny<PicoPrograms>()))
                        .Returns(false);
        programCheckerMock.Setup(m => m.Check(PicoPrograms.PicoConnect))
                        .Returns(true);
        Mock<IConfigChecker> configCheckerMock = new Mock<IConfigChecker>();
        configCheckerMock.Setup(m => m.GetTransferProtocolNumber(It.IsAny<PicoPrograms>()))
                        .Returns(2); // old transfer protocol

        // act
        IPicoConnector? got = ConnectorFactory.build(logger.Object, programCheckerMock.Object, configCheckerMock.Object);

        // assert
        Assert.AreEqual(typeof(LegacyConnector), got?.GetType());
        Assert.AreEqual("PICO Connect", got?.GetProcessName());
    }

    [TestMethod]
    public void ReturnLegacyConnectorIfStreamingAssistantIsRunning()
    {
        // arrange
        Mock<ILogger> logger = new Mock<ILogger>();
        Mock<IProgramChecker> programCheckerMock = new Mock<IProgramChecker>();
        programCheckerMock.Setup(m => m.Check(It.IsAny<PicoPrograms>()))
                        .Returns(false);
        programCheckerMock.Setup(m => m.Check(PicoPrograms.StreamingAssistant))
                        .Returns(true);
        Mock<IConfigChecker> configCheckerMock = new Mock<IConfigChecker>();
        configCheckerMock.Setup(m => m.GetTransferProtocolNumber(It.IsAny<PicoPrograms>()))
                        .Returns(0);

        // act
        IPicoConnector? got = ConnectorFactory.build(logger.Object, programCheckerMock.Object, configCheckerMock.Object);

        // assert
        Assert.AreEqual(typeof(LegacyConnector), got?.GetType());
        Assert.AreEqual("Streaming Assistant", got?.GetProcessName());
    }

    [TestMethod]
    public void ReturnLegacyConnectorIfBusinessStreamingIsRunning()
    {
        // arrange
        Mock<ILogger> logger = new Mock<ILogger>();
        Mock<IProgramChecker> programCheckerMock = new Mock<IProgramChecker>();
        programCheckerMock.Setup(m => m.Check(It.IsAny<PicoPrograms>()))
                        .Returns(false);
        programCheckerMock.Setup(m => m.Check(PicoPrograms.BusinessStreaming))
                        .Returns(true);
        Mock<IConfigChecker> configCheckerMock = new Mock<IConfigChecker>();
        configCheckerMock.Setup(m => m.GetTransferProtocolNumber(It.IsAny<PicoPrograms>()))
                        .Returns(0);

        // act
        IPicoConnector? got = ConnectorFactory.build(logger.Object, programCheckerMock.Object, configCheckerMock.Object);

        // assert
        Assert.AreEqual(typeof(LegacyConnector), got?.GetType());
        Assert.AreEqual("Business Streaming", got?.GetProcessName());
    }
}