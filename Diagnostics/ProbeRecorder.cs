using System.Text;
using System.Text.Json;

namespace KeyboundTab.Diagnostics;

internal sealed class ProbeRecorder : IDisposable
{
    private const long MaxBytes = 16 * 1024 * 1024;
    private readonly string path;
    private string? lastState;
    private StreamWriter? writer;
    private long bytes;

    public ProbeRecorder(string directory)
    {
        Directory.CreateDirectory(directory);
        this.path = Path.Combine(directory, "keybound-probe.jsonl");
    }

    public string PathName => this.path;

    public void Record(ProbeReport report, bool force = false)
    {
        var state = JsonSerializer.Serialize(report);
        if (!force && state == this.lastState)
            return;
        this.lastState = state;
        var line = JsonSerializer.Serialize(new { Time = DateTimeOffset.Now, Report = report });
        var length = Encoding.UTF8.GetByteCount(line) + 2;
        this.writer ??= this.Open();
        if (this.bytes + length > MaxBytes)
        {
            this.writer.Dispose();
            this.writer = null;
            File.Move(this.path, this.path + ".previous", true);
            this.writer = this.Open();
        }
        this.writer.WriteLine(line);
        this.writer.Flush();
        this.bytes += length;
    }

    public void Dispose()
    {
        this.writer?.Dispose();
        this.writer = null;
    }

    private StreamWriter Open()
    {
        this.bytes = File.Exists(this.path) ? new FileInfo(this.path).Length : 0;
        return new StreamWriter(new FileStream(this.path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false));
    }
}
