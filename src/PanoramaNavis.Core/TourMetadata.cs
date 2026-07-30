using System;
using System.Collections.Generic;

namespace PanoramaNavis.Core
{
    /// <summary>平面図の情報（tour.json の "plan" セクション）。</summary>
    public sealed class TourPlanInfo
    {
        /// <summary>ツアーフォルダからの相対パス（例: "plan.png"）。</summary>
        public string ImageFile { get; set; } = "plan.png";

        /// <summary>画像⇔ワールドの対応。</summary>
        public PlanMapping Mapping { get; set; }

        /// <summary>断面下端（床レベル）のZ座標（モデル単位）。</summary>
        public double FloorZ { get; set; }

        /// <summary>断面の厚み（床レベルから上方向）。</summary>
        public double SlabThickness { get; set; }

        /// <summary>断面上端のZ座標。</summary>
        public double CutTopZ => FloorZ + SlabThickness;

        /// <summary>厚みのクリップ平面を適用できたか（COM APIが使えない環境では false）。</summary>
        public bool SectionClipApplied { get; set; }
    }

    /// <summary>ツアーの撮影地点1件。</summary>
    public sealed class TourPointRecord
    {
        /// <summary>フォルダ名にも使う識別子（例: "P01"）。</summary>
        public string Id { get; set; } = "";

        /// <summary>表示名（ユーザーが変更可能。既定はIdと同じ）。</summary>
        public string Name { get; set; } = "";

        /// <summary>平面図画像上のクリック位置（左上原点・連続ピクセル座標）。</summary>
        public double PixelX { get; set; }
        public double PixelY { get; set; }

        /// <summary>パノラマのカメラ位置（ワールド座標。Z＝床レベル＋目線高さ）。</summary>
        public Vec3 Position { get; set; }

        /// <summary>ツアーフォルダからの相対パス（"/"区切り。例: "P01/panorama.jpg"）。</summary>
        public string PanoramaFile { get; set; } = "";
        public string ThumbnailFile { get; set; } = "";
    }

    /// <summary>
    /// 平面図パノラマツアーのメタデータ。tour.json として出力される。
    /// スキーマ仕様は docs/ARCHITECTURE.md「tour.json 仕様」を参照。
    /// </summary>
    public sealed class TourMetadata
    {
        public const string SchemaId = "panoramaNavis/tour/1";

        public string TourId { get; set; } = "";
        public DateTimeOffset CreatedAt { get; set; }
        public string CreatedBy { get; set; } = "";
        public string SourceDocument { get; set; } = "";
        public string ModelUnits { get; set; } = "";

        /// <summary>床レベルからカメラ位置までの目線高さ（モデル単位）。</summary>
        public double EyeHeight { get; set; }

        public TourPlanInfo Plan { get; set; } = new TourPlanInfo();
        public List<TourPointRecord> Points { get; } = new List<TourPointRecord>();

        public string ToJson()
        {
            if (Plan == null || Plan.Mapping == null)
                throw new InvalidOperationException("Plan.Mapping が設定されていません。");

            var json = new JsonLite();
            json.BeginObject();
            json.Property("schema", SchemaId);
            json.Property("tour_id", TourId);
            json.Property("created_at", CreatedAt.ToString("yyyy-MM-dd'T'HH:mm:sszzz"));
            json.Property("created_by", CreatedBy);
            json.Property("source_document", SourceDocument);
            json.Property("model_units", ModelUnits);
            json.Property("eye_height", EyeHeight);

            json.BeginObject("plan");
            json.Property("image", Plan.ImageFile);
            json.Property("image_width", Plan.Mapping.ImageWidth);
            json.Property("image_height", Plan.Mapping.ImageHeight);
            json.Property("world_min_x", Plan.Mapping.WorldMinX);
            json.Property("world_min_y", Plan.Mapping.WorldMinY);
            json.Property("world_max_x", Plan.Mapping.WorldMaxX);
            json.Property("world_max_y", Plan.Mapping.WorldMaxY);
            // 平面図の向きの規約（ビューア・後段処理が座標変換に使う）
            json.Property("view_direction", "-Z");
            json.Property("image_up_axis", "+Y");
            json.Property("image_right_axis", "+X");
            json.Property("floor_z", Plan.FloorZ);
            json.Property("slab_thickness", Plan.SlabThickness);
            json.Property("cut_top_z", Plan.CutTopZ);
            json.Property("section_clip_applied", Plan.SectionClipApplied);
            json.EndObject();

            json.BeginArray("points");
            foreach (TourPointRecord p in Points)
            {
                json.BeginObject();
                json.Property("id", p.Id);
                json.Property("name", p.Name);
                json.Property("pixel_x", p.PixelX);
                json.Property("pixel_y", p.PixelY);
                json.PropertyVec3("position", p.Position);
                json.Property("panorama", p.PanoramaFile);
                json.Property("thumbnail", p.ThumbnailFile);
                json.EndObject();
            }
            json.EndArray();
            json.EndObject();
            return json.ToString();
        }
    }
}
