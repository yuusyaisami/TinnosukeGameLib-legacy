#nullable enable

using System;
using System.Collections.Generic;
using Game.Channel;
using Game.Common;
using Game.Collision;
using Game.Search;
using Unity.Mathematics;
using UnityEngine;
using VContainer;
using VNext = Game.Commands.VNext;

namespace Game.Targeting
{
    public sealed class TargetChannelRuntime : ITargetChannelRuntime
    {
        readonly IDynamicSearchService? _search;
        readonly IUnityCollisionManager? _collisionManager;
        readonly IHitColliderScopeRegistry? _hitScopeRegistry;
        readonly TargetChannelOwner _owner;
        readonly List<DynamicSearchHit> _hits;
        readonly List<DynamicSearchHit> _directHits;
        readonly HashSet<IScopeNode> _collisionScopeSet = new();

        Collider2D[] _collisionBuffer;

        TargetChannelPreset _basePreset;
        TargetChannelPreset _currentPreset;
        int _lastUpdatedFrame = int.MinValue;

        const int MinCollisionBufferSize = 32;
        const int MaxCollisionBufferSize = 2048;

        public TargetChannelRuntime(
            IDynamicSearchService? search,
            in TargetChannelOwner owner,
            TargetChannelPreset preset,
            IUnityCollisionManager? collisionManager = null,
            IHitColliderScopeRegistry? hitScopeRegistry = null)
        {
            _search = search;
            _collisionManager = collisionManager;
            _hitScopeRegistry = hitScopeRegistry;
            _owner = owner;
            _basePreset = preset?.CreateRuntimeCopy() ?? throw new ArgumentNullException(nameof(preset));
            _currentPreset = _basePreset.CreateRuntimeCopy();
            _hits = new List<DynamicSearchHit>(Mathf.Max(0, _currentPreset.ExpectedResultCount));
            _directHits = new List<DynamicSearchHit>(Mathf.Max(0, _currentPreset.ExpectedResultCount));
            _collisionBuffer = new Collider2D[MinCollisionBufferSize];
            EnsureCollisionBufferCapacity(Mathf.Max(0, _currentPreset.ExpectedResultCount));
        }

        public string Tag => _currentPreset.Tag;
        public bool Enabled
        {
            get => _currentPreset.Enabled;
            set
            {
                _currentPreset.Enabled = value;
                if (!value)
                    _hits.Clear();
            }
        }

        public TargetChannelPreset CurrentPreset => _currentPreset;
        public int LastUpdatedFrame => _lastUpdatedFrame;
        public List<DynamicSearchHit> Hits
        {
            get
            {
                EnsureUpdated(ignoreInterval: false);
                PruneInvalidHits();
                return _hits;
            }
        }

        public void Invalidate()
        {
            _lastUpdatedFrame = int.MinValue;
            if (!_currentPreset.IsNoneSearch)
                _hits.Clear();
        }

        public void ForceRefresh()
        {
            EnsureUpdated(ignoreInterval: true);
        }

        public bool SwapPreset(TargetChannelPreset preset)
        {
            if (preset == null || string.IsNullOrWhiteSpace(preset.Tag))
                return false;

            _basePreset = preset.CreateRuntimeCopy();
            _currentPreset = _basePreset.CreateRuntimeCopy();
            _directHits.Clear();
            ResizeHitCapacity();
            Invalidate();
            return true;
        }

        public bool MutateSettings(TargetChannelRuntimeMutation mutation)
        {
            if (mutation == null || !mutation.HasAnyMutation())
                return false;

            _currentPreset.ApplyMutation(mutation);
            ResizeHitCapacity();
            Invalidate();
            return true;
        }

        public bool ResetRuntimeOverrides()
        {
            _currentPreset = _basePreset.CreateRuntimeCopy();
            _directHits.Clear();
            ResizeHitCapacity();
            Invalidate();
            return true;
        }

