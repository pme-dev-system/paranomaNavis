using System;
using PanoramaNavis.Core;
using Xunit;

namespace PanoramaNavis.Core.Tests
{
    public class EquirectangularConverterTests
    {
        private static readonly (CubeFace Face, byte R, byte G, byte B)[] FaceColors =
        {
            (CubeFace.Front, 255, 0, 0),
            (CubeFace.Right, 0, 255, 0),
            (CubeFace.Back, 0, 0, 255),
            (CubeFace.Left, 255, 255, 0),
            (CubeFace.Up, 255, 0, 255),
            (CubeFace.Down, 0, 255, 255),
        };

        private static CubeMapSet CreateSolidColorCube(int faceSize = 64)
        {
            var cube = new CubeMapSet();
            foreach (var (face, r, g, b) in FaceColors)
            {
                var buf = new PixelBuffer(faceSize, faceSize);
                buf.Fill(r, g, b);
                cube.SetFace(face, buf);
            }
            return cube;
        }

        private static (byte R, byte G, byte B) ExpectedColor(CubeFace face)
        {
            foreach (var (f, r, g, b) in FaceColors)
                if (f == face)
                    return (r, g, b);
            throw new ArgumentOutOfRangeException(nameof(face));
        }

        [Fact]
        public void Convert_OutputIsTwoToOne()
        {
            PixelBuffer output = EquirectangularConverter.Convert(CreateSolidColorCube(), 512);
            Assert.Equal(512, output.Width);
            Assert.Equal(256, output.Height);
        }

        [Theory]
        // 画像中央 = 前方(lon=0, lat=0)
        [InlineData(0.50, 0.50, CubeFace.Front)]
        // 中央から右へ1/4 = 東(lon=+90°) → Right面
        [InlineData(0.75, 0.50, CubeFace.Right)]
        // 左端/右端 = 後方(lon=±180°)
        [InlineData(0.01, 0.50, CubeFace.Back)]
        [InlineData(0.99, 0.50, CubeFace.Back)]
        // 中央から左へ1/4 = 西(lon=−90°) → Left面
        [InlineData(0.25, 0.50, CubeFace.Left)]
        // 上端 = 天頂、下端 = 天底
        [InlineData(0.50, 0.01, CubeFace.Up)]
        [InlineData(0.50, 0.99, CubeFace.Down)]
        public void Convert_SolidColorFaces_AppearAtExpectedRegions(
            double relX, double relY, CubeFace expectedFace)
        {
            PixelBuffer output = EquirectangularConverter.Convert(CreateSolidColorCube(), 512);

            int x = (int)(relX * output.Width);
            int y = (int)(relY * output.Height);
            var (r, g, b, _) = output.GetPixel(x, y);
            var expected = ExpectedColor(expectedFace);

            Assert.Equal(expected.R, r);
            Assert.Equal(expected.G, g);
            Assert.Equal(expected.B, b);
        }

        [Fact]
        public void Convert_CoversAllSixFaces()
        {
            PixelBuffer output = EquirectangularConverter.Convert(CreateSolidColorCube(), 256);

            // 全画素がいずれかの面の色（単色面なので補間しても面内は純色、境界のみ混色）
            // ここでは「6色すべてが出現する」ことを確認する
            var seen = new System.Collections.Generic.HashSet<(byte, byte, byte)>();
            for (int y = 0; y < output.Height; y++)
            {
                for (int x = 0; x < output.Width; x++)
                {
                    var (r, g, b, _) = output.GetPixel(x, y);
                    seen.Add((r, g, b));
                }
            }
            foreach (var (face, r, g, b) in FaceColors)
                Assert.True(seen.Contains((r, g, b)), $"面 {face} の色が出力に現れていません。");
        }

        [Fact]
        public void Convert_IncompleteCube_Throws()
        {
            var cube = new CubeMapSet();
            var buf = new PixelBuffer(16, 16);
            cube.SetFace(CubeFace.Front, buf);
            Assert.Throws<ArgumentException>(() => EquirectangularConverter.Convert(cube, 64));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(63)] // 奇数
        [InlineData(-8)]
        public void Convert_InvalidWidth_Throws(int width)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => EquirectangularConverter.Convert(CreateSolidColorCube(16), width));
        }

        [Fact]
        public void Convert_MismatchedFaceSizes_Throws()
        {
            var cube = new CubeMapSet();
            cube.SetFace(CubeFace.Front, new PixelBuffer(16, 16));
            Assert.Throws<ArgumentException>(() => cube.SetFace(CubeFace.Back, new PixelBuffer(32, 32)));
        }

        [Fact]
        public void Convert_NonSquareFace_Throws()
        {
            var cube = new CubeMapSet();
            Assert.Throws<ArgumentException>(() => cube.SetFace(CubeFace.Front, new PixelBuffer(32, 16)));
        }

        [Fact]
        public void Downscale_AveragesPixels()
        {
            var src = new PixelBuffer(4, 2);
            src.Fill(100, 100, 100);
            src.SetPixel(0, 0, 200, 200, 200);

            PixelBuffer result = EquirectangularConverter.Downscale(src, 2);

            Assert.Equal(2, result.Width);
            Assert.Equal(1, result.Height);
            // 左半分は (200+100+100+100)/4 = 125
            var (r, _, _, _) = result.GetPixel(0, 0);
            Assert.Equal(125, r);
            var (r2, _, _, _) = result.GetPixel(1, 0);
            Assert.Equal(100, r2);
        }
    }
}
