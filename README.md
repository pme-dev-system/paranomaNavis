# PanoramaNavis — Navisworks 360°パノラマ生成アドイン

Autodesk Navisworks 上で指定した場所から **360度パノラマ画像（正距円筒図法 / Equirectangular）** を生成する .NET アドインです。

同一カメラ位置から前・右・後・左・上・下の6方向（キューブマップ）を撮影し、1枚の 2:1 パノラマJPEGへ合成します。生成したパノラマは付属のWebビューア（`viewer/panorama-viewer.html`）でブラウザ閲覧でき、Navisworksを持たない関係者へ「その場所に立った視界」を共有できます。

リボンには2つの機能が並びます。

| リボンボタン | 内容 |
|---|---|
| **360°パノラマ作成**（v1） | 1地点のパノラマを生成 |
| **平面図パノラマツアー**（v2） | 断面指定で平面図を撮影 → 平面図をクリックして複数地点を指定 → 全地点のパノラマを一括生成し、平面図＋パノラマ群＋地点情報JSON（`tour.json`）を出力 |

```text
【v1: 1地点】
Navisworks モデル
      │  ① 撮影位置を決定（現在視点 / 選択部材 / 座標入力）
      │  ② カメラ位置固定・90°画角で6方向をキャプチャ
      ▼
6面キューブマップ (PNG)
      │  ③ 正距円筒図法へ変換（PanoramaNavis.Core）
      ▼
panorama.jpg (2:1) ＋ thumbnail.jpg ＋ metadata.json

【v2: 平面図パノラマツアー】
Navisworks モデル
      │  ① 床レベル（地点）と断面の厚みを設定
      │  ② 真上・北上固定の正射投影で平面図を撮影（厚み範囲のみ表示）
      ▼
plan.png（平面図）
      │  ③ 平面図をクリックして撮影地点を複数決定（P01, P02, ...）
      │  ④ 各地点で6面キャプチャ→パノラマ合成を一括実行
      ▼
plan.png ＋ P01..Pnn/panorama.jpg ＋ tour.json（地点のピクセル/ワールド座標）
      │
      ▼
ツアービューア（viewer/tour-viewer.html）: 平面図の地点クリックでパノラマ閲覧
```

## リポジトリ構成

| パス | 内容 |
|---|---|
| `src/PanoramaNavis.Core/` | キューブマップ→正距円筒変換エンジン（netstandard2.0、Navisworks非依存・単体テスト可能） |
| `src/PanoramaNavis.Addin/` | Navisworksアドイン本体（.NET Framework 4.8、C# + Navisworks .NET API） |
| `tests/PanoramaNavis.Core.Tests/` | 変換数学の単体テスト（xUnit） |
| `tools/TestPatternGenerator/` | Navisworks不要の検証ツール（合成シーンからテストパノラマ・サンプルツアーを生成） |
| `viewer/panorama-viewer.html` | 依存ライブラリなしの単体WebGLパノラマビューア（1枚表示） |
| `viewer/tour-viewer.html` | ツアービューア（平面図＋地点マーカー＋パノラマ。`tour.json` を読み込む） |
| `viewer/sample-panorama.jpg` | 上記ツールで生成したサンプルパノラマ（ビューアの動作確認用） |
| `docs/PLAN.md` | 開発計画・フェーズ・検証項目・リスク |
| `docs/ARCHITECTURE.md` | 座標系規約・変換数式・コンポーネント設計・メタデータ仕様 |

## 開発ステータス

現在は **Phase 0（設計＋スキャフォールド）＋ v2（平面図パノラマツアー）実装完了** の段階です。

- ✅ 変換エンジン（キューブ→正距円筒）実装・単体テスト48件パス
- ✅ 変換規約のエンドツーエンド検証済み（合成キューブ面→正距円筒→WebGLビューアで
  経緯度グリッドが面境界・左右端・天地で連続することを目視確認）
- ✅ アドイン本体のコード一式（Windows + Navisworks SDK でのビルド検証待ち）
- ✅ v2: 平面図撮影（正射投影＋断面クリップ）・地点ピッカー・一括撮影・`tour.json` 出力
- ✅ Webビューア（1枚表示＋ツアー）＋サンプルパノラマ。ツアービューアは合成サンプルで
  ブラウザ実機検証済み（自動読込・地点切替・平面図クリック・視線方向連動）
- ⬜ Windows実機でのPoC検証（`docs/PLAN.md` の検証チェックリスト参照）

Navisworks APIに依存する部分は実機でのみ検証可能なため、要確認箇所を `docs/ARCHITECTURE.md` の「API検証状況」にまとめています。

## ビルド（Windows）

前提: **Navisworks Manage/Simulate 2023以降がインストールされたWindows PC** と、次のいずれかのビルドツール。
.NET Framework 4.8 Developer Pack は不要です（参照アセンブリはNuGetから自動取得されます）。

| ビルドツール | 向いている場合 |
|---|---|
| .NET SDK 8（コマンドライン） | 最小構成。管理者権限なしでもインストール可 |
| Visual Studio 2022（Community可） | GUIで開発・デバッグもしたい場合 |
| Visual Studio Build Tools 2022 | IDE不要で `msbuild` コマンドだけ欲しい場合 |

参照先の既定は **Navisworks Simulate 2023**（`Directory.Build.props` で定義）なので、Simulate 2023ならそのままビルドできます。

```powershell
dotnet build PanoramaNavis.sln -c Release
```

.NET SDK を管理者権限なしで入れる場合（ユーザーフォルダにインストールされます）:

```powershell
Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile dotnet-install.ps1
powershell -ExecutionPolicy Bypass -File dotnet-install.ps1 -Channel 8.0
$env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"
```

