#nullable enable
// Game.Movement
// ================================================================================
// MovementTypes - InputMovement システムの基盤データ構造
// ================================================================================
//
// 【概要】
// InputMovementSystem で使用される構造体・ユーティリティを定義。
// すべて構造体ベースで GC 回避を徹底。
//
// 【含まれる型】
// - TargetSnapshot: ターゲット情報のスナップショット
// - MovementGuidanceFrame: パイプライン入力フレーム情報
// - MotionOutput: Motion の出力
// - MovementMath: 方向計算ユーティリティ
// ================================================================================

using System;
using UnityEngine;
using Game.Search;

namespace Game.Movement
{
    // ================================================================================
    // TargetSnapshot - ターゲット情報のスナップショット
    // ================================================================================

    /// <summary>
    /// ターゲット情報のスナップショット（構造体で GC 回避）。
    /// Homing/Motion で共通利用される。
    /// </summary>
    public readonly struct TargetSnapshot
    {
        /// <summary>有効なターゲットが存在するか</summary>
        public readonly bool HasTarget;

        /// <summary>ターゲットのワールド座標</summary>
        public readonly Vector2 TargetPosition;

        /// <summary>Owner のワールド座標</summary>
        public readonly Vector2 OwnerPosition;

        /// <summary>Owner → Target の距離</summary>
        public readonly float Distance;

        /// <summary>Owner → Target の方向（正規化）</summary>
        public readonly Vector2 TargetDirection;

        /// <summary>ターゲットの ScopeNode（任意）</summary>
        public readonly IScopeNode? TargetScope;

        /// <summary>ターゲットの Identity（任意）</summary>
        public readonly ILTSIdentityService? TargetIdentity;

        public TargetSnapshot(
            bool hasTarget,
            Vector2 targetPosition,
            Vector2 ownerPosition,
            float distance,
            Vector2 targetDirection,
            IScopeNode? targetScope = null,
            ILTSIdentityService? targetIdentity = null)
        {
            HasTarget = hasTarget;
            TargetPosition = targetPosition;
            OwnerPosition = ownerPosition;
            Distance = distance;
            TargetDirection = targetDirection;
            TargetScope = targetScope;
            TargetIdentity = targetIdentity;
        }

        /// <summary>無効なスナップショット</summary>
        public static TargetSnapshot Invalid => new(false, default, default, 0f, default);
    }

    // ================================================================================
    // MovementGuidanceFrame - パイプライン入力フレーム情報
    // ================================================================================

    /// <summary>
    /// Movement パイプラインのフレーム情報（構造体で GC 回避）。
    /// InputMovementService → IMotionMovement に渡される。
    /// </summary>
    public struct MovementGuidanceFrame
    {
        // ================================================================
        // Time
        // ================================================================

        /// <summary>現在のフレーム番号（Time.frameCount）</summary>
        public int FrameCount;

        /// <summary>デルタタイム</summary>
        public float DeltaTime;

        /// <summary>Motion 開始からの経過時間（Motion 用）</summary>
        public float TimeSinceStart;

        // ================================================================
        // Direction
        // ================================================================

        /// <summary>入力方向（正規化 or zero）</summary>
        public Vector2 BaseDirection;

        /// <summary>Homing 出力方向（正規化 or zero）</summary>
        public Vector2 GuidanceDirection;

        /// <summary>Motion 出力方向（正規化 or zero）</summary>
        public Vector2 MotionDirection;

        // ================================================================
        // Target
        // ================================================================

        /// <summary>ターゲット情報</summary>
        public TargetSnapshot Target;

        /// <summary>Homing が有効か（BoolLayer 結果）</summary>
        public bool HomingEnabled;

        // ================================================================
        // Speed
        // ================================================================

        /// <summary>Scalar から取得した基本速度</summary>
        public float SpeedBase;

        /// <summary>Motion の速度倍率</summary>
        public float SpeedMul;

        /// <summary>Motion の加算速度</summary>
        public Vector2 AdditiveVelocity;

        /// <summary>最終速度</summary>
        public Vector2 FinalVelocity;
    }

    // ================================================================================
    // MotionOutput - Motion の出力
    // ================================================================================

