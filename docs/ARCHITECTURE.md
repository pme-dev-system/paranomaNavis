# 技術設計 — PanoramaNavis

## コンポーネント構成

```text
┌─────────────────────────────────────────────────────┐
│ PanoramaNavis.Addin  (.NET Framework 4.8 / Windows)  │
│                                                      │
│  CapturePanoramaAddin   … AddInPlugin エントリ        │
│  CaptureDialog          … WinForms 設定ダイアログ      │
│  PanoramaCaptureService … 撮影オーケストレーション      │
│  NavisCamera            … ビューポイント/カメラ制御     │
│  ViewExporter           … COM API (lcodpimage) 出力   │
│  GdiPixelBufferIO       … PNG/JPEG ⇔ PixelBuffer     │
└──────────────────────┬──────────────────────────────┘
                       │ 参照
┌──────────────────────▼──────────────────────────────┐
│ PanoramaNavis.Core  (netstandard2.0 / OS非依存)       │
│                                                      │
│  Vec3, CubeFace, CubeOrientation … 座標系規約の単一ソース│
│  PixelBuffer            … RGBA32 バッファ＋バイリニア補間│
│  CubeMapSet             … 6面画像の集合                │
│  EquirectangularConverter … キューブ→正距円筒変換       │
│  PanoramaMetadata / JsonLite … メタデータ出力          │
└─────────────────────────────────────────────────────┘
```

設計原則:

1. **数学はCoreに集約する。** 面の向き・UV変換・合成はNavisworksに依存しない
   `PanoramaNavis.Core` に置き、どのOSでも単体テストできるようにする。
2. **撮影と変換の規約は単一ソースにする。** 6面の(視線, 上, 右)基底は
   `CubeOrientation` だけが定義し、アドインのカメラ制御とCoreのサンプリングの両方が参照する。
   継ぎ目ズレの大半は「撮影側と合成側の規約不一致」から生じるため。
3. **Navisworks API依存は薄いラッパに隔離する。** バージョン差異・API変更の影響を
   `NavisCamera` / `ViewExporter` に閉じ込める。

## 座標系規約

### パノラマ基準フレーム

- 正規基底: `right = +X`, `forward = +Y`, `up = +Z`（右手系）
- 実際の撮影ではモデルのUp（既定 `(0,0,1)`）と、撮影時の基準方位 `forward₀`
  （カメラの現在の向きを水平化したもの）から回転行列 `R = [right₀ forward₀ up₀]` を作り、
  正規基底の各面ベクトルをワールドへ写像する。
- `metadata.json` に `forward₀` / `up₀` を記録するため、後からモデル座標へ復元可能。

### 経度・緯度と正距円筒画像

出力画像は幅W:高さH = 2:1。画素(i, j)（左上原点）に対して:

```text
lon = (i + 0.5) / W * 2π − π      … −π（後方）〜 0（前方 forward）〜 +π
lat = π/2 − (j + 0.5) / H * π     … +π/2（真上）〜 −π/2（真下）
方向 d = cos(lat)·sin(lon)·right + cos(lat)·cos(lon)·forward + sin(lat)·up
```

つまり **画像中央が forward（lon=0, lat=0）**、右へ行くほど東回り（rightの方向）。

### 6面の基底（CubeOrientation）

各面は「視線 f・画像上向き u・画像右向き r = f × u」で定義する。OpenGLキューブマップと
同型の規約（真上面の画像上端は後方、真下面の画像上端は前方）。

| 面 | 視線 f | 画像上 u | 画像右 r = f × u |
|---|---|---|---|
| Front | ( 0, 1, 0) | (0, 0, 1) | ( 1, 0, 0) |
| Right | ( 1, 0, 0) | (0, 0, 1) | ( 0,−1, 0) |
| Back  | ( 0,−1, 0) | (0, 0, 1) | (−1, 0, 0) |
| Left  | (−1, 0, 0) | (0, 0, 1) | ( 0, 1, 0) |
| Up    | ( 0, 0, 1) | (0,−1, 0) | ( 1, 0, 0) |
| Down  | ( 0, 0,−1) | (0, 1, 0) | ( 1, 0, 0) |

方向 d から面を選ぶときは |d·軸| が最大の軸を採用し、面内座標は

```text
t = d / (d·f)        … 面平面（f方向距離1）への射影
uv = (t·r, t·u)      … 各成分は [−1, +1]
画素 x = (u+1)/2·S − 0.5,  y = (1−v)/2·S − 0.5   （S=面の一辺、バイリニア＋端クランプ）
```

### 面画角のパラメータ化

既定は各面90°だが、`CaptureOptions.FaceFovDegrees` で90°超のオーバーラップ撮影に対応できる
（画角誤差・継ぎ目対策の保険）。その場合のサンプリングは `tan(fov/2)` で正規化する。

