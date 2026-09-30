using System.Net;
using Microsoft.Extensions.Logging;

namespace Pico4SAFTExtTrackingModule.Diagnostics;

internal sealed class ReceiveDiagnostics
{
    private readonly PicoDiagnostics diagnostics;
    private readonly Func<bool> isProgramRunning;
    private TimeSpan lastPacket, lastValid, nextStatistics, nextProcessCheck;
    private DateTime? lastPacketUtc, lastValidUtc;
    private bool firstValid, prolongedSilence;
    private bool? programRunning;
    private string health = "waiting";
    private long packets, bytes, valid, shorts, otherTypes, superseded, limits, timeouts;
    private int minimumShort = int.MaxValue, maximumShort;
    private long batchShorts, batchOtherTypes;
    // Untrusted tracking_type bytes select bounded counters, never log signatures.
    // These counts cover the statistics interval and are cleared only by Statistics().
    private readonly long[] otherTypeCounts = new long[256];
    private int firstShortLength;
    private byte firstOtherType;
    private int firstOtherLength;
    private IPEndPoint? firstOtherSender;
    private IPEndPoint? firstShortSender, validSender;
    private int validLength;
    private TimeSpan silenceStarted;

    public ReceiveDiagnostics(PicoDiagnostics diagnostics, Func<bool> isProgramRunning)
    {
        this.diagnostics = diagnostics;
        this.isProgramRunning = isProgramRunning;
        lastPacket = lastValid = diagnostics.ReceptionElapsed;
        nextStatistics = diagnostics.Elapsed + TimeSpan.FromSeconds(30);
    }

    public void Packet(int length)
    {
        packets++;
        bytes += length;
        lastPacket = diagnostics.ReceptionElapsed;
        lastPacketUtc = diagnostics.UtcNow;
    }

    public void ShortPacket(int length, IPEndPoint? sender)
    {
        shorts++;
        if (batchShorts++ == 0) (firstShortLength, firstShortSender) = (length, sender);
        minimumShort = Math.Min(minimumShort, length);
        maximumShort = Math.Max(maximumShort, length);
    }

    public void OtherType(byte type, int length, IPEndPoint? sender)
    {
        otherTypes++;
        if (batchOtherTypes++ == 0) (firstOtherType, firstOtherLength, firstOtherSender) = (type, length, sender);
        otherTypeCounts[type]++;
    }

    public void ValidPacket(int length, IPEndPoint? sender)
    {
        valid++;
        lastValid = lastPacket;
        lastValidUtc = lastPacketUtc;
        validSender = sender;
        validLength = length;
    }

    public void EndBatch(int validInBatch, bool limitReached, bool observeHealth = true)
    {
        superseded += Math.Max(0, validInBatch - 1);
        if (batchShorts > 0)
            diagnostics.Report(LogLevel.Warning, "ShortPacket", "length<312", () =>
                $"Received short UDP datagram from {firstShortSender}: {firstShortLength} bytes; " +
                "minimum=312. Skipped; listener retained; no error backoff.", occurrences: batchShorts);
        if (batchOtherTypes > 0)
            diagnostics.Report(LogLevel.Debug, "OtherPacketType", "", () =>
                $"Skipped {batchOtherTypes} non-face UDP datagrams in this update; first from {firstOtherSender}: " +
                $"{firstOtherLength} bytes, tracking_type={firstOtherType}; expected=2; " +
                $"interval {OtherTypeDistribution()}; no error backoff.", occurrences: batchOtherTypes);
        batchShorts = batchOtherTypes = 0;
        if (limitReached)
        {
            limits++;
            diagnostics.Report(LogLevel.Debug, "ReceiveLimit", "1024", () =>
                "Received 1024 datagrams in one update and data is still queued; continuing on the next update.");
        }

        if (validInBatch > 0)
        {
            if (!firstValid)
                diagnostics.Report(LogLevel.Information, "FirstValidSample", $"{validSender}/{validLength}", () =>
                    $"First face sample accepted from {validSender}: {validLength} bytes. " +
                    "This sender has not been authenticated.");
            else if (health != "receiving")
                diagnostics.Report(LogLevel.Information, "ReceptionResumed", "", () =>
                    $"Valid face samples resumed after {(diagnostics.ReceptionElapsed - silenceStarted).TotalSeconds:F3}s " +
                    "of observed interruption (deliberate pauses/backoff excluded).");
            firstValid = true;
            if (health != "receiving") diagnostics.Flush("No");
            health = "receiving";
            prolongedSilence = false;
        }
        if (observeHealth) CheckHealth();
        Statistics();
        diagnostics.Tick();
    }

