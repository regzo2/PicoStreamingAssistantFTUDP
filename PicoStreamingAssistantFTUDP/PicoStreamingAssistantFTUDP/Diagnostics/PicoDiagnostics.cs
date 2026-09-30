using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Pico4SAFTExtTrackingModule.Diagnostics;

// This object lives as long as the module, including across connector replacements.
// It buffers counts only: the first message and exception are sent to the host immediately.
internal sealed class PicoDiagnostics
{
    private static readonly TimeSpan RepeatWindow = TimeSpan.FromSeconds(30);
    private const int MaximumSignatures = 128;
    private readonly Func<ILogger> output;
    private readonly Func<DateTime>? utcNow;
    private readonly Stopwatch stopwatch = Stopwatch.StartNew();
    private readonly DateTime started;
    private readonly object gate = new();
    private readonly Dictionary<string, RepeatedEvent> events = new();
    private TimeSpan nextPoll;
    private TimeSpan excludedReceptionTime;
    private TimeSpan? receptionPausedAt;
    private long writeFailures;

    public PicoDiagnostics(Func<ILogger> output, Func<DateTime>? utcNow = null)
    {
        this.output = output;
        this.utcNow = utcNow;
        started = UtcNow;
        Logger = new DiagnosticLogger(this);
    }

    // Structured ILogger calls group by EventId and the unrendered message template,
    // ignoring argument values. Use Report with a stable, bounded signature for high-
    // frequency events or when a semantic state change must produce a new first detail.
    // Do not interpolate variable values into ILogger message templates.
    public ILogger Logger { get; }
    public DateTime UtcNow => utcNow?.Invoke() ?? DateTime.UtcNow;
    public TimeSpan Elapsed => utcNow == null ? stopwatch.Elapsed : UtcNow - started;
    public int ConnectionAttempt { get; private set; }
    private ILogger Output => output() ?? NullLogger.Instance;

