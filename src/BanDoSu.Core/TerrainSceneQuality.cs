using System.Globalization;
using System.Text.Json.Nodes;

namespace BanDoSu.Core;

/// <summary>
/// Static preflight for Terrain JSON. Checks renderable structure and visual coverage,
/// not historical truth or actual pixel quality. Image-based QA remains a separate step.
/// </summary>
public static class TerrainSceneQuality
{
    public sealed record Issue(string Code, string Subject, string Message, bool Blocking = false);

    private static readonly string[] LandscapeLayers =
        ["fields", "water", "rivers", "roads", "tracks", "ditches", "woods", "trees",
         "treelines", "rocks", "buildings", "villages", "hedges", "berms"];

    private static readonly string[] PolygonLayers = ["fields", "water", "woods"];
    private static readonly string[] LineLayers = ["rivers", "roads", "tracks", "ditches", "treelines", "hedges"];

    public static bool HasBlockingIssues(IEnumerable<Issue> issues) => issues.Any(i => i.Blocking);

    public static string Format(IEnumerable<Issue> issues) =>
        string.Join("; ", issues.Take(12).Select(i =>
            i.Code + " [" + i.Subject + "] " + i.Message));

    public static IReadOnlyList<Issue> EvaluateWorlds(JsonObject worlds)
    {
        var issues = new List<Issue>();
        foreach (var (id, value) in worlds)
        {
            if (value is not JsonObject world)
            {
                issues.Add(new Issue("WORLD_INVALID", id, "World phải là JSON object.", true));
                continue;
            }

            var validLayers = 0;
            foreach (var layer in LandscapeLayers)
            {
                if (world[layer] is null) continue;
                if (world[layer] is not JsonArray arr)
                {
                    issues.Add(new Issue("LAYER_INVALID", id, layer + " phải là JSON array.", true));
                    continue;
                }
                if (arr.Count == 0) continue;
                validLayers++;
                for (var i = 0; i < arr.Count; i++)
                {
                    if (arr[i] is not JsonObject feature)
                    {
                        issues.Add(new Issue("FEATURE_INVALID", id, layer + "[" + i + "] không phải object.", true));
                        continue;
                    }
                    if (PolygonLayers.Contains(layer) || LineLayers.Contains(layer) ||
                        (layer == "rocks" && feature["pts"] is not null))
                    {
                        var min = PolygonLayers.Contains(layer) ? 3 : 2;
                        if (!ValidPoints(feature["pts"], min))
                            issues.Add(new Issue("POINTS_INVALID", id,
                                layer + "[" + i + "].pts cần ít nhất " + min +
                                " điểm [x,y] hữu hạn.", true));
                    }
                    else if (layer is "trees" or "buildings" or "villages" or "berms" or "rocks")
                    {
                        if (!ValidPoint(feature["p"]) && !(layer == "rocks" && ValidPoints(feature["pts"], 3)))
                            issues.Add(new Issue("POINT_INVALID", id,
                                layer + "[" + i + "] thiếu vị trí p=[x,y].", true));
                    }
                }
            }

            var hasDem = world["dem"] is JsonObject;
            var hills = (world["hills"] as JsonArray)?.Count ?? 0;
            if (world["hills"] is JsonArray hillArray)
                for (var i = 0; i < hillArray.Count; i++)
                {
                    if (hillArray[i] is not JsonObject hill || !ValidPoint(hill["p"]) ||
                        !ValidPositive(hill["r"]) || !ValidFinite(hill["h"]))
                        issues.Add(new Issue("HILL_INVALID", id,
                            "hills[" + i + "] cần p=[x,y], r>0, h hữu hạn.", true));
                }
            if (!hasDem && hills == 0 && validLayers == 0)
                issues.Add(new Issue("EMPTY_WORLD", id,
                    "Không có địa hình DEM, đồi hay lớp cảnh quan nào ngoài chữ và cờ.", true));
            if (validLayers == 0)
                issues.Add(new Issue("SCHEMATIC_ONLY", id,
                    "World chỉ có địa hình cơ bản, chữ/cờ; hãy thêm tối thiểu một lớp cảnh quan phù hợp lời đọc.", true));
            else if (validLayers == 1 && !hasDem)
                issues.Add(new Issue("LOW_DETAIL", id,
                    "Chỉ có một lớp cảnh quan; cân nhắc thêm chi tiết có căn cứ."));
            if (world["undulate"] is not null && Number(world["undulate"]) is 0 &&
                Number(world["contours"]) is 0 && !hasDem)
                issues.Add(new Issue("FLAT_RELIEF", id,
                    "undulate=0 và contours=0: sa bàn có thể thiếu chiều sâu."));
            if (SimilarColors(world["ground"]?.ToString(), world["haze"]?.ToString()))
                issues.Add(new Issue("LOW_CONTRAST", id,
                    "ground và haze gần màu nhau; địa hình dễ bị nhạt."));
        }
        return issues;
    }

    public static IReadOnlyList<Issue> Evaluate(JsonNode root)
    {
        var worlds = root["worlds"] as JsonObject;
        var shots = root["shots"] as JsonArray;
        if (worlds is null || shots is null)
            return [new Issue("DOCUMENT_INVALID", "shots.json", "Thiếu worlds hoặc shots.", true)];

        var issues = new List<Issue>(EvaluateWorlds(worlds));
        var previousCameras = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var node in shots)
        {
            if (node is not JsonObject shot) continue;
            var id = shot["id"]?.ToString() ?? "(no id)";
            var tpl = shot["tpl"]?.ToString();
            var args = shot["args"] as JsonObject;
            if (tpl != "tw" || args is null) continue;
            var worldId = args["world"]?.ToString() ?? "";
            if (worlds[worldId] is not JsonObject world)
            {
                issues.Add(new Issue("MISSING_WORLD", id, "Không tìm thấy world tham chiếu.", true));
                continue;
            }

            if (args["note"] is not null)
                issues.Add(new Issue("VISIBLE_NOTE", id,
                    "args.note được renderer vẽ lên khung hình; đưa ghi chú kỹ thuật sang metadata."));
            var line = shot["line"]?.ToString() ?? "";
            var riverMentioned = line.Contains("sông", StringComparison.OrdinalIgnoreCase) ||
                                 line.Contains("river", StringComparison.OrdinalIgnoreCase);
            if (riverMentioned && !HasItems(world, "rivers") && !HasItems(world, "water"))
                issues.Add(new Issue("MISSING_RIVER", id,
                    "Lời đọc nhắc tới sông, nhưng world không có rivers/water. Chỉ bổ sung nếu có căn cứ minh họa."));
            var capitalMentioned = line.Contains("kinh đô", StringComparison.OrdinalIgnoreCase) ||
                                   line.Contains("thành Đại La", StringComparison.OrdinalIgnoreCase);
            if (capitalMentioned && !HasItems(world, "buildings") && !HasItems(world, "villages"))
                issues.Add(new Issue("MISSING_SETTLEMENT", id,
                    "Lời đọc nhắc tới kinh đô/thành nhưng chỉ có nhãn; cân nhắc mô hình khu cư trú sơ đồ."));

            var cam = args["cam"] as JsonObject;
            if (cam is null) continue;
            var signature = cam.ToJsonString();
            if (previousCameras.TryGetValue(worldId, out var previous) && previous == signature)
                issues.Add(new Issue("REPEATED_CAMERA", id,
                    "Camera giống hệt cảnh trước sử dụng cùng world."));
            previousCameras[worldId] = signature;
            if (cam["c"] is JsonArray center && center.Count == 2 && world["size"] is JsonArray size &&
                size.Count == 2 && Number(center[0]) is double x && Number(center[1]) is double y &&
                Number(size[0]) is double width && Number(size[1]) is double depth &&
                (Math.Abs(x) > width / 2 || Math.Abs(y) > depth / 2))
                issues.Add(new Issue("CAMERA_OUTSIDE", id,
                    "Tâm camera nằm ngoài ranh giới world; có thể nhìn thấy vùng trống."));
        }
        return issues;
    }

    private static bool HasItems(JsonObject obj, string key) =>
        obj[key] is JsonArray values && values.Count > 0;

    private static bool ValidPoints(JsonNode? node, int min) =>
        node is JsonArray points && points.Count >= min && points.All(ValidPoint);

    private static bool ValidPoint(JsonNode? node) =>
        node is JsonArray point && point.Count == 2 &&
        ValidFinite(point[0]) && ValidFinite(point[1]);

    private static bool ValidPositive(JsonNode? node) => Number(node) is double n && n > 0;

    private static bool ValidFinite(JsonNode? node) => Number(node) is not null;

    private static double? Number(JsonNode? node) =>
        double.TryParse(node?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture,
            out var n) && double.IsFinite(n) ? n : null;

    private static bool SimilarColors(string? a, string? b)
    {
        if (!TryColor(a, out var x) || !TryColor(b, out var y)) return false;
        var d = Math.Sqrt(Math.Pow(x[0] - y[0], 2) + Math.Pow(x[1] - y[1], 2) +
            Math.Pow(x[2] - y[2], 2));
        return d < 40;
    }

    private static bool TryColor(string? hex, out int[] rgb)
    {
        rgb = [];
        if (hex is null || hex.Length != 7 || hex[0] != '#') return false;
        if (!int.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                out var value)) return false;
        rgb = [(value >> 16) & 255, (value >> 8) & 255, value & 255];
        return true;
    }
}
