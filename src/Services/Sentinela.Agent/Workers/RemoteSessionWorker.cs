using System.Collections.Concurrent;
using System.Diagnostics;
using Sentinela.Agent.Core.Monitors;
using Sentinela.Agent.Services;

namespace Sentinela.Agent.Workers;

public class RemoteSessionWorker : BackgroundService
{
    private readonly IAgentHubClient _hubClient;
    private readonly IScreenCaptureService _screenCaptureService;
    private readonly ILogger<RemoteSessionWorker> _logger;
    private readonly ConcurrentDictionary<string, SessionStreamState> _activeSessions = new();

    private sealed class SessionStreamState
    {
        public CancellationTokenSource Cts { get; } = new();
        public int? MonitorIndex { get; set; }
    }

    public RemoteSessionWorker(
        IAgentHubClient hubClient,
        IScreenCaptureService screenCaptureService,
        ILogger<RemoteSessionWorker> logger)
    {
        _hubClient = hubClient;
        _screenCaptureService = screenCaptureService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RemoteSessionWorker started");

        _hubClient.RemoteSessionStarted += OnSessionStarted;
        _hubClient.RemoteSessionStopped += OnSessionStopped;
        _hubClient.RemoteSessionMonitorChanged += OnSessionMonitorChanged;

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            foreach (var state in _activeSessions.Values)
                state.Cts.Cancel();
        }
    }

    private void OnSessionStarted(object? sender, RemoteSessionStartedEventArgs e)
    {
        _logger.LogInformation("Remote session {SessionId} started ({Type})", e.SessionId, e.SessionType);

        if (_activeSessions.ContainsKey(e.SessionId)) return;

        var state = new SessionStreamState { MonitorIndex = e.MonitorIndex };
        _activeSessions[e.SessionId] = state;
        _ = Task.Run(() => StreamSessionAsync(e.SessionId, state, state.Cts.Token));
    }

    private void OnSessionStopped(object? sender, RemoteSessionStoppedEventArgs e)
    {
        _logger.LogInformation("Remote session {SessionId} stopped", e.SessionId);

        if (_activeSessions.TryRemove(e.SessionId, out var state))
            state.Cts.Cancel();
    }

    private void OnSessionMonitorChanged(object? sender, RemoteSessionMonitorChangedEventArgs e)
    {
        if (!_activeSessions.TryGetValue(e.SessionId, out var state)) return;
        state.MonitorIndex = e.MonitorIndex;
        _logger.LogInformation("Remote session {SessionId} switched to monitor {MonitorIndex}", e.SessionId, e.MonitorIndex);
    }

    private async Task StreamSessionAsync(string sessionId, SessionStreamState state, CancellationToken ct)
    {
        long frameNumber = 0;
        var interval = TimeSpan.FromMilliseconds(150);

        while (!ct.IsCancellationRequested)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var frame = await _screenCaptureService.CaptureForStreamingAsync(maxWidth: 1920, quality: 60, monitorIndex: state.MonitorIndex);
                if (frame != null && frame.Length > 0)
                {
                    frameNumber++;
                    await _hubClient.SendRemoteScreenFrameAsync(new RemoteScreenFrameData
                    {
                        SessionId = sessionId,
                        FrameData = frame,
                        FrameNumber = frameNumber,
                        Timestamp = DateTime.UtcNow
                    }, ct);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stream frame for session {SessionId}", sessionId);
            }

            sw.Stop();
            var remaining = interval - sw.Elapsed;
            if (remaining > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(remaining, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("Stream for session {SessionId} ended after {Frames} frames", sessionId, frameNumber);
    }
}
