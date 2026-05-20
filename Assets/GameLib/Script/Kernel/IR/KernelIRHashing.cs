#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Game.Kernel.IR
{
    public sealed class KernelIRDumpReport
    {
        public KernelIRDumpReport(
            string documentId,
            int formatVersion,
            string projectName,
            string profileId,
            string generatorVersion,
            KernelProfileMask profileMask,
            Hash128 sourceHash,
            Hash128 normalizedHash,
            string[] sources,
            string[] modules,
            string[] scopes,
            string[] services,
            string[] commands,
            string[] valueKeys,
            string[] valueInitPlans,
            string[] lifecycles,
            string[] runtimeQueries,
            string[] dependencies,
            string[] diagnosticSeeds)
        {
            DocumentId = documentId ?? throw new ArgumentNullException(nameof(documentId));
            FormatVersion = formatVersion;
            ProjectName = projectName ?? throw new ArgumentNullException(nameof(projectName));
            ProfileId = profileId ?? throw new ArgumentNullException(nameof(profileId));
            GeneratorVersion = generatorVersion ?? throw new ArgumentNullException(nameof(generatorVersion));
            ProfileMask = profileMask;
            SourceHash = sourceHash;
            NormalizedHash = normalizedHash;
            Sources = CloneLines(sources, nameof(sources));
            Modules = CloneLines(modules, nameof(modules));
            Scopes = CloneLines(scopes, nameof(scopes));
            Services = CloneLines(services, nameof(services));
            Commands = CloneLines(commands, nameof(commands));
            ValueKeys = CloneLines(valueKeys, nameof(valueKeys));
            ValueInitPlans = CloneLines(valueInitPlans, nameof(valueInitPlans));
            Lifecycles = CloneLines(lifecycles, nameof(lifecycles));
            RuntimeQueries = CloneLines(runtimeQueries, nameof(runtimeQueries));
            Dependencies = CloneLines(dependencies, nameof(dependencies));
            DiagnosticSeeds = CloneLines(diagnosticSeeds, nameof(diagnosticSeeds));
        }

        public string DocumentId { get; }

        public int FormatVersion { get; }

        public string ProjectName { get; }

        public string ProfileId { get; }

        public string GeneratorVersion { get; }

        public KernelProfileMask ProfileMask { get; }

        public Hash128 SourceHash { get; }

        public Hash128 NormalizedHash { get; }

        public string[] Sources { get; }

        public string[] Modules { get; }

        public string[] Scopes { get; }

        public string[] Services { get; }

        public string[] Commands { get; }

        public string[] ValueKeys { get; }

        public string[] ValueInitPlans { get; }

        public string[] Lifecycles { get; }

        public string[] RuntimeQueries { get; }

        public string[] Dependencies { get; }

        public string[] DiagnosticSeeds { get; }

        static string[] CloneLines(string[] lines, string parameterName)
        {
            if (lines == null)
                throw new ArgumentNullException(parameterName);

            string[] clone = new string[lines.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                clone[i] = lines[i] ?? throw new ArgumentException("Report sections must not contain null lines.", parameterName);
            }

            return clone;
        }
    }

    public static class Hash128Serialization
    {
        public static string ToHexString(Hash128 value)
        {
            return value.ToString();
        }

        public static Hash128 Parse(string hex)
        {
            if (!TryParse(hex, out Hash128 value))
                throw new FormatException("Hash128 hex strings must contain exactly 32 hexadecimal characters.");

            return value;
        }

        public static bool TryParse(string? hex, out Hash128 value)
        {
            if (hex == null || hex.Length != 32)
            {
                value = default;
                return false;
            }

            if (!TryParseUInt32(hex, 0, out uint a)
                || !TryParseUInt32(hex, 8, out uint b)
                || !TryParseUInt32(hex, 16, out uint c)
                || !TryParseUInt32(hex, 24, out uint d))
            {
                value = default;
                return false;
            }

            value = new Hash128(a, b, c, d);
            return true;
        }

        public static byte[] ToBytes(Hash128 value)
        {
            byte[] bytes = new byte[16];
            WriteUInt32LittleEndian(bytes, 0, value.A);
            WriteUInt32LittleEndian(bytes, 4, value.B);
            WriteUInt32LittleEndian(bytes, 8, value.C);
            WriteUInt32LittleEndian(bytes, 12, value.D);
            return bytes;
        }

        public static Hash128 FromBytes(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            if (bytes.Length != 16)
                throw new ArgumentException("Hash128 byte arrays must contain exactly 16 bytes.", nameof(bytes));

            return new Hash128(
                ReadUInt32LittleEndian(bytes, 0),
                ReadUInt32LittleEndian(bytes, 4),
                ReadUInt32LittleEndian(bytes, 8),
                ReadUInt32LittleEndian(bytes, 12));
        }

        static bool TryParseUInt32(string value, int startIndex, out uint parsed)
        {
            return uint.TryParse(value.Substring(startIndex, 8), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed);
        }

        static void WriteUInt32LittleEndian(byte[] bytes, int startIndex, uint value)
        {
            bytes[startIndex] = (byte)(value & 0xffu);
            bytes[startIndex + 1] = (byte)((value >> 8) & 0xffu);
            bytes[startIndex + 2] = (byte)((value >> 16) & 0xffu);
            bytes[startIndex + 3] = (byte)((value >> 24) & 0xffu);
        }

        static uint ReadUInt32LittleEndian(byte[] bytes, int startIndex)
        {
            return (uint)(bytes[startIndex]
                | (bytes[startIndex + 1] << 8)
                | (bytes[startIndex + 2] << 16)
                | (bytes[startIndex + 3] << 24));
        }
    }

    public static class KernelIRHashing
    {
        public static Hash128 ComputeNormalizedHash(KernelIR ir)
        {
            if (ir == null)
                throw new ArgumentNullException(nameof(ir));

            using (KernelIRTokenWriter writer = new KernelIRTokenWriter())
            {
                WriteHeader(writer, ir.Header);
                WriteProfile(writer, ir.Profile);
                WriteModules(writer, ir.Modules, includeSourceData: false);
                WriteScopes(writer, ir.Scopes, includeSourceData: false);
                WriteServices(writer, ir.Services, includeSourceData: false);
                WriteCommands(writer, ir.Commands, includeSourceData: false);
                WriteValueKeys(writer, ir.ValueKeys, includeSourceData: false);
                WriteValueInitPlans(writer, ir.ValueInitPlans, includeSourceData: false);
                WriteLifecycles(writer, ir.Lifecycles, includeSourceData: false);
                WriteRuntimeQueries(writer, ir.RuntimeQueries, includeSourceData: false);
                WriteDependencies(writer, ir.Dependencies, includeSourceData: false);
                WriteDiagnosticSeeds(writer, ir.DiagnosticSeeds, includeSourceData: false);
                return writer.ToHash128();
            }
        }

        public static Hash128 ComputeSourceHash(KernelIR ir)
        {
            if (ir == null)
                throw new ArgumentNullException(nameof(ir));

            using (KernelIRTokenWriter writer = new KernelIRTokenWriter())
            {
                WriteHeader(writer, ir.Header);
                WriteProfile(writer, ir.Profile);
                WriteSourceLocationTable(writer, ir.Sources);
                WriteModules(writer, ir.Modules, includeSourceData: true);
                WriteScopes(writer, ir.Scopes, includeSourceData: true);
                WriteServices(writer, ir.Services, includeSourceData: true);
                WriteCommands(writer, ir.Commands, includeSourceData: true);
                WriteValueKeys(writer, ir.ValueKeys, includeSourceData: true);
                WriteValueInitPlans(writer, ir.ValueInitPlans, includeSourceData: true);
                WriteLifecycles(writer, ir.Lifecycles, includeSourceData: true);
                WriteRuntimeQueries(writer, ir.RuntimeQueries, includeSourceData: true);
                WriteDependencies(writer, ir.Dependencies, includeSourceData: true);
                WriteDiagnosticSeeds(writer, ir.DiagnosticSeeds, includeSourceData: true);
                return writer.ToHash128();
            }
        }

        public static KernelIRDumpReport CreateReport(KernelIR ir)
        {
            if (ir == null)
                throw new ArgumentNullException(nameof(ir));

            Hash128 sourceHash = ComputeSourceHash(ir);
            Hash128 normalizedHash = ComputeNormalizedHash(ir);
            return new KernelIRDumpReport(
                ir.Header.DocumentId,
                ir.Header.FormatVersion,
                ir.Header.ProjectName,
                ir.Header.ProfileId,
                ir.Header.GeneratorVersion,
                ir.Profile.Mask,
                sourceHash,
                normalizedHash,
                BuildSourceLines(ir.Sources),
                BuildModuleLines(ir.Modules),
                BuildScopeLines(ir.Scopes),
                BuildServiceLines(ir.Services),
                BuildCommandLines(ir.Commands),
                BuildValueKeyLines(ir.ValueKeys),
                BuildValueInitPlanLines(ir.ValueInitPlans),
                BuildLifecycleLines(ir.Lifecycles),
                BuildRuntimeQueryLines(ir.RuntimeQueries),
                BuildDependencyLines(ir.Dependencies),
                BuildDiagnosticSeedLines(ir.DiagnosticSeeds));
        }

        public static string DumpText(KernelIR ir)
        {
            KernelIRDumpReport report = CreateReport(ir);
            StringBuilder builder = new StringBuilder(2048);
            builder.AppendLine("KernelIR Report");
            builder.AppendLine("DocumentId: " + report.DocumentId);
            builder.AppendLine("FormatVersion: " + report.FormatVersion);
            builder.AppendLine("ProjectName: " + report.ProjectName);
            builder.AppendLine("ProfileId: " + report.ProfileId);
            builder.AppendLine("ProfileMask: " + report.ProfileMask);
            builder.AppendLine("GeneratorVersion: " + report.GeneratorVersion);
            builder.AppendLine("ComputedSourceHash: " + Hash128Serialization.ToHexString(report.SourceHash));
            builder.AppendLine("ComputedNormalizedHash: " + Hash128Serialization.ToHexString(report.NormalizedHash));
            AppendSection(builder, "Sources", report.Sources);
            AppendSection(builder, "Modules", report.Modules);
            AppendSection(builder, "Scopes", report.Scopes);
            AppendSection(builder, "Services", report.Services);
            AppendSection(builder, "Commands", report.Commands);
            AppendSection(builder, "ValueKeys", report.ValueKeys);
            AppendSection(builder, "ValueInitPlans", report.ValueInitPlans);
            AppendSection(builder, "Lifecycles", report.Lifecycles);
            AppendSection(builder, "RuntimeQueries", report.RuntimeQueries);
            AppendSection(builder, "Dependencies", report.Dependencies);
            AppendSection(builder, "DiagnosticSeeds", report.DiagnosticSeeds);
            return builder.ToString();
        }

        static void AppendSection(StringBuilder builder, string sectionName, string[] lines)
        {
            builder.AppendLine(sectionName + " (" + lines.Length + "):");
            for (int i = 0; i < lines.Length; i++)
            {
                builder.Append("  - ");
                builder.AppendLine(lines[i]);
            }
        }

        static void WriteHeader(KernelIRTokenWriter writer, KernelIRHeader header)
        {
            writer.WriteString("Header");
            writer.WriteString(header.DocumentId);
            writer.WriteInt(header.FormatVersion);
            writer.WriteString(header.ProjectName);
            writer.WriteString(header.ProfileId);
            writer.WriteString(header.GeneratorVersion);
        }

        static void WriteProfile(KernelIRTokenWriter writer, KernelProfileIR profile)
        {
            writer.WriteString("Profile");
            writer.WriteString(profile.Id);
            writer.WriteInt((int)profile.Mask);
            WriteAvailability(writer, profile.Availability);
        }

        static void WriteSourceLocationTable(KernelIRTokenWriter writer, SourceLocationTable sourceLocationTable)
        {
            writer.WriteString("Sources");
            writer.WriteInt(sourceLocationTable.Count);
            ReadOnlySpan<SourceLocationIR> sources = sourceLocationTable.Sources;
            for (int index = 0; index < sources.Length; index++)
            {
                writer.WriteInt(index + 1);
                WriteSourceLocation(writer, sources[index]);
            }
        }

        static void WriteModules(KernelIRTokenWriter writer, ReadOnlySpan<ModuleIR> modules, bool includeSourceData)
        {
            writer.WriteString("Modules");
            writer.WriteInt(modules.Length);
            for (int moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++)
            {
                ModuleIR module = modules[moduleIndex];
                writer.WriteInt(module.Id.Value);
                writer.WriteString(module.Name);
                writer.WriteInt((int)module.Kind);
                writer.WriteInt(module.Version.Value);
                WriteAvailability(writer, module.Availability.Value);
                writer.WriteInt(module.LegacyCompat != null ? (int)module.LegacyCompat.Kind : 0);
                writer.WriteString(module.LegacyCompat?.LegacySystemName);
                writer.WriteString(module.LegacyCompat?.TargetSubsystem);
                writer.WriteInt(module.LegacyCompat != null ? (int)module.LegacyCompat.Profiles : 0);
                writer.WriteInt(module.LegacyCompat != null ? (int)module.LegacyCompat.RemovalStatus : 0);
                writer.WriteString(module.LegacyCompat?.DiagnosticsCode);
                writer.WriteString(module.LegacyCompat?.RemovalCondition);
                if (includeSourceData)
                    writer.WriteInt(module.Source.Value);

                ModuleDependencyIR[] requiredModules = CopyAndSort(module.RequiredModules, CompareModuleDependency);
                writer.WriteInt(requiredModules.Length);
                for (int dependencyIndex = 0; dependencyIndex < requiredModules.Length; dependencyIndex++)
                {
                    OptionalDependencyAbsenceBehavior? absenceBehavior = requiredModules[dependencyIndex].AbsenceBehavior;
                    writer.WriteInt(requiredModules[dependencyIndex].ModuleId.Value);
                    writer.WriteInt(absenceBehavior.HasValue ? (int)absenceBehavior.Value : 0);
                    writer.WriteString(requiredModules[dependencyIndex].DisabledContribution);
                    writer.WriteInt(requiredModules[dependencyIndex].AlternativeModuleId.Value);
                    writer.WriteInt((int)requiredModules[dependencyIndex].ProfileSpecificErrorProfiles);
                    if (includeSourceData)
                        writer.WriteInt(requiredModules[dependencyIndex].Source.Value);
                }

                ModuleDependencyIR[] optionalModules = CopyAndSort(module.OptionalModules, CompareModuleDependency);
                writer.WriteInt(optionalModules.Length);
                for (int dependencyIndex = 0; dependencyIndex < optionalModules.Length; dependencyIndex++)
                {
                    OptionalDependencyAbsenceBehavior? absenceBehavior = optionalModules[dependencyIndex].AbsenceBehavior;
                    writer.WriteInt(optionalModules[dependencyIndex].ModuleId.Value);
                    writer.WriteInt(absenceBehavior.HasValue ? (int)absenceBehavior.Value : 0);
                    writer.WriteString(optionalModules[dependencyIndex].DisabledContribution);
                    writer.WriteInt(optionalModules[dependencyIndex].AlternativeModuleId.Value);
                    writer.WriteInt((int)optionalModules[dependencyIndex].ProfileSpecificErrorProfiles);
                    if (includeSourceData)
                        writer.WriteInt(optionalModules[dependencyIndex].Source.Value);
                }
            }
        }

        static void WriteScopes(KernelIRTokenWriter writer, ReadOnlySpan<ScopeIR> scopes, bool includeSourceData)
        {
            writer.WriteString("Scopes");
            writer.WriteInt(scopes.Length);
            for (int scopeIndex = 0; scopeIndex < scopes.Length; scopeIndex++)
            {
                ScopeIR scope = scopes[scopeIndex];
                writer.WriteInt(scope.AuthoringId.Value);
                writer.WriteInt(scope.PlanId.Value);
                writer.WriteString(scope.Name);
                writer.WriteInt((int)scope.Kind);
                writer.WriteInt(scope.OwnerModule.Value);
                writer.WriteInt(scope.ParentAuthoringId.Value);
                if (includeSourceData)
                    writer.WriteInt(scope.Source.Value);

                ScopeServiceBoundaryIR serviceBoundary = scope.ServiceBoundary;
                writer.WriteInt((int)serviceBoundary.Kind);
                writer.WriteInt(serviceBoundary.ExpectedInstanceCount);
                if (includeSourceData)
                    writer.WriteInt(serviceBoundary.Source.Value);

                ScopeServiceRequirementIR[] requiredServices = CopyAndSort(scope.RequiredServices, CompareScopeServiceRequirement);
                writer.WriteInt(requiredServices.Length);
                for (int requirementIndex = 0; requirementIndex < requiredServices.Length; requirementIndex++)
                {
                    writer.WriteInt(requiredServices[requirementIndex].ServiceId.Value);
                    writer.WriteInt((int)requiredServices[requirementIndex].Strength);
                    if (includeSourceData)
                        writer.WriteInt(requiredServices[requirementIndex].Source.Value);
                }

                ScopeValueInitRefIR[] valueInitPlans = CopyAndSort(scope.ValueInitPlans, CompareScopeValueInit);
                writer.WriteInt(valueInitPlans.Length);
                for (int planIndex = 0; planIndex < valueInitPlans.Length; planIndex++)
                {
                    writer.WriteInt(valueInitPlans[planIndex].PlanId.Value);
                    if (includeSourceData)
                        writer.WriteInt(valueInitPlans[planIndex].Source.Value);
                }

                writer.WriteInt(scope.Lifecycle.PlanId.Value);
                if (includeSourceData)
                    writer.WriteInt(scope.Lifecycle.Source.Value);
            }
        }

        static void WriteServices(KernelIRTokenWriter writer, ReadOnlySpan<ServiceIR> services, bool includeSourceData)
        {
            writer.WriteString("Services");
            writer.WriteInt(services.Length);
            for (int serviceIndex = 0; serviceIndex < services.Length; serviceIndex++)
            {
                ServiceIR service = services[serviceIndex];
                writer.WriteInt(service.Id.Value);
                writer.WriteString(service.Name);
                writer.WriteInt((int)service.Lifetime);
                writer.WriteInt((int)service.Cardinality);
                writer.WriteInt(service.OwnerModule.Value);
                writer.WriteInt((int)service.FactoryKind);
                if (includeSourceData)
                    writer.WriteInt(service.Source.Value);

                ServiceContractIR[] contracts = CopyAndSort(service.Contracts, CompareServiceContract);
                writer.WriteInt(contracts.Length);
                for (int contractIndex = 0; contractIndex < contracts.Length; contractIndex++)
                {
                    writer.WriteString(contracts[contractIndex].ContractName);
                    if (includeSourceData)
                        writer.WriteInt(contracts[contractIndex].Source.Value);
                }

                ServiceDependencyIR[] dependencies = CopyAndSort(service.Dependencies, CompareServiceDependency);
                writer.WriteInt(dependencies.Length);
                for (int dependencyIndex = 0; dependencyIndex < dependencies.Length; dependencyIndex++)
                {
                    WriteDependencyNode(writer, dependencies[dependencyIndex].Target);
                    writer.WriteInt((int)dependencies[dependencyIndex].Strength);
                    if (includeSourceData)
                        writer.WriteInt(dependencies[dependencyIndex].Source.Value);
                }
            }
        }

        static void WriteCommands(KernelIRTokenWriter writer, ReadOnlySpan<CommandIR> commands, bool includeSourceData)
        {
            writer.WriteString("Commands");
            writer.WriteInt(commands.Length);
            for (int commandIndex = 0; commandIndex < commands.Length; commandIndex++)
            {
                CommandIR command = commands[commandIndex];
                writer.WriteInt(command.TypeId.Value);
                writer.WriteString(command.RuntimeName);
                writer.WriteInt(command.AuthoringKey.Id.Value);
                writer.WriteString(command.AuthoringKey.Value);
                writer.WriteInt(command.CategoryId.Value);
                writer.WriteInt(command.OwnerModule.Value);
                writer.WriteInt(command.PayloadSchema.Id.Value);
                writer.WriteInt((int)command.PayloadSchema.UnknownFieldPolicy);
                writer.WriteInt(command.Executor.Id.Value);
                if (includeSourceData)
                {
                    writer.WriteInt(command.AuthoringKey.Source.Value);
                    writer.WriteInt(command.PayloadSchema.Source.Value);
                    writer.WriteInt(command.Executor.Source.Value);
                    writer.WriteInt(command.Source.Value);
                }

                CommandPayloadFieldIR[] payloadFields = CopyAndSort(command.PayloadSchema.Fields, CompareCommandPayloadField);
                writer.WriteInt(payloadFields.Length);
                for (int fieldIndex = 0; fieldIndex < payloadFields.Length; fieldIndex++)
                {
                    CommandPayloadFieldIR field = payloadFields[fieldIndex];
                    writer.WriteString(field.FieldPath);
                    writer.WriteInt((int)field.Kind);
                    writer.WriteInt((int)field.Requirement);
                    writer.WriteInt((int)field.ReferenceKind);
                    writer.WriteInt(BoolToInt(field.AllowNull));
                    if (includeSourceData)
                        writer.WriteInt(field.Source.Value);
                }

                CommandDependencyIR[] dependencies = CopyAndSort(command.Dependencies, CompareCommandDependency);
                writer.WriteInt(dependencies.Length);
                for (int dependencyIndex = 0; dependencyIndex < dependencies.Length; dependencyIndex++)
                {
                    WriteDependencyNode(writer, dependencies[dependencyIndex].Target);
                    writer.WriteInt((int)dependencies[dependencyIndex].Strength);
                    if (includeSourceData)
                        writer.WriteInt(dependencies[dependencyIndex].Source.Value);
                }
            }
        }

        static void WriteValueKeys(KernelIRTokenWriter writer, ReadOnlySpan<ValueKeyIR> valueKeys, bool includeSourceData)
        {
            writer.WriteString("ValueKeys");
            writer.WriteInt(valueKeys.Length);
            for (int valueKeyIndex = 0; valueKeyIndex < valueKeys.Length; valueKeyIndex++)
            {
                ValueKeyIR valueKey = valueKeys[valueKeyIndex];
                writer.WriteInt(valueKey.Id.Value);
                writer.WriteString(valueKey.StableKey);
                writer.WriteString(valueKey.DisplayName);
                writer.WriteInt((int)valueKey.Kind);
                writer.WriteInt(valueKey.OwnerModule.Value);
                writer.WriteInt(valueKey.Schema.Id.Value);
                writer.WriteBool(valueKey.SavePolicy.Persists);
                writer.WriteBool(valueKey.SavePolicy.SaveAcrossProfiles);
                writer.WriteString(valueKey.SavePolicy.Channel);
                if (includeSourceData)
                {
                    writer.WriteInt(valueKey.Schema.Source.Value);
                    writer.WriteInt(valueKey.Source.Value);
                }
            }
        }

        static void WriteValueInitPlans(KernelIRTokenWriter writer, ReadOnlySpan<ValueInitPlanIR> valueInitPlans, bool includeSourceData)
        {
            writer.WriteString("ValueInitPlans");
            writer.WriteInt(valueInitPlans.Length);
            for (int planIndex = 0; planIndex < valueInitPlans.Length; planIndex++)
            {
                ValueInitPlanIR valueInitPlan = valueInitPlans[planIndex];
                writer.WriteInt(valueInitPlan.PlanId.Value);
                writer.WriteInt(valueInitPlan.OwnerModule.Value);
                writer.WriteInt(valueInitPlan.TargetScopePlanId.Value);
                writer.WriteString(valueInitPlan.TargetStoreRef);
                writer.WriteInt((int)valueInitPlan.ExecutionPhase);
                writer.WriteInt(valueInitPlan.Order);
                WriteAvailability(writer, valueInitPlan.Availability);
                if (includeSourceData)
                    writer.WriteInt(valueInitPlan.Source.Value);

                ValueInitEntryIR[] entries = CopyAndSort(valueInitPlan.Entries, CompareValueInitEntry);
                writer.WriteInt(entries.Length);
                for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                {
                    ValueInitEntryIR entry = entries[entryIndex];
                    writer.WriteInt(entry.KeyId.Value);
                    writer.WriteInt((int)entry.SourceKind);
                    writer.WriteInt((int)entry.ValueKind);
                    writer.WriteInt(entry.Order);
                    writer.WriteInt((int)entry.OverwritePolicy);
                    writer.WriteString(entry.SerializedValue);
                    writer.WriteString(entry.EvaluationLocalRef);
                    if (includeSourceData)
                        writer.WriteInt(entry.Source.Value);
                }
            }
        }

        static void WriteLifecycles(KernelIRTokenWriter writer, ReadOnlySpan<LifecycleIR> lifecycles, bool includeSourceData)
        {
            writer.WriteString("Lifecycles");
            writer.WriteInt(lifecycles.Length);
            for (int lifecycleIndex = 0; lifecycleIndex < lifecycles.Length; lifecycleIndex++)
            {
                LifecycleIR lifecycle = lifecycles[lifecycleIndex];
                writer.WriteInt(lifecycle.PlanId.Value);
                writer.WriteString(lifecycle.Name);
                writer.WriteInt(lifecycle.OwnerModule.Value);
                writer.WriteInt((int)lifecycle.FailurePolicy);
                writer.WriteBool(lifecycle.FailurePolicyIsExplicit);
                writer.WriteInt((int)lifecycle.FailurePolicyJustificationProfiles);
                writer.WriteString(lifecycle.FailurePolicyJustification);
                if (includeSourceData)
                    writer.WriteInt(lifecycle.Source.Value);

                LifecycleStepIR[] steps = CopyAndSort(lifecycle.Steps, CompareLifecycleStep);
                writer.WriteInt(steps.Length);
                for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
                {
                    writer.WriteInt(steps[stepIndex].Id.Value);
                    writer.WriteInt((int)steps[stepIndex].Phase);
                    writer.WriteInt(steps[stepIndex].Order);
                    WriteLifecycleTarget(writer, steps[stepIndex].Target);
                    writer.WriteInt((int)steps[stepIndex].Action);
                    DependencyEdgeId[] dependencyIds = CopyAndSort(steps[stepIndex].Dependencies, CompareDependencyEdgeId);
                    writer.WriteInt(dependencyIds.Length);
                    for (int dependencyIndex = 0; dependencyIndex < dependencyIds.Length; dependencyIndex++)
                    {
                        writer.WriteInt(dependencyIds[dependencyIndex].Value);
                    }

                    if (includeSourceData)
                        writer.WriteInt(steps[stepIndex].Source.Value);
                }
            }
        }

        static void WriteRuntimeQueries(KernelIRTokenWriter writer, ReadOnlySpan<RuntimeQueryIR> runtimeQueries, bool includeSourceData)
        {
            writer.WriteString("RuntimeQueries");
            writer.WriteInt(runtimeQueries.Length);
            for (int queryIndex = 0; queryIndex < runtimeQueries.Length; queryIndex++)
            {
                RuntimeQueryIR runtimeQuery = runtimeQueries[queryIndex];
                writer.WriteInt(runtimeQuery.Id.Value);
                writer.WriteString(runtimeQuery.Name);
                writer.WriteInt((int)runtimeQuery.TargetKind);
                writer.WriteBool(runtimeQuery.Policy.RequiresUniqueResult);
                writer.WriteBool(runtimeQuery.Policy.AllowMissing);
                writer.WriteInt((int)runtimeQuery.Policy.UpdatePhase);
                writer.WriteInt(runtimeQuery.OwnerModule.Value);
                if (includeSourceData)
                    writer.WriteInt(runtimeQuery.Source.Value);

                RuntimeIdentityFieldIR[] fields = CopyAndSort(runtimeQuery.IndexedFields, CompareRuntimeIdentityField);
                writer.WriteInt(fields.Length);
                for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
                {
                    writer.WriteString(fields[fieldIndex].Name);
                    writer.WriteString(fields[fieldIndex].ValueType);
                    writer.WriteBool(fields[fieldIndex].IsRequired);
                }
            }
        }

        static void WriteDependencies(KernelIRTokenWriter writer, ReadOnlySpan<DependencyEdgeIR> dependencies, bool includeSourceData)
        {
            writer.WriteString("Dependencies");
            writer.WriteInt(dependencies.Length);
            for (int dependencyIndex = 0; dependencyIndex < dependencies.Length; dependencyIndex++)
            {
                writer.WriteInt(dependencies[dependencyIndex].Id.Value);
                WriteDependencyNode(writer, dependencies[dependencyIndex].From);
                WriteDependencyNode(writer, dependencies[dependencyIndex].To);
                writer.WriteInt((int)dependencies[dependencyIndex].Kind);
                writer.WriteInt((int)dependencies[dependencyIndex].Phase);
                writer.WriteInt((int)dependencies[dependencyIndex].Strength);
                writer.WriteInt((int)dependencies[dependencyIndex].RuntimeCycleMediation);
                if (includeSourceData)
                    writer.WriteInt(dependencies[dependencyIndex].Source.Value);
            }
        }

        static void WriteDiagnosticSeeds(KernelIRTokenWriter writer, ReadOnlySpan<DiagnosticSeedIR> diagnosticSeeds, bool includeSourceData)
        {
            writer.WriteString("DiagnosticSeeds");
            DiagnosticSeedIR[] sortedSeeds = CopyAndSort(diagnosticSeeds, CompareDiagnosticSeed);
            writer.WriteInt(sortedSeeds.Length);
            for (int seedIndex = 0; seedIndex < sortedSeeds.Length; seedIndex++)
            {
                writer.WriteString(sortedSeeds[seedIndex].SeedKey);
                writer.WriteString(sortedSeeds[seedIndex].DebugName);
                writer.WriteInt(sortedSeeds[seedIndex].OwnerModule.Value);
                if (includeSourceData)
                    writer.WriteInt(sortedSeeds[seedIndex].Source.Value);
            }
        }

        static void WriteAvailability(KernelIRTokenWriter writer, AvailabilityIR availability)
        {
            writer.WriteInt((int)availability.Profiles);
            writer.WriteBool(availability.EnabledByDefault);
            writer.WriteString(availability.Condition);
        }

        static void WriteDependencyNode(KernelIRTokenWriter writer, DependencyNodeIR dependencyNode)
        {
            writer.WriteInt((int)dependencyNode.Kind);
            writer.WriteInt(dependencyNode.ModuleId.Value);
            writer.WriteInt(dependencyNode.ServiceId.Value);
            writer.WriteInt(dependencyNode.ScopePlanId.Value);
            writer.WriteInt(dependencyNode.CommandTypeId.Value);
            writer.WriteInt(dependencyNode.ValueKeyId.Value);
            writer.WriteInt(dependencyNode.LifecycleStepId.Value);
            writer.WriteInt(dependencyNode.RuntimeQueryId.Value);
        }

        static void WriteLifecycleTarget(KernelIRTokenWriter writer, LifecycleTargetRefIR lifecycleTarget)
        {
            writer.WriteInt((int)lifecycleTarget.Kind);
            writer.WriteInt(lifecycleTarget.TargetService.Value);
            writer.WriteInt(lifecycleTarget.TargetScope.Value);
            writer.WriteInt(lifecycleTarget.TargetRuntimeQuery.Value);
            writer.WriteString(lifecycleTarget.TargetLocalRef);
        }

        static void WriteSourceLocation(KernelIRTokenWriter writer, SourceLocationIR sourceLocation)
        {
            writer.WriteInt((int)sourceLocation.Kind);
            switch (sourceLocation.Kind)
            {
                case SourceLocationKind.Unity:
                    UnitySourceLocation unitySource = sourceLocation.UnitySource!.Value;
                    writer.WriteString(unitySource.AssetGuid);
                    writer.WriteString(SanitizePathForHash(unitySource.AssetPath));
                    writer.WriteLong(unitySource.LocalFileId);
                    writer.WriteString(SanitizePathForHash(unitySource.ScenePath));
                    writer.WriteString(unitySource.GameObjectPath);
                    writer.WriteString(unitySource.ComponentType);
                    writer.WriteString(unitySource.PropertyPath);
                    break;
                case SourceLocationKind.Legacy:
                    LegacySourceLocation legacySource = sourceLocation.LegacySource!.Value;
                    writer.WriteString(legacySource.LegacySystemName);
                    writer.WriteString(legacySource.LegacyOrigin);
                    writer.WriteString(legacySource.MigrationAdapter);
                    break;
                case SourceLocationKind.Generated:
                    GeneratedSourceLocation generatedSource = sourceLocation.GeneratedSource!.Value;
                    writer.WriteString(generatedSource.GeneratorName);
                    writer.WriteString(generatedSource.GeneratedFrom);
                    writer.WriteString(generatedSource.GenerationPhase);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(sourceLocation), sourceLocation.Kind, "Unsupported source location kind.");
            }
        }

        static string[] BuildSourceLines(SourceLocationTable sourceLocationTable)
        {
            ReadOnlySpan<SourceLocationIR> sources = sourceLocationTable.Sources;
            string[] lines = new string[sources.Length];
            for (int index = 0; index < sources.Length; index++)
            {
                lines[index] = "SourceLocationId(" + (index + 1) + ") = " + FormatSourceLocation(sources[index]);
            }

            return lines;
        }

        static string[] BuildModuleLines(ReadOnlySpan<ModuleIR> modules)
        {
            string[] lines = new string[modules.Length];
            for (int moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++)
            {
                ModuleIR module = modules[moduleIndex];
                lines[moduleIndex] = module.Id + " Name=" + module.Name + " Kind=" + module.Kind + " Version=" + module.Version.Value + " Availability=" + FormatAvailability(module.Availability.Value) + " LegacyCompat=" + FormatLegacyCompat(module.LegacyCompat) + " Source=" + FormatSourceId(module.Source) + " Required=" + FormatModuleDependencies(module.RequiredModules) + " Optional=" + FormatModuleDependencies(module.OptionalModules);
            }

            return lines;
        }

        static string[] BuildScopeLines(ReadOnlySpan<ScopeIR> scopes)
        {
            string[] lines = new string[scopes.Length];
            for (int scopeIndex = 0; scopeIndex < scopes.Length; scopeIndex++)
            {
                ScopeIR scope = scopes[scopeIndex];
                lines[scopeIndex] = scope.PlanId + " AuthoringId=" + scope.AuthoringId.Value + " Name=" + scope.Name + " Kind=" + scope.Kind + " OwnerModule=" + scope.OwnerModule.Value + " ParentAuthoringId=" + scope.ParentAuthoringId.Value + " Source=" + FormatSourceId(scope.Source) + " ServiceBoundary=" + FormatScopeServiceBoundary(scope.ServiceBoundary) + " RequiredServices=" + FormatScopeServiceRequirements(scope.RequiredServices) + " ValueInitPlans=" + FormatScopeValueInitRefs(scope.ValueInitPlans) + " LifecyclePlan=" + scope.Lifecycle.PlanId + "@" + FormatSourceId(scope.Lifecycle.Source);
            }

            return lines;
        }

        static string[] BuildServiceLines(ReadOnlySpan<ServiceIR> services)
        {
            string[] lines = new string[services.Length];
            for (int serviceIndex = 0; serviceIndex < services.Length; serviceIndex++)
            {
                ServiceIR service = services[serviceIndex];
                lines[serviceIndex] = service.Id + " Name=" + service.Name + " Lifetime=" + service.Lifetime + " Cardinality=" + service.Cardinality + " OwnerModule=" + service.OwnerModule.Value + " FactoryKind=" + service.FactoryKind + " Source=" + FormatSourceId(service.Source) + " Contracts=" + FormatServiceContracts(service.Contracts) + " Dependencies=" + FormatServiceDependencies(service.Dependencies);
            }

            return lines;
        }

        static string[] BuildCommandLines(ReadOnlySpan<CommandIR> commands)
        {
            string[] lines = new string[commands.Length];
            for (int commandIndex = 0; commandIndex < commands.Length; commandIndex++)
            {
                CommandIR command = commands[commandIndex];
                lines[commandIndex] = command.TypeId + " RuntimeName=" + command.RuntimeName + " AuthoringKey=" + command.AuthoringKey.Value + "@" + command.AuthoringKey.Id + "@" + FormatSourceId(command.AuthoringKey.Source) + " CategoryId=" + command.CategoryId.Value + " OwnerModule=" + command.OwnerModule.Value + " PayloadSchema=" + command.PayloadSchema.Id + "@" + FormatSourceId(command.PayloadSchema.Source) + " UnknownFieldPolicy=" + command.PayloadSchema.UnknownFieldPolicy + " PayloadFields=" + FormatCommandPayloadFields(command.PayloadSchema.Fields) + " Executor=" + command.Executor.Id + "@" + FormatSourceId(command.Executor.Source) + " Source=" + FormatSourceId(command.Source) + " Dependencies=" + FormatCommandDependencies(command.Dependencies);
            }

            return lines;
        }

        static string[] BuildValueKeyLines(ReadOnlySpan<ValueKeyIR> valueKeys)
        {
            string[] lines = new string[valueKeys.Length];
            for (int valueKeyIndex = 0; valueKeyIndex < valueKeys.Length; valueKeyIndex++)
            {
                ValueKeyIR valueKey = valueKeys[valueKeyIndex];
                lines[valueKeyIndex] = valueKey.Id + " StableKey=" + valueKey.StableKey + " DisplayName=" + valueKey.DisplayName + " Kind=" + valueKey.Kind + " OwnerModule=" + valueKey.OwnerModule.Value + " Schema=" + valueKey.Schema.Id + "@" + FormatSourceId(valueKey.Schema.Source) + " SavePolicy=" + FormatSavePolicy(valueKey.SavePolicy) + " Source=" + FormatSourceId(valueKey.Source);
            }

            return lines;
        }

        static string[] BuildValueInitPlanLines(ReadOnlySpan<ValueInitPlanIR> valueInitPlans)
        {
            string[] lines = new string[valueInitPlans.Length];
            for (int planIndex = 0; planIndex < valueInitPlans.Length; planIndex++)
            {
                ValueInitPlanIR valueInitPlan = valueInitPlans[planIndex];
                lines[planIndex] = valueInitPlan.PlanId + " OwnerModule=" + valueInitPlan.OwnerModule.Value + " TargetScope=" + valueInitPlan.TargetScopePlanId.Value + " TargetStoreRef=" + valueInitPlan.TargetStoreRef + " Phase=" + valueInitPlan.ExecutionPhase + " Order=" + valueInitPlan.Order + " Availability=" + FormatAvailability(valueInitPlan.Availability) + " Source=" + FormatSourceId(valueInitPlan.Source) + " Entries=" + FormatValueInitEntries(valueInitPlan.Entries);
            }

            return lines;
        }

        static string[] BuildLifecycleLines(ReadOnlySpan<LifecycleIR> lifecycles)
        {
            string[] lines = new string[lifecycles.Length];
            for (int lifecycleIndex = 0; lifecycleIndex < lifecycles.Length; lifecycleIndex++)
            {
                LifecycleIR lifecycle = lifecycles[lifecycleIndex];
                lines[lifecycleIndex] = lifecycle.PlanId + " Name=" + lifecycle.Name + " OwnerModule=" + lifecycle.OwnerModule.Value + " FailurePolicy=" + lifecycle.FailurePolicy + " FailurePolicyExplicit=" + lifecycle.FailurePolicyIsExplicit + " FailurePolicyJustificationProfiles=" + lifecycle.FailurePolicyJustificationProfiles + " FailurePolicyJustification=" + (lifecycle.FailurePolicyJustification ?? string.Empty) + " Source=" + FormatSourceId(lifecycle.Source) + " Steps=" + FormatLifecycleSteps(lifecycle.Steps);
            }

            return lines;
        }

        static string[] BuildRuntimeQueryLines(ReadOnlySpan<RuntimeQueryIR> runtimeQueries)
        {
            string[] lines = new string[runtimeQueries.Length];
            for (int queryIndex = 0; queryIndex < runtimeQueries.Length; queryIndex++)
            {
                RuntimeQueryIR runtimeQuery = runtimeQueries[queryIndex];
                lines[queryIndex] = runtimeQuery.Id + " Name=" + runtimeQuery.Name + " TargetKind=" + runtimeQuery.TargetKind + " Policy=" + FormatRuntimeQueryPolicy(runtimeQuery.Policy) + " OwnerModule=" + runtimeQuery.OwnerModule.Value + " Source=" + FormatSourceId(runtimeQuery.Source) + " IndexedFields=" + FormatRuntimeIdentityFields(runtimeQuery.IndexedFields);
            }

            return lines;
        }

        static string[] BuildDependencyLines(ReadOnlySpan<DependencyEdgeIR> dependencies)
        {
            string[] lines = new string[dependencies.Length];
            for (int dependencyIndex = 0; dependencyIndex < dependencies.Length; dependencyIndex++)
            {
                DependencyEdgeIR dependency = dependencies[dependencyIndex];
                lines[dependencyIndex] = dependency.Id + " From=" + FormatDependencyNode(dependency.From) + " To=" + FormatDependencyNode(dependency.To) + " Kind=" + dependency.Kind + " Phase=" + dependency.Phase + " Strength=" + dependency.Strength + " RuntimeCycleMediation=" + dependency.RuntimeCycleMediation + " Source=" + dependency.Source.Value;
            }

            return lines;
        }

        static string[] BuildDiagnosticSeedLines(ReadOnlySpan<DiagnosticSeedIR> diagnosticSeeds)
        {
            DiagnosticSeedIR[] sortedSeeds = CopyAndSort(diagnosticSeeds, CompareDiagnosticSeed);
            string[] lines = new string[sortedSeeds.Length];
            for (int seedIndex = 0; seedIndex < sortedSeeds.Length; seedIndex++)
            {
                lines[seedIndex] = sortedSeeds[seedIndex].SeedKey + " DebugName=" + sortedSeeds[seedIndex].DebugName + " OwnerModule=" + sortedSeeds[seedIndex].OwnerModule.Value + " Source=" + FormatSourceId(sortedSeeds[seedIndex].Source);
            }

            return lines;
        }

        static string FormatSourceLocation(SourceLocationIR sourceLocation)
        {
            switch (sourceLocation.Kind)
            {
                case SourceLocationKind.Unity:
                    UnitySourceLocation unitySource = sourceLocation.UnitySource!.Value;
                    return "Unity(AssetGuid=" + SafeString(unitySource.AssetGuid) + ", AssetPath=" + SafeString(SanitizePathForReport(unitySource.AssetPath)) + ", LocalFileId=" + unitySource.LocalFileId + ", ScenePath=" + SafeString(SanitizePathForReport(unitySource.ScenePath)) + ", GameObjectPath=" + SafeString(unitySource.GameObjectPath) + ", ComponentType=" + SafeString(unitySource.ComponentType) + ", PropertyPath=" + SafeString(unitySource.PropertyPath) + ")";
                case SourceLocationKind.Legacy:
                    LegacySourceLocation legacySource = sourceLocation.LegacySource!.Value;
                    return "Legacy(System=" + SafeString(legacySource.LegacySystemName) + ", Origin=" + SafeString(legacySource.LegacyOrigin) + ", MigrationAdapter=" + SafeString(legacySource.MigrationAdapter) + ")";
                case SourceLocationKind.Generated:
                    GeneratedSourceLocation generatedSource = sourceLocation.GeneratedSource!.Value;
                    return "Generated(Generator=" + SafeString(generatedSource.GeneratorName) + ", From=" + SafeString(generatedSource.GeneratedFrom) + ", Stage=" + SafeString(generatedSource.GenerationPhase) + ")";
                default:
                    return "<invalid>";
            }
        }

        static string FormatDependencyNode(DependencyNodeIR dependencyNode)
        {
            switch (dependencyNode.Kind)
            {
                case DependencyNodeKind.Module:
                    return dependencyNode.ModuleId.ToString();
                case DependencyNodeKind.Service:
                    return dependencyNode.ServiceId.ToString();
                case DependencyNodeKind.Scope:
                    return dependencyNode.ScopePlanId.ToString();
                case DependencyNodeKind.Command:
                    return dependencyNode.CommandTypeId.ToString();
                case DependencyNodeKind.ValueKey:
                    return dependencyNode.ValueKeyId.ToString();
                case DependencyNodeKind.LifecycleStep:
                    return dependencyNode.LifecycleStepId.ToString();
                case DependencyNodeKind.RuntimeQuery:
                    return dependencyNode.RuntimeQueryId.ToString();
                default:
                    return "<invalid>";
            }
        }

        static string SanitizePathForHash(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return Path.IsPathRooted(value) ? string.Empty : value.Replace('\\', '/');
        }

        static string SanitizePathForReport(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return Path.IsPathRooted(value) ? "<excluded-absolute-path>" : value.Replace('\\', '/');
        }

        static string SafeString(string? value)
        {
            return value ?? "<none>";
        }

        static string FormatSourceId(SourceLocationId source)
        {
            return "SourceLocationId(" + source.Value + ")";
        }

        static string FormatAvailability(AvailabilityIR availability)
        {
            return "Availability(Profiles=" + availability.Profiles + ", EnabledByDefault=" + availability.EnabledByDefault + ", Condition=" + SafeString(availability.Condition) + ")";
        }

        static string FormatLegacyCompat(LegacyCompatDescriptorIR? legacyCompat)
        {
            if (legacyCompat == null)
                return "<none>";

            return "LegacyCompat(Kind=" + legacyCompat.Kind + ", LegacySystemName=" + SafeString(legacyCompat.LegacySystemName) + ", TargetSubsystem=" + SafeString(legacyCompat.TargetSubsystem) + ", Profiles=" + legacyCompat.Profiles + ", RemovalStatus=" + legacyCompat.RemovalStatus + ", DiagnosticsCode=" + SafeString(legacyCompat.DiagnosticsCode) + ", RemovalCondition=" + SafeString(legacyCompat.RemovalCondition) + ")";
        }

        static string FormatModuleDependencies(ReadOnlySpan<ModuleDependencyIR> dependencies)
        {
            if (dependencies.Length == 0)
                return "[]";

            ModuleDependencyIR[] sortedDependencies = CopyAndSort(dependencies, CompareModuleDependency);
            StringBuilder builder = new StringBuilder();
            builder.Append('[');
            for (int index = 0; index < sortedDependencies.Length; index++)
            {
                if (index > 0)
                    builder.Append(", ");

                builder.Append(sortedDependencies[index].ModuleId);
                OptionalDependencyAbsenceBehavior? absenceBehavior = sortedDependencies[index].AbsenceBehavior;
                if (absenceBehavior.HasValue)
                {
                    builder.Append(" Behavior=");
                    builder.Append(absenceBehavior.Value);
                }

                if (!string.IsNullOrWhiteSpace(sortedDependencies[index].DisabledContribution))
                {
                    builder.Append(" Disable=");
                    builder.Append(SafeString(sortedDependencies[index].DisabledContribution));
                }

                if (sortedDependencies[index].AlternativeModuleId.Value != 0)
                {
                    builder.Append(" Alternative=");
                    builder.Append(sortedDependencies[index].AlternativeModuleId);
                }

                if (sortedDependencies[index].ProfileSpecificErrorProfiles != KernelProfileMask.None)
                {
                    builder.Append(" ErrorProfiles=");
                    builder.Append(sortedDependencies[index].ProfileSpecificErrorProfiles);
                }

                builder.Append('@');
                builder.Append(FormatSourceId(sortedDependencies[index].Source));
            }

            builder.Append(']');
            return builder.ToString();
        }

        static string FormatScopeServiceRequirements(ReadOnlySpan<ScopeServiceRequirementIR> requirements)
        {
            if (requirements.Length == 0)
                return "[]";

            ScopeServiceRequirementIR[] sortedRequirements = CopyAndSort(requirements, CompareScopeServiceRequirement);
            StringBuilder builder = new StringBuilder();
            builder.Append('[');
            for (int index = 0; index < sortedRequirements.Length; index++)
            {
                if (index > 0)
                    builder.Append(", ");

                builder.Append(sortedRequirements[index].ServiceId);
                builder.Append(":Strength=");
                builder.Append(sortedRequirements[index].Strength);
                builder.Append('@');
                builder.Append(FormatSourceId(sortedRequirements[index].Source));
            }

            builder.Append(']');
            return builder.ToString();
        }

        static string FormatScopeServiceBoundary(ScopeServiceBoundaryIR serviceBoundary)
        {
            return serviceBoundary.Kind + "(ExpectedInstances=" + serviceBoundary.ExpectedInstanceCount + ", Source=" + FormatSourceId(serviceBoundary.Source) + ")";
        }

        static string FormatScopeValueInitRefs(ReadOnlySpan<ScopeValueInitRefIR> valueInitPlans)
        {
            if (valueInitPlans.Length == 0)
                return "[]";

            ScopeValueInitRefIR[] sortedValueInitPlans = CopyAndSort(valueInitPlans, CompareScopeValueInit);
            StringBuilder builder = new StringBuilder();
            builder.Append('[');
            for (int index = 0; index < sortedValueInitPlans.Length; index++)
            {
                if (index > 0)
                    builder.Append(", ");

                builder.Append(sortedValueInitPlans[index].PlanId);
                builder.Append('@');
                builder.Append(FormatSourceId(sortedValueInitPlans[index].Source));
            }

            builder.Append(']');
            return builder.ToString();
        }

        static string FormatServiceContracts(ReadOnlySpan<ServiceContractIR> contracts)
        {
            if (contracts.Length == 0)
                return "[]";

            ServiceContractIR[] sortedContracts = CopyAndSort(contracts, CompareServiceContract);
            StringBuilder builder = new StringBuilder();
            builder.Append('[');
            for (int index = 0; index < sortedContracts.Length; index++)
            {
                if (index > 0)
                    builder.Append(", ");

                builder.Append(sortedContracts[index].ContractName);
                builder.Append('@');
                builder.Append(FormatSourceId(sortedContracts[index].Source));
            }

            builder.Append(']');
            return builder.ToString();
        }

        static string FormatServiceDependencies(ReadOnlySpan<ServiceDependencyIR> dependencies)
        {
            if (dependencies.Length == 0)
                return "[]";

            ServiceDependencyIR[] sortedDependencies = CopyAndSort(dependencies, CompareServiceDependency);
            StringBuilder builder = new StringBuilder();
            builder.Append('[');
            for (int index = 0; index < sortedDependencies.Length; index++)
            {
                if (index > 0)
                    builder.Append(", ");

                builder.Append(FormatDependencyNode(sortedDependencies[index].Target));
                builder.Append(":Strength=");
                builder.Append(sortedDependencies[index].Strength);
                builder.Append('@');
                builder.Append(FormatSourceId(sortedDependencies[index].Source));
            }

            builder.Append(']');
            return builder.ToString();
        }

        static string FormatCommandDependencies(ReadOnlySpan<CommandDependencyIR> dependencies)
        {
            if (dependencies.Length == 0)
                return "[]";

            CommandDependencyIR[] sortedDependencies = CopyAndSort(dependencies, CompareCommandDependency);
            StringBuilder builder = new StringBuilder();
            builder.Append('[');
            for (int index = 0; index < sortedDependencies.Length; index++)
            {
                if (index > 0)
                    builder.Append(", ");

                builder.Append(FormatDependencyNode(sortedDependencies[index].Target));
                builder.Append(":Strength=");
                builder.Append(sortedDependencies[index].Strength);
                builder.Append('@');
                builder.Append(FormatSourceId(sortedDependencies[index].Source));
            }

            builder.Append(']');
            return builder.ToString();
        }

        static string FormatCommandPayloadFields(ReadOnlySpan<CommandPayloadFieldIR> fields)
        {
            if (fields.Length == 0)
                return "[]";

            CommandPayloadFieldIR[] sortedFields = CopyAndSort(fields, CompareCommandPayloadField);
            StringBuilder builder = new StringBuilder();
            builder.Append('[');
            for (int index = 0; index < sortedFields.Length; index++)
            {
                if (index > 0)
                    builder.Append(", ");

                CommandPayloadFieldIR field = sortedFields[index];
                builder.Append(field.FieldPath);
                builder.Append(":Kind=");
                builder.Append(field.Kind);
                builder.Append(":Requirement=");
                builder.Append(field.Requirement);
                builder.Append(":Reference=");
                builder.Append(field.ReferenceKind);
                builder.Append(":AllowNull=");
                builder.Append(field.AllowNull);
                builder.Append('@');
                builder.Append(FormatSourceId(field.Source));
            }

            builder.Append(']');
            return builder.ToString();
        }

        static string FormatSavePolicy(SavePolicyIR savePolicy)
        {
            return "SavePolicy(Persists=" + savePolicy.Persists + ", SaveAcrossProfiles=" + savePolicy.SaveAcrossProfiles + ", Channel=" + SafeString(savePolicy.Channel) + ")";
        }

        static string FormatValueInitEntries(ReadOnlySpan<ValueInitEntryIR> entries)
        {
            if (entries.Length == 0)
                return "[]";

            ValueInitEntryIR[] sortedEntries = CopyAndSort(entries, CompareValueInitEntry);
            StringBuilder builder = new StringBuilder();
            builder.Append('[');
            for (int index = 0; index < sortedEntries.Length; index++)
            {
                if (index > 0)
                    builder.Append(", ");

                ValueInitEntryIR entry = sortedEntries[index];
                builder.Append(entry.KeyId);
                builder.Append("(SourceKind=");
                builder.Append(entry.SourceKind);
                builder.Append(", ValueKind=");
                builder.Append(entry.ValueKind);
                builder.Append(", Order=");
                builder.Append(entry.Order);
                builder.Append(", Overwrite=");
                builder.Append(entry.OverwritePolicy);
                builder.Append(", Serialized=");
                builder.Append(SafeString(entry.SerializedValue));
                builder.Append(", EvalRef=");
                builder.Append(SafeString(entry.EvaluationLocalRef));
                builder.Append(", Source=");
                builder.Append(FormatSourceId(entry.Source));
                builder.Append(')');
            }

            builder.Append(']');
            return builder.ToString();
        }

        static string FormatLifecycleSteps(ReadOnlySpan<LifecycleStepIR> steps)
        {
            LifecycleStepIR[] sortedSteps = CopyAndSort(steps, CompareLifecycleStep);
            StringBuilder builder = new StringBuilder();
            builder.Append('[');
            for (int index = 0; index < sortedSteps.Length; index++)
            {
                if (index > 0)
                    builder.Append(", ");

                builder.Append(sortedSteps[index].Id);
                builder.Append("(Phase=");
                builder.Append(sortedSteps[index].Phase);
                builder.Append(", Order=");
                builder.Append(sortedSteps[index].Order);
                builder.Append(", Target=");
                builder.Append(FormatLifecycleTarget(sortedSteps[index].Target));
                builder.Append(", Action=");
                builder.Append(sortedSteps[index].Action);
                builder.Append(", Dependencies=");
                builder.Append(FormatDependencyIds(sortedSteps[index].Dependencies));
                builder.Append(", Source=");
                builder.Append(FormatSourceId(sortedSteps[index].Source));
                builder.Append(')');
            }

            builder.Append(']');
            return builder.ToString();
        }

        static string FormatDependencyIds(ReadOnlySpan<DependencyEdgeId> dependencyIds)
        {
            if (dependencyIds.Length == 0)
                return "[]";

            DependencyEdgeId[] sortedDependencyIds = CopyAndSort(dependencyIds, CompareDependencyEdgeId);
            StringBuilder builder = new StringBuilder();
            builder.Append('[');
            for (int index = 0; index < sortedDependencyIds.Length; index++)
            {
                if (index > 0)
                    builder.Append(", ");

                builder.Append(sortedDependencyIds[index]);
            }

            builder.Append(']');
            return builder.ToString();
        }

        static string FormatLifecycleTarget(LifecycleTargetRefIR lifecycleTarget)
        {
            return "LifecycleTarget(Kind=" + lifecycleTarget.Kind + ", Service=" + lifecycleTarget.TargetService + ", Scope=" + lifecycleTarget.TargetScope + ", RuntimeQuery=" + lifecycleTarget.TargetRuntimeQuery + ", LocalRef=" + SafeString(lifecycleTarget.TargetLocalRef) + ")";
        }

        static string FormatRuntimeQueryPolicy(RuntimeQueryPolicyIR policy)
        {
            return "RuntimeQueryPolicy(RequiresUniqueResult=" + policy.RequiresUniqueResult + ", AllowMissing=" + policy.AllowMissing + ", UpdatePhase=" + policy.UpdatePhase + ")";
        }

        static string FormatRuntimeIdentityFields(ReadOnlySpan<RuntimeIdentityFieldIR> fields)
        {
            RuntimeIdentityFieldIR[] sortedFields = CopyAndSort(fields, CompareRuntimeIdentityField);
            StringBuilder builder = new StringBuilder();
            builder.Append('[');
            for (int index = 0; index < sortedFields.Length; index++)
            {
                if (index > 0)
                    builder.Append(", ");

                builder.Append(sortedFields[index].Name);
                builder.Append(":Type=");
                builder.Append(sortedFields[index].ValueType);
                builder.Append(":Required=");
                builder.Append(sortedFields[index].IsRequired);
            }

            builder.Append(']');
            return builder.ToString();
        }

        static int CompareModuleDependency(ModuleDependencyIR left, ModuleDependencyIR right)
        {
            int result = left.ModuleId.Value.CompareTo(right.ModuleId.Value);
            if (result != 0)
                return result;

            result = Nullable.Compare((int?)left.AbsenceBehavior, (int?)right.AbsenceBehavior);
            if (result != 0)
                return result;

            result = StringComparer.Ordinal.Compare(left.DisabledContribution, right.DisabledContribution);
            if (result != 0)
                return result;

            result = left.AlternativeModuleId.Value.CompareTo(right.AlternativeModuleId.Value);
            if (result != 0)
                return result;

            result = ((int)left.ProfileSpecificErrorProfiles).CompareTo((int)right.ProfileSpecificErrorProfiles);
            if (result != 0)
                return result;

            return left.Source.Value.CompareTo(right.Source.Value);
        }

        static int CompareScopeServiceRequirement(ScopeServiceRequirementIR left, ScopeServiceRequirementIR right)
        {
            int result = left.ServiceId.Value.CompareTo(right.ServiceId.Value);
            if (result != 0)
                return result;

            result = ((int)left.Strength).CompareTo((int)right.Strength);
            return result != 0 ? result : left.Source.Value.CompareTo(right.Source.Value);
        }

        static int CompareScopeValueInit(ScopeValueInitRefIR left, ScopeValueInitRefIR right)
        {
            int result = left.PlanId.Value.CompareTo(right.PlanId.Value);
            return result != 0 ? result : left.Source.Value.CompareTo(right.Source.Value);
        }

        static int CompareValueInitEntry(ValueInitEntryIR left, ValueInitEntryIR right)
        {
            int result = left.Order.CompareTo(right.Order);
            if (result != 0)
                return result;

            result = left.KeyId.Value.CompareTo(right.KeyId.Value);
            if (result != 0)
                return result;

            result = ((int)left.SourceKind).CompareTo((int)right.SourceKind);
            if (result != 0)
                return result;

            result = ((int)left.ValueKind).CompareTo((int)right.ValueKind);
            if (result != 0)
                return result;

            result = ((int)left.OverwritePolicy).CompareTo((int)right.OverwritePolicy);
            if (result != 0)
                return result;

            result = StringComparer.Ordinal.Compare(left.SerializedValue, right.SerializedValue);
            if (result != 0)
                return result;

            result = StringComparer.Ordinal.Compare(left.EvaluationLocalRef, right.EvaluationLocalRef);
            return result != 0 ? result : left.Source.Value.CompareTo(right.Source.Value);
        }

        static int CompareServiceContract(ServiceContractIR left, ServiceContractIR right)
        {
            int result = StringComparer.Ordinal.Compare(left.ContractName, right.ContractName);
            return result != 0 ? result : left.Source.Value.CompareTo(right.Source.Value);
        }

        static int CompareServiceDependency(ServiceDependencyIR left, ServiceDependencyIR right)
        {
            int result = CompareDependencyNode(left.Target, right.Target);
            if (result != 0)
                return result;

            result = ((int)left.Strength).CompareTo((int)right.Strength);
            return result != 0 ? result : left.Source.Value.CompareTo(right.Source.Value);
        }

        static int CompareCommandDependency(CommandDependencyIR left, CommandDependencyIR right)
        {
            int result = CompareDependencyNode(left.Target, right.Target);
            if (result != 0)
                return result;

            result = ((int)left.Strength).CompareTo((int)right.Strength);
            return result != 0 ? result : left.Source.Value.CompareTo(right.Source.Value);
        }

        static int CompareLifecycleStep(LifecycleStepIR left, LifecycleStepIR right)
        {
            int result = left.Order.CompareTo(right.Order);
            return result != 0 ? result : left.Id.Value.CompareTo(right.Id.Value);
        }

        static int CompareDependencyEdgeId(DependencyEdgeId left, DependencyEdgeId right)
        {
            return left.Value.CompareTo(right.Value);
        }

        static int CompareRuntimeIdentityField(RuntimeIdentityFieldIR left, RuntimeIdentityFieldIR right)
        {
            int result = StringComparer.Ordinal.Compare(left.Name, right.Name);
            if (result != 0)
                return result;

            result = StringComparer.Ordinal.Compare(left.ValueType, right.ValueType);
            if (result != 0)
                return result;

            return BoolToInt(left.IsRequired).CompareTo(BoolToInt(right.IsRequired));
        }

        static int CompareDependencyNode(DependencyNodeIR left, DependencyNodeIR right)
        {
            int result = ((int)left.Kind).CompareTo((int)right.Kind);
            if (result != 0)
                return result;

            result = left.ModuleId.Value.CompareTo(right.ModuleId.Value);
            if (result != 0)
                return result;

            result = left.ServiceId.Value.CompareTo(right.ServiceId.Value);
            if (result != 0)
                return result;

            result = left.ScopePlanId.Value.CompareTo(right.ScopePlanId.Value);
            if (result != 0)
                return result;

            result = left.CommandTypeId.Value.CompareTo(right.CommandTypeId.Value);
            if (result != 0)
                return result;

            result = left.ValueKeyId.Value.CompareTo(right.ValueKeyId.Value);
            if (result != 0)
                return result;

            result = left.LifecycleStepId.Value.CompareTo(right.LifecycleStepId.Value);
            return result != 0 ? result : left.RuntimeQueryId.Value.CompareTo(right.RuntimeQueryId.Value);
        }

        static int CompareDiagnosticSeed(DiagnosticSeedIR left, DiagnosticSeedIR right)
        {
            int result = StringComparer.Ordinal.Compare(left.SeedKey, right.SeedKey);
            if (result != 0)
                return result;

            result = StringComparer.Ordinal.Compare(left.DebugName, right.DebugName);
            if (result != 0)
                return result;

            result = left.OwnerModule.Value.CompareTo(right.OwnerModule.Value);
            return result != 0 ? result : left.Source.Value.CompareTo(right.Source.Value);
        }

        static int BoolToInt(bool value)
        {
            return value ? 1 : 0;
        }

        static int CompareCommandPayloadField(CommandPayloadFieldIR left, CommandPayloadFieldIR right)
        {
            int result = StringComparer.Ordinal.Compare(left.FieldPath, right.FieldPath);
            return result != 0 ? result : left.Source.Value.CompareTo(right.Source.Value);
        }

        static T[] CopyAndSort<T>(ReadOnlySpan<T> source, Comparison<T> comparison)
        {
            T[] copy = new T[source.Length];
            source.CopyTo(copy);
            Array.Sort(copy, comparison);
            return copy;
        }

        sealed class KernelIRTokenWriter : IDisposable
        {
            readonly MemoryStream stream;
            readonly BinaryWriter writer;

            public KernelIRTokenWriter()
            {
                stream = new MemoryStream();
                writer = new BinaryWriter(stream, Encoding.UTF8, true);
            }

            public void WriteBool(bool value)
            {
                writer.Write(value);
            }

            public void WriteInt(int value)
            {
                writer.Write(value);
            }

            public void WriteLong(long value)
            {
                writer.Write(value);
            }

            public void WriteString(string? value)
            {
                if (value == null)
                {
                    writer.Write((byte)0);
                    return;
                }

                writer.Write((byte)1);
                writer.Write(value);
            }

            public Hash128 ToHash128()
            {
                writer.Flush();
                byte[] digest;
                using (SHA256 sha256 = SHA256.Create())
                {
                    digest = sha256.ComputeHash(stream.ToArray());
                }

                return new Hash128(
                    ReadUInt32LittleEndian(digest, 0),
                    ReadUInt32LittleEndian(digest, 4),
                    ReadUInt32LittleEndian(digest, 8),
                    ReadUInt32LittleEndian(digest, 12));
            }

            public void Dispose()
            {
                writer.Dispose();
                stream.Dispose();
            }

            static uint ReadUInt32LittleEndian(byte[] bytes, int startIndex)
            {
                return (uint)(bytes[startIndex]
                    | (bytes[startIndex + 1] << 8)
                    | (bytes[startIndex + 2] << 16)
                    | (bytes[startIndex + 3] << 24));
            }
        }
    }
}