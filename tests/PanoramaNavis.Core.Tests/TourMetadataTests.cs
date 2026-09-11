using System;
using System.Text.Json;
using PanoramaNavis.Core;
using Xunit;

namespace PanoramaNavis.Core.Tests
{
    public class TourMetadataTests
    {
        private static TourMetadata CreateSample()
        {
            var tour = new TourMetadata
            {
                TourId = "Tour_20260730_120000",
                CreatedAt = new DateTimeOffset(2026, 7, 30, 12, 0, 0, TimeSpan.FromHours(9)),
                CreatedBy = "tester",
                SourceDocument = @"C:\models\plant.nwd",
                ModelUnits = "Millimeters",
                EyeHeight = 1600,
                Plan = new TourPlanInfo
                {
                    ImageFile = "plan.png",
                    Mapping = PlanMapping.FitToLongSide(0, 0, 20000, 15000, 2048),
                    FloorZ = 100,
                    SlabThickness = 3000,
                    SectionClipApplied = true,
                },
            };
            tour.Points.Add(new TourPointRecord
            {
                Id = "P01",
                Name = "機械室",
                PixelX = 512.5,
                PixelY = 300.25,
                Position = new Vec3(5000, 12000, 1700),
                PanoramaFile = "P01/panorama.jpg",
                ThumbnailFile = "P01/thumbnail.jpg",
            });
            tour.Points.Add(new TourPointRecord
            {
                Id = "P02",
                Name = "P02",
                PixelX = 1024,
                PixelY = 768,
                Position = new Vec3(10000, 7500, 1700),
                PanoramaFile = "P02/panorama.jpg",
                ThumbnailFile = "P02/thumbnail.jpg",
            });
            return tour;
        }

        [Fact]
        public void ToJson_IsValidJsonAndRoundTripsValues()
        {
            string json = CreateSample().ToJson();

            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            Assert.Equal("panoramaNavis/tour/1", root.GetProperty("schema").GetString());
            Assert.Equal("Tour_20260730_120000", root.GetProperty("tour_id").GetString());
            Assert.Equal(1600.0, root.GetProperty("eye_height").GetDouble());

            JsonElement plan = root.GetProperty("plan");
            Assert.Equal("plan.png", plan.GetProperty("image").GetString());
            Assert.Equal(2048, plan.GetProperty("image_width").GetInt32());
            Assert.Equal(1536, plan.GetProperty("image_height").GetInt32());
            Assert.Equal(0.0, plan.GetProperty("world_min_x").GetDouble());
            Assert.Equal(20000.0, plan.GetProperty("world_max_x").GetDouble());
            Assert.Equal(100.0, plan.GetProperty("floor_z").GetDouble());
            Assert.Equal(3000.0, plan.GetProperty("slab_thickness").GetDouble());
            Assert.Equal(3100.0, plan.GetProperty("cut_top_z").GetDouble());
            Assert.True(plan.GetProperty("section_clip_applied").GetBoolean());
            Assert.Equal("+Y", plan.GetProperty("image_up_axis").GetString());

            JsonElement points = root.GetProperty("points");
            Assert.Equal(JsonValueKind.Array, points.ValueKind);
            Assert.Equal(2, points.GetArrayLength());

            JsonElement p1 = points[0];
            Assert.Equal("P01", p1.GetProperty("id").GetString());
            Assert.Equal("機械室", p1.GetProperty("name").GetString());
            Assert.Equal(512.5, p1.GetProperty("pixel_x").GetDouble());
            Assert.Equal(5000.0, p1.GetProperty("position").GetProperty("x").GetDouble());
            Assert.Equal("P01/panorama.jpg", p1.GetProperty("panorama").GetString());

            Assert.Equal("P02", points[1].GetProperty("id").GetString());
        }

        [Fact]
        public void ToJson_EmptyPointsProducesEmptyArray()
        {
            var tour = CreateSample();
            tour.Points.Clear();

            using JsonDocument doc = JsonDocument.Parse(tour.ToJson());
            Assert.Equal(0, doc.RootElement.GetProperty("points").GetArrayLength());
        }

        [Fact]
        public void ToJson_ThrowsWithoutMapping()
        {
            var tour = new TourMetadata();
            tour.Plan.Mapping = null;
            Assert.Throws<InvalidOperationException>(() => tour.ToJson());
        }

        [Fact]
        public void PanoramaMetadata_ToJson_IsValidJson()
        {
            // 回帰テスト: 数値プロパティ直後のカンマ欠落（JsonLiteのバグ）を実パーサで検出する
            var metadata = new PanoramaMetadata
            {
                PanoramaId = "Panorama_20260724_143000",
                CreatedAt = new DateTimeOffset(2026, 7, 24, 14, 30, 0, TimeSpan.FromHours(9)),
                CreatedBy = "tester",
                SourceDocument = @"C:\models\plant.nwd",
                ModelUnits = "Millimeters",
                Position = new Vec3(12500, 8200, 1600),
                Forward = new Vec3(0, 1, 0),
                Up = new Vec3(0, 0, 1),
                EyeHeight = 1600,
                PositionSource = PositionSource.CurrentViewpoint,
                FaceSize = 2048,
                FaceFovDegrees = 90,
                OutputWidth = 8192,
            };

            using JsonDocument doc = JsonDocument.Parse(metadata.ToJson());
            Assert.Equal(1600.0, doc.RootElement.GetProperty("eye_height").GetDouble());
            Assert.Equal(8192, doc.RootElement.GetProperty("output_width").GetInt32());
            Assert.Equal("equirectangular", doc.RootElement.GetProperty("projection").GetString());
        }

        [Fact]
        public void JsonLite_NestedObjectsAndArrays()
        {
            var json = new JsonLite();
            json.BeginObject();
            json.Property("count", 2);
            json.BeginArray("items");
            json.BeginObject();
            json.Property("value", 1.5);
            json.EndObject();
            json.BeginObject();
            json.Property("flag", false);
            json.EndObject();
            json.EndArray();
            json.BeginObject("nested");
            json.Property("name", "abc");
            json.EndObject();
            json.EndObject();

            using JsonDocument doc = JsonDocument.Parse(json.ToString());
            JsonElement root = doc.RootElement;
            Assert.Equal(2, root.GetProperty("count").GetInt32());
            Assert.Equal(1.5, root.GetProperty("items")[0].GetProperty("value").GetDouble());
            Assert.False(root.GetProperty("items")[1].GetProperty("flag").GetBoolean());
            Assert.Equal("abc", root.GetProperty("nested").GetProperty("name").GetString());
        }
    }
}
