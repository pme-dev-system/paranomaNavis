using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using PanoramaNavis.Core;

namespace PanoramaNavis.Addin.Imaging
{
    /// <summary>
    /// System.Drawing (GDI+) と PixelBuffer の相互変換。
    /// GDI+の32bppArgbはメモリ上 B,G,R,A 順のため、Core側のR,G,B,A順と入れ替える。
    /// </summary>
    internal static class GdiPixelBufferIO
    {
        public static PixelBuffer LoadImage(string path)
        {
            using (var src = new Bitmap(path))
            using (var bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height));
                }

                var buffer = new PixelBuffer(bmp.Width, bmp.Height);
                var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
                BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int rowBytes = bmp.Width * 4;
                    var row = new byte[rowBytes];
                    for (int y = 0; y < bmp.Height; y++)
                    {
                        Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, rowBytes);
                        int o = buffer.OffsetOf(0, y);
                        for (int x = 0; x < rowBytes; x += 4)
                        {
                            buffer.Data[o + x] = row[x + 2];     // R
                            buffer.Data[o + x + 1] = row[x + 1]; // G
                            buffer.Data[o + x + 2] = row[x];     // B
                            buffer.Data[o + x + 3] = row[x + 3]; // A
                        }
                    }
                }
                finally
                {
                    bmp.UnlockBits(data);
                }
                return buffer;
            }
        }

        public static void SaveJpeg(PixelBuffer buffer, string path, int quality)
        {
            using (var bmp = new Bitmap(buffer.Width, buffer.Height, PixelFormat.Format32bppArgb))
            {
                var rect = new Rectangle(0, 0, buffer.Width, buffer.Height);
                BitmapData data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int rowBytes = buffer.Width * 4;
                    var row = new byte[rowBytes];
                    for (int y = 0; y < buffer.Height; y++)
                    {
                        int o = buffer.OffsetOf(0, y);
                        for (int x = 0; x < rowBytes; x += 4)
                        {
                            row[x] = buffer.Data[o + x + 2];     // B
                            row[x + 1] = buffer.Data[o + x + 1]; // G
                            row[x + 2] = buffer.Data[o + x];     // R
                            row[x + 3] = buffer.Data[o + x + 3]; // A
                        }
                        Marshal.Copy(row, 0, data.Scan0 + y * data.Stride, rowBytes);
                    }
                }
                finally
                {
                    bmp.UnlockBits(data);
                }

                ImageCodecInfo jpegCodec = ImageCodecInfo.GetImageEncoders()
                    .First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                using (var parameters = new EncoderParameters(1))
                using (var qualityParam = new EncoderParameter(Encoder.Quality, (long)quality))
                {
                    parameters.Param[0] = qualityParam;
                    bmp.Save(path, jpegCodec, parameters);
                }
            }
        }
    }
}
