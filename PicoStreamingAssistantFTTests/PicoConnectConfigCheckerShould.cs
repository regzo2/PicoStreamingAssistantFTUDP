using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Pico4SAFTExtTrackingModule.PicoConnectors.ConfigChecker;
using Pico4SAFTExtTrackingModule.PicoConnectors.ConfigChecker.PicoConnect;
using Pico4SAFTExtTrackingModule.PicoConnectors.ProgramChecker;

namespace Pico4SAFTExtTrackingModule.PicoConnectors;

[TestClass]
public class PicoConnectConfigCheckerShould
{
    [TestMethod]
    public void ReturnTransferProtocol0WhenFileContainsTransferProtocol0()
    {
        // arrange
        PicoConnectConfigChecker uut = new PicoConnectConfigChecker();

        // act
        int got = uut.GetTransferProtocolNumber(PicoPrograms.PicoConnect);

        // assert
        Assert.AreEqual(0, got);
    }

    [TestMethod]
    public void ReturnTransferProtocol2WhenFileContainsTransferProtocol2()
    {
        // arrange
        PicoConnectConfigChecker uut = new PicoConnectConfigChecker();

        // act
        int got = uut.GetTransferProtocolNumber(PicoPrograms.PicoConnect);

        // assert
        Assert.AreEqual(2, got);
    }
}