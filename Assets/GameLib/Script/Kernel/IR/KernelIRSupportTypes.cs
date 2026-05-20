#nullable enable
using System;

namespace Game.Kernel.IR
{
    public enum ModuleKind
    {
        Unknown = 0,
        Feature = 10,
        Content = 20,
        Bridge = 30,
        System = 40,
        MigrationAdapter = 50,
    }

    public enum LegacyCompatKind
    {
        None = 0,
        AuthoringMigration = 10,
        DataMigration = 20,
        RuntimeAdapter = 30,
        DiagnosticAdapter = 40,
        TestAdapter = 50,
        TemporaryBridge = 60,
        ForbiddenFallback = 90,
    }

    public enum LegacyRemovalStatus
    {
        Unknown = 0,
        Temporary = 10,
        MigrationOnly = 20,
        TestOnly = 30,
        Deprecated = 40,
        Forbidden = 90,
    }

    public enum LegacyAdapterSurface
    {
        None = 0,
        Installer = 10,
        Resolver = 20,
        Command = 30,
        Value = 40,
        Lifecycle = 50,
        Authoring = 60,
    }

    public readonly struct ModuleVersion : IEquatable<ModuleVersion>
    {
        public ModuleVersion(int value)
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "ModuleVersion must be positive.");

            Value = value;
        }

        public int Value { get; }

        public bool Equals(ModuleVersion other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is ModuleVersion other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "ModuleVersion(" + Value + ")";
        }

        public static bool operator ==(ModuleVersion left, ModuleVersion right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ModuleVersion left, ModuleVersion right)
        {
            return !left.Equals(right);
        }
    }

    [Flags]
    public enum KernelProfileMask
    {
        None = 0,
        Development = 1 << 0,
        Release = 1 << 1,
        Test = 1 << 2,
        Editor = 1 << 3,
        All = Development | Release | Test | Editor,
    }

    public enum ServiceLifetimeKind
    {
        Unknown = 0,
        Kernel = 10,
        Singleton = 10,
        Project = 20,
        Scene = 30,
        Scope = 40,
        Scoped = 40,
        ExplicitTransient = 50,
        Transient = 50,
    }

    public enum ServiceCardinalityKind
    {
        Unknown = 0,
        SingletonGlobal = 10,
        OnePerProject = 20,
        OnePerScene = 30,
        OnePerAuthoredScope = 40,
        BoundedPool = 50,
        UnboundedRuntime = 90,
    }

    public enum ServiceFactoryKind
    {
        Unknown = 0,
        GeneratedFactory = 10,
        ProvidedInstance = 20,
        LegacyAdapter = 30,
    }

    public enum ScopeKind
    {
        Unknown = 0,
        Root = 10,
        Child = 20,
        Dynamic = 30,
        Detached = 40,
    }

    public enum ScopeServiceBoundaryKind
    {
        Unknown = 0,
        Detached = 10,
        OwnedLocal = 20,
        ReferencesParent = 30,
    }

    public enum ValueKind
    {
        Null = 0,
        Bool = 10,
        Int = 20,
        Long = 30,
        Float = 40,
        Double = 50,
        String = 60,
        Vector2 = 70,
        Vector3 = 80,
        Color = 90,
        ObjectRef = 100,
        ManagedRef = 110,
        Record = 200,
        RecordList = 210,
        Table = 220,
        LayeredNumeric = 300,
    }

    public enum ValueInitEntrySourceKind
    {
        Unknown = 0,
        Literal = 10,
        DynamicEvaluation = 20,
        ReactiveEvaluation = 30,
    }

    public static class ValueInitEntrySourceKindUtilities
    {
        public static bool IsDefined(ValueInitEntrySourceKind kind)
        {
            switch (kind)
            {
                case ValueInitEntrySourceKind.Literal:
                case ValueInitEntrySourceKind.DynamicEvaluation:
                case ValueInitEntrySourceKind.ReactiveEvaluation:
                    return true;

                default:
                    return false;
            }
        }
    }

    public enum ValueInitOverwritePolicy
    {
        ErrorIfExists = 10,
        KeepExisting = 20,
        Overwrite = 30,
        ClearIfNull = 40,
        Merge = 50,
    }

    public static class ValueInitOverwritePolicyUtilities
    {
        public static bool IsDefined(ValueInitOverwritePolicy policy)
        {
            switch (policy)
            {
                case ValueInitOverwritePolicy.ErrorIfExists:
                case ValueInitOverwritePolicy.KeepExisting:
                case ValueInitOverwritePolicy.Overwrite:
                case ValueInitOverwritePolicy.ClearIfNull:
                case ValueInitOverwritePolicy.Merge:
                    return true;

                default:
                    return false;
            }
        }
    }

    public enum RuntimeQueryTargetKind
    {
        Unknown = 0,
        Service = 10,
        Scope = 20,
        ValueKey = 30,
        RuntimeObjectOwner = 40,
        LegacyAdapter = 90,
    }

    public enum LifecyclePhase
    {
        Boot = 10,
        Create = 20,
        Build = 30,
        Acquire = 40,
        Activate = 50,
        Tick = 60,
        FixedTick = 70,
        LateTick = 80,
        PreRelease = 90,
        Release = 100,
        Reset = 110,
        Destroy = 120,
        Dispose = 130,
    }

    public enum LifecycleActionKind
    {
        Unknown = 0,
        ServiceMethod = 10,
        GeneratedStaticCall = 20,
        ScopeStateTransition = 30,
        RuntimeObjectOwnerCall = 40,
        ValueInit = 50,
        RuntimeQueryNotify = 60,
        LegacyAdapterCall = 90,
    }

    public enum LifecycleTickCardinalityKind
    {
        Unknown = 0,
        Hub = 10,
        PerEntity = 20,
    }

    public static class LifecycleTickCardinalityKindUtilities
    {
        public static bool IsDefined(LifecycleTickCardinalityKind cardinality)
        {
            switch (cardinality)
            {
                case LifecycleTickCardinalityKind.Hub:
                case LifecycleTickCardinalityKind.PerEntity:
                    return true;

                default:
                    return false;
            }
        }
    }

    public enum LifecycleExecutionModeKind
    {
        Unknown = 0,
        Synchronous = 10,
        TrackedAsync = 20,
    }

    public static class LifecycleExecutionModeKindUtilities
    {
        public static bool IsDefined(LifecycleExecutionModeKind mode)
        {
            switch (mode)
            {
                case LifecycleExecutionModeKind.Synchronous:
                case LifecycleExecutionModeKind.TrackedAsync:
                    return true;

                default:
                    return false;
            }
        }
    }

    public enum LifecycleAsyncCancellationSourceKind
    {
        Unknown = 0,
        DispatcherOwned = 10,
        LinkedToCaller = 20,
    }

    public static class LifecycleAsyncCancellationSourceKindUtilities
    {
        public static bool IsDefined(LifecycleAsyncCancellationSourceKind cancellationSourceKind)
        {
            switch (cancellationSourceKind)
            {
                case LifecycleAsyncCancellationSourceKind.DispatcherOwned:
                case LifecycleAsyncCancellationSourceKind.LinkedToCaller:
                    return true;

                default:
                    return false;
            }
        }
    }

    public enum LifecycleAsyncTimeoutPolicyKind
    {
        Unknown = 0,
        None = 10,
        DurationMilliseconds = 20,
    }

    public static class LifecycleAsyncTimeoutPolicyKindUtilities
    {
        public static bool IsDefined(LifecycleAsyncTimeoutPolicyKind timeoutPolicyKind)
        {
            switch (timeoutPolicyKind)
            {
                case LifecycleAsyncTimeoutPolicyKind.None:
                case LifecycleAsyncTimeoutPolicyKind.DurationMilliseconds:
                    return true;

                default:
                    return false;
            }
        }
    }

    public enum LifecycleAsyncCompletionRequirementKind
    {
        Unknown = 0,
        BeforeNextStep = 10,
        BeforePhaseExit = 20,
    }

    public static class LifecycleAsyncCompletionRequirementKindUtilities
    {
        public static bool IsDefined(LifecycleAsyncCompletionRequirementKind completionRequirementKind)
        {
            switch (completionRequirementKind)
            {
                case LifecycleAsyncCompletionRequirementKind.BeforeNextStep:
                case LifecycleAsyncCompletionRequirementKind.BeforePhaseExit:
                    return true;

                default:
                    return false;
            }
        }
    }

    public enum LifecycleFailurePolicy
    {
        Unknown = 0,
        FailOperation = 10,
        FailScope = 20,
        FailScene = 30,
        FailKernel = 40,
        ContinueWithError = 50,
    }

    public static class LifecycleFailurePolicyUtilities
    {
        public static bool IsDefined(LifecycleFailurePolicy policy)
        {
            switch (policy)
            {
                case LifecycleFailurePolicy.FailOperation:
                case LifecycleFailurePolicy.FailScope:
                case LifecycleFailurePolicy.FailScene:
                case LifecycleFailurePolicy.FailKernel:
                case LifecycleFailurePolicy.ContinueWithError:
                    return true;

                default:
                    return false;
            }
        }
    }

    public enum LifecycleAcquireRollbackPolicy
    {
        Unknown = 0,
        None = 10,
        ReverseCompletedAcquireSteps = 20,
    }

    public static class LifecycleAcquireRollbackPolicyUtilities
    {
        public static bool IsDefined(LifecycleAcquireRollbackPolicy policy)
        {
            switch (policy)
            {
                case LifecycleAcquireRollbackPolicy.None:
                case LifecycleAcquireRollbackPolicy.ReverseCompletedAcquireSteps:
                    return true;

                default:
                    return false;
            }
        }
    }

    public enum LifecycleTargetKind
    {
        Unknown = 0,
        Service = 10,
        Scope = 20,
        ValueStore = 30,
        RuntimeQuery = 40,
        RuntimeObjectOwner = 50,
        LegacyAdapter = 90,
    }

    public readonly struct Hash128 : IEquatable<Hash128>
    {
        public Hash128(uint a, uint b, uint c, uint d)
        {
            A = a;
            B = b;
            C = c;
            D = d;
        }

        public uint A { get; }

        public uint B { get; }

        public uint C { get; }

        public uint D { get; }

        public bool IsZero => A == 0 && B == 0 && C == 0 && D == 0;

        public bool Equals(Hash128 other)
        {
            return A == other.A && B == other.B && C == other.C && D == other.D;
        }

        public override bool Equals(object? obj)
        {
            return obj is Hash128 other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)A;
                hash = (hash * 397) ^ (int)B;
                hash = (hash * 397) ^ (int)C;
                hash = (hash * 397) ^ (int)D;
                return hash;
            }
        }

        public override string ToString()
        {
            return A.ToString("x8") + B.ToString("x8") + C.ToString("x8") + D.ToString("x8");
        }

        public static bool operator ==(Hash128 left, Hash128 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Hash128 left, Hash128 right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct AvailabilityIR : IEquatable<AvailabilityIR>
    {
        public AvailabilityIR(KernelProfileMask profiles, bool enabledByDefault, string? condition)
        {
            if (condition != null && condition.Trim().Length == 0)
                throw new ArgumentException("Availability conditions must be null or non-empty.", nameof(condition));

            Profiles = profiles;
            EnabledByDefault = enabledByDefault;
            Condition = condition;
        }

        public KernelProfileMask Profiles { get; }

        public bool EnabledByDefault { get; }

        public string? Condition { get; }

        public bool Equals(AvailabilityIR other)
        {
            return Profiles == other.Profiles
                && EnabledByDefault == other.EnabledByDefault
                && StringComparer.Ordinal.Equals(Condition, other.Condition);
        }

        public override bool Equals(object? obj)
        {
            return obj is AvailabilityIR other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Profiles;
                hash = (hash * 397) ^ EnabledByDefault.GetHashCode();
                hash = (hash * 397) ^ (Condition == null ? 0 : StringComparer.Ordinal.GetHashCode(Condition));
                return hash;
            }
        }

        public override string ToString()
        {
            return "AvailabilityIR(Profiles=" + Profiles + ", EnabledByDefault=" + EnabledByDefault + ", Condition=" + (Condition ?? "<none>") + ")";
        }

        public static bool operator ==(AvailabilityIR left, AvailabilityIR right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AvailabilityIR left, AvailabilityIR right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct ModuleAvailabilityIR : IEquatable<ModuleAvailabilityIR>
    {
        public ModuleAvailabilityIR(AvailabilityIR value)
        {
            Value = value;
        }

        public AvailabilityIR Value { get; }

        public bool Equals(ModuleAvailabilityIR other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object? obj)
        {
            return obj is ModuleAvailabilityIR other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return "ModuleAvailabilityIR(" + Value + ")";
        }

        public static bool operator ==(ModuleAvailabilityIR left, ModuleAvailabilityIR right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ModuleAvailabilityIR left, ModuleAvailabilityIR right)
        {
            return !left.Equals(right);
        }
    }

    public sealed class KernelProfileIR
    {
        public KernelProfileIR(string id, KernelProfileMask mask, AvailabilityIR availability)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Kernel profiles must provide a stable identifier.", nameof(id));

            Id = id;
            Mask = mask;
            Availability = availability;
        }

        public string Id { get; }

        public KernelProfileMask Mask { get; }

        public AvailabilityIR Availability { get; }
    }

    public readonly struct CommandCategoryId : IEquatable<CommandCategoryId>
    {
        public CommandCategoryId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(CommandCategoryId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is CommandCategoryId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "CommandCategoryId(" + Value + ")";
        }

        public static bool operator ==(CommandCategoryId left, CommandCategoryId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CommandCategoryId left, CommandCategoryId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct ValueInitPlanId : IEquatable<ValueInitPlanId>
    {
        public ValueInitPlanId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(ValueInitPlanId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is ValueInitPlanId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "ValueInitPlanId(" + Value + ")";
        }

        public static bool operator ==(ValueInitPlanId left, ValueInitPlanId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ValueInitPlanId left, ValueInitPlanId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct LifecyclePlanId : IEquatable<LifecyclePlanId>
    {
        public LifecyclePlanId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(LifecyclePlanId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is LifecyclePlanId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "LifecyclePlanId(" + Value + ")";
        }

        public static bool operator ==(LifecyclePlanId left, LifecyclePlanId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(LifecyclePlanId left, LifecyclePlanId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct DependencyEdgeId : IEquatable<DependencyEdgeId>
    {
        public DependencyEdgeId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(DependencyEdgeId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is DependencyEdgeId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "DependencyEdgeId(" + Value + ")";
        }

        public static bool operator ==(DependencyEdgeId left, DependencyEdgeId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DependencyEdgeId left, DependencyEdgeId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct DiagnosticSeedIR : IEquatable<DiagnosticSeedIR>
    {
        public DiagnosticSeedIR(string seedKey, string debugName, ModuleId ownerModule, SourceLocationId source)
        {
            if (string.IsNullOrWhiteSpace(seedKey))
                throw new ArgumentException("Diagnostic seeds must provide a stable seed key.", nameof(seedKey));

            if (string.IsNullOrWhiteSpace(debugName))
                throw new ArgumentException("Diagnostic seeds must provide a debug name.", nameof(debugName));

            if (ownerModule.Value == 0)
                throw new ArgumentException("Diagnostic seeds must provide a non-zero owner module identity.", nameof(ownerModule));

            if (source.Value == 0)
                throw new ArgumentException("Diagnostic seeds must provide a non-zero source location identity.", nameof(source));

            SeedKey = seedKey;
            DebugName = debugName;
            OwnerModule = ownerModule;
            Source = source;
        }

        public string SeedKey { get; }

        public string DebugName { get; }

        public ModuleId OwnerModule { get; }

        public SourceLocationId Source { get; }

        public bool Equals(DiagnosticSeedIR other)
        {
            return StringComparer.Ordinal.Equals(SeedKey, other.SeedKey)
                && StringComparer.Ordinal.Equals(DebugName, other.DebugName)
                && OwnerModule == other.OwnerModule
                && Source == other.Source;
        }

        public override bool Equals(object? obj)
        {
            return obj is DiagnosticSeedIR other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(SeedKey);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(DebugName);
                hash = (hash * 397) ^ OwnerModule.GetHashCode();
                hash = (hash * 397) ^ Source.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return "DiagnosticSeedIR(SeedKey=" + SeedKey + ", DebugName=" + DebugName + ", OwnerModule=" + OwnerModule + ", Source=" + Source + ")";
        }
    }
}
