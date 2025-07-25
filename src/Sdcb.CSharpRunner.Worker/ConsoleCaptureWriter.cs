using Sdcb.CSharpRunner.Shared;
using System.Text;
using System.Threading.Channels;

namespace Sdcb.CSharpRunner.Worker;

public sealed class ConsoleCaptureWriter(ChannelWriter<SseResponse> channel, bool isStdout) : TextWriter
{
    private readonly StringBuilder _buffer = new(256);

    public override Encoding Encoding => Encoding.UTF8;

    /*──────── 统一入口 ────────*/
    public override void Write(char value) => WriteCore(value.ToString());
    public override void Write(char[] buffer, int index, int count)
        => WriteCore(new string(buffer, index, count));
    public override void Write(string? value) => WriteCore(value);

    public override void WriteLine() => WriteCore(Environment.NewLine);
    public override void WriteLine(string? value)
        => WriteCore((value ?? "") + Environment.NewLine);

    /*──────── 核心实现 ────────*/
    private void WriteCore(string? txt)
    {
        if (string.IsNullOrEmpty(txt)) return;

        if (_buffer.Length + txt.Length > 4 * 1024 * 1024)
        {
            throw new Exception("Console output too large, please reduce the output size.");
        }

        // ② 累积到内存，用于最后 EndSseResponse
        _buffer.Append(txt);

        // ③ 即时推送 SSE
        SseResponse msg = isStdout
            ? new StdoutSseResponse { StdOutput = txt }
            : new StdErrSseResponse { StdError = txt };

        // TryWrite 保证在 Channel full 时不会阻塞脚本
        channel.TryWrite(msg);
    }

    /*──────── 让调用方可以在结束时取得完整内容 ────────*/
    public string? CapturedText => _buffer.ToString() switch
    {
        "" => null,
        var x => x,
    };
}