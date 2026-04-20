#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Game.Collision;
using Game.Entity;
using Game.Search;
using Sirenix.OdinInspector;
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
        CollisionSearch = 30,
        None = 40,
    }

    public enum TargetChannelCollisionRangeSource
    {
        AreaChannelRect = 10,
        DynamicRect = 20,
    }

    [Serializable]
    public sealed class TargetChannelPreset : IDynamicManagedRefValue
    {
        public bool IsDynamicSearch => SearchType == TargetChannelSearchType.DynamicSearch;
        public bool IsScopeSearch => SearchType == TargetChannelSearchType.ScopeSearch;
        public bool IsCollisionSearch => SearchType == TargetChannelSearchType.CollisionSearch;
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

        [BoxGroup("Filters")]
        [Title("Direct Targets")]
        [ShowIf(nameof(IsNoneSearch))]
        [LabelText("Monitor Active State")]
        public bool MonitorActiveState = false;

        [BoxGroup("Filters")]
        [ShowIf(nameof(IsNoneSearch))]
        [LabelText("Valid Distance")]
        [Tooltip("Inspector setting.")]
        [MinValue(0f)]
        public float DirectTargetValidDistance = 0f;

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

        [BoxGroup("Collision Search")]
        [ShowIf(nameof(IsCollisionSearch))]
        [LabelText("Range Source")]
        [EnumToggleButtons]
        public TargetChannelCollisionRangeSource CollisionRangeSource = TargetChannelCollisionRangeSource.AreaChannelRect;

        [BoxGroup("Collision Search")]
        [ShowIf("@IsCollisionSearch && CollisionRangeSource == TargetChannelCollisionRangeSource.AreaChannelRect")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Area Hub Source\", CollisionAreaActorSource)")]
        public VNext.ActorSource CollisionAreaActorSource = new() { Kind = VNext.ActorSourceKind.Current };

        [BoxGroup("Collision Search")]
        [ShowIf("@IsCollisionSearch && CollisionRangeSource == TargetChannelCollisionRangeSource.AreaChannelRect")]
        [LabelText("Area Tag")]
        public string CollisionAreaTag = "default";

        [BoxGroup("Collision Search")]
        [ShowIf("@IsCollisionSearch && CollisionRangeSource == TargetChannelCollisionRangeSource.DynamicRect")]
        [LabelText("Rect Center")]
        public DynamicValue<Vector3> CollisionRectCenter = DynamicValueExtensions.FromLiteral(Vector3.zero);

        [BoxGroup("Collision Search")]
        [ShowIf("@IsCollisionSearch && CollisionRangeSource == TargetChannelCollisionRangeSource.DynamicRect")]
        [LabelText("Rect Size")]
        public DynamicValue<Vector2> CollisionRectSize = DynamicValueExtensions.FromLiteral(new Vector2(1f, 1f));

        [BoxGroup("Collision Search")]
        [ShowIf(nameof(IsCollisionSearch))]
        [LabelText("Use Include Dynamic Sets")]
        public bool CollisionUseIncludeDynamicSets = false;

        [BoxGroup("Collision Search")]
        [ShowIf("@IsCollisionSearch && CollisionUseIncludeDynamicSets")]
        [LabelText("Include Dynamic Sets")]
        public DynamicColliderSetRef[] CollisionIncludeDynamicSets = Array.Empty<DynamicColliderSetRef>();

        [BoxGroup("Collision Search")]
        [ShowIf(nameof(IsCollisionSearch))]
        [LabelText("Use Exclude Dynamic Sets")]
        public bool CollisionUseExcludeDynamicSets = false;

        [BoxGroup("Collision Search")]
        [ShowIf("@IsCollisionSearch && CollisionUseExcludeDynamicSets")]
        [LabelText("Exclude Dynamic Sets")]
        public DynamicColliderSetRef[] CollisionExcludeDynamicSets = Array.Empty<DynamicColliderSetRef>();

        [BoxGroup("Collision Search")]
        [ShowIf(nameof(IsCollisionSearch))]
        [LabelText("Match Any Include")]
        public bool CollisionMatchAnyInclude = true;

        [BoxGroup("Collision Search")]
        [ShowIf(nameof(IsCollisionSearch))]
        [InlineProperty]
        [LabelText("Hit Filter")]
        public HitFilter CollisionHitFilter;

        [BoxGroup("Debug")]
        [LabelText("Debug Log")]
        [Tooltip("Inspector setting.")]
        public bool DebugLogEnabled = false;

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
                MonitorActiveState = MonitorActiveState,
                DirectTargetValidDistance = DirectTargetValidDistance,
                OriginSource = OriginSource,
                CustomOriginTransform = CustomOriginTransform,
                ForwardSource = ForwardSource,
                CustomForwardTransform = CustomForwardTransform,
                CustomForwardVector = CustomForwardVector,
                ActorSource = ActorSource,
                ScopeRequireActive = ScopeRequireActive,
                CollisionRangeSource = CollisionRangeSource,
                CollisionAreaActorSource = CollisionAreaActorSource,
                CollisionAreaTag = CollisionAreaTag,
                CollisionRectCenter = CollisionRectCenter,
                CollisionRectSize = CollisionRectSize,
                CollisionUseIncludeDynamicSets = CollisionUseIncludeDynamicSets,
                CollisionIncludeDynamicSets = CollisionIncludeDynamicSets != null
                    ? (DynamicColliderSetRef[])CollisionIncludeDynamicSets.Clone()
                    : Array.Empty<DynamicColliderSetRef>(),
                CollisionUseExcludeDynamicSets = CollisionUseExcludeDynamicSets,
                CollisionExcludeDynamicSets = CollisionExcludeDynamicSets != null
                    ? (DynamicColliderSetRef[])CollisionExcludeDynamicSets.Clone()
                    : Array.Empty<DynamicColliderSetRef>(),
                CollisionMatchAnyInclude = CollisionMatchAnyInclude,
                CollisionHitFilter = CollisionHitFilter,
                DebugLogEnabled = DebugLogEnabled,
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

            if (mutation.ApplyMonitorActiveState)
                MonitorActiveState = mutation.MonitorActiveState;

            if (mutation.ApplyCollisionSearch)
            {
                CollisionRangeSource = mutation.CollisionRangeSource;
                CollisionAreaActorSource = mutation.CollisionAreaActorSource;
                CollisionAreaTag = mutation.CollisionAreaTag;
                CollisionRectCenter = mutation.CollisionRectCenter;
                CollisionRectSize = mutation.CollisionRectSize;
                CollisionUseIncludeDynamicSets = mutation.CollisionUseIncludeDynamicSets;
                CollisionIncludeDynamicSets = mutation.CollisionIncludeDynamicSets != null
                    ? (DynamicColliderSetRef[])mutation.CollisionIncludeDynamicSets.Clone()
                    : Array.Empty<DynamicColliderSetRef>();
                CollisionUseExcludeDynamicSets = mutation.CollisionUseExcludeDynamicSets;
                CollisionExcludeDynamicSets = mutation.CollisionExcludeDynamicSets != null
                    ? (DynamicColliderSetRef[])mutation.CollisionExcludeDynamicSets.Clone()
                    : Array.Empty<DynamicColliderSetRef>();
                CollisionMatchAnyInclude = mutation.CollisionMatchAnyInclude;
                CollisionHitFilter = mutation.CollisionHitFilter;
            }

            if (mutation.ApplyDebugLog)
                DebugLogEnabled = mutation.DebugLogEnabled;
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

        [BoxGroup("Collision Search")]
        [ToggleLeft]
        [LabelText("Apply Collision Search")]
        public bool ApplyCollisionSearch;

        [BoxGroup("Collision Search")]
        [ShowIf(nameof(ApplyCollisionSearch))]
        [LabelText("Range Source")]
        [EnumToggleButtons]
        public TargetChannelCollisionRangeSource CollisionRangeSource = TargetChannelCollisionRangeSource.AreaChannelRect;

        [BoxGroup("Collision Search")]
        [ShowIf(nameof(ApplyCollisionSearch))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Area Hub Source\", CollisionAreaActorSource)")]
        public VNext.ActorSource CollisionAreaActorSource = new() { Kind = VNext.ActorSourceKind.Current };

        [BoxGroup("Collision Search")]
        [ShowIf(nameof(ApplyCollisionSearch))]
        [LabelText("Area Tag")]
        public string CollisionAreaTag = "default";

        [BoxGroup("Collision Search")]
        [ShowIf(nameof(ApplyCollisionSearch))]
        [LabelText("Rect Center")]
        public DynamicValue<Vector3> CollisionRectCenter = DynamicValueExtensions.FromLiteral(Vector3.zero);

        [BoxGroup("Collision Search")]
        [ShowIf(nameof(ApplyCollisionSearch))]
        [LabelText("Rect Size")]
        public DynamicValue<Vector2> CollisionRectSize = DynamicValueExtensions.FromLiteral(new Vector2(1f, 1f));

        [BoxGroup("Collision Search")]
        [ShowIf(nameof(ApplyCollisionSearch))]
        [LabelText("Use Include Dynamic Sets")]
        public bool CollisionUseIncludeDynamicSets = false;

        [BoxGroup("Collision Search")]
        [ShowIf(nameof(ApplyCollisionSearch))]
        [LabelText("Include Dynamic Sets")]
        public DynamicColliderSetRef[] CollisionIncludeDynamicSets = Array.Empty<DynamicColliderSetRef>();

        [BoxGroup("Collision Search")]
        [ShowIf(nameof(ApplyCollisionSearch))]
        [LabelText("Use Exclude Dynamic Sets")]
        public bool CollisionUseExcludeDynamicSets = false;

        [BoxGroup("Collision Search")]
        [ShowIf(nameof(ApplyCollisionSearch))]
        [LabelText("Exclude Dynamic Sets")]
        public DynamicColliderSetRef[] CollisionExcludeDynamicSets = Array.Empty<DynamicColliderSetRef>();

        [BoxGroup("Collision Search")]
        [ShowIf(nameof(ApplyCollisionSearch))]
        [LabelText("Match Any Include")]
        public bool CollisionMatchAnyInclude = true;

        [BoxGroup("Collision Search")]
        [ShowIf(nameof(ApplyCollisionSearch))]
        [InlineProperty]
        [LabelText("Hit Filter")]
        public HitFilter CollisionHitFilter;

        [BoxGroup("Debug")]
        [ToggleLeft]
        [LabelText("Apply Debug Log")]
        public bool ApplyDebugLog;

        [BoxGroup("Debug")]
        [ShowIf(nameof(ApplyDebugLog))]
        [LabelText("Debug Log")]
        public bool DebugLogEnabled = false;

        [BoxGroup("Direct Targets")]
        [ToggleLeft]
        [LabelText("Apply Monitor Active State")]
        public bool ApplyMonitorActiveState;

        [BoxGroup("Direct Targets")]
        [ShowIf(nameof(ApplyMonitorActiveState))]
        [LabelText("Monitor Active State")]
        public bool MonitorActiveState = false;

        public bool HasAnyMutation()
        {
            return ApplyEnabled || ApplySearchType || ApplyDynamicSearch || ApplyScopeSearch || ApplyCollisionSearch || ApplyDebugLog || ApplyMonitorActiveState;
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

    public readonly struct TargetChannelTargetTelemetrySnapshot
    {
        public readonly string ScopeLabel;
        public readonly LifetimeScopeKind Kind;
        public readonly string Id;
        public readonly string Category;
        public readonly bool IsActive;
        public readonly Vector2 Position;
        public readonly float Distance;

        public TargetChannelTargetTelemetrySnapshot(
            string scopeLabel,
            LifetimeScopeKind kind,
            string id,
            string category,
            bool isActive,
            Vector2 position,
            float distance)
        {
            ScopeLabel = scopeLabel;
            Kind = kind;
            Id = id;
            Category = category;
            IsActive = isActive;
            Position = position;
            Distance = distance;
        }
    }

    public readonly struct TargetChannelTelemetrySnapshot
    {
        public readonly string Tag;
        public readonly bool Enabled;
        public readonly TargetChannelSearchType SearchType;
        public readonly TargetQueryKind Kind;
        public readonly float Radius;
        public readonly float HalfAngleDeg;
        public readonly int RefreshIntervalFrames;
        public readonly int ExpectedResultCount;
        public readonly LifetimeScopeMask KindMask;
        public readonly string? FilterId;
        public readonly string? FilterCategory;
        public readonly bool ExcludeSelf;
        public readonly TargetOriginSource OriginSource;
        public readonly TargetForwardSource ForwardSource;
        public readonly bool ScopeRequireActive;
        public readonly bool MonitorActiveState;
        public readonly float DirectTargetValidDistance;
        public readonly TargetChannelCollisionRangeSource CollisionRangeSource;
        public readonly string CollisionAreaTag;
        public readonly string CollisionFilterSummary;
        public readonly int TargetCount;
        public readonly IReadOnlyList<TargetChannelTargetTelemetrySnapshot> Targets;

        public TargetChannelTelemetrySnapshot(
            string tag,
            bool enabled,
            TargetChannelSearchType searchType,
            TargetQueryKind kind,
            float radius,
            float halfAngleDeg,
            int refreshIntervalFrames,
            int expectedResultCount,
            LifetimeScopeMask kindMask,
            string? filterId,
            string? filterCategory,
            bool excludeSelf,
            TargetOriginSource originSource,
            TargetForwardSource forwardSource,
            bool scopeRequireActive,
            bool monitorActiveState,
            float directTargetValidDistance,
            TargetChannelCollisionRangeSource collisionRangeSource,
            string collisionAreaTag,
            string collisionFilterSummary,
            int targetCount,
            IReadOnlyList<TargetChannelTargetTelemetrySnapshot> targets)
        {
            Tag = tag;
            Enabled = enabled;
            SearchType = searchType;
            Kind = kind;
            Radius = radius;
            HalfAngleDeg = halfAngleDeg;
            RefreshIntervalFrames = refreshIntervalFrames;
            ExpectedResultCount = expectedResultCount;
            KindMask = kindMask;
            FilterId = filterId;
            FilterCategory = filterCategory;
            ExcludeSelf = excludeSelf;
            OriginSource = originSource;
            ForwardSource = forwardSource;
            ScopeRequireActive = scopeRequireActive;
            MonitorActiveState = monitorActiveState;
            DirectTargetValidDistance = directTargetValidDistance;
            CollisionRangeSource = collisionRangeSource;
            CollisionAreaTag = collisionAreaTag;
            CollisionFilterSummary = collisionFilterSummary;
            TargetCount = targetCount;
            Targets = targets;
        }
    }

    public readonly struct TargetChannelHubTelemetrySnapshot
    {
        public readonly int Version;
        public readonly int ChannelCount;
        public readonly IReadOnlyList<TargetChannelTelemetrySnapshot> Channels;

        public TargetChannelHubTelemetrySnapshot(
            int version,
            int channelCount,
            IReadOnlyList<TargetChannelTelemetrySnapshot> channels)
        {
            Version = version;
            ChannelCount = channelCount;
            Channels = channels;
        }
    }

    public interface ITargetChannelHubTelemetry
    {
        int TelemetryVersion { get; }
        TargetChannelHubTelemetrySnapshot GetTelemetrySnapshot();
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