        public bool SetDirectTargets(IReadOnlyList<DynamicSearchHit> hits)
        {
            if (!_currentPreset.IsNoneSearch)
                return false;

            _directHits.Clear();
            if (hits != null)
            {
                for (int i = 0; i < hits.Count; i++)
                    _directHits.Add(hits[i]);
            }

            RefreshDirectTargets();

            _lastUpdatedFrame = Time.frameCount;
            _hits.Clear();
            _hits.AddRange(_directHits);
            ApplySelfExclusion();
            return true;
        }

        public bool ClearDirectTargets()
        {
            if (!_currentPreset.IsNoneSearch)
                return false;

            _directHits.Clear();
            _hits.Clear();
            _lastUpdatedFrame = Time.frameCount;
            return true;
        }

        void EnsureUpdated(bool ignoreInterval)
        {
            MainThread.AssertMainThread();

            if (!_currentPreset.Enabled)
            {
                _hits.Clear();
                return;
            }

            int frame = Time.frameCount;
            if (frame == _lastUpdatedFrame)
                return;

            if (!ignoreInterval && !_currentPreset.IsNoneSearch && _currentPreset.RefreshIntervalFrames > 1)
            {
                int delta = frame - _lastUpdatedFrame;
                if (delta > 0 && delta < _currentPreset.RefreshIntervalFrames)
                    return;
            }

            if (_currentPreset.IsNoneSearch)
                RefreshDirectTargets();

            _lastUpdatedFrame = frame;
            _hits.Clear();

            switch (_currentPreset.SearchType)
            {
                case TargetChannelSearchType.DynamicSearch:
                    CollectDynamicHits();
                    break;
                case TargetChannelSearchType.ScopeSearch:
                    CollectScopeHits();
                    break;
                case TargetChannelSearchType.CollisionSearch:
                    CollectCollisionHits();
                    break;
                case TargetChannelSearchType.None:
                    CollectDirectHits();
                    break;
            }

            ApplySelfExclusion();
        }

        void PruneInvalidHits()
        {
            for (int i = _hits.Count - 1; i >= 0; i--)
            {
                if (!TargetChannelTargetPositionSourceHelper.IsHitAlive(_hits[i]))
                    _hits.RemoveAt(i);
            }
        }

        void CollectDynamicHits()
        {
            if (_search == null)
            {
                Debug.LogError($"[TargetChannelRuntime] IDynamicSearchService not found for '{_currentPreset.Tag}'.");
                return;
            }

            float2 origin = ResolveOrigin();
            float radius = Mathf.Max(0.01f, _currentPreset.Radius);
            if (_currentPreset.Kind == TargetQueryKind.Cone)
            {
                var q = new DynamicSearchQuery(
                    origin,
                    radius,
                    ResolveForward(),
                    _currentPreset.CosHalfAngle,
                    kindMask: _currentPreset.KindMask,
                    requireActive: true,
                    filterId: _currentPreset.FilterId,
                    filterCategory: _currentPreset.FilterCategory);
                _search.Query(in q, _hits);
                return;
            }

            var query = new DynamicSearchQuery(
                origin,
                radius,
                kindMask: _currentPreset.KindMask,
                requireActive: true,
                filterId: _currentPreset.FilterId,
                filterCategory: _currentPreset.FilterCategory);
            _search.Query(in query, _hits);
        }

        void CollectScopeHits()
        {
            var source = _currentPreset.ActorSource;
            if (source.Kind == VNext.ActorSourceKind.ByIdentity)
            {
                if (!TryResolveScopeRegistry(out var registry) || registry == null)
                    return;

                var scopes = registry.ResolveAll(source.Identity, _owner.OwnerScope);
                if (scopes == null || scopes.Count == 0)
                    return;

                var origin = ResolveOwnerOrigin();
                for (int i = 0; i < scopes.Count; i++)
                    AddScopeHit(scopes[i], origin, requireActive: false);

                return;
            }

            var scope = VNext.ActorSourceFastResolver.Resolve(_owner.OwnerScope, source);
            if (scope == null)
                return;

            AddScopeHit(scope, ResolveOwnerOrigin(), _currentPreset.ScopeRequireActive);
        }

