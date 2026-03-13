using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using DG.Tweening;
using Game; // ILTSIdentityService

namespace Game.Times
{
    // ================================================================
    // TimeScaleExtensions - LTS ベースの TimeScale 拡張
    // ================================================================
    //
    // ## 概要
    //
    // LTS の TimeScaleBehavior に基づいて DOTween, UniTask の
    // Scaled/Unscaled を設定する拡張メソッド。
    //
    // ## 使用例
    //
    // ```csharp
    // // DOTween
    // transform.DOMove(target, 1f).SetUpdateFromLTS(identity);
    //
    // // UniTask
    // await identity.DelayAsync(1f, ct);
    // ```
    //
    // ================================================================

    public static class TimeScaleExtensions
    {
        // ================================================================
        // DOTween Extensions
        // ================================================================

        /// <summary>
        /// LTS の TimeScaleBehavior に基づいて SetUpdate を設定。
        /// </summary>
        public static T SetUpdateFromLTS<T>(this T tween, ILTSIdentityService identity)
            where T : Tween
        {
            if (tween == null) return tween;

            bool unscaled = identity?.TimeScaleBehavior == TimeScaleBehavior.Unscaled;
            return tween.SetUpdate(isIndependentUpdate: unscaled);
        }

        /// <summary>
        /// TimeScaleBehavior に基づいて SetUpdate を設定。
        /// </summary>
        public static T SetUpdateFromBehavior<T>(this T tween, TimeScaleBehavior behavior)
            where T : Tween
        {
            if (tween == null) return tween;

            bool unscaled = behavior == TimeScaleBehavior.Unscaled;
            return tween.SetUpdate(isIndependentUpdate: unscaled);
        }

        // ================================================================
        // UniTask Extensions
        // ================================================================

        /// <summary>
        /// LTS の TimeScaleBehavior に基づいた遅延。
        /// </summary>
        public static UniTask DelayAsync(
            this ILTSIdentityService identity,
            float seconds,
            CancellationToken ct = default)
        {
            var delayType = identity?.TimeScaleBehavior == TimeScaleBehavior.Unscaled
                ? DelayType.UnscaledDeltaTime
                : DelayType.DeltaTime;

            return UniTask.Delay(TimeSpan.FromSeconds(seconds), delayType, cancellationToken: ct);
        }

        /// <summary>
        /// TimeScaleBehavior に基づいた遅延。
        /// </summary>
        public static UniTask DelayAsync(
            TimeScaleBehavior behavior,
            float seconds,
            CancellationToken ct = default)
        {
            var delayType = behavior == TimeScaleBehavior.Unscaled
                ? DelayType.UnscaledDeltaTime
                : DelayType.DeltaTime;

            return UniTask.Delay(TimeSpan.FromSeconds(seconds), delayType, cancellationToken: ct);
        }

        /// <summary>
        /// LTS の TimeScaleBehavior に基づいた遅延（ミリ秒）。
        /// </summary>
        public static UniTask DelayMillisecondsAsync(
            this ILTSIdentityService identity,
            int milliseconds,
            CancellationToken ct = default)
        {
            var delayType = identity?.TimeScaleBehavior == TimeScaleBehavior.Unscaled
                ? DelayType.UnscaledDeltaTime
                : DelayType.DeltaTime;

            return UniTask.Delay(milliseconds, delayType, cancellationToken: ct);
        }

        /// <summary>
        /// LTS の TimeScaleBehavior に対応する DelayType を取得。
        /// </summary>
        public static DelayType ToDelayType(this TimeScaleBehavior behavior)
        {
            return behavior == TimeScaleBehavior.Unscaled
                ? DelayType.UnscaledDeltaTime
                : DelayType.DeltaTime;
        }

        /// <summary>
        /// LTS の TimeScaleBehavior に対応する UpdateType を取得。
        /// </summary>
        public static bool IsIndependentUpdate(this TimeScaleBehavior behavior)
        {
            return behavior == TimeScaleBehavior.Unscaled;
        }

        // ================================================================
        // IObjectResolver Extensions
        // ================================================================

        /// <summary>
        /// Container から ILTSIdentityService を取得し、TimeScaleBehavior を返す。
        /// 見つからない場合は Scaled をデフォルトとする。
        /// </summary>
        public static TimeScaleBehavior GetTimeScaleBehavior(this IObjectResolver resolver)
        {
            if (resolver == null) return TimeScaleBehavior.Scaled;

            if (resolver.TryResolve<ILTSIdentityService>(out var identity) && identity != null)
            {
                return identity.TimeScaleBehavior;
            }

            return TimeScaleBehavior.Scaled;
        }
    }
}