Visual Studio Build Tools の場合は次のコマンドになります。

```powershell
msbuild PanoramaNavis.sln /restore /p:Configuration=Release
```

> ビルドが必要なのは1台だけです。生成された2つのDLLを他のPCの配置先へコピーすれば動くため、利用者全員がビルド環境を持つ必要はありません。

別バージョンやManageの場合のみ、環境変数で参照先を上書きします。

```powershell
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
%APPDATA%\Autodesk Navisworks Simulate 2023\Plugins\PanoramaNavis.Addin\PanoramaNavis.Addin.dll
（または <Navisworksインストール先>\Plugins\PanoramaNavis.Addin\PanoramaNavis.Addin.dll。
　Manage の場合はフォルダ名の Simulate を Manage に読み替え）
```

`PanoramaNavis.Core.dll` も同じフォルダへコピーします（ビルド出力に含まれます）。

起動後、リボンの「ツール アドイン」タブに **「360°パノラマ作成」** が表示されます。

## 使い方（PoC）

### v1: 360°パノラマ作成（1地点）

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

### v2: 平面図パノラマツアー（複数地点）

1. モデルを開き、「平面図パノラマツアー」を実行
2. **床レベル**（現在の視点から／選択部材の底面／Z入力）と**断面の厚み**、
   目線高さ・解像度・出力先を指定して「平面図を撮影して地点選択へ」
3. 真上（北=+Y が画像上）の正射投影で平面図が撮影されます。
   厚み範囲（床Z〜床Z＋厚み）以外はクリップ平面で非表示になります
4. 表示された平面図を**クリックして撮影地点を追加**（複数可）。
   ドラッグで移動、ホイールで拡大縮小、一覧で名前変更・削除ができます
5. 「決定（パノラマ撮影へ）」で全地点のパノラマを一括撮影（進捗表示・中断可）

```text
<出力先>/Tour_20260730_120000/
├─ plan.png              # 平面図（北上・正射投影）
├─ tour.json             # 平面図の座標対応＋全地点の情報（下記）
├─ P01/
│  ├─ panorama.jpg       # 地点P01の360°パノラマ
│  ├─ thumbnail.jpg
│  └─ metadata.json      # v1と同形式（単体ビューアでも閲覧可）
├─ P02/ ...
└─ diagnostics.log       # COM APIオプション・クリップ平面の診断ログ
```

`tour.json` には平面図画像とワールドXY座標のアフィン対応（`world_min/max`）、
床レベル・厚み、各地点のクリック位置（ピクセル）・カメラ位置（ワールド）・
パノラマ相対パスが記録されます。詳細は `docs/ARCHITECTURE.md` の「tour.json 仕様」参照。

> 断面クリップはCOM APIを使用します。適用に失敗した環境では警告の上、
> 断面なし（屋根を含む外観）の平面図で続行できます。その場合は
> Navisworksのセクショニングで手動断面を設定してから実行する方法もあります。

## ビューアでの閲覧

**1枚表示**: `viewer/panorama-viewer.html` をブラウザで開き、`panorama.jpg` を
ドラッグ＆ドロップしてください。マウスドラッグで見回し、ホイール／ピンチでズームできます。
サーバー不要・単一ファイルで動作します。
まず試す場合は同フォルダの `sample-panorama.jpg`（方位色相帯＋経緯度グリッドの合成テストシーン）をドロップしてください。

**ツアー表示**: `viewer/tour-viewer.html` をブラウザで開き、アドインが出力した
**ツアーフォルダごと**ドラッグ＆ドロップ（または「フォルダを選択」）してください。
左の平面図の地点マーカー／一覧クリックでパノラマが切り替わり、
見回しに合わせて平面図上に視線方向の扇形が表示されます（←→キーで地点切替）。
Webサーバーで配信する場合は、ツアーフォルダに `tour-viewer.html` を置くだけで
`tour.json` を自動読込します。

## テスト・検証（Navisworks不要）

変換エンジンはNavisworks非依存のため、どのOSでも実行できます。

```bash
# 単体テスト（面規約・UV変換・往復精度・合成配置・メタデータ・平面図座標対応）
dotnet test tests/PanoramaNavis.Core.Tests

# テストパノラマの生成（キューブ→正距円筒の視覚検証用BMPを出力）
dotnet run --project tools/TestPatternGenerator -c Release -- ./out

# --tour を付けるとツアービューア検証用のサンプルツアー（./out/tour-sample/）も生成
dotnet run --project tools/TestPatternGenerator -c Release -- ./out --tour
```

生成された `test-panorama.bmp` をビューアで開き、グリッド線が面境界で折れずに連続していれば、撮影規約と合成規約が一致しています。`tour-sample/` フォルダを `viewer/tour-viewer.html` へドラッグすると、ツアービューアの動作（地点切替・平面図連動）を確認できます。

## ロードマップ（概要）

| フェーズ | 内容 |
|---|---|
| Phase 0 | 設計・スキャフォールド・変換エンジン＋テスト |
| Phase 1 | Windows実機PoC：現在視点からの360°生成、継ぎ目・画角検証 |
| Phase 2.5 | **平面図パノラマツアー（v2・実装済み）**: 断面平面図→クリック地点指定→一括撮影→tour.json＋ツアービューア |
| Phase 2 | 部材内部判定と位置補正、保存済みビューポイント一括変換、高解像度化 |
| Phase 3 | ホットスポット、PMESYSTEM連携（撮影地点・課題・図面の紐付け）、モデル版比較 |
| Phase 4 | フォトリアルレンダリング、現場360°写真との定点比較 |

詳細は [docs/PLAN.md](docs/PLAN.md) を参照してください。
