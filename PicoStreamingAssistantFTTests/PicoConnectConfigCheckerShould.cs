using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Pico4SAFTExtTrackingModule.PicoConnectors.ConfigChecker.PicoConnect;
using System.IO.Abstractions.TestingHelpers;

namespace Pico4SAFTExtTrackingModule.PicoConnectors;

[TestClass]
public class PicoConnectConfigCheckerShould
{
    public static readonly string CONFIG_FILE_PATH = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PICO Connect\\settings.json");

    public static Mock<ILogger> GetLoggerMock(List<string> errors)
    {
        Mock<ILogger> logger = new Mock<ILogger>();
        logger.Setup(m => m.Log(
                    It.Is<LogLevel>(logLevel => logLevel == LogLevel.Error || logLevel == LogLevel.Critical),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()))
            .Callback(new InvocationAction(invocation => {
                var logLevel = (LogLevel)invocation.Arguments[0]; // The first two will always be whatever is specified in the setup above
                var eventId = (EventId)invocation.Arguments[1];  // so I'm not sure you would ever want to actually use them
                var state = invocation.Arguments[2];
                var exception = (Exception)invocation.Arguments[3];
                var formatter = invocation.Arguments[4];

                var invokeMethod = formatter.GetType().GetMethod("Invoke");
                var logMessage = (string)invokeMethod?.Invoke(formatter, new[] { state, exception });

                errors.Add(logMessage);
            }));
        return logger;
    }

    [TestMethod]
    public void ReturnTransferProtocol0WhenFileContainsTransferProtocol0()
    {
        // arrange
        List<string> errors = new List<string>();
        Mock<ILogger> logger = GetLoggerMock(errors);

        MockFileSystem mockFileSysteme = new MockFileSystem();
        MockFileData mockFileData = new MockFileData("File content");
        //mockFileSysteme.AddFile(CONFIG_FILE_PATH, mockFileData);

        PicoConnectConfigChecker uut = new PicoConnectConfigChecker(logger.Object, mockFileSysteme);

        // act
        int got = uut.GetTransferProtocolNumber(PicoPrograms.PicoConnect);

        // assert
        Assert.AreEqual(0, errors.Count, "Expected no errors; instead got:\n" + String.Join("\n", errors));
        Assert.AreEqual(0, got);
    }

    [TestMethod]
    public void ReturnTransferProtocol2WhenFileContainsTransferProtocol2()
    {
        // arrange
        List<string> errors = new List<string>();
        Mock<ILogger> logger = GetLoggerMock(errors);

        MockFileSystem mockFileSysteme = new MockFileSystem();
        MockFileData mockFileData = new MockFileData("File content");
        mockFileSysteme.AddFile(CONFIG_FILE_PATH, mockFileData);

        PicoConnectConfigChecker uut = new PicoConnectConfigChecker(logger.Object, mockFileSysteme);

        // act
        int got = uut.GetTransferProtocolNumber(PicoPrograms.PicoConnect);

        // assert
        Assert.AreEqual(0, errors.Count, "Expected no errors; instead got:\n" + String.Join("\n", errors));
        Assert.AreEqual(2, got);
    }
}