#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Game.Commands;

namespace Game.Common
{
    public enum DynamicEvaluationPhase
    {
        Unknown = 0,
        Init = 10,
        Acquire = 20,
        CommandExecute = 30,
        CommandWaitResume = 40,
        Tick = 50,
        ExplicitRead = 60,
        TestFixture = 70,
    }

    public enum DynamicDependencyDeclarationMode
    {
        Static = 10,
        Tracked = 20,
        Hybrid = 30,
    }

    public enum DynamicFallbackPolicy
    {
        Forbidden = 10,
        OptionalDefault = 20,
    }

    public enum DynamicCachePolicy
    {
        None = 0,
        SharedTracked = 10,
    }

    public enum DynamicSchedulingPolicy
    {
        OnDemand = 10,
        OnDependencyChange = 20,
        ExplicitTick = 30,
        EveryFrame = 40,
    }

    public enum DynamicInvalidationPolicy
    {
        Manual = 10,
        OnDependencyStampChange = 20,
        OnSourceConfigChange = 30,
    }

    public enum DynamicEvaluationDiagnosticSeverity
    {
        Info = 10,
        Warning = 20,
        Error = 30,
    }

    public readonly struct DynamicEvaluationDiagnostic
    {
        public DynamicEvaluationDiagnostic(
            string code,
            DynamicEvaluationDiagnosticSeverity severity,
            string message,
            string phase,
            string sourceType,
            string? planId = null,
            string? rootSource = null,
            string? sourceLocation = null,
            Exception? exception = null)
        {
            Code = code ?? string.Empty;
            Severity = severity;
            Message = message ?? string.Empty;
            Phase = phase ?? string.Empty;
            SourceType = sourceType ?? string.Empty;
            PlanId = planId;
            RootSource = rootSource;
            SourceLocation = sourceLocation;
            Exception = exception;
        }

        public string Code { get; }
        public DynamicEvaluationDiagnosticSeverity Severity { get; }
        public string Message { get; }
        public string Phase { get; }
        public string SourceType { get; }
        public string? PlanId { get; }
        public string? RootSource { get; }
        public string? SourceLocation { get; }
        public Exception? Exception { get; }
    }

    public interface IDynamicEvaluationDiagnosticSink
    {
        void Report(in DynamicEvaluationDiagnostic diagnostic);
    }

    public readonly struct DynamicEvaluationPlanId : IEquatable<DynamicEvaluationPlanId>
    {
        public DynamicEvaluationPlanId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(DynamicEvaluationPlanId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is DynamicEvaluationPlanId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();
        public static bool operator ==(DynamicEvaluationPlanId left, DynamicEvaluationPlanId right) => left.Equals(right);
        public static bool operator !=(DynamicEvaluationPlanId left, DynamicEvaluationPlanId right) => !left.Equals(right);
    }

    public readonly struct ReactiveEvaluationPlanId : IEquatable<ReactiveEvaluationPlanId>
    {
        public ReactiveEvaluationPlanId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(ReactiveEvaluationPlanId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ReactiveEvaluationPlanId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();
        public static bool operator ==(ReactiveEvaluationPlanId left, ReactiveEvaluationPlanId right) => left.Equals(right);
        public static bool operator !=(ReactiveEvaluationPlanId left, ReactiveEvaluationPlanId right) => !left.Equals(right);
    }

    public enum DynamicTrackedPlanKind
    {
        Dynamic = 10,
        Reactive = 20,
    }

    public readonly struct DynamicTrackedPlanKey : IEquatable<DynamicTrackedPlanKey>
    {
        public DynamicTrackedPlanKey(DynamicTrackedPlanKind kind, int value)
        {
            Kind = kind;
            Value = value;
        }

        public DynamicTrackedPlanKind Kind { get; }
        public int Value { get; }
        public bool IsValid => Value != 0;

        public static DynamicTrackedPlanKey From(DynamicEvaluationPlanId planId)
            => new DynamicTrackedPlanKey(DynamicTrackedPlanKind.Dynamic, planId.Value);

        public static DynamicTrackedPlanKey From(ReactiveEvaluationPlanId planId)
            => new DynamicTrackedPlanKey(DynamicTrackedPlanKind.Reactive, planId.Value);

        public bool Equals(DynamicTrackedPlanKey other) => Kind == other.Kind && Value == other.Value;
        public override bool Equals(object obj) => obj is DynamicTrackedPlanKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Kind * 397) ^ Value;
            }
        }

