using System.Text.Json;
using System.Text.Json.Nodes;

namespace BanDoSu.Core;

public sealed record ShotRow(string Id, double Start, double End, string Line, string Template);

public sealed class ShotDocument
{
    public JsonNode Root { get; }
    public ShotDocument(JsonNode root)
    {
        Root = root;
        if (root["shots"] is not JsonArray) throw new FormatException("Expected shots array.");
    }

    public JsonArray Shots => (JsonArray)Root["shots"]!;

    public IReadOnlyList<ShotRow> Rows => Shots.Select(n => new ShotRow(
        n?["id"]?.ToString() ?? "",
        ToDouble(n?["start"]),
        ToDouble(n?["end"]),
        n?["line"]?.ToString() ?? "",
        n?["tpl"]?.ToString() ?? "")).ToArray();

    private static double ToDouble(JsonNode? n) => double.TryParse(n?.ToString(),
        System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;

    public string GetArgsJson(int index) => Shots[index]?["args"]?.ToJsonString(JsonOptions.Pretty) ?? "{}";

    public void UpdateArgs(int index, string json)
    {
        if (index < 0 || index >= Shots.Count) throw new ArgumentOutOfRangeException(nameof(index));
        var node = JsonNode.Parse(json);
        if (node is not JsonObject) throw new JsonException("Scene args must be a JSON object.");
        Shots[index]!["args"] = node;
        // draft_shots.py does not set tpl for narration-only shots.
        if (string.IsNullOrWhiteSpace(Shots[index]?["tpl"]?.ToString()))
            Shots[index]!["tpl"] = "map";
    }

    public void UpdateLine(int index, string line)
    {
        if (index < 0 || index >= Shots.Count) throw new ArgumentOutOfRangeException(nameof(index));
        Shots[index]!["line"] = line;
    }

    public void SetInitialMapDefaults()
    {
        if (Shots.Count == 0) return;
        foreach (var shot in Shots)
        {
            if (shot is not JsonObject scene) continue;
            if (string.IsNullOrWhiteSpace(scene["tpl"]?.ToString()))
                scene["tpl"] = "map";
            scene["args"] ??= new JsonObject();
        }
        var first = (JsonObject)Shots[0]!;
        var args = (JsonObject)first["args"]!;
        args["view"] ??= JsonValue.Create("viet-nam");
        args["fly"] ??= JsonValue.Create(0);
    }

    public void Validate()
    {
        var last = 0.0;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in Rows)
        {
            if (string.IsNullOrWhiteSpace(s.Id) || !ids.Add(s.Id))
                throw new FormatException("Scene IDs must be nonempty and unique.");
            if (s.End <= s.Start || s.Start < last - 0.001)
                throw new FormatException($"Invalid scene time range at {s.Id}.");
            last = s.End;
        }
    }
}