    /// <summary>
    /// Motion の出力（構造体で GC 回避）。
    /// </summary>
    public readonly struct MotionOutput
    {
        /// <summary>進行方向（正規化 or zero）</summary>
        public readonly Vector2 Direction;

        /// <summary>速度倍率（通常 0〜2 程度）</summary>
        public readonly float SpeedMul;

        /// <summary>速度加算（Vector2）</summary>
        public readonly Vector2 AdditiveVelocity;

        public MotionOutput(Vector2 direction, float speedMul, Vector2 additiveVelocity)
        {
            Direction = direction;
            SpeedMul = speedMul;
            AdditiveVelocity = additiveVelocity;
        }

        /// <summary>デフォルト出力（方向維持、倍率1、加算なし）</summary>
        public static MotionOutput Default(Vector2 direction) => new(direction, 1f, Vector2.zero);

        /// <summary>ゼロ出力</summary>
        public static MotionOutput Zero => new(Vector2.zero, 0f, Vector2.zero);
    }

    // ================================================================================
    // MovementMath - 方向計算ユーティリティ
    // ================================================================================

    /// <summary>
    /// Movement 計算用のユーティリティ関数群。
    /// NaN/ゼロベクトル対策を含む安全な実装。
    /// </summary>
    public static class MovementMath
    {
        /// <summary>正規化判定の閾値</summary>
        public const float NormalizeEpsilon = 0.000001f;

        /// <summary>
        /// 方向ベクトルを正規化する。長さが極小ならゼロを返す。
        /// </summary>
        /// <param name="v">入力ベクトル</param>
        /// <param name="epsilon">閾値</param>
        /// <returns>正規化済みベクトル or Vector2.zero</returns>
        public static Vector2 NormalizeDirection(Vector2 v, float epsilon = NormalizeEpsilon)
        {
            float sqrMag = v.sqrMagnitude;
            if (sqrMag < epsilon)
                return Vector2.zero;
            return v / Mathf.Sqrt(sqrMag);
        }

        /// <summary>
        /// 2D 角度補間。反対方向でもゼロにならない（角度ベースで回転）。
        /// </summary>
        /// <param name="from">開始方向（正規化済み推奨）</param>
        /// <param name="to">終了方向（正規化済み推奨）</param>
        /// <param name="t">補間係数 (0..1)</param>
        /// <returns>補間後の方向（正規化済み）</returns>
        public static Vector2 Slerp2D(Vector2 from, Vector2 to, float t)
        {
            // from または to がゼロの場合の特例処理
            float fromMag = from.magnitude;
            float toMag = to.magnitude;
            if (fromMag < 0.0001f) return NormalizeDirection(to);
            if (toMag < 0.0001f) return NormalizeDirection(from);

            // 正規化
            from /= fromMag;
            to /= toMag;

            // 角度を取得
            float fromAngle = Mathf.Atan2(from.y, from.x);
            float toAngle = Mathf.Atan2(to.y, to.x);

            // 最短経路で補間
            float delta = Mathf.DeltaAngle(fromAngle * Mathf.Rad2Deg, toAngle * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            float resultAngle = fromAngle + delta * Mathf.Clamp01(t);

            return new Vector2(Mathf.Cos(resultAngle), Mathf.Sin(resultAngle));
        }

        /// <summary>
        /// ターゲット方向を計算。同位置の場合はフォールバックを使用。
        /// </summary>
        /// <param name="ownerPos">Owner 位置</param>
        /// <param name="targetPos">Target 位置</param>
        /// <param name="fallback">フォールバック方向</param>
        /// <returns>正規化された方向ベクトル</returns>
        public static Vector2 ComputeTargetDirection(Vector2 ownerPos, Vector2 targetPos, Vector2 fallback)
        {
            var diff = targetPos - ownerPos;
            float sqrMag = diff.sqrMagnitude;

            // Owner と Target が同位置
            if (sqrMag < NormalizeEpsilon)
            {
                // フォールバック優先順位: 前回の TargetDirection > BaseDirection > Vector2.up
                if (fallback.sqrMagnitude > 0.0001f)
                    return NormalizeDirection(fallback);
                return Vector2.up;
            }

            return diff / Mathf.Sqrt(sqrMag);
        }

        /// <summary>
        /// 方向ベクトルに対して垂直な方向を取得（左90度回転）。
        /// </summary>
        /// <param name="direction">元の方向（正規化済み推奨）</param>
        /// <returns>垂直方向</returns>
        public static Vector2 GetPerpendicular(Vector2 direction)
        {
            return new Vector2(-direction.y, direction.x);
        }

        /// <summary>
        /// 方向ベクトルを指定角度だけ回転させる。
        /// </summary>
        /// <param name="direction">元の方向</param>
        /// <param name="angleDeg">回転角度（度数法）</param>
        /// <returns>回転後の方向</returns>
        public static Vector2 RotateDirection(Vector2 direction, float angleDeg)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return new Vector2(
                direction.x * cos - direction.y * sin,
                direction.x * sin + direction.y * cos
            );
        }
    }
}
