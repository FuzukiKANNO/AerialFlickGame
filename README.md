# AerialFlickGame — 予測型当たり判定ゲーム

OptiTrack で追跡した物体（円柱 / 直方体）と、左→右へ飛来する円との**予測衝突判定**を実装した
Unity プロジェクト。物理的に接触する `DetectionLeadTime` 秒前に当たり判定を発火する。

- Unity バージョン: **6000.3.10f1**（`finger_from_side` と同一）
- レンダーパイプライン: URP
- 入力: Input System（New）— OptiTrack 未接続時は**マウス操作にフォールバック**

## セットアップ

1. Unity Hub の「開く」→ `C:\Users\fuzuk\Unity\Projects\AerialFlickGame` を追加して開く。
   - OptiTrack SDK（NatNet ネイティブ DLL 含む）は `finger_from_side` からコピー済み。
2. 初回インポート後、メニュー **`AerialFlickGame > Build Game Scene`** を実行。
   - 配線済みのシーン `Assets/Scenes/AerialFlickGame.unity` が自動生成・保存される。
   - カメラ・ライト・Manager・TrackedObject・Spawner・UI・円プレハブが全て作られ、参照も接続済み。
3. そのシーンを開いて **Play**。

## トラッキング無し（マウス）でのテスト

`TrackedObject` の各コンポーネント（`CylinderTracked` / `BoxTracked`）に:

- `UseMouseWhenUntracked = true`（既定）… OptiTrack が取れないときマウスで動かす
- `ForceMouseControl = true` … OptiTrack を無視して常にマウス操作

マウスは `InputCamera`（既定でメインカメラ）を通して XY 平面（Z = `PlaneZ`）へ投影される。
マウスを動かすと速度も推定されるので、`Velocity` を使った予測判定もそのまま検証できる。

## OptiTrack 接続時

1. シーンに `Client - OptiTrack` プレハブ（`Assets/OptiTrack/Prefabs`）を配置。
2. `TrackedObject` の `StreamingClient` にそのクライアントを、`RigidBodyId` に Motive 上の ID を設定。
3. `ForceMouseControl` を false のままにすれば、トラッキングが得られる間は OptiTrack 位置を使用する。

## 円柱 / 直方体の切り替え

`TrackedObject` には `CylinderTracked`（既定 enabled）と `BoxTracked`（既定 disabled）の両方が乗っている。

- 直方体を使う場合: `BoxTracked` を有効化、`CylinderTracked` を無効化し、
  `Manager > PredictiveHitDetector > TrackedObject` の参照を `BoxTracked` に差し替える
  （`PredictionGizmo > TrackedObject` も同様）。

## スクリプト構成

```
Assets/Scripts/AerialFlickGame/
  Core/
    VelocityEstimator.cs      最小二乗フィットで XY 速度を推定
    HitPrediction.cs          ヒット発火時の情報 struct
    PredictiveHitDetector.cs  毎フレーム予測 → LeadTime 以内で発火
  TrackedObjects/
    TrackedObjectBase.cs      位置取得 + 速度推定 + マウスフォールバック
    CylinderTracked.cs        円 vs 円（解析解）
    BoxTracked.cs             矩形 vs 円（数値ステッピング）
  Circle/
    FlyingCircle.cs           飛来円（Flying / Hit / Missed）
    CircleSpawner.cs          一定間隔でスポーン
  Game/
    GameManager.cs            スコア・残機・R キーでリスタート
  Debug/
    PredictionGizmo.cs        予測軌跡・衝突点を Scene ビューに描画
  Editor/
    SceneBuilder.cs           シーン自動生成メニュー
```

## 検出モード（PredictiveHitDetector.Mode）

設計は菅野ら「空中像を手指で弾く際の当たり判定距離と接近速度に関する評価」(VRSJ2026) に基づく。
論文の当たり判定距離 = `17.0 × 速度 + 2.8 [cm]`（上限）で、傾きの時間換算 170ms は
「円が動き出してから指を止めるまでの反応時間」。上限・下限の傾き平均から **89ms** を採用。

- **CompensatedContact（既定・論文モデル）**
  追跡物体（指）だけを LeadTime 分だけ先読みし、円は実位置で判定。
  有効当たり判定距離 = `円半径 + CollisionMargin + LeadTime × 指の接近速度`。
  指が静止していれば実接触の瞬間に発火し、**円の速度では早出ししない**（論文どおり）。
- **Predictive（比較用）**
  相対的な衝突時刻 T_col を予測し、その LeadTime 前に発火。
  円の速度でも早出しするため、指が静止していても手前で跳ね返る。論文モデルとは異なる。
- **PhysicalContact**
  予測も補償もせず、実際に接触した瞬間に発火。

`CollisionMargin` は論文の定数オフセット（下限 ≈ 1cm）に対応。必要なら 0.01 前後を設定する。

## 奥行き(Z)・高さ(Y)の許容範囲（PredictiveHitDetector）

finger_from_side の `SphereInteraction` に準拠。追跡物体が結像面から離れすぎている場合は
発火させない「センシング範囲ゲート」。全モード共通で発火前にチェックする。
Z は `transform` ではなく `TrackedObject.Position.z`（実測値）で判定する。

| 変数 | 既定 | 意味 |
|---|---|---|
| `UseDepthGate` | true | 奥行きゲートの有効/無効 |
| `ImagePlaneZ` | 0 | 結像面の基準 Z |
| `ZToleranceFront` | 0.015 m | 基準面より +Z 側にどれだけ離れても反応するか |
| `ZToleranceBack` | 0.010 m | 基準面より -Z 側にどれだけ離れても反応するか |
| `UseHeightGate` | false | 高さゲートの有効/無効 |
| `HeightTolerance` | 0.02 m | 指と円中心の Y 差の許容 |

- **注意（Z の向き）**: +Z / -Z のどちらが「手前(カメラ寄り)」かは OptiTrack のキャリブレーションと
  カメラ配置に依存する。本シーンのカメラは z=-1 で +Z を向くため、手前は -Z 側になりうる。
  実機で確認し、必要なら Front/Back の値を入れ替える。
- マウス操作時は Z を制御できず `Position.z = PlaneZ` 固定なので、`ImagePlaneZ = PlaneZ` なら常に通過する。
- 高さゲートは XY 平面判定（`ComputeDistanceTo`）に対する**追加**の制約。XY 距離自体にも Y は
  含まれるため、既定は off。純粋に水平距離だけで判定したい場合は別途相談。

## パラメータ既定値

| パラメータ | 場所 | 既定値 | 単位 |
|---|---|---|---|
| DetectionLeadTime | PredictiveHitDetector | 0.089 | s |
| CollisionMargin | PredictiveHitDetector | 0.000 | m |
| CircleRadius | PredictiveHitDetector | 0.010 | m |
| MaxLookAhead | PredictiveHitDetector | 0.500 | s |
| VelocityFrames | TrackedObjectBase | 5 | frames |
| CircleSpeed | CircleSpawner | 0.300 | m/s |
| SpawnInterval | CircleSpawner | 2.000 | s |
| CylinderRadius | CylinderTracked | 0.030 | m |
| BoxWidth / BoxHeight | BoxTracked | 0.060 / 0.040 | m |
| StepSize | BoxTracked | 0.002 | s |

## 注意

- 衝突判定は **XY 平面（結像面）のみ**。Z 軸は使わない。
- `FindTimeToCollision` は現在フレーム情報のみで完結する純粋関数（副作用なし）。
- HIT 時は Console に `[HIT] leadTime=... | v_tracked=... | v_rel=... | margin=...` を出力。
