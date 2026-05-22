#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game;
using Game.Commands;
using Game.Commands.VNext;
using Game.Kernel.Abstractions;
using Game.Kernel.Boot;
using Game.Kernel.Generation;
using Game.Kernel.IR;
using Game.Kernel.Validation;
using Game.Project.Bootstrap;
using NUnit.Framework;
using UnityEngine;
using KernelHash128 = Game.Kernel.IR.Hash128;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class KernelVerifiedCommandRuntimeTests
    {
        const int CommandTypeIdValue = 7001;
        const int CommandSchemaIdValue = 7002;
        const int ValueKeyIdValue = 7101;
        const int RuntimeQueryIdValue = 7201;

        [SetUp]
        public void SetUp()
        {
            KernelVerifiedCommandRuntime.Deactivate();
        }

        [TearDown]
        public void TearDown()
        {
            KernelVerifiedCommandRuntime.Deactivate();
        }

        [Test]
        public void Activate_ThrowsWhenRequiredVerifiedPlansAreMissing()
        {
            KernelBootPublishedArtifactBundle bundle = CreateBundleWithoutVerifiedCommandArtifacts();
            BootValidationInput input = bundle.CreateValidationInput(new BootFallbackValidationState(false, false, false, false, false, false));
            KernelBootBoundaryResult result = KernelBootBoundary.Execute(input);

            Assert.That(result, Is.TypeOf<KernelBootBoundaryResult.Success>());

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                KernelVerifiedCommandRuntime.Activate(((KernelBootBoundaryResult.Success)result).RuntimeSurface));

            Assert.That(exception.Message, Does.Contain("CommandCatalogPlan"));
        }

        [Test]
        public void Activate_ThrowsWhenVerifiedCommandPlanReusesExecutorIds()
        {
            KernelBootRuntimeSurface runtimeSurface = CreateRuntimeSurface(CreateVerifiedCommandBundleWithDuplicateExecutorIds());

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                KernelVerifiedCommandRuntime.Activate(runtimeSurface));

            Assert.That(exception.Message, Does.Contain("unique positive CommandExecutorId values"));
        }

        [Test]
        public void Activate_BuildsVerifiedPayloadSchemaAndExplicitExecutorCatalog()
        {
            KernelBootRuntimeSurface runtimeSurface = CreateRuntimeSurface();

            KernelVerifiedCommandRuntime.Activate(runtimeSurface);

            Assert.That(VerifiedCommandRuntimeBridge.TryGetSession(out IVerifiedCommandRuntimeSession? session), Is.True);
            Assert.That(session, Is.Not.Null);
            Assert.That(session!.Catalog.TryGetPayloadSchema(CommandTypeIdValue, out CommandPayloadSchema schema), Is.True);
            Assert.That(schema.SchemaId, Is.EqualTo(CommandSchemaIdValue));
            Assert.That(schema.Fields.Length, Is.EqualTo(1));
            Assert.That(schema.Fields[0].FieldPath, Is.EqualTo("valueKey"));
            Assert.That(schema.Fields[0].ReferenceKind, Is.EqualTo(CommandPayloadReferenceKind.ValueKeyId));

            Assert.That(session.KeyResolver.TryResolve("test.command", out CommandKeyId keyId), Is.True);
            Assert.That(keyId.Value, Is.EqualTo(CommandTypeIdValue));
            Assert.That(session.Catalog.TryResolve(keyId, out _), Is.False);

            Assert.That(
                session.PayloadReferenceValidator.TryValidateReference(
                    CommandPayloadReferenceKind.ValueKeyId,
                    CommandPayloadFieldValue.FromReference(CommandPayloadFieldKind.ValueKeyId, ValueKeyIdValue),
                    out string validMessage),
                Is.True,
                validMessage);

            Assert.That(
                session.PayloadReferenceValidator.TryValidateReference(
                    CommandPayloadReferenceKind.ValueKeyId,
                    CommandPayloadFieldValue.FromReference(CommandPayloadFieldKind.ValueKeyId, 999999),
                    out string invalidMessage),
                Is.False);
            Assert.That(invalidMessage, Does.Contain("verified registry"));

            RuntimeContainerBuilder builder = new RuntimeContainerBuilder();
            builder.Register<TestCommandExecutor>(RuntimeLifetime.Singleton)
                .AsSelf();

            using IRuntimeResolver resolver = builder.Build();
            ICommandExecutorCatalog executorCatalog = session.CreateExecutorCatalog(
                resolver,
                new[] { ExplicitCommandExecutorBinding.For<TestCommandExecutor>() });

            Assert.That(executorCatalog.TryGet(CommandTypeIdValue, out ICommandExecutor executor), Is.True);
            Assert.That(executor, Is.TypeOf<TestCommandExecutor>());
        }

        [Test]
        public void Activate_DoesNotResolveUnknownStableKeysThroughRuntimeFallback()
        {
            KernelBootRuntimeSurface runtimeSurface = CreateRuntimeSurface();

            KernelVerifiedCommandRuntime.Activate(runtimeSurface);

            Assert.That(VerifiedCommandRuntimeBridge.TryGetSession(out IVerifiedCommandRuntimeSession? session), Is.True);
            Assert.That(session, Is.Not.Null);

            Assert.That(session!.KeyResolver.TryResolve("__missing_command_key__", out CommandKeyId keyId), Is.False);
            Assert.That(keyId.IsValid, Is.False);
        }

        [Test]
        public void ExplicitExecutorCatalog_RejectsBindingsOutsideVerifiedPlan()
        {
            KernelBootRuntimeSurface runtimeSurface = CreateRuntimeSurface();

            KernelVerifiedCommandRuntime.Activate(runtimeSurface);

            Assert.That(VerifiedCommandRuntimeBridge.TryGetSession(out IVerifiedCommandRuntimeSession? session), Is.True);
            Assert.That(session, Is.Not.Null);

            RuntimeContainerBuilder builder = new RuntimeContainerBuilder();
            builder.Register<WrongCommandExecutor>(RuntimeLifetime.Singleton)
                .AsSelf();

            using IRuntimeResolver resolver = builder.Build();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => session!.CreateExecutorCatalog(
                resolver,
                new[] { ExplicitCommandExecutorBinding.For<WrongCommandExecutor>() }));

            Assert.That(exception.Message, Does.Contain("no verified CommandCatalogPlan entry exists"));
        }

        [Test]
        public void InstallCommandRuntime_ThrowsWhenVerifiedCommandRuntimeIsInactive()
        {
            GameObject gameObject = new GameObject("command-runner-installer-test");
            try
            {
                CommandRunnerMB installer = gameObject.AddComponent<CommandRunnerMB>();
                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                    installer.InstallCommandRuntime(new RuntimeContainerBuilder(), new TestScopeNode(LifetimeScopeKind.Project)));

                Assert.That(exception.Message, Does.Contain("verified command runtime authority"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public async Task InstallCommandRuntime_NonProjectScopeReusesProjectExecutorCatalogAuthority()
        {
            KernelVerifiedCommandRuntime.Activate(CreateRuntimeSurface());

            RuntimeContainerBuilder projectBuilder = new RuntimeContainerBuilder();
            RecordingCommandExecutor recordingExecutor = new RecordingCommandExecutor();
            StubCommandExecutorCatalog projectCatalog = new StubCommandExecutorCatalog(recordingExecutor);
            projectBuilder.RegisterInstance<ICommandExecutorCatalog>(projectCatalog);

            using IRuntimeResolver projectResolver = projectBuilder.Build();

            TestScopeNode projectScope = new TestScopeNode(LifetimeScopeKind.Project);
            projectScope.SetResolver(projectResolver);

            TestScopeNode entityScope = new TestScopeNode(LifetimeScopeKind.Entity, projectScope);

            GameObject gameObject = new GameObject("command-runner-non-project-authority-test");
            try
            {
                CommandRunnerMB installer = gameObject.AddComponent<CommandRunnerMB>();
                RuntimeContainerBuilder builder = new RuntimeContainerBuilder();
                installer.InstallCommandRuntime(builder, entityScope);

                using IRuntimeResolver resolver = builder.Build();
                entityScope.SetResolver(resolver);

                Assert.That(resolver.Resolve<ICommandExecutorCatalog>(), Is.SameAs(projectCatalog));
                IEntityCommandRunner runner = resolver.Resolve<IEntityCommandRunner>();
                Assert.That(runner, Is.Not.Null);

                CommandRunResult result = await runner.ExecuteSingleAsync(
                    new TestPayloadCommandData(),
                    default!,
                    CancellationToken.None,
                    CommandRunOptions.Default);

                Assert.That(result.Status, Is.EqualTo(CommandRunStatus.Completed), result.Message);
                Assert.That(recordingExecutor.ExecuteCount, Is.EqualTo(1));
                Assert.That(recordingExecutor.LastScope, Is.SameAs(entityScope));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        static KernelBootRuntimeSurface CreateRuntimeSurface()
        {
            return CreateRuntimeSurface(CreateVerifiedCommandBundle());
        }

        static KernelBootRuntimeSurface CreateRuntimeSurface(KernelBootPublishedArtifactBundle bundle)
        {
            BootValidationInput input = bundle.CreateValidationInput(new BootFallbackValidationState(false, false, false, false, false, false));
            KernelBootBoundaryResult result = KernelBootBoundary.Execute(input);

            Assert.That(result.IsReady, Is.True);
            Assert.That(result, Is.TypeOf<KernelBootBoundaryResult.Success>());

            return (KernelBootRuntimeSurface)((KernelBootBoundaryResult.Success)result).RuntimeSurface;
        }

        static KernelBootPublishedArtifactBundle CreateVerifiedCommandBundle()
        {
            return CreateVerifiedCommandBundleCore(CreatePrimaryVerifiedCommands(), "PlanId:70001");
        }

        static KernelBootPublishedArtifactBundle CreateVerifiedCommandBundleWithDuplicateExecutorIds()
        {
            return CreateVerifiedCommandBundleCore(CreateDuplicateExecutorIdCommands(), "PlanId:70021");
        }

        static KernelBootPublishedArtifactBundle CreateVerifiedCommandBundleCore(CommandIR[] commands, string planLabel)
        {
            KernelProfile profile = new KernelProfile(new KernelProfileId(70001), KernelProfileKind.Development);
            ServiceIR[] services = Array.Empty<ServiceIR>();
            ScopeIR[] scopes = Array.Empty<ScopeIR>();
            KernelDebugMapEntry[] debugEntries = Array.Empty<KernelDebugMapEntry>();

            ValueKeyIR[] valueKeys =
            {
                new ValueKeyIR(
                    new ValueKeyId(ValueKeyIdValue),
                    "value.test",
                    "Value Test",
                    ValueKind.Int,
                    new ModuleId(70014),
                    new ValueSchemaRefIR(new ValueSchemaId(7102), new SourceLocationId(7103)),
                    new SavePolicyIR(false, false, null),
                    new SourceLocationId(7104)),
            };

            RuntimeQueryIR[] runtimeQueries =
            {
                new RuntimeQueryIR(
                    new RuntimeQueryId(RuntimeQueryIdValue),
                    "runtime.query.test",
                    RuntimeQueryTargetKind.Scope,
                    new[]
                    {
                        new RuntimeIdentityFieldIR("ScopeId", "int", true),
                    },
                    new RuntimeQueryPolicyIR(requiresUniqueResult: true, allowMissing: false, DependencyPhase.Runtime),
                    new ModuleId(70014),
                    new SourceLocationId(7202)),
            };

            KernelHash128 sourceHash = VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[]
            {
                "KernelVerifiedCommandRuntimeTests",
                planLabel,
            });

            KernelHash128 registryHash = VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[]
            {
                "KernelVerifiedCommandRuntimeTests",
                "Registry:" + planLabel,
            });

            KernelHash128 profileHash = VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[]
            {
                "KernelVerifiedCommandRuntimeTests",
                "Profile:" + profile.Kind,
            });

            KernelHash128 serviceHash = KernelProjectionHashing.ComputeServiceGraphHash(services);
            KernelHash128 scopeHash = KernelProjectionHashing.ComputeScopeGraphHash(scopes);
            KernelHash128 commandHash = KernelProjectionHashing.ComputeCommandCatalogHash(commands);
            KernelHash128 valueHash = KernelProjectionHashing.ComputeValueSchemaHash(valueKeys);
            KernelHash128 runtimeQueryHash = KernelProjectionHashing.ComputeRuntimeQueryHash(runtimeQueries);
            KernelHash128 debugMapHash = KernelProjectionHashing.ComputeDebugMapHash(debugEntries);

            ServiceGraphPlan serviceGraphPlan = new ServiceGraphPlan(
                CreateHeader(new ArtifactId(1), ArtifactKind.ServiceGraph, sourceHash, registryHash, profileHash, debugMapHash, serviceHash),
                services);

            ScopeGraphPlan scopeGraphPlan = new ScopeGraphPlan(
                CreateHeader(new ArtifactId(2), ArtifactKind.ScopeGraph, sourceHash, registryHash, profileHash, debugMapHash, scopeHash),
                scopes);

            CommandCatalogPlan commandCatalogPlan = new CommandCatalogPlan(
                CreateHeader(new ArtifactId(4), ArtifactKind.CommandCatalog, sourceHash, registryHash, profileHash, debugMapHash, commandHash),
                commands);

            ValueSchemaPlan valueSchemaPlan = new ValueSchemaPlan(
                CreateHeader(new ArtifactId(5), ArtifactKind.ValueSchema, sourceHash, registryHash, profileHash, debugMapHash, valueHash),
                valueKeys);

            RuntimeQueryPlan runtimeQueryPlan = new RuntimeQueryPlan(
                CreateHeader(new ArtifactId(6), ArtifactKind.RuntimeQuery, sourceHash, registryHash, profileHash, debugMapHash, runtimeQueryHash),
                runtimeQueries);

            KernelDebugMap debugMap = new KernelDebugMap(
                CreateHeader(new ArtifactId(7), ArtifactKind.KernelDebugMap, sourceHash, registryHash, profileHash, debugMapHash, debugMapHash),
                debugEntries);

            VerifiedArtifactSetRef artifactSet = new VerifiedArtifactSetRef(
                new ArtifactSetId(70001),
                new PlanId(70001),
                sourceHash.ToString(),
                profileHash.ToString(),
                1,
                registryHash.ToString(),
                debugMapHash.ToString());

            KernelBootManifest manifest = new KernelBootManifest(
                new ManifestId(70001),
                profile.Id,
                artifactSet,
                new BootPolicyId(70001),
                BootDiagnosticsPolicy.ForKind(profile.Kind));

            return new KernelBootPublishedArtifactBundle(
                manifest,
                profile,
                serviceGraphPlan,
                scopeGraphPlan,
                lifecyclePlan: null,
                debugMap,
                commandCatalogPlan: commandCatalogPlan,
                valueSchemaPlan: valueSchemaPlan,
                runtimeQueryPlan: runtimeQueryPlan);
        }

        static CommandIR[] CreatePrimaryVerifiedCommands()
        {
            return new[]
            {
                new CommandIR(
                    new CommandTypeId(CommandTypeIdValue),
                    "TestCommand",
                    new CommandAuthoringKeyRefIR(new CommandAuthoringKeyId(70011), "test.command", new SourceLocationId(70012)),
                    new CommandCategoryId(70013),
                    new ModuleId(70014),
                    new CommandPayloadSchemaRefIR(
                        new CommandPayloadSchemaId(CommandSchemaIdValue),
                        new SourceLocationId(70015),
                        new[]
                        {
                            new CommandPayloadFieldIR(
                                "valueKey",
                                CommandPayloadFieldKindIR.ValueKeyId,
                                CommandPayloadFieldRequirementIR.Required,
                                new SourceLocationId(70016),
                                CommandPayloadReferenceKindIR.ValueKeyId),
                        }),
                    new CommandExecutorRefIR(new CommandExecutorId(70017), new SourceLocationId(70018)),
                    Array.Empty<CommandDependencyIR>(),
                    new SourceLocationId(70019)),
            };
        }

        static CommandIR[] CreateDuplicateExecutorIdCommands()
        {
            return new[]
            {
                new CommandIR(
                    new CommandTypeId(CommandTypeIdValue),
                    "TestCommand",
                    new CommandAuthoringKeyRefIR(new CommandAuthoringKeyId(70011), "test.command", new SourceLocationId(70012)),
                    new CommandCategoryId(70013),
                    new ModuleId(70014),
                    new CommandPayloadSchemaRefIR(
                        new CommandPayloadSchemaId(CommandSchemaIdValue),
                        new SourceLocationId(70015),
                        new[]
                        {
                            new CommandPayloadFieldIR(
                                "valueKey",
                                CommandPayloadFieldKindIR.ValueKeyId,
                                CommandPayloadFieldRequirementIR.Required,
                                new SourceLocationId(70016),
                                CommandPayloadReferenceKindIR.ValueKeyId),
                        }),
                    new CommandExecutorRefIR(new CommandExecutorId(70017), new SourceLocationId(70018)),
                    Array.Empty<CommandDependencyIR>(),
                    new SourceLocationId(70019)),
                new CommandIR(
                    new CommandTypeId(7003),
                    "DuplicateExecutorCommand",
                    new CommandAuthoringKeyRefIR(new CommandAuthoringKeyId(70031), "test.command.duplicate", new SourceLocationId(70032)),
                    new CommandCategoryId(70033),
                    new ModuleId(70014),
                    new CommandPayloadSchemaRefIR(
                        new CommandPayloadSchemaId(7004),
                        new SourceLocationId(70035),
                        Array.Empty<CommandPayloadFieldIR>()),
                    new CommandExecutorRefIR(new CommandExecutorId(70017), new SourceLocationId(70036)),
                    Array.Empty<CommandDependencyIR>(),
                    new SourceLocationId(70037)),
            };
        }

        static KernelBootPublishedArtifactBundle CreateBundleWithoutVerifiedCommandArtifacts()
        {
            KernelProfile profile = new KernelProfile(new KernelProfileId(73001), KernelProfileKind.Development);
            ServiceIR[] services = Array.Empty<ServiceIR>();
            ScopeIR[] scopes = Array.Empty<ScopeIR>();
            KernelDebugMapEntry[] debugEntries = Array.Empty<KernelDebugMapEntry>();

            KernelHash128 sourceHash = VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[]
            {
                "KernelVerifiedCommandRuntimeTests",
                "MissingArtifacts",
            });

            KernelHash128 registryHash = VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[]
            {
                "KernelVerifiedCommandRuntimeTests",
                "MissingArtifactsRegistry",
            });

            KernelHash128 profileHash = VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[]
            {
                "KernelVerifiedCommandRuntimeTests",
                "Profile:" + profile.Kind,
            });

            KernelHash128 serviceHash = KernelProjectionHashing.ComputeServiceGraphHash(services);
            KernelHash128 scopeHash = KernelProjectionHashing.ComputeScopeGraphHash(scopes);
            KernelHash128 debugMapHash = KernelProjectionHashing.ComputeDebugMapHash(debugEntries);

            ServiceGraphPlan serviceGraphPlan = new ServiceGraphPlan(
                CreateHeader(new ArtifactId(31), ArtifactKind.ServiceGraph, sourceHash, registryHash, profileHash, debugMapHash, serviceHash),
                services);

            ScopeGraphPlan scopeGraphPlan = new ScopeGraphPlan(
                CreateHeader(new ArtifactId(32), ArtifactKind.ScopeGraph, sourceHash, registryHash, profileHash, debugMapHash, scopeHash),
                scopes);

            KernelDebugMap debugMap = new KernelDebugMap(
                CreateHeader(new ArtifactId(37), ArtifactKind.KernelDebugMap, sourceHash, registryHash, profileHash, debugMapHash, debugMapHash),
                debugEntries);

            VerifiedArtifactSetRef artifactSet = new VerifiedArtifactSetRef(
                new ArtifactSetId(73001),
                new PlanId(73001),
                sourceHash.ToString(),
                profileHash.ToString(),
                1,
                registryHash.ToString(),
                debugMapHash.ToString());

            KernelBootManifest manifest = new KernelBootManifest(
                new ManifestId(73001),
                profile.Id,
                artifactSet,
                new BootPolicyId(73001),
                BootDiagnosticsPolicy.ForKind(profile.Kind));

            return new KernelBootPublishedArtifactBundle(
                manifest,
                profile,
                serviceGraphPlan,
                scopeGraphPlan,
                lifecyclePlan: null,
                debugMap);
        }

        static VerifiedArtifactHeader CreateHeader(
            ArtifactId artifactId,
            ArtifactKind artifactKind,
            KernelHash128 sourceHash,
            KernelHash128 registryHash,
            KernelHash128 profileHash,
            KernelHash128 debugMapHash,
            KernelHash128 contentHash)
        {
            return new VerifiedArtifactHeader(
                new PlanId(70001),
                new ArtifactSetId(70001),
                artifactId,
                artifactKind,
                1,
                sourceHash,
                registryHash,
                profileHash,
                debugMapHash,
                contentHash,
                "KernelVerifiedCommandRuntimeTests");
        }

        sealed class TestCommandExecutor : ICommandExecutor
        {
            public int CommandId => CommandTypeIdValue;

            public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
            {
                _ = data;
                _ = ctx;
                _ = ct;
                return UniTask.CompletedTask;
            }
        }

        sealed class WrongCommandExecutor : ICommandExecutor
        {
            public int CommandId => 7999;

            public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
            {
                _ = data;
                _ = ctx;
                _ = ct;
                return UniTask.CompletedTask;
            }
        }

        sealed class StubCommandExecutorCatalog : ICommandExecutorCatalog
        {
            readonly ICommandExecutor executor;

            public StubCommandExecutorCatalog(ICommandExecutor executor)
            {
                this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
            }

            public bool TryGet(int commandId, out ICommandExecutor executor)
            {
                if (this.executor.CommandId == commandId)
                {
                    executor = this.executor;
                    return true;
                }

                executor = null!;
                return false;
            }
        }

        sealed class RecordingCommandExecutor : ICommandExecutor
        {
            public int ExecuteCount { get; private set; }

            public IScopeNode? LastScope { get; private set; }

            public int CommandId => CommandTypeIdValue;

            public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
            {
                _ = data;
                _ = ct;
                ExecuteCount++;
                LastScope = ctx.Scope;
                return UniTask.CompletedTask;
            }
        }

        sealed class TestPayloadCommandData : ICommandData, ICommandPayloadFieldReader
        {
            public int CommandId => CommandTypeIdValue;

            public string DebugData => "TestPayloadCommandData";

            public bool TryReadPayloadField(string fieldPath, out CommandPayloadFieldValue value)
            {
                if (fieldPath == "valueKey")
                {
                    value = CommandPayloadFieldValue.FromReference(CommandPayloadFieldKind.ValueKeyId, ValueKeyIdValue);
                    return true;
                }

                value = CommandPayloadFieldValue.Missing();
                return false;
            }

            public void CollectPayloadFieldPaths(ICollection<string> fieldPaths)
            {
                fieldPaths.Add("valueKey");
            }
        }

        sealed class TestScopeNode : IScopeNode
        {
            readonly IReadOnlyList<IScopeNode> path;
            IRuntimeResolver? resolver;

            public TestScopeNode(LifetimeScopeKind kind, IScopeNode? parent = null)
            {
                Kind = kind;
                Parent = parent;

                List<IScopeNode> computedPath = new List<IScopeNode>();
                IReadOnlyList<IScopeNode>? parentPath = parent?.GetPathFromRoot();
                if (parentPath != null)
                {
                    for (int index = 0; index < parentPath.Count; index++)
                        computedPath.Add(parentPath[index]);
                }

                computedPath.Add(this);
                path = computedPath;
            }

            public IScopeNode? Parent { get; }

            public IScopeIdentityService? Identity => null;

            public LifetimeScopeKind Kind { get; }

            public IRuntimeResolver? Resolver => resolver;

            public bool IsVisible => true;

            public bool IsActive => true;

            public void SetResolver(IRuntimeResolver? value)
            {
                resolver = value;
            }

            public bool TrySetVisible(bool visible, bool isReset = false)
            {
                _ = visible;
                _ = isReset;
                return true;
            }

            public bool TrySetActive(bool active, bool isReset = false)
            {
                _ = active;
                _ = isReset;
                return true;
            }

            public UniTask SetActiveAsync(bool active, bool isReset = false, CancellationToken ct = default)
            {
                _ = active;
                _ = isReset;
                _ = ct;
                return UniTask.CompletedTask;
            }

            public IReadOnlyList<IScopeNode>? GetPathFromRoot()
            {
                return path;
            }
        }
    }
}