        void CollectCollisionHits()
        {
            if (_collisionManager == null || _hitScopeRegistry == null)
            {
                LogDebug($"CollisionSearch skipped: dependencies missing (collisionManager={_collisionManager != null}, hitScopeRegistry={_hitScopeRegistry != null})");
                return;
            }

            if (!TryResolveCollisionRect(out var center, out var size))
            {
                LogDebug($"CollisionSearch skipped: area resolution failed (rangeSource={_currentPreset.CollisionRangeSource}, areaTag='{NormalizeTagForLog(_currentPreset.CollisionAreaTag)}')");
                return;
            }

            size.x = math.max(0.01f, size.x);
            size.y = math.max(0.01f, size.y);

            LogDebug($"CollisionSearch start: areaTag='{NormalizeTagForLog(_currentPreset.CollisionAreaTag)}' center=({center.x:0.##},{center.y:0.##}) size=({size.x:0.##},{size.y:0.##}) hitFilter={DescribeHitFilter(_currentPreset.CollisionHitFilter)} excludeSelf={_currentPreset.ExcludeSelf}");

            var overlapFilter = default(ContactFilter2D);
            overlapFilter.useTriggers = true;

            var overlapCount = Physics2D.OverlapBox(
                new Vector2(center.x, center.y),
                new Vector2(size.x, size.y),
                0f,
                overlapFilter,
                _collisionBuffer);
            if (overlapCount <= 0)
            {
                LogDebug("CollisionSearch overlap count = 0");
                return;
            }

            LogDebug($"CollisionSearch overlap count = {overlapCount}");

            if (overlapCount >= _collisionBuffer.Length && _collisionBuffer.Length < MaxCollisionBufferSize)
            {
                EnsureCollisionBufferCapacity(_collisionBuffer.Length * 2);
                overlapCount = Physics2D.OverlapBox(
                    new Vector2(center.x, center.y),
                    new Vector2(size.x, size.y),
                    0f,
                    overlapFilter,
                    _collisionBuffer);
                if (overlapCount <= 0)
                {
                    LogDebug("CollisionSearch overlap count became 0 after buffer resize retry");
                    return;
                }

                LogDebug($"CollisionSearch overlap count after resize retry = {overlapCount}");
            }

            var ownerOrigin = ResolveOwnerOrigin();
            _collisionScopeSet.Clear();

            for (var i = 0; i < overlapCount; i++)
            {
                var collider = _collisionBuffer[i];
                if (collider == null)
                {
                    LogDebug($"Overlap[{i}] rejected: collider null");
                    continue;
                }

                if (!_collisionManager.TryGetDynamicHandle(collider, out var handle) || !handle.IsValid)
                {
                    LogDebug($"Overlap[{i}] rejected: no dynamic handle collider={DescribeCollider(collider)}");
                    continue;
                }

                if (!_collisionManager.TryGetDynamicMetadata(handle, out var metadata))
                {
                    LogDebug($"Overlap[{i}] rejected: metadata missing handle={handle.Id}:{handle.Generation} collider={DescribeCollider(collider)}");
                    continue;
                }

                if (!PassCollisionSetFilters(metadata.SetId))
                {
                    LogDebug($"Overlap[{i}] rejected: set filter rejected setId={metadata.SetId} handle={handle.Id}:{handle.Generation} colliderTag='{metadata.ColliderTag}'");
                    continue;
                }

                var pseudoHit = new CollisionHit
                {
                    Kind = CollisionKind.DynamicDynamic,
                    Self = handle,
                    OtherDynamic = handle,
                    OtherStatic = StaticColliderHandle.Invalid,
                    OtherSetId = metadata.SetId,
                    OtherStaticKind = default,
                };

                if (!_currentPreset.CollisionHitFilter.Matches(in pseudoHit))
                {
                    LogDebug($"Overlap[{i}] rejected: hit filter rejected setId={metadata.SetId} handle={handle.Id}:{handle.Generation} colliderTag='{metadata.ColliderTag}'");
                    continue;
                }

                if (!_hitScopeRegistry.TryResolve(handle, out var scope) || scope == null)
                {
                    LogDebug($"Overlap[{i}] rejected: scope registry could not resolve handle={handle.Id}:{handle.Generation} collider={DescribeCollider(collider)}");
                    continue;
                }

                if (!_collisionScopeSet.Add(scope))
                {
                    LogDebug($"Overlap[{i}] rejected: duplicate scope {DescribeScope(scope)} handle={handle.Id}:{handle.Generation}");
                    continue;
                }

                var identity = scope.Identity;
                if (identity == null)
                {
                    LogDebug($"Overlap[{i}] rejected: identity missing scope={DescribeScope(scope)} handle={handle.Id}:{handle.Generation}");
                    continue;
                }

                if (!TryResolveScopePosition(scope, identity, out var pos))
                {
                    LogDebug($"Overlap[{i}] rejected: position resolve failed scope={DescribeScope(scope)} identityId='{identity.Id}' kind={identity.Kind} hasSelfTransform={identity.SelfTransform != null}");
                    continue;
                }

                var delta = pos - ownerOrigin;
                var distSq = math.dot(delta, delta);
                _hits.Add(new DynamicSearchHit(scope, identity, distSq, pos));
                LogDebug($"Overlap[{i}] accepted: scope={DescribeScope(scope)} identityId='{identity.Id}' kind={identity.Kind} pos=({pos.x:0.##},{pos.y:0.##}) dist={math.sqrt(distSq):0.##}");
            }

            LogDebug($"CollisionSearch done: hits={_hits.Count}");

            for (var i = 0; i < overlapCount; i++)
                _collisionBuffer[i] = null!;
        }

