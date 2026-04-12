using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MZResourceManager.Models;

namespace MZResourceManager.Services;

public static class MapRenderer
{
    private const int TileW = 48;
    private const int TileH = 48;
    private const int HalfW = 24;
    private const int HalfH = 24;

    // Ported from rmmz_core.js
    private static readonly int[][] Floor = [
        [2,4,1,4,2,3,1,3],[2,0,1,4,2,3,1,3],[2,4,3,0,2,3,1,3],[2,0,3,0,2,3,1,3],
        [2,4,1,4,2,3,3,1],[2,0,1,4,2,3,3,1],[2,4,3,0,2,3,3,1],[2,0,3,0,2,3,3,1],
        [2,4,1,4,2,1,1,3],[2,0,1,4,2,1,1,3],[2,4,3,0,2,1,1,3],[2,0,3,0,2,1,1,3],
        [2,4,1,4,2,1,3,1],[2,0,1,4,2,1,3,1],[2,4,3,0,2,1,3,1],[2,0,3,0,2,1,3,1],
        [0,4,1,4,0,3,1,3],[0,4,3,0,0,3,1,3],[0,4,1,4,0,3,3,1],[0,4,3,0,0,3,3,1],
        [2,2,1,2,2,3,1,3],[2,2,1,2,2,3,3,1],[2,2,1,2,2,1,1,3],[2,2,1,2,2,1,3,1],
        [2,4,3,4,2,3,3,3],[2,4,3,4,2,1,3,3],[2,0,3,4,2,3,3,3],[2,0,3,4,2,1,3,3],
        [2,4,1,4,2,5,1,5],[2,0,1,4,2,5,1,5],[2,4,3,0,2,5,1,5],[2,0,3,0,2,5,1,5],
        [0,4,3,4,0,3,3,3],[2,2,1,2,2,5,1,5],[0,2,1,2,0,3,1,3],[0,2,1,2,0,3,3,1],
        [2,2,3,2,2,3,3,3],[2,2,3,2,2,1,3,3],[2,4,3,4,2,5,3,5],[2,0,3,4,2,5,3,5],
        [0,4,1,4,0,5,1,5],[0,4,3,0,0,5,1,5],[0,2,3,2,0,3,3,3],[0,2,1,2,0,5,1,5],
        [0,4,3,4,0,5,3,5],[2,2,3,2,2,5,3,5],[0,2,3,2,0,5,3,5],[0,0,1,0,0,1,1,1],
    ];

    private static readonly int[][] Wall = [
        [2,2,1,2,2,1,1,1],[0,2,1,2,0,1,1,1],[2,0,1,0,2,1,1,1],[0,0,1,0,0,1,1,1],
        [2,2,3,2,2,1,3,1],[0,2,3,2,0,1,3,1],[2,0,3,0,2,1,3,1],[0,0,3,0,0,1,3,1],
        [2,2,1,2,2,3,1,3],[0,2,1,2,0,3,1,3],[2,0,1,0,2,3,1,3],[0,0,1,0,0,3,1,3],
        [2,2,3,2,2,3,3,3],[0,2,3,2,0,3,3,3],[2,0,3,0,2,3,3,3],[0,0,3,0,0,3,3,3],
    ];

    public static async Task<BitmapSource?> RenderAsync(
        int mapId, string gameFolder, IReadOnlyList<MzTileset> tilesets,
        CancellationToken ct = default)
    {
        var mapPath = System.IO.Path.Combine(gameFolder, "data", $"Map{mapId:D3}.json");
        if (!File.Exists(mapPath)) return null;

        MzMapData mapData;
        await using (var stream = File.OpenRead(mapPath))
        {
            mapData = await JsonSerializer.DeserializeAsync<MzMapData>(stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct)
                ?? throw new InvalidDataException();
        }
        if (mapData.Data.Length == 0) return null;

        var tileset = tilesets.FirstOrDefault(t => t.Id == mapData.TilesetId);
        if (tileset == null) return null;

        var sheetData = await Task.Run(() => DecodeSheets(gameFolder, tileset), ct);

        int pw = mapData.Width * TileW;
        int ph = mapData.Height * TileH;
        var pixels = await Task.Run(() => RenderPixels(mapData, pw, ph, sheetData), ct);

        var bmp = BitmapSource.Create(pw, ph, 96, 96, PixelFormats.Pbgra32, null, pixels, pw * 4);
        bmp.Freeze();
        return bmp;
    }

    // Sheet decoding
    private record SheetData(byte[] Pixels, int Width, int Height);

