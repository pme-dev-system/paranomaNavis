using System;
using System.Windows.Forms;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;
using NavisApp = Autodesk.Navisworks.Api.Application;

namespace PanoramaNavis.Addin.Plugin
{
    /// <summary>
    /// リボン「ツール アドイン」タブに表示されるエントリポイント（v2: 平面図パノラマツアー）。
    /// 平面図を撮影 → 平面図上のクリックで複数地点を指定 → 各地点のパノラマを一括生成し、
    /// 平面図・パノラマ群・地点情報（tour.json）を1フォルダに出力する。
    /// 既存の「360°パノラマ作成」（1地点）とは独立した機能として共存する。
    /// </summary>
    [PluginAttribute("PanoramaNavis.TourCapture", "PMED",
        DisplayName = "平面図パノラマツアー",
        ToolTip = "平面図を撮影し、クリックで指定した複数地点の360度パノラマを一括生成します")]
    [AddInPluginAttribute(AddInLocation.AddIn)]
    public class TourPanoramaAddin : AddInPlugin
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

                using (var dialog = new UI.TourSetupDialog(doc))
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
