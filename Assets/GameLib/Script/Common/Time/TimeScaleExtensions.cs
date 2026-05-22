using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using DG.Tweening;
using Game; // IScopeIdentityService

namespace Game.Times
{
    // ================================================================
    // TimeScaleExtensions - LTS ベ�Eスの TimeScale 拡張
    // ================================================================
    //
    // ## 概要E
    //
    // LTS の TimeScaleBehavior に基づぁE�� DOTween, UniTask の
    // Scaled/Unscaled を設定する拡張メソチE��、E
    //
    // ## 使用侁E
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
        /// LTS の TimeScaleBehavior に基づぁE�� SetUpdate を設定、E
        /// </summary>
        public static T SetUpdateFromLTS<T>(this T tween, IScopeIdentityService identity)
            where T : Tween
        {
            if (tween == null) return tween;

            bool unscaled = identity?.TimeScaleBehavior == TimeScaleBehavior.Unscaled;
            return tween.SetUpdate(isIndependentUpdate: unscaled);
        }

        /// <summary>
        /// TimeScaleBehavior に基づぁE�� SetUpdate を設定、E
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
        /// LTS の TimeScaleBehavior に基づぁE��遁E��、E
        /// </summary>
        public static UniTask DelayAsync(
            this IScopeIdentityService identity,
            float seconds,
            CancellationToken ct = default)
        {
            var delayType = identity?.TimeScaleBehavior == TimeScaleBehavior.Unscaled
                ? DelayType.UnscaledDeltaTime
                : DelayType.DeltaTime;

            return UniTask.Delay(TimeSpan.FromSeconds(seconds), delayType, cancellationToken: ct);
        }

        /// <summary>
        /// TimeScaleBehavior に基づぁE��遁E��、E
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
        /// LTS の TimeScaleBehavior に基づぁE��遁E���E�ミリ秒）、E
        /// </summary>
        public static UniTask DelayMillisecondsAsync(
            this IScopeIdentityService identity,
            int milliseconds,
            CancellationToken ct = default)
        {
            var delayType = identity?.TimeScaleBehavior == TimeScaleBehavior.Unscaled
                ? DelayType.UnscaledDeltaTime
                : DelayType.DeltaTime;

            return UniTask.Delay(milliseconds, delayType, cancellationToken: ct);
        }

        /// <summary>
        /// LTS の TimeScaleBehavior に対応すめEDelayType を取得、E
        /// </summary>
        public static DelayType ToDelayType(this TimeScaleBehavior behavior)
        {
            return behavior == TimeScaleBehavior.Unscaled
                ? DelayType.UnscaledDeltaTime
                : DelayType.DeltaTime;
        }

        /// <summary>
        /// LTS の TimeScaleBehavior に対応すめEUpdateType を取得、E
        /// </summary>
        public static bool IsIndependentUpdate(this TimeScaleBehavior behavior)
        {
            return behavior == TimeScaleBehavior.Unscaled;
        }

        // ================================================================
        // IRuntimeResolver Extensions
        // ================================================================

        /// <summary>
        /// Container から IScopeIdentityService を取得し、TimeScaleBehavior を返す、E
        /// 見つからなぁE��合�E Scaled をデフォルトとする、E
        /// </summary>
        public static TimeScaleBehavior GetTimeScaleBehavior(this IRuntimeResolver resolver)
        {
            if (resolver == null) return TimeScaleBehavior.Scaled;

            if (resolver.TryResolve<IScopeIdentityService>(out var identity) && identity != null)
            {
                return identity.TimeScaleBehavior;
            }

            return TimeScaleBehavior.Scaled;
        }
    }
}

