#nullable enable
using System;

namespace Game.Kernel.IR
{
    public enum DependencyNodeKind
    {
        Unknown = 0,
        Module = 10,
        Service = 20,
        Scope = 30,
        Command = 40,
        ValueKey = 50,
        LifecycleStep = 60,
        RuntimeQuery = 70,
    }

    public enum DependencyKind
    {
        Unknown = 0,
        Requires = 10,
        Owns = 20,
        Produces = 30,
        Consumes = 40,
        Triggers = 50,
        References = 60,
    }

    public enum DependencyPhase
    {
        Build = 10,
        Generate = 20,
        Boot = 30,
        Acquire = 40,
        Runtime = 50,
        Save = 60,
        EditorOnly = 70,
    }

    public enum DependencyStrength
    {
        Required = 10,
        Optional = 20,
        Weak = 30,
        DiagnosticOnly = 40,
    }

    public enum OptionalDependencyAbsenceBehavior
    {
        DisableContribution = 10,
        EmitWarning = 20,
        UseExplicitAlternative = 30,
        ProfileSpecificError = 40,
    }

    public enum RuntimeCycleMediationKind
    {
        None = 0,
        LazyHandle = 10,
        EventChannel = 20,
        RuntimeQuery = 30,
    }

    public readonly struct DependencyNodeIR : IEquatable<DependencyNodeIR>
    {
        public DependencyNodeIR(ModuleId moduleId)
            : this(DependencyNodeKind.Module, moduleId, default, default, default, default, default, default)
        {
        }

        public DependencyNodeIR(ServiceId serviceId)
            : this(DependencyNodeKind.Service, default, serviceId, default, default, default, default, default)
        {
        }

        public DependencyNodeIR(ScopePlanId scopePlanId)
            : this(DependencyNodeKind.Scope, default, default, scopePlanId, default, default, default, default)
        {
        }

        public DependencyNodeIR(CommandTypeId commandTypeId)
            : this(DependencyNodeKind.Command, default, default, default, commandTypeId, default, default, default)
        {
        }

        public DependencyNodeIR(ValueKeyId valueKeyId)
            : this(DependencyNodeKind.ValueKey, default, default, default, default, valueKeyId, default, default)
        {
        }

        public DependencyNodeIR(LifecycleStepId lifecycleStepId)
            : this(DependencyNodeKind.LifecycleStep, default, default, default, default, default, lifecycleStepId, default)
        {
        }

        public DependencyNodeIR(RuntimeQueryId runtimeQueryId)
            : this(DependencyNodeKind.RuntimeQuery, default, default, default, default, default, default, runtimeQueryId)
        {
        }

        DependencyNodeIR(
            DependencyNodeKind kind,
            ModuleId moduleId,
            ServiceId serviceId,
            ScopePlanId scopePlanId,
            CommandTypeId commandTypeId,
            ValueKeyId valueKeyId,
            LifecycleStepId lifecycleStepId,
            RuntimeQueryId runtimeQueryId)
        {
            Validate(kind, moduleId, serviceId, scopePlanId, commandTypeId, valueKeyId, lifecycleStepId, runtimeQueryId);
            Kind = kind;
            ModuleId = moduleId;
            ServiceId = serviceId;
            ScopePlanId = scopePlanId;
            CommandTypeId = commandTypeId;
            ValueKeyId = valueKeyId;
            LifecycleStepId = lifecycleStepId;
            RuntimeQueryId = runtimeQueryId;
        }

        public DependencyNodeKind Kind { get; }

        public ModuleId ModuleId { get; }

        public ServiceId ServiceId { get; }

        public ScopePlanId ScopePlanId { get; }

        public CommandTypeId CommandTypeId { get; }

        public ValueKeyId ValueKeyId { get; }

        public LifecycleStepId LifecycleStepId { get; }

        public RuntimeQueryId RuntimeQueryId { get; }

        public bool Equals(DependencyNodeIR other)
        {
            return Kind == other.Kind
                && ModuleId == other.ModuleId
                && ServiceId == other.ServiceId
                && ScopePlanId == other.ScopePlanId
                && CommandTypeId == other.CommandTypeId
                && ValueKeyId == other.ValueKeyId
                && LifecycleStepId == other.LifecycleStepId
                && RuntimeQueryId == other.RuntimeQueryId;
        }

        public override bool Equals(object? obj)
        {
            return obj is DependencyNodeIR other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ ModuleId.GetHashCode();
                hash = (hash * 397) ^ ServiceId.GetHashCode();
                hash = (hash * 397) ^ ScopePlanId.GetHashCode();
                hash = (hash * 397) ^ CommandTypeId.GetHashCode();
                hash = (hash * 397) ^ ValueKeyId.GetHashCode();
                hash = (hash * 397) ^ LifecycleStepId.GetHashCode();
                hash = (hash * 397) ^ RuntimeQueryId.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case DependencyNodeKind.Module:
                    return "DependencyNodeIR(Module, " + ModuleId + ")";
                case DependencyNodeKind.Service:
                    return "DependencyNodeIR(Service, " + ServiceId + ")";
                case DependencyNodeKind.Scope:
                    return "DependencyNodeIR(Scope, " + ScopePlanId + ")";
                case DependencyNodeKind.Command:
                    return "DependencyNodeIR(Command, " + CommandTypeId + ")";
                case DependencyNodeKind.ValueKey:
                    return "DependencyNodeIR(ValueKey, " + ValueKeyId + ")";
                case DependencyNodeKind.LifecycleStep:
                    return "DependencyNodeIR(LifecycleStep, " + LifecycleStepId + ")";
                case DependencyNodeKind.RuntimeQuery:
                    return "DependencyNodeIR(RuntimeQuery, " + RuntimeQueryId + ")";
                default:
                    return "DependencyNodeIR(<invalid>)";
            }
        }