## 撮影シーケンス（PanoramaCaptureService）

```text
1. 現在のビューポイントを CreateCopy() で退避
2. 撮影位置を決定（現在視点 / 選択部材のBBox中心＋目線高さ / 座標入力）
3. 基準方位 forward₀ を現在カメラ向きから水平化して算出
4. for face in [Front, Right, Back, Left, Up, Down]:
     a. f_world = R·f_face, u_world = R·u_face
     b. Viewpoint: Position固定, AlignDirection(f_world) → AlignUp(u_world),
        Projection=Perspective, HeightField=π/2（垂直90°）
     c. ViewExporter で正方形PNGを出力（faces/face_front.png 等）
     d. PNGを PixelBuffer へロード
5. EquirectangularConverter.Convert() で 2:1 パノラマ生成
6. JPEG保存（panorama.jpg / thumbnail.jpg）＋ metadata.json 出力
7. 退避したビューポイントへ CopyFrom() で復帰
```

カメラ位置は全面で完全に同一（`Position` を再設定するだけで移動しない）。
向きのみを変えることが継ぎ目品質の前提条件。

## Navisworks API 使用箇所と検証状況

Linux上ではNavisworks SDKに対してコンパイルできないため、確度を明示する。
「要検証」はPhase 1の実機ビルドで確認し、この表を更新すること。

| API | 用途 | 確度 |
|---|---|---|
| `AddInPlugin` + `[Plugin]` + `[AddInPlugin(AddInLocation.AddIn)]` | リボン「ツール アドイン」への登録 | 高 |
| `Application.ActiveDocument` / `CurrentViewpoint.CreateCopy()/CopyFrom()` | 視点の取得・退避・復帰 | 高 |
| `Viewpoint.Position / Projection / PointAt / AlignDirection / AlignUp` | カメラ位置・向き制御 | 高（上下面での挙動は要検証） |
| `Viewpoint.HeightField`（垂直画角・ラジアン） | 90°画角設定 | 中（要検証: 垂直画角の定義） |
| `Viewpoint.AspectRatio = 1.0` | 正方形画角の明示 | 中（要検証: プロパティ有無。無ければ削除可） |
| `ComApiBridge.State` → `GetIOPluginOptions("lcodpimage")` → `DriveIOPlugin` | ビュー画像出力 | 中〜高（ADN公式サンプル準拠。オプション名は初回実行時に診断ログへ全出力） |
| `Document.Units` | 目線高さの単位表示 | 高 |
| `ModelItem.BoundingBox()` | 選択部材からの撮影位置算出 | 高 |

`ViewExporter` は初回実行時に `lcodpimage` の全オプション名・現在値を
`diagnostics.log` に書き出す。実機での正確なオプション名確認（AA・レンダラ指定等）に使う。

## metadata.json 仕様（v1）

```json
{
  "schema": "panoramaNavis/1",
  "panorama_id": "Panorama_20260724_143000",
  "created_at": "2026-07-24T14:30:00+09:00",
  "created_by": "user name",
  "source_document": "C:\\models\\plant.nwd",
  "model_units": "Millimeters",
  "position": { "x": 12500.0, "y": 8200.0, "z": 1600.0 },
  "forward": { "x": 0.0, "y": 1.0, "z": 0.0 },
  "up": { "x": 0.0, "y": 0.0, "z": 1.0 },
  "eye_height": 1600.0,
  "position_source": "CurrentViewpoint",
  "face_size": 2048,
  "face_fov_degrees": 90.0,
  "output_width": 8192,
  "projection": "equirectangular"
}
```

Phase 3で `project_id` / `model_version` / `equipment_ids` / `issue_ids` を追加予定
（スキーマは `schema` フィールドで版管理）。

## ビルド・配置

- `Directory.Build.props` の `NavisworksApiDir`（環境変数 `NAVISWORKS_API_DIR` で上書き可）
  がNavisworks DLLの参照先。DLLはリポジトリに含めない（再配布不可のため）。
- Navisworks参照は `Private=false`（CopyLocalしない）。実行時はNavisworks本体が解決する。
- 配置先は `%APPDATA%\Autodesk Navisworks Manage <ver>\Plugins\PanoramaNavis.Addin\`。
  **フォルダ名はアセンブリ名と一致が必須**（Navisworksのプラグイン検出規約）。

## 既知の設計上の割り切り（PoC）

- 6面キャプチャはUIスレッドで同期実行（Navisworks APIはUIスレッド前提のため）。
  進捗はダイアログのラベル更新のみ。
- 継ぎ目は端クランプのバイリニア補間。面境界1px程度のにじみ対策
  （隣接面ブレンド）はPhase 2で必要なら導入。
- サムネイルは正距円筒の縮小版（1024×512）。
