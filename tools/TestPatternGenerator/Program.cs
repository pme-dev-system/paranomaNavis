using System;
using System.IO;
using System.Linq;
using PanoramaNavis.Core;

// Navisworksなしで変換パイプラインを検証するテストパターン生成ツール。
//
// 合成シーン（空・地面・方位で色相が変わる水平帯・経緯度グリッド・北の赤帯）を
// 6面キューブマップとして生成し、正距円筒パノラマへ変換してBMP出力する。
// 面境界（±45°等）でグリッド線が直線として連続していれば、
// 撮影規約（CubeOrientation）と合成規約の一致が確認できる。
//
// 使い方: dotnet run --project tools/TestPatternGenerator [出力フォルダ] [--tour]
//   --tour を付けると viewer/tour-viewer.html の検証用に、合成平面図＋3地点の
//   パノラマ＋tour.json 一式（tour-sample/）も生成する。

const int FaceSize = 1024;
const int OutputWidth = 4096;

(byte R, byte G, byte B) ColorForDirection(Vec3 d)
{
    double lonDeg = Math.Atan2(d.X, d.Y) * 180.0 / Math.PI;
    double latDeg = Math.Asin(Math.Max(-1, Math.Min(1, d.Z))) * 180.0 / Math.PI;

    static double GridDist(double v, double m)
    {
        double r = Math.Abs(v) % m;
        return Math.Min(r, m - r);
    }

    // 経緯度グリッド（経線30°・緯線15°）
    bool isMeridian = GridDist(lonDeg, 30) < 0.35;
    bool isParallel = GridDist(latDeg, 15) < 0.35;
    if (isMeridian || isParallel)
        return (30, 30, 34);

    // 北（forward, lon=0）の目印: 赤い縦帯
    if (Math.Abs(lonDeg) < 2.5 && Math.Abs(latDeg) < 40)
        return (220, 50, 45);

    // 天頂・天底マーカー
    if (latDeg > 78) return (250, 250, 250);
    if (latDeg < -78) return (25, 20, 18);

    if (latDeg > 18)
    {
        double t = Math.Min(1, (latDeg - 18) / 60.0);
        return ((byte)(135 - 110 * t), (byte)(206 - 132 * t), (byte)(235 - 97 * t));
    }
    if (latDeg < -18)
    {
        double t = Math.Min(1, (-latDeg - 18) / 60.0);
        return ((byte)(176 - 96 * t), (byte)(144 - 80 * t), (byte)(112 - 64 * t));
    }

    // 水平帯: 方位で色相が変わるHSVバンド（経度連続性の確認用）
    double hue = (lonDeg + 180) / 360.0 * 6.0;
    int i = (int)hue % 6;
    double f = hue - Math.Floor(hue);
    const double V = 0.93, S = 0.80;
    double p = V * (1 - S), q = V * (1 - S * f), u = V * (1 - S * (1 - f));
    double[] rgb = i switch
    {
        0 => new[] { V, u, p },
        1 => new[] { q, V, p },
        2 => new[] { p, V, u },
        3 => new[] { p, q, V },
        4 => new[] { u, p, V },
        _ => new[] { V, p, q },
    };
    return ((byte)(rgb[0] * 255), (byte)(rgb[1] * 255), (byte)(rgb[2] * 255));
}

var cube = new CubeMapSet();
foreach (FaceBasis basis in CubeOrientation.Faces)
{
    var buf = new PixelBuffer(FaceSize, FaceSize);
    for (int y = 0; y < FaceSize; y++)
    {
        double v = 1.0 - (y + 0.5) * 2.0 / FaceSize;
        for (int x = 0; x < FaceSize; x++)
        {
            double u = (x + 0.5) * 2.0 / FaceSize - 1.0;
            Vec3 d = CubeOrientation.FaceUVToDirection(basis.Face, u, v);
            var (r, g, b) = ColorForDirection(d);
            buf.SetPixel(x, y, r, g, b);
        }
    }
    cube.SetFace(basis.Face, buf);
}

