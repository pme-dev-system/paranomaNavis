using System;
using System.Threading.Tasks;

namespace PanoramaNavis.Core
{
    /// <summary>
    /// キューブマップ6面を正距円筒図法（equirectangular）の360度画像へ変換する。
    /// 座標規約は CubeOrientation / docs/ARCHITECTURE.md に従う。
    /// </summary>
    public static class EquirectangularConverter
    {
        /// <param name="cube">6面すべてが設定済みのキューブマップ。</param>
        /// <param name="outputWidth">出力パノラマの幅（高さは幅の半分）。偶数であること。</param>
        /// <param name="faceFovDegrees">
        /// 各面の撮影画角（度）。既定90。オーバーラップ撮影時は90より大きい値を指定する。
        /// </param>
        public static PixelBuffer Convert(CubeMapSet cube, int outputWidth, double faceFovDegrees = 90.0)
        {
            if (cube == null)
                throw new ArgumentNullException(nameof(cube));
            if (!cube.IsComplete)
                throw new ArgumentException("キューブマップに未設定の面があります。", nameof(cube));
            if (outputWidth < 2 || outputWidth % 2 != 0)
                throw new ArgumentOutOfRangeException(nameof(outputWidth), "出力幅は2以上の偶数が必要です。");
            if (faceFovDegrees < 90.0 || faceFovDegrees >= 180.0)
                throw new ArgumentOutOfRangeException(nameof(faceFovDegrees), "面画角は90以上180未満が必要です。");

            int w = outputWidth;
            int h = outputWidth / 2;
            var output = new PixelBuffer(w, h);

            // 面画角90°のとき接平面座標は [-1,1]。90°超では tan(fov/2) で正規化する。
            double tanHalfFov = Math.Tan(faceFovDegrees * Math.PI / 180.0 / 2.0);
            int faceSize = cube.FaceSize;

            Parallel.For(0, h, j =>
            {
                double lat = Math.PI / 2 - (j + 0.5) / h * Math.PI;
                double cosLat = Math.Cos(lat);
                double sinLat = Math.Sin(lat);

                for (int i = 0; i < w; i++)
                {
                    double lon = (i + 0.5) / w * 2.0 * Math.PI - Math.PI;

                    // パノラマ基準フレーム: right=+X, forward=+Y, up=+Z / lon=0 が forward
                    var d = new Vec3(
                        cosLat * Math.Sin(lon),
                        cosLat * Math.Cos(lon),
                        sinLat);

                    CubeFace face = CubeOrientation.DirectionToFaceUV(d, out double u, out double v);

                    double un = u / tanHalfFov;
                    double vn = v / tanHalfFov;

                    double x = (un + 1.0) * 0.5 * faceSize - 0.5;
                    double y = (1.0 - vn) * 0.5 * faceSize - 0.5;

                    var (r, g, b, a) = cube[face].SampleBilinearClamped(x, y);
                    output.SetPixel(i, j, r, g, b, a);
                }
            });

            return output;
        }

        /// <summary>サムネイル等のための単純な縮小（ボックス平均）。</summary>
        public static PixelBuffer Downscale(PixelBuffer source, int targetWidth)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (targetWidth <= 0 || targetWidth > source.Width)
                throw new ArgumentOutOfRangeException(nameof(targetWidth));

            int targetHeight = Math.Max(1, (int)Math.Round((double)source.Height * targetWidth / source.Width));
            var output = new PixelBuffer(targetWidth, targetHeight);
            double sx = (double)source.Width / targetWidth;
            double sy = (double)source.Height / targetHeight;

            Parallel.For(0, targetHeight, j =>
            {
                int y0 = (int)(j * sy);
                int y1 = Math.Min(source.Height, Math.Max(y0 + 1, (int)((j + 1) * sy)));
                for (int i = 0; i < targetWidth; i++)
                {
                    int x0 = (int)(i * sx);
                    int x1 = Math.Min(source.Width, Math.Max(x0 + 1, (int)((i + 1) * sx)));
                    long r = 0, g = 0, b = 0, a = 0;
                    int count = 0;
                    for (int y = y0; y < y1; y++)
                    {
                        for (int x = x0; x < x1; x++)
                        {
                            var (pr, pg, pb, pa) = source.GetPixel(x, y);
                            r += pr; g += pg; b += pb; a += pa;
                            count++;
                        }
                    }
                    output.SetPixel(i, j,
                        (byte)(r / count), (byte)(g / count), (byte)(b / count), (byte)(a / count));
                }
            });

            return output;
        }
    }
}
