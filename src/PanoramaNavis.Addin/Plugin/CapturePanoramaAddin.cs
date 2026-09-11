using System;
using System.Windows.Forms;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;
using NavisApp = Autodesk.Navisworks.Api.Application;

namespace PanoramaNavis.Addin.Plugin
{
    /// <summary>
    /// リボン「ツール アドイン」タブに表示されるエントリポイント。
    /// 実行するとパノラマ生成ダイアログを開く。
    /// </summary>
    [PluginAttribute("PanoramaNavis.Capture360", "PMED",
        DisplayName = "360°パノラマ作成",
        ToolTip = "指定した場所から360度パノラマ画像を生成します")]
    [AddInPluginAttribute(AddInLocation.AddIn)]
    public class CapturePanoramaAddin : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            try
            {
                Document doc = NavisApp.ActiveDocument;
                if (doc == null || doc.IsClear)
                {
                    MessageBox.Show("モデルが開かれていません。モデルを開いてから実行してください。",
                        "PanoramaNavis", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }

                using (var dialog = new UI.CaptureDialog(doc))
                {
                    dialog.ShowDialog();
                }
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("予期しないエラーが発生しました:\n" + ex,
                    "PanoramaNavis", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return -1;
            }
        }
    }
}
