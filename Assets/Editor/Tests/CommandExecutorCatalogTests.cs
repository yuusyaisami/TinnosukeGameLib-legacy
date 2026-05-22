#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Commands;
using Game.Commands.VNext;
using Game.Common;
using Game.Flow;
using Game.Kernel.Generation;
using Game.Kernel.IR;
using NUnit.Framework;
using UnityEngine;
using KernelHash128 = Game.Kernel.IR.Hash128;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class CommandExecutorCatalogTests
    {
        [Test]
        public void CommandKeyResolver_FailsClosedWhenStableKeyIsMissing()
        {
            FieldInfo? cachedRegistryField = typeof(CommandKeyRegistryLocator).GetField("_cachedRegistry", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(cachedRegistryField, Is.Not.Null, "CommandKeyRegistryLocator cache field must exist for the test harness.");

            CommandKeyRegistry? previousRegistry = cachedRegistryField!.GetValue(null) as CommandKeyRegistry;
            CommandKeyRegistry testRegistry = ScriptableObject.CreateInstance<CommandKeyRegistry>();

            try
            {
                cachedRegistryField.SetValue(null, testRegistry);

                CommandKeyResolver resolver = new();
                bool resolved = resolver.TryResolve("missing.command.key", out CommandKeyId keyId);

                Assert.That(resolved, Is.False);
                Assert.That(keyId.IsValid, Is.False);
            }
            finally
            {
                cachedRegistryField.SetValue(null, previousRegistry);
                UnityEngine.Object.DestroyImmediate(testRegistry);
            }
        }

        [Test]
        public void TryGet_ReturnsRegisteredExecutor()
        {
            TestCommandExecutor executor = new(101);
            CommandExecutorCatalog catalog = new(new ICommandExecutor[] { executor });

            bool found = catalog.TryGet(101, out ICommandExecutor resolved);

            Assert.That(found, Is.True);
            Assert.That(resolved, Is.SameAs(executor));
        }

        [Test]
        public void Constructor_RejectsDuplicateIds()
        {
            TestCommandExecutor first = new(202);
            TestCommandExecutor second = new(202);

            ArgumentException exception = Assert.Throws<ArgumentException>(() => new CommandExecutorCatalog(new ICommandExecutor[] { first, second }));

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.Message, Does.Contain("Duplicate CommandId"));
        }

        [Test]
        public void Constructor_RejectsInvalidIdsAndNullEntries()
        {
            TestCommandExecutor valid = new(303);

            ArgumentException exception = Assert.Throws<ArgumentException>(() => new CommandExecutorCatalog(new ICommandExecutor[]
            {
                null,
                new TestCommandExecutor(0),
                valid,
            }));

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.Message, Does.Contain("Executor entry is null"));
            Assert.That(exception!.Message, Does.Contain("Invalid CommandId"));
        }

        [Test]
        public void PayloadValidator_AcceptsDeclaredFields()
        {
            TestPayloadSchemaCatalog catalog = new(new CommandPayloadSchema(
                404,
                405,
                CommandPayloadUnknownFieldPolicy.Reject,
                new[]
                {
                    new CommandPayloadFieldSchema("valueKey", CommandPayloadFieldKind.ValueKeyId, CommandPayloadFieldRequirement.Required, CommandPayloadReferenceKind.ValueKeyId),
                }));
            TestCommandData data = new(404);
            data.Set("valueKey", CommandPayloadFieldValue.FromReference(CommandPayloadFieldKind.ValueKeyId, 301));

            CommandPayloadValidationResult result = CommandPayloadValidator.Validate(data, catalog);

            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void VerifiedPayloadSchemaCatalog_UsesCommandCatalogPlanFields()
        {
            CommandIR[] commands =
            {
                new CommandIR(
                    new CommandTypeId(808),
                    "TestCommand",
                    new CommandAuthoringKeyRefIR(new CommandAuthoringKeyId(809), "test.command", new SourceLocationId(1)),
                    new CommandCategoryId(810),
                    new ModuleId(811),
                    new CommandPayloadSchemaRefIR(
                        new CommandPayloadSchemaId(812),
                        new SourceLocationId(2),
                        new[]
                        {
                            new CommandPayloadFieldIR(
                                "valueKey",
                                CommandPayloadFieldKindIR.ValueKeyId,
                                CommandPayloadFieldRequirementIR.Required,
                                new SourceLocationId(3),
                                CommandPayloadReferenceKindIR.ValueKeyId),
                        }),
                    new CommandExecutorRefIR(new CommandExecutorId(813), new SourceLocationId(4)),
                    Array.Empty<CommandDependencyIR>(),
                    new SourceLocationId(5)),
            };
            KernelHash128 contentHash = KernelProjectionHashing.ComputeCommandCatalogHash(commands);
            VerifiedArtifactHeader header = new(
                new PlanId(814),
                new ArtifactSetId(815),
                new ArtifactId(816),
                ArtifactKind.CommandCatalog,
                4,
                new KernelHash128(1, 2, 3, 4),
                new KernelHash128(5, 6, 7, 8),
                new KernelHash128(9, 10, 11, 12),
                new KernelHash128(13, 14, 15, 16),
                contentHash,
                "1.0.0");
            CommandCatalogPlan plan = new(header, commands);
            CommandEntryPlan entry = plan.Entries[0];
            CommandPayloadFieldPlan field = entry.PayloadSchema.Fields[0];
            CommandPayloadSchema schemaFromVerifiedPlan = new(
                entry.TypeId.Value,
                entry.PayloadSchema.SchemaId.Value,
                CommandPayloadUnknownFieldPolicy.Reject,
                new[]
                {
                    new CommandPayloadFieldSchema(
                        field.FieldPath,
                        CommandPayloadFieldKind.ValueKeyId,
                        CommandPayloadFieldRequirement.Required,
                        CommandPayloadReferenceKind.ValueKeyId,
                        field.AllowNull,
                        field.Source.Value),
                });

            VerifiedCommandPayloadSchemaCatalog catalog = new(new[] { schemaFromVerifiedPlan });
            bool found = catalog.TryGetPayloadSchema(808, out CommandPayloadSchema schema);

            Assert.That(found, Is.True);
            Assert.That(schema.SchemaId, Is.EqualTo(812));
            Assert.That(schema.Fields.Length, Is.EqualTo(1));
            Assert.That(schema.Fields[0].FieldPath, Is.EqualTo("valueKey"));
            Assert.That(schema.Fields[0].ReferenceKind, Is.EqualTo(CommandPayloadReferenceKind.ValueKeyId));
        }

        [Test]
        public void PayloadValidator_UsesExternalAccessorProvider()
        {
            TestPayloadSchemaCatalog catalog = new(new CommandPayloadSchema(
                818,
                819,
                CommandPayloadUnknownFieldPolicy.Reject,
                new[]
                {
                    new CommandPayloadFieldSchema("amount", CommandPayloadFieldKind.Int, CommandPayloadFieldRequirement.Required),
                }));
            AccessorOnlyCommandData data = new(818, 42);
            CommandPayloadFieldReaderProvider provider = new();
            provider.Register<AccessorOnlyCommandData>(
                static (AccessorOnlyCommandData command, string fieldPath, out CommandPayloadFieldValue value) =>
                {
                    if (string.Equals(fieldPath, "amount", StringComparison.Ordinal))
                    {
                        value = CommandPayloadFieldValue.FromInt(command.Amount);
                        return true;
                    }

                    value = CommandPayloadFieldValue.Missing();
                    return false;
                },
                static (command, fieldPaths) =>
                {
                    _ = command;
                    fieldPaths.Add("amount");
                });

            CommandPayloadValidationResult result = CommandPayloadValidator.Validate(
                data,
                new CommandPayloadValidationContext(catalog, provider));

            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void PayloadValidator_RejectsMissingRequiredField()
        {
            TestPayloadSchemaCatalog catalog = new(new CommandPayloadSchema(
                505,
                506,
                CommandPayloadUnknownFieldPolicy.Reject,
                new[]
                {
                    new CommandPayloadFieldSchema("amount", CommandPayloadFieldKind.Int, CommandPayloadFieldRequirement.Required),
                }));
            TestCommandData data = new(505);

            CommandPayloadValidationResult result = CommandPayloadValidator.Validate(data, catalog);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.FieldPath, Is.EqualTo("amount"));
            Assert.That(result.Message, Does.Contain("missing"));
        }

        [Test]
        public void PayloadValidator_RejectsUnknownFieldsWhenPolicyRejects()
        {
            TestPayloadSchemaCatalog catalog = new(CommandPayloadSchema.Empty(606, 607));
            TestCommandData data = new(606);
            data.Set("unexpected", CommandPayloadFieldValue.FromInt(1));

            CommandPayloadValidationResult result = CommandPayloadValidator.Validate(data, catalog);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.FieldPath, Is.EqualTo("unexpected"));
            Assert.That(result.Message, Does.Contain("unknown field"));
        }

        [Test]
        public void PayloadValidator_RejectsInvalidValueKeyReference()
        {
            TestPayloadSchemaCatalog catalog = new(new CommandPayloadSchema(
                707,
                708,
                CommandPayloadUnknownFieldPolicy.Reject,
                new[]
                {
                    new CommandPayloadFieldSchema("valueKey", CommandPayloadFieldKind.ValueKeyId, CommandPayloadFieldRequirement.Required, CommandPayloadReferenceKind.ValueKeyId),
                }));
            TestCommandData data = new(707);
            data.Set("valueKey", CommandPayloadFieldValue.FromReference(CommandPayloadFieldKind.ValueKeyId, 0));

            CommandPayloadValidationResult result = CommandPayloadValidator.Validate(data, catalog);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.FieldPath, Is.EqualTo("valueKey"));
            Assert.That(result.Message, Does.Contain("reference"));
        }

        [Test]
        public void PayloadValidator_RejectsValueKeyMissingFromVerifiedRegistry()
        {
            TestPayloadSchemaCatalog catalog = new(new CommandPayloadSchema(
                727,
                728,
                CommandPayloadUnknownFieldPolicy.Reject,
                new[]
                {
                    new CommandPayloadFieldSchema("valueKey", CommandPayloadFieldKind.ValueKeyId, CommandPayloadFieldRequirement.Required, CommandPayloadReferenceKind.ValueKeyId),
                }));
            TestCommandData data = new(727);
            data.Set("valueKey", CommandPayloadFieldValue.FromReference(CommandPayloadFieldKind.ValueKeyId, 301));
            CommandPayloadReferenceRegistry references = new(valueKeyIds: new[] { 302 });

            CommandPayloadValidationResult result = CommandPayloadValidator.Validate(
                data,
                new CommandPayloadValidationContext(catalog, referenceValidator: references));

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.FieldPath, Is.EqualTo("valueKey"));
            Assert.That(result.Message, Does.Contain("verified registry"));
        }

        [Test]
        public async UniTask CommandRunner_DoesNotInvokeExecutorWhenPayloadInvalid()
        {
            TestCommandExecutor executor = new(909);
            CommandExecutorCatalog executorCatalog = new(new ICommandExecutor[] { executor });
            TestPayloadSchemaCatalog catalog = new(new CommandPayloadSchema(
                909,
                910,
                CommandPayloadUnknownFieldPolicy.Reject,
                new[]
                {
                    new CommandPayloadFieldSchema("amount", CommandPayloadFieldKind.Int, CommandPayloadFieldRequirement.Required),
                }));
            TestScopeNode scope = new();
            CommandRunner runner = new(
                scope,
                executorCatalog,
                catalog,
                NullCommandKeyResolver.Instance,
                NullCommandResolveLogger.Instance,
                SelfCommandPayloadFieldReaderProvider.Instance,
                MissingCommandPayloadReferenceValidator.Instance);
            CommandContext context = new(scope, NullVarStore.Instance, runner);
            TestCommandData data = new(909);

            CommandRunResult result = await runner.ExecuteSingleAsync(data, context, CancellationToken.None, CommandRunOptions.Default);

            Assert.That(result.Status, Is.EqualTo(CommandRunStatus.Error));
            Assert.That(result.FailureKind, Is.EqualTo(CommandRunFailureKind.PayloadInvalid));
            Assert.That(executor.ExecutionCount, Is.EqualTo(0));
        }

        [Test]
        public async UniTask CommandRunner_AttachesFrameAndLocalBeforeExecutor()
        {
            CommandFrameSnapshot observedFrame = default;
            CommandLocal? observedLocal = null;
            TestCommandExecutor executor = new(920, (data, ctx, ct) =>
            {
                _ = data;
                _ = ct;
                observedFrame = ctx.CurrentFrame;
                observedLocal = ctx.Local;
                return UniTask.CompletedTask;
            });
            CommandRunner runner = CreateRunner(920, executor, CommandPayloadSchema.Empty(920, 921));
            CommandContext context = new(runner.Scope, NullVarStore.Instance, runner);

            CommandRunResult result = await runner.ExecuteSingleAsync(new TestCommandData(920), context, CancellationToken.None, CommandRunOptions.Default);

            Assert.That(result.Status, Is.EqualTo(CommandRunStatus.Completed));
            Assert.That(observedFrame.FrameId.IsValid, Is.True);
            Assert.That(observedFrame.CommandId, Is.EqualTo(920));
            Assert.That(observedLocal, Is.Not.Null);
            Assert.That(observedLocal!.OwnerFrameId, Is.EqualTo(observedFrame.FrameId));
            Assert.That(result.RootFrame.FrameId, Is.EqualTo(observedFrame.FrameId));
        }

        [Test]
        public async UniTask CommandRunner_IsolatesCommandLocalPerFrameInList()
        {
            List<CommandFrameId> localOwners = new();
            TestCommandExecutor executor = new(922, (data, ctx, ct) =>
            {
                _ = data;
                _ = ct;
                localOwners.Add(ctx.Local.OwnerFrameId);
                return UniTask.CompletedTask;
            });
            CommandRunner runner = CreateRunner(922, executor, CommandPayloadSchema.Empty(922, 923));
            CommandContext context = new(runner.Scope, NullVarStore.Instance, runner);
            CommandListData list = new();
            list.Add(new TestCommandSource(new TestCommandData(922), "first"));
            list.Add(new TestCommandSource(new TestCommandData(922), "second"));

            CommandRunResult result = await runner.ExecuteListAsync(list, context, CancellationToken.None, CommandRunOptions.Default);

            Assert.That(result.Status, Is.EqualTo(CommandRunStatus.Completed));
            Assert.That(localOwners.Count, Is.EqualTo(2));
            Assert.That(localOwners[0].IsValid, Is.True);
            Assert.That(localOwners[1].IsValid, Is.True);
            Assert.That(localOwners[0], Is.Not.EqualTo(localOwners[1]));
        }

        [Test]
        public async UniTask CommandRunner_ConvertsFrameTimeoutToStructuredFailure()
        {
            TestCommandExecutor executor = new(924, async (data, ctx, ct) =>
            {
                _ = data;
                _ = ctx;
                await UniTask.Delay(TimeSpan.FromMilliseconds(500), cancellationToken: ct);
            });
            CommandRunner runner = CreateRunner(924, executor, CommandPayloadSchema.Empty(924, 925));
            CommandContext context = new(runner.Scope, NullVarStore.Instance, runner);
            CommandRunOptions options = CommandRunOptions.Default.WithFramePolicy(CommandExecutionDomain.Test, CommandFailureBoundary.FailFrame, timeoutMilliseconds: 10);

            CommandRunResult result = await runner.ExecuteSingleAsync(new TestCommandData(924), context, CancellationToken.None, options);

            Assert.That(result.Status, Is.EqualTo(CommandRunStatus.Error));
            Assert.That(result.FailureKind, Is.EqualTo(CommandRunFailureKind.Timeout));
            Assert.That(result.TimedOut, Is.True);
            Assert.That(result.RootFrame.Domain, Is.EqualTo(CommandExecutionDomain.Test));
        }

        [Test]
        public async UniTask CommandRunner_CancelsInFlightFrameWhenCallerTokenIsCanceled()
        {
            UniTaskCompletionSource started = new();
            TestCommandExecutor executor = new(925, async (data, ctx, ct) =>
            {
                _ = data;
                _ = ctx;
                started.TrySetResult();
                await UniTask.Delay(TimeSpan.FromSeconds(5), cancellationToken: ct);
            });
            CommandRunner runner = CreateRunner(925, executor, CommandPayloadSchema.Empty(925, 926));
            CommandContext context = new(runner.Scope, NullVarStore.Instance, runner);
            CancellationTokenSource cancellation = new();

            UniTask<CommandRunResult> execution = runner.ExecuteSingleAsync(new TestCommandData(925), context, cancellation.Token, CommandRunOptions.Default);
            await started.Task;
            cancellation.Cancel();

            CommandRunResult result = await execution;

            Assert.That(result.Status, Is.EqualTo(CommandRunStatus.Canceled));
            Assert.That(result.FailureKind, Is.EqualTo(CommandRunFailureKind.Canceled));
            Assert.That(result.TimedOut, Is.False);
        }

        [Test]
        public async UniTask CommandRunner_ExplicitCancelInterruptsInFlightDelayExecutor()
        {
            TestCommandExecutor afterDelay = new(939);
            CommandRunner runner = CreateRunner(
                new[]
                {
                    CommandPayloadSchema.Empty(CommandIds.Sequence, 9391, CommandPayloadUnknownFieldPolicy.Ignore),
                    CommandPayloadSchema.Empty(CommandIds.DelayExecutor, 9392, CommandPayloadUnknownFieldPolicy.Ignore),
                    CommandPayloadSchema.Empty(CommandIds.Cancel, 9393, CommandPayloadUnknownFieldPolicy.Ignore),
                    CommandPayloadSchema.Empty(939, 9394, CommandPayloadUnknownFieldPolicy.Ignore),
                },
                new SequenceExecutor(),
                new DelayExecutorExecutor(),
                new CancelExecutor(),
                afterDelay);
            CommandContext context = new(runner.Scope, NullVarStore.Instance, runner);

            DelayExecutorCommandData delay = new()
            {
                DelaySeconds = DynamicValueExtensions.FromLiteral(0.5f),
                FirstCommands = new CommandListData(),
                SecondCommands = new CommandListData(),
            };
            delay.FirstCommands.Add(new TestCommandSource(new CancelCommandData(), "delay-first-cancel"));
            delay.SecondCommands.Add(new TestCommandSource(new TestCommandData(939), "delay-second"));

            SequenceCommandData data = new();
            data.BodyCommands.Add(new TestCommandSource(delay, "sequence-delay"));

            CommandRunResult result = await runner.ExecuteSingleAsync(data, context, CancellationToken.None, CommandRunOptions.Default);

            Assert.That(result.Status, Is.EqualTo(CommandRunStatus.Canceled));
            Assert.That(result.FailureKind, Is.EqualTo(CommandRunFailureKind.Canceled));
            Assert.That(afterDelay.ExecutionCount, Is.EqualTo(0));
        }

        [Test]
        public void CommandRunner_RejectsDetachedExecutionWithoutPolicy()
        {
            TestCommandExecutor executor = new(926);
            CommandRunner runner = CreateRunner(926, executor, CommandPayloadSchema.Empty(926, 927));
            CommandContext context = new(runner.Scope, NullVarStore.Instance, runner);
            ICommandDetachedRunner detachedRunner = runner;
            bool invoked = false;

            CommandRunResult result = detachedRunner.StartDetached(
                context,
                CommandDetachedExecutionPolicy.Forbidden,
                CancellationToken.None,
                (ctx, ct) =>
                {
                    _ = ctx;
                    _ = ct;
                    invoked = true;
                    return UniTask.FromResult(CommandRunResult.Completed(-1, 0, CommandRunFailureKind.None, -1, string.Empty, null, null));
                });

            Assert.That(result.Status, Is.EqualTo(CommandRunStatus.Error));
            Assert.That(result.FailureKind, Is.EqualTo(CommandRunFailureKind.DetachedPolicyMissing));
            Assert.That(invoked, Is.False);
        }

        [Test]
        public async UniTask CommandRunner_StartsDetachedExecutionWithExplicitPolicy()
        {
            TestCommandExecutor executor = new(928);
            CommandRunner runner = CreateRunner(928, executor, CommandPayloadSchema.Empty(928, 929));
            CommandContext context = new(runner.Scope, NullVarStore.Instance, runner);
            ICommandDetachedRunner detachedRunner = runner;
            UniTaskCompletionSource completion = new();
            CommandDetachedExecutionPolicy policy = new(
                isAllowed: true,
                new CommandFrameId(1),
                runner.Scope,
                CommandDetachedCancellationMode.DetachFromCaller,
                "test",
                "detached-test");

            CommandRunResult result = detachedRunner.StartDetached(
                context,
                policy,
                CancellationToken.None,
                (ctx, ct) =>
                {
                    _ = ctx;
                    _ = ct;
                    completion.TrySetResult();
                    return UniTask.FromResult(CommandRunResult.Completed(-1, 0, CommandRunFailureKind.None, -1, string.Empty, null, null));
                });

            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(1));
            await completion.Task.AttachExternalCancellation(timeout.Token);
            Assert.That(result.Status, Is.EqualTo(CommandRunStatus.Completed));
        }

        [Test]
        public async UniTask CommandRunner_SequenceStopsAtExplicitCancelAndRunsOnCanceledCommands()
        {
            TestCommandExecutor first = new(930);
            TestCommandExecutor after = new(931);
            TestCommandExecutor canceled = new(932);
            CommandRunner runner = CreateRunner(
                new[]
                {
                    CommandPayloadSchema.Empty(CommandIds.Sequence, 9301, CommandPayloadUnknownFieldPolicy.Ignore),
                    CommandPayloadSchema.Empty(930, 9302, CommandPayloadUnknownFieldPolicy.Ignore),
                    CommandPayloadSchema.Empty(931, 9303, CommandPayloadUnknownFieldPolicy.Ignore),
                    CommandPayloadSchema.Empty(932, 9304, CommandPayloadUnknownFieldPolicy.Ignore),
                    CommandPayloadSchema.Empty(CommandIds.Cancel, 9305, CommandPayloadUnknownFieldPolicy.Ignore),
                },
                new SequenceExecutor(),
                first,
                after,
                canceled,
                new CancelExecutor());
            CommandContext context = new(runner.Scope, NullVarStore.Instance, runner);
            SequenceCommandData data = new();
            data.BodyCommands.Add(new TestCommandSource(new TestCommandData(930), "sequence-first"));
            data.BodyCommands.Add(new TestCommandSource(new CancelCommandData(), "sequence-cancel"));
            data.BodyCommands.Add(new TestCommandSource(new TestCommandData(931), "sequence-after"));
            data.OnCanceledCommands.Add(new TestCommandSource(new TestCommandData(932), "sequence-on-canceled"));

            CommandRunResult result = await runner.ExecuteSingleAsync(data, context, CancellationToken.None, CommandRunOptions.Default);

            Assert.That(result.Status, Is.EqualTo(CommandRunStatus.Canceled));
            Assert.That(first.ExecutionCount, Is.EqualTo(1));
            Assert.That(after.ExecutionCount, Is.EqualTo(0));
            Assert.That(canceled.ExecutionCount, Is.EqualTo(1));
        }

        [Test]
        public async UniTask CommandRunner_IfExecutesOnlySelectedBranch()
        {
            TestCommandExecutor thenExecutor = new(933);
            TestCommandExecutor elseExecutor = new(934);
            CommandRunner runner = CreateRunner(
                new[]
                {
                    CommandPayloadSchema.Empty(CommandIds.If, 9331, CommandPayloadUnknownFieldPolicy.Ignore),
                    CommandPayloadSchema.Empty(933, 9332, CommandPayloadUnknownFieldPolicy.Ignore),
                    CommandPayloadSchema.Empty(934, 9333, CommandPayloadUnknownFieldPolicy.Ignore),
                },
                new IfExecutor(),
                thenExecutor,
                elseExecutor);
            CommandContext context = new(runner.Scope, NullVarStore.Instance, runner);
            IfCommandData data = new()
            {
                Condition = DynamicValueExtensions.FromLiteral(true),
            };
            data.ThenCommands.Add(new TestCommandSource(new TestCommandData(933), "if-then"));
            data.ElseCommands.Add(new TestCommandSource(new TestCommandData(934), "if-else"));

            CommandRunResult result = await runner.ExecuteSingleAsync(data, context, CancellationToken.None, CommandRunOptions.Default);

            Assert.That(result.Status, Is.EqualTo(CommandRunStatus.Completed));
            Assert.That(thenExecutor.ExecutionCount, Is.EqualTo(1));
            Assert.That(elseExecutor.ExecutionCount, Is.EqualTo(0));
        }

        [Test]
        public async UniTask CommandRunner_SwitchSelectsMatchingCaseOnly()
        {
            TestCommandExecutor caseOne = new(935);
            TestCommandExecutor caseTwo = new(936);
            CommandRunner runner = CreateRunner(
                new[]
                {
                    CommandPayloadSchema.Empty(CommandIds.Switch, 9351, CommandPayloadUnknownFieldPolicy.Ignore),
                    CommandPayloadSchema.Empty(935, 9352, CommandPayloadUnknownFieldPolicy.Ignore),
                    CommandPayloadSchema.Empty(936, 9353, CommandPayloadUnknownFieldPolicy.Ignore),
                },
                new SwitchExecutor(),
                caseOne,
                caseTwo);
            CommandContext context = new(runner.Scope, NullVarStore.Instance, runner);
            SwitchCommandData data = new()
            {
                SwitchValue = DynamicValueExtensions.FromLiteral(2),
            };
            data.Cases.Add(new SwitchCase
            {
                CaseValue = DynamicValueExtensions.FromLiteral(1),
                Commands = new CommandListData(),
            });
            data.Cases[0].Commands.Add(new TestCommandSource(new TestCommandData(935), "switch-case-1"));
            data.Cases.Add(new SwitchCase
            {
                CaseValue = DynamicValueExtensions.FromLiteral(2),
                Commands = new CommandListData(),
            });
            data.Cases[1].Commands.Add(new TestCommandSource(new TestCommandData(936), "switch-case-2"));

            CommandRunResult result = await runner.ExecuteSingleAsync(data, context, CancellationToken.None, CommandRunOptions.Default);

            Assert.That(result.Status, Is.EqualTo(CommandRunStatus.Completed));
            Assert.That(caseOne.ExecutionCount, Is.EqualTo(0));
            Assert.That(caseTwo.ExecutionCount, Is.EqualTo(1));
        }

        [Test]
        public async UniTask CommandRunner_ForExecutesBodyByCount()
        {
            TestCommandExecutor bodyExecutor = new(937);
            CommandRunner runner = CreateRunner(
                new[]
                {
                    CommandPayloadSchema.Empty(CommandIds.For, 9371, CommandPayloadUnknownFieldPolicy.Ignore),
                    CommandPayloadSchema.Empty(937, 9372, CommandPayloadUnknownFieldPolicy.Ignore),
                },
                new ForExecutor(),
                bodyExecutor);
            CommandContext context = new(runner.Scope, NullVarStore.Instance, runner);
            ForCommandData data = new()
            {
                Mode = ForLoopMode.Count,
                Count = DynamicValueExtensions.FromLiteral(3),
                MaxIterations = 10,
                WaitForCompletion = true,
            };
            data.BodyCommands.Add(new TestCommandSource(new TestCommandData(937), "for-body"));

            CommandRunResult result = await runner.ExecuteSingleAsync(data, context, CancellationToken.None, CommandRunOptions.Default);

            Assert.That(result.Status, Is.EqualTo(CommandRunStatus.Completed));
            Assert.That(bodyExecutor.ExecutionCount, Is.EqualTo(3));
        }

        [Test]
        public async UniTask CommandRunner_ForRejectsMissingLoopBound()
        {
            TestCommandExecutor bodyExecutor = new(938);
            CommandRunner runner = CreateRunner(
                new[]
                {
                    CommandPayloadSchema.Empty(CommandIds.For, 9381, CommandPayloadUnknownFieldPolicy.Ignore),
                    CommandPayloadSchema.Empty(938, 9382, CommandPayloadUnknownFieldPolicy.Ignore),
                },
                new ForExecutor(),
                bodyExecutor);
            CommandContext context = new(runner.Scope, NullVarStore.Instance, runner);
            ForCommandData data = new()
            {
                Mode = ForLoopMode.Count,
                Count = DynamicValueExtensions.FromLiteral(1),
                MaxIterations = 0,
                WaitForCompletion = true,
            };
            data.BodyCommands.Add(new TestCommandSource(new TestCommandData(938), "for-body-missing-bound"));

            CommandRunResult result = await runner.ExecuteSingleAsync(data, context, CancellationToken.None, CommandRunOptions.Default);

            Assert.That(result.Status, Is.EqualTo(CommandRunStatus.Error));
            Assert.That(result.FailureKind, Is.EqualTo(CommandRunFailureKind.LoopBoundMissing));
            Assert.That(bodyExecutor.ExecutionCount, Is.EqualTo(0));
        }

        [Test]
        public async UniTask FunctionExecutor_PropagatesChangedVarsBackToCaller()
        {
            const int bodyCommandId = 940;
            const int callerVarId = 401;

            VarMutatingExecutor bodyExecutor = new(bodyCommandId, callerVarId, 22);
            CommandRunner runner = CreateRunner(
                new[]
                {
                    CommandPayloadSchema.Empty(CommandIds.Function, 9401, CommandPayloadUnknownFieldPolicy.Ignore),
                    CommandPayloadSchema.Empty(bodyCommandId, 9402, CommandPayloadUnknownFieldPolicy.Ignore),
                },
                new FunctionExecutor(),
                bodyExecutor);

            VarStore callerVars = new();
            callerVars.TrySetVariant(callerVarId, DynamicVariant.FromInt(11));

            CommandFunctionPreset function = new();
            function.Commands.Add(new TestCommandSource(new TestCommandData(bodyCommandId), "function-body"));

            FunctionCommandData data = new(function, initialVars: null, debugName: "function-test");
            CommandContext context = new(runner.Scope, callerVars, runner);

            CommandRunResult result = await runner.ExecuteSingleAsync(data, context, CancellationToken.None, CommandRunOptions.Default);

            Assert.That(result.Status, Is.EqualTo(CommandRunStatus.Completed));
            Assert.That(callerVars.TryGetVariant(callerVarId, out DynamicVariant updated), Is.True);
            Assert.That(updated.AsInt, Is.EqualTo(22));
        }

        [Test]
        public async UniTask WithActorExecutor_ExecutesBodyOnResolvedActorScope()
        {
            const int bodyCommandId = 941;

            IScopeNode? observedScope = null;
            IScopeNode? observedActor = null;

            TestCommandExecutor actorBodyExecutor = new(bodyCommandId, (data, ctx, ct) =>
            {
                _ = data;
                _ = ct;
                observedScope = ctx.Scope;
                observedActor = ctx.Actor;
                return UniTask.CompletedTask;
            });

            TestRuntimeResolver actorResolver = new();
            TestScopeNode actorScope = new(actorResolver, parent: null, kind: LifetimeScopeKind.Entity);
            CommandRunner actorRunner = CreateRunner(
                actorScope,
                new[] { CommandPayloadSchema.Empty(bodyCommandId, 9411, CommandPayloadUnknownFieldPolicy.Ignore) },
                actorBodyExecutor);
            actorResolver.Register<ICommandRunner>(actorRunner);

            TestScopeNode originScope = new();
            CommandRunner outerRunner = CreateRunner(
                originScope,
                new[] { CommandPayloadSchema.Empty(CommandIds.WithActor, 9412, CommandPayloadUnknownFieldPolicy.Ignore) },
                new WithActorExecutor());

            WithActorCommandData data = new()
            {
                ActorSource = new ActorSource { Kind = ActorSourceKind.Current },
                ExecutionScope = WithActorExecutionScope.ActorOnly,
                AwaitMode = FlowRunAwaitMode.WaitForCompletion,
            };
            data.Body.Add(new TestCommandSource(new TestCommandData(bodyCommandId), "with-actor-body"));

            CommandContext context = new(originScope, NullVarStore.Instance, outerRunner, actorScope, CommandRunOptions.Default);

            CommandRunResult result = await outerRunner.ExecuteSingleAsync(data, context, CancellationToken.None, CommandRunOptions.Default);

            Assert.That(result.Status, Is.EqualTo(CommandRunStatus.Completed));
            Assert.That(actorBodyExecutor.ExecutionCount, Is.EqualTo(1));
            Assert.That(observedScope, Is.SameAs(actorScope));
            Assert.That(observedActor, Is.SameAs(actorScope));
        }

        [Test]
        public async UniTask CommandChannelExecutor_ExecutesRegisteredTagCommands()
        {
            const int channelBodyCommandId = 942;

            TestCommandExecutor channelBodyExecutor = new(channelBodyCommandId);
            TestRuntimeResolver resolver = new();
            TestScopeNode scope = new(resolver);
            CommandRunner runner = CreateRunner(
                scope,
                new[]
                {
                    CommandPayloadSchema.Empty(CommandIds.CommandChannelExecute, 9421, CommandPayloadUnknownFieldPolicy.Ignore),
                    CommandPayloadSchema.Empty(channelBodyCommandId, 9422, CommandPayloadUnknownFieldPolicy.Ignore),
                },
                new CommandChannelExecutor(),
                channelBodyExecutor);

            CommandChannelHubService hub = new(new EmptyCommandChannelSettings());
            CommandListData channelCommands = new();
            channelCommands.Add(new TestCommandSource(new TestCommandData(channelBodyCommandId), "channel-body"));
            Assert.That(hub.RegisterOrUpdate("test-tag", channelCommands), Is.True);
            resolver.Register<ICommandChannelHubService>(hub);

            CommandChannelCommandData data = new()
            {
                ActorSource = new ActorSource { Kind = ActorSourceKind.Current },
                Tag = "test-tag",
                AwaitMode = FlowRunAwaitMode.WaitForCompletion,
                ExecutionScope = WithActorExecutionScope.ActorOnly,
            };

            CommandContext context = new(scope, NullVarStore.Instance, runner);
            CommandRunResult result = await runner.ExecuteSingleAsync(data, context, CancellationToken.None, CommandRunOptions.Default);

            Assert.That(result.Status, Is.EqualTo(CommandRunStatus.Completed));
            Assert.That(channelBodyExecutor.ExecutionCount, Is.EqualTo(1));
        }

        [Test]
        public async UniTask SceneChangeExecutor_UsesAncestorSceneService()
        {
            TestSceneService sceneService = new();
            TestRuntimeResolver rootResolver = new();
            rootResolver.Register<ISceneService>(sceneService);

            TestScopeNode rootScope = new(rootResolver, parent: null, kind: LifetimeScopeKind.Project);
            TestScopeNode childScope = new(new TestRuntimeResolver(), rootScope, LifetimeScopeKind.Scene);
            CommandRunner runner = CreateRunner(
                childScope,
                new[] { CommandPayloadSchema.Empty(CommandIds.SceneChange, 9431, CommandPayloadUnknownFieldPolicy.Ignore) },
                new SceneChangeExecutor());

            SceneChangeCommandData data = new()
            {
                Mode = SceneChangeMode.LoadSingle,
                TargetMode = SceneChangeTargetMode.GameScene,
                Scene = GameScene.HUD,
                ForceReload = true,
            };

            CommandContext context = new(childScope, NullVarStore.Instance, runner);
            CommandRunResult result = await runner.ExecuteSingleAsync(data, context, CancellationToken.None, CommandRunOptions.Default);

            Assert.That(result.Status, Is.EqualTo(CommandRunStatus.Completed));
            Assert.That(sceneService.LastLoadSingleScene, Is.EqualTo(GameScene.HUD));
            Assert.That(sceneService.LastForceReload, Is.True);
        }

        [Test]
        public async UniTask SelfDespawnExecutor_RunsBeforeDespawnCommands()
        {
            const int beforeCommandId = 944;

            TestCommandExecutor beforeExecutor = new(beforeCommandId);
            GameObject scopeObject = new("self-despawn-test-scope");
            try
            {
                TestComponentScope scope = scopeObject.AddComponent<TestComponentScope>();
                scope.Initialize(new TestRuntimeResolver(), parent: null, kind: LifetimeScopeKind.Entity);

                CommandRunner runner = CreateRunner(
                    scope,
                    new[]
                    {
                        CommandPayloadSchema.Empty(CommandIds.SelfDespawn, 9441, CommandPayloadUnknownFieldPolicy.Ignore),
                        CommandPayloadSchema.Empty(beforeCommandId, 9442, CommandPayloadUnknownFieldPolicy.Ignore),
                    },
                    new SelfDespawnExecutor(),
                    beforeExecutor);

                SelfDespawnCommandData data = new();
                data.BeforeDespawnCommands.Add(new TestCommandSource(new TestCommandData(beforeCommandId), "before-despawn"));

                CommandContext context = new(scope, NullVarStore.Instance, runner);
                CommandRunResult result = await runner.ExecuteSingleAsync(data, context, CancellationToken.None, CommandRunOptions.Default);

                Assert.That(result.Status, Is.EqualTo(CommandRunStatus.Completed));
                Assert.That(beforeExecutor.ExecutionCount, Is.EqualTo(1));
                await UniTask.Yield();
            }
            finally
            {
                if (scopeObject != null)
                    UnityEngine.Object.DestroyImmediate(scopeObject);
            }
        }

        static CommandRunner CreateRunner(int commandId, ICommandExecutor executor, CommandPayloadSchema schema)
        {
            _ = commandId;
            return CreateRunner(new[] { schema }, executor);
        }

        static CommandRunner CreateRunner(CommandPayloadSchema[] schemas, params ICommandExecutor[] executors)
        {
            return CreateRunner(new TestScopeNode(), schemas, executors);
        }

        static CommandRunner CreateRunner(IScopeNode scope, CommandPayloadSchema[] schemas, params ICommandExecutor[] executors)
        {
            return new CommandRunner(
                scope,
                new CommandExecutorCatalog(executors),
                new TestPayloadSchemaCatalog(schemas),
                NullCommandKeyResolver.Instance,
                NullCommandResolveLogger.Instance,
                SelfCommandPayloadFieldReaderProvider.Instance,
                MissingCommandPayloadReferenceValidator.Instance);
        }

        sealed class VarMutatingExecutor : ICommandExecutor
        {
            readonly int varId;
            readonly int value;

            public VarMutatingExecutor(int commandId, int varId, int value)
            {
                CommandId = commandId;
                this.varId = varId;
                this.value = value;
            }

            public int CommandId { get; }

            public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
            {
                _ = data;
                _ = ct;
                ctx.Vars.TrySetVariant(varId, DynamicVariant.FromInt(value));
                return UniTask.CompletedTask;
            }
        }

        sealed class TestCommandExecutor : ICommandExecutor
        {
            readonly Func<ICommandData, CommandContext, CancellationToken, UniTask>? execute;

            public TestCommandExecutor(int commandId, Func<ICommandData, CommandContext, CancellationToken, UniTask>? execute = null)
            {
                CommandId = commandId;
                this.execute = execute;
            }

            public int CommandId { get; }

            public int ExecutionCount { get; private set; }

            public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
            {
                _ = data;
                _ = ctx;
                _ = ct;
                ExecutionCount++;
                return execute != null ? execute(data, ctx, ct) : UniTask.CompletedTask;
            }
        }

        sealed class TestPayloadSchemaCatalog : ICommandCatalog
        {
            readonly Dictionary<int, CommandPayloadSchema> schemas = new();

            public TestPayloadSchemaCatalog(params CommandPayloadSchema[] schemas)
            {
                foreach (CommandPayloadSchema schema in schemas)
                    this.schemas[schema.CommandId] = schema;
            }

            public bool TryGetPayloadSchema(int commandId, out CommandPayloadSchema schema)
            {
                return schemas.TryGetValue(commandId, out schema);
            }

            public bool TryResolve(CommandKeyId keyId, out ICommandData data)
            {
                _ = keyId;
                data = null!;
                return false;
            }

            public bool TryResolve(CommandKeyRef key, out ICommandData data)
            {
                _ = key;
                data = null!;
                return false;
            }

            public bool TryGetMeta(CommandKeyRef key, out CommandCatalogMeta meta)
            {
                _ = key;
                meta = default;
                return false;
            }
        }

        sealed class TestCommandSource : ICommandSource
        {
            readonly ICommandData data;

            public TestCommandSource(ICommandData data, string debugName)
            {
                this.data = data;
                DebugName = debugName;
            }

            public string DebugName { get; }

            public bool TryResolve(CommandResolveContext ctx, out ICommandData commandData)
            {
                _ = ctx;
                commandData = data;
                return true;
            }
        }

        sealed class AccessorOnlyCommandData : ICommandData
        {
            public AccessorOnlyCommandData(int commandId, int amount)
            {
                CommandId = commandId;
                Amount = amount;
            }

            public int CommandId { get; }

            public int Amount { get; }

            public string DebugData => "AccessorOnlyCommandData";
        }

        sealed class TestCommandData : ICommandData, ICommandPayloadFieldReader
        {
            readonly Dictionary<string, CommandPayloadFieldValue> fields = new(StringComparer.Ordinal);

            public TestCommandData(int commandId)
            {
                CommandId = commandId;
            }

            public int CommandId { get; }

            public string DebugData => "TestCommandData";

            public void Set(string fieldPath, CommandPayloadFieldValue value)
            {
                fields[fieldPath] = value;
            }

            public bool TryReadPayloadField(string fieldPath, out CommandPayloadFieldValue value)
            {
                return fields.TryGetValue(fieldPath, out value);
            }

            public void CollectPayloadFieldPaths(ICollection<string> fieldPaths)
            {
                foreach (string fieldPath in fields.Keys)
                    fieldPaths.Add(fieldPath);
            }
        }

        sealed class TestScopeNode : IScopeNode
        {
            readonly IRuntimeResolver resolver;
            readonly IScopeNode? parent;
            readonly LifetimeScopeKind kind;

            public TestScopeNode(IRuntimeResolver? resolver = null, IScopeNode? parent = null, LifetimeScopeKind kind = LifetimeScopeKind.Project)
            {
                this.resolver = resolver ?? new TestRuntimeResolver();
                this.parent = parent;
                this.kind = kind;
            }

            public IScopeNode Parent => parent!;
            public IScopeIdentityService Identity => null!;
            public LifetimeScopeKind Kind => kind;
            public IRuntimeResolver Resolver => resolver;
            public bool IsVisible => true;
            public bool IsActive => true;

            public IReadOnlyList<IScopeNode> GetPathFromRoot()
            {
                if (parent == null)
                    return new[] { this };

                var parentPath = parent.GetPathFromRoot();
                List<IScopeNode> path = new(parentPath?.Count + 1 ?? 1);
                if (parentPath != null)
                {
                    for (int i = 0; i < parentPath.Count; i++)
                        path.Add(parentPath[i]);
                }

                path.Add(this);
                return path;
            }

            public UniTask SetActiveAsync(bool active, bool isReset = false, CancellationToken ct = default)
            {
                _ = active;
                _ = isReset;
                _ = ct;
                return UniTask.CompletedTask;
            }

            public bool TrySetActive(bool active, bool isReset = false)
            {
                _ = active;
                _ = isReset;
                return true;
            }

            public bool TrySetVisible(bool visible, bool isReset = false)
            {
                _ = visible;
                _ = isReset;
                return true;
            }
        }

        sealed class TestComponentScope : MonoBehaviour, IScopeNode
        {
            IRuntimeResolver resolver = null!;
            IScopeNode? parent;
            LifetimeScopeKind kind;

            public void Initialize(IRuntimeResolver resolver, IScopeNode? parent, LifetimeScopeKind kind)
            {
                this.resolver = resolver;
                this.parent = parent;
                this.kind = kind;
            }

            public IScopeNode Parent => parent!;

            public IScopeIdentityService Identity => null!;

            public LifetimeScopeKind Kind => kind;

            public IRuntimeResolver Resolver => resolver;

            public bool IsVisible => true;

            public bool IsActive => true;

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

            public IReadOnlyList<IScopeNode> GetPathFromRoot()
            {
                if (parent == null)
                    return new IScopeNode[] { this };

                var parentPath = parent.GetPathFromRoot();
                List<IScopeNode> path = new(parentPath?.Count + 1 ?? 1);
                if (parentPath != null)
                {
                    for (int i = 0; i < parentPath.Count; i++)
                        path.Add(parentPath[i]);
                }

                path.Add(this);
                return path;
            }
        }

        sealed class EmptyCommandChannelSettings : ICommandChannelHubSettings
        {
            public CommandChannelEntry[] Entries => Array.Empty<CommandChannelEntry>();
        }

        sealed class TestSceneService : ISceneService
        {
            public GameScene? LastLoadSingleScene { get; private set; }

            public bool LastForceReload { get; private set; }

            public UniTask LoadSingle(GameScene scene, bool forceReload = false)
            {
                LastLoadSingleScene = scene;
                LastForceReload = forceReload;
                return UniTask.CompletedTask;
            }

            public UniTask LoadAdditive(GameScene scene)
            {
                return UniTask.CompletedTask;
            }

            public UniTask Unload(GameScene scene)
            {
                return UniTask.CompletedTask;
            }

            public bool IsLoaded(GameScene scene)
            {
                _ = scene;
                return false;
            }

            public UniTask LoadSingle(string sceneName, bool forceReload = false)
            {
                _ = sceneName;
                LastForceReload = forceReload;
                return UniTask.CompletedTask;
            }

            public UniTask LoadAdditive(string sceneName)
            {
                _ = sceneName;
                return UniTask.CompletedTask;
            }

            public UniTask Unload(string sceneName)
            {
                _ = sceneName;
                return UniTask.CompletedTask;
            }

            public bool IsLoaded(string sceneName)
            {
                _ = sceneName;
                return false;
            }
        }

        sealed class TestRuntimeResolver : IRuntimeResolver
        {
            readonly Dictionary<Type, object> services = new();

            public void Register<T>(T instance)
            {
                services[typeof(T)] = instance!;
            }

            public void Dispose()
            {
            }

            public void Inject(object instance)
            {
                _ = instance;
            }

            public object Resolve(Type type)
            {
                if (services.TryGetValue(type, out object instance))
                    return instance;

                throw new InvalidOperationException($"Test resolver has no service for {type.FullName}.");
            }

            public T Resolve<T>()
            {
                if (TryResolve<T>(out T instance))
                    return instance;

                throw new InvalidOperationException($"Test resolver has no service for {typeof(T).FullName}.");
            }

            public object ResolveOrDefault(Type type)
            {
                return services.TryGetValue(type, out object instance)
                    ? instance
                    : null!;
            }

            public bool TryResolve(Type type, out object instance)
            {
                return services.TryGetValue(type, out instance!);
            }

            public bool TryResolve<T>(out T instance)
            {
                if (services.TryGetValue(typeof(T), out object resolved) && resolved is T typed)
                {
                    instance = typed;
                    return true;
                }

                instance = default!;
                return false;
            }
        }
    }
}
