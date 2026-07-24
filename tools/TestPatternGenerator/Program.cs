using System;
using System.IO;
using PanoramaNavis.Core;

// Navisworksなしで変換パイプラインを検証するテストパターン生成ツール。
//
// 合成シーン（空・地面・方位で色相が変わる水平帯・経緯度グリッド・北の赤帯）を
// 6面キューブマップとして生成し、正距円筒パノラマへ変換してBMP出力する。
// 面境界（±45°等）でグリッド線が直線として連続していれば、
// 撮影規約（CubeOrientation）と合成規約の一致が確認できる。
//
// 使い方: dotnet run --project tools/TestPatternGenerator [出力フォルダ]

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

string outDir = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
Directory.CreateDirectory(outDir);

WriteBmp(pano, Path.Combine(outDir, "test-panorama.bmp"));
foreach (FaceBasis basis in CubeOrientation.Faces)
{
    string name = $"test-face-{basis.Face.ToString().ToLowerInvariant()}.bmp";
    WriteBmp(cube[basis.Face], Path.Combine(outDir, name));
}
Console.WriteLine("出力先: " + Path.GetFullPath(outDir));
Console.WriteLine("test-panorama.bmp を viewer/panorama-viewer.html で開いて確認してください。");