        public static bool operator ==(DependencyNodeIR left, DependencyNodeIR right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DependencyNodeIR left, DependencyNodeIR right)
        {
            return !left.Equals(right);
        }

        static void Validate(
            DependencyNodeKind kind,
            ModuleId moduleId,
            ServiceId serviceId,
            ScopePlanId scopePlanId,
            CommandTypeId commandTypeId,
            ValueKeyId valueKeyId,
            LifecycleStepId lifecycleStepId,
            RuntimeQueryId runtimeQueryId)
        {
            int populatedCount = 0;
            populatedCount += moduleId.Value != 0 ? 1 : 0;
            populatedCount += serviceId.Value != 0 ? 1 : 0;
            populatedCount += scopePlanId.Value != 0 ? 1 : 0;
            populatedCount += commandTypeId.Value != 0 ? 1 : 0;
            populatedCount += valueKeyId.Value != 0 ? 1 : 0;
            populatedCount += lifecycleStepId.Value != 0 ? 1 : 0;
            populatedCount += runtimeQueryId.Value != 0 ? 1 : 0;

            if (kind == DependencyNodeKind.Unknown)
                throw new ArgumentException("Dependency nodes must provide a node kind.", nameof(kind));

            if (populatedCount != 1)
                throw new ArgumentException("Dependency nodes must provide exactly one typed endpoint.", nameof(kind));

            switch (kind)
            {
                case DependencyNodeKind.Module when moduleId.Value != 0:
                case DependencyNodeKind.Service when serviceId.Value != 0:
                case DependencyNodeKind.Scope when scopePlanId.Value != 0:
                case DependencyNodeKind.Command when commandTypeId.Value != 0:
                case DependencyNodeKind.ValueKey when valueKeyId.Value != 0:
                case DependencyNodeKind.LifecycleStep when lifecycleStepId.Value != 0:
                case DependencyNodeKind.RuntimeQuery when runtimeQueryId.Value != 0:
                    return;
                default:
                    throw new ArgumentException("Dependency nodes must match their typed endpoint to the declared node kind.", nameof(kind));
            }
        }
    }

    public readonly struct DependencyEdgeIR : IEquatable<DependencyEdgeIR>
    {
        public DependencyEdgeIR(
            DependencyEdgeId id,
            DependencyNodeIR from,
            DependencyNodeIR to,
            DependencyKind kind,
            DependencyPhase phase,
            DependencyStrength strength,
            SourceLocationId source,
            RuntimeCycleMediationKind runtimeCycleMediation = RuntimeCycleMediationKind.None)
        {
            if (id.Value == 0)
                throw new ArgumentException("Dependency edges must provide a non-zero identity.", nameof(id));

            if (kind == DependencyKind.Unknown)
                throw new ArgumentException("Dependency edges must provide a dependency kind.", nameof(kind));

            if (source.Value == 0)
                throw new ArgumentException("Dependency edges must provide a non-zero source location identity.", nameof(source));

            if (phase != DependencyPhase.Runtime && runtimeCycleMediation != RuntimeCycleMediationKind.None)
                throw new ArgumentException("Runtime cycle mediation metadata is valid only on Runtime dependency edges.", nameof(runtimeCycleMediation));

            Id = id;
            From = from;
            To = to;
            Kind = kind;
            Phase = phase;
            Strength = strength;
            Source = source;
            RuntimeCycleMediation = runtimeCycleMediation;
        }

        public DependencyEdgeId Id { get; }

        public DependencyNodeIR From { get; }

        public DependencyNodeIR To { get; }

        public DependencyKind Kind { get; }

        public DependencyPhase Phase { get; }

        public DependencyStrength Strength { get; }

        public SourceLocationId Source { get; }

        public RuntimeCycleMediationKind RuntimeCycleMediation { get; }

        public bool Equals(DependencyEdgeIR other)
        {
            return Id == other.Id
                && From == other.From
                && To == other.To
                && Kind == other.Kind
                && Phase == other.Phase
                && Strength == other.Strength
                && Source == other.Source
                && RuntimeCycleMediation == other.RuntimeCycleMediation;
        }

        public override bool Equals(object? obj)
        {
            return obj is DependencyEdgeIR other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Id.GetHashCode();
                hash = (hash * 397) ^ From.GetHashCode();
                hash = (hash * 397) ^ To.GetHashCode();
                hash = (hash * 397) ^ (int)Kind;
                hash = (hash * 397) ^ (int)Phase;
                hash = (hash * 397) ^ (int)Strength;
                hash = (hash * 397) ^ Source.GetHashCode();
                hash = (hash * 397) ^ (int)RuntimeCycleMediation;
                return hash;
            }
        }

        public override string ToString()
        {
            return "DependencyEdgeIR(Id=" + Id + ", From=" + From + ", To=" + To + ", Kind=" + Kind + ", Phase=" + Phase + ", Strength=" + Strength + ", Source=" + Source + ", RuntimeCycleMediation=" + RuntimeCycleMediation + ")";
        }
    }
}