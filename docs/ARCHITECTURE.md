# 技術設計 — PanoramaNavis

## コンポーネント構成

```text
┌─────────────────────────────────────────────────────┐
│ PanoramaNavis.Addin  (.NET Framework 4.8 / Windows)  │
│                                                      │
│ [v1: 1地点]                                          │
│  CapturePanoramaAddin   … AddInPlugin エントリ        │
│  CaptureDialog          … WinForms 設定ダイアログ      │
│  PanoramaCaptureService … 撮影オーケストレーション      │
│                           （RunAt: 任意地点・任意ID）   │
│ [v2: 平面図パノラマツアー]                              │
│  TourPanoramaAddin      … AddInPlugin エントリ        │
│  TourSetupDialog        … 断面・撮影設定ダイアログ      │
│  PlanPointPickerDialog  … 平面図クリックで地点指定      │
│  ProgressForm           … 一括撮影の進捗・中断          │
│  TourCaptureService     … ツアー全体のオーケストレーション│
│  PlanCaptureService     … 正射投影の平面図撮影          │
│  SectionClipper         … COM APIの断面クリップ（厚み） │
│ [共通]                                               │
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
│  PlanMapping            … 平面図画像⇔ワールドXYの変換   │
│  PanoramaMetadata / TourMetadata / JsonLite … JSON出力 │
│  CaptureOptions / TourCaptureOptions … 設定値          │
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

## 平面図の規約（v2）

平面図は**真上（-Z視線）・北上固定の正射投影**で撮影する（「決められた方向」）。

- 画像上 = +Y（北）、画像右 = +X（東）。パノラマの Down 面（視線(0,0,-1)・画像上(0,1,0)）と
  同一の向きなので、平面図とパノラマの方位は一致する（ビューアの視線扇形もこの前提）。
- 撮影範囲 = 全モデルのバウンディングボックスXY ＋ 余白（既定3%）。
  この範囲と画像サイズの対応は `PlanMapping` が唯一のソースで、tour.json にも記録される。
- ピクセル⇔ワールドはアフィン変換（左上原点・連続ピクセル座標）:

```text
worldX = world_min_x + pixelX / image_width  × (world_max_x − world_min_x)
worldY = world_max_y − pixelY / image_height × (world_max_y − world_min_y)
```

- **厚み（断面）**: 床レベルZ〜床レベルZ＋厚みの範囲だけが平面図に写るよう、
  COM APIのクリップ平面を2枚（上端・下端）一時追加する（`SectionClipper`）。
  撮影後は必ず取り除く。COM APIが使えない場合は断面なしで続行し、
  `tour.json` の `section_clip_applied` に false を記録する。
- 正射投影のビュー縦寸は `Viewpoint.HeightField`（モデル単位）で指定し、
  横は出力画像のアスペクト（＝範囲のアスペクト）に追従させる（要実機検証）。
- パノラマのカメラ高さは全地点共通で `床レベルZ ＋ 目線高さ`。

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

## ツアーシーケンス（TourCaptureService / v2）

```text
1. 床レベルZを決定（現在視点−目線高さ / 選択部材の底面 / 直接入力）
2. PlanCaptureService:
     a. 全モデルのBBox合併＋余白 → PlanMapping（画像サイズ・座標対応）
     b. SectionClipper で [床Z, 床Z+厚み] 外を一時クリップ
     c. 正射投影・真下視線・Up=+Y のビューポイントで plan.png を出力
     d. クリップ・ビューポイントを復元
3. PlanPointPickerDialog: 平面図クリックで地点追加（P01..Pnn、名前編集・削除可）
4. 各地点 Pxx について（進捗表示・中断可）:
     PanoramaCaptureService.RunAt(位置=(x, y, 床Z+目線高さ), ID=Pxx)
     → Tour_xxx/Pxx/panorama.jpg ＋ thumbnail.jpg ＋ metadata.json
