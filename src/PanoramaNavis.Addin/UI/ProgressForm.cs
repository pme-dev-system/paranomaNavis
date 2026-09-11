using System;
using System.Windows.Forms;

namespace PanoramaNavis.Addin.UI
{
    /// <summary>
    /// 一括撮影の進捗表示と中断ボタン（モードレス）。
    /// 撮影はUIスレッドで同期実行されるため、中断は各地点の合間（DoEvents経由）で反映される。
    /// </summary>
    internal sealed class ProgressForm : Form
    {
        private readonly Label _message;
        private readonly ProgressBar _bar;
        private readonly Button _cancel;
        private bool _running = true;

        public bool CancelRequested { get; private set; }

        public ProgressForm(string title, int totalSteps)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new System.Drawing.Size(480, 130);

            _message = new Label
            {
                Text = "準備中...",
                Location = new System.Drawing.Point(12, 12),
                Size = new System.Drawing.Size(456, 42),
            };
            _bar = new ProgressBar
            {
                Location = new System.Drawing.Point(12, 58),
                Size = new System.Drawing.Size(456, 20),
                Minimum = 0,
                Maximum = Math.Max(1, totalSteps),
            };
            _cancel = new Button
            {
                Text = "中断",
                Location = new System.Drawing.Point(374, 90),
                Size = new System.Drawing.Size(94, 28),
            };
            _cancel.Click += (sender, e) =>
            {
                CancelRequested = true;
                _cancel.Enabled = false;
                ReportMessage("中断しています...（現在の地点の撮影が終わり次第停止します）");
            };

            Controls.AddRange(new Control[] { _message, _bar, _cancel });

            // 撮影中はフォームを閉じさせない（中断要求として扱う）
            FormClosing += (sender, e) =>
            {
                if (_running)
                {
                    CancelRequested = true;
                    e.Cancel = true;
                }
            };
        }

        /// <summary>完了ステップ数を進捗バーへ反映する。</summary>
        public void ReportStep(int completedSteps)
        {
            _bar.Value = Math.Max(_bar.Minimum, Math.Min(completedSteps, _bar.Maximum));
        }

        public void ReportMessage(string message)
        {
            _message.Text = message;
            _message.Refresh();
        }

        /// <summary>撮影終了後に呼ぶ。以降はフォームを閉じられる。</summary>
        public void Finish()
        {
            _running = false;
        }
    }
}
