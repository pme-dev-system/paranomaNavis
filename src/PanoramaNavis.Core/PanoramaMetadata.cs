using System;

namespace PanoramaNavis.Core
{
    /// <summary>
    /// 生成したパノラマ1件のメタデータ。metadata.json として出力される。
    /// スキーマ仕様は docs/ARCHITECTURE.md「metadata.json 仕様」を参照。
    /// </summary>
    public sealed class PanoramaMetadata
    {
        public const string SchemaId = "panoramaNavis/1";

        public string PanoramaId { get; set; } = "";
        public DateTimeOffset CreatedAt { get; set; }
        public string CreatedBy { get; set; } = "";
        public string SourceDocument { get; set; } = "";
        public string ModelUnits { get; set; } = "";
        public Vec3 Position { get; set; }
        public Vec3 Forward { get; set; }
        public Vec3 Up { get; set; }
        public double EyeHeight { get; set; }
        public PositionSource PositionSource { get; set; }
        public int FaceSize { get; set; }
        public double FaceFovDegrees { get; set; }
        public int OutputWidth { get; set; }

        public string ToJson()
        {
            var json = new JsonLite();
            json.BeginObject();
            json.Property("schema", SchemaId);
            json.Property("panorama_id", PanoramaId);
            json.Property("created_at", CreatedAt.ToString("yyyy-MM-dd'T'HH:mm:sszzz"));
            json.Property("created_by", CreatedBy);
            json.Property("source_document", SourceDocument);
            json.Property("model_units", ModelUnits);
            json.PropertyVec3("position", Position);
            json.PropertyVec3("forward", Forward);
            json.PropertyVec3("up", Up);
            json.Property("eye_height", EyeHeight);
            json.Property("position_source", PositionSource.ToString());
            json.Property("face_size", FaceSize);
            json.Property("face_fov_degrees", FaceFovDegrees);
            json.Property("output_width", OutputWidth);
            json.Property("projection", "equirectangular");
            json.EndObject();
            return json.ToString();
        }
    }
}
