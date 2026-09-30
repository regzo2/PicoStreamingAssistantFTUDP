## English || [简体中文](logging_CN.md)

# PICO module diagnostic logging

The module continues to call the `ILogger` provided by VRCFT synchronously, and adds
no log message queue or batch disk-write thread.
The first occurrence of an event is output immediately; receive-path events such as
short packets are output when the receive round ends, avoiding formatting logs inside
the per-packet loop.
Only the repeat count is kept here, and it cannot be guaranteed that counts not yet
output are saved when the host write fails or the process terminates.

Every module log contains local time, milliseconds, time zone, message number,
connection attempt number and event name, for example:

```text
2026-09-29 21:03:12.140 +08:00 [PICO #12 connection=2 UpdateFailure/receive] ...
```

Exception details are output as part of the first log and contain the complete
information of `Exception.ToString()`.
The summary of the same event references the first message number and contains the
start of the counting round, and the time of the first and last occurrence.
All time fields (including the summary and the last receive time) uniformly use local
time `yyyy-MM-dd HH:mm:ss.fff zzz`, using the Gregorian calendar and fixed numeric
format, and do not change with the system display language.
A multi-line stack belongs to the same timestamped log. Timestamps use the wall clock;
windows and durations in the production path use monotonic timing, avoiding system time
adjustments affecting the diagnostic windows. The connection attempt number means a
local probe/bind attempt, not the headset re-establishing streaming.

## Levels and events

| Level | Events |
| --- | --- |
| Info | Startup and scaling config summary, waiting for/selected streamer, protocol value, UDP bind, first valid sample, start of no data, receive/processing recovery, module pause and exit |
| Warning | Short packets, consecutive absence of valid face samples, no valid data for a long time, connection/receive exceptions, missing or unreadable config, connector cleanup failure |
| Error | Invalid config JSON, scaling config processing failure, eye/expression processing exception, initialization failure |
| Debug | Streamer probe results, config paths, non-face packet types, handled timeouts, receive count/sample coverage/queue limit statistics, expected receive cancellation |

The VRCFT version pinned by the repository logs Debug and above to the file by default,
and the output page shows Info and above.
The module does not modify the host's filtering rules, file flush policy or log file
rotation policy.

## Deduplication and counting

- When the same event occurs consecutively, only the first detail is kept within
  30 seconds. The extra repeat count is output when the period expires, the problem
  recovers or the module exits normally.
- Each event is counted independently; exceptions are distinguished by stage, processing
  operation, level, type, error code, message and the first position of the stack.
  A new error is not suppressed by the window of other errors. An event that occurs again
  after 30 seconds without reappearing keeps its detail again.
- Counts are kept in the module and are preserved across connector rebuilds; at most 128
  signatures are kept, and when exceeded the longest-absent signature is summarized and
  evicted first.
  On recovery the remaining counts are output, but the deduplication signature is kept, so
  that the same failure does not print the complete stack again just because it reappears
  after a brief recovery.
- Short packets are classified by `<312`, and the Debug statistics additionally record the
  count and the length range. All non-face packet types share one
  `OtherPacketType` event and a fixed signature; `tracking_type`, length and source
  address all do not enter the signature of this event.
  The first detail records the count of this round and the type, length and source of the
  first packet, and the repeat packet count is aggregated by a 30 second window.
  Each type is still counted separately in a fixed 256 slot array; the first detail and
  the periodic statistics show the 5 most numerous types within the current statistics
  interval (for equal counts, in ascending order of the type value), in the format
  `otherTypeCounts=[type:packet count, ...]`.
  `otherTypeOtherPackets` means the total packet count of the types not listed, not the
  number of types; the classification counts are cleared after the statistics are output.
- Receive statistics are output every 30 seconds and when a connector closes.
  `supersededValid` means the number of samples in the same round that were superseded by
  an updated valid sample, not network packet loss. When the 1024 packet limit is reached
  and data is still queued, `ReceiveLimit` is recorded.
- A host log write exception does not escape into the tracking flow; on a subsequent
  successful write the number of previous write failures is attached.

## Signature conventions when adding logs

`diagnostics.Logger` deduplicates structured logs by level, event name, event ID, the
unrendered `{OriginalFormat}` message template
and the exception signature; changes in template parameters do not produce a new detail.
Different templates or explicit IDs are not merged with each other just because the
default event name is
`Diagnostic`. The formatter is called only when the first detail needs to be output.

Use fixed templates and placeholders, for example `LogDebug("Count={Count}", count)`; do
not construct the template with string interpolation first.
Events of high frequency, and events that need to show a state change immediately, should
directly use `Report(level, event name, stable signature, message factory)`.
A signature only contains bounded semantic state; do not use packet content, arbitrary
paths or continuously growing counts.
The streamer probe signature contains the presence state of the three streamers; the
protocol signature distinguishes supported legacy from unsupported configurations, and
the concrete protocol value of an unsupported configuration is still kept in the first
detail.

If a custom `ILogger` state has no `{OriginalFormat}`, an explicit event name or non-zero
ID must be provided for each event,
or `Report` must be used instead. For calls that have neither a template nor an explicit
event identity, for compatibility they are still distinguished by the rendered text,
and carry no rate-limiting guarantee for changing text, so they must not be used for
high-frequency logs. The maximum of 128 signatures only limits memory usage and cannot
replace rate limiting.

## No data and recovery

The last receipt of any datagram, the last receipt of a valid face sample and the
recovery after a processing failure are recorded separately.
"Valid" here only means passing the existing length and type checks, not that all fields
or floating point numbers have been verified.

After observing 5 seconds without a valid face sample: when there is no datagram at all,
Info `NoPackets` is recorded;
when packets are still received but cannot be used for face tracking, Warning
`NoValidSamples` is recorded.
When there is no valid sample for 30 seconds, a Warning `NoDataProlonged` is additionally
recorded once.
During no data, the selected streamer is checked at most once every 30 seconds, and Info
is recorded when the process presence state changes.
These events do not close the socket, do not trigger the error backoff, and cannot prove
that the headset is asleep or that the network dropped packets.
The source address is not authenticated, and received short packets cannot be
definitively attributed to PICO.

The receive timeout is still 5000ms; the state is checked after receive returns, so the
detection time may be later than the above thresholds.
During deliberate module pause and exception backoff, reception is not observed, and the
relevant time is excluded from the no-data duration.
Receive recovery and processing recovery are recorded separately; the processing recovery
duration includes the actual exception backoff time.

A `SocketException` / `ObjectDisposedException` escaping the receive stage rebuilds the
local UDP listener,
while other receive exceptions and processing exceptions keep the connector; both record
a 1000ms update backoff.
Service probe/connection failure records a 5000ms connection backoff. The logging
enhancement does not modify these recovery rules.
