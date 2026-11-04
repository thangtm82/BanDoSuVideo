using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BanDoSu.Core;

public enum AiScriptProvider { OpenAI, Gemini }

public sealed record AiScriptRequest(
    AiScriptProvider Provider, string Model, string ApiKey, string Topic,
    int Minutes = 12);

public sealed record AiScriptBundle(
    string Slug, string Script, string Voice, string MapPlan, string Package,
    int WordCount, int VoiceSegments);

/// <summary>
/// Creates the four Bản Đồ Sử artifacts from the packaged local Skill.
/// A provider response is unverified creative draft; historical facts, sources,
/// coordinates and engine templates must be reviewed before production.
/// </summary>
public sealed class AiScriptStudio
{
    private readonly HttpClient _client;
    private readonly string _skillRoot;

    public AiScriptStudio(string skillRoot, HttpClient? client = null)
    {
        _skillRoot = Path.GetFullPath(skillRoot);
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromMinutes(8) };
    }

    public bool HasSkill =>
        File.Exists(Path.Combine(_skillRoot, "SKILL.md")) &&
        File.Exists(Path.Combine(_skillRoot, "references", "cau-truc.md")) &&
        File.Exists(Path.Combine(_skillRoot, "references", "dia-danh.md"));

    public static string ResolveSkillRoot(string appRoot, string engineRoot)
    {
        var installed = Path.Combine(engineRoot, "skills", "kich-ban-bandosu");
        if (File.Exists(Path.Combine(installed, "SKILL.md")) &&
            File.Exists(Path.Combine(installed, "references", "cau-truc.md")) &&
            File.Exists(Path.Combine(installed, "references", "dia-danh.md")))
            return installed;

        return Path.Combine(appRoot, "skills", "kich-ban-bandosu");
    }

    public async Task<AiScriptBundle> GenerateAsync(AiScriptRequest request,
        string slug, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        ProjectFiles.ValidateName(slug);
        if (request.Minutes is < 8 or > 30)
            throw new ArgumentOutOfRangeException(nameof(request.Minutes), "Thời lượng phải từ 8 đến 30 phút.");
        if (string.IsNullOrWhiteSpace(request.Topic) || request.Topic.Trim().Length < 8)
            throw new ArgumentException("Vui lòng nhập đề tài lịch sử rõ ràng.");
        if (string.IsNullOrWhiteSpace(request.Model))
            throw new ArgumentException("Chưa chọn tên model AI.");
        if (string.IsNullOrWhiteSpace(request.ApiKey))
            throw new ArgumentException("Chưa nhập API key.");
        if (!HasSkill)
            throw new FileNotFoundException("Thiếu SKILL.md hoặc references/cau-truc.md, dia-danh.md trong " + _skillRoot);

        var instructions = await File.ReadAllTextAsync(Path.Combine(_skillRoot, "SKILL.md"), ct);
        var structure = await File.ReadAllTextAsync(Path.Combine(_skillRoot, "references", "cau-truc.md"), ct);
        var places = await File.ReadAllTextAsync(Path.Combine(_skillRoot, "references", "dia-danh.md"), ct);
        var context = "HƯỚNG DẪN SKILL:\n" + instructions +
            "\n\nCẤU TRÚC:\n" + structure + "\n\nĐỊA DANH:\n" + places;
        var wordsTarget = request.Minutes <= 12 ? "2.000–2.400" : "4.000–5.800";
        progress?.Report("1/3 — AI đang viết kịch bản lời đọc...");
        var script = (await CompleteAsync(request, context +
            "\n\nNHIỆM VỤ 1: Viết riêng nội dung file 01-SCRIPT cho đề tài: " + request.Topic +
            "\nThời lượng: " + request.Minutes + " phút; số chữ mục tiêu " + wordsTarget +
            ". Trả duy nhất lời thuyết minh tiếng Việt liền mạch, không markdown, không tiêu đề, " +
            "không số chữ, không tệp khác. Tuân thủ 7 nhịp, 3 lý do, có các năm và địa danh. " +
            "Không bịa số liệu hay trích dẫn. Những mốc chưa chắc chắn thì trình bày thận trọng. " +
            "Không được tự nhận đã tra cứu trực tuyến: ở phiên làm việc này không có công cụ tra cứu web.",
            ct)).Trim();
        script = NormalizeScript(script);
        var count = CountWords(script);
        if (count < 200)
            throw new InvalidDataException("AI trả kịch bản quá ngắn. Hãy thử model khác hoặc tạo lại.");
        progress?.Report("2/3 — Đang chia lời đọc và lập kế hoạch bản đồ...");
        var (voice, segments) = MakeVoice(script);
        var plan = (await CompleteAsync(request, context +
            "\n\nNHIỆM VỤ 2: Lập riêng file 03-BAN-DO bằng Markdown từ lời đọc sau. " +
            "Bảng phải ánh xạ chính xác số đoạn voice từ 1 đến " + segments +
            ", dùng view/place có trong dia-danh.md, năm và thay đổi bản đồ theo mỗi chặng, " +
            "thanh chủ đề, bảng màu, 2–3 điểm Hãy nhìn lại bản đồ, nhạc và mục Nguồn. " +
            "Không tự bịa URL sách; ghi rõ CẦN KIỂM CHỨNG cho nguồn/mốc chưa được kiểm tra. " +
            "Đây là kế hoạch tham khảo, không phải shots.json hoặc lệnh renderer đã được xác thực.\n\nLỜI ĐỌC:\n" + script,
            ct)).Trim();
        progress?.Report("3/3 — Đang viết gói nội dung YouTube...");
        var package = (await CompleteAsync(request, context +
            "\n\nNHIỆM VỤ 3: Viết riêng file 05-PACKAGE bằng Markdown từ kịch bản dưới. " +
            "Gồm 10 tiêu đề, 3 phương án thumbnail bản đồ, mô tả, 6–10 chương với timecode placeholder, " +
            "20 tag, hashtag, bình luận ghim và mục nguồn tham khảo. Nêu rõ nguồn chưa được xác minh nếu cần. " +
            "Không bịa thông tin có thể kiểm chứng.\n\nLỜI ĐỌC:\n" + script,
            ct)).Trim();
        var footer = $"\r\n\r\nSố chữ: {count:N0} · Thời lượng ước tính: {count / 195.0:0.0} phút";
        progress?.Report("Đã tạo xong bản nháp. Hãy duyệt sử liệu và nguồn trước khi xuất bản.");
        return new(slug, script + footer, voice, plan, package, count, segments);
    }

    public static int CountWords(string text) => Regex.Matches(text, @"\S+").Count;

    public static string NormalizeScript(string text) =>
        Regex.Replace(text.Replace("|", ",").Replace("\r\n", "\n").Replace('\r', '\n'),
            @"[ \t]+", " ").Trim();

    public static string ScriptNarration(string script) =>
        Regex.Replace(script.Trim(), @"\s*Số chữ:\s*[\d.,]+\s*[·|–-]\s*Thời lượng ước tính:[^\r\n]*\s*$",
            "", RegexOptions.IgnoreCase).Trim();

    public static (string Voice, int Segments) MakeVoice(string narration)
    {
        var flat = Regex.Replace(ScriptNarration(narration).Replace("|", ","), @"\s+", " ").Trim();
        if (flat.Length == 0) throw new ArgumentException("Lời đọc đang trống.");
        // Split on sentence breaks, then on whitespace when a sentence is too long.
        // Normalizing whitespace maintains exactly the same words in 01 and 02.
        var sentences = Regex.Split(flat, @"(?<=[.!?…])\s+").Where(x => x.Length != 0);
        var segments = new List<string>();
        var current = new List<string>();
        foreach (var sentence in sentences)
        {
            var words = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                if (current.Count >= 40)
                {
                    segments.Add(string.Join(" ", current));
                    current.Clear();
                }
                current.Add(word);
            }
            if (current.Count >= 22)
            {
                segments.Add(string.Join(" ", current));
                current.Clear();
            }
        }
        if (current.Count != 0) segments.Add(string.Join(" ", current));
        if (segments.Any(x => CountWords(x) > 55))
            throw new InvalidDataException("Một đoạn voice dài quá 55 chữ.");
        var result = string.Join(" |\r\n", segments);
        if (!EquivalentNarration(flat, result))
            throw new InvalidDataException("Lời đọc bị thay đổi khi chia đoạn.");
        return (result, segments.Count);
    }

    public static bool EquivalentNarration(string script, string voice)
    {
        static string Flatten(string value) => Regex.Replace(
            value.Replace("|", " ").Replace("\r", " ").Replace("\n", " "), @"\s+", " ").Trim();
        return string.Equals(Flatten(ScriptNarration(script)), Flatten(voice), StringComparison.Ordinal);
    }

    public static void ValidateBundle(string script, string voice, string map, string package)
    {
        if (!EquivalentNarration(script, voice))
            throw new InvalidDataException("File 02 phải chứa đúng toàn bộ lời đọc của file 01, chỉ thêm dấu |.");
        if (voice.Split('|').Any(p => CountWords(p) > 55 || CountWords(p) == 0))
            throw new InvalidDataException("Mỗi đoạn 02-VOICE cần từ 1 đến 55 chữ.");
        if (string.IsNullOrWhiteSpace(map) || string.IsNullOrWhiteSpace(package))
            throw new InvalidDataException("Chưa có đủ file 03-BAN-DO và 05-PACKAGE.");
    }

    private async Task<string> CompleteAsync(AiScriptRequest req, string prompt, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post,
            req.Provider == AiScriptProvider.OpenAI
                ? "https://api.openai.com/v1/responses"
                : $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(req.Model)}:generateContent");
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (req.Provider == AiScriptProvider.OpenAI)
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", req.ApiKey.Trim());
            message.Content = JsonContent(new
            {
                model = req.Model.Trim(),
                input = prompt,
                max_output_tokens = 12000
            });
        }
        else
        {
            message.Headers.Add("x-goog-api-key", req.ApiKey.Trim());
            message.Content = JsonContent(new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } },
                generationConfig = new { maxOutputTokens = 12000 }
            });
        }
        using var response = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            // Never log or display full server error bodies: they could contain secrets.
            throw new HttpRequestException("API AI trả về HTTP " + (int)response.StatusCode +
                " (" + response.ReasonPhrase + "). Kiểm tra API key, model và hạn mức.");
        }
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        if (req.Provider == AiScriptProvider.Gemini)
        {
            if (!root.TryGetProperty("candidates", out var candidates) ||
                candidates.GetArrayLength() == 0)
                throw new InvalidDataException("Gemini không trả nội dung.");
            var parts = candidates[0].GetProperty("content").GetProperty("parts");
            return string.Join("\n", parts.EnumerateArray().Where(x => x.TryGetProperty("text", out _))
                .Select(x => x.GetProperty("text").GetString()));
        }
        // Responses API: output[] -> message.content[] -> output_text.text
        if (root.TryGetProperty("output", out var output))
        {
            var chunks = new List<string>();
            foreach (var element in output.EnumerateArray())
            {
                if (!element.TryGetProperty("content", out var parts)) continue;
                foreach (var part in parts.EnumerateArray())
                    if (part.TryGetProperty("text", out var text)) chunks.Add(text.GetString() ?? "");
            }
            if (chunks.Count != 0) return string.Join("\n", chunks);
        }
        throw new InvalidDataException("OpenAI không trả nội dung văn bản (output_text).");
    }

    private static StringContent JsonContent(object data) =>
        new(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");
}