Console.WriteLine("6面生成完了。正距円筒へ変換中...");
PixelBuffer pano = EquirectangularConverter.Convert(cube, OutputWidth);

static void WriteBmp(PixelBuffer img, string path)
{
    int w = img.Width, h = img.Height;
    int rowBytes = w * 3;
    int padding = (4 - rowBytes % 4) % 4;
    int dataSize = (rowBytes + padding) * h;
    using var fs = new FileStream(path, FileMode.Create);
    using var bw = new BinaryWriter(fs);
    bw.Write((ushort)0x4D42);        // "BM"
    bw.Write(54 + dataSize);
    bw.Write(0);
    bw.Write(54);
    bw.Write(40);                    // BITMAPINFOHEADER
    bw.Write(w);
    bw.Write(h);
    bw.Write((ushort)1);
    bw.Write((ushort)24);
    bw.Write(0);
    bw.Write(dataSize);
    bw.Write(2835);
    bw.Write(2835);
    bw.Write(0);
    bw.Write(0);
    var pad = new byte[padding];
    for (int y = h - 1; y >= 0; y--)
    {
        for (int x = 0; x < w; x++)
        {
            var (r, g, b, _) = img.GetPixel(x, y);
            bw.Write(b);
            bw.Write(g);
            bw.Write(r);
        }
        bw.Write(pad);
    }
}

bool makeTour = args.Contains("--tour");
string outDir = args.FirstOrDefault(a => !a.StartsWith("--")) ?? Directory.GetCurrentDirectory();
Directory.CreateDirectory(outDir);

WriteBmp(pano, Path.Combine(outDir, "test-panorama.bmp"));
foreach (FaceBasis basis in CubeOrientation.Faces)
{
    string name = $"test-face-{basis.Face.ToString().ToLowerInvariant()}.bmp";
    WriteBmp(cube[basis.Face], Path.Combine(outDir, name));
}
Console.WriteLine("出力先: " + Path.GetFullPath(outDir));
Console.WriteLine("test-panorama.bmp を viewer/panorama-viewer.html で開いて確認してください。");

if (makeTour)
    GenerateTourSample(outDir, cube);

// ---- ツアーサンプル生成（viewer/tour-viewer.html の検証用） ----
//
// アドインの出力と同じ構成（plan + P0x/panorama + tour.json）を合成データで作る。
// 建物 20m×15m（mm単位）を想定し、平面図の範囲はアドインと同じ「モデル範囲＋3%余白」。
void GenerateTourSample(string baseDir, CubeMapSet sourceCube)
{
    Console.WriteLine("ツアーサンプルを生成中...");
    string tourDir = Path.Combine(baseDir, "tour-sample");
    Directory.CreateDirectory(tourDir);

    const double MinX = 0, MinY = 0, MaxX = 20000, MaxY = 15000;
    const double FloorZ = 0, Thickness = 3000, EyeHeight = 1600;
    double planMargin = Math.Max(MaxX - MinX, MaxY - MinY) * 0.03;
    PlanMapping mapping = PlanMapping.FitToLongSide(
        MinX - planMargin, MinY - planMargin, MaxX + planMargin, MaxY + planMargin, 1024);

    WriteBmp(RenderSamplePlan(mapping, MinX, MinY, MaxX, MaxY), Path.Combine(tourDir, "plan.bmp"));

    var tour = new TourMetadata
    {
        TourId = "Tour_Sample",
        CreatedAt = DateTimeOffset.Now,
        CreatedBy = "TestPatternGenerator",
        SourceDocument = "synthetic://sample-building",
        ModelUnits = "Millimeters",
        EyeHeight = EyeHeight,
        Plan = new TourPlanInfo
        {
            ImageFile = "plan.bmp",
            Mapping = mapping,
            FloorZ = FloorZ,
            SlabThickness = Thickness,
            SectionClipApplied = true,
        },
    };

    // ビューアで地点切替が判別できるよう、地点ごとに色味を変えたパノラマを出力する
    PixelBuffer tourPano = EquirectangularConverter.Convert(sourceCube, 2048);
    var samplePoints = new[]
    {
        (Id: "P01", Name: "機械室", X: 5000.0, Y: 11000.0),
        (Id: "P02", Name: "P02", X: 15000.0, Y: 11000.0),
        (Id: "P03", Name: "P03", X: 10000.0, Y: 4000.0),
    };
    for (int i = 0; i < samplePoints.Length; i++)
    {
        var p = samplePoints[i];
        Directory.CreateDirectory(Path.Combine(tourDir, p.Id));
        WriteBmp(TintCopy(tourPano, i), Path.Combine(tourDir, p.Id, "panorama.bmp"));

        var pixel = mapping.WorldToPixel(p.X, p.Y);
        tour.Points.Add(new TourPointRecord
        {
            Id = p.Id,
            Name = p.Name,
            PixelX = pixel.X,
            PixelY = pixel.Y,
            Position = new Vec3(p.X, p.Y, FloorZ + EyeHeight),
            PanoramaFile = p.Id + "/panorama.bmp",
            ThumbnailFile = p.Id + "/panorama.bmp",
        });
    }

    File.WriteAllText(Path.Combine(tourDir, "tour.json"), tour.ToJson());
    Console.WriteLine("ツアーサンプル出力先: " + Path.GetFullPath(tourDir));
    Console.WriteLine("フォルダごと viewer/tour-viewer.html へドラッグして確認してください。");
}

