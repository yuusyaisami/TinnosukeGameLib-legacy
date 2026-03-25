#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Game.Entity;
using Game.Search;
using Sirenix.OdinInspector;
using Unity.Mathematics;
using UnityEngine;
using VContainer;
using VNext = Game.Commands.VNext;

namespace Game.Targeting
{
    public enum TargetQueryKind
    {
        Circle = 10,
        Cone = 20,
    }

    public enum TargetOriginSource
    {
        OwnerFoot = 10,
        OwnerTransformPosition = 20,
        CustomTransform = 30,
    }

    public enum TargetForwardSource
    {
        OwnerTransformUp = 10,
        OwnerTransformRight = 20,
        CustomTransformUp = 30,
        CustomTransformRight = 40,
        CustomVector = 50,
    }

    public enum TargetChannelSearchType
    {
        DynamicSearch = 10,
        ScopeSearch = 20,
        None = 30,
    }

    [Serializable]
    public sealed class TargetChannelPreset : IDynamicManagedRefValue
    {
        public bool IsDynamicSearch => SearchType == TargetChannelSearchType.DynamicSearch;
        public bool IsScopeSearch => SearchType == TargetChannelSearchType.ScopeSearch;
        public bool IsNoneSearch => SearchType == TargetChannelSearchType.None;

        [BoxGroup("Identity")]
        [LabelText("Tag")]
        [Required]
        public string Tag = "default";

        [BoxGroup("Identity")]
        [LabelText("Enabled")]
        public bool Enabled = true;

        [BoxGroup("Search")]
        [LabelText("Type")]
        [EnumToggleButtons]
        public TargetChannelSearchType SearchType = TargetChannelSearchType.DynamicSearch;

        [BoxGroup("Query")]
        [ShowIf(nameof(IsDynamicSearch))]
        [LabelText("Kind")]
        public TargetQueryKind Kind = TargetQueryKind.Circle;

        [BoxGroup("Query")]
        [ShowIf(nameof(IsDynamicSearch))]
        [LabelText("Radius")]
        [MinValue(0.01f)]
        public float Radius = 5f;

        [BoxGroup("Query")]
        [ShowIf("@IsDynamicSearch && Kind == TargetQueryKind.Cone")]
        [LabelText("Half Angle (deg)")]
        [Range(1f, 179f)]
        public float HalfAngleDeg = 60f;

        [BoxGroup("Query")]
        [LabelText("Refresh Interval (frames)")]
        [MinValue(1)]
        public int RefreshIntervalFrames = 1;

        [BoxGroup("Query")]
        [LabelText("Expected Results")]
        [MinValue(0)]
        public int ExpectedResultCount = 32;

        [BoxGroup("Filters")]
        [ShowIf(nameof(IsDynamicSearch))]
        [LabelText("Kind Mask")]
        public LifetimeScopeMask KindMask = LifetimeScopeMask.Entity;

        [BoxGroup("Filters")]
        [ShowIf(nameof(IsDynamicSearch))]
        [LabelText("Filter Id")]
        public string? FilterId;

        [BoxGroup("Filters")]
        [ShowIf(nameof(IsDynamicSearch))]
        [LabelText("Filter Category")]
        public string? FilterCategory;

        [BoxGroup("Filters")]
        [LabelText("Exclude Self")]
        public bool ExcludeSelf = true;

        [BoxGroup("Sources")]
        [ShowIf(nameof(IsDynamicSearch))]
        [LabelText("Origin Source")]
        public TargetOriginSource OriginSource = TargetOriginSource.OwnerFoot;

        [BoxGroup("Sources")]
        [ShowIf("@IsDynamicSearch && OriginSource == TargetOriginSource.CustomTransform")]
        [LabelText("Custom Origin Transform")]
        public Transform? CustomOriginTransform;

        [BoxGroup("Sources")]
        [ShowIf("@IsDynamicSearch && Kind == TargetQueryKind.Cone")]
        [LabelText("Forward Source")]
        public TargetForwardSource ForwardSource = TargetForwardSource.OwnerTransformUp;

        [BoxGroup("Sources")]
        [ShowIf("@IsDynamicSearch && Kind == TargetQueryKind.Cone && (ForwardSource == TargetForwardSource.CustomTransformUp || ForwardSource == TargetForwardSource.CustomTransformRight)")]
        [LabelText("Custom Forward Transform")]
        public Transform? CustomForwardTransform;

        [BoxGroup("Sources")]
        [ShowIf("@IsDynamicSearch && Kind == TargetQueryKind.Cone && ForwardSource == TargetForwardSource.CustomVector")]
        [LabelText("Custom Forward Vector")]
        public Vector2 CustomForwardVector = Vector2.up;

