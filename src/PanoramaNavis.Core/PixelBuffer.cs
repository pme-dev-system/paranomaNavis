using System;

namespace PanoramaNavis.Core
{
    /// <summary>RGBA 8bit×4 の単純な画像バッファ。System.Drawing に依存しない。</summary>
    public sealed class PixelBuffer
    {
        public int Width { get; }
        public int Height { get; }

        /// <summary>行順（上→下）、画素順（左→右）、チャネル順 R,G,B,A。</summary>
        public byte[] Data { get; }

        public PixelBuffer(int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "画像サイズは正の値が必要です。");
            Width = width;
            Height = height;
            Data = new byte[(long)width * height * 4];
        }

        public int OffsetOf(int x, int y) => (y * Width + x) * 4;

        public void SetPixel(int x, int y, byte r, byte g, byte b, byte a = 255)
        {
            int o = OffsetOf(x, y);
            Data[o] = r;
            Data[o + 1] = g;
            Data[o + 2] = b;
            Data[o + 3] = a;
        }

        public (byte R, byte G, byte B, byte A) GetPixel(int x, int y)
        {
            int o = OffsetOf(x, y);
            return (Data[o], Data[o + 1], Data[o + 2], Data[o + 3]);
        }

        /// <summary>
        /// バイリニア補間サンプリング。範囲外は端の画素にクランプする。
        /// (x, y) は画素中心基準の連続座標（画素 (0,0) の中心が x=0, y=0）。
        /// </summary>
        public (byte R, byte G, byte B, byte A) SampleBilinearClamped(double x, double y)
        {
            int x0 = (int)Math.Floor(x);
            int y0 = (int)Math.Floor(y);
            double fx = x - x0;
            double fy = y - y0;

            int cx0 = Clamp(x0, 0, Width - 1);
            int cx1 = Clamp(x0 + 1, 0, Width - 1);
            int cy0 = Clamp(y0, 0, Height - 1);
            int cy1 = Clamp(y0 + 1, 0, Height - 1);

            int o00 = OffsetOf(cx0, cy0);
            int o10 = OffsetOf(cx1, cy0);
            int o01 = OffsetOf(cx0, cy1);
            int o11 = OffsetOf(cx1, cy1);

            double w00 = (1 - fx) * (1 - fy);
            double w10 = fx * (1 - fy);
            double w01 = (1 - fx) * fy;
            double w11 = fx * fy;

            byte Mix(int c)
            {
                double val = Data[o00 + c] * w00 + Data[o10 + c] * w10 +
                             Data[o01 + c] * w01 + Data[o11 + c] * w11;
                return (byte)Clamp((int)Math.Round(val), 0, 255);
            }

            return (Mix(0), Mix(1), Mix(2), Mix(3));
        }

        private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);

        /// <summary>単色で塗りつぶす（テスト・デバッグ用）。</summary>
        public void Fill(byte r, byte g, byte b, byte a = 255)
        {
            for (int i = 0; i < Data.Length; i += 4)
            {
                Data[i] = r;
                Data[i + 1] = g;
                Data[i + 2] = b;
                Data[i + 3] = a;
            }
        }
    }
}
