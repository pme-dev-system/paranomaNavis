using System;
using PanoramaNavis.Core;
using Xunit;

namespace PanoramaNavis.Core.Tests
{
    public class PixelBufferTests
    {
        [Fact]
        public void SetGetPixel_RoundTrips()
        {
            var buf = new PixelBuffer(4, 4);
            buf.SetPixel(2, 3, 10, 20, 30, 40);
            Assert.Equal(((byte)10, (byte)20, (byte)30, (byte)40), buf.GetPixel(2, 3));
        }

        [Fact]
        public void SampleBilinear_AtPixelCenter_ReturnsExactValue()
        {
            var buf = new PixelBuffer(2, 2);
            buf.SetPixel(0, 0, 100, 0, 0);
            buf.SetPixel(1, 0, 200, 0, 0);
            buf.SetPixel(0, 1, 50, 0, 0);
            buf.SetPixel(1, 1, 150, 0, 0);

            Assert.Equal(100, buf.SampleBilinearClamped(0, 0).R);
            Assert.Equal(200, buf.SampleBilinearClamped(1, 0).R);
        }

        [Fact]
        public void SampleBilinear_BetweenPixels_Interpolates()
        {
            var buf = new PixelBuffer(2, 1);
            buf.SetPixel(0, 0, 100, 0, 0);
            buf.SetPixel(1, 0, 200, 0, 0);

            Assert.Equal(150, buf.SampleBilinearClamped(0.5, 0).R);
        }

        [Fact]
        public void SampleBilinear_OutsideEdges_ClampsToBorder()
        {
            var buf = new PixelBuffer(2, 2);
            buf.Fill(0, 0, 0);
            buf.SetPixel(0, 0, 240, 0, 0);

            Assert.Equal(240, buf.SampleBilinearClamped(-5.0, -5.0).R);
        }

        [Fact]
        public void Constructor_InvalidSize_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PixelBuffer(0, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PixelBuffer(10, -1));
        }
    }
}
