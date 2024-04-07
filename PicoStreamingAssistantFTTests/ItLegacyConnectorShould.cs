using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using System.Net;
using Pico4SAFTExtTrackingModule.Mocks;

namespace Pico4SAFTExtTrackingModule.PicoConnectors;

[TestClass]
public class ItLegacyConnectorShould
{

    [TestMethod(), Timeout(30_000)]
    [Ignore()] // revisit test
    public void CloseANeverStablishedConnection()
    {
        Mock<ILogger> logger = new Mock<ILogger>();
        LegacyConnector uut = new LegacyConnector(logger.Object, PicoPrograms.PicoConnect);

        Thread startThread = new Thread(new ThreadStart(() => uut.Connect()));
        startThread.Start(); // this will try to connect, but (as we don't send anything) will be stuck at "Waiting for data..."

        Thread.Sleep(6_000);
        Assert.IsTrue(startThread.IsAlive, "Expected connector to still wait for connection; got that it finished already");

        uut.Teardown();

        SpinWait.SpinUntil(() => !startThread.IsAlive, 4_000);
        Assert.IsFalse(startThread.IsAlive, "Expected connector to finish; got still running");
    }

    [TestMethod(), Timeout(40_000)]
    [Ignore()] // revisit test
    public void CloseATimedoutConnection()
    {
        Mock<ILogger> logger = new Mock<ILogger>();
        LegacyConnector uut = new LegacyConnector(logger.Object, PicoPrograms.StreamingAssistant);
        StreamingAssistantSocketMock socketMock = new StreamingAssistantSocketMock();
        Thread? startThread = null, socketThread = null;

        try
        {
            startThread = new Thread(new ThreadStart(() => uut.Connect()));
            startThread.Start();

            socketMock.Setup();
            socketThread = new Thread(new ThreadStart(() => socketMock.Run()));
            socketThread.Start();

            SpinWait.SpinUntil(() => !startThread.IsAlive, 4_000);
            Assert.IsFalse(startThread.IsAlive, "Expected connector to finish after data was sent; got still running");
            startThread = null; // thread finished

            unsafe
            {
                // now the socket is connected; try to get something
                SpinWait.SpinUntil(() => uut.GetBlendShapes() != null, 5_000);
                Assert.IsTrue(uut.GetBlendShapes() != null, "Expected data to return; got null");

                // now close the server; a timeout should raise closing the connection
                socketMock.Dispose();
                socketThread.Join(8_000);
                Assert.IsFalse(socketThread.IsAlive, "Expected socket loop to finish; got still running");
                socketThread = null; // thread finished

                Func<bool> socketIsClosed = () => {
                    try
                    {
                        return uut.GetBlendShapes() == null;
                    }
                    catch (SocketException ex) when (ex.ErrorCode is 10060)
                    {
                        return true;
                    }
                };
                SpinWait.SpinUntil(socketIsClosed, 15_000);
                Assert.IsTrue(socketIsClosed(), "Expected connection to be closed; still returning data");
            }

            // re-create again
            uut.Teardown();
            startThread = new Thread(new ThreadStart(() => uut.Connect()));
            startThread.Start();

            Thread.Sleep(6_000);
            Assert.IsTrue(startThread.IsAlive, "Expected connector to still wait for connection; got that it finished already");

            // now try to close the connection
            uut.Teardown();

            SpinWait.SpinUntil(() => !startThread.IsAlive, 1_000); // less than 1s is way too noticeable
            Assert.IsFalse(startThread.IsAlive, "Expected connector to finish; got still running");
            startThread = null; // thread finished 
        }
        finally
        {
            Console.WriteLine("Cleaning...");
            socketMock.Dispose();
            uut.Teardown();
            startThread?.Join();
            socketThread?.Join();
        }
    }
}