        public override string ToString() => Kind + ":" + Value;

        public static bool operator ==(DynamicTrackedPlanKey left, DynamicTrackedPlanKey right) => left.Equals(right);
        public static bool operator !=(DynamicTrackedPlanKey left, DynamicTrackedPlanKey right) => !left.Equals(right);
    }

    public readonly struct DynamicSourceHandle : IEquatable<DynamicSourceHandle>
    {
        public DynamicSourceHandle(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(DynamicSourceHandle other) => Value == other.Value;
        public override bool Equals(object obj) => obj is DynamicSourceHandle other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();
        public static bool operator ==(DynamicSourceHandle left, DynamicSourceHandle right) => left.Equals(right);
        public static bool operator !=(DynamicSourceHandle left, DynamicSourceHandle right) => !left.Equals(right);
    }

    public readonly struct DynamicDependencyTokenSet : IEquatable<DynamicDependencyTokenSet>
    {
        public DynamicDependencyTokenSet(int runtimeQueryVersion = 0, int scopeVersion = 0, int commandVersion = 0, int extraVersion = 0)
        {
            RuntimeQueryVersion = runtimeQueryVersion;
            ScopeVersion = scopeVersion;
            CommandVersion = commandVersion;
            ExtraVersion = extraVersion;
        }

        public int RuntimeQueryVersion { get; }
        public int ScopeVersion { get; }
        public int CommandVersion { get; }
        public int ExtraVersion { get; }
        public bool IsEmpty => RuntimeQueryVersion == 0 && ScopeVersion == 0 && CommandVersion == 0 && ExtraVersion == 0;

        public bool Equals(DynamicDependencyTokenSet other)
            => RuntimeQueryVersion == other.RuntimeQueryVersion
                && ScopeVersion == other.ScopeVersion
                && CommandVersion == other.CommandVersion
                && ExtraVersion == other.ExtraVersion;

        public override bool Equals(object obj) => obj is DynamicDependencyTokenSet other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = RuntimeQueryVersion;
                hash = (hash * 397) ^ ScopeVersion;
                hash = (hash * 397) ^ CommandVersion;
                hash = (hash * 397) ^ ExtraVersion;
                return hash;
            }
        }
    }

    public interface IDynamicDependencyTokenSource
    {
        DynamicDependencyTokenSet GetDynamicDependencyTokens();
    }

    public readonly struct DynamicEvaluationOrigin : IEquatable<DynamicEvaluationOrigin>
    {
        public DynamicEvaluationOrigin(int scopeIdentity, int commandIdentity, int commandFrameId = 0, int extraIdentity = 0)
        {
            ScopeIdentity = scopeIdentity;
            CommandIdentity = commandIdentity;
            CommandFrameId = commandFrameId;
            ExtraIdentity = extraIdentity;
        }

        public int ScopeIdentity { get; }
        public int CommandIdentity { get; }
        public int CommandFrameId { get; }
        public int ExtraIdentity { get; }
        public bool IsEmpty => ScopeIdentity == 0 && CommandIdentity == 0 && CommandFrameId == 0 && ExtraIdentity == 0;

        public static DynamicEvaluationOrigin Empty => default;

        public static DynamicEvaluationOrigin FromContext(IDynamicContext context)
        {
            if (context == null)
                return Empty;

            if (context is IDynamicEvaluationOriginProvider provider)
                return provider.GetDynamicEvaluationOrigin();

            var commandFrameId = 0;
            if (context is Game.Commands.VNext.CommandContext commandContext)
            {
                commandFrameId = commandContext.CurrentFrame.FrameId.Value;
            }
            else if (context is Game.Commands.VNext.CommandResolveContext resolveContext && resolveContext.RuntimeContext != null)
            {
                commandFrameId = resolveContext.RuntimeContext.CurrentFrame.FrameId.Value;
            }

            return FromScopeNodes(context.Scope, context.CommandRootScope, commandFrameId);
        }

        public static DynamicEvaluationOrigin FromScopeNodes(IScopeNode? scope, IScopeNode? commandRootScope, int commandFrameId = 0, int extraIdentity = 0)
        {
            return new DynamicEvaluationOrigin(
                ComputeStableScopeIdentity(scope),
                ComputeStableScopeIdentity(commandRootScope),
                commandFrameId,
                extraIdentity);
        }

        public static int ComputeStableScopeIdentity(IScopeNode? scope)
        {
            if (scope == null)
                return 0;

            unchecked
            {
                var hash = unchecked((int)2166136261u);
                hash = Mix(hash, scope.Kind.ToString());
                hash = Mix(hash, scope.GetType().FullName ?? string.Empty);

                var identity = scope.Identity;
                if (identity != null)
                {
                    hash = Mix(hash, identity.Kind.ToString());
                    hash = Mix(hash, identity.Id ?? string.Empty);
                    hash = Mix(hash, identity.Category ?? string.Empty);
                }

                return hash;
            }
        }

        public bool Equals(DynamicEvaluationOrigin other)
        {
            return ScopeIdentity == other.ScopeIdentity
                && CommandIdentity == other.CommandIdentity
                && CommandFrameId == other.CommandFrameId
                && ExtraIdentity == other.ExtraIdentity;
        }

        public override bool Equals(object obj) => obj is DynamicEvaluationOrigin other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ScopeIdentity;
                hash = (hash * 397) ^ CommandIdentity;
                hash = (hash * 397) ^ CommandFrameId;
                hash = (hash * 397) ^ ExtraIdentity;
                return hash;
            }
        }

        public static bool operator ==(DynamicEvaluationOrigin left, DynamicEvaluationOrigin right) => left.Equals(right);
        public static bool operator !=(DynamicEvaluationOrigin left, DynamicEvaluationOrigin right) => !left.Equals(right);

        static int Mix(int hash, string value)
        {
            unchecked
            {
                if (string.IsNullOrEmpty(value))
                    return (hash * 16777619) ^ 0;

                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619;
                }

                return hash;
            }
        }
    }

    public interface IDynamicEvaluationOriginProvider
    {
        DynamicEvaluationOrigin GetDynamicEvaluationOrigin();
    }

    public readonly struct DynamicDependencyStamp : IEquatable<DynamicDependencyStamp>
    {
        public DynamicDependencyStamp(int valueStoreVersion, int runtimeQueryVersion, int scopeVersion, int commandVersion, int sourceVersion, int extraVersion = 0)
        {
            ValueStoreVersion = valueStoreVersion;
            RuntimeQueryVersion = runtimeQueryVersion;
            ScopeVersion = scopeVersion;
            CommandVersion = commandVersion;
            SourceVersion = sourceVersion;
            ExtraVersion = extraVersion;
        }

        public int ValueStoreVersion { get; }
        public int RuntimeQueryVersion { get; }
        public int ScopeVersion { get; }
        public int CommandVersion { get; }
        public int SourceVersion { get; }
        public int ExtraVersion { get; }

        public bool IsEmpty => ValueStoreVersion == 0 && RuntimeQueryVersion == 0 && ScopeVersion == 0 && CommandVersion == 0 && SourceVersion == 0 && ExtraVersion == 0;

        public static DynamicDependencyStamp Empty => default;

        public static DynamicDependencyStamp FromContext(IDynamicContext context, int sourceVersion = 0, int extraVersion = 0)
        {
            if (context == null)
                return new DynamicDependencyStamp(0, 0, 0, 0, sourceVersion, extraVersion);

            var valueStoreVersion = context.Vars?.GlobalVersion ?? 0;
            var tokens = context is IDynamicDependencyTokenSource tokenSource
                ? tokenSource.GetDynamicDependencyTokens()
                : default;

            return new DynamicDependencyStamp(
                valueStoreVersion,
                tokens.RuntimeQueryVersion,
                tokens.ScopeVersion,
                tokens.CommandVersion,
                sourceVersion,
                extraVersion != 0 ? extraVersion : tokens.ExtraVersion);
        }

        public DynamicDependencyStamp WithExtraVersion(int extraVersion)
            => new DynamicDependencyStamp(ValueStoreVersion, RuntimeQueryVersion, ScopeVersion, CommandVersion, SourceVersion, extraVersion);

        public bool Equals(DynamicDependencyStamp other)
        {
            return ValueStoreVersion == other.ValueStoreVersion
                && RuntimeQueryVersion == other.RuntimeQueryVersion
                && ScopeVersion == other.ScopeVersion
                && CommandVersion == other.CommandVersion
                && SourceVersion == other.SourceVersion
                && ExtraVersion == other.ExtraVersion;
        }

        public override bool Equals(object obj) => obj is DynamicDependencyStamp other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ValueStoreVersion;
                hash = (hash * 397) ^ RuntimeQueryVersion;
                hash = (hash * 397) ^ ScopeVersion;
                hash = (hash * 397) ^ CommandVersion;
                hash = (hash * 397) ^ SourceVersion;
                hash = (hash * 397) ^ ExtraVersion;
                return hash;
            }
        }

        public static bool operator ==(DynamicDependencyStamp left, DynamicDependencyStamp right) => left.Equals(right);
        public static bool operator !=(DynamicDependencyStamp left, DynamicDependencyStamp right) => !left.Equals(right);
    }

    public sealed class DynamicEvaluationPlan
    {
        public DynamicEvaluationPlanId PlanId { get; set; }
        public DynamicSourceHandle RootSource { get; set; }
        public DynamicEvaluationPhase Phase { get; set; } = DynamicEvaluationPhase.ExplicitRead;
        public DynamicDependencyDeclarationMode DependencyMode { get; set; } = DynamicDependencyDeclarationMode.Tracked;
        public DynamicFallbackPolicy FallbackPolicy { get; set; } = DynamicFallbackPolicy.Forbidden;
        public DynamicCachePolicy CachePolicy { get; set; } = DynamicCachePolicy.SharedTracked;
        public bool RequirePlan { get; set; } = true;
        public string? SourceLocation { get; set; }
    }

    public sealed class ReactiveEvaluationPlan
    {
        public ReactiveEvaluationPlanId PlanId { get; set; }
        public DynamicSourceHandle RootSource { get; set; }
        public DynamicEvaluationPhase Phase { get; set; } = DynamicEvaluationPhase.Tick;
        public DynamicDependencyDeclarationMode DependencyMode { get; set; } = DynamicDependencyDeclarationMode.Tracked;
        public DynamicInvalidationPolicy Invalidation { get; set; } = DynamicInvalidationPolicy.OnDependencyStampChange;
        public DynamicCachePolicy CachePolicy { get; set; } = DynamicCachePolicy.SharedTracked;
        public DynamicSchedulingPolicy Scheduling { get; set; } = DynamicSchedulingPolicy.OnDependencyChange;
        public string? SourceLocation { get; set; }

        public DynamicEvaluationPlan ToDynamicPlan()
        {
            return new DynamicEvaluationPlan
            {
                PlanId = new DynamicEvaluationPlanId(PlanId.Value),
                RootSource = RootSource,
                Phase = Phase,
                DependencyMode = DependencyMode,
                FallbackPolicy = DynamicFallbackPolicy.Forbidden,
                CachePolicy = CachePolicy,
                RequirePlan = true,
                SourceLocation = SourceLocation,
            };
        }
    }

    public readonly struct DynamicDependencyEdge
    {
        public DynamicDependencyEdge(int parentSourceId, string parentSourceType, int childSourceId, string childSourceType, DynamicEvaluationPhase phase, DynamicDependencyStamp stamp)
        {
            ParentSourceId = parentSourceId;
            ParentSourceType = parentSourceType ?? string.Empty;
            ChildSourceId = childSourceId;
            ChildSourceType = childSourceType ?? string.Empty;
            Phase = phase;
            Stamp = stamp;
        }

        public int ParentSourceId { get; }
        public string ParentSourceType { get; }
        public int ChildSourceId { get; }
        public string ChildSourceType { get; }
        public DynamicEvaluationPhase Phase { get; }
        public DynamicDependencyStamp Stamp { get; }
    }

    public interface IDynamicEvaluationContext : IDynamicContext
    {
        DynamicEvaluationRuntime Runtime { get; }
        DynamicEvaluationPlan? Plan { get; }
        ReactiveEvaluationPlan? ReactivePlan { get; }
        DynamicEvaluationPhase Phase { get; }
        DynamicDependencyStamp DependencyStamp { get; }
        bool RequirePlan { get; }
        IDynamicEvaluationDiagnosticSink? Diagnostics { get; }
    }

    public sealed class DynamicEvaluationContext : IDynamicEvaluationContext
    {
        readonly IDynamicContext _legacyContext;

        public DynamicEvaluationContext(
            IDynamicContext legacyContext,
            DynamicEvaluationRuntime runtime,
            DynamicEvaluationPlan? plan = null,
            DynamicEvaluationPhase phase = DynamicEvaluationPhase.ExplicitRead,
            DynamicDependencyStamp? dependencyStamp = null,
            bool requirePlan = false,
            IDynamicEvaluationDiagnosticSink? diagnostics = null,
            ReactiveEvaluationPlan? reactivePlan = null)
        {
            _legacyContext = legacyContext ?? DummyDynamicContext.Instance;
            Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            Plan = plan;
            ReactivePlan = reactivePlan;
            Phase = phase;
            DependencyStamp = dependencyStamp ?? DynamicDependencyStamp.Empty;
            RequirePlan = requirePlan;
            Diagnostics = diagnostics;
        }

        public DynamicEvaluationRuntime Runtime { get; }
        public DynamicEvaluationPlan? Plan { get; }
        public ReactiveEvaluationPlan? ReactivePlan { get; }
        public DynamicEvaluationPhase Phase { get; }
        public DynamicDependencyStamp DependencyStamp { get; }
        public bool RequirePlan { get; }
        public IDynamicEvaluationDiagnosticSink? Diagnostics { get; }

        public IVarStore Vars => _legacyContext.Vars;
        public IScopeNode Scope => _legacyContext.Scope;
        public IScopeNode? CommandRootScope => _legacyContext.CommandRootScope;

        public IScopeNode ResolveOtherScope(CommandTargetIdentityFilter filter)
            => _legacyContext.ResolveOtherScope(filter);

        public DynamicEvaluationContext WithPlan(DynamicEvaluationPlan plan, bool requirePlan = true)
            => new DynamicEvaluationContext(_legacyContext, Runtime, plan, Phase, DependencyStamp, requirePlan, Diagnostics, reactivePlan: null);

        public DynamicEvaluationContext WithPhase(DynamicEvaluationPhase phase)
            => new DynamicEvaluationContext(_legacyContext, Runtime, Plan, phase, DependencyStamp, RequirePlan, Diagnostics, ReactivePlan);

        public DynamicEvaluationContext WithDependencyStamp(DynamicDependencyStamp stamp)
            => new DynamicEvaluationContext(_legacyContext, Runtime, Plan, Phase, stamp, RequirePlan, Diagnostics, ReactivePlan);

        public DynamicEvaluationContext WithReactivePlan(ReactiveEvaluationPlan plan, bool requirePlan = true)
            => new DynamicEvaluationContext(_legacyContext, Runtime, plan: null, phase: plan?.Phase ?? Phase, dependencyStamp: DependencyStamp, requirePlan: requirePlan, diagnostics: Diagnostics, reactivePlan: plan);
    }

    public sealed class DynamicEvaluationRuntime
    {
        const string PlanMissingCode = "DYN_EVAL_PLAN_MISSING";
        const string PlanConflictCode = "DYN_EVAL_PLAN_CONFLICT";
        const string PhaseMismatchCode = "DYN_EVAL_PHASE_MISMATCH";
        const string StampMissingCode = "DYN_EVAL_STAMP_MISSING";
        const string CycleDetectedCode = "DYN_EVAL_CYCLE_DETECTED";
        const string CachePolicyViolationCode = "DYN_CACHE_POLICY_VIOLATION";
        const string TrackerDegradedCode = "DYN_TRACKER_DEGRADED";
        const string SourceNotTrackableCode = "DYN_EVAL_SOURCE_NOT_TRACKABLE";

        readonly Dictionary<DynamicEvaluationCacheKey, DynamicVariant> _sharedCache = new(64);
        readonly Dictionary<DynamicTrackedPlanKey, DynamicDependencyEdge[]> _lastDependenciesByPlan = new();
        readonly Dictionary<ReactiveEvaluationPlanId, ReactiveEvaluationPlanState> _reactiveStateByPlan = new();
        readonly List<DynamicDependencyEdge> _workingDependencies = new(16);
        readonly List<DynamicEvaluationCacheKey> _cacheRemovalBuffer = new(16);
        readonly Stack<object> _evaluationStack = new();
        readonly HashSet<object> _activeSources = new(ReferenceEqualityComparer<object>.Instance);
        DynamicDependencyEdge[] _lastDependencies = Array.Empty<DynamicDependencyEdge>();

        public IReadOnlyList<DynamicDependencyEdge> LastDependencies => _lastDependencies;
        public bool IsEvaluating => _evaluationStack.Count > 0;

        public bool TryEvaluate(IDynamicSource source, IDynamicEvaluationContext context, out DynamicVariant value)
        {
            value = DynamicVariant.Null;

            if (source == null)
                return false;

            if (context.Plan != null && context.ReactivePlan != null)
            {
                Report(context, PlanConflictCode, DynamicEvaluationDiagnosticSeverity.Error, "Dynamic evaluation context must not carry both dynamic and reactive plans.", source);
                return false;
            }

            var hasPlan = TryResolveTrackedPlan(context, out var trackedPlanKey, out var plannedPhase, out var cachePolicy);

            if (context.RequirePlan && !hasPlan)
            {
                Report(context, PlanMissingCode, DynamicEvaluationDiagnosticSeverity.Error, "Dynamic evaluation requires a verified plan.", source);
                return false;
            }

            if (hasPlan && plannedPhase != DynamicEvaluationPhase.Unknown && plannedPhase != context.Phase)
            {
                Report(context, PhaseMismatchCode, DynamicEvaluationDiagnosticSeverity.Error, $"Dynamic evaluation phase mismatch. plan={plannedPhase} actual={context.Phase}.", source);
                return false;
            }

            if (hasPlan && cachePolicy == DynamicCachePolicy.None && context.RequirePlan)
            {
                Report(context, CachePolicyViolationCode, DynamicEvaluationDiagnosticSeverity.Error, "Tracked evaluation requires cache participation but the plan disabled it.", source);
                return false;
            }

            var useCache = hasPlan && cachePolicy != DynamicCachePolicy.None;
            if (useCache && context.DependencyStamp.IsEmpty)
            {
                Report(context, StampMissingCode, DynamicEvaluationDiagnosticSeverity.Error, "Tracked evaluation requires an explicit dependency stamp.", source);
                return false;
            }

            var reactiveState = context.ReactivePlan != null ? GetOrCreateReactiveState(context.ReactivePlan.PlanId) : null;
            var sourceConfigurationRevision = GetSourceConfigurationRevision(source);
            var sourceDependencyRevision = GetSourceDependencyRevision(source, context);
            if (useCache && source is IDynamicTrackedEvaluationPolicyProvider trackedPolicy && !trackedPolicy.AllowTrackedEvaluation)
            {
                Report(context, SourceNotTrackableCode, DynamicEvaluationDiagnosticSeverity.Error, "Tracked evaluation is not allowed for this source because it is nondeterministic or otherwise unsuitable for shared caching.", source);
                return false;
            }

            var cacheKey = useCache
                ? new DynamicEvaluationCacheKey(
                    source,
                    trackedPlanKey,
                    context.Phase,
                    DynamicEvaluationOrigin.FromContext(context),
                    context.DependencyStamp,
                    sourceConfigurationRevision,
                    sourceDependencyRevision,
                    reactiveState?.InvalidationRevision ?? 0)
                : default;

            if (useCache && _sharedCache.TryGetValue(cacheKey, out value))
                return true;

            if (!_activeSources.Add(source))
            {
                Report(context, CycleDetectedCode, DynamicEvaluationDiagnosticSeverity.Error, "Dynamic evaluation cycle detected.", source);
                return false;
            }

            var isRoot = _evaluationStack.Count == 0;
            if (isRoot)
                _workingDependencies.Clear();

            if (_evaluationStack.Count > 0)
            {
                var parent = _evaluationStack.Peek();
                _workingDependencies.Add(new DynamicDependencyEdge(
                    RuntimeHelpers.GetHashCode(parent),
                    parent.GetType().Name,
                    RuntimeHelpers.GetHashCode(source),
                    source.GetType().Name,
                    context.Phase,
                    context.DependencyStamp));
            }

            _evaluationStack.Push(source);
            try
            {
                value = source.Evaluate(context);
                if (useCache)
                    _sharedCache[cacheKey] = value;

                if (reactiveState != null && isRoot)
                    reactiveState.MarkEvaluated();

                return true;
            }
            catch (Exception exception)
            {
                Report(context, TrackerDegradedCode, DynamicEvaluationDiagnosticSeverity.Error, exception.Message, source, exception);
                value = DynamicVariant.Null;
                return false;
            }
            finally
            {
                _evaluationStack.Pop();
                _activeSources.Remove(source);

                if (isRoot)
                {
                    _lastDependencies = _workingDependencies.Count == 0 ? Array.Empty<DynamicDependencyEdge>() : _workingDependencies.ToArray();
                    if (hasPlan)
                        _lastDependenciesByPlan[trackedPlanKey] = _lastDependencies;
                }
            }
        }

        public bool TryEvaluateReactive(
            IDynamicSource source,
            IDynamicContext baseContext,
            ReactiveEvaluationPlan plan,
            out DynamicVariant value,
            int sourceVersion = 0,
            IDynamicEvaluationDiagnosticSink? diagnostics = null)
        {
            if (plan == null)
            {
                value = DynamicVariant.Null;
                return false;
            }

            var stamp = DynamicDependencyStamp.FromContext(baseContext, sourceVersion);
            var context = new DynamicEvaluationContext(baseContext, this, plan: null, phase: plan.Phase, dependencyStamp: stamp, requirePlan: true, diagnostics: diagnostics, reactivePlan: plan);
            return TryEvaluate(source, context, out value);
        }

        public void InvalidateAll()
        {
            _sharedCache.Clear();
        }

        public void InvalidatePlan(DynamicEvaluationPlanId planId)
        {
            var trackedPlanKey = DynamicTrackedPlanKey.From(planId);
            if (_sharedCache.Count == 0)
            {
                _lastDependenciesByPlan.Remove(trackedPlanKey);
                return;
            }

            _cacheRemovalBuffer.Clear();
            foreach (var entry in _sharedCache)
            {
                if (entry.Key.PlanKey == trackedPlanKey)
                    _cacheRemovalBuffer.Add(entry.Key);
            }

            for (int i = 0; i < _cacheRemovalBuffer.Count; i++)
                _sharedCache.Remove(_cacheRemovalBuffer[i]);

            _lastDependenciesByPlan.Remove(trackedPlanKey);
        }

        public void InvalidateReactivePlan(ReactiveEvaluationPlanId planId)
        {
            var trackedPlanKey = DynamicTrackedPlanKey.From(planId);
            var state = GetOrCreateReactiveState(planId);
            state.Invalidate();

            if (_sharedCache.Count != 0)
            {
                _cacheRemovalBuffer.Clear();
                foreach (var entry in _sharedCache)
                {
                    if (entry.Key.PlanKey == trackedPlanKey)
                        _cacheRemovalBuffer.Add(entry.Key);
                }

                for (int i = 0; i < _cacheRemovalBuffer.Count; i++)
                    _sharedCache.Remove(_cacheRemovalBuffer[i]);
            }

            _lastDependenciesByPlan.Remove(trackedPlanKey);
        }

        public void ClearLastDependencies()
        {
            _lastDependencies = Array.Empty<DynamicDependencyEdge>();
            _lastDependenciesByPlan.Clear();
        }

        public bool TryGetLastDependencies(DynamicEvaluationPlanId planId, out IReadOnlyList<DynamicDependencyEdge> dependencies)
        {
            if (_lastDependenciesByPlan.TryGetValue(DynamicTrackedPlanKey.From(planId), out var snapshot))
            {
                dependencies = snapshot;
                return true;
            }

            dependencies = Array.Empty<DynamicDependencyEdge>();
            return false;
        }

        public bool TryGetLastDependencies(ReactiveEvaluationPlanId planId, out IReadOnlyList<DynamicDependencyEdge> dependencies)
        {
            if (_lastDependenciesByPlan.TryGetValue(DynamicTrackedPlanKey.From(planId), out var snapshot))
            {
                dependencies = snapshot;
                return true;
            }

            dependencies = Array.Empty<DynamicDependencyEdge>();
            return false;
        }

        static bool TryResolveTrackedPlan(
            IDynamicEvaluationContext context,
            out DynamicTrackedPlanKey planKey,
            out DynamicEvaluationPhase planPhase,
            out DynamicCachePolicy cachePolicy)
        {
            if (context.ReactivePlan != null)
            {
                planKey = DynamicTrackedPlanKey.From(context.ReactivePlan.PlanId);
                planPhase = context.ReactivePlan.Phase;
                cachePolicy = context.ReactivePlan.CachePolicy;
                return true;
            }

            if (context.Plan != null)
            {
                planKey = DynamicTrackedPlanKey.From(context.Plan.PlanId);
                planPhase = context.Plan.Phase;
                cachePolicy = context.Plan.CachePolicy;
                return true;
            }

            planKey = default;
            planPhase = DynamicEvaluationPhase.Unknown;
            cachePolicy = DynamicCachePolicy.None;
            return false;
        }

        ReactiveEvaluationPlanState GetOrCreateReactiveState(ReactiveEvaluationPlanId planId)
        {
            if (_reactiveStateByPlan.TryGetValue(planId, out var state))
                return state;

            state = new ReactiveEvaluationPlanState();
            _reactiveStateByPlan.Add(planId, state);
            return state;
        }

        static int GetSourceConfigurationRevision(IDynamicSource source)
        {
            if (source is IDynamicSourceConfigurationRevisionProvider revisionProvider)
                return revisionProvider.GetSourceConfigurationRevision();

            return 0;
        }

        static int GetSourceDependencyRevision(IDynamicSource source, IDynamicContext context)
        {
            if (source is IDynamicSourceDependencyRevisionProvider revisionProvider)
                return revisionProvider.GetSourceDependencyRevision(context);

            return 0;
        }

        void Report(
            IDynamicEvaluationContext context,
            string code,
            DynamicEvaluationDiagnosticSeverity severity,
            string message,
            IDynamicSource source,
            Exception exception = null)
        {
            if (context.Diagnostics == null)
                return;

            context.Diagnostics.Report(new DynamicEvaluationDiagnostic(
                code,
                severity,
                message,
                context.Phase.ToString(),
                source.GetType().Name,
                context.Plan != null ? context.Plan.PlanId.Value.ToString() : null,
                context.Plan != null ? context.Plan.RootSource.Value.ToString() : null,
                context.Plan?.SourceLocation,
                exception));
        }

        readonly struct DynamicEvaluationCacheKey : IEquatable<DynamicEvaluationCacheKey>
        {
            readonly object _source;
            readonly DynamicTrackedPlanKey _planKey;
            readonly DynamicEvaluationPhase _phase;
            readonly DynamicEvaluationOrigin _origin;
            readonly DynamicDependencyStamp _stamp;
            readonly int _sourceConfigurationRevision;
            readonly int _sourceDependencyRevision;
            readonly int _reactiveInvalidationRevision;

            public DynamicTrackedPlanKey PlanKey => _planKey;

            public DynamicEvaluationCacheKey(
                object source,
                DynamicTrackedPlanKey planKey,
                DynamicEvaluationPhase phase,
                DynamicEvaluationOrigin origin,
                DynamicDependencyStamp stamp,
                int sourceConfigurationRevision,
                int sourceDependencyRevision,
                int reactiveInvalidationRevision)
            {
                _source = source;
                _planKey = planKey;
                _phase = phase;
                _origin = origin;
                _stamp = stamp;
                _sourceConfigurationRevision = sourceConfigurationRevision;
                _sourceDependencyRevision = sourceDependencyRevision;
                _reactiveInvalidationRevision = reactiveInvalidationRevision;
            }

            public bool Equals(DynamicEvaluationCacheKey other)
            {
                return ReferenceEquals(_source, other._source)
                    && _planKey == other._planKey
                    && _phase == other._phase
                    && _origin == other._origin
                    && _stamp == other._stamp
                    && _sourceConfigurationRevision == other._sourceConfigurationRevision
                    && _sourceDependencyRevision == other._sourceDependencyRevision
                    && _reactiveInvalidationRevision == other._reactiveInvalidationRevision;
            }

            public override bool Equals(object obj) => obj is DynamicEvaluationCacheKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = _source != null ? RuntimeHelpers.GetHashCode(_source) : 0;
                    hash = (hash * 397) ^ _planKey.GetHashCode();
                    hash = (hash * 397) ^ (int)_phase;
                    hash = (hash * 397) ^ _origin.GetHashCode();
                    hash = (hash * 397) ^ _stamp.GetHashCode();
                    hash = (hash * 397) ^ _sourceConfigurationRevision;
                    hash = (hash * 397) ^ _sourceDependencyRevision;
                    hash = (hash * 397) ^ _reactiveInvalidationRevision;
                    return hash;
                }
            }
        }

        sealed class ReactiveEvaluationPlanState
        {
            int _invalidationRevision;
            int _resultRevision;

            public int InvalidationRevision => _invalidationRevision;
            public int ResultRevision => _resultRevision;

            public void Invalidate()
            {
                unchecked
                {
                    _invalidationRevision = _invalidationRevision == int.MaxValue ? 1 : _invalidationRevision + 1;
                    _resultRevision = _resultRevision == int.MaxValue ? 1 : _resultRevision + 1;
                }
            }

            public void MarkEvaluated()
            {
                unchecked
                {
                    _resultRevision = _resultRevision == int.MaxValue ? 1 : _resultRevision + 1;
                }
            }
        }
    }
}
