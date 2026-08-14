using System.Diagnostics;
using Microsoft.Extensions.Options;
using Sentinela.Agent.Configuration;
using Sentinela.Agent.Recording;

namespace Sentinela.Agent.Workers;

public class ContinuousRecordingWorker : BackgroundService
{
    private readonly IScreenCaptureService _capture;
    private readonly IRecordingStore _store;
    private readonly AgentOptions _options;
    private readonly ILogger<ContinuousRecordingWorker> _logger;

    public ContinuousRecordingWorker(
        IScreenCaptureService capture,
        IRecordingStore store,
        IOptions<AgentOptions> options,
        ILogger<ContinuousRecordingWorker> logger)
    {
        _capture = capture;
        _store = store;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableContinuousRecording)
        {
            _logger.LogInformation("Continuous 24h recording is disabled");
            return;
        }

        var schedule = _options.RecordingSchedule;
        var idleSeconds = Math.Clamp(_options.RecordingIdleSeconds, 2, 120);
        var maxBytes = (long)(Math.Max(1, _options.RecordingMaxBytesGb) * 1024L * 1024L * 1024L);
        _store.SetQuota(maxBytes);

        _logger.LogInformation(
            "Continuous recording started ({Fps} fps, q{Quality}, max {Width}px, idle {Idle}s, cap {CapGb:0} GB, per monitor, schedule {Schedule}) at {Path}",
            _options.RecordingFps, _options.RecordingQuality, _options.RecordingMaxWidth,
            idleSeconds, _options.RecordingMaxBytesGb,
            schedule.Summary(),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Sentinela", "Agent", "recordings"));

        var interval = TimeSpan.FromMilliseconds(Math.Clamp(1000.0 / Math.Max(1, _options.RecordingFps), 50, 10_000));
        var idleDelay = TimeSpan.FromSeconds(15);
        var idleWrite = TimeSpan.FromSeconds(idleSeconds);
        var retention = TimeSpan.FromHours(Math.Max(1, _options.RecordingRetentionHours));
        var lastPurge = DateTime.MinValue;
        var lastHashByMonitor = new Dictionary<int, string>();
        var lastForcedWriteByMonitor = new Dictionary<int, DateTime>();
        var wasInSchedule = schedule.IsActiveNow();
        _store.SetSchedule(wasInSchedule, schedule.Summary());
        _store.Purge(retention, maxBytes);

        while (!stoppingToken.IsCancellationRequested)
        {
            var cycle = Stopwatch.StartNew();
            try
            {
                var inSchedule = schedule.IsActiveNow();
                _store.SetSchedule(inSchedule, schedule.Summary());
                if (inSchedule != wasInSchedule)
                {
                    _logger.LogInformation(inSchedule
                        ? "Recording window started ({Schedule})"
                        : "Recording window ended ({Schedule})", schedule.Summary());
                    if (!inSchedule)
                    {
                        _store.CloseOpenSegments();
                        lastHashByMonitor.Clear();
                        lastForcedWriteByMonitor.Clear();
                    }
                    wasInSchedule = inSchedule;
                }

                var monitors = _capture.GetMonitors();
                _store.SetMonitors(monitors.Select((m, index) => new RecordingMonitorInfo
                {
                    Index = index,
                    Name = m.IsPrimary ? "Principal" : $"Monitor {index + 1}",
                    Width = m.Width,
                    Height = m.Height,
                    IsPrimary = m.IsPrimary
                }).ToList());

                if (inSchedule)
                {
                    for (var i = 0; i < monitors.Count; i++)
                    {
                        var jpeg = await _capture.CaptureForStreamingAsync(_options.RecordingMaxWidth, _options.RecordingQuality, i);
                        if (jpeg is not { Length: > 0 }) continue;

                        var now = DateTime.UtcNow;
                        var hash = FrameHash.Compute(jpeg);
                        lastHashByMonitor.TryGetValue(i, out var previous);
                        lastForcedWriteByMonitor.TryGetValue(i, out var lastForced);
                        var unchanged = hash == previous;
                        if (!unchanged || now - lastForced > idleWrite)
                        {
                            _store.AppendFrame(now, jpeg, i);
                            lastHashByMonitor[i] = hash;
                            lastForcedWriteByMonitor[i] = now;
                        }
                    }
                }

                var nowPurge = DateTime.UtcNow;
                if (nowPurge - lastPurge > TimeSpan.FromMinutes(1))
                {
                    _store.Purge(retention, maxBytes);
                    lastPurge = nowPurge;
                }

                var remaining = (inSchedule ? interval : idleDelay) - cycle.Elapsed;
                if (remaining > TimeSpan.Zero)
                {
                    try { await Task.Delay(remaining, stoppingToken); }
                    catch (OperationCanceledException) { break; }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Continuous recording tick failed");
                try { await Task.Delay(interval, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }
}
