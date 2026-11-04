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
                    // Validate geometry and proportions, not just field presence.
                    var pointList = feature["pts"] as JsonArray;
                    if (pointList is not null && ValidPoints(pointList,
                        PolygonLayers.Contains(layer) ? 3 : 2))
                    {
                        if (PolygonLayers.Contains(layer))
                        {
                            var polygonArea = PolygonArea(pointList);
                            var worldArea = WorldArea(world);
                            if (worldArea > 0 && polygonArea / worldArea > 0.11 &&
                                layer == "fields")
                                issues.Add(new Issue("OVERSIZED_FIELD", id,
                                    layer + "[" + i + "] chiếm hơn 11% bản đồ, dễ thành mảng màu lớn phẳng."));
                            if (polygonArea < 0.01)
                                issues.Add(new Issue("DEGENERATE_POLYGON", id,
                                    layer + "[" + i + "] có diện tích gần bằng 0.", true));
                        }
                        if (LineLayers.Contains(layer))
                        {
                            if (layer == "rivers" && pointList.Count < 4)
                                issues.Add(new Issue("ANGULAR_RIVER", id,
                                    "Sông chỉ có " + pointList.Count +
                                    " điểm; thêm điểm cong có cơ sở hoặc phân đoạn mượt hơn."));
                            if (HasSharpBends(pointList))
                                issues.Add(new Issue("SHARP_BENDS", id,
                                    layer + "[" + i + "] có góc gấp khúc gắt; kiểm tra lại đường nét."));
                        }
                        if (WorldArea(world) > 0 && GeometryBoundsOutside(pointList, world))
                            issues.Add(new Issue("GEOMETRY_OUTSIDE", id,
                                layer + "[" + i + "] có điểm nằm ngoài world; kiểm tra tọa độ."));
                    }
                    if (layer is "buildings" or "villages" &&
                        ValidPoint(feature["p"]) && IsOutside(feature["p"]!, world))
                        issues.Add(new Issue("BUILDING_OUTSIDE", id,
                            layer + "[" + i + "] nằm ngoài world."));
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
            // This is an approximate framing heuristic, NOT a projected screen-space measurement.
            if (world["size"] is JsonArray dimensions && dimensions.Count == 2 &&
                Number(dimensions[0]) is double w && Number(dimensions[1]) is double d &&
                Number(cam["dist"]) is double dist && dist > Math.Max(w, d) * 1.5)
                issues.Add(new Issue("DISTANT_CAMERA", id,
                    "Khoảng cách camera > 1,5 lần chiều rộng world; chủ thể có thể quá nhỏ."));
            if (Number(cam["el"]) is double elev && elev > 72 &&
                (HasItems(world, "buildings") || HasItems(world, "villages")))
                issues.Add(new Issue("TOPDOWN_SETTLEMENT", id,
                    "Camera gần thẳng đứng, khó nhìn chiều cao công trình/khu dân cư."));
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

    private static double WorldArea(JsonObject world)
    {
        if (world["size"] is not JsonArray size || size.Count != 2 ||
            Number(size[0]) is not double w || Number(size[1]) is not double d)
            return 0;
        return w * d;
    }

    private static double PolygonArea(JsonArray points)
    {
        var sum = 0.0;
        for (var i = 0; i < points.Count; i++)
        {
            var a = (JsonArray)points[i]!;
            var b = (JsonArray)points[(i + 1) % points.Count]!;
            sum += Number(a[0])!.Value * Number(b[1])!.Value -
                   Number(b[0])!.Value * Number(a[1])!.Value;
        }
        return Math.Abs(sum) / 2;
    }

    private static bool GeometryBoundsOutside(JsonArray points, JsonObject world) =>
        points.Any(point => IsOutside(point!, world));

    private static bool IsOutside(JsonNode point, JsonObject world)
    {
        if (world["size"] is not JsonArray size || size.Count != 2 ||
            Number(size[0]) is not double width || Number(size[1]) is not double depth ||
            point is not JsonArray p || p.Count != 2 ||
            Number(p[0]) is not double x || Number(p[1]) is not double y) return false;
        return Math.Abs(x) > width / 2 || Math.Abs(y) > depth / 2;
    }

    private static bool HasSharpBends(JsonArray points)
    {
        for (var i = 1; i < points.Count - 1; i++)
        {
            var a = (JsonArray)points[i - 1]!;
            var b = (JsonArray)points[i]!;
            var c = (JsonArray)points[i + 1]!;
            var x1 = Number(a[0])!.Value - Number(b[0])!.Value;
            var y1 = Number(a[1])!.Value - Number(b[1])!.Value;
            var x2 = Number(c[0])!.Value - Number(b[0])!.Value;
            var y2 = Number(c[1])!.Value - Number(b[1])!.Value;
            var n1 = Math.Sqrt(x1 * x1 + y1 * y1);
            var n2 = Math.Sqrt(x2 * x2 + y2 * y2);
            if (n1 < 0.001 || n2 < 0.001) continue;
            // Interior angle < 65 degrees produces a conspicuous acute kink.
            if ((x1 * x2 + y1 * y2) / (n1 * n2) > Math.Cos(65 * Math.PI / 180))
                return true;
        }
        return false;
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
