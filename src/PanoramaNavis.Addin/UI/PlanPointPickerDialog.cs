using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using PanoramaNavis.Core;

namespace PanoramaNavis.Addin.UI
{
    /// <summary>ピッカーで選んだ撮影地点（平面図画像のピクセル座標）。</summary>
    internal sealed class PickedPoint
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double PixelX { get; set; }
        public double PixelY { get; set; }
    }

    /// <summary>
    /// 撮影した平面図を表示し、クリックで撮影地点を複数指定するダイアログ。
    /// 操作: 左クリック=地点追加（マーカー上なら選択）/ 左右ドラッグ=パン / ホイール=ズーム /
    /// Delete=選択地点の削除 / 一覧の名前はクリックで編集可。
    /// IDは常に表示順で P01, P02, ... と振り直される（撮影前のため安全）。
    /// </summary>
    internal sealed class PlanPointPickerDialog : Form
    {
        private const double MarkerHitRadius = 14.0; // パネル座標での当たり判定半径(px)

        private readonly PlanMapping _mapping;
        private readonly string _unitsLabel;
        private readonly MemoryStream _planImageStream;
        private readonly Image _planImage;

        private readonly List<PickedPoint> _points = new List<PickedPoint>();
        private int _selectedIndex = -1;

        private PlanPanel _panel;
        private ListView _list;
        private Button _delete;
        private Button _clear;
        private Button _ok;
        private Button _cancel;
        private Label _info;
        private Label _coords;
        private Label _help;

        // 表示変換: パネル座標 = (画像座標 − 視野中心) × ズーム + パネル中心
        private double _zoom = 1.0;
        private double _viewCenterX;
        private double _viewCenterY;
        private bool _fitPending = true;

        private bool _leftDown;
        private bool _dragging;
        private Point _lastMouse;
        private Point _mouseDownAt;
        private bool _syncingList;

        public IReadOnlyList<PickedPoint> Points => _points;

        public PlanPointPickerDialog(
            string planImagePath, PlanMapping mapping, string unitsLabel,
            double floorZ, double slabThickness)
        {
            _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
            _unitsLabel = unitsLabel ?? "";

            // ファイルロックを避けるためメモリへ読み込む（GDI+はストリームの生存を要求する）
            _planImageStream = new MemoryStream(File.ReadAllBytes(planImagePath));
            _planImage = Image.FromStream(_planImageStream);

            BuildLayout(floorZ, slabThickness);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _planImage.Dispose();
                _planImageStream.Dispose();
            }
            base.Dispose(disposing);
        }

        private void BuildLayout(double floorZ, double slabThickness)
        {
            Text = "撮影地点の指定 — PanoramaNavis";
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1000, 640);
            MinimumSize = new Size(820, 520);
            KeyPreview = true;

            _panel = new PlanPanel
            {
                Location = new Point(12, 12),
                Size = new Size(ClientSize.Width - 308, ClientSize.Height - 64),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            };
            _panel.Paint += OnPanelPaint;
            _panel.MouseDown += OnPanelMouseDown;
            _panel.MouseMove += OnPanelMouseMove;
            _panel.MouseUp += OnPanelMouseUp;
            _panel.MouseWheel += OnPanelMouseWheel;
            _panel.MouseEnter += (sender, e) => _panel.Focus();
            _panel.Resize += (sender, e) => { _fitPending = true; _panel.Invalidate(); };

            int rightX = ClientSize.Width - 284;
            const AnchorStyles rightAnchor = AnchorStyles.Top | AnchorStyles.Right;

            _info = new Label
            {
                Text = string.Format(CultureInfo.InvariantCulture,
                    "床レベルZ: {0:0.###} / 厚み: {1:0.###}（{2}）",
                    floorZ, slabThickness, _unitsLabel),
                Location = new Point(rightX, 12),
                Size = new Size(272, 34),
                Anchor = rightAnchor,
            };

            _list = new ListView
            {
                Location = new Point(rightX, 50),
                Size = new Size(272, ClientSize.Height - 240),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right,
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false,
                MultiSelect = false,
                LabelEdit = true,
            };
            _list.Columns.Add("名前", 108);
            _list.Columns.Add("X", 78, HorizontalAlignment.Right);
            _list.Columns.Add("Y", 78, HorizontalAlignment.Right);
            _list.SelectedIndexChanged += OnListSelectionChanged;
            _list.AfterLabelEdit += OnListLabelEdit;

            _coords = new Label
            {
                Text = "カーソル位置: —",
                Location = new Point(rightX, ClientSize.Height - 184),
                Size = new Size(272, 20),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            };

            _delete = new Button
            {
                Text = "削除",
                Location = new Point(rightX, ClientSize.Height - 156),
                Size = new Size(130, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Enabled = false,
            };
            _delete.Click += (sender, e) => DeleteSelected();

            _clear = new Button
            {
                Text = "全消去",
                Location = new Point(rightX + 142, ClientSize.Height - 156),
                Size = new Size(130, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            };
            _clear.Click += OnClearAll;

            _help = new Label
            {
                Text = "左クリック: 地点追加（マーカー上は選択）\n" +
                       "ドラッグ: 表示移動 / ホイール: 拡大縮小\n" +
                       "Delete: 選択地点を削除 / 名前はクリックで編集",
                Location = new Point(rightX, ClientSize.Height - 120),
                Size = new Size(272, 56),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            };

            _ok = new Button
            {
                Text = "決定（パノラマ撮影へ）",
                Location = new Point(rightX, ClientSize.Height - 44),
                Size = new Size(170, 30),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                DialogResult = DialogResult.OK,
                Enabled = false,
            };
            _cancel = new Button
            {
                Text = "キャンセル",
                Location = new Point(rightX + 178, ClientSize.Height - 44),
                Size = new Size(94, 30),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                DialogResult = DialogResult.Cancel,
            };

            Controls.AddRange(new Control[]
            {
                _panel, _info, _list, _coords, _delete, _clear, _help, _ok, _cancel,
            });
            AcceptButton = null; // Enterでの誤決定を防ぐ（決定はボタンのみ）
            CancelButton = _cancel;

            KeyDown += (sender, e) =>
            {
                if (e.KeyCode == Keys.Delete && !_list.Focused)
                {
                    DeleteSelected();
                    e.Handled = true;
                }
            };
        }

        // ---- 座標変換 ----

        private PointF ImageToPanel(double imageX, double imageY)
        {
            return new PointF(
                (float)((imageX - _viewCenterX) * _zoom + _panel.ClientSize.Width / 2.0),
                (float)((imageY - _viewCenterY) * _zoom + _panel.ClientSize.Height / 2.0));
        }

        private (double X, double Y) PanelToImage(Point panelPoint)
        {
            return (
                (panelPoint.X - _panel.ClientSize.Width / 2.0) / _zoom + _viewCenterX,
                (panelPoint.Y - _panel.ClientSize.Height / 2.0) / _zoom + _viewCenterY);
        }

        private double FitZoom()
        {
            if (_panel.ClientSize.Width <= 0 || _panel.ClientSize.Height <= 0)
                return 1.0;
            return Math.Min(
                (double)_panel.ClientSize.Width / _planImage.Width,
                (double)_panel.ClientSize.Height / _planImage.Height) * 0.97;
        }

        private void FitToPanel()
        {
            _zoom = Math.Max(FitZoom(), 0.001);
            _viewCenterX = _planImage.Width / 2.0;
            _viewCenterY = _planImage.Height / 2.0;
        }

        // ---- 描画 ----

        private void OnPanelPaint(object sender, PaintEventArgs e)
        {
            if (_fitPending)
            {
                FitToPanel();
                _fitPending = false;
            }

            Graphics g = e.Graphics;
            g.InterpolationMode = _zoom >= 1.0
                ? InterpolationMode.NearestNeighbor
                : InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            PointF topLeft = ImageToPanel(0, 0);
            g.DrawImage(_planImage, new RectangleF(
                topLeft.X, topLeft.Y,
                (float)(_planImage.Width * _zoom), (float)(_planImage.Height * _zoom)));

            g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = 0; i < _points.Count; i++)
            {
                PickedPoint point = _points[i];
                PointF center = ImageToPanel(point.PixelX, point.PixelY);
                DrawMarker(g, center, i + 1, point, i == _selectedIndex);
            }
        }

        private static void DrawMarker(Graphics g, PointF center, int number, PickedPoint point, bool selected)
        {
            const float radius = 11f;
            Color fill = selected ? Color.FromArgb(235, 255, 140, 0) : Color.FromArgb(225, 46, 104, 210);

            using (var brush = new SolidBrush(fill))
            using (var border = new Pen(Color.White, 2f))
            {
                g.FillEllipse(brush, center.X - radius, center.Y - radius, radius * 2, radius * 2);
                g.DrawEllipse(border, center.X - radius, center.Y - radius, radius * 2, radius * 2);
            }

            using (var font = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            })
            {
                g.DrawString(number.ToString(CultureInfo.InvariantCulture), font, Brushes.White,
                    new RectangleF(center.X - radius, center.Y - radius, radius * 2, radius * 2), format);
            }

            // IDと異なる名前が付いていればマーカー横に表示する
            if (!string.IsNullOrEmpty(point.Name) && point.Name != point.Id)
            {
                using (var font = new Font("Segoe UI", 9f))
                {
                    SizeF size = g.MeasureString(point.Name, font);
                    var rect = new RectangleF(center.X + radius + 3, center.Y - size.Height / 2,
                        size.Width + 6, size.Height);
                    using (var back = new SolidBrush(Color.FromArgb(180, 20, 23, 28)))
                        g.FillRectangle(back, rect);
                    g.DrawString(point.Name, font, Brushes.White, rect.X + 3, rect.Y);
                }
            }
        }

        // ---- マウス操作 ----

        private void OnPanelMouseDown(object sender, MouseEventArgs e)
        {
            _panel.Focus();
            _lastMouse = e.Location;
            _mouseDownAt = e.Location;
            _dragging = false;
            if (e.Button == MouseButtons.Left)
                _leftDown = true;
        }

        private void OnPanelMouseMove(object sender, MouseEventArgs e)
        {
            var image = PanelToImage(e.Location);
            if (_mapping.ContainsPixel(image.X, image.Y))
            {
                var world = _mapping.PixelToWorld(image.X, image.Y);
                _coords.Text = string.Format(CultureInfo.InvariantCulture,
                    "カーソル位置: X={0:0.###}, Y={1:0.###}", world.X, world.Y);
            }
            else
            {
                _coords.Text = "カーソル位置: —";
            }

            bool panButton = _leftDown
                || e.Button == MouseButtons.Right || e.Button == MouseButtons.Middle;
            if (!panButton)
                return;

            if (!_dragging)
            {
                int dx = e.X - _mouseDownAt.X;
                int dy = e.Y - _mouseDownAt.Y;
                if (dx * dx + dy * dy < 25) // 5px未満はクリック扱いのまま
                    return;
                _dragging = true;
            }

            _viewCenterX -= (e.X - _lastMouse.X) / _zoom;
            _viewCenterY -= (e.Y - _lastMouse.Y) / _zoom;
            _lastMouse = e.Location;
            _panel.Invalidate();
        }

        private void OnPanelMouseUp(object sender, MouseEventArgs e)
        {
            bool wasClick = !_dragging && e.Button == MouseButtons.Left && _leftDown;
            if (e.Button == MouseButtons.Left)
                _leftDown = false;
            _dragging = false;
            if (!wasClick)
                return;

            int hit = HitTest(e.Location);
            if (hit >= 0)
            {
                SelectPoint(hit);
                return;
            }

            var image = PanelToImage(e.Location);
            if (!_mapping.ContainsPixel(image.X, image.Y))
                return;

            _points.Add(new PickedPoint { PixelX = image.X, PixelY = image.Y });
            RenumberIds();
            RebuildList();
            SelectPoint(_points.Count - 1);
        }

        private void OnPanelMouseWheel(object sender, MouseEventArgs e)
        {
            var anchor = PanelToImage(e.Location);
            double minZoom = Math.Max(FitZoom() * 0.3, 0.001);
            _zoom = Math.Max(minZoom, Math.Min(32.0, _zoom * Math.Exp(e.Delta * 0.0012)));

            // カーソル下の画像座標が動かないよう視野中心を補正する
            _viewCenterX = anchor.X - (e.Location.X - _panel.ClientSize.Width / 2.0) / _zoom;
            _viewCenterY = anchor.Y - (e.Location.Y - _panel.ClientSize.Height / 2.0) / _zoom;
            _panel.Invalidate();
        }

        private int HitTest(Point panelPoint)
        {
            int best = -1;
            double bestDist = MarkerHitRadius;
            for (int i = 0; i < _points.Count; i++)
            {
                PointF c = ImageToPanel(_points[i].PixelX, _points[i].PixelY);
                double dist = Math.Sqrt(
                    (c.X - panelPoint.X) * (c.X - panelPoint.X) +
                    (c.Y - panelPoint.Y) * (c.Y - panelPoint.Y));
                if (dist <= bestDist)
                {
                    bestDist = dist;
                    best = i;
                }
            }
            return best;
        }

        // ---- 地点リスト管理 ----

        /// <summary>IDを表示順で P01, P02, ... に振り直す（未リネームの名前はIDに追従）。</summary>
        private void RenumberIds()
        {
            for (int i = 0; i < _points.Count; i++)
            {
                string newId = "P" + (i + 1).ToString("00", CultureInfo.InvariantCulture);
                PickedPoint point = _points[i];
                if (string.IsNullOrEmpty(point.Name) || point.Name == point.Id)
                    point.Name = newId;
                point.Id = newId;
            }
        }

        private void RebuildList()
        {
            _syncingList = true;
            try
            {
                _list.BeginUpdate();
                _list.Items.Clear();
                foreach (PickedPoint point in _points)
                {
                    var world = _mapping.PixelToWorld(point.PixelX, point.PixelY);
                    var item = new ListViewItem(point.Name);
                    item.SubItems.Add(world.X.ToString("0.###", CultureInfo.InvariantCulture));
                    item.SubItems.Add(world.Y.ToString("0.###", CultureInfo.InvariantCulture));
                    _list.Items.Add(item);
                }
                _list.EndUpdate();
            }
            finally
            {
                _syncingList = false;
            }

            _ok.Enabled = _points.Count > 0;
            _delete.Enabled = _selectedIndex >= 0 && _selectedIndex < _points.Count;
            _panel.Invalidate();
        }

        private void SelectPoint(int index)
        {
            _selectedIndex = index;
            _syncingList = true;
            try
            {
                _list.SelectedItems.Clear();
                if (index >= 0 && index < _list.Items.Count)
                {
                    _list.Items[index].Selected = true;
                    _list.Items[index].EnsureVisible();
                }
            }
            finally
            {
                _syncingList = false;
            }
            _delete.Enabled = index >= 0;
            _panel.Invalidate();
        }

        private void OnListSelectionChanged(object sender, EventArgs e)
        {
            if (_syncingList)
                return;
            _selectedIndex = _list.SelectedIndices.Count > 0 ? _list.SelectedIndices[0] : -1;
            _delete.Enabled = _selectedIndex >= 0;
            _panel.Invalidate();
        }

        private void OnListLabelEdit(object sender, LabelEditEventArgs e)
        {
            if (e.Label == null)
                return; // 変更なし
            if (string.IsNullOrWhiteSpace(e.Label))
            {
                e.CancelEdit = true;
                return;
            }
            _points[e.Item].Name = e.Label.Trim();
            _panel.Invalidate();
        }

        private void DeleteSelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _points.Count)
                return;
            _points.RemoveAt(_selectedIndex);
            _selectedIndex = Math.Min(_selectedIndex, _points.Count - 1);
            RenumberIds();
            RebuildList();
            SelectPoint(_selectedIndex);
        }

        private void OnClearAll(object sender, EventArgs e)
        {
            if (_points.Count == 0)
                return;
            if (MessageBox.Show(this, "すべての地点を削除しますか？", "PanoramaNavis",
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
                return;
            _points.Clear();
            _selectedIndex = -1;
            RebuildList();
        }

        /// <summary>ちらつき防止のダブルバッファ付きパネル。</summary>
        private sealed class PlanPanel : Panel
        {
            public PlanPanel()
            {
                DoubleBuffered = true;
                ResizeRedraw = true;
                BackColor = Color.FromArgb(32, 35, 40);
                BorderStyle = BorderStyle.FixedSingle;
                TabStop = true;
            }
        }
    }
}
