# PanoramaNavis — Navisworks 360°パノラマ生成アドイン

Autodesk Navisworks 上で指定した場所から **360度パノラマ画像（正距円筒図法 / Equirectangular）** を生成する .NET アドインです。

同一カメラ位置から前・右・後・左・上・下の6方向（キューブマップ）を撮影し、1枚の 2:1 パノラマJPEGへ合成します。生成したパノラマは付属のWebビューア（`viewer/panorama-viewer.html`）でブラウザ閲覧でき、Navisworksを持たない関係者へ「その場所に立った視界」を共有できます。

```text
Navisworks モデル
      │  ① 撮影位置を決定（現在視点 / 選択部材 / 座標入力）
      │  ② カメラ位置固定・90°画角で6方向をキャプチャ
      ▼
6面キューブマップ (PNG)
      │  ③ 正距円筒図法へ変換（PanoramaNavis.Core）
      ▼
panorama.jpg (2:1) ＋ thumbnail.jpg ＋ metadata.json
      │
      ▼
Webビューア / PMESYSTEM 連携（将来）
```

## リポジトリ構成

| パス | 内容 |
|---|---|
| `src/PanoramaNavis.Core/` | キューブマップ→正距円筒変換エンジン（netstandard2.0、Navisworks非依存・単体テスト可能） |
| `src/PanoramaNavis.Addin/` | Navisworksアドイン本体（.NET Framework 4.8、C# + Navisworks .NET API） |
| `tests/PanoramaNavis.Core.Tests/` | 変換数学の単体テスト（xUnit） |
| `tools/TestPatternGenerator/` | Navisworks不要の検証ツール（合成シーンからテストパノラマを生成） |
| `viewer/panorama-viewer.html` | 依存ライブラリなしの単体WebGLパノラマビューア |
| `viewer/sample-panorama.jpg` | 上記ツールで生成したサンプルパノラマ（ビューアの動作確認用） |
| `docs/PLAN.md` | 開発計画・フェーズ・検証項目・リスク |
| `docs/ARCHITECTURE.md` | 座標系規約・変換数式・コンポーネント設計・メタデータ仕様 |

## 開発ステータス

現在は **Phase 0（設計＋スキャフォールド）完了** の段階です。

- ✅ 変換エンジン（キューブ→正距円筒）実装・単体テスト36件パス
- ✅ 変換規約のエンドツーエンド検証済み（合成キューブ面→正距円筒→WebGLビューアで
  経緯度グリッドが面境界・左右端・天地で連続することを目視確認）
- ✅ アドイン本体のコード一式（Windows + Navisworks SDK でのビルド検証待ち）
- ✅ Webビューア＋サンプルパノラマ
- ⬜ Windows実機でのPoC検証（`docs/PLAN.md` の検証チェックリスト参照）

Navisworks APIに依存する部分は実機でのみ検証可能なため、要確認箇所を `docs/ARCHITECTURE.md` の「API検証状況」にまとめています。

## ビルド（Windows）

前提: Visual Studio 2022 もしくは .NET SDK 8＋.NET Framework 4.8 Developer Pack、Navisworks Manage/Simulate 2024以降。

```powershell
# Navisworks のインストール先を指定（既定は Navisworks Manage 2025）
$env:NAVISWORKS_API_DIR = "C:\Program Files\Autodesk\Navisworks Manage 2025"

dotnet build PanoramaNavis.sln -c Release
```

参照するNavisworks DLL（インストール先ルートにあります）:

- `Autodesk.Navisworks.Api.dll`
- `Autodesk.Navisworks.Api.ComApi.dll`
- `Autodesk.Navisworks.Api.Interop.ComApi.dll`

> Navisworksのバージョンが .NET 8 ベースに移行した場合は、`src/PanoramaNavis.Addin/PanoramaNavis.Addin.csproj` の `TargetFramework` を `net8.0-windows` に変更してください（SDKスタイルのため1行の変更です）。

## 配置

ビルドした `PanoramaNavis.Addin.dll` を、**アセンブリ名と同名のフォルダ**に入れてNavisworksのPluginsフォルダへ配置します。

```text
%APPDATA%\Autodesk Navisworks Manage 2025\Plugins\PanoramaNavis.Addin\PanoramaNavis.Addin.dll
（または <Navisworksインストール先>\Plugins\PanoramaNavis.Addin\PanoramaNavis.Addin.dll）
```

`PanoramaNavis.Core.dll` も同じフォルダへコピーします（ビルド出力に含まれます）。

起動後、リボンの「ツール アドイン」タブに **「360°パノラマ作成」** が表示されます。

## 使い方（PoC）

1. モデルを開き、撮影したい場所へ視点を移動（または部材を選択）
2. 「360°パノラマ作成」を実行
3. ダイアログで撮影位置ソース・目線高さ・解像度・出力先を指定
4. 「生成」を押すと6面キャプチャ→合成が実行され、以下が出力されます

```text
<出力先>/Panorama_20260724_143000/
├─ panorama.jpg          # 正距円筒 360°画像（幅:高さ = 2:1）
├─ thumbnail.jpg         # サムネイル
├─ metadata.json         # 撮影座標・方位・視点高さ・モデル情報
└─ faces/                # （オプション）中間キューブ面PNG
```

生成後は元のビューポイントに自動復帰します。

## ビューアでの閲覧

`viewer/panorama-viewer.html` をブラウザで開き、`panorama.jpg` をドラッグ＆ドロップしてください。マウスドラッグで見回し、ホイール／ピンチでズームできます。サーバー不要・単一ファイルで動作します。

まず試す場合は同フォルダの `sample-panorama.jpg`（方位色相帯＋経緯度グリッドの合成テストシーン）をドロップしてください。

## テスト・検証（Navisworks不要）

変換エンジンはNavisworks非依存のため、どのOSでも実行できます。

```bash
# 単体テスト（面規約・UV変換・往復精度・合成配置・メタデータ）
dotnet test tests/PanoramaNavis.Core.Tests

# テストパノラマの生成（キューブ→正距円筒の視覚検証用BMPを出力）
dotnet run --project tools/TestPatternGenerator -c Release -- ./out
```

生成された `test-panorama.bmp` をビューアで開き、グリッド線が面境界で折れずに連続していれば、撮影規約と合成規約が一致しています。

## ロードマップ（概要）

| フェーズ | 内容 |
|---|---|
| Phase 0 | 設計・スキャフォールド・変換エンジン＋テスト（本コミット） |
| Phase 1 | Windows実機PoC：現在視点からの360°生成、継ぎ目・画角検証 |
| Phase 2 | クリック地点指定、部材内部判定と位置補正、保存済みビューポイント一括変換、高解像度化 |
| Phase 3 | ホットスポット、PMESYSTEM連携（撮影地点・課題・図面の紐付け）、モデル版比較 |
| Phase 4 | フォトリアルレンダリング、現場360°写真との定点比較 |

詳細は [docs/PLAN.md](docs/PLAN.md) を参照してください。