        [BoxGroup("Scope Search")]
        [ShowIf(nameof(IsScopeSearch))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(ActorSource)")]
        public VNext.ActorSource ActorSource;

        [BoxGroup("Scope Search")]
        [ShowIf("@IsScopeSearch && ActorSource.Kind != Game.Commands.VNext.ActorSourceKind.ByIdentity")]
        [LabelText("Require Active")]
        public bool ScopeRequireActive = true;

        public float CosHalfAngle
        {
            get
            {
                if (Kind != TargetQueryKind.Cone)
                    return -1f;

                var rad = HalfAngleDeg * Mathf.Deg2Rad;
                return Mathf.Cos(rad);
            }
        }

        public TargetChannelPreset CreateRuntimeCopy()
        {
            return new TargetChannelPreset
            {
                Tag = Tag,
                Enabled = Enabled,
                SearchType = SearchType,
                Kind = Kind,
                Radius = Radius,
                HalfAngleDeg = HalfAngleDeg,
                RefreshIntervalFrames = RefreshIntervalFrames,
                ExpectedResultCount = ExpectedResultCount,
                KindMask = KindMask,
                FilterId = FilterId,
                FilterCategory = FilterCategory,
                ExcludeSelf = ExcludeSelf,
                OriginSource = OriginSource,
                CustomOriginTransform = CustomOriginTransform,
                ForwardSource = ForwardSource,
                CustomForwardTransform = CustomForwardTransform,
                CustomForwardVector = CustomForwardVector,
                ActorSource = ActorSource,
                ScopeRequireActive = ScopeRequireActive,
            };
        }

        public void ApplyMutation(TargetChannelRuntimeMutation mutation)
        {
            if (mutation == null)
                return;

            if (mutation.ApplyEnabled)
                Enabled = mutation.Enabled;

            if (mutation.ApplySearchType)
                SearchType = mutation.SearchType;

            if (mutation.ApplyDynamicSearch)
            {
                Kind = mutation.Kind;
                Radius = mutation.Radius;
                HalfAngleDeg = mutation.HalfAngleDeg;
                RefreshIntervalFrames = mutation.RefreshIntervalFrames;
                ExpectedResultCount = mutation.ExpectedResultCount;
                KindMask = mutation.KindMask;
                FilterId = mutation.FilterId;
                FilterCategory = mutation.FilterCategory;
                ExcludeSelf = mutation.ExcludeSelf;
                OriginSource = mutation.OriginSource;
                CustomOriginTransform = mutation.CustomOriginTransform;
                ForwardSource = mutation.ForwardSource;
                CustomForwardTransform = mutation.CustomForwardTransform;
                CustomForwardVector = mutation.CustomForwardVector;
            }

            if (mutation.ApplyScopeSearch)
            {
                ActorSource = mutation.ActorSource;
                ScopeRequireActive = mutation.ScopeRequireActive;
            }
        }
    }

    [Serializable]
    public sealed class TargetChannelRuntimeMutation
    {
        [BoxGroup("General")]
        [ToggleLeft]
        [LabelText("Apply Enabled")]
        public bool ApplyEnabled;

        [BoxGroup("General")]
        [ShowIf(nameof(ApplyEnabled))]
        [LabelText("Enabled")]
        public bool Enabled = true;

        [BoxGroup("General")]
        [ToggleLeft]
        [LabelText("Apply Search Type")]
        public bool ApplySearchType;

        [BoxGroup("General")]
        [ShowIf(nameof(ApplySearchType))]
        [LabelText("Search Type")]
        [EnumToggleButtons]
        public TargetChannelSearchType SearchType = TargetChannelSearchType.DynamicSearch;

        [BoxGroup("Dynamic Search")]
        [ToggleLeft]
        [LabelText("Apply Dynamic Search")]
        public bool ApplyDynamicSearch;

        [BoxGroup("Dynamic Search")]
        [ShowIf(nameof(ApplyDynamicSearch))]
        [LabelText("Kind")]
        public TargetQueryKind Kind = TargetQueryKind.Circle;

        [BoxGroup("Dynamic Search")]
        [ShowIf(nameof(ApplyDynamicSearch))]
        [MinValue(0.01f)]
        [LabelText("Radius")]
        public float Radius = 5f;

        [BoxGroup("Dynamic Search")]
        [ShowIf(nameof(ApplyDynamicSearch))]
        [Range(1f, 179f)]
        [LabelText("Half Angle (deg)")]
        public float HalfAngleDeg = 60f;

        [BoxGroup("Dynamic Search")]
        [ShowIf(nameof(ApplyDynamicSearch))]
        [MinValue(1)]
        [LabelText("Refresh Interval (frames)")]
        public int RefreshIntervalFrames = 1;

        [BoxGroup("Dynamic Search")]
        [ShowIf(nameof(ApplyDynamicSearch))]
        [MinValue(0)]
        [LabelText("Expected Results")]
        public int ExpectedResultCount = 32;

        [BoxGroup("Dynamic Search")]
        [ShowIf(nameof(ApplyDynamicSearch))]
        [LabelText("Kind Mask")]
        public LifetimeScopeMask KindMask = LifetimeScopeMask.Entity;

        [BoxGroup("Dynamic Search")]
        [ShowIf(nameof(ApplyDynamicSearch))]
        [LabelText("Filter Id")]
        public string? FilterId;