5. tour.json を出力（平面図の座標対応＋全地点のピクセル/ワールド座標＋相対パス）
```

中断時も撮影済み地点だけで有効な tour.json を出力する。

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
| `ViewpointProjection.Orthographic` | 平面図の正射投影 | 高 |
| `Viewpoint.HeightField`（正射投影時＝ビュー縦の実寸） | 平面図の表示範囲指定 | 中（要検証: 正射投影での単位・意味） |
| `Document.Models` / `Model.RootItem` | 平面図範囲＝全モデルBBoxの合併 | 高 |
| COM `state.CurrentView.ClippingPlanes()` → `CreatePlane` / `RemovePlane` / `Plane` | 断面クリップ（厚み） | 低〜中（**dynamic遅延バインドで呼び、失敗時は断面なしで続行**。実機でメンバ名・平面式の符号を確認し、この行を更新すること。手順は SectionClipper のコメント参照） |
| COM `state.ObjectFactory(eObjectType_nwLPlane3f)` → `SetValue(nx,ny,nz,d)` | クリップ平面の定義 | 低〜中（同上。`n·p + d > 0` 側が非表示と仮定） |

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

## tour.json 仕様（v2 / panoramaNavis/tour/1）

ツアーフォルダ直下に出力される。パスはすべてツアーフォルダからの相対（"/"区切り）。

```json
{
  "schema": "panoramaNavis/tour/1",
  "tour_id": "Tour_20260730_120000",
  "created_at": "2026-07-30T12:00:00+09:00",
  "created_by": "user name",
  "source_document": "C:\\models\\plant.nwd",
  "model_units": "Millimeters",
  "eye_height": 1600.0,
  "plan": {
    "image": "plan.png",
    "image_width": 2048,
    "image_height": 1536,
    "world_min_x": -600.0,
    "world_min_y": -600.0,
    "world_max_x": 20600.0,
    "world_max_y": 15600.0,
    "view_direction": "-Z",
    "image_up_axis": "+Y",
    "image_right_axis": "+X",
    "floor_z": 100.0,
    "slab_thickness": 3000.0,
    "cut_top_z": 3100.0,
    "section_clip_applied": true
  },
  "points": [
    {
      "id": "P01",
      "name": "機械室",
      "pixel_x": 512.5,
      "pixel_y": 300.25,
      "position": { "x": 5000.0, "y": 12000.0, "z": 1700.0 },
      "panorama": "P01/panorama.jpg",
      "thumbnail": "P01/thumbnail.jpg"
    }
  ]
}
```

- `pixel_x/y` は平面図画像上のクリック位置（左上原点・連続ピクセル）。
- `position` はパノラマのカメラ位置（`z = floor_z + eye_height`）。
- ピクセル⇔ワールドの変換式は「平面図の規約」を参照
  （`PlanMapping` と `viewer/tour-viewer.html` が同一規約を実装）。

## ビルド・配置

- `Directory.Build.props` の `NavisworksApiDir`（環境変数 `NAVISWORKS_API_DIR` で上書き可）
  がNavisworks DLLの参照先。DLLはリポジトリに含めない（再配布不可のため）。
- Navisworks参照は `Private=false`（CopyLocalしない）。実行時はNavisworks本体が解決する。
- 配置先は `%APPDATA%\Autodesk Navisworks Simulate <ver>\Plugins\PanoramaNavis.Addin\`
  （Manageの場合は `Autodesk Navisworks Manage <ver>`）。
  **フォルダ名はアセンブリ名と一致が必須**（Navisworksのプラグイン検出規約）。

## 既知の設計上の割り切り（PoC）

- 6面キャプチャはUIスレッドで同期実行（Navisworks APIはUIスレッド前提のため）。
  進捗はダイアログのラベル更新のみ（v2の一括撮影は進捗フォーム＋地点間での中断に対応）。
- 継ぎ目は端クランプのバイリニア補間。面境界1px程度のにじみ対策
  （隣接面ブレンド）はPhase 2で必要なら導入。
- サムネイルは正距円筒の縮小版（1024×512）。
- v2の断面クリップはCOM APIの遅延バインド呼び出し。失敗時は断面なしの平面図で
  続行できる（`section_clip_applied: false` を記録）。
- 平面図のピクセル⇔ワールド対応は「HeightField＝ビュー縦の実寸」「横は画像アスペクト追従」
  の仮定に基づく。実機検証（既知寸法モデルの角をクリックして座標照合）で確認する。
