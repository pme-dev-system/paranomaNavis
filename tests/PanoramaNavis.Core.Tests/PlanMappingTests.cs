using System;
using PanoramaNavis.Core;
using Xunit;

namespace PanoramaNavis.Core.Tests
{
    public class PlanMappingTests
    {
        // 20m × 15m の範囲を 2048px 長辺で（横長 → 幅2048・高さ1536）
        private static PlanMapping CreateSample() =>
            PlanMapping.FitToLongSide(1000, 2000, 21000, 17000, 2048);

        [Fact]
        public void FitToLongSide_KeepsAspectRatio()
        {
            PlanMapping m = CreateSample();
            Assert.Equal(2048, m.ImageWidth);
            Assert.Equal(1536, m.ImageHeight); // 2048 * 15/20

            PlanMapping tall = PlanMapping.FitToLongSide(0, 0, 10, 40, 1000);
            Assert.Equal(1000, tall.ImageHeight);
            Assert.Equal(250, tall.ImageWidth);
        }

        [Fact]
        public void PixelToWorld_TopLeftIsMinXMaxY()
        {
            // 画像上=+Y（北）の規約: 左上隅 = (MinX, MaxY)、右下隅 = (MaxX, MinY)
            PlanMapping m = CreateSample();

            var topLeft = m.PixelToWorld(0, 0);
            Assert.Equal(1000, topLeft.X, 6);
            Assert.Equal(17000, topLeft.Y, 6);

            var bottomRight = m.PixelToWorld(m.ImageWidth, m.ImageHeight);
            Assert.Equal(21000, bottomRight.X, 6);
            Assert.Equal(2000, bottomRight.Y, 6);
        }

        [Fact]
        public void PixelToWorld_CenterIsWorldCenter()
        {
            PlanMapping m = CreateSample();
            var center = m.PixelToWorld(m.ImageWidth / 2.0, m.ImageHeight / 2.0);
            Assert.Equal(11000, center.X, 6);
            Assert.Equal(9500, center.Y, 6);
        }

        [Fact]
        public void WorldToPixel_RoundTripsWithPixelToWorld()
        {
            PlanMapping m = CreateSample();
            var random = new Random(42);
            for (int i = 0; i < 100; i++)
            {
                double px = random.NextDouble() * m.ImageWidth;
                double py = random.NextDouble() * m.ImageHeight;
                var world = m.PixelToWorld(px, py);
                var back = m.WorldToPixel(world.X, world.Y);
                Assert.Equal(px, back.X, 6);
                Assert.Equal(py, back.Y, 6);
            }
        }

        [Fact]
        public void WorldToPixel_NorthIsUp()
        {
            // Yが大きい（北にある）点ほど画像上では上（pixelYが小さい）
            PlanMapping m = CreateSample();
            var south = m.WorldToPixel(11000, 3000);
            var north = m.WorldToPixel(11000, 16000);
            Assert.True(north.Y < south.Y);

            // Xが大きい（東にある）点ほど画像では右
            var west = m.WorldToPixel(2000, 9500);
            var east = m.WorldToPixel(20000, 9500);
            Assert.True(east.X > west.X);
        }

        [Fact]
        public void ContainsPixel_DetectsBounds()
        {
            PlanMapping m = CreateSample();
            Assert.True(m.ContainsPixel(0, 0));
            Assert.True(m.ContainsPixel(2048, 1536));
            Assert.False(m.ContainsPixel(-1, 100));
            Assert.False(m.ContainsPixel(100, 1537));
        }

        [Fact]
        public void Constructor_RejectsInvalidRange()
        {
            Assert.Throws<ArgumentException>(() => new PlanMapping(10, 0, 10, 5, 100, 100));
            Assert.Throws<ArgumentException>(() => new PlanMapping(0, 5, 10, 5, 100, 100));
            Assert.Throws<ArgumentException>(() => new PlanMapping(0, 0, 10, 5, 0, 100));
            Assert.Throws<ArgumentException>(() => PlanMapping.FitToLongSide(0, 0, 10, 5, 0));
        }
    }
}
