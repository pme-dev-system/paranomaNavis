using System;
using System.IO;
using Autodesk.Navisworks.Api;
using PanoramaNavis.Core;

namespace PanoramaNavis.Addin.Capture
{
    /// <summary>平面図撮影の結果。</summary>
    internal sealed class PlanCaptureResult
    {
        public string PlanImagePath { get; set; }
        public PlanMapping Mapping { get; set; }
        public double FloorZ { get; set; }
        public double SlabThickness { get; set; }
        public bool SectionClipApplied { get; set; }
    }

    /// <summary>
    /// 真上（-Z視線）からの正射投影で平面図を撮影する。
    /// 撮影方向は北上固定: 画像上=+Y、画像右=+X（PlanMapping と同一規約。
    /// パノラマの Down 面と同じ向きなので、平面図とパノラマの方位が一致する）。
    /// 断面（床Z〜床Z＋厚み）は SectionClipper の一時クリップで表現し、撮影後に必ず戻す。
    /// </summary>
    internal sealed class PlanCaptureService
    {
        private readonly Document _doc;
        private readonly Action<string> _progress;

        public PlanCaptureService(Document doc, Action<string> progress)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _progress = progress ?? (_ => { });
        }

        public PlanCaptureResult Capture(TourCaptureOptions options, double floorZ, string outputFolder)
        {
            Directory.CreateDirectory(outputFolder);
            string diagnosticsPath = Path.Combine(outputFolder, "diagnostics.log");

            // モデル全体のXY範囲＋余白 ＝ 平面図がカバーする範囲
            GetModelBounds(out Vec3 boundsMin, out Vec3 boundsMax);
            double margin = Math.Max(boundsMax.X - boundsMin.X, boundsMax.Y - boundsMin.Y)
                * options.PlanMarginRatio;
            PlanMapping mapping = PlanMapping.FitToLongSide(
                boundsMin.X - margin, boundsMin.Y - margin,
                boundsMax.X + margin, boundsMax.Y + margin,
                options.PlanLongSidePixels);

            double cutTopZ = floorZ + options.SlabThickness;
            var exporter = new ViewExporter(diagnosticsPath);

            // 撮影中にカメラと断面を変えるため、必ず元の状態へ復帰できるようにする
            Viewpoint originalViewpoint = _doc.CurrentViewpoint.CreateCopy();
            using (var clipper = new SectionClipper(diagnosticsPath))
            {
                try
                {
                    _progress("断面クリップを設定中...");
                    bool clipApplied = clipper.TryApplyHorizontalSlab(floorZ, cutTopZ);

                    _progress("平面図を撮影中...");
                    Viewpoint planViewpoint = CreatePlanViewpoint(
                        originalViewpoint, mapping, boundsMax.Z, cutTopZ);
                    _doc.CurrentViewpoint.CopyFrom(planViewpoint);
                    System.Windows.Forms.Application.DoEvents();

                    string planPath = Path.Combine(outputFolder, "plan.png");
                    exporter.ExportPng(planPath, mapping.ImageWidth, mapping.ImageHeight);

                    return new PlanCaptureResult
                    {
                        PlanImagePath = planPath,
                        Mapping = mapping,
                        FloorZ = floorZ,
                        SlabThickness = options.SlabThickness,
                        SectionClipApplied = clipApplied,
                    };
                }
                finally
                {
                    _doc.CurrentViewpoint.CopyFrom(originalViewpoint);
                }
            }
        }

        /// <summary>読み込まれている全モデルのバウンディングボックスの合併を返す。</summary>
        private void GetModelBounds(out Vec3 min, out Vec3 max)
        {
            double minX = double.PositiveInfinity, minY = double.PositiveInfinity, minZ = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity, maxZ = double.NegativeInfinity;
            bool found = false;

            foreach (Model model in _doc.Models)
            {
                ModelItem root = model.RootItem;
                BoundingBox3D box = root?.BoundingBox();
                if (box == null)
                    continue;
                found = true;
                minX = Math.Min(minX, box.Min.X);
                minY = Math.Min(minY, box.Min.Y);
                minZ = Math.Min(minZ, box.Min.Z);
                maxX = Math.Max(maxX, box.Max.X);
                maxY = Math.Max(maxY, box.Max.Y);
                maxZ = Math.Max(maxZ, box.Max.Z);
            }

            if (!found || !(maxX > minX) || !(maxY > minY))
                throw new InvalidOperationException(
                    "モデルのXY範囲を取得できませんでした。モデルが読み込まれているか確認してください。");

            min = new Vec3(minX, minY, minZ);
            max = new Vec3(maxX, maxY, maxZ);
        }

        private static Viewpoint CreatePlanViewpoint(
            Viewpoint baseViewpoint, PlanMapping mapping, double modelTopZ, double cutTopZ)
        {
            Viewpoint vp = baseViewpoint.CreateCopy();
            vp.Projection = ViewpointProjection.Orthographic;

            double centerX = (mapping.WorldMinX + mapping.WorldMaxX) / 2.0;
            double centerY = (mapping.WorldMinY + mapping.WorldMaxY) / 2.0;

            // 正射投影では高さが画角に影響しないため、モデル最上部・断面上端より確実に上へ置く
            double clearance = Math.Max(
                mapping.WorldMaxX - mapping.WorldMinX,
                mapping.WorldMaxY - mapping.WorldMinY) * 0.05;
            double cameraZ = Math.Max(modelTopZ, cutTopZ) + Math.Max(clearance, 1.0);

            vp.Position = new Point3D(centerX, centerY, cameraZ);

            // 真下視線・画像上=+Y。先に視線、その後Upでロールを確定（NavisCameraと同じ順序）
            vp.AlignDirection(new Vector3D(0, 0, -1));
            vp.AlignUp(new Vector3D(0, 1, 0));

            // 正射投影の HeightField はビューの縦の実寸（モデル単位）。要実機検証（ARCHITECTURE.md）
            vp.HeightField = mapping.WorldMaxY - mapping.WorldMinY;
            NavisCamera.TrySetAspectRatio(
                vp, (mapping.WorldMaxX - mapping.WorldMinX) / (mapping.WorldMaxY - mapping.WorldMinY));
            return vp;
        }
    }
}