    private static Dictionary<int, SheetData> DecodeSheets(string gameFolder, MzTileset tileset)
    {
        var result = new Dictionary<int, SheetData>();
        var dir = System.IO.Path.Combine(gameFolder, "img", "tilesets");

        for (int i = 0; i < tileset.TilesetNames.Length; i++)
        {
            var name = tileset.TilesetNames[i];
            if (string.IsNullOrEmpty(name)) continue;

            string? path = null;
            foreach (var ext in new[] { ".png", ".jpg", ".jpeg" })
            {
                var p = System.IO.Path.Combine(dir, name + ext);
                if (File.Exists(p)) { path = p; break; }
            }
            if (path == null) continue;

            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();

                var conv = new FormatConvertedBitmap(bmp, PixelFormats.Pbgra32, null, 0);
                int stride = conv.PixelWidth * 4;
                var pixels = new byte[conv.PixelHeight * stride];
                conv.CopyPixels(pixels, stride, 0);

                result[i] = new SheetData(pixels, conv.PixelWidth, conv.PixelHeight);
            }
            catch { }
        }
        return result;
    }

    // Rendering

    private static byte[] RenderPixels(MzMapData map, int pw, int ph,
        Dictionary<int, SheetData> sheets)
    {
        var dst = new byte[pw * ph * 4];

        // Dark background
        for (int i = 0; i < dst.Length; i += 4)
        { dst[i] = 0x22; dst[i + 1] = 0x22; dst[i + 2] = 0x22; dst[i + 3] = 0xFF; }

        for (int layer = 0; layer < 4; layer++)
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    int tileId = map.Data[layer * map.Height * map.Width + y * map.Width + x];
                    if (tileId <= 0) continue;
                    DrawTile(dst, pw, tileId, x * TileW, y * TileH, sheets);
                }

        return dst;
    }

    private static void DrawTile(byte[] dst, int dstW, int tileId, int dx, int dy,
        Dictionary<int, SheetData> sheets)
    {
        if (tileId >= 2048)
        {
            DrawAutotile(dst, dstW, tileId, dx, dy, sheets);
        }
        else
        {
            int setNumber = tileId >= 1536 ? 4 : 5 + tileId / 256;
            if (!sheets.TryGetValue(setNumber, out var s)) return;

            int sx = ((tileId / 128 % 2) * 8 + (tileId % 8)) * TileW;
            int sy = (tileId % 256 / 8 % 16) * TileH;
            Blit(dst, dstW, s, sx, sy, dx, dy, TileW, TileH);
        }
    }

    private static void DrawAutotile(byte[] dst, int dstW, int tileId, int dx, int dy,
        Dictionary<int, SheetData> sheets)
    {
        int kind = (tileId - 2048) / 48;
        int shape = (tileId - 2048) % 48;
        int tx = kind % 8;
        int ty = kind / 8;
        int setNumber, bx, by;
        int[][] table;

        if (tileId < 2816) // A1
        {
            setNumber = 0;
            if (kind == 0) { bx = 0; by = 0; }
            else if (kind == 1) { bx = 0; by = 3; }
            else if (kind == 2) { bx = 6; by = 0; }
            else if (kind == 3) { bx = 6; by = 3; }
            else
            {
                bx = tx / 4 * 8 + (kind % 2 == 0 ? 0 : 6);
                by = ty * 6 + (tx / 2 % 2) * 3;
            }
            table = Floor;
        }
        else if (tileId < 4352) // A2
        {
            setNumber = 1;
            bx = tx * 2;
            by = (ty - 2) * 3;
            table = Floor;
        }
        else if (tileId < 5888) // A3
        {
            setNumber = 2;
            bx = tx * 2;
            by = (ty - 6) * 2;
            table = Wall;
        }
        else // A4
        {
            setNumber = 3;
            bx = tx * 2;
            by = (int)Math.Floor((ty - 10) * 2.5 + (ty % 2 == 1 ? 0.5 : 0));
            table = ty % 2 == 1 ? Wall : Floor;
        }

        if (!sheets.TryGetValue(setNumber, out var sheet)) return;

        shape = Math.Min(shape, table.Length - 1);
        var entry = table[shape];

        for (int i = 0; i < 4; i++)
        {
            int qsx = entry[i * 2];
            int qsy = entry[i * 2 + 1];
            int sx1 = (bx * 2 + qsx) * HalfW;
            int sy1 = (by * 2 + qsy) * HalfH;
            int dx1 = dx + (i % 2) * HalfW;
            int dy1 = dy + (i / 2) * HalfH;
            Blit(dst, dstW, sheet, sx1, sy1, dx1, dy1, HalfW, HalfH);
        }
    }

    private static void Blit(byte[] dst, int dstW, SheetData src,
        int srcX, int srcY, int dstX, int dstY, int w, int h)
    {
        if (srcX < 0 || srcY < 0 || dstX < 0 || dstY < 0) return;
        if (srcX + w > src.Width || srcY + h > src.Height) return;
        if (dstX + w > dstW) return;

        for (int row = 0; row < h; row++)
        {
            int srcOff = ((srcY + row) * src.Width + srcX) * 4;
            int dstOff = ((dstY + row) * dstW + dstX) * 4;

            for (int col = 0; col < w; col++, srcOff += 4, dstOff += 4)
            {
                byte a = src.Pixels[srcOff + 3];
                if (a == 0) continue;
                if (a == 255)
                {
                    dst[dstOff] = src.Pixels[srcOff];
                    dst[dstOff + 1] = src.Pixels[srcOff + 1];
                    dst[dstOff + 2] = src.Pixels[srcOff + 2];
                    dst[dstOff + 3] = 255;
                }
                else
                {
                    int inv = 255 - a;
                    dst[dstOff] = (byte)((src.Pixels[srcOff] * a + dst[dstOff] * inv) / 255);
                    dst[dstOff + 1] = (byte)((src.Pixels[srcOff + 1] * a + dst[dstOff + 1] * inv) / 255);
                    dst[dstOff + 2] = (byte)((src.Pixels[srcOff + 2] * a + dst[dstOff + 2] * inv) / 255);
                    dst[dstOff + 3] = (byte)Math.Min(255, dst[dstOff + 3] + a);
                }
            }
        }
    }
}
