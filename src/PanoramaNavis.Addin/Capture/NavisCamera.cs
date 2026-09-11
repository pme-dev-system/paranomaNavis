using System;
using Autodesk.Navisworks.Api;
using PanoramaNavis.Core;

namespace PanoramaNavis.Addin.Capture
{
    /// <summary>
    /// Navisworksビューポイントのカメラ制御。
    /// 面の向きの規約はCore側の CubeOrientation を唯一の定義とし、
    /// ここではワールド座標への写像とViewpointへの適用のみを行う。
    /// </summary>
    internal static class NavisCamera
    {
        public static Vec3 ToVec3(Point3D p) => new Vec3(p.X, p.Y, p.Z);
        public static Point3D ToPoint3D(Vec3 v) => new Point3D(v.X, v.Y, v.Z);
        public static Vector3D ToVector3D(Vec3 v) => new Vector3D(v.X, v.Y, v.Z);

        /// <summary>
        /// パノラマ基準フレーム（right/forward/up）を張るワールド基底。
        /// forward は水平（up と直交）であること。
        /// </summary>
        public readonly struct PanoFrame
        {
            public readonly Vec3 Right;
            public readonly Vec3 Forward;
            public readonly Vec3 Up;

            public PanoFrame(Vec3 forward, Vec3 up)
            {
                Up = up.Normalized();
                Forward = forward.Horizontalized(Up);
                if (Forward.Equals(Vec3.Zero))
                    Forward = Vec3.UnitY.Horizontalized(Up);
                if (Forward.Equals(Vec3.Zero))
                    Forward = Vec3.UnitX.Horizontalized(Up);
                Right = Vec3.Cross(Forward, Up).Normalized();
            }

            /// <summary>正規基底 (x=right, y=forward, z=up) のベクトルをワールドへ写像する。</summary>
            public Vec3 ToWorld(Vec3 canonical) =>
                Right * canonical.X + Forward * canonical.Y + Up * canonical.Z;
        }

        /// <summary>
        /// 指定位置・指定キューブ面に向けてカメラを設定した新しいViewpointを返す。
        /// </summary>
        public static Viewpoint CreateFaceViewpoint(
            Viewpoint baseViewpoint, Vec3 position, PanoFrame frame, FaceBasis face, double fovRadians)
        {
            Viewpoint vp = baseViewpoint.CreateCopy();
            vp.Projection = ViewpointProjection.Perspective;
            vp.Position = ToPoint3D(position);

            Vec3 viewDirWorld = frame.ToWorld(face.Forward);
            Vec3 upWorld = frame.ToWorld(face.Up);

            // 先に視線を合わせ、その後ロールをUpベクトルで確定させる（順序が重要）
            vp.AlignDirection(ToVector3D(viewDirWorld));
            vp.AlignUp(ToVector3D(upWorld));

            // 垂直画角。正方形出力と併せて水平画角も同値になる
            vp.HeightField = fovRadians;

            TrySetAspectRatio(vp, 1.0);
            return vp;
        }

        /// <summary>
        /// Viewpoint.AspectRatio はNavisworksのバージョンにより存在しない可能性があるため、
        /// リフレクション経由で設定を試みる（存在しなければ何もしない）。
        /// 実機検証後、プロパティの存在が確認できたら直接代入へ置き換えること。
        /// </summary>
        internal static void TrySetAspectRatio(Viewpoint vp, double aspectRatio)
        {
            try
            {
                var prop = vp.GetType().GetProperty("AspectRatio");
                if (prop != null && prop.CanWrite)
                    prop.SetValue(vp, aspectRatio, null);
            }
            catch
            {
                // 画角の縦横比は出力画像サイズ側でも担保されるため、失敗しても続行する
            }
        }
    }
}
