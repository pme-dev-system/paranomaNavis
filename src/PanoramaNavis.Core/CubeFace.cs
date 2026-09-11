using System;

namespace PanoramaNavis.Core
{
    /// <summary>キューブマップの6面。</summary>
    public enum CubeFace
    {
        Front = 0,
        Right = 1,
        Back = 2,
        Left = 3,
        Up = 4,
        Down = 5,
    }

    /// <summary>
    /// 6面それぞれの (視線 Forward, 画像上 Up, 画像右 Right) 基底。
    ///
    /// この規約は「撮影側（アドインのカメラ制御）」と「合成側（正距円筒変換）」の
    /// 唯一の共有定義であり、両者はここ以外で面の向きを定義してはならない。
    /// 規約の詳細は docs/ARCHITECTURE.md「座標系規約」を参照。
    ///
    /// パノラマ基準フレーム: right=+X, forward=+Y, up=+Z の右手系。
    /// Right(画像右) = Forward × Up。
    /// </summary>
    public readonly struct FaceBasis
    {
        public readonly CubeFace Face;
        public readonly Vec3 Forward;
        public readonly Vec3 Up;
        public readonly Vec3 Right;

        public FaceBasis(CubeFace face, Vec3 forward, Vec3 up)
        {
            Face = face;
            Forward = forward;
            Up = up;
            Right = Vec3.Cross(forward, up);
        }
    }

    public static class CubeOrientation
    {
        public static readonly FaceBasis[] Faces =
        {
            new FaceBasis(CubeFace.Front, new Vec3( 0,  1,  0), new Vec3(0,  0,  1)),
            new FaceBasis(CubeFace.Right, new Vec3( 1,  0,  0), new Vec3(0,  0,  1)),
            new FaceBasis(CubeFace.Back,  new Vec3( 0, -1,  0), new Vec3(0,  0,  1)),
            new FaceBasis(CubeFace.Left,  new Vec3(-1,  0,  0), new Vec3(0,  0,  1)),
            new FaceBasis(CubeFace.Up,    new Vec3( 0,  0,  1), new Vec3(0, -1,  0)),
            new FaceBasis(CubeFace.Down,  new Vec3( 0,  0, -1), new Vec3(0,  1,  0)),
        };

        public static FaceBasis GetBasis(CubeFace face) => Faces[(int)face];

        /// <summary>
        /// 方向ベクトル d（正規化不要）が属する面と、その面の接平面座標 (u, v) を返す。
        /// u, v は面画角90°のとき [-1, +1]。u は画像右方向、v は画像上方向。
        /// </summary>
        public static CubeFace DirectionToFaceUV(Vec3 d, out double u, out double v)
        {
            double ax = Math.Abs(d.X), ay = Math.Abs(d.Y), az = Math.Abs(d.Z);

            CubeFace face;
            if (ax >= ay && ax >= az)
                face = d.X >= 0 ? CubeFace.Right : CubeFace.Left;
            else if (ay >= ax && ay >= az)
                face = d.Y >= 0 ? CubeFace.Front : CubeFace.Back;
            else
                face = d.Z >= 0 ? CubeFace.Up : CubeFace.Down;

            FaceBasis basis = Faces[(int)face];
            double forwardDist = Vec3.Dot(d, basis.Forward);
            // 面選択により forwardDist は必ず正（最大成分の軸を選ぶため）
            Vec3 t = d / forwardDist;
            u = Vec3.Dot(t, basis.Right);
            v = Vec3.Dot(t, basis.Up);
            return face;
        }

        /// <summary>
        /// 面と接平面座標 (u, v) から方向ベクトルを復元する（DirectionToFaceUV の逆変換）。
        /// </summary>
        public static Vec3 FaceUVToDirection(CubeFace face, double u, double v)
        {
            FaceBasis basis = Faces[(int)face];
            return (basis.Forward + basis.Right * u + basis.Up * v).Normalized();
        }
    }
}
