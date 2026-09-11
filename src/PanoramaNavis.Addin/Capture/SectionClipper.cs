using System;
using System.IO;
using ComApi = Autodesk.Navisworks.Api.Interop.ComApi;
using ComApiBridge = Autodesk.Navisworks.Api.ComApi.ComApiBridge;

namespace PanoramaNavis.Addin.Capture
{
    /// <summary>
    /// COM APIのクリップ平面で水平スラブ断面（床Z〜断面上端Z）を一時的に設定する。
    ///
    /// セクショニングは.NET APIに公開されていないためCOM API
    /// （InwOpView.ClippingPlanes / InwOpClipPlaneColl）を使う。COM側のメンバ構成は
    /// バージョン差異の可能性があるため dynamic（遅延バインド）で呼び、失敗しても
    /// 例外で本処理を止めず「断面なしで撮影」へ切り替えられるよう成否を返す。
    /// 実機で確認したメンバ名は docs/ARCHITECTURE.md「API検証状況」へ反映すること。
    ///
    /// Dispose（または RestoreNow）で追加した平面を取り除き、元の状態へ戻す。
    /// </summary>
    internal sealed class SectionClipper : IDisposable
    {
        private readonly string _diagnosticsPath;
        private dynamic _clipColl;
        private bool _collWasEnabled;
        private int _planesBefore;

        public SectionClipper(string diagnosticsPath)
        {
            _diagnosticsPath = diagnosticsPath;
        }

        /// <summary>
        /// Z範囲 [floorZ, topZ] の外側を非表示にするクリップ平面（上端・下端の2枚）を追加する。
        /// 成功なら true。失敗時は診断ログへ理由を書き、表示状態を変えずに false を返す。
        /// </summary>
        public bool TryApplyHorizontalSlab(double floorZ, double topZ)
        {
            if (!(topZ > floorZ))
            {
                Log($"クリップ範囲が不正のため断面なしで続行します: floorZ={floorZ}, topZ={topZ}");
                return false;
            }

            try
            {
                dynamic state = ComApiBridge.State;
                dynamic view = state.CurrentView;
                _clipColl = GetClippingPlanes(view);
                _collWasEnabled = (bool)_clipColl.Enabled;
                _planesBefore = (int)_clipColl.Count;

                // 平面 n·p + d > 0 の側が非表示になる想定（要実機検証。逆ならこの2行の符号を反転）
                //   上端: n=(0,0,+1), d=−topZ   → z > topZ  を非表示（上階・屋根を消す）
                //   下端: n=(0,0,−1), d=+floorZ → z < floorZ を非表示（下階を消す）
                AddPlane(state, 0, 0, 1, -topZ);
                AddPlane(state, 0, 0, -1, floorZ);
                _clipColl.Enabled = true;

                Log($"クリップ平面を適用: z=[{floorZ}, {topZ}]（適用前の平面数={_planesBefore}, 有効={_collWasEnabled}）");
                return true;
            }
            catch (Exception ex)
            {
                Log("クリップ平面の適用に失敗しました（平面図は断面なしで撮影されます）: " + Describe(ex));
                RestoreNow();
                return false;
            }
        }

        /// <summary>追加した平面を取り除き、コレクションの有効状態を元へ戻す。失敗してもログのみ。</summary>
        public void RestoreNow()
        {
            if (_clipColl == null)
                return;

            try
            {
                int current = (int)_clipColl.Count;
                for (int i = current; i > _planesBefore; i--)
                    RemovePlaneAt(i);
                _clipColl.Enabled = _collWasEnabled;
                Log("クリップ平面を元に戻しました。");
            }
            catch (Exception ex)
            {
                // 取り外せない場合は少なくとも無効化を試み、手動解除の案内を残す
                try { _clipColl.Enabled = false; } catch { /* 下のログのみ */ }
                Log("クリップ平面の復元に失敗しました。Navisworksのセクショニング設定を手動で確認してください: "
                    + Describe(ex));
            }
            finally
            {
                _clipColl = null;
            }
        }

        public void Dispose() => RestoreNow();

        private void AddPlane(dynamic state, double nx, double ny, double nz, double distance)
        {
            dynamic clipPlane;
            try
            {
                clipPlane = _clipColl.CreatePlane();
            }
            catch
            {
                // 一部バージョンは挿入位置（1始まり）を要求する
                clipPlane = _clipColl.CreatePlane((int)_clipColl.Count + 1);
            }

            dynamic plane = state.ObjectFactory(
                ComApi.nwEObjectType.eObjectType_nwLPlane3f, null, null);
            plane.SetValue(nx, ny, nz, distance);
            clipPlane.Plane = plane;
            clipPlane.Enabled = true;
        }

        private void RemovePlaneAt(int oneBasedIndex)
        {
            try
            {
                _clipColl.RemovePlane(oneBasedIndex);
            }
            catch
            {
                // 0始まりの場合
                _clipColl.RemovePlane(oneBasedIndex - 1);
            }
        }

        private static dynamic GetClippingPlanes(dynamic view)
        {
            try
            {
                return view.ClippingPlanes();
            }
            catch
            {
                // メソッドではなくプロパティの場合
                return view.ClippingPlanes;
            }
        }

        private static string Describe(Exception ex) =>
            ex.GetType().Name + ": " + ex.Message;

        private void Log(string message)
        {
            if (string.IsNullOrEmpty(_diagnosticsPath))
                return;
            try
            {
                File.AppendAllText(
                    _diagnosticsPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] SectionClipper: {message}\n");
            }
            catch
            {
                // 診断ログの失敗で本処理を止めない
            }
        }
    }
}