        [BoxGroup("Dynamic Search")]
        [ShowIf(nameof(ApplyDynamicSearch))]
        [LabelText("Filter Category")]
        public string? FilterCategory;

        [BoxGroup("Dynamic Search")]
        [ShowIf(nameof(ApplyDynamicSearch))]
        [LabelText("Exclude Self")]
        public bool ExcludeSelf = true;

        [BoxGroup("Dynamic Search")]
        [ShowIf(nameof(ApplyDynamicSearch))]
        [LabelText("Origin Source")]
        public TargetOriginSource OriginSource = TargetOriginSource.OwnerFoot;

        [BoxGroup("Dynamic Search")]
        [ShowIf(nameof(ApplyDynamicSearch))]
        [LabelText("Custom Origin Transform")]
        public Transform? CustomOriginTransform;

        [BoxGroup("Dynamic Search")]
        [ShowIf(nameof(ApplyDynamicSearch))]
        [LabelText("Forward Source")]
        public TargetForwardSource ForwardSource = TargetForwardSource.OwnerTransformUp;

        [BoxGroup("Dynamic Search")]
        [ShowIf(nameof(ApplyDynamicSearch))]
        [LabelText("Custom Forward Transform")]
        public Transform? CustomForwardTransform;

        [BoxGroup("Dynamic Search")]
        [ShowIf(nameof(ApplyDynamicSearch))]
        [LabelText("Custom Forward Vector")]
        public Vector2 CustomForwardVector = Vector2.up;

        [BoxGroup("Scope Search")]
        [ToggleLeft]
        [LabelText("Apply Scope Search")]
        public bool ApplyScopeSearch;

        [BoxGroup("Scope Search")]
        [ShowIf(nameof(ApplyScopeSearch))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(ActorSource)")]
        public VNext.ActorSource ActorSource;

        [BoxGroup("Scope Search")]
        [ShowIf(nameof(ApplyScopeSearch))]
        [LabelText("Require Active")]
        public bool ScopeRequireActive = true;

        public bool HasAnyMutation()
        {
            return ApplyEnabled || ApplySearchType || ApplyDynamicSearch || ApplyScopeSearch;
        }
    }

    [CreateAssetMenu(menuName = "Game/Targeting/Target Channel Preset", fileName = "TargetChannelPreset")]
    public sealed class TargetChannelPresetSO : ScriptableObject, IDynamicValueAsset<TargetChannelPreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        TargetChannelPreset? _preset = new();

        public TargetChannelPreset? Preset
        {
            get
            {
                if (_preset == null)
                    _preset = new TargetChannelPreset();
                return _preset;
            }
        }

        void OnEnable()
        {
            if (_preset == null)
                _preset = new TargetChannelPreset();
        }

        void OnValidate()
        {
            if (_preset == null)
                _preset = new TargetChannelPreset();
        }
    }

    public readonly struct TargetChannelOwner
    {
        public readonly Transform OwnerTransform;
        public readonly IScopeNode? OwnerScope;

        public TargetChannelOwner(Transform ownerTransform, IScopeNode? ownerScope)
        {
            OwnerTransform = ownerTransform;
            OwnerScope = ownerScope;
        }

        public FootTransformMB? ResolveFootTransform()
        {
            if (OwnerScope?.Resolver != null &&
                OwnerScope.Resolver.TryResolve<FootTransformMB>(out var resolverFoot) &&
                resolverFoot != null)
            {
                return resolverFoot;
            }

            if (OwnerTransform != null)
            {
                var parentFoot = OwnerTransform.GetComponentInParent<FootTransformMB>();
                if (parentFoot != null)
                    return parentFoot;
            }

            if (OwnerScope is Component scopeComponent)
            {
                var scopeFoot = scopeComponent.GetComponent<FootTransformMB>();
                if (scopeFoot != null)
                    return scopeFoot;
            }

            return null;
        }
    }

    public interface ITargetChannelRuntime
    {
        string Tag { get; }
        bool Enabled { get; set; }
        TargetChannelPreset CurrentPreset { get; }
        int LastUpdatedFrame { get; }
        List<DynamicSearchHit> Hits { get; }
        void Invalidate();
        void ForceRefresh();
        bool SwapPreset(TargetChannelPreset preset);
        bool MutateSettings(TargetChannelRuntimeMutation mutation);
        bool ResetRuntimeOverrides();
        bool SetDirectTargets(IReadOnlyList<DynamicSearchHit> hits);
        bool ClearDirectTargets();
    }

    public interface ITargetChannelHub
    {
        int ChannelCount { get; }
        bool TryGetRuntime(string tag, out ITargetChannelRuntime runtime);
        ITargetChannelRuntime RegisterOrReplace(TargetChannelPreset preset);
        bool SwapPreset(string tag, TargetChannelPreset preset);
        bool MutateSettings(string tag, TargetChannelRuntimeMutation mutation);
        bool ResetRuntimeOverrides(string tag);
        bool SetDirectTargets(string tag, IReadOnlyList<DynamicSearchHit> hits);
        bool ClearDirectTargets(string tag);
        bool Unregister(string tag);
        void Clear();
    }
}