        bool TryResolveCollisionRect(out float2 center, out float2 size)
        {
            center = default;
            size = default;

            return _currentPreset.CollisionRangeSource switch
            {
                TargetChannelCollisionRangeSource.AreaChannelRect => TryResolveAreaCollisionRect(out center, out size),
                TargetChannelCollisionRangeSource.DynamicRect => TryResolveDynamicCollisionRect(out center, out size),
                _ => false,
            };
        }

        bool TryResolveAreaCollisionRect(out float2 center, out float2 size)
        {
            center = default;
            size = default;

            var ownerScope = _owner.OwnerScope;
            if (ownerScope == null)
            {
                LogDebug("CollisionSearch area resolve failed: owner scope is null");
                return false;
            }

            var areaScope = VNext.ActorSourceFastResolver.Resolve(ownerScope, _currentPreset.CollisionAreaActorSource);
            if (areaScope == null)
            {
                LogDebug($"CollisionSearch area resolve failed: area actor source resolved null (actorSourceKind={_currentPreset.CollisionAreaActorSource.Kind})");
                return false;
            }

            if (!TryResolveAreaHub(areaScope, out var areaHub) || areaHub == null)
            {
                LogDebug($"CollisionSearch area resolve failed: IAreaChannelHubService not found from scope={DescribeScope(areaScope)}");
                return false;
            }

            var areaTag = string.IsNullOrWhiteSpace(_currentPreset.CollisionAreaTag)
                ? "default"
                : _currentPreset.CollisionAreaTag.Trim();
            if (!areaHub.TryGetRectSnapshot(areaTag, out var snapshot))
            {
                LogDebug($"CollisionSearch area resolve failed: rect snapshot not found for tag='{areaTag}' scope={DescribeScope(areaScope)}");
                return false;
            }

            center = snapshot.Plane == AreaPlane.XZ
                ? new float2(snapshot.Center.x, snapshot.Center.z)
                : new float2(snapshot.Center.x, snapshot.Center.y);
            size = new float2(snapshot.Size.x, snapshot.Size.y);

            LogDebug($"CollisionSearch area resolved: tag='{areaTag}' plane={snapshot.Plane} center=({center.x:0.##},{center.y:0.##}) size=({size.x:0.##},{size.y:0.##})");
            return true;
        }

