using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Sentinela.Agent.Recording;

public interface IRecordingStore
{
    RecordingStatus GetStatus();
    void SetMonitors(IReadOnlyList<RecordingMonitorInfo> monitors);
    void SetSchedule(bool inSchedule, string? summary);
    void CloseOpenSegments();
    void SetQuota(long maxBytes);
    void AppendFrame(DateTime utc, byte[] jpeg, int monitorIndex);
    byte[]? GetFrame(DateTime utc, int monitorIndex = 0);
    string CreateJpegZip(DateTime fromUtc, DateTime toUtc, int monitorIndex = 0);
    void Purge(TimeSpan retention, long maxBytes);
}

public class RecordingMonitorInfo
{
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsPrimary { get; set; }
}

public class RecordingStatus
{
    public bool Enabled { get; set; } = true;
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public long Bytes { get; set; }
    public int SegmentCount { get; set; }
    public bool InSchedule { get; set; } = true;
    public string? ScheduleSummary { get; set; }
    public long MaxBytes { get; set; }
    public List<RecordingMonitorInfo> Monitors { get; set; } = [];
    public List<RecordingSegmentInfo> Segments { get; set; } = [];
}

public class RecordingSegmentInfo
{
    public int MonitorIndex { get; set; }
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
}

public sealed class RecordingStore : IRecordingStore, IDisposable
{
    public const string Magic = "SREC";
    public const ushort Version = 1;

    private readonly string _root;
    private readonly object _gate = new();
    private readonly Dictionary<int, OpenSegment> _open = new();
    private readonly Dictionary<int, (DateTime Utc, byte[] Jpeg)> _lastFrame = new();
    private readonly TimeSpan _segmentLength = TimeSpan.FromMinutes(5);
    private List<RecordingMonitorInfo> _monitors = [];
    private bool _inSchedule = true;
    private string? _scheduleSummary;
    private long _maxBytes;

    public RecordingStore()
    {
        _root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Sentinela", "Agent", "recordings");
        Directory.CreateDirectory(_root);
        LoadMonitors();
    }

    public void SetMonitors(IReadOnlyList<RecordingMonitorInfo> monitors)
    {
        lock (_gate)
        {
            _monitors = monitors.ToList();
            var path = Path.Combine(_root, "monitors.json");
            File.WriteAllText(path, JsonSerializer.Serialize(_monitors));
        }
    }

    public void SetSchedule(bool inSchedule, string? summary)
    {
        lock (_gate)
        {
            _inSchedule = inSchedule;
            _scheduleSummary = summary;
        }
    }

    public void SetQuota(long maxBytes)
    {
        lock (_gate)
        {
            _maxBytes = maxBytes;
        }
    }

    public void CloseOpenSegments()
    {
        lock (_gate)
        {
            foreach (var segment in _open.Values)
                segment.Stream.Dispose();
            _open.Clear();
            _lastFrame.Clear();
        }
    }

    public RecordingStatus GetStatus()
    {
        lock (_gate)
        {
            FlushCurrent();
            var files = ListSegments();
            long bytes = 0;
            foreach (var file in files)
                bytes += new FileInfo(file.Path).Length;

            var indexes = files.Select(f => f.MonitorIndex).Distinct().OrderBy(i => i).ToList();
            if (indexes.Count == 0 && _monitors.Count > 0)
                indexes = _monitors.Select(m => m.Index).ToList();

            var monitors = indexes.Select(i =>
            {
                var known = _monitors.FirstOrDefault(m => m.Index == i);
                return known ?? new RecordingMonitorInfo
                {
                    Index = i,
                    Name = i == 0 ? "Principal" : $"Monitor {i + 1}",
                    IsPrimary = i == 0
                };
            }).ToList();

            var now = DateTime.UtcNow;
            var openPaths = _open.Values.Select(s => s.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var segments = new List<RecordingSegmentInfo>();
            foreach (var group in files.GroupBy(f => f.MonitorIndex).OrderBy(g => g.Key))
            {
                RecordingSegmentInfo? current = null;
                foreach (var file in group.OrderBy(f => f.StartUtc))
                {
                    var end = openPaths.Contains(file.Path)
                        ? now
                        : file.StartUtc + _segmentLength;
                    if (end > now) end = now;
                    if (end <= file.StartUtc) continue;

                    if (current is not null && file.StartUtc <= current.ToUtc.AddSeconds(90))
                    {
                        if (end > current.ToUtc) current.ToUtc = end;
                        continue;
                    }

                    current = new RecordingSegmentInfo
                    {
                        MonitorIndex = group.Key,
                        FromUtc = file.StartUtc,
                        ToUtc = end
                    };
                    segments.Add(current);
                }
            }

            return new RecordingStatus
            {
                Enabled = true,
                FromUtc = files.Count > 0 ? files.Min(f => f.StartUtc) : null,
                ToUtc = files.Count > 0 ? now : null,
                Bytes = bytes,
                SegmentCount = files.Count,
                Monitors = monitors,
                Segments = segments,
                InSchedule = _inSchedule,
                ScheduleSummary = _scheduleSummary,
                MaxBytes = _maxBytes
            };
        }
    }

    public void AppendFrame(DateTime utc, byte[] jpeg, int monitorIndex)
    {
        if (jpeg.Length == 0) return;
        utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);

        lock (_gate)
        {
            RotateIfNeeded(utc, monitorIndex);
            if (!_open.TryGetValue(monitorIndex, out var segment)) return;

            WriteInt64(segment.Stream, utc.Ticks);
            WriteInt32(segment.Stream, jpeg.Length);
            segment.Stream.Write(jpeg, 0, jpeg.Length);
            segment.Stream.Flush(flushToDisk: false);
            _lastFrame[monitorIndex] = (utc, jpeg);
        }
    }

    public byte[]? GetFrame(DateTime utc, int monitorIndex = 0)
    {
        utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        string? path = null;
        byte[]? fallback = null;

        lock (_gate)
        {
            FlushCurrent();
            if (_lastFrame.TryGetValue(monitorIndex, out var last))
            {
                fallback = last.Jpeg;
                if (utc >= last.Utc.AddSeconds(-30))
                    return last.Jpeg;
            }

            var files = ListSegments().Where(f => f.MonitorIndex == monitorIndex).ToList();
            if (files.Count == 0) return fallback;
            var idx = files.FindLastIndex(f => f.StartUtc <= utc);
            path = (idx >= 0 ? files[idx] : files[0]).Path;
        }

        return ReadNearestFrame(path!, utc) ?? fallback;
    }

    public string CreateJpegZip(DateTime fromUtc, DateTime toUtc, int monitorIndex = 0)
    {
        fromUtc = DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc);
        toUtc = DateTime.SpecifyKind(toUtc, DateTimeKind.Utc);
        if (toUtc < fromUtc) (fromUtc, toUtc) = (toUtc, fromUtc);

        var zipPath = Path.Combine(Path.GetTempPath(), $"sentinela-rec-{Guid.NewGuid():N}.zip");
        lock (_gate)
        {
            FlushCurrent();
            var files = ListSegments()
                .Where(f => f.MonitorIndex == monitorIndex)
                .Where(f => f.StartUtc <= toUtc && f.StartUtc.Add(_segmentLength) >= fromUtc)
                .ToList();

            using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            foreach (var file in files)
            {
                foreach (var (ticks, jpeg) in ReadAllFrames(file.Path))
                {
                    var ts = new DateTime(ticks, DateTimeKind.Utc);
                    if (ts < fromUtc || ts > toUtc) continue;
                    var entry = zip.CreateEntry($"{ts:yyyy-MM-dd}/{ticks}.jpg", CompressionLevel.NoCompression);
                    using var stream = entry.Open();
                    stream.Write(jpeg);
                }
            }
        }

