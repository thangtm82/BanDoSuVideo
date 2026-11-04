using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BanDoSu.Core;

public enum AiScriptProvider { OpenAI, Gemini, Custom }

public enum AiCustomEndpoint { ChatCompletions, Responses }

/// <summary>Normalizes a trusted, user-entered OpenAI-compatible base URI.
/// The base may end in /v1, /v1/chat/completions or /v1/responses.
/// Query strings, fragments and embedded credentials are rejected.</summary>
public static class AiEndpoint
{
    public static Uri Resolve(string baseUrl, AiCustomEndpoint endpoint)
    {
        if (!Uri.TryCreate(baseUrl?.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("Base URL phải là URL http/https hợp lệ, không chứa tài khoản, query hoặc fragment.");
        var path = uri.AbsolutePath.TrimEnd('/');
        if (path.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            path = path[..^"/chat/completions".Length];
        else if (path.EndsWith("/responses", StringComparison.OrdinalIgnoreCase))
            path = path[..^"/responses".Length];
        if (path.Length == 0) path = "/v1";
        var suffix = endpoint == AiCustomEndpoint.ChatCompletions ? "/chat/completions" : "/responses";
        var builder = new UriBuilder(uri) { Path = path + suffix, Query = "", Fragment = "" };
        return builder.Uri;
    }
}

public sealed record AiScriptRequest(
    AiScriptProvider Provider, string Model, string ApiKey, string Topic,
    int Minutes = 12, string? BaseUrl = null,
    AiCustomEndpoint CustomEndpoint = AiCustomEndpoint.ChatCompletions);

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
        // A redirected custom endpoint must not forward script content or tokens.
        // Injected HttpClient is used by deterministic tests.
        _client = client ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        { Timeout = TimeSpan.FromMinutes(8) };
    }

    public bool HasSkill =>
        File.Exists(Path.Combine(_skillRoot, "SKILL.md")) &&
        File.Exists(Path.Combine(_skillRoot, "references", "cau-truc.md"));

    private bool IsTerrain => Path.GetFileName(_skillRoot)
        .Equals("kich-ban-terrain", StringComparison.OrdinalIgnoreCase);

    public static string ResolveSkillRoot(string appRoot, string engineRoot, string pack = "bando")
    {
        var name = pack.Equals("bando", StringComparison.OrdinalIgnoreCase)
            ? "kich-ban-bandosu" : "kich-ban-" + pack;
        var installed = Path.Combine(engineRoot, "skills", name);
        if (File.Exists(Path.Combine(installed, "SKILL.md")) &&
            File.Exists(Path.Combine(installed, "references", "cau-truc.md")))
            return installed;
        return Path.Combine(appRoot, "skills", name);
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
        ValidateRequest(request);
        if (!HasSkill)
            throw new FileNotFoundException("Thiếu SKILL.md hoặc references/cau-truc.md trong " + _skillRoot);

        var instructions = await File.ReadAllTextAsync(Path.Combine(_skillRoot, "SKILL.md"), ct);
        var structure = await File.ReadAllTextAsync(Path.Combine(_skillRoot, "references", "cau-truc.md"), ct);
        var placesPath = Path.Combine(_skillRoot, "references", "dia-danh.md");
        var places = File.Exists(placesPath) ? await File.ReadAllTextAsync(placesPath, ct) : "";
        var context = "HƯỚNG DẪN SKILL:\n" + instructions +
            "\n\nCẤU TRÚC:\n" + structure +
            (string.IsNullOrWhiteSpace(places) ? "" : "\n\nĐỊA DANH:\n" + places);
        var wordsTarget = IsTerrain
            ? request.Minutes <= 12 ? "1.350–1.800" : "3.300–3.800"
            : request.Minutes <= 12 ? "2.000–2.400" : "4.000–5.800";
        progress?.Report("1/3 — AI đang viết kịch bản lời đọc...");
        var taskOne = IsTerrain
            ? "Viết riêng file 01-SCRIPT bằng tiếng Anh Mỹ, 9 nhịp theo cau-truc.md. " +
              "Trình bày chiến thuật trên địa hình thật, có ngày, vị trí, lực lượng, diễn biến, quy luật chiến thuật. " +
              "Không bịa quân số, tọa độ, quote hoặc trích dẫn quân sự. " +
              "Không có web research trong phiên này; các chi tiết chưa chứng minh phải được để xác minh thủ công."
            : "Viết riêng nội dung file 01-SCRIPT bằng tiếng Việt, 7 nhịp, 3 lý do, " +
              "không markdown/tiêu đề. Không bịa số liệu hoặc trích dẫn.";
        var script = (await CompleteAsync(request, context +
            "\n\nNHIỆM VỤ 1: " + taskOne + "\nĐề tài: " + request.Topic +
            "\nThời lượng: " + request.Minutes + " phút; mục tiêu " + wordsTarget +
            " chữ. Trả lại duy nhất lời thuyết minh voiceover-ready, không kèm footer đếm chữ. " +
            "Không được tự nhận đã nghiên cứu trực tuyến.", ct)).Trim();
        script = NormalizeScript(script);
        var count = CountWords(script);
        if (count < 200)
            throw new InvalidDataException("AI trả kịch bản quá ngắn. Hãy thử model khác hoặc tạo lại.");
        progress?.Report("2/3 — Đang chia lời đọc và lập kế hoạch bản đồ...");
        var (voice, segments) = MakeVoice(script);
        var planTask = IsTerrain
            ? "Lập file 03-HINH bằng Markdown: thế giới 3D, các ID world, DEM [lon,lat] " +
              "(chỉ ghi tọa độ có cơ sở, còn lại CẦN KIỂM CHỨNG), hướng camera, đơn vị quân, " +
              "diễn tiến từng đoạn voice 1.." + segments + ", âm thanh/SFX, nguồn. " +
              "Phân biệt template tw và map2d; không tự giả vờ đã truy vấn địa hình thực."
            : "Lập file 03-BAN-DO bằng Markdown; ánh xạ từng đoạn voice 1.." + segments +
              ", dùng view/place nếu có trong dia-danh.md, kế hoạch cảnh và nguồn. " +
              "Không tự tạo nguồn không kiểm chứng.";
        var plan = (await CompleteAsync(request, context +
            "\n\nNHIỆM VỤ 2: " + planTask +
            " Đây là kế hoạch hình ảnh, không phải shots.json đã được kiểm thử.\n\nLỜI ĐỌC:\n" + script,
            ct)).Trim();
        progress?.Report("3/3 — Đang viết gói nội dung YouTube...");
        var package = (await CompleteAsync(request, context +
            "\n\nNHIỆM VỤ 3: Viết riêng file 05-PACKAGE bằng Markdown từ kịch bản dưới. " +
            "Gồm 10 tiêu đề, 3 phương án thumbnail bản đồ, mô tả, 6–10 chương với timecode placeholder, " +
            "20 tag, hashtag, bình luận ghim và mục nguồn tham khảo. Nêu rõ nguồn chưa được xác minh nếu cần. " +
            "Không bịa thông tin có thể kiểm chứng.\n\nLỜI ĐỌC:\n" + script,
            ct)).Trim();
        var footer = $"\r\n\r\nSố chữ: {count:N0} · Thời lượng ước tính: {count / (IsTerrain ? 150.0 : 195.0):0.0} phút";
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

    private static void ValidateRequest(AiScriptRequest req)
    {
        if (req.Provider != AiScriptProvider.Custom && string.IsNullOrWhiteSpace(req.ApiKey))
            throw new ArgumentException("Chưa nhập API key.");
        if (req.Provider == AiScriptProvider.Custom)
            _ = AiEndpoint.Resolve(req.BaseUrl ?? "", req.CustomEndpoint);
    }

    /// <summary>Reusable non-streaming completion for grounded H2 scene authoring.
    /// Uses the same OpenAI/Gemini/Custom settings as AI Kịch bản.</summary>
    public Task<string> CompletePromptAsync(AiScriptRequest request, string prompt,
        CancellationToken ct = default, int maxTokens = 12000)
    {
        if (string.IsNullOrWhiteSpace(request.Model))
            throw new ArgumentException("Chưa chọn model AI.");
        ValidateRequest(request);
        if (maxTokens is < 64 or > 20000)
            throw new ArgumentOutOfRangeException(nameof(maxTokens));
        return CompleteAsync(request, prompt, ct, maxTokens);
    }

    /// <summary>Issues one small real request without generating or saving a script.</summary>
    public async Task<string> TestConnectionAsync(AiScriptRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Model))
            throw new ArgumentException("Chưa nhập tên model.");
        ValidateRequest(request);
        var answer = await CompleteAsync(request, "Reply with the single word OK.", ct, maxTokens: 32);
        if (string.IsNullOrWhiteSpace(answer))
            throw new InvalidDataException("API không trả nội dung văn bản.");
        return "Kết nối thành công: " + request.Provider + " / " + request.Model;
    }

