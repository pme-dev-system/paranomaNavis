using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Navisworks.Api;
using PanoramaNavis.Core;

namespace PanoramaNavis.Addin.Capture
{
    /// <summary>ピッカーで決定した撮影地点（ワールド座標解決済み）。</summary>
    internal sealed class TourPointInput
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double PixelX { get; set; }
        public double PixelY { get; set; }
        public double WorldX { get; set; }
        public double WorldY { get; set; }
    }

    /// <summary>ツアー一括撮影の結果。</summary>
    internal sealed class TourResult
    {
        public string TourFolder { get; set; }
        public string TourJsonPath { get; set; }
        public int RequestedCount { get; set; }
        public int CapturedCount { get; set; }
        public bool Cancelled { get; set; }
    }

    /// <summary>
    /// 平面図パノラマツアーのオーケストレーション:
    /// 床レベル解決 → 平面図撮影（PlanCaptureService）→ 各地点のパノラマ撮影 → tour.json 出力。
    /// </summary>
    internal sealed class TourCaptureService
    {
        private readonly Document _doc;
        private readonly Action<string> _progress;

        public TourCaptureService(Document doc, Action<string> progress)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _progress = progress ?? (_ => { });
        }

        /// <summary>設定に従って床レベル（断面下端）のZ座標を決める。</summary>
        public double ResolveFloorZ(TourCaptureOptions options)
        {
            switch (options.FloorSource)
            {
                case FloorLevelSource.CurrentViewpoint:
                    // 現在のカメラは目線高さに立っているとみなし、目線高さぶん下げて床とする
                    return _doc.CurrentViewpoint.CreateCopy().Position.Z - options.Panorama.EyeHeight;

                case FloorLevelSource.SelectedItem:
                {
                    ModelItem item = _doc.CurrentSelection.SelectedItems.FirstOrDefault();
                    if (item == null)
                        throw new InvalidOperationException(
                            "部材が選択されていません。床レベルの基準にする部材（床スラブなど）を選択してください。");
                    BoundingBox3D box = item.BoundingBox();
                    if (box == null)
                        throw new InvalidOperationException("選択部材の位置を取得できませんでした。");
                    return box.Min.Z;
                }

                case FloorLevelSource.ManualZ:
                    return options.ManualFloorZ;

                default:
                    throw new ArgumentOutOfRangeException(nameof(options.FloorSource));
            }
        }

        /// <summary>平面図を撮影する（結果はピッカーと CapturePanoramas に渡す）。</summary>
        public PlanCaptureResult CapturePlan(TourCaptureOptions options, double floorZ, string tourFolder)
        {
            return new PlanCaptureService(_doc, _progress).Capture(options, floorZ, tourFolder);
        }

        /// <summary>
        /// 各地点のパノラマを撮影し tour.json を出力する。
        /// isCancelled が true を返すと次の地点の開始前に中断する（撮影済み地点は tour.json に残る）。
        /// pointProgress は各地点の開始時に（開始番号, 総数）で呼ばれる。
        /// </summary>
        public TourResult CapturePanoramas(
            string tourFolder, string tourId, TourCaptureOptions options,
            PlanCaptureResult plan, IReadOnlyList<TourPointInput> points,
            Func<bool> isCancelled = null, Action<int, int> pointProgress = null)
        {
            if (points == null || points.Count == 0)
                throw new ArgumentException("撮影地点が指定されていません。");
            if (isCancelled == null)
                isCancelled = () => false;

            var tour = new TourMetadata
            {
                TourId = tourId,
                CreatedAt = DateTimeOffset.Now,
                CreatedBy = Environment.UserName,
                SourceDocument = _doc.FileName ?? "",
                ModelUnits = _doc.Units.ToString(),
                EyeHeight = options.Panorama.EyeHeight,
                Plan = new TourPlanInfo
                {
                    ImageFile = Path.GetFileName(plan.PlanImagePath),
                    Mapping = plan.Mapping,
                    FloorZ = plan.FloorZ,
                    SlabThickness = plan.SlabThickness,
                    SectionClipApplied = plan.SectionClipApplied,
                },
            };

            // パノラマのカメラ高さは全地点共通: 床レベル＋目線高さ
            double cameraZ = plan.FloorZ + options.Panorama.EyeHeight;
            bool cancelled = false;
            int index = 0;

            foreach (TourPointInput point in points)
            {
                if (isCancelled())
                {
                    cancelled = true;
                    break;
                }
                index++;
                pointProgress?.Invoke(index, points.Count);

                string prefix = $"地点 {point.Id}（{index}/{points.Count}）: ";
                var service = new PanoramaCaptureService(_doc, message => _progress(prefix + message));

                var position = new Vec3(point.WorldX, point.WorldY, cameraZ);
                service.RunAt(ClonePanoramaOptions(options.Panorama, tourFolder), position, point.Id);

                tour.Points.Add(new TourPointRecord
                {
                    Id = point.Id,
                    Name = string.IsNullOrWhiteSpace(point.Name) ? point.Id : point.Name,
                    PixelX = point.PixelX,
                    PixelY = point.PixelY,
                    Position = position,
                    // ブラウザから参照するため区切りは "/" 固定
                    PanoramaFile = point.Id + "/panorama.jpg",
                    ThumbnailFile = point.Id + "/thumbnail.jpg",
                });
            }

            _progress("tour.json を出力中...");
            string tourJsonPath = Path.Combine(tourFolder, "tour.json");
            File.WriteAllText(tourJsonPath, tour.ToJson(), new System.Text.UTF8Encoding(false));

            return new TourResult
            {
                TourFolder = tourFolder,
                TourJsonPath = tourJsonPath,
                RequestedCount = points.Count,
                CapturedCount = tour.Points.Count,
                Cancelled = cancelled,
            };
        }

        /// <summary>ツアー用にパノラマ設定を複製する（出力先をツアーフォルダへ、位置指定は地点由来に）。</summary>
        private static CaptureOptions ClonePanoramaOptions(CaptureOptions source, string outputDirectory)
        {
            return new CaptureOptions
            {
                PositionSource = PositionSource.PlanPoint,
                EyeHeight = source.EyeHeight,
                FaceSize = source.FaceSize,
                FaceFovDegrees = source.FaceFovDegrees,
                OutputWidth = source.OutputWidth,
                ThumbnailWidth = source.ThumbnailWidth,
                OutputDirectory = outputDirectory,
                KeepFaceImages = source.KeepFaceImages,
                JpegQuality = source.JpegQuality,
            };
        }
    }
}
