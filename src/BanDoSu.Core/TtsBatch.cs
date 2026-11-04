namespace BanDoSu.Core;

/// <summary>
/// Future TTS API adapters implement this contract. A successful request must
/// return MP3 bytes, not a provider-specific JSON document or WAV data.
/// </summary>
public interface ITtsSynthesisProvider
{
    Task<byte[]> SynthesizeMp3Async(string text, string voiceId, CancellationToken cancellationToken);
}

public sealed record TtsSegment(int Index, string Text)
{
    public string FileName => Index + ".mp3";
}

public sealed record TtsProgress(int Completed, int Total, string Message);

/// <summary>
/// Provider-neutral MP3 batch coordinator. It deliberately does not ship a
/// fake/silent MP3 generator: the desktop will show "API chưa cấu hình"
/// until the user supplies the provider contract.
/// </summary>
public static class TtsBatch
{
    public static IReadOnlyList<TtsSegment> ParseSegments(string script)
    {
        if (string.IsNullOrWhiteSpace(script))
            throw new InvalidDataException("Lời đọc đang trống. Hãy nhập hoặc tải kịch bản trước.");

        var raw = script.Split('|');
        if (raw.Any(x => string.IsNullOrWhiteSpace(x)))
            throw new InvalidDataException("Lời đọc có đoạn trống giữa các dấu |. Hãy kiểm tra lại.");

        var pieces = raw.Select((text, index) => new TtsSegment(index + 1, text.Trim())).ToArray();
        if (pieces.Length == 0) throw new InvalidDataException("Không tìm thấy đoạn lời đọc.");
        return pieces;
    }

    public static string RequireVoiceId(string? voiceId)
    {
        var text = voiceId?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidDataException("Hãy chọn hoặc nhập Voice ID trước khi tạo MP3.");
        if (text.Any(char.IsControl))
            throw new InvalidDataException("Voice ID chứa ký tự điều khiển không hợp lệ.");
        if (text.Length > 200)
            throw new InvalidDataException("Voice ID quá dài.");
        return text;
    }

    public static string GetOutputPath(string directory, TtsSegment segment)
        => Path.Combine(Path.GetFullPath(directory), segment.FileName);

    public static bool LooksLikeMp3(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 3 && bytes[0] == (byte)'I' && bytes[1] == (byte)'D' &&
            bytes[2] == (byte)'3' ||
        bytes.Length >= 2 && bytes[0] == 0xFF && (bytes[1] & 0xE0) == 0xE0;

    public static async Task<int> GenerateAsync(
        string script, string voiceId, string outputDirectory, ITtsSynthesisProvider? provider,
        IProgress<TtsProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var segments = ParseSegments(script);
        var id = RequireVoiceId(voiceId);
        if (provider is null)
            throw new NotSupportedException("Chưa cấu hình API TTS. Hãy cung cấp đặc tả API để kích hoạt Tạo MP3.");

        var directory = Path.GetFullPath(outputDirectory);
        if (File.Exists(directory))
            throw new IOException("Đường dẫn thư mục MP3 trỏ tới một tệp.");
        Directory.CreateDirectory(directory);
        // Never silently overwrite previously generated voice clips.
        var collisions = segments.Select(x => GetOutputPath(directory, x))
            .Where(File.Exists).ToArray();
        if (collisions.Length > 0)
            throw new IOException("Đã có " + collisions.Length +
                " tệp MP3. Hãy sao lưu hoặc chọn thư mục khác trước khi tạo lại.");

        var completed = 0;
        foreach (var segment in segments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new TtsProgress(completed, segments.Count,
                "Đang tạo " + segment.FileName));
            var audio = await provider.SynthesizeMp3Async(segment.Text, id, cancellationToken);
            if (!LooksLikeMp3(audio))
                throw new InvalidDataException("API TTS không trả dữ liệu MP3 hợp lệ cho " +
                    segment.FileName + ". Không lưu tệp.");
            var target = GetOutputPath(directory, segment);
            var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await File.WriteAllBytesAsync(temporary, audio, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporary, target, overwrite: false);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
            completed++;
            progress?.Report(new TtsProgress(completed, segments.Count,
                "Đã tạo " + segment.FileName));
        }
        return completed;
    }
}
