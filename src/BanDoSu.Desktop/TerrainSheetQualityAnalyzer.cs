using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BanDoSu.Desktop;

/// <summary>
/// Lightweight image inspection for H2's 4-column render sheets. This is a
/// pixel-statistics warning system, not semantic AI Vision or historical QA.
/// </summary>
internal static class TerrainSheetQualityAnalyzer
{
    public static IReadOnlyList<string> Analyze(string sheetsDirectory, int shotCount)
    {
        var warnings = new List<string>();
        if (shotCount <= 0 || !Directory.Exists(sheetsDirectory))
            return warnings;

        var sheets = Directory.GetFiles(sheetsDirectory, "sheet_*.jpg")
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        var processed = 0;
        foreach (var file in sheets)
        {
            if (processed >= shotCount) break;
            try
            {
                var decoder = BitmapDecoder.Create(new Uri(file), BitmapCreateOptions.None,
                    BitmapCacheOption.OnLoad);
                var frame = decoder.Frames[0];
                var bitmap = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
                var width = bitmap.PixelWidth;
                var height = bitmap.PixelHeight;
                var stride = checked(width * 4);
                var pixels = new byte[checked(stride * height)];
                bitmap.CopyPixels(pixels, stride, 0);

                // H2 makeSheet(..., 4, 480) renders four columns with label strips.
                // The core of each frame is sampled away from its labels and borders.
                var columns = Math.Min(4, shotCount - processed);
                var cellWidth = width / 4;
                var rows = Math.Min(4, (shotCount - processed + 3) / 4);
                var cellHeight = Math.Max(1, height / rows);
                for (var i = 0; i < columns * rows && processed < shotCount; i++)
                {
                    var column = i % 4;
                    var row = i / 4;
                    var x0 = column * cellWidth + cellWidth / 7;
                    var x1 = Math.Min(width, (column + 1) * cellWidth - cellWidth / 7);
                    var y0 = row * cellHeight + cellHeight / 5;
                    var y1 = Math.Min(height, (row + 1) * cellHeight - cellHeight / 4);
                    if (x1 <= x0 || y1 <= y0) { processed++; continue; }
                    var samples = 0;
                    double sum = 0, sq = 0, sat = 0;
                    for (var y = y0; y < y1; y += 5)
                    for (var x = x0; x < x1; x += 5)
                    {
                        var ix = y * stride + x * 4;
                        var b = pixels[ix];
                        var g = pixels[ix + 1];
                        var r = pixels[ix + 2];
                        var high = Math.Max(r, Math.Max(g, b));
                        var low = Math.Min(r, Math.Min(g, b));
                        var l = 0.2126 * r + 0.7152 * g + 0.0722 * b;
                        sum += l;
                        sq += l * l;
                        sat += high == 0 ? 0 : (high - low) / (double)high;
                        samples++;
                    }
                    if (samples > 0)
                    {
                        var contrast = Math.Sqrt(Math.Max(0, sq / samples -
                            Math.Pow(sum / samples, 2)));
                        var saturation = sat / samples;
                        if (contrast < 13 && saturation < 0.23)
                            warnings.Add($"Cảnh {processed + 1:D3}: ảnh ít tương phản/màu " +
                                $"(độ lệch sáng {contrast:0.0}, bão hòa {saturation:0.00}). " +
                                "Kiểm tra camera, haze và màu vật liệu.");
                    }
                    processed++;
                }
            }
            catch (Exception ex)
            {
                warnings.Add("Không thể phân tích ảnh " + Path.GetFileName(file) + ": " + ex.Message);
            }
        }
        return warnings;
    }
}
