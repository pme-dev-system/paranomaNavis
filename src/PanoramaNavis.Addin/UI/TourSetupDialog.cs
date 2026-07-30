using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using Autodesk.Navisworks.Api;
using PanoramaNavis.Addin.Capture;
using PanoramaNavis.Core;

namespace PanoramaNavis.Addin.UI
{
    /// <summary>
    /// 平面図パノラマツアーの設定ダイアログ（コードのみのWinForms、デザイナ非依存）。
    /// 「平面図を撮影して地点選択へ」で 平面図撮影 → 地点ピッカー → 一括撮影 の流れを進める。
    /// </summary>
    internal sealed class TourSetupDialog : Form
    {
        private readonly Document _doc;

        private RadioButton _floorFromViewpoint;
        private RadioButton _floorFromSelection;
        private RadioButton _floorFromManual;
        private TextBox _floorZ;
        private TextBox _thickness;
        private ComboBox _planResolution;
        private TextBox _eyeHeight;
        private ComboBox _faceSize;
        private ComboBox _outputWidth;
        private CheckBox _keepFaces;
        private TextBox _outputDir;
        private Button _start;
        private Button _close;
        private Label _status;

        public TourSetupDialog(Document doc)
        {
            _doc = doc;
            BuildLayout();
            LoadDefaults();
        }

