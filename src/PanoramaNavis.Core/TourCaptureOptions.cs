namespace PanoramaNavis.Core
{
    /// <summary>平面図の床レベル（断面下端）の決め方。</summary>
    public enum FloorLevelSource
    {
        /// <summary>現在のビューポイントの高さから（カメラZ − 目線高さ ＝ 床レベル）。</summary>
        CurrentViewpoint,

        /// <summary>選択中の部材の底面（BoundingBox の最小Z）を床レベルとする。</summary>
        SelectedItem,

        /// <summary>床レベルのZ座標を直接入力。</summary>
        ManualZ,
    }

    /// <summary>
    /// 平面図パノラマツアー生成の設定値。ダイアログとサービスの間で受け渡す。
    /// パノラマ側の解像度・出力先などは <see cref="Panorama"/>（既存の CaptureOptions）に持つ。
    /// </summary>
    public sealed class TourCaptureOptions
    {
        public FloorLevelSource FloorSource { get; set; } = FloorLevelSource.CurrentViewpoint;

        /// <summary>ManualZ のときの床レベルZ（モデル単位）。</summary>
        public double ManualFloorZ { get; set; }

        /// <summary>断面の厚み（床レベルから上方向、モデル単位）。平面図にはこの範囲だけが写る。</summary>
        public double SlabThickness { get; set; } = 3000.0;

        /// <summary>平面図画像の長辺ピクセル数。</summary>
        public int PlanLongSidePixels { get; set; } = 2048;

        /// <summary>モデルXY範囲の外側に足す余白（範囲の長辺に対する比率）。</summary>
        public double PlanMarginRatio { get; set; } = 0.03;

        /// <summary>
        /// パノラマ撮影の設定。目線高さ（EyeHeight）・面解像度・パノラマ幅・出力先ルート・
        /// 中間ファイル保持は本オプションを使用する。PositionSource は使用しない（地点は平面図から決まる）。
        /// </summary>
        public CaptureOptions Panorama { get; set; } = new CaptureOptions();
    }
}
