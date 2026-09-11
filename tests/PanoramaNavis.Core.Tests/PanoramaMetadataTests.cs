using System;
using PanoramaNavis.Core;
using Xunit;

namespace PanoramaNavis.Core.Tests
{
    public class PanoramaMetadataTests
    {
        private static PanoramaMetadata CreateSample() => new PanoramaMetadata
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

        [Fact]
        public void ToJson_ContainsExpectedFields()
        {
            string json = CreateSample().ToJson();

            Assert.Contains("\"schema\": \"panoramaNavis/1\"", json);
            Assert.Contains("\"panorama_id\": \"Panorama_20260724_143000\"", json);
            Assert.Contains("\"created_at\": \"2026-07-24T14:30:00+09:00\"", json);
            Assert.Contains("\"position\": { \"x\": 12500.0, \"y\": 8200.0, \"z\": 1600.0 }", json);
            Assert.Contains("\"position_source\": \"CurrentViewpoint\"", json);
            Assert.Contains("\"face_fov_degrees\": 90.0", json);
            Assert.Contains("\"projection\": \"equirectangular\"", json);
        }

        [Fact]
        public void ToJson_EscapesBackslashesInPaths()
        {
            string json = CreateSample().ToJson();
            Assert.Contains("\"source_document\": \"C:\\\\models\\\\plant.nwd\"", json);
        }

        [Fact]
        public void ToJson_IsWellFormedEnoughToParse()
        {
            // 依存を増やさないため簡易チェック: 括弧の対応と末尾カンマなし
            string json = CreateSample().ToJson();

            int depth = 0;
            bool inString = false;
            char prev = '\0';
            foreach (char c in json)
            {
                if (c == '"' && prev != '\\')
                    inString = !inString;
                if (!inString)
                {
                    if (c == '{') depth++;
                    if (c == '}') depth--;
                }
                prev = c;
            }
            Assert.False(inString, "文字列リテラルが閉じていません。");
            Assert.Equal(0, depth);
            Assert.DoesNotContain(",\n}", json);
        }

        [Fact]
        public void JsonLite_EscapesControlCharacters()
        {
            var json = new JsonLite();
            json.BeginObject();
            json.Property("text", "line1\nline2\t\"quoted\"");
            json.EndObject();

            string result = json.ToString();
            Assert.Contains("line1\\nline2\\t\\\"quoted\\\"", result);
        }
    }
}