        private void BuildLayout()
        {
            Text = "平面図パノラマツアー — PanoramaNavis";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new System.Drawing.Size(508, 540);

            var sectionGroup = new GroupBox
            {
                Text = "断面（平面図の撮影範囲）",
                Location = new System.Drawing.Point(12, 12),
                Size = new System.Drawing.Size(484, 190),
            };

            _floorFromViewpoint = new RadioButton
            {
                Text = "現在の視点の高さから（視点Z − 目線高さ ＝ 床レベル）",
                Location = new System.Drawing.Point(16, 24),
                AutoSize = true,
                Checked = true,
            };
            _floorFromSelection = new RadioButton
            {
                Text = "選択中の部材の底面を床レベルにする（床スラブ等を選択）",
                Location = new System.Drawing.Point(16, 50),
                AutoSize = true,
            };
            _floorFromManual = new RadioButton
            {
                Text = "床レベルZを入力:",
                Location = new System.Drawing.Point(16, 76),
                AutoSize = true,
            };
            _floorZ = new TextBox
            {
                Location = new System.Drawing.Point(160, 74),
                Size = new System.Drawing.Size(110, 23),
            };

            var thicknessLabel = new Label
            {
                Text = "断面の厚み（床から上へ）:",
                Location = new System.Drawing.Point(16, 112),
                AutoSize = true,
            };
            _thickness = new TextBox
            {
                Location = new System.Drawing.Point(180, 108),
                Size = new System.Drawing.Size(90, 23),
                Text = "3000",
            };
            var unitsLabel = new Label
            {
                Text = "モデル単位: " + _doc.Units,
                Location = new System.Drawing.Point(286, 112),
                AutoSize = true,
            };

            var planResLabel = new Label
            {
                Text = "平面図の解像度（長辺px）:",
                Location = new System.Drawing.Point(16, 148),
                AutoSize = true,
            };
            _planResolution = new ComboBox
            {
                Location = new System.Drawing.Point(180, 144),
                Size = new System.Drawing.Size(90, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            _planResolution.Items.AddRange(new object[] { "1024", "2048", "4096" });
            _planResolution.SelectedIndex = 1;

            sectionGroup.Controls.AddRange(new Control[]
            {
                _floorFromViewpoint, _floorFromSelection, _floorFromManual, _floorZ,
                thicknessLabel, _thickness, unitsLabel, planResLabel, _planResolution,
            });

            var panoramaGroup = new GroupBox
            {
                Text = "パノラマ撮影",
                Location = new System.Drawing.Point(12, 210),
                Size = new System.Drawing.Size(484, 130),
            };

            var eyeHeightLabel = new Label
            {
                Text = "目線高さ（床から）:",
                Location = new System.Drawing.Point(16, 28),
                AutoSize = true,
            };
            _eyeHeight = new TextBox
            {
                Location = new System.Drawing.Point(140, 24),
                Size = new System.Drawing.Size(90, 23),
                Text = "1600",
            };

            var faceSizeLabel = new Label
            {
                Text = "面解像度(px):",
                Location = new System.Drawing.Point(16, 64),
                AutoSize = true,
            };
            _faceSize = new ComboBox
            {
                Location = new System.Drawing.Point(140, 60),
                Size = new System.Drawing.Size(90, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            _faceSize.Items.AddRange(new object[] { "1024", "2048", "4096" });
            _faceSize.SelectedIndex = 1;

            var outputWidthLabel = new Label
            {
                Text = "パノラマ幅(px):",
                Location = new System.Drawing.Point(250, 64),
                AutoSize = true,
            };
            _outputWidth = new ComboBox
            {
                Location = new System.Drawing.Point(360, 60),
                Size = new System.Drawing.Size(90, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            _outputWidth.Items.AddRange(new object[] { "4096", "8192", "16384" });
            _outputWidth.SelectedIndex = 1;

            _keepFaces = new CheckBox
            {
                Text = "中間ファイル（6面PNG）を残す",
                Location = new System.Drawing.Point(16, 96),
                AutoSize = true,
                Checked = false,
            };

            panoramaGroup.Controls.AddRange(new Control[]
            {
                eyeHeightLabel, _eyeHeight, faceSizeLabel, _faceSize,
                outputWidthLabel, _outputWidth, _keepFaces,
            });

            var outputGroup = new GroupBox
            {
                Text = "出力",
                Location = new System.Drawing.Point(12, 348),
                Size = new System.Drawing.Size(484, 64),
            };
            var outputDirLabel = new Label
            {
                Text = "出力先:",
                Location = new System.Drawing.Point(16, 28),
                AutoSize = true,
            };
            _outputDir = new TextBox
            {
                Location = new System.Drawing.Point(80, 24),
                Size = new System.Drawing.Size(350, 23),
            };
            var browseButton = new Button
            {
                Text = "...",
                Location = new System.Drawing.Point(436, 23),
                Size = new System.Drawing.Size(32, 25),
            };
            browseButton.Click += OnBrowseOutputDir;
            outputGroup.Controls.AddRange(new Control[] { outputDirLabel, _outputDir, browseButton });

            _status = new Label
            {
                Text = "断面と出力先を指定し、「平面図を撮影して地点選択へ」を押してください。\n" +
                       "平面図は真上から北（+Y）を上にして撮影されます。",
                Location = new System.Drawing.Point(12, 424),
                Size = new System.Drawing.Size(484, 60),
            };

            _start = new Button
            {
                Text = "平面図を撮影して地点選択へ",
                Location = new System.Drawing.Point(188, 498),
                Size = new System.Drawing.Size(206, 30),
            };
            _start.Click += OnStart;

            _close = new Button
            {
                Text = "閉じる",
                Location = new System.Drawing.Point(402, 498),
                Size = new System.Drawing.Size(94, 30),
                DialogResult = DialogResult.Cancel,
            };

            Controls.AddRange(new Control[]
            {
                sectionGroup, panoramaGroup, outputGroup, _status, _start, _close,
            });
            AcceptButton = _start;
            CancelButton = _close;
        }

        private void LoadDefaults()
        {
            _outputDir.Text = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "PanoramaNavis");

            // 床レベル入力の初期値として現在のカメラ高さ−目線高さを入れておく
            Point3D pos = _doc.CurrentViewpoint.CreateCopy().Position;
            _floorZ.Text = (pos.Z - 1600.0).ToString("0.###", CultureInfo.InvariantCulture);
        }

        private void OnBrowseOutputDir(object sender, EventArgs e)
        {
            using (var browser = new FolderBrowserDialog())
            {
                browser.Description = "ツアーの出力先フォルダを選択してください";
                if (Directory.Exists(_outputDir.Text))
                    browser.SelectedPath = _outputDir.Text;
                if (browser.ShowDialog(this) == DialogResult.OK)
                    _outputDir.Text = browser.SelectedPath;
            }
        }

        private void OnStart(object sender, EventArgs e)
        {
            TourCaptureOptions options;
            try
            {
                options = BuildOptions();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "入力エラー",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _start.Enabled = false;
            _close.Enabled = false;
            string tourFolder = null;
            try
            {
                var service = new TourCaptureService(_doc, ReportStatus);

                double floorZ = service.ResolveFloorZ(options);
                string tourId = "Tour_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                tourFolder = Path.Combine(options.Panorama.OutputDirectory, tourId);

                // ① 平面図撮影
                PlanCaptureResult plan = service.CapturePlan(options, floorZ, tourFolder);

                if (!plan.SectionClipApplied)
                {
                    DialogResult keep = MessageBox.Show(this,
                        "断面クリップを設定できませんでした（詳細: diagnostics.log）。\n" +
                        "平面図には屋根や上階が写っている可能性があります。\n\n" +
                        "このまま地点選択へ進みますか？\n" +
                        "（「いいえ」の場合は中止します。Navisworksのセクショニングで断面を\n" +
                        "手動設定してから再実行する方法もあります）",
                        "PanoramaNavis", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (keep != DialogResult.Yes)
                    {
                        TryDeleteFolder(tourFolder);
                        ReportStatus("中止しました。");
                        return;
                    }
                }

                // ② 平面図上で地点指定
                using (var picker = new PlanPointPickerDialog(
                    plan.PlanImagePath, plan.Mapping, _doc.Units.ToString(),
                    plan.FloorZ, plan.SlabThickness))
                {
                    if (picker.ShowDialog(this) != DialogResult.OK || picker.Points.Count == 0)
                    {
                        TryDeleteFolder(tourFolder);
                        ReportStatus("地点選択をキャンセルしました。");
                        return;
                    }

                    // ③ 各地点のパノラマ一括撮影
                    var points = new System.Collections.Generic.List<TourPointInput>();
                    foreach (PickedPoint p in picker.Points)
                    {
                        var world = plan.Mapping.PixelToWorld(p.PixelX, p.PixelY);
                        points.Add(new TourPointInput
                        {
                            Id = p.Id,
                            Name = p.Name,
                            PixelX = p.PixelX,
                            PixelY = p.PixelY,
                            WorldX = world.X,
                            WorldY = world.Y,
                        });
                    }

                    TourResult result;
                    using (var progress = new ProgressForm("パノラマ一括撮影 — PanoramaNavis", points.Count))
                    {
                        progress.Show(this);
                        try
                        {
                            var progressService = new TourCaptureService(_doc, message =>
                            {
                                progress.ReportMessage(message);
                                ReportStatus(message);
                            });
                            result = progressService.CapturePanoramas(
                                tourFolder, tourId, options, plan, points,
                                isCancelled: () => progress.CancelRequested,
                                pointProgress: (done, total) => progress.ReportStep(done - 1));
                        }
                        finally
                        {
                            progress.Finish();
                            progress.Close();
                        }
                    }

                    string summary = result.Cancelled
                        ? $"中断しました（{result.CapturedCount}/{result.RequestedCount}地点を撮影済み）。"
                        : $"完了: {result.CapturedCount}地点のパノラマを生成しました。";
                    ReportStatus(summary + "\n" + result.TourFolder);

                    DialogResult open = MessageBox.Show(this,
                        summary + "\n\n" + result.TourFolder +
                        "\n（plan.png / tour.json / 各地点フォルダ）" +
                        "\n\nviewer/tour-viewer.html にこのフォルダをドラッグすると閲覧できます。" +
                        "\n\n出力フォルダを開きますか？",
                        "PanoramaNavis", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    if (open == DialogResult.Yes)
                        Process.Start("explorer.exe", "\"" + result.TourFolder + "\"");
                }
            }
            catch (Exception ex)
            {
                ReportStatus("失敗: " + ex.Message);
                MessageBox.Show(this, "ツアー生成に失敗しました:\n" + ex.Message,
                    "PanoramaNavis", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _start.Enabled = true;
                _close.Enabled = true;
            }
        }

        private TourCaptureOptions BuildOptions()
        {
            var options = new TourCaptureOptions
            {
                SlabThickness = ParseDouble(_thickness.Text, "断面の厚み"),
                PlanLongSidePixels = int.Parse(
                    (string)_planResolution.SelectedItem, CultureInfo.InvariantCulture),
                Panorama = new CaptureOptions
                {
                    EyeHeight = ParseDouble(_eyeHeight.Text, "目線高さ"),
                    FaceSize = int.Parse((string)_faceSize.SelectedItem, CultureInfo.InvariantCulture),
                    OutputWidth = int.Parse((string)_outputWidth.SelectedItem, CultureInfo.InvariantCulture),
                    OutputDirectory = _outputDir.Text.Trim(),
                    KeepFaceImages = _keepFaces.Checked,
                },
            };

            if (options.SlabThickness <= 0)
                throw new ArgumentException("断面の厚みは正の値を指定してください。");
            if (string.IsNullOrWhiteSpace(options.Panorama.OutputDirectory))
                throw new ArgumentException("出力先フォルダを指定してください。");

            if (_floorFromViewpoint.Checked)
                options.FloorSource = FloorLevelSource.CurrentViewpoint;
            else if (_floorFromSelection.Checked)
                options.FloorSource = FloorLevelSource.SelectedItem;
            else
            {
                options.FloorSource = FloorLevelSource.ManualZ;
                options.ManualFloorZ = ParseDouble(_floorZ.Text, "床レベルZ");
            }

            return options;
        }

        private void ReportStatus(string message)
        {
            _status.Text = message;
            _status.Refresh();
        }

        private static double ParseDouble(string text, string fieldName)
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                throw new ArgumentException($"{fieldName} の値が数値として読み取れません: 「{text}」");
            return value;
        }

        private static void TryDeleteFolder(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
            }
            catch
            {
                // キャンセル時の後片付け失敗は無視する
            }
        }
    }
}
