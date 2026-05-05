using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Microsoft.Extensions.Logging;

namespace Pico4SAFTExtTrackingModule.PicoConnectors;

/// <summary>
/// Connector class for Streaming Assitant & Business Streaming.
/// Also used for PICO Connect on `mergetype=2`
/// </summary>
public sealed partial class LegacyConnector : IPicoConnector
{
    private const string IP_ADDRESS = "127.0.0.1";
    private const int PORT_NUMBER = 29765;

    private static readonly int s_pxrHeaderSize = Unsafe.SizeOf<TrackingDataHeader>();
    private static readonly int s_pxrFtInfoSize = Unsafe.SizeOf<PxrFTInfo>();
    private static readonly int s_packetIndex = s_pxrHeaderSize;
    private static readonly int s_packetSize = s_pxrHeaderSize + s_pxrFtInfoSize;

    private readonly ILogger _logger;
    private readonly string _processName;

    private volatile int _ready; // 0 created 1 success -1 fail
    private volatile bool _success;
    private PxrFTInfo _data;
    private Task _task = Task.CompletedTask;
    private CancellationTokenSource? _cancellationTokenSource;

    public LegacyConnector(ILogger Logger, PicoPrograms program_using)
    {
        _logger = Logger;

        _processName = program_using switch
        {
            PicoPrograms.StreamingAssistant => "Streaming Assistant",
            PicoPrograms.BusinessStreamingV1 or PicoPrograms.BusinessStreaming => "Business Streaming",
            PicoPrograms.PicoConnect => "PICO Connect",
            _ => string.Empty,
        };

        if (string.IsNullOrEmpty(_processName))
        {
            // shouldn't reach this
            LogWarningUnknownProcess(program_using);
            _processName = "[?]";
        }
    }

    public string GetProcessName() => _processName;

    public bool Connect()
    {
        for (int retry = 0; retry < 3; retry++)
        {
            _cancellationTokenSource?.Cancel();
            _task.Wait();

            _cancellationTokenSource = new();
            _task = StartListening(_cancellationTokenSource.Token);

            SpinWait.SpinUntil(() => Interlocked.CompareExchange(ref _ready, 1, 1) is not 0);

            return _ready is 1;
        }

        return false;
    }

    public ReadOnlySpan<float> GetBlendShapes()
    {
        if (!Interlocked.CompareExchange(ref _success, false, true))
            return [];

        return _data.blendShapeWeight;
    }

    public void Teardown()
    {
        LogTeardown();

        _cancellationTokenSource?.Cancel();
        _task.Wait();
    }

    private async Task StartListening(CancellationToken cancellationToken)
    {
        Interlocked.Exchange(ref _ready, 0);
        byte[] buffer = GC.AllocateUninitializedArray<byte>(s_packetSize * 2);
        IPEndPoint endPoint = new(IPAddress.Parse(IP_ADDRESS), PORT_NUMBER);
        using Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Any, PORT_NUMBER));

        LogDebugHostEndpoint(endPoint);
        LogDebugTimeout(socket.ReceiveTimeout);
        LogDebugEstablished();

        LogWaiting(_processName);

        try
        {
            if (!await ReceivePxrDataAsync(cancellationToken))
            {
                Interlocked.Exchange(ref _ready, -1);

                return;
            }
        }
        catch (SocketException ex) when (ex.ErrorCode is 10048)
        {
            // pico_et_ft_bt_bridge.exe is obsoluted
            Interlocked.Exchange(ref _ready, -1);
            return;
        }
        catch (Exception e)
        {
            LogWarning(e);
            Interlocked.Exchange(ref _ready, -1);
            return;
        }

        LogHandshakeSuccess(_processName);
        socket.ReceiveTimeout = 5000;

        Interlocked.Exchange(ref _ready, 1);
        while (true)
        {
            Interlocked.Exchange(ref _success, await ReceivePxrDataAsync(cancellationToken));
        }

        async Task<bool> ReceivePxrDataAsync(CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await socket.ReceiveFromAsync(buffer, endPoint, cancellationToken);
                if (result.ReceivedBytes is 0)
                    return false;

                var span = buffer.AsSpan(..result.ReceivedBytes);

                // 0 copy cast
                ref var tdh = ref MemoryMarshal.AsRef<TrackingDataHeader>(span);
                if (tdh.tracking_type != 2)
                    return false;

                // clone
                _data = MemoryMarshal.AsRef<PxrFTInfo>(span[s_packetIndex..]);

                return true;
            }
            catch (SocketException ex) when (ex.ErrorCode is 10060)
            {
                // socket time out
                LogDebugReceivePxrDataError(ex);
            }
            catch (SocketException ex) when (ex.ErrorCode is 10004)
            {
                LogSocketClosed();
            }
            return false;
        }
    }

    [LoggerMessage(LogLevel.Warning, "Unhandled Exception")]
    private partial void LogWarning(Exception exception);

    [LoggerMessage(LogLevel.Warning, "Couldn't find the name for program {type}")]
    private partial void LogWarningUnknownProcess(PicoPrograms type);

    [LoggerMessage(LogLevel.Debug, "Host end-point: {endPoint}")]
    private partial void LogDebugHostEndpoint(EndPoint endPoint);

    [LoggerMessage(LogLevel.Debug, "Initialization Timeout: {timeout}ms")]
    private partial void LogDebugTimeout(int timeout);

    [LoggerMessage(LogLevel.Debug, "Client established: attempting to receive PxrFTInfo.")]
    private partial void LogDebugEstablished();

    [LoggerMessage(LogLevel.Debug, "Data was not sent within the timeout.")]
    private partial void LogDebugReceivePxrDataError(Exception exception);

    [LoggerMessage(LogLevel.Information, "Waiting for {process} data stream.")]
    private partial void LogWaiting(string process);

    [LoggerMessage(LogLevel.Information, "{process} handshake success.")]
    private partial void LogHandshakeSuccess(string process);

    [LoggerMessage(LogLevel.Information, "Disposing of PxrFaceTracking UDP Client.")]
    private partial void LogTeardown();

    [LoggerMessage(LogLevel.Information, "Data was not sent within the timeout (is headset hibernated?), reinitialize...")]
    private partial void LogReInitialize();

    [LoggerMessage(LogLevel.Information, "Socket closed")]
    private partial void LogSocketClosed();
}
