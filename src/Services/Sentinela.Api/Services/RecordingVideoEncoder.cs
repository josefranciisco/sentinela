using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace Sentinela.Api.Services;

public class RecordingVideoEncoder
{
    private readonly ILogger<RecordingVideoEncoder> _logger;

    public RecordingVideoEncoder(ILogger<RecordingVideoEncoder> logger)
    {
        _logger = logger;
    }

    public async Task<string> ZipToMp4Async(string zipPath, string mp4Path, CancellationToken ct = default)
    {
        var workDir = Path.Combine(Path.GetTempPath(), "sentinela-rec-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            ZipFile.ExtractToDirectory(zipPath, workDir);
            var frames = Directory.GetFiles(workDir, "*.jpg", SearchOption.AllDirectories)
                .Select(path => (Path: path, At: ParseTimestamp(path)))
                .OrderBy(f => f.At)
                .ThenBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (frames.Count == 0)
                throw new InvalidOperationException("A exportação não contém quadros de gravação.");

            var listPath = Path.Combine(workDir, "frames.txt");
            await File.WriteAllTextAsync(listPath, BuildConcatList(frames), new UTF8Encoding(false), ct);

            var ffmpeg = ResolveFfmpeg();
            var args =
                $"-y -f concat -safe 0 -i \"{listPath}\" " +
                "-vf \"fps=10,scale=trunc(iw/2)*2:trunc(ih/2)*2,format=yuv420p\" " +
                "-c:v libx264 -preset veryfast -crf 20 -movflags +faststart " +
                $"\"{mp4Path}\"";

            var (exit, stderr) = await RunAsync(ffmpeg, args, TimeSpan.FromMinutes(8), ct);
            if (exit != 0 || !File.Exists(mp4Path) || new FileInfo(mp4Path).Length == 0)
                throw new InvalidOperationException("Falha ao gerar o vídeo MP4. " + TrimLog(stderr));

            return mp4Path;
        }
        finally
        {
            try { Directory.Delete(workDir, recursive: true); } catch { /* ignore */ }
        }
    }

    private static string BuildConcatList(List<(string Path, DateTime At)> frames)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < frames.Count; i++)
        {
            var current = frames[i];
            var nextAt = i + 1 < frames.Count ? frames[i + 1].At : current.At.AddMilliseconds(125);
            var duration = Math.Clamp((nextAt - current.At).TotalSeconds, 0.04, 3600);
            sb.Append("file ").Append(FfmpegPath(current.Path)).AppendLine();
            sb.Append("duration ").Append(duration.ToString("0.###", CultureInfo.InvariantCulture)).AppendLine();
        }

        sb.Append("file ").Append(FfmpegPath(frames[^1].Path)).AppendLine();
        return sb.ToString();
    }

    private static DateTime ParseTimestamp(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        if (long.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks)
            && ticks > DateTime.MinValue.Ticks && ticks < DateTime.MaxValue.Ticks)
            return new DateTime(ticks, DateTimeKind.Utc);
        if (DateTime.TryParseExact(name, "yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var withMs))
            return withMs;
        if (DateTime.TryParseExact(name, "yyyyMMdd-HHmmss", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var ts))
            return ts;
        return File.GetLastWriteTimeUtc(path);
    }

    private static string FfmpegPath(string path)
        => "'" + path.Replace('\\', '/').Replace("'", "'\\''") + "'";

    private static string ResolveFfmpeg()
    {
        var configured = Environment.GetEnvironmentVariable("FFMPEG_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return configured;
        return "ffmpeg";
    }

    private async Task<(int Exit, string Stderr)> RunAsync(string fileName, string arguments, TimeSpan timeout, CancellationToken ct)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        var stderr = new StringBuilder();
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data)) stderr.AppendLine(e.Data);
        };

        _logger.LogInformation("Encoding recording video: {File} {Args}", fileName, arguments);
        if (!process.Start())
            throw new InvalidOperationException("Não foi possível iniciar o ffmpeg.");

        process.BeginErrorReadLine();
        _ = process.StandardOutput.ReadToEndAsync(ct);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            throw new TimeoutException("A geração do vídeo excedeu o tempo limite.");
        }

        return (process.ExitCode, stderr.ToString());
    }

    private static string TrimLog(string stderr)
    {
        var text = stderr.Trim();
        return text.Length <= 400 ? text : text[^400..];
    }
}
