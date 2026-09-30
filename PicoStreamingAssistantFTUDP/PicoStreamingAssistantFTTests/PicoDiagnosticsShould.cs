using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pico4SAFTExtTrackingModule.Diagnostics;
using Pico4SAFTExtTrackingModule.PicoConnectors;
using System.Net;
using System.Net.Sockets;
using System.Globalization;
using Moq;
using Pico4SAFTExtTrackingModule.PicoConnectors.ConfigChecker;
using Pico4SAFTExtTrackingModule.PicoConnectors.ProgramChecker;

namespace Pico4SAFTExtTrackingModule;

[TestClass]
[DoNotParallelize]
public class PicoDiagnosticsShould
{
    [TestMethod]
    public void WriteFirstExceptionImmediatelyWithTimestampAndKeepOnlyRepeatCounts()
    {
        var clock = new DiagnosticTestClock();
        var log = new DiagnosticTestLogger();
        var diagnostics = new PicoDiagnostics(() => log, () => clock.UtcNow);
        var error = new CountedException();
        DateTime first = clock.UtcNow;

        diagnostics.Report(LogLevel.Warning, "ReceiveFailure", "", () => "rebuild listener; backoff=1000ms", error);
        Assert.AreEqual(1, log.Entries.Count);
        StringAssert.Contains(log.Entries[0].Text, clock.UtcNow.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff zzz"));
        StringAssert.Contains(log.Entries[0].Text, "complete exception stack");

        clock.Advance(1);
        diagnostics.Report(LogLevel.Warning, "ReceiveFailure", "", () => throw new Exception("Must not format duplicates"), error);
        Assert.AreEqual(1, log.Entries.Count);
        Assert.AreEqual(1, error.Formatted);

        clock.Advance(29);
        diagnostics.Tick(); // No new error is needed to write the summary.
        Assert.AreEqual(2, log.Entries.Count);
        StringAssert.Contains(log.Entries[1].Text, "1 additional occurrences");
        StringAssert.Contains(log.Entries[1].Text, "first detail=#1");
        StringAssert.Contains(log.Entries[1].Text, $"since={Timestamp(first)}");
        StringAssert.Contains(log.Entries[1].Text, $"first={Timestamp(first)}");
        StringAssert.Contains(log.Entries[1].Text, $"last={Timestamp(first.AddSeconds(1))}");
        Assert.AreEqual(1, error.Formatted);
    }

    [TestMethod]
    public void DeduplicateStructuredLoggerArgumentsWithoutMergingDifferentTemplatesOrEventIds()
    {
        var clock = new DiagnosticTestClock();
        var log = new DiagnosticTestLogger();
        var diagnostics = new PicoDiagnostics(() => log, () => clock.UtcNow);
        for (int i = 0; i < 10_000; i++)
            diagnostics.Logger.LogDebug("Observed {Count} packets at {Path}.", i, $"/source/{i}");
        Assert.AreEqual(1, log.Entries.Count);
        StringAssert.Contains(log.Entries[0].Text, "Observed 0 packets at /source/0.");

        diagnostics.Logger.LogDebug("Different event: {Count} packets.", 0);
        diagnostics.Logger.LogDebug(new EventId(1, "Poll"), "Observed {Count} packets at {Path}.", 1, "/source/1");
        diagnostics.Logger.LogDebug(new EventId(2, "Poll"), "Observed {Count} packets at {Path}.", 2, "/source/2");
        Assert.AreEqual(4, log.Entries.Count);
        clock.Advance(30);
        diagnostics.Tick();
        Assert.AreEqual(5, log.Entries.Count);
        StringAssert.Contains(log.Single("DiagnosticSummary").Text, "9999 additional occurrences");
    }

    [TestMethod]
    public void UseExplicitEventIdentityForCustomLoggerStatesAndOnlyFormatFirstDetails()
    {
        var clock = new DiagnosticTestClock();
        var log = new DiagnosticTestLogger();
        var diagnostics = new PicoDiagnostics(() => log, () => clock.UtcNow);
        int formatted = 0;
        for (int i = 0; i < 1000; i++)
            diagnostics.Logger.Log(LogLevel.Information, new EventId(7, "CustomPoll"), i, null,
                (count, _) => { formatted++; return $"custom count={count}"; });
        Assert.AreEqual(1, formatted);
        Assert.AreEqual(1, log.Entries.Count);
        clock.Advance(30);
        diagnostics.Tick();
        StringAssert.Contains(log.Single("CustomPollSummary").Text, "999 additional occurrences");
    }

    [TestMethod]
    public void KeepStreamingProgramAndProtocolSupportChangesVisibleWithinTheRepeatWindow()
    {
        var log = new DiagnosticTestLogger();
        var diagnostics = new PicoDiagnostics(() => log);
        bool running = true;
        int protocol = 2;
        var programs = new Mock<IProgramChecker>();
        programs.Setup(checker => checker.Check(It.IsAny<PicoPrograms>()))
            .Returns((PicoPrograms program) => running && program == PicoPrograms.PicoConnect);
        var config = new Mock<IConfigChecker>();
        config.Setup(checker => checker.GetTransferProtocolNumber(PicoPrograms.PicoConnect)).Returns(() => protocol);
        Assert.IsInstanceOfType(ConnectorFactory.build(diagnostics.Logger, programs.Object, config.Object), typeof(LegacyConnector));
        protocol = 3;
        Assert.IsInstanceOfType(ConnectorFactory.build(diagnostics.Logger, programs.Object, config.Object), typeof(PicoConnectConnector));
        // Different unsupported values cannot churn the signature cache either.
        for (int i = 0; i < 1000; i++)
        {
            protocol = 100 + i;
            ConnectorFactory.build(diagnostics.Logger, programs.Object, config.Object);
        }
        Assert.AreEqual(2, log.Entries.Count(entry => entry.Name == "PicoConnectProtocol"));
        Assert.IsTrue(log.Entries.Any(entry => entry.Text.Contains("faceTrackingTransferProtocol=2")));
        Assert.IsTrue(log.Entries.Any(entry => entry.Text.Contains("faceTrackingTransferProtocol=3")));
        running = false;
        Assert.IsNull(ConnectorFactory.build(diagnostics.Logger, programs.Object, config.Object));
        Assert.AreEqual(2, log.Entries.Count(entry => entry.Name == "StreamingProgramProbe"));
    }

    [TestMethod]
    public void KeepGregorianMillisecondTimestampsWhenTheCurrentCultureUsesAnotherCalendar()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("th-TH");
            var clock = new DiagnosticTestClock();
            var log = new DiagnosticTestLogger();
            var diagnostics = new PicoDiagnostics(() => log, () => clock.UtcNow);
            diagnostics.Report(LogLevel.Information, "Timestamp", "", () => "test");
            StringAssert.Contains(log.Single("Timestamp").Text, Timestamp(clock.UtcNow));
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [TestMethod]
    public void KeepFirstDetailsForDifferentCodesStagesMessagesAndSeverity()
    {
        var log = new DiagnosticTestLogger();
        var diagnostics = new PicoDiagnostics(() => log);
        diagnostics.Report(LogLevel.Warning, "receive", "", () => "one", new SocketException(10054));
        diagnostics.Report(LogLevel.Warning, "receive", "", () => "two", new SocketException(10050));
        diagnostics.Report(LogLevel.Error, "processing", "", () => "three", new SocketException(10054));
        diagnostics.Report(LogLevel.Warning, "receive", "", () => "four", new InvalidOperationException("a"));
        diagnostics.Report(LogLevel.Warning, "receive", "", () => "five", new InvalidOperationException("b"));
        diagnostics.Report(LogLevel.Error, "receive", "", () => "six", new SocketException(10054));
        Assert.AreEqual(6, log.Entries.Count);
    }

    [TestMethod]
    public void RetainDeduplicationAcrossConnectorReplacementAndFlushOnShutdown()
    {
        var log = new DiagnosticTestLogger();
        var diagnostics = new PicoDiagnostics(() => log);
        new PicoConnectConnector(diagnostics.Logger).Connect();
        new PicoConnectConnector(diagnostics.Logger).Connect();
        Assert.AreEqual(1, log.Entries.Count);
        diagnostics.Flush();
        Assert.AreEqual(2, log.Entries.Count);
        StringAssert.Contains(log.Entries[1].Text, "1 additional occurrences");
    }

    [TestMethod]
    public void NeverLetHostLogWriteFailureEscapeAndRetryTheMissingFirstDetail()
    {
        var log = new DiagnosticTestLogger { FailWrites = true };
        var diagnostics = new PicoDiagnostics(() => log);
        diagnostics.Report(LogLevel.Error, "failure", "", () => "first", new CountedException());
        log.FailWrites = false;
        diagnostics.Report(LogLevel.Error, "failure", "", () => "first", new CountedException());
        Assert.AreEqual(1, log.Entries.Count);
        StringAssert.Contains(log.Entries[0].Text, "complete exception stack");
        StringAssert.Contains(log.Entries[0].Text, "Previous host log write failures=1");
    }

    [TestMethod]
    public void KeepTheFirstStackWhenTheSameFaultReturnsImmediatelyAfterRecovery()
    {
        var clock = new DiagnosticTestClock();
        var log = new DiagnosticTestLogger();
        var diagnostics = new PicoDiagnostics(() => log, () => clock.UtcNow);
        var error = new CountedException();
        diagnostics.Report(LogLevel.Error, "UpdateFailure/processing", "", () => "first", error);
        diagnostics.Flush("UpdateFailure/");
        clock.Advance(1);
        diagnostics.Report(LogLevel.Error, "UpdateFailure/processing", "", () => "first", error);
        diagnostics.Flush("UpdateFailure/");
        Assert.AreEqual(1, error.Formatted);
        Assert.AreEqual(2, log.Entries.Count);
        StringAssert.Contains(log.Entries[1].Text, "1 additional occurrences");
    }

    [TestMethod]
    public void DistinguishNoDatagramsFromDatagramsWithoutFaceSamplesAndReportRecovery()
    {
        var clock = new DiagnosticTestClock();
        var log = new DiagnosticTestLogger();
        var diagnostics = new PicoDiagnostics(() => log, () => clock.UtcNow);
        var receive = new ReceiveDiagnostics(diagnostics, () => true);
        receive.Packet(312);
        receive.ValidPacket(312, Sender);
        receive.EndBatch(1, false);
        DateTime lastPacket = clock.UtcNow;
        clock.Advance(5);
        receive.Timeout();
        receive.EndBatch(0, false);
        Assert.AreEqual(LogLevel.Information, log.Single("NoPackets").Level);
        StringAssert.Contains(log.Single("NoPackets").Text, "cause unknown");
        StringAssert.Contains(log.Single("NoPackets").Text, $"last UDP datagram={Timestamp(lastPacket)}");
        StringAssert.Contains(log.Single("NoPackets").Text, $"last valid sample={Timestamp(lastPacket)}");

        receive.Packet(12);
        receive.ShortPacket(12, Sender);
        receive.EndBatch(0, false);
        Assert.AreEqual(LogLevel.Warning, log.Single("NoValidSamples").Level);
        receive.Packet(312);
        receive.ValidPacket(312, Sender);
        receive.EndBatch(1, false);
        StringAssert.Contains(log.Single("ReceptionResumed").Text, "5.000s");
        Assert.AreEqual(1, log.Entries.Count(entry => entry.Name == "FirstValidSample"));
    }

    [TestMethod]
    public void ExcludeModulePauseAndErrorBackoffFromNoDataDetection()
    {
        var clock = new DiagnosticTestClock();
        var log = new DiagnosticTestLogger();
        var diagnostics = new PicoDiagnostics(() => log, () => clock.UtcNow);
        var receive = new ReceiveDiagnostics(diagnostics, () => true);
        receive.Packet(312);
        receive.ValidPacket(312, Sender);
        receive.EndBatch(1, false);
        clock.Advance(1);
        diagnostics.PauseReception();
        clock.Advance(120);
        diagnostics.ResumeReception();
        receive.EndBatch(0, false);
        Assert.IsFalse(log.Entries.Any(entry => entry.Name == "NoPackets"));
        clock.Advance(4);
        receive.EndBatch(0, false);
        StringAssert.Contains(log.Single("NoPackets").Text, "5.000s");
    }

    [TestMethod]
    public void DoNotMislabelAFailedReceiveAsTheStreamerStoppingItsData()
    {
        var clock = new DiagnosticTestClock();
        var log = new DiagnosticTestLogger();
        var diagnostics = new PicoDiagnostics(() => log, () => clock.UtcNow);
        var receive = new ReceiveDiagnostics(diagnostics, () => true);
        clock.Advance(10);
        receive.EndBatch(0, false, observeHealth: false);
        Assert.IsFalse(log.Entries.Any(entry => entry.Name is "NoPackets" or "NoValidSamples"));
    }

    [TestMethod]
    public void CountShortPacketsWithoutFormattingEveryPacketAndPreserveStatistics()
    {
        var log = new DiagnosticTestLogger();
        var diagnostics = new PicoDiagnostics(() => log);
        var receive = new ReceiveDiagnostics(diagnostics, () => false);
        foreach (int length in new[] { 0, 12, 311 })
        {
            receive.Packet(length);
            receive.ShortPacket(length, Sender);
        }
        receive.EndBatch(0, false);
        Assert.AreEqual(1, log.Entries.Count);
        Assert.AreEqual(LogLevel.Warning, log.Single("ShortPacket").Level);
        receive.Statistics(final: true);
        diagnostics.Flush();
        StringAssert.Contains(log.Single("ReceiveStatistics").Text, "short=3; shortLengthRange=0..311");
        StringAssert.Contains(log.Single("ShortPacketSummary").Text, "2 additional occurrences");
    }

    [TestMethod]
    public unsafe void LogRealUdpShortAndOtherPacketsWhileAcceptingTheLatestValidSample()
    {
        var log = new DiagnosticTestLogger();
        var diagnostics = new PicoDiagnostics(() => log);
        var connector = new LegacyConnector(diagnostics.Logger, PicoPrograms.PicoConnect);
        try
        {
            Assert.IsTrue(connector.Connect());
            using var sender = new UdpClient();
            var target = new IPEndPoint(IPAddress.Loopback, 29765);
            foreach (byte[] packet in new[] { new byte[12], new byte[311], FacePacket(0.2f), new byte[312], FacePacket(0.4f) })
                sender.Send(packet, packet.Length, target);
            float* shapes = connector.GetBlendShapes();
            Assert.IsTrue(shapes != null);
            Assert.AreEqual(0.4f, shapes[(int)BlendShapeIndex.JawOpen]);
            connector.Teardown();
            diagnostics.Flush();
            StringAssert.Contains(log.Single("FirstValidSample").Text, "312 bytes");
            StringAssert.Contains(log.Single("ReceiveStatistics").Text, "packets=5; valid=2");
            StringAssert.Contains(log.Single("ReceiveStatistics").Text, "supersededValid=1");
            Assert.AreEqual(1, log.Entries.Count(entry => entry.Name == "ShortPacket"));
            Assert.AreEqual(LogLevel.Debug, log.Single("OtherPacketType").Level);
        }
        finally { connector.Teardown(); }
    }

    private static IPEndPoint Sender => new(IPAddress.Loopback, 12345);

    [TestMethod]
    public void PreserveDifferentNonFacePacketTypesWithinTheSameBatch()
    {
        var log = new DiagnosticTestLogger();
        var diagnostics = new PicoDiagnostics(() => log);
        var receive = new ReceiveDiagnostics(diagnostics, () => true);
        foreach (byte type in new byte[] { 1, 3, 1 })
        {
            receive.Packet(400);
            receive.OtherType(type, 400, Sender);
        }
        receive.EndBatch(0, false);
        receive.Statistics(final: true);
        diagnostics.Flush();
        Assert.AreEqual(1, log.Entries.Count(entry => entry.Name == "OtherPacketType"));
        StringAssert.Contains(log.Single("OtherPacketType").Text, "otherTypeCounts=[1:2, 3:1]");
        StringAssert.Contains(log.Single("ReceiveStatistics").Text, "otherType=3; otherTypeCounts=[1:2, 3:1]; otherTypeOtherPackets=0");
        StringAssert.Contains(log.Single("OtherPacketTypeSummary").Text, "2 additional occurrences");
    }

    [TestMethod]
    public void BoundLogsForAllNonFaceTypesAcrossBatchesAndPreserveUnrelatedErrorDetails()
    {
        var clock = new DiagnosticTestClock();
        var log = new DiagnosticTestLogger();
        var diagnostics = new PicoDiagnostics(() => log, () => clock.UtcNow);
        var receive = new ReceiveDiagnostics(diagnostics, () => true);
        diagnostics.Report(LogLevel.Error, "UnrelatedError", "", () => "first error detail");
        for (int batch = 0; batch < 100; batch++)
        {
            for (int type = 0; type < 256; type++)
            {
                if (type == 2) continue;
                receive.Packet(400);
                receive.OtherType((byte)type, 400, Sender);
            }
            receive.EndBatch(0, false);
        }
        Assert.AreEqual(2, log.Entries.Count);
        Assert.AreEqual(1, log.Entries.Count(entry => entry.Name == "OtherPacketType"));
        diagnostics.Report(LogLevel.Error, "UnrelatedError", "", () => throw new Exception("First error must remain cached"));
        clock.Advance(30);
        diagnostics.Tick();
        StringAssert.Contains(log.Single("OtherPacketTypeSummary").Text, "25499 additional occurrences");
        receive.Statistics(final: true);
        StringAssert.Contains(log.Single("ReceiveStatistics").Text, "otherType=25500");
        StringAssert.Contains(log.Single("ReceiveStatistics").Text, "otherTypeCounts=[0:100, 1:100, 3:100, 4:100, 5:100]");
        StringAssert.Contains(log.Single("ReceiveStatistics").Text, "otherTypeOtherPackets=25000");
    }

    [TestMethod]
    public void ShowTheMostFrequentOtherTypesAndResetTheirStatisticsBetweenIntervals()
    {
        var clock = new DiagnosticTestClock();
        var log = new DiagnosticTestLogger();
        var diagnostics = new PicoDiagnostics(() => log, () => clock.UtcNow);
        var receive = new ReceiveDiagnostics(diagnostics, () => true);
        foreach (int type in new[] { 0, 1, 3, 4, 5, 6, 6, 6 })
        {
            receive.Packet(400);
            receive.OtherType((byte)type, 400, Sender);
        }
        receive.EndBatch(0, false);
        StringAssert.Contains(log.Single("OtherPacketType").Text, "otherTypeCounts=[6:3, 0:1, 1:1, 3:1, 4:1]");
        StringAssert.Contains(log.Single("OtherPacketType").Text, "otherTypeOtherPackets=1");
        receive.Statistics(final: true);
        clock.Advance(30);
        receive.Packet(400);
        receive.OtherType(9, 400, Sender);
        receive.EndBatch(0, false, observeHealth: false);
        var stats = log.Entries.Last(entry => entry.Name == "ReceiveStatistics");
        StringAssert.Contains(stats.Text, "otherType=1; otherTypeCounts=[9:1]; otherTypeOtherPackets=0");
    }

    [TestMethod]
    public void ReportProlongedMissingDataOnceAndNoticeAStreamingProcessExit()
    {
        var clock = new DiagnosticTestClock();
        var log = new DiagnosticTestLogger();
        var diagnostics = new PicoDiagnostics(() => log, () => clock.UtcNow);
        bool running = true;
        int probes = 0;
        var receive = new ReceiveDiagnostics(diagnostics, () => { probes++; return running; });
        for (int i = 0; i < 7; i++)
        {
            clock.Advance(5);
            receive.Timeout();
            receive.EndBatch(0, false);
            if (i == 0) running = false;
        }
        Assert.AreEqual(1, log.Entries.Count(entry => entry.Name == "NoPackets"));
        Assert.AreEqual(1, log.Entries.Count(entry => entry.Name == "NoDataProlonged"));
        Assert.AreEqual(2, probes);
        Assert.IsTrue(log.Entries.Any(entry => entry.Name == "StreamerProcess" && entry.Text.Contains("running=False")));
    }

    private static byte[] FacePacket(float weight)
    {
        var packet = new byte[312];
        packet[2] = 2;
        BitConverter.GetBytes(weight).CopyTo(packet, 24 + (int)BlendShapeIndex.JawOpen * sizeof(float));
        return packet;
    }

    private static string Timestamp(DateTime utc) =>
        utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture);

    private sealed class CountedException : Exception
    {
        public int Formatted { get; private set; }
        public override string ToString() { Formatted++; return "complete exception stack"; }
    }
}

internal sealed class DiagnosticTestClock
{
    public DateTime UtcNow { get; private set; } = new(2026, 9, 29, 0, 0, 0, 123, DateTimeKind.Utc);
    public void Advance(double seconds) => UtcNow = UtcNow.AddSeconds(seconds);
}

internal sealed class DiagnosticTestLogger : ILogger
{
    public readonly List<(LogLevel Level, string Name, string Text)> Entries = new();
    public bool FailWrites { get; set; }
    public bool IsEnabled(LogLevel level) => true;
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (FailWrites) throw new IOException("Host writer unavailable");
        // Match the host provider: it does not append the exception argument itself.
        Entries.Add((level, eventId.Name!, formatter(state, exception)));
    }
    public (LogLevel Level, string Name, string Text) Single(string name) => Entries.Single(entry => entry.Name == name);
}
