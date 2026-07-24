namespace PanoramaNavis.Core
{
    /// <summary>撮影位置の決め方。</summary>
    public enum PositionSource
    {
        /// <summary>現在のビューポイントのカメラ位置をそのまま使う。</summary>
        CurrentViewpoint,

        /// <summary>選択中の部材のバウンディングボックス中心（床レベル）＋目線高さ。</summary>
        SelectedItem,

        /// <summary>XYZ座標を直接入力。</summary>
        ManualCoordinates,
    }

    /// <summary>パノラマ生成の設定値。ダイアログとサービスの間で受け渡す。</summary>
    public sealed class CaptureOptions
    {
        public PositionSource PositionSource { get; set; } = PositionSource.CurrentViewpoint;

        /// <summary>ManualCoordinates のときの撮影位置（モデル単位）。</summary>
        public Vec3 ManualPosition { get; set; } = Vec3.Zero;

        /// <summary>SelectedItem のときに床レベルへ加算する目線高さ（モデル単位）。</summary>
        public double EyeHeight { get; set; } = 1600.0;

        /// <summary>キューブ各面の一辺ピクセル数。</summary>
        public int FaceSize { get; set; } = 2048;

        /// <summary>各面の画角（度）。既定90。オーバーラップ撮影時のみ変更する。</summary>
        public double FaceFovDegrees { get; set; } = 90.0;

        /// <summary>出力パノラマの幅（高さは半分）。</summary>
        public int OutputWidth { get; set; } = 8192;

        /// <summary>サムネイルの幅。</summary>
        public int ThumbnailWidth { get; set; } = 1024;

        /// <summary>出力先ルートフォルダ。</summary>
        public string OutputDirectory { get; set; } = "";

        /// <summary>中間ファイル（6面PNG）を残すか。</summary>
        public bool KeepFaceImages { get; set; } = true;

        /// <summary>JPEG品質（1-100）。</summary>
        public int JpegQuality { get; set; } = 92;
    }
}