// 合成平面図: 外壁・間仕切り壁・1mグリッド・北向き矢印（赤）を持つ簡易プラン
PixelBuffer RenderSamplePlan(PlanMapping mapping, double minX, double minY, double maxX, double maxY)
{
    var plan = new PixelBuffer(mapping.ImageWidth, mapping.ImageHeight);
    for (int py = 0; py < mapping.ImageHeight; py++)
    {
        for (int px = 0; px < mapping.ImageWidth; px++)
        {
            var (x, y) = mapping.PixelToWorld(px + 0.5, py + 0.5);
            (byte r, byte g, byte b) color;

            bool inside = x >= minX && x <= maxX && y >= minY && y <= maxY;
            if (!inside)
            {
                color = ((byte)226, (byte)228, (byte)232); // 敷地外
            }
            else if (x <= minX + 200 || x >= maxX - 200 || y <= minY + 200 || y >= maxY - 200)
            {
                color = ((byte)55, (byte)58, (byte)66); // 外壁
            }
            else if (Math.Abs(x - 10000) < 100 && y > 6000 && !(y > 9200 && y < 10200))
            {
                color = ((byte)55, (byte)58, (byte)66); // 間仕切り壁（縦・開口あり）
            }
            else if (Math.Abs(y - 7500) < 100 && x < 10000 && !(x > 4200 && x < 5200))
            {
                color = ((byte)55, (byte)58, (byte)66); // 間仕切り壁（横・開口あり）
            }
            else if (y > 13600 && y < 14500 && Math.Abs(x - 18500) < 350 * (14500 - y) / 900)
            {
                color = ((byte)210, (byte)55, (byte)50); // 北向き矢印
            }
            else if (Math.Abs(x % 1000) < 40 || Math.Abs(y % 1000) < 40)
            {
                color = ((byte)208, (byte)212, (byte)218); // 1mグリッド
            }
            else
            {
                color = ((byte)246, (byte)247, (byte)249); // 床
            }

            plan.SetPixel(px, py, color.r, color.g, color.b);
        }
    }
    return plan;
}

// 地点インデックスに応じて色味を変えたコピーを返す（0:そのまま 1:赤強調 2:緑強調）
PixelBuffer TintCopy(PixelBuffer source, int pointIndex)
{
    var copy = new PixelBuffer(source.Width, source.Height);
    Array.Copy(source.Data, copy.Data, source.Data.Length);
    if (pointIndex == 0)
        return copy;

    int channel = pointIndex == 1 ? 0 : 1;
    for (int i = channel; i < copy.Data.Length; i += 4)
        copy.Data[i] = (byte)Math.Min(255, copy.Data[i] * 1.4);
    return copy;
}