    public void Timeout()
    {
        timeouts++;
        diagnostics.Report(LogLevel.Debug, "ReceiveTimeout", "10060", () =>
            "Receive timed out (10060); listener retained; no error backoff.");
    }

    private void CheckHealth()
    {
        var now = diagnostics.ReceptionElapsed;
        var noPacketFor = now - lastPacket;
        var noValidFor = now - lastValid;
        if (noValidFor < TimeSpan.FromSeconds(5)) return;
        string state = noPacketFor >= TimeSpan.FromSeconds(5) ? "no-packets" : "no-valid-samples";
        if (state != health)
        {
            if (health is "waiting" or "receiving") silenceStarted = lastValid;
            health = state;
            diagnostics.Report(state == "no-packets" ? LogLevel.Information : LogLevel.Warning,
                state == "no-packets" ? "NoPackets" : "NoValidSamples", "", () =>
                    $"{state}: no valid face sample for {noValidFor.TotalSeconds:F3}s; " +
                    $"last UDP datagram={Format(lastPacketUtc)}; last valid sample={Format(lastValidUtc)}. " +
                    "Listener retained; cause unknown (headset sleep cannot be confirmed).");
        }
        if (noValidFor >= TimeSpan.FromSeconds(30) && !prolongedSilence)
        {
            prolongedSilence = true;
            diagnostics.Report(LogLevel.Warning, "NoDataProlonged", "", () =>
                $"No valid face sample for {noValidFor.TotalSeconds:F3}s; state={health}. " +
                "Listener retained; no reconnect triggered by missing data.");
        }
        if (diagnostics.Elapsed < nextProcessCheck) return;
        nextProcessCheck = diagnostics.Elapsed + TimeSpan.FromSeconds(30);
        try
        {
            bool running = isProgramRunning();
            if (running != programRunning)
                diagnostics.Report(LogLevel.Information, "StreamerProcess", running.ToString(), () =>
                    $"During missing data, selected streaming program running={running}. " +
                    "Process presence does not establish that face data is being sent.");
            programRunning = running;
        }
        catch (Exception ex)
        {
            diagnostics.Report(LogLevel.Debug, "StreamerProcessCheckFailed", "", () =>
                "Could not check the streaming program while diagnosing missing data.", ex);
        }
    }

    private static string Format(DateTime? time) => time is { } utc
        ? PicoDiagnostics.FormatTimestamp(utc) : "never received";

    private string OtherTypeDistribution()
    {
        // Only format on a first detail or a statistics write, never for every packet.
        // Show up to five most frequent types, with numeric type order breaking ties.
        var top = otherTypeCounts.Select((count, type) => (Type: type, Count: count))
            .Where(entry => entry.Count > 0).OrderByDescending(entry => entry.Count)
            .ThenBy(entry => entry.Type).Take(5).ToArray();
        return $"otherTypeCounts=[{string.Join(", ", top.Select(entry => $"{entry.Type}:{entry.Count}"))}]; " +
               $"otherTypeOtherPackets={otherTypes - top.Sum(entry => entry.Count)}";
    }

    public void Statistics(bool final = false)
    {
        if (!final && diagnostics.Elapsed < nextStatistics) return;
        if (packets + timeouts > 0)
            diagnostics.Report(LogLevel.Debug, "ReceiveStatistics", diagnostics.Elapsed.Ticks.ToString(), () =>
                $"{(final ? "Final" : "Interval")} UDP counts: packets={packets}; valid={valid}; " +
                $"short={shorts}; shortLengthRange={(shorts == 0 ? "none" : $"{minimumShort}..{maximumShort}")}; " +
                $"otherType={otherTypes}; {OtherTypeDistribution()}; " +
                $"supersededValid={superseded}; drainLimit={limits}; timeouts={timeouts}; bytes={bytes}. " +
                "Counts do not establish a network packet-loss rate.");
        packets = bytes = valid = shorts = otherTypes = superseded = limits = timeouts = 0;
        Array.Clear(otherTypeCounts);
        minimumShort = int.MaxValue;
        maximumShort = 0;
        nextStatistics = diagnostics.Elapsed + TimeSpan.FromSeconds(30);
    }
}
