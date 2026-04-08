using Game.Health;
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
                0,
                remainingUseCount,
                maxUseCount,
                sortOrder)
        {
        }
    }
}
