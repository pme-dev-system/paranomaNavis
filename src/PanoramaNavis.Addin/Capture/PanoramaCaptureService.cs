using System;
using System.IO;
using System.Linq;
using Autodesk.Navisworks.Api;
using PanoramaNavis.Addin.Imaging;
using PanoramaNavis.Core;

namespace PanoramaNavis.Addin.Capture
{
    /// <summary>パノラマ生成の結果。</summary>
    internal sealed class CaptureResult
    {
        public string OutputFolder { get; set; }
        public string PanoramaPath { get; set; }
    }

    /// <summary>
    /// 6面キャプチャ→正距円筒合成→メタデータ出力のオーケストレーション。
    /// 処理の流れは docs/ARCHITECTURE.md「撮影シーケンス」を参照。
    /// </summary>
    internal sealed class PanoramaCaptureService
    {
        private readonly Document _doc;
        private readonly Action<string> _progress;

        public PanoramaCaptureService(Document doc, Action<string> progress)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _progress = progress ?? (_ => { });
        }

        public CaptureResult Run(CaptureOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.OutputDirectory))
                throw new ArgumentException("出力先フォルダが指定されていません。");

            string panoramaId = "Panorama_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string outputFolder = Path.Combine(options.OutputDirectory, panoramaId);
            string facesFolder = Path.Combine(outputFolder, "faces");
            Directory.CreateDirectory(facesFolder);

            var exporter = new ViewExporter(Path.Combine(outputFolder, "diagnostics.log"));

            // 撮影中にカメラを動かすため、必ず元のビューポイントへ復帰できるよう退避する
            Viewpoint originalViewpoint = _doc.CurrentViewpoint.CreateCopy();
            try
            {
                Vec3 position = ResolvePosition(options, originalViewpoint);

                // PoCでは基準方位をモデルの+Y（プロジェクト北）に固定する。
                // パノラマは全周を含むため機能上の欠落はなく、方位は metadata に記録される。
                var frame = new NavisCamera.PanoFrame(Vec3.UnitY, Vec3.UnitZ);
                double fovRadians = options.FaceFovDegrees * Math.PI / 180.0;

                var cube = new CubeMapSet();
                foreach (FaceBasis face in CubeOrientation.Faces)
                {
                    _progress($"面 {face.Face} を撮影中... ({(int)face.Face + 1}/6)");

                    Viewpoint faceVp = NavisCamera.CreateFaceViewpoint(
                        originalViewpoint, position, frame, face, fovRadians);
                    _doc.CurrentViewpoint.CopyFrom(faceVp);

                    // ビュー更新を描画キューに反映させてからキャプチャする
                    System.Windows.Forms.Application.DoEvents();

                    string facePath = Path.Combine(
                        facesFolder, $"face_{face.Face.ToString().ToLowerInvariant()}.png");
                    exporter.ExportPng(facePath, options.FaceSize, options.FaceSize);

                    cube.SetFace(face.Face, GdiPixelBufferIO.LoadImage(facePath));
                }

                _progress("正距円筒図法へ変換中...");
                PixelBuffer panorama = EquirectangularConverter.Convert(
                    cube, options.OutputWidth, options.FaceFovDegrees);

                _progress("ファイルを出力中...");
                string panoramaPath = Path.Combine(outputFolder, "panorama.jpg");
                GdiPixelBufferIO.SaveJpeg(panorama, panoramaPath, options.JpegQuality);

                PixelBuffer thumbnail = EquirectangularConverter.Downscale(panorama, options.ThumbnailWidth);
                GdiPixelBufferIO.SaveJpeg(thumbnail, Path.Combine(outputFolder, "thumbnail.jpg"), 85);

                WriteMetadata(outputFolder, panoramaId, options, position, frame);

                if (!options.KeepFaceImages)
                    TryDeleteFolder(facesFolder);

                return new CaptureResult
                {
                    OutputFolder = outputFolder,
                    PanoramaPath = panoramaPath,
                };
            }
            finally
            {
                _doc.CurrentViewpoint.CopyFrom(originalViewpoint);
            }
        }

        private Vec3 ResolvePosition(CaptureOptions options, Viewpoint currentViewpoint)
        {
            switch (options.PositionSource)
            {
                case PositionSource.CurrentViewpoint:
                    return NavisCamera.ToVec3(currentViewpoint.Position);

                case PositionSource.SelectedItem:
                {
                    ModelItem item = _doc.CurrentSelection.SelectedItems.FirstOrDefault();
                    if (item == null)
                        throw new InvalidOperationException(
                            "部材が選択されていません。撮影位置の基準にする部材を選択してください。");
                    BoundingBox3D box = item.BoundingBox();
                    if (box == null)
                        throw new InvalidOperationException("選択部材の位置を取得できませんでした。");
                    // 部材のXY中心・底面レベル＋目線高さをカメラ位置とする
                    return new Vec3(
                        (box.Min.X + box.Max.X) / 2.0,
                        (box.Min.Y + box.Max.Y) / 2.0,
                        box.Min.Z + options.EyeHeight);
                }

                case PositionSource.ManualCoordinates:
                    return options.ManualPosition;

                default:
                    throw new ArgumentOutOfRangeException(nameof(options.PositionSource));
            }
        }

        private void WriteMetadata(
            string outputFolder, string panoramaId, CaptureOptions options,
            Vec3 position, NavisCamera.PanoFrame frame)
        {
            var metadata = new PanoramaMetadata
            {
                PanoramaId = panoramaId,
                CreatedAt = DateTimeOffset.Now,
                CreatedBy = Environment.UserName,
                SourceDocument = _doc.FileName ?? "",
                ModelUnits = _doc.Units.ToString(),
                Position = position,
                Forward = frame.Forward,
                Up = frame.Up,
                EyeHeight = options.EyeHeight,
                PositionSource = options.PositionSource,
                FaceSize = options.FaceSize,
                FaceFovDegrees = options.FaceFovDegrees,
                OutputWidth = options.OutputWidth,
            };
            File.WriteAllText(
                Path.Combine(outputFolder, "metadata.json"),
                metadata.ToJson(),
                new System.Text.UTF8Encoding(false));
        }

        private static void TryDeleteFolder(string path)
        {
            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch
            {
                // 中間ファイルの削除失敗は無視する（成果物には影響しない）
            }
        }
    }
}