    public static string FormatTimestamp(DateTime utc) =>
        utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture);

    // Reception health excludes deliberate module pauses and error backoff.
    public TimeSpan ReceptionElapsed => (receptionPausedAt ?? Elapsed) - excludedReceptionTime;

    public static PicoDiagnostics ForLogger(ILogger logger) => logger is DiagnosticLogger diagnostic
        ? diagnostic.Owner
        : new PicoDiagnostics(() => logger);

    public void BeginConnectionAttempt() => ConnectionAttempt++;
    public void PauseReception() => receptionPausedAt ??= Elapsed;

    public void ResumeReception()
    {
        if (receptionPausedAt is { } paused)
        {
            excludedReceptionTime += Elapsed - paused;
            receptionPausedAt = null;
        }
    }

    public void Report(LogLevel level, string name, string signature, Func<string> message,
        Exception? exception = null, long occurrences = 1)
    {
        lock (gate)
        {
            if (!Output.IsEnabled(level)) return;
            var now = Elapsed;
            string key = level + "/" + name + "/" + signature + ExceptionSignature(exception);
            if (events.TryGetValue(key, out var previous))
            {
                if (now - previous.LastElapsed < RepeatWindow)
                {
                    if (now >= previous.NextSummary) Summarize(previous, now);
                    previous.Repeats += occurrences;
                    previous.LastElapsed = now;
                    previous.LastUtc = UtcNow;
                    return;
                }
                Summarize(previous, now);
                events.Remove(key);
            }

            if (events.Count >= MaximumSignatures)
            {
                var oldest = events.MinBy(entry => entry.Value.LastElapsed);
                Summarize(oldest.Value, now);
                events.Remove(oldest.Key);
            }
            var entry = new RepeatedEvent(level, name, UtcNow, now);
            entry.Repeats = occurrences - 1;
            entry.DetailNumber = messageNumber + 1;
            events.Add(key, entry);
            if (!Write(level, name, message(), exception)) events.Remove(key);
        }
    }

    // Called by the host's existing update loop, including while backing off or idle.
    // No timer thread or delayed queue of log messages is introduced.
    public void Tick()
    {
        lock (gate)
        {
            var now = Elapsed;
            if (now < nextPoll) return;
            nextPoll = now + TimeSpan.FromSeconds(1);
            foreach (var entry in events.Values)
                if (now >= entry.NextSummary) Summarize(entry, now);
        }
    }

    public void Flush(string? namePrefix = null, bool forget = false)
    {
        lock (gate)
        {
            var now = Elapsed;
            foreach (var key in events.Where(entry => namePrefix == null ||
                         entry.Value.Name.StartsWith(namePrefix, StringComparison.Ordinal))
                         .Select(entry => entry.Key).ToArray())
            {
                Summarize(events[key], now);
                // Recovery flushes counts, but a brief recovery must not cause the
                // same exception stack to be printed again inside its repeat window.
                if (forget) events.Remove(key);
            }
        }
    }

    private void Summarize(RepeatedEvent entry, TimeSpan now)
    {
        if (entry.Repeats > 0 && !Write(entry.Level, entry.Name + "Summary",
                $"{entry.Repeats} additional occurrences; since={FormatTimestamp(entry.WindowStartedUtc)}; " +
                $"first={FormatTimestamp(entry.FirstUtc)}; last={FormatTimestamp(entry.LastUtc)}; " +
                $"first detail=#{entry.DetailNumber}.", null)) return;
        entry.Repeats = 0;
        entry.NextSummary = now + RepeatWindow;
        entry.WindowStartedUtc = UtcNow;
    }

    private long messageNumber;

    private bool Write(LogLevel level, string name, string message, Exception? exception)
    {
        try
        {
            // The pinned VRCFT provider ignores the exception argument, so materialize
            // exception.ToString() in the first entry's text as well as passing it separately.
            string detail = exception == null ? "" : Environment.NewLine + exception;
            string failures = writeFailures == 0 ? "" : $" Previous host log write failures={writeFailures}.";
            long number = ++messageNumber;
            Output.Log(level, new EventId(0, name), exception,
                "{Timestamp} [PICO #{Number} connection={Connection} {Event}] {Message}{Failures}{ExceptionDetail}",
                FormatTimestamp(UtcNow), number,
                ConnectionAttempt, name, message, failures, detail);
            writeFailures = 0;
            return true;
        }
        catch (Exception)
        {
            // A failing host logger must not escape into tracking or recursively log itself.
            writeFailures++;
            return false;
        }
    }

    private static string ExceptionSignature(Exception? exception)
    {
        if (exception == null) return "";
        string code = exception is SocketException socket ? socket.ErrorCode.ToString() : "";
        string site = exception.StackTrace?.Split('\n', 2)[0].Trim() ?? exception.TargetSite?.ToString() ?? "";
        return $"/{exception.GetType().FullName}/{code}/{exception.Message}/{site}" +
               (exception.InnerException == null ? "" : ExceptionSignature(exception.InnerException));
    }

    private sealed class RepeatedEvent
    {
        public RepeatedEvent(LogLevel level, string name, DateTime utc, TimeSpan elapsed)
        {
            Level = level;
            Name = name;
            FirstUtc = LastUtc = utc;
            WindowStartedUtc = utc;
            LastElapsed = elapsed;
            NextSummary = elapsed + RepeatWindow;
        }

        public LogLevel Level { get; }
        public string Name { get; }
        public DateTime FirstUtc { get; }
        public DateTime LastUtc { get; set; }
        public DateTime WindowStartedUtc { get; set; }
        public TimeSpan LastElapsed { get; set; }
        public TimeSpan NextSummary { get; set; }
        public long Repeats { get; set; }
        public long DetailNumber { get; set; }
    }

    private sealed class DiagnosticLogger : ILogger
    {
        public DiagnosticLogger(PicoDiagnostics owner) => Owner = owner;
        public PicoDiagnostics Owner { get; }
        public bool IsEnabled(LogLevel logLevel) => Owner.Output.IsEnabled(logLevel);
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            string? template = OriginalFormat(state);
            if (template != null || eventId.Id != 0 || eventId.Name != null)
            {
                Owner.Report(logLevel, eventId.Name ?? "Diagnostic", $"{eventId.Id}/{template}",
                    () => formatter(state, exception), exception);
                return;
            }

            // Custom ILogger states without a template or an explicit EventId have no
            // stable identity. Preserve their details for compatibility; these callers
            // must use Report (or a specific EventId) before logging at high frequency.
            string text = formatter(state, exception);
            Owner.Report(logLevel, eventId.Name ?? "Diagnostic", text, () => text, exception);
        }

        private static string? OriginalFormat<TState>(TState state)
        {
            if (state is IReadOnlyList<KeyValuePair<string, object?>> values)
            {
                for (int i = values.Count - 1; i >= 0; i--)
                    if (values[i].Key == "{OriginalFormat}") return values[i].Value as string;
            }
            else if (state is IEnumerable<KeyValuePair<string, object?>> fields)
            {
                foreach (var field in fields)
                    if (field.Key == "{OriginalFormat}") return field.Value as string;
            }
            return null;
        }
    }
}