    private async Task<string> CompleteAsync(AiScriptRequest req, string prompt,
        CancellationToken ct, int maxTokens = 12000)
    {
        Uri uri = req.Provider switch
        {
            AiScriptProvider.OpenAI => new("https://api.openai.com/v1/responses"),
            AiScriptProvider.Gemini => new(
                $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(req.Model)}:generateContent"),
            AiScriptProvider.Custom => AiEndpoint.Resolve(req.BaseUrl ?? "", req.CustomEndpoint),
            _ => throw new NotSupportedException("Nhà cung cấp AI không được hỗ trợ.")
        };
        using var message = new HttpRequestMessage(HttpMethod.Post, uri);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (req.Provider == AiScriptProvider.Gemini)
        {
            message.Headers.Add("x-goog-api-key", req.ApiKey.Trim());
            message.Content = JsonContent(new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } },
                generationConfig = new { maxOutputTokens = maxTokens }
            });
        }
        else
        {
            // Custom endpoints may be unauthenticated (e.g., local llama.cpp).
            if (!string.IsNullOrWhiteSpace(req.ApiKey))
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", req.ApiKey.Trim());
            if (req.Provider == AiScriptProvider.Custom &&
                req.CustomEndpoint == AiCustomEndpoint.ChatCompletions)
            {
                message.Content = JsonContent(new
                {
                    model = req.Model.Trim(),
                    messages = new[] { new { role = "user", content = prompt } },
                    max_tokens = maxTokens
                });
            }
            else
            {
                message.Content = JsonContent(new
                {
                    model = req.Model.Trim(),
                    input = prompt,
                    max_output_tokens = maxTokens
                });
            }
        }

        using var response = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
        if ((int)response.StatusCode is >= 300 and < 400)
            throw new HttpRequestException("Endpoint AI chuyển hướng HTTP. Hãy nhập URL đích trực tiếp.");
        if (!response.IsSuccessStatusCode)
        {
            // Do not echo response bodies or URLs: either might include a secret.
            throw new HttpRequestException("API AI trả về HTTP " + (int)response.StatusCode +
                ". Kiểm tra Base URL, API key, model và endpoint.");
        }
        var body = await response.Content.ReadAsStringAsync(ct);
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        if (req.Provider == AiScriptProvider.Gemini)
        {
            if (!root.TryGetProperty("candidates", out var candidates) ||
                candidates.GetArrayLength() == 0)
                throw new InvalidDataException("Gemini không trả nội dung.");
            var parts = candidates[0].GetProperty("content").GetProperty("parts");
            return string.Join("\n", parts.EnumerateArray()
                .Where(x => x.TryGetProperty("text", out _))
                .Select(x => x.GetProperty("text").GetString()));
        }
        if (req.Provider == AiScriptProvider.Custom &&
            req.CustomEndpoint == AiCustomEndpoint.ChatCompletions)
        {
            if (root.TryGetProperty("choices", out var choices) &&
                choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0 &&
                choices[0].TryGetProperty("message", out var answer) &&
                answer.TryGetProperty("content", out var content) &&
                content.ValueKind == JsonValueKind.String)
                return content.GetString() ?? "";
            throw new InvalidDataException("API Custom không trả choices[0].message.content.");
        }
        if (root.TryGetProperty("output", out var output) &&
            output.ValueKind == JsonValueKind.Array)
        {
            var chunks = new List<string>();
            foreach (var element in output.EnumerateArray())
            {
                if (!element.TryGetProperty("content", out var parts) ||
                    parts.ValueKind != JsonValueKind.Array) continue;
                foreach (var part in parts.EnumerateArray())
                    if (part.TryGetProperty("text", out var text) &&
                        text.ValueKind == JsonValueKind.String)
                        chunks.Add(text.GetString() ?? "");
            }
            if (chunks.Count != 0) return string.Join("\n", chunks);
        }
        throw new InvalidDataException("API không trả nội dung output_text tương thích Responses.");
    }

    private static StringContent JsonContent(object data) =>
        new(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");
}
