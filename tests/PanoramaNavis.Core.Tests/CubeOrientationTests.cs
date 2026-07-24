using System;
using PanoramaNavis.Core;
using Xunit;

namespace PanoramaNavis.Core.Tests
{
    public class CubeOrientationTests
    {
        private const double Eps = 1e-9;

        [Fact]
        public void AllFaceBases_AreRightHandedOrthonormal()
        {
            foreach (FaceBasis basis in CubeOrientation.Faces)
            {
                Assert.Equal(1.0, basis.Forward.Length, 9);
                Assert.Equal(1.0, basis.Up.Length, 9);
                Assert.Equal(1.0, basis.Right.Length, 9);

                Assert.Equal(0.0, Vec3.Dot(basis.Forward, basis.Up), 9);
                Assert.Equal(0.0, Vec3.Dot(basis.Forward, basis.Right), 9);
                Assert.Equal(0.0, Vec3.Dot(basis.Up, basis.Right), 9);

                // 画像右 = 視線 × 画像上（右手系の規約）
                Vec3 expectedRight = Vec3.Cross(basis.Forward, basis.Up);
                Assert.True((basis.Right - expectedRight).Length < Eps);
            }
        }

        [Theory]
        [InlineData(0, 1, 0, CubeFace.Front)]
        [InlineData(1, 0, 0, CubeFace.Right)]
        [InlineData(0, -1, 0, CubeFace.Back)]
        [InlineData(-1, 0, 0, CubeFace.Left)]
        [InlineData(0, 0, 1, CubeFace.Up)]
        [InlineData(0, 0, -1, CubeFace.Down)]
        public void AxisDirections_MapToExpectedFaceCenters(double x, double y, double z, CubeFace expected)
        {
            CubeFace face = CubeOrientation.DirectionToFaceUV(new Vec3(x, y, z), out double u, out double v);

            Assert.Equal(expected, face);
            Assert.Equal(0.0, u, 9);
            Assert.Equal(0.0, v, 9);
        }

        [Fact]
        public void FaceCenters_RoundTripToForwardVector()
        {
            foreach (FaceBasis basis in CubeOrientation.Faces)
            {
                Vec3 dir = CubeOrientation.FaceUVToDirection(basis.Face, 0, 0);
                Assert.True((dir - basis.Forward).Length < Eps);
            }
        }

        [Fact]
        public void RandomDirections_RoundTripThroughFaceUV()
        {
            var random = new Random(42);
            for (int i = 0; i < 2000; i++)
            {
                var d = new Vec3(
                    random.NextDouble() * 2 - 1,
                    random.NextDouble() * 2 - 1,
                    random.NextDouble() * 2 - 1);
                if (d.Length < 1e-3)
                    continue;
                d = d.Normalized();

                CubeFace face = CubeOrientation.DirectionToFaceUV(d, out double u, out double v);
                Vec3 restored = CubeOrientation.FaceUVToDirection(face, u, v);

                Assert.True(Vec3.Dot(d, restored) > 1.0 - 1e-9,
                    $"往復で方向がずれました: {d} -> {face}({u:0.###},{v:0.###}) -> {restored}");
            }
        }

        [Fact]
        public void RandomDirections_UVAlwaysWithinFaceBounds()
        {
            var random = new Random(7);
            for (int i = 0; i < 2000; i++)
            {
                var d = new Vec3(
                    random.NextDouble() * 2 - 1,
                    random.NextDouble() * 2 - 1,
                    random.NextDouble() * 2 - 1);
                if (d.Length < 1e-3)
                    continue;

                CubeOrientation.DirectionToFaceUV(d, out double u, out double v);

                // 面画角90°の接平面座標は [-1, +1] に収まる（面選択が最大軸のため）
                Assert.InRange(u, -1.0 - Eps, 1.0 + Eps);
                Assert.InRange(v, -1.0 - Eps, 1.0 + Eps);
            }
        }

        [Fact]
        public void UpFace_TopOfImage_PointsBackward()
        {
            // 真上面の画像上端(v=+1)は後方(-Y)を向く規約（OpenGLキューブマップと同型）
            Vec3 dir = CubeOrientation.FaceUVToDirection(CubeFace.Up, 0, 1);
            Assert.True(dir.Y < 0);
            Assert.True(dir.Z > 0);

            // 真下面の画像上端は前方(+Y)
            Vec3 dirDown = CubeOrientation.FaceUVToDirection(CubeFace.Down, 0, 1);
            Assert.True(dirDown.Y > 0);
            Assert.True(dirDown.Z < 0);
        }
    }
}
