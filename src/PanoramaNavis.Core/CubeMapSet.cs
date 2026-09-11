using System;

namespace PanoramaNavis.Core
{
    /// <summary>同一カメラ位置から撮影した6面画像の集合。全面が同一サイズの正方形であること。</summary>
    public sealed class CubeMapSet
    {
        private readonly PixelBuffer[] _faces = new PixelBuffer[6];

        public int FaceSize { get; private set; }

        public PixelBuffer this[CubeFace face]
        {
            get
            {
                PixelBuffer buf = _faces[(int)face];
                if (buf == null)
                    throw new InvalidOperationException($"面 {face} の画像が設定されていません。");
                return buf;
            }
        }

        public void SetFace(CubeFace face, PixelBuffer image)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));
            if (image.Width != image.Height)
                throw new ArgumentException($"面 {face} の画像が正方形ではありません ({image.Width}x{image.Height})。");
            if (FaceSize == 0)
                FaceSize = image.Width;
            else if (image.Width != FaceSize)
                throw new ArgumentException($"面 {face} のサイズ {image.Width} が他の面 {FaceSize} と一致しません。");
            _faces[(int)face] = image;
        }

        public bool IsComplete
        {
            get
            {
                foreach (PixelBuffer f in _faces)
                    if (f == null)
                        return false;
                return true;
            }
        }
    }
}