        bool TryResolveDynamicCollisionRect(out float2 center, out float2 size)
        {
            center = default;
            size = default;

            var ownerScope = _owner.OwnerScope;
            if (ownerScope == null)
                return false;

            var context = new SimpleDynamicContext(ResolveVars(ownerScope), ownerScope);
            if (!_currentPreset.CollisionRectCenter.TryGet(context, out var center3))
                return false;

            if (!_currentPreset.CollisionRectSize.TryGet(context, out var size2))
                return false;

            center = new float2(center3.x, center3.y);
            size = new float2(size2.x, size2.y);
            return true;
        }

        bool PassCollisionSetFilters(DynamicColliderSetId setId)
        {
            var hasInclude = _currentPreset.CollisionUseIncludeDynamicSets &&
                             _currentPreset.CollisionIncludeDynamicSets != null &&
                             _currentPreset.CollisionIncludeDynamicSets.Length > 0;
            if (hasInclude)
            {
                var includeMatched = ContainsSet(_currentPreset.CollisionIncludeDynamicSets, setId);
                if (_currentPreset.CollisionMatchAnyInclude)
                {
                    if (!includeMatched)
                        return false;
                }
                else
                {
                    if (!includeMatched)
                        return false;
                }
            }

            var hasExclude = _currentPreset.CollisionUseExcludeDynamicSets &&
                             _currentPreset.CollisionExcludeDynamicSets != null &&
                             _currentPreset.CollisionExcludeDynamicSets.Length > 0;
            if (hasExclude && ContainsSet(_currentPreset.CollisionExcludeDynamicSets, setId))
                return false;

            return true;
        }

        static bool ContainsSet(DynamicColliderSetRef[]? sets, DynamicColliderSetId setId)
        {
            if (sets == null)
                return false;

            for (var i = 0; i < sets.Length; i++)
            {
                if (sets[i].Id == setId)
                    return true;
            }

            return false;
        }

        bool TryResolveAreaHub(IScopeNode startScope, out IAreaChannelHubService? areaHub)
        {
            for (var current = startScope; current != null; current = current.Parent)
            {
                var resolver = current.Resolver;
                if (resolver != null && resolver.TryResolve<IAreaChannelHubService>(out var hub) && hub != null)
                {
                    areaHub = hub;
                    return true;
                }
            }

            areaHub = null;
            return false;
        }

        void CollectDirectHits()
        {
            if (_directHits.Count == 0)
                return;

            var requireActive = _currentPreset.IsNoneSearch && _currentPreset.MonitorActiveState;
            var origin = ResolveOwnerOrigin();
            for (int i = 0; i < _directHits.Count; i++)
            {
                var direct = _directHits[i];
                if (!TargetChannelTargetPositionSourceHelper.IsHitAlive(direct, requireActive))
                    continue;

                float2 pos;
                if (!TryResolveScopePosition(direct.Scope, direct.Identity, out pos))
                    pos = direct.Position;

                var delta = pos - origin;
                var distSq = math.dot(delta, delta);
                _hits.Add(new DynamicSearchHit(direct.Scope, direct.Identity, distSq, pos));
            }
        }

        void RefreshDirectTargets()
        {
            if (!_currentPreset.IsNoneSearch || _directHits.Count == 0)
                return;

            var requireActive = _currentPreset.MonitorActiveState;
            var hasValidDistance = _currentPreset.DirectTargetValidDistance > 0f;
            var maxDistanceSq = hasValidDistance
                ? _currentPreset.DirectTargetValidDistance * _currentPreset.DirectTargetValidDistance
                : 0f;
            var origin = hasValidDistance ? ResolveOwnerOrigin() : default;

            for (int i = _directHits.Count - 1; i >= 0; i--)
            {
                var direct = _directHits[i];
                if (!TargetChannelTargetPositionSourceHelper.IsHitAlive(direct, requireActive))
                {
                    _directHits.RemoveAt(i);
                    continue;
                }

                if (!hasValidDistance)
                    continue;

                float2 pos;
                if (!TryResolveScopePosition(direct.Scope, direct.Identity, out pos))
                    pos = direct.Position;

                var delta = pos - origin;
                if (math.dot(delta, delta) > maxDistanceSq)
                    _directHits.RemoveAt(i);
            }
        }

