using System;
using Game.Common;
using Game.Health;
using Game.Vars.Generated;
using UnityEngine;

namespace Game.StatusEffect
{
    public readonly struct EffectState
    {
        public readonly string EffectId;
        public readonly string InstanceId;
        public readonly string RuntimeTag;
        public readonly string DisplayName;
        public readonly string NameKey;
        public readonly string DescriptionKey;
        public readonly Sprite Icon;
        public readonly EffectType Type;
        public readonly int SortOrder;
        public readonly float RemainingTime;
        public readonly float TotalDuration;
        public readonly float RemainingUseCooldown;
        public readonly float IntensityA;
        public readonly float IntensityB;
        public readonly float IntensityC;
        public readonly float IntensityD;
        public readonly float IntensityE;
        public readonly float IntensityF;
        public readonly float IntensityG;
        public readonly int StackCount;
        public readonly bool IsEnabled;
        public readonly bool IsApplied;
        public readonly bool IsActive;
        public readonly bool IsUseBlocked;
        public readonly bool UsesServiceGlobalLifetime;
        public readonly bool UsesServiceGlobalUseCooldown;
        public readonly bool UsesServiceGlobalCount;
        public readonly bool UsesAnyServiceGlobalUseState;
        public readonly int UsedCount;
        public readonly int RemainingUseCount;
        public readonly int MaxUseCount;

        public EffectState(
            string effectId,
            string instanceId,
            string runtimeTag,
            string displayName,
            string nameKey,
            string descriptionKey,
            Sprite icon,
            EffectType type,
            float remainingTime,
            float totalDuration,
            float remainingUseCooldown,
            float intensityA,
            float intensityB,
            float intensityC,
            float intensityD,
            float intensityE,
            float intensityF,
            float intensityG,
            int stackCount,
            bool isEnabled,
            bool isApplied,
            bool isActive,
            bool isUseBlocked,
            bool usesServiceGlobalLifetime,
            bool usesServiceGlobalUseCooldown,
            bool usesServiceGlobalCount,
            bool usesAnyServiceGlobalUseState,
            int usedCount,
            int remainingUseCount,
            int maxUseCount,
            int sortOrder = 0)
        {
            EffectId = effectId;
            InstanceId = instanceId;
            RuntimeTag = runtimeTag;
            DisplayName = displayName;
            NameKey = nameKey;
            DescriptionKey = descriptionKey;
            Icon = icon;
            Type = type;
            SortOrder = sortOrder;
            RemainingTime = remainingTime;
            TotalDuration = totalDuration;
            RemainingUseCooldown = remainingUseCooldown;
            IntensityA = intensityA;
            IntensityB = intensityB;
            IntensityC = intensityC;
            IntensityD = intensityD;
            IntensityE = intensityE;
            IntensityF = intensityF;
            IntensityG = intensityG;
            StackCount = stackCount;
            IsEnabled = isEnabled;
            IsApplied = isApplied;
            IsActive = isActive;
            IsUseBlocked = isUseBlocked;
            UsesServiceGlobalLifetime = usesServiceGlobalLifetime;
            UsesServiceGlobalUseCooldown = usesServiceGlobalUseCooldown;
            UsesServiceGlobalCount = usesServiceGlobalCount;
            UsesAnyServiceGlobalUseState = usesAnyServiceGlobalUseState;
            UsedCount = usedCount;
            RemainingUseCount = remainingUseCount;
            MaxUseCount = maxUseCount;
        }

        public EffectState(
            string effectId,
            string instanceId,
            string displayName,
            string nameKey,
            string descriptionKey,
            Sprite icon,
            EffectType type,
            float remainingTime,
            float totalDuration,
            float intensityA,
            float intensityB,
            float intensityC,
            float intensityD,
            float intensityE,
            float intensityF,
            float intensityG,
            int stackCount,
            bool isEnabled,
            bool isApplied,
            bool isActive,
            bool usesServiceGlobalLifetime,
            bool usesServiceGlobalUseCooldown,
            bool usesServiceGlobalCount,
            bool usesAnyServiceGlobalUseState,
            int remainingUseCount,
            int maxUseCount,
            int sortOrder = 0)
            : this(
                effectId,
                instanceId,
                string.Empty,
                displayName,
                nameKey,
                descriptionKey,
                icon,
                type,
                remainingTime,
                totalDuration,
                0f,
                intensityA,
                intensityB,
                intensityC,
                intensityD,
                intensityE,
                intensityF,
                intensityG,
                stackCount,
                isEnabled,
                isApplied,
                isActive,
                false,
                usesServiceGlobalLifetime,
                usesServiceGlobalUseCooldown,
                usesServiceGlobalCount,
                usesAnyServiceGlobalUseState,
                0,
                remainingUseCount,
                maxUseCount,
                sortOrder)
        {
        }
    }

    [Serializable]
    public struct StatusEffectGlobalRuntimeState
    {
        public bool HasInitialized;
        public bool IsLifetimeEnabled;
        public float LifetimeRemaining;
        public float LifetimeTotal;
        public bool IsLifetimeExpired;
        public bool IsUseCooldownEnabled;
        public float UseCooldownRemaining;
        public float UseCooldownTotal;
        public bool IsUseCooldownActive;
        public bool IsCountEnabled;
        public int CurrentCount;
        public int MaxCount;
        public int UsedCount;
        public bool IsCountExhausted;
        public bool CanUse;
        public bool CanConsumeUse;
        public string StatusText;

