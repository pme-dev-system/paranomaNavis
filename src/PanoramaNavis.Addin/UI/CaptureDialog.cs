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
    /// <summary>パノラマ生成の設定ダイアログ（コードのみのWinForms、デザイナ非依存）。</summary>
    internal sealed class CaptureDialog : Form
    {
        private readonly Document _doc;

        private RadioButton _fromViewpoint;
        private RadioButton _fromSelection;
        private RadioButton _fromCoordinates;
        private TextBox _eyeHeight;
        private TextBox _coordX;
        private TextBox _coordY;
        private TextBox _coordZ;
        private ComboBox _faceSize;
        private ComboBox _outputWidth;
        private TextBox _outputDir;
        private CheckBox _keepFaces;
        private Button _generate;
        private Button _close;
        private Label _status;

        public CaptureDialog(Document doc)
        {
            _doc = doc;
            BuildLayout();
            LoadDefaults();
        }

        private void BuildLayout()
        {
            Text = "360°パノラマ作成 — PanoramaNavis";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new System.Drawing.Size(478, 436);

            var positionGroup = new GroupBox
            {
                Text = "撮影位置",
                Location = new System.Drawing.Point(12, 12),
                Size = new System.Drawing.Size(454, 168),
            };

            _fromViewpoint = new RadioButton
            {
                Text = "現在のビューポイント",
                Location = new System.Drawing.Point(16, 24),
                AutoSize = true,
                Checked = true,
            };
            _fromSelection = new RadioButton
            {
                Text = "選択中の部材の位置 ＋ 目線高さ",
                Location = new System.Drawing.Point(16, 50),
                AutoSize = true,
            };
            var eyeHeightLabel = new Label
            {
                Text = "目線高さ:",
                Location = new System.Drawing.Point(36, 78),
                AutoSize = true,
            };
            _eyeHeight = new TextBox
            {
                Location = new System.Drawing.Point(110, 74),
                Size = new System.Drawing.Size(90, 23),
                Text = "1600",
            };
            var unitsLabel = new Label
            {
                Text = "モデル単位: " + _doc.Units,
                Location = new System.Drawing.Point(210, 78),
                AutoSize = true,
            };
            _fromCoordinates = new RadioButton
            {
                Text = "座標を入力",
                Location = new System.Drawing.Point(16, 104),
                AutoSize = true,
            };
            var coordLabel = new Label
            {
                Text = "X / Y / Z:",
                Location = new System.Drawing.Point(36, 134),
                AutoSize = true,
            };
            _coordX = new TextBox { Location = new System.Drawing.Point(110, 130), Size = new System.Drawing.Size(100, 23) };
            _coordY = new TextBox { Location = new System.Drawing.Point(216, 130), Size = new System.Drawing.Size(100, 23) };
            _coordZ = new TextBox { Location = new System.Drawing.Point(322, 130), Size = new System.Drawing.Size(100, 23) };

            positionGroup.Controls.AddRange(new Control[]
            {
                _fromViewpoint, _fromSelection, eyeHeightLabel, _eyeHeight, unitsLabel,
                _fromCoordinates, coordLabel, _coordX, _coordY, _coordZ,
            });

            var outputGroup = new GroupBox
            {
                Text = "出力設定",
                Location = new System.Drawing.Point(12, 188),
                Size = new System.Drawing.Size(454, 150),
            };

            var faceSizeLabel = new Label
            {
                Text = "面解像度(px):",
                Location = new System.Drawing.Point(16, 28),
                AutoSize = true,
            };
            _faceSize = new ComboBox
            {
                Location = new System.Drawing.Point(120, 24),
                Size = new System.Drawing.Size(90, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            _faceSize.Items.AddRange(new object[] { "1024", "2048", "4096" });
            _faceSize.SelectedIndex = 1;

            var outputWidthLabel = new Label
            {
                Text = "パノラマ幅(px):",
                Location = new System.Drawing.Point(230, 28),
                AutoSize = true,
            };
            _outputWidth = new ComboBox
            {
                Location = new System.Drawing.Point(340, 24),
                Size = new System.Drawing.Size(90, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            _outputWidth.Items.AddRange(new object[] { "4096", "8192", "16384" });
            _outputWidth.SelectedIndex = 1;

            var outputDirLabel = new Label
            {
                Text = "出力先:",
                Location = new System.Drawing.Point(16, 64),
                AutoSize = true,
            };
            _outputDir = new TextBox
            {
                Location = new System.Drawing.Point(80, 60),
                Size = new System.Drawing.Size(320, 23),
            };
            var browseButton = new Button
            {
                Text = "...",
                Location = new System.Drawing.Point(406, 59),
                Size = new System.Drawing.Size(32, 25),
            };
            browseButton.Click += OnBrowseOutputDir;

            _keepFaces = new CheckBox
            {
                Text = "中間ファイル（6面PNG）を残す",
                Location = new System.Drawing.Point(16, 96),
                AutoSize = true,
                Checked = true,
            };

            outputGroup.Controls.AddRange(new Control[]
            {
                faceSizeLabel, _faceSize, outputWidthLabel, _outputWidth,
                outputDirLabel, _outputDir, browseButton, _keepFaces,
            });

            _status = new Label
            {
                Text = "撮影位置と出力先を指定して「生成」を押してください。",
                Location = new System.Drawing.Point(12, 350),
                Size = new System.Drawing.Size(454, 40),
            };

            _generate = new Button
            {
                Text = "生成",
                Location = new System.Drawing.Point(270, 398),
                Size = new System.Drawing.Size(94, 28),
            };
            _generate.Click += OnGenerate;

            _close = new Button
            {
                Text = "閉じる",
                Location = new System.Drawing.Point(372, 398),
                Size = new System.Drawing.Size(94, 28),
                DialogResult = DialogResult.Cancel,
            };

            Controls.AddRange(new Control[] { positionGroup, outputGroup, _status, _generate, _close });
            AcceptButton = _generate;
            CancelButton = _close;
        }

        private void LoadDefaults()
        {
            _outputDir.Text = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "PanoramaNavis");

            // 座標入力の初期値として現在のカメラ位置を入れておく
            Point3D pos = _doc.CurrentViewpoint.CreateCopy().Position;
            _coordX.Text = pos.X.ToString("0.###", CultureInfo.InvariantCulture);
            _coordY.Text = pos.Y.ToString("0.###", CultureInfo.InvariantCulture);
            _coordZ.Text = pos.Z.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private void OnBrowseOutputDir(object sender, EventArgs e)
        {
            using (var browser = new FolderBrowserDialog())
            {
                browser.Description = "パノラマの出力先フォルダを選択してください";
                if (Directory.Exists(_outputDir.Text))
                    browser.SelectedPath = _outputDir.Text;
                if (browser.ShowDialog(this) == DialogResult.OK)
                    _outputDir.Text = browser.SelectedPath;
            }
        }

        private void OnGenerate(object sender, EventArgs e)
        {
            CaptureOptions options;
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

            _generate.Enabled = false;
            _close.Enabled = false;
            try
            {
                var service = new PanoramaCaptureService(_doc, message =>
                {
                    _status.Text = message;
                    _status.Refresh();
                });
                CaptureResult result = service.Run(options);

                _status.Text = "完了: " + result.OutputFolder;
                DialogResult answer = MessageBox.Show(this,
                    "パノラマを生成しました。\n\n" + result.PanoramaPath +
                    "\n\n出力フォルダを開きますか？",
                    "PanoramaNavis", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (answer == DialogResult.Yes)
                    Process.Start("explorer.exe", "\"" + result.OutputFolder + "\"");
            }
            catch (Exception ex)
            {
                _status.Text = "失敗: " + ex.Message;
                MessageBox.Show(this, "パノラマ生成に失敗しました:\n" + ex.Message,
                    "PanoramaNavis", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _generate.Enabled = true;
                _close.Enabled = true;
            }
        }

        private CaptureOptions BuildOptions()
        {
            var options = new CaptureOptions
            {
                FaceSize = int.Parse((string)_faceSize.SelectedItem, CultureInfo.InvariantCulture),
                OutputWidth = int.Parse((string)_outputWidth.SelectedItem, CultureInfo.InvariantCulture),
                OutputDirectory = _outputDir.Text.Trim(),
                KeepFaceImages = _keepFaces.Checked,
            };

            if (string.IsNullOrWhiteSpace(options.OutputDirectory))
                throw new ArgumentException("出力先フォルダを指定してください。");

            if (_fromViewpoint.Checked)
            {
                options.PositionSource = PositionSource.CurrentViewpoint;
            }
            else if (_fromSelection.Checked)
            {
                options.PositionSource = PositionSource.SelectedItem;
                options.EyeHeight = ParseDouble(_eyeHeight.Text, "目線高さ");
            }
            else
            {
                options.PositionSource = PositionSource.ManualCoordinates;
                options.ManualPosition = new Vec3(
                    ParseDouble(_coordX.Text, "X座標"),
                    ParseDouble(_coordY.Text, "Y座標"),
                    ParseDouble(_coordZ.Text, "Z座標"));
            }

            return options;
        }

        private static double ParseDouble(string text, string fieldName)
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                throw new ArgumentException($"{fieldName} の値が数値として読み取れません: 「{text}」");
            return value;
        }
    }
}