        void AddScopeHit(IScopeNode scope, float2 origin, bool requireActive)
        {
            if (scope == null)
                return;

            var identity = scope.Identity;
            if (identity == null)
                return;

            if (requireActive && !identity.IsActive)
                return;

            if (!TryResolveScopePosition(scope, identity, out var pos))
                return;

            var delta = pos - origin;
            float distSq = math.dot(delta, delta);
            _hits.Add(new DynamicSearchHit(scope, identity, distSq, pos));
        }

        void ApplySelfExclusion()
        {
            if (!_currentPreset.ExcludeSelf || _owner.OwnerScope == null)
                return;

            for (int i = _hits.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_hits[i].Scope, _owner.OwnerScope))
                    _hits.RemoveAt(i);
            }
        }

        void ResizeHitCapacity()
        {
            var capacity = Mathf.Max(0, _currentPreset.ExpectedResultCount);
            if (_hits.Capacity < capacity)
                _hits.Capacity = capacity;
            if (_directHits.Capacity < capacity)
                _directHits.Capacity = capacity;

            EnsureCollisionBufferCapacity(capacity);
        }

        void EnsureCollisionBufferCapacity(int expectedResultCount)
        {
            var targetSize = Mathf.Clamp(
                Mathf.Max(MinCollisionBufferSize, Mathf.Max(1, expectedResultCount) * 2),
                MinCollisionBufferSize,
                MaxCollisionBufferSize);
            if (_collisionBuffer.Length >= targetSize)
                return;

            _collisionBuffer = new Collider2D[targetSize];
        }

        float2 ResolveOrigin()
        {
            switch (_currentPreset.OriginSource)
            {
                case TargetOriginSource.OwnerFoot:
                    {
                        var foot = _owner.ResolveFootTransform();
                        if (foot != null)
                        {
                            var p = foot.FootWorldPosition;
                            return new float2(p.x, p.y);
                        }

                        var ownerPos = _owner.OwnerTransform.position;
                        return new float2(ownerPos.x, ownerPos.y);
                    }

                case TargetOriginSource.CustomTransform:
                    {
                        var tr = _currentPreset.CustomOriginTransform != null ? _currentPreset.CustomOriginTransform : _owner.OwnerTransform;
                        var p = tr.position;
                        return new float2(p.x, p.y);
                    }

                default:
                    {
                        var p = _owner.OwnerTransform.position;
                        return new float2(p.x, p.y);
                    }
            }
        }

        float2 ResolveForward()
        {
            Vector2 f;
            switch (_currentPreset.ForwardSource)
            {
                case TargetForwardSource.OwnerTransformRight:
                    f = _owner.OwnerTransform.right;
                    break;
                case TargetForwardSource.CustomTransformUp:
                    f = (_currentPreset.CustomForwardTransform ?? _owner.OwnerTransform).up;
                    break;
                case TargetForwardSource.CustomTransformRight:
                    f = (_currentPreset.CustomForwardTransform ?? _owner.OwnerTransform).right;
                    break;
                case TargetForwardSource.CustomVector:
                    f = _currentPreset.CustomForwardVector;
                    break;
                default:
                    f = _owner.OwnerTransform.up;
                    break;
            }

            float lenSq = f.sqrMagnitude;
            if (lenSq < 0.000001f)
                f = Vector2.up;
            else
                f /= Mathf.Sqrt(lenSq);

            return new float2(f.x, f.y);
        }

        float2 ResolveOwnerOrigin()
        {
            var p = _owner.OwnerTransform.position;
            return new float2(p.x, p.y);
        }

        bool TryResolveScopeRegistry(out IBaseLifetimeScopeRegistry? registry)
        {
            var current = _owner.OwnerScope;
            while (current != null)
            {
                var resolver = current.Resolver;
                if (resolver != null && resolver.TryResolve<IBaseLifetimeScopeRegistry>(out var resolved) && resolved != null)
                {
                    registry = resolved;
                    return true;
                }

                current = current.Parent;
            }

            registry = null;
            return false;
        }

        static IVarStore ResolveVars(IScopeNode scope)
        {
            if (scope.Resolver != null &&
                scope.Resolver.TryResolve<IVarStore>(out var vars) &&
                vars != null)
            {
                return vars;
            }

            return NullVarStore.Instance;
        }

        void LogDebug(string message)
        {
            if (!_currentPreset.DebugLogEnabled)
                return;

            Debug.Log($"[TargetChannel:{Tag} f={Time.frameCount}] {message} owner={DescribeScope(_owner.OwnerScope)}");
        }

        static string NormalizeTagForLog(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return "(default)";

            return tag.Trim();
        }

        static string DescribeScope(IScopeNode? scope)
        {
            if (scope == null)
                return "(null)";

            var identity = scope.Identity;
            if (identity != null)
                return $"{identity.Id}:{identity.Kind}";

            if (scope is Component component && component != null)
                return component.gameObject.name;

            return scope.GetType().Name;
        }

        static string DescribeCollider(Collider2D collider)
        {
            if (collider == null)
                return "(null)";

            return $"{collider.name} enabled={collider.enabled} trigger={collider.isTrigger} layer={collider.gameObject.layer}";
        }

        static string DescribeHitFilter(in HitFilter filter)
        {
            var mobility = filter.UseMobility ? filter.Mobility.ToString() : "off";
            var dynamicSet = filter.UseDynamicSet ? filter.DynamicSetId.ToString() : "off";
            var staticKind = filter.UseStaticKind ? filter.StaticKind.ToString() : "off";
            return $"mobility={mobility} dynSet={dynamicSet} staticKind={staticKind}";
        }

        static bool TryResolveScopePosition(IScopeNode scope, IScopeIdentityService identity, out float2 pos)
        {
            pos = default;
            if (identity.SelfTransform != null)
            {
                var p = identity.SelfTransform.position;
                pos = new float2(p.x, p.y);
                return true;
            }

            if (scope is Component comp)
            {
                var p = comp.transform.position;
                pos = new float2(p.x, p.y);
                return true;
            }

            return false;
        }
    }

    public sealed class TargetChannelScopeRuntime : ITargetChannelRuntime
    {
        readonly TargetChannelRuntime _runtime;

        public TargetChannelScopeRuntime(in TargetChannelOwner owner, TargetChannelPreset preset)
        {
            _runtime = new TargetChannelRuntime(search: null, owner, preset);
        }

        public string Tag => _runtime.Tag;
        public bool Enabled { get => _runtime.Enabled; set => _runtime.Enabled = value; }
        public TargetChannelPreset CurrentPreset => _runtime.CurrentPreset;
        public int LastUpdatedFrame => _runtime.LastUpdatedFrame;
        public List<DynamicSearchHit> Hits => _runtime.Hits;
        public void Invalidate() => _runtime.Invalidate();
        public void ForceRefresh() => _runtime.ForceRefresh();
        public bool SwapPreset(TargetChannelPreset preset) => _runtime.SwapPreset(preset);
        public bool MutateSettings(TargetChannelRuntimeMutation mutation) => _runtime.MutateSettings(mutation);
        public bool ResetRuntimeOverrides() => _runtime.ResetRuntimeOverrides();
        public bool SetDirectTargets(IReadOnlyList<DynamicSearchHit> hits) => _runtime.SetDirectTargets(hits);
        public bool ClearDirectTargets() => _runtime.ClearDirectTargets();
    }
}

