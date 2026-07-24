using System;
using System.IO;
using System.Text;
using ComApi = Autodesk.Navisworks.Api.Interop.ComApi;
using ComApiBridge = Autodesk.Navisworks.Api.ComApi.ComApiBridge;

namespace PanoramaNavis.Addin.Capture
{
    /// <summary>
    /// 現在のビューを画像ファイルへ出力する。
    /// COM APIの画像IOプラグイン "lcodpimage" を使用する（ADN公式サンプル準拠の方式）。
    /// </summary>
    internal sealed class ViewExporter
    {
        private const string ImagePluginId = "lcodpimage";
        private const string PngFormatId = "lcodpexpng";

        private readonly string _diagnosticsPath;
        private bool _optionsLogged;

        /// <param name="diagnosticsPath">
        /// lcodpimage の全オプション名を書き出す診断ログのパス（null可）。
        /// 実機でのオプション名確認（AA・レンダラ指定等）に使う。
        /// </param>
        public ViewExporter(string diagnosticsPath)
        {
            _diagnosticsPath = diagnosticsPath;
        }

        public void ExportPng(string filePath, int width, int height)
        {
            ComApi.InwOpState10 state = ComApiBridge.State;
            ComApi.InwOaPropertyVec options = state.GetIOPluginOptions(ImagePluginId);

            LogOptionsOnce(options);

            foreach (ComApi.InwOaProperty option in options.Properties())
            {
                switch (option.name)
                {
                    case "export.image.format":
                        option.value = PngFormatId;
                        break;
                    case "export.image.width":
                        option.value = width;
                        break;
                    case "export.image.height":
                        option.value = height;
                        break;
                }
            }

            state.DriveIOPlugin(ImagePluginId, filePath, options);

            if (!File.Exists(filePath))
                throw new IOException(
                    $"ビュー画像の出力に失敗しました: {filePath}\n" +
                    "diagnostics.log の lcodpimage オプション一覧を確認してください。");
        }

        private void LogOptionsOnce(ComApi.InwOaPropertyVec options)
        {
            if (_optionsLogged || string.IsNullOrEmpty(_diagnosticsPath))
                return;
            _optionsLogged = true;
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ImagePluginId} options:");
                foreach (ComApi.InwOaProperty option in options.Properties())
                    sb.AppendLine($"  {option.name} = {option.value}");
                File.AppendAllText(_diagnosticsPath, sb.ToString());
            }
            catch
            {
                // 診断ログの失敗で本処理を止めない
            }
        }
    }
}
