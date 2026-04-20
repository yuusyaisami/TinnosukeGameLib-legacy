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
    // TimeScaleExtensions - LTS 繝吶・繧ｹ縺ｮ TimeScale 諡｡蠑ｵ
    // ================================================================
    //
    // ## 讎りｦ・
    //
    // LTS 縺ｮ TimeScaleBehavior 縺ｫ蝓ｺ縺･縺・※ DOTween, UniTask 縺ｮ
    // Scaled/Unscaled 繧定ｨｭ螳壹☆繧区僑蠑ｵ繝｡繧ｽ繝・ラ縲・
    //
    // ## 菴ｿ逕ｨ萓・
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
        /// LTS 縺ｮ TimeScaleBehavior 縺ｫ蝓ｺ縺･縺・※ SetUpdate 繧定ｨｭ螳壹・
        /// </summary>
        public static T SetUpdateFromLTS<T>(this T tween, ILTSIdentityService identity)
            where T : Tween
        {
            if (tween == null) return tween;

            bool unscaled = identity?.TimeScaleBehavior == TimeScaleBehavior.Unscaled;
            return tween.SetUpdate(isIndependentUpdate: unscaled);
        }

        /// <summary>
        /// TimeScaleBehavior 縺ｫ蝓ｺ縺･縺・※ SetUpdate 繧定ｨｭ螳壹・
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
        /// LTS 縺ｮ TimeScaleBehavior 縺ｫ蝓ｺ縺･縺・◆驕・ｻｶ縲・
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
        /// TimeScaleBehavior 縺ｫ蝓ｺ縺･縺・◆驕・ｻｶ縲・
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
        /// LTS 縺ｮ TimeScaleBehavior 縺ｫ蝓ｺ縺･縺・◆驕・ｻｶ・医Α繝ｪ遘抵ｼ峨・
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
        /// LTS 縺ｮ TimeScaleBehavior 縺ｫ蟇ｾ蠢懊☆繧・DelayType 繧貞叙蠕励・
        /// </summary>
        public static DelayType ToDelayType(this TimeScaleBehavior behavior)
        {
            return behavior == TimeScaleBehavior.Unscaled
                ? DelayType.UnscaledDeltaTime
                : DelayType.DeltaTime;
        }

        /// <summary>
        /// LTS 縺ｮ TimeScaleBehavior 縺ｫ蟇ｾ蠢懊☆繧・UpdateType 繧貞叙蠕励・
        /// </summary>
        public static bool IsIndependentUpdate(this TimeScaleBehavior behavior)
        {
            return behavior == TimeScaleBehavior.Unscaled;
        }

        // ================================================================
        // IRuntimeResolver Extensions
        // ================================================================

        /// <summary>
        /// Container 縺九ｉ ILTSIdentityService 繧貞叙蠕励＠縲ゝimeScaleBehavior 繧定ｿ斐☆縲・
        /// 隕九▽縺九ｉ縺ｪ縺・ｴ蜷医・ Scaled 繧偵ョ繝輔か繝ｫ繝医→縺吶ｋ縲・
        /// </summary>
        public static TimeScaleBehavior GetTimeScaleBehavior(this IRuntimeResolver resolver)
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