        return zipPath;
    }

    public void Purge(TimeSpan retention, long maxBytes)
    {
        var cutoff = DateTime.UtcNow - retention;
        lock (_gate)
        {
            var openPaths = _open.Values.Select(s => s.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var file in ListSegments().Where(f => f.StartUtc < cutoff).ToList())
            {
                if (openPaths.Contains(file.Path)) continue;
                try { File.Delete(file.Path); }
                catch { /* keep going */ }
            }

            if (maxBytes <= 0) return;

            var remaining = ListSegments()
                .Select(f => (File: f, Length: SafeLength(f.Path)))
                .OrderBy(x => x.File.StartUtc)
                .ToList();
            var total = remaining.Sum(x => x.Length);
            foreach (var (file, length) in remaining)
            {
                if (total <= maxBytes) break;
                if (openPaths.Contains(file.Path)) continue;
                try
                {
                    File.Delete(file.Path);
                    total -= length;
                }
                catch { /* keep going */ }
            }
        }
    }

    private static long SafeLength(string path)
    {
        try { return new FileInfo(path).Length; }
        catch { return 0; }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var segment in _open.Values)
                segment.Stream.Dispose();
            _open.Clear();
        }
    }

    private void RotateIfNeeded(DateTime utc, int monitorIndex)
    {
        if (_open.TryGetValue(monitorIndex, out var current) && utc - current.Start < _segmentLength)
            return;

        if (current is not null)
        {
            current.Stream.Dispose();
            _open.Remove(monitorIndex);
        }

        var start = new DateTime(utc.Ticks - (utc.Ticks % _segmentLength.Ticks), DateTimeKind.Utc);
        var path = Path.Combine(_root, $"{start:yyyyMMddTHHmmss}Z.m{monitorIndex}.srec");
        var exists = File.Exists(path);
        var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 64 * 1024);
        if (!exists || stream.Length == 0)
        {
            stream.Write(Encoding.ASCII.GetBytes(Magic));
            WriteUInt16(stream, Version);
            stream.Flush(flushToDisk: false);
        }

        _open[monitorIndex] = new OpenSegment(stream, path, start);
    }

    private void FlushCurrent()
    {
        foreach (var segment in _open.Values)
        {
            try { segment.Stream.Flush(flushToDisk: false); }
            catch { /* ignore */ }
        }
    }

    private void LoadMonitors()
    {
        var path = Path.Combine(_root, "monitors.json");
        if (!File.Exists(path)) return;
        try
        {
            var json = File.ReadAllText(path);
            _monitors = JsonSerializer.Deserialize<List<RecordingMonitorInfo>>(json) ?? [];
        }
        catch
        {
            _monitors = [];
        }
    }

    private List<SegmentFile> ListSegments()
    {
        if (!Directory.Exists(_root)) return [];
        var list = new List<SegmentFile>();
        foreach (var path in Directory.GetFiles(_root, "*.srec"))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var monitor = 0;
            var stamp = name;
            var marker = name.LastIndexOf(".m", StringComparison.OrdinalIgnoreCase);
            if (marker > 0)
            {
                stamp = name[..marker];
                _ = int.TryParse(name[(marker + 2)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out monitor);
            }

            if (DateTime.TryParseExact(stamp, "yyyyMMddTHHmmss'Z'", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var start))
            {
                list.Add(new SegmentFile(path, start, monitor));
            }
        }
        list.Sort((a, b) =>
        {
            var cmp = a.MonitorIndex.CompareTo(b.MonitorIndex);
            return cmp != 0 ? cmp : a.StartUtc.CompareTo(b.StartUtc);
        });
        return list;
    }

    private static byte[]? ReadNearestFrame(string path, DateTime utc)
    {
        byte[]? last = null;
        foreach (var (ticks, jpeg) in ReadAllFrames(path))
        {
            var ts = new DateTime(ticks, DateTimeKind.Utc);
            if (ts > utc) return last ?? jpeg;
            last = jpeg;
        }
        return last;
    }

    private static IEnumerable<(long Ticks, byte[] Jpeg)> ReadAllFrames(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 64 * 1024);
        if (fs.Length < 6) yield break;
        using var reader = new BinaryReader(fs, Encoding.UTF8, leaveOpen: true);
        var magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (magic != Magic) yield break;
        reader.ReadUInt16();

        while (fs.Position + 12 <= fs.Length)
        {
            long ticks;
            int len;
            try
            {
                ticks = reader.ReadInt64();
                len = reader.ReadInt32();
                if (len <= 0 || len > 16_000_000 || fs.Position + len > fs.Length)
                    yield break;
            }
            catch
            {
                yield break;
            }

            var jpeg = reader.ReadBytes(len);
            yield return (ticks, jpeg);
        }
    }

    private sealed record OpenSegment(FileStream Stream, string Path, DateTime Start);
    private readonly record struct SegmentFile(string Path, DateTime StartUtc, int MonitorIndex);

    private static void WriteInt32(Stream stream, int value)
    {
        Span<byte> buf = stackalloc byte[4];
        BitConverter.TryWriteBytes(buf, value);
        stream.Write(buf);
    }

    private static void WriteInt64(Stream stream, long value)
    {
        Span<byte> buf = stackalloc byte[8];
        BitConverter.TryWriteBytes(buf, value);
        stream.Write(buf);
    }

    private static void WriteUInt16(Stream stream, ushort value)
    {
        Span<byte> buf = stackalloc byte[2];
        BitConverter.TryWriteBytes(buf, value);
        stream.Write(buf);
    }
}

public static class FrameHash
{
    public static string Compute(byte[] jpeg) => Convert.ToHexString(SHA256.HashData(jpeg));
}