        public StatusEffectGlobalRuntimeState(
            bool hasInitialized,
            bool isLifetimeEnabled,
            float lifetimeRemaining,
            float lifetimeTotal,
            bool isLifetimeExpired,
            bool isUseCooldownEnabled,
            float useCooldownRemaining,
            float useCooldownTotal,
            bool isUseCooldownActive,
            bool isCountEnabled,
            int currentCount,
            int maxCount,
            int usedCount,
            bool isCountExhausted,
            bool canUse,
            bool canConsumeUse)
        {
            HasInitialized = hasInitialized;
            IsLifetimeEnabled = isLifetimeEnabled;
            LifetimeRemaining = lifetimeRemaining;
            LifetimeTotal = lifetimeTotal;
            IsLifetimeExpired = isLifetimeExpired;
            IsUseCooldownEnabled = isUseCooldownEnabled;
            UseCooldownRemaining = useCooldownRemaining;
            UseCooldownTotal = useCooldownTotal;
            IsUseCooldownActive = isUseCooldownActive;
            IsCountEnabled = isCountEnabled;
            CurrentCount = currentCount;
            MaxCount = maxCount;
            UsedCount = usedCount;
            IsCountExhausted = isCountExhausted;
            CanUse = canUse;
            CanConsumeUse = canConsumeUse;
            StatusText = BuildStatusText(
                hasInitialized,
                isLifetimeEnabled,
                lifetimeRemaining,
                lifetimeTotal,
                isLifetimeExpired,
                isUseCooldownEnabled,
                useCooldownRemaining,
                useCooldownTotal,
                isUseCooldownActive,
                isCountEnabled,
                currentCount,
                maxCount,
                isCountExhausted,
                canUse);
        }

        public static StatusEffectGlobalRuntimeState CreateUnavailable(string statusText)
        {
            return new StatusEffectGlobalRuntimeState
            {
                HasInitialized = false,
                IsLifetimeEnabled = false,
                LifetimeRemaining = -1f,
                LifetimeTotal = -1f,
                IsLifetimeExpired = false,
                IsUseCooldownEnabled = false,
                UseCooldownRemaining = 0f,
                UseCooldownTotal = 0f,
                IsUseCooldownActive = false,
                IsCountEnabled = false,
                CurrentCount = -1,
                MaxCount = 0,
                UsedCount = 0,
                IsCountExhausted = false,
                CanUse = false,
                CanConsumeUse = false,
                StatusText = statusText ?? string.Empty,
            };
        }

        static string BuildStatusText(
            bool hasInitialized,
            bool isLifetimeEnabled,
            float lifetimeRemaining,
            float lifetimeTotal,
            bool isLifetimeExpired,
            bool isUseCooldownEnabled,
            float useCooldownRemaining,
            float useCooldownTotal,
            bool isUseCooldownActive,
            bool isCountEnabled,
            int currentCount,
            int maxCount,
            bool isCountExhausted,
            bool canUse)
        {
            if (!hasInitialized)
                return "Uninitialized";

            if (!canUse)
            {
                if (isLifetimeEnabled && lifetimeTotal >= 0f && lifetimeRemaining <= 0f)
                    return "Lifetime expired";

                if (isUseCooldownEnabled && isUseCooldownActive && useCooldownRemaining > 0f)
                    return "Use cooldown active";

                if (isCountEnabled && maxCount > 0 && currentCount <= 0)
                    return "Count exhausted";

                if (isLifetimeExpired)
                    return "Lifetime expired";

                if (isCountExhausted)
                    return "Count exhausted";

                return "Blocked";
            }

            if (!isLifetimeEnabled && !isUseCooldownEnabled && !isCountEnabled)
                return "Ready";

            return "Ready";
        }
    }

    internal static class StatusEffectGlobalRuntimeStateWriter
    {
        internal static void Write(IVarStore vars, in StatusEffectGlobalRuntimeState state)
        {
            if (vars == null)
                return;

            vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Global.hasInitialized, DynamicVariant.FromBool(state.HasInitialized));
            vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Global.isLifetimeEnabled, DynamicVariant.FromBool(state.IsLifetimeEnabled));
            vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Global.lifetimeRemaining, DynamicVariant.FromFloat(state.LifetimeRemaining));
            vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Global.lifetimeTotal, DynamicVariant.FromFloat(state.LifetimeTotal));
            vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Global.isLifetimeExpired, DynamicVariant.FromBool(state.IsLifetimeExpired));
            vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Global.isUseCooldownEnabled, DynamicVariant.FromBool(state.IsUseCooldownEnabled));
            vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Global.cooldownRemaining, DynamicVariant.FromFloat(state.UseCooldownRemaining));
            vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Global.cooldownMax, DynamicVariant.FromFloat(state.UseCooldownTotal));
            vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Global.isUseCooldownActive, DynamicVariant.FromBool(state.IsUseCooldownActive));
            vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Global.isCountEnabled, DynamicVariant.FromBool(state.IsCountEnabled));
            vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Global.currentCount, DynamicVariant.FromInt(state.CurrentCount));
            vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Global.maxCount, DynamicVariant.FromInt(state.MaxCount));
            vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Global.usedCount, DynamicVariant.FromInt(state.UsedCount));
            vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Global.isCountExhausted, DynamicVariant.FromBool(state.IsCountExhausted));
            vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Global.canUse, DynamicVariant.FromBool(state.CanUse));
            vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Global.canConsumeUse, DynamicVariant.FromBool(state.CanConsumeUse));
        }
    }
}
