using System;

namespace PanoramaNavis.Core
{
    /// <summary>
    /// 平面図画像とワールドXY座標の相互変換（アフィン写像）。
    ///
    /// 平面図の撮影規約（docs/ARCHITECTURE.md「平面図の規約」）:
    ///   真上（-Z方向視線）からの正射投影で、画像上=+Y（北）、画像右=+X（東）。
    /// ピクセル座標は左上原点の連続値で、(0,0)=左上隅、(ImageWidth, ImageHeight)=右下隅。
    /// ピクセル(i, j)の中心は (i+0.5, j+0.5)。
    /// </summary>
    public sealed class PlanMapping
    {
        /// <summary>画像がカバーするワールドXY範囲。</summary>
        public double WorldMinX { get; }
        public double WorldMinY { get; }
        public double WorldMaxX { get; }
        public double WorldMaxY { get; }

        public int ImageWidth { get; }
        public int ImageHeight { get; }

        public PlanMapping(
            double worldMinX, double worldMinY, double worldMaxX, double worldMaxY,
            int imageWidth, int imageHeight)
        {
            if (!(worldMaxX > worldMinX) || !(worldMaxY > worldMinY))
                throw new ArgumentException("ワールド範囲が不正です（max が min 以下、またはNaN）。");
            if (imageWidth <= 0 || imageHeight <= 0)
                throw new ArgumentException("画像サイズは正の値を指定してください。");

            WorldMinX = worldMinX;
            WorldMinY = worldMinY;
            WorldMaxX = worldMaxX;
            WorldMaxY = worldMaxY;
            ImageWidth = imageWidth;
            ImageHeight = imageHeight;
        }

        /// <summary>
        /// ワールド範囲と長辺ピクセル数から、縦横比を保った画像サイズのマッピングを作る。
        /// </summary>
        public static PlanMapping FitToLongSide(
            double worldMinX, double worldMinY, double worldMaxX, double worldMaxY, int longSidePixels)
        {
            if (longSidePixels <= 0)
                throw new ArgumentException("長辺ピクセル数は正の値を指定してください。");

            double w = worldMaxX - worldMinX;
            double h = worldMaxY - worldMinY;
            int imageWidth, imageHeight;
            if (w >= h)
            {
                imageWidth = longSidePixels;
                imageHeight = Math.Max(1, (int)Math.Round(longSidePixels * h / w));
            }
            else
            {
                imageHeight = longSidePixels;
                imageWidth = Math.Max(1, (int)Math.Round(longSidePixels * w / h));
            }
            return new PlanMapping(worldMinX, worldMinY, worldMaxX, worldMaxY, imageWidth, imageHeight);
        }

        /// <summary>1ピクセルあたりのワールド距離（X方向）。</summary>
        public double WorldPerPixelX => (WorldMaxX - WorldMinX) / ImageWidth;

        /// <summary>1ピクセルあたりのワールド距離（Y方向）。</summary>
        public double WorldPerPixelY => (WorldMaxY - WorldMinY) / ImageHeight;

        /// <summary>画像ピクセル座標（連続値・左上原点）→ ワールドXY。</summary>
        public (double X, double Y) PixelToWorld(double pixelX, double pixelY)
        {
            double x = WorldMinX + pixelX * WorldPerPixelX;
            double y = WorldMaxY - pixelY * WorldPerPixelY; // 画像上が+Y（北）のため反転
            return (x, y);
        }

        /// <summary>ワールドXY → 画像ピクセル座標（連続値・左上原点）。</summary>
        public (double X, double Y) WorldToPixel(double worldX, double worldY)
        {
            double px = (worldX - WorldMinX) / WorldPerPixelX;
            double py = (WorldMaxY - worldY) / WorldPerPixelY;
            return (px, py);
        }

        /// <summary>ピクセル座標が画像内かどうか。</summary>
        public bool ContainsPixel(double pixelX, double pixelY) =>
            pixelX >= 0 && pixelX <= ImageWidth && pixelY >= 0 && pixelY <= ImageHeight;
    }
}
