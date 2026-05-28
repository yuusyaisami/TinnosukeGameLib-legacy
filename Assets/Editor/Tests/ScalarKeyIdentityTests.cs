#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Common;
using Game.Kernel.IR;
using Game.Profile;
using Game.Save;
using Game.Times;
using NUnit.Framework;
using Game.Scalar;
using UnityEngine;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class ScalarKeyIdentityTests
    {
        [Test]
        public void Constructor_ResolvesVerifiedIdentityForKnownKey()
        {
            var key = new ScalarKey("GameLib.Movement.DefaultSpeed");

            Assert.That(key.Id, Is.GreaterThan(0));
            Assert.That(key.KeyId.Value, Is.EqualTo(key.Id));
            Assert.That(key.Name, Is.EqualTo("GameLib.Movement.DefaultSpeed"));
            Assert.That(key.IsVerified, Is.True);
        }

        [Test]
        public void Constructor_RejectsUnknownKey()
        {
            var key = new ScalarKey("Unknown.Scalar.Key");

            Assert.That(key.Id, Is.EqualTo(0));
            Assert.That(key.IsVerified, Is.False);
        }

        [Test]
        public void OnAfterDeserialize_UsesVerifiedIdentity()
        {
            var first = new ScalarKey("GameLib.Health.Current");
            var second = new ScalarKey("GameLib.Health.Current");

            Assert.That(first.Id, Is.EqualTo(second.Id));
            Assert.That(first.IsVerified, Is.True);
        }

        [Test]
        public void ScalarOwnerIdentity_RequiresExplicitOwnerId()
        {
            var owner = new ScalarOwnerIdentity(
                ScalarOwnerKind.Scene,
                new ScalarOwnerId("scene:gameplay"));

            Assert.That(owner.Kind, Is.EqualTo(ScalarOwnerKind.Scene));
            Assert.That(owner.OwnerId.Value, Is.EqualTo("scene:gameplay"));
            Assert.That(owner.IsValid, Is.True);
        }

        [Test]
        public void ScalarBindingEndpoint_UsesOwnerIdentityAndVerifiedKeyId()
        {
            var key = new ScalarKey("GameLib.Movement.DefaultSpeed");
            var owner = new ScalarOwnerIdentity(
                ScalarOwnerKind.Global,
                new ScalarOwnerId("global:app"));
            var endpoint = new ScalarBindingEndpoint(owner, key.KeyId);

            Assert.That(endpoint.Owner, Is.EqualTo(owner));
            Assert.That(endpoint.KeyId, Is.EqualTo(key.KeyId));
            Assert.That(endpoint.IsValid, Is.True);
            Assert.That(endpoint.ToString(), Does.Contain("global:app"));
        }

        [Test]
        public void ScalarBindingEdge_ConnectsExplicitEndpoints()
        {
            var sourceKey = new ScalarKey("GameLib.Movement.DefaultSpeed");
            var targetKey = new ScalarKey("GameLib.Movement.SpeedMultiplier");
            var source = new ScalarBindingEndpoint(
                new ScalarOwnerIdentity(ScalarOwnerKind.Global, new ScalarOwnerId("global:app")),
                sourceKey.KeyId);
            var target = new ScalarBindingEndpoint(
                new ScalarOwnerIdentity(ScalarOwnerKind.Scene, new ScalarOwnerId("scene:battle")),
                targetKey.KeyId);
            var edge = new ScalarBindingEdge(source, target);

            Assert.That(edge.Source, Is.EqualTo(source));
            Assert.That(edge.Target, Is.EqualTo(target));
            Assert.That(edge.IsValid, Is.True);
        }

        [Test]
        public void ScalarBindingEndpoint_RejectsUnverifiedKeyId()
        {
            var owner = new ScalarOwnerIdentity(
                ScalarOwnerKind.Scene,
                new ScalarOwnerId("scene:gameplay"));

            Assert.Throws<ArgumentOutOfRangeException>(() => new ScalarBindingEndpoint(owner, new ScalarKeyId(0)));
        }

        [Test]
        public void ScalarDatabaseEntry_ProjectsToVerifiedDeclaration()
        {
            var key = new ScalarKey("GameLib.Movement.DefaultSpeed");
            var owner = CreateOwner();
            var source = CreateGeneratedSource();
            var entry = new ScalarDatabaseEntry
            {
                Key = key,
                BaseValue = 12.5f,
                UseEffectMod = true,
                UseRoundMod = true,
                RoundDigits = 2,
                UseClampMod = true,
                Clamp = new ScalarClamp
                {
                    UseMin = true,
                    Min = DynamicValueExtensions.FromLiteral(1f),
                    UseMax = true,
                    Max = DynamicValueExtensions.FromLiteral(99f),
                },
                SaveEnabled = true,
                SaveLayer = SaveLayer.Profile,
            };

            var ok = ScalarDeclarationProjection.TryCreateDatabaseDeclaration(entry, owner, source, out var declaration, out var failureReason);

            Assert.That(ok, Is.True, failureReason);
            Assert.That(declaration.Endpoint.Owner, Is.EqualTo(owner));
            Assert.That(declaration.Endpoint.KeyId, Is.EqualTo(key.KeyId));
            Assert.That(declaration.ApplyPolicy, Is.EqualTo(ScalarDeclarationApplyPolicy.ReplaceRuntime));
            Assert.That(declaration.KeyName, Is.EqualTo(key.Name));
            Assert.That(declaration.DeclarationSourceKind, Is.EqualTo(ScalarDeclarationSourceKind.Database));
            Assert.That(declaration.RuntimeConfig.BaseValue, Is.EqualTo(12.5f));
            Assert.That(declaration.RuntimeConfig.UseEffectMod, Is.True);
            Assert.That(declaration.RuntimeConfig.UseRoundMod, Is.True);
            Assert.That(declaration.RuntimeConfig.RoundDigits, Is.EqualTo(2));
            Assert.That(declaration.RuntimeConfig.UseClampMod, Is.True);
            Assert.That(declaration.SaveEnabled, Is.True);
            Assert.That(declaration.SaveLayer, Is.EqualTo(SaveLayer.Profile));
        }

        [Test]
        public void ProfileFloatValue_ProjectsToVerifiedDeclaration()
        {
            var key = new ScalarKey("GameLib.Movement.DefaultSpeed");
            var owner = CreateOwner();
            var source = CreateGeneratedSource();
            IScalarDeclarationAuthoring binding = new ProfileFloatValue
            {
                Value = 8f,
                ScalarKeyValue = key,
                ScalarPolicyValue = ScalarBindPolicy.UpdateBaseline,
                UseEffectMod = true,
                UseRoundMod = true,
                RoundDigits = 1,
                UseClampMod = true,
                Clamp = new ScalarClamp
                {
                    UseMin = true,
                    Min = DynamicValueExtensions.FromLiteral(0f),
                },
                UseLocalBase = true,
                LocalBaseValue = 3f,
                ScalarSaveEnabledValue = true,
                ScalarSaveLayerValue = SaveLayer.GameLogic,
            };

            var ok = binding.TryCreateScalarDeclaration(owner, "MovementProfileSO", source, out var declaration, out var failureReason);

            Assert.That(ok, Is.True, failureReason);
            Assert.That(declaration.Endpoint.Owner, Is.EqualTo(owner));
            Assert.That(declaration.Endpoint.KeyId, Is.EqualTo(key.KeyId));
            Assert.That(declaration.ApplyPolicy, Is.EqualTo(ScalarDeclarationApplyPolicy.UpdateBaseline));
            Assert.That(declaration.KeyName, Is.EqualTo(key.Name));
            Assert.That(declaration.DeclarationSourceKind, Is.EqualTo(ScalarDeclarationSourceKind.ProfileBinding));
            Assert.That(declaration.RuntimeConfig.BaseValue, Is.EqualTo(8f));
            Assert.That(declaration.RuntimeConfig.UseEffectMod, Is.True);
            Assert.That(declaration.RuntimeConfig.UseRoundMod, Is.True);
            Assert.That(declaration.RuntimeConfig.RoundDigits, Is.EqualTo(1));
            Assert.That(declaration.HasLocalBase, Is.True);
            Assert.That(declaration.LocalBaseValue, Is.EqualTo(3f));
            Assert.That(declaration.SaveEnabled, Is.True);
            Assert.That(declaration.SaveLayer, Is.EqualTo(SaveLayer.GameLogic));
        }

        [Test]
        public void DatabaseAndProfileProjection_ShareDeclarationVocabulary()
        {
            var key = new ScalarKey("GameLib.Movement.DefaultSpeed");
            var owner = CreateOwner();
            var source = CreateGeneratedSource();
            var databaseEntry = new ScalarDatabaseEntry
            {
                Key = key,
                BaseValue = 5f,
                UseEffectMod = true,
                UseRoundMod = false,
                RoundDigits = 0,
                UseClampMod = false,
                SaveEnabled = true,
                SaveLayer = SaveLayer.Session,
            };
            IScalarDeclarationAuthoring profileBinding = new ProfileFloatValue
            {
                Value = 5f,
                ScalarKeyValue = key,
                ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
                UseEffectMod = true,
                UseRoundMod = false,
                RoundDigits = 0,
                UseClampMod = false,
                ScalarSaveEnabledValue = true,
                ScalarSaveLayerValue = SaveLayer.Session,
            };

            var databaseOk = ScalarDeclarationProjection.TryCreateDatabaseDeclaration(databaseEntry, owner, source, out var databaseDeclaration, out var databaseFailureReason);
            var profileOk = profileBinding.TryCreateScalarDeclaration(owner, "MovementProfileSO", source, out var profileDeclaration, out var profileFailureReason);

            Assert.That(databaseOk, Is.True, databaseFailureReason);
            Assert.That(profileOk, Is.True, profileFailureReason);
            Assert.That(profileDeclaration.Endpoint, Is.EqualTo(databaseDeclaration.Endpoint));
            Assert.That(profileDeclaration.RuntimeConfig.BaseValue, Is.EqualTo(databaseDeclaration.RuntimeConfig.BaseValue));
            Assert.That(profileDeclaration.RuntimeConfig.UseEffectMod, Is.EqualTo(databaseDeclaration.RuntimeConfig.UseEffectMod));
            Assert.That(profileDeclaration.RuntimeConfig.UseRoundMod, Is.EqualTo(databaseDeclaration.RuntimeConfig.UseRoundMod));
            Assert.That(profileDeclaration.RuntimeConfig.UseClampMod, Is.EqualTo(databaseDeclaration.RuntimeConfig.UseClampMod));
            Assert.That(profileDeclaration.SaveEnabled, Is.EqualTo(databaseDeclaration.SaveEnabled));
            Assert.That(profileDeclaration.SaveLayer, Is.EqualTo(databaseDeclaration.SaveLayer));
        }

        [Test]
        public void ProfileFloatValue_RejectsDynamicClampDeclaration()
        {
            var key = new ScalarKey("GameLib.Movement.DefaultSpeed");
            var owner = CreateOwner();
            var source = CreateGeneratedSource();
            IScalarDeclarationAuthoring binding = new ProfileFloatValue
            {
                Value = 2f,
                ScalarKeyValue = key,
                ScalarPolicyValue = ScalarBindPolicy.UpdateBaseline,
                UseClampMod = true,
                Clamp = new ScalarClamp
                {
                    UseMin = true,
                    Min = DynamicValue<float>.FromSource(new RandomFloatRangeSource()),
                },
            };

            var ok = binding.TryCreateScalarDeclaration(owner, "MovementProfileSO", source, out _, out var failureReason);

            Assert.That(ok, Is.False);
            Assert.That(failureReason, Does.Contain("literal clamp bounds"));
        }

        [Test]
        public void ProfileProjection_RejectsDuplicateEndpoints()
        {
            var key = new ScalarKey("GameLib.Movement.DefaultSpeed");
            var owner = CreateOwner();
            var source = CreateGeneratedSource();
            IProfileValueBinding[] bindings =
            {
                new ProfileFloatValue
                {
                    Value = 1f,
                    ScalarKeyValue = key,
                    ScalarPolicyValue = ScalarBindPolicy.UpdateBaseline,
                },
                new ProfileFloatValue
                {
                    Value = 2f,
                    ScalarKeyValue = key,
                    ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
                },
            };

            var ok = ProfileScalarDeclarationProjection.TryCreateScalarDeclarations(bindings, owner, "MovementProfileSO", source, out _, out var failureReason);

            Assert.That(ok, Is.False);
            Assert.That(failureReason, Does.Contain("Duplicate scalar declaration endpoint"));
        }

        [Test]
        public void ProfileProjection_RejectsUnresolvedScalarKey()
        {
            var owner = CreateOwner();
            var source = CreateGeneratedSource();
            IProfileValueBinding[] bindings =
            {
                new ProfileFloatValue
                {
                    Value = 1f,
                    ScalarKeyValue = new ScalarKey("Unknown.Scalar.Key"),
                    ScalarPolicyValue = ScalarBindPolicy.UpdateBaseline,
                },
            };

            var ok = ProfileScalarDeclarationProjection.TryCreateScalarDeclarations(bindings, owner, "MovementProfileSO", source, out _, out var failureReason);

            Assert.That(ok, Is.False);
            Assert.That(failureReason, Does.Contain("verified scalar key"));
        }

        [Test]
        public void ScopeBindingRegistryService_AppliesScalarBindingsViaDeclarations()
        {
            var key = new ScalarKey("GameLib.Movement.DefaultSpeed");
            var identity = new TestIdentityService("scene:gameplay", "scene", LifetimeScopeKind.Scene);
            var scope = new TestScopeNode(identity);
            var scalar = new BaseScalarService(scope, null);
            var registry = new ScopeBindingRegistryService(null, scalar, identity.Id, scope);
            var profile = new TestScalarProfile
            {
                DefaultSpeed = new ProfileFloatValue
                {
                    Value = 7.5f,
                    ScalarKeyValue = key,
                    ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
                },
            };

            registry.SetProfileDefinition(profile);

            Assert.That(scalar.TryGetRuntime(key, out _), Is.True);
            Assert.That(scalar.LocalGet(key), Is.EqualTo(7.5f));
        }

        [Test]
        public void ScopeBindingRegistryService_FailsClosedWhenAnyScalarKeyIsUnresolved()
        {
            var validKey = new ScalarKey("GameLib.Movement.DefaultSpeed");
            var identity = new TestIdentityService("scene:gameplay", "scene", LifetimeScopeKind.Scene);
            var scope = new TestScopeNode(identity);
            var scalar = new BaseScalarService(scope, null);
            var registry = new ScopeBindingRegistryService(null, scalar, identity.Id, scope);
            var profile = new TestScalarProfile
            {
                DefaultSpeed = new ProfileFloatValue
                {
                    Value = 7.5f,
                    ScalarKeyValue = validKey,
                    ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
                },
                InvalidBinding = new ProfileFloatValue
                {
                    Value = 2f,
                    ScalarKeyValue = new ScalarKey("Unknown.Scalar.Key"),
                    ScalarPolicyValue = ScalarBindPolicy.UpdateBaseline,
                },
            };

            registry.SetProfileDefinition(profile);

            Assert.That(scalar.TryGetRuntime(validKey, out _), Is.False);
        }

        [Test]
        public void ScalarRuntimeService_ReportsUnavailableUntilStarted()
        {
            var key = new ScalarKey("GameLib.Movement.DefaultSpeed");
            var identity = new TestIdentityService("scene:gameplay", "scene", LifetimeScopeKind.Scene);
            var scope = new TestScopeNode(identity);
            var scalar = new ScalarRuntimeService(scope, null);

            var beforeStart = scalar.TryReadLocal(key, out var beforeValue, out var beforeFailureReason);

            Assert.That(scalar.IsStarted, Is.False);
            Assert.That(beforeStart, Is.False);
            Assert.That(beforeValue, Is.EqualTo(0f));
            Assert.That(beforeFailureReason, Is.EqualTo("ScalarRuntimeService is not started."));

            scalar.OnAcquire(scope, false);
            scalar.EnsureRuntime(key, new ScalarRuntimeConfig { BaseValue = 7.5f });

            var afterStart = scalar.TryReadLocal(key, out var afterValue, out var afterFailureReason);

            Assert.That(scalar.IsStarted, Is.True);
            Assert.That(afterStart, Is.True);
            Assert.That(afterValue, Is.EqualTo(7.5f));
            Assert.That(afterFailureReason, Is.Empty);

            scalar.OnRelease(scope, false);

            Assert.That(scalar.IsStarted, Is.False);
        }

        [Test]
        public void ScalarDeclarationRuntimeBridge_UsesShellInstallPath()
        {
            var key = new ScalarKey("GameLib.Movement.DefaultSpeed");
            var identity = new TestIdentityService("scene:gameplay", "scene", LifetimeScopeKind.Scene);
            var scope = new TestScopeNode(identity);
            var scalar = new ScalarRuntimeService(scope, null);
            var entry = new ScalarDatabaseEntry
            {
                Key = key,
                BaseValue = 7.5f,
                UseEffectMod = false,
                UseRoundMod = false,
                RoundDigits = 0,
                UseClampMod = false,
                SaveEnabled = false,
                SaveLayer = SaveLayer.Profile,
            };

            var projectionOk = ScalarDeclarationProjection.TryCreateDatabaseDeclaration(entry, CreateOwner(), CreateGeneratedSource(), out var declaration, out var projectionFailureReason);
            Assert.That(projectionOk, Is.True, projectionFailureReason);

            var applyOk = ScalarDeclarationRuntimeBridge.TryApplyDeclarations((IScalarRuntimeShell)scalar, new[] { declaration }, out var applyFailureReason);

            Assert.That(applyOk, Is.True, applyFailureReason);
            Assert.That(scalar.TryGetRuntime(key, out _), Is.True);
            Assert.That(scalar.LocalGet(key), Is.EqualTo(7.5f));
        }

        static ScalarOwnerIdentity CreateOwner()
        {
            return new ScalarOwnerIdentity(
                ScalarOwnerKind.Scene,
                new ScalarOwnerId("scene:gameplay"));
        }

        static SourceLocationIR CreateGeneratedSource()
        {
            return new SourceLocationIR(new GeneratedSourceLocation("ScalarKeyIdentityTests", "M9.2", "editor"));
        }

        [Serializable]
        sealed class TestScalarProfile : BaseProfileData
        {
            public ProfileFloatValue DefaultSpeed = new ProfileFloatValue();
            public ProfileFloatValue InvalidBinding = new ProfileFloatValue();

            public override Type ProfileType => typeof(TestScalarProfile);
        }

        sealed class TestIdentityService : ILTSIdentityService
        {
            public TestIdentityService(string id, string category, LifetimeScopeKind kind)
            {
                Id = id;
                Category = category;
                Kind = kind;
                IsActive = true;
            }

            public LifetimeScopeKind Kind { get; }
            public string Id { get; }
            public string Category { get; }
            public bool IsActive { get; set; }
            public Transform SelfTransform => null!;
            public float Radius => 0f;
            public TimeScaleBehavior TimeScaleBehavior => TimeScaleBehavior.Scaled;
        }

        sealed class TestScopeNode : IScopeNode
        {
            public TestScopeNode(ILTSIdentityService identity)
            {
                Identity = identity;
            }

            public IScopeNode? Parent => null;
            public ILTSIdentityService? Identity { get; }
            public LifetimeScopeKind Kind => Identity?.Kind ?? LifetimeScopeKind.None;
            public IRuntimeResolver? Resolver => null;
            public bool IsVisible => true;
            public bool IsActive => true;

            public bool TrySetVisible(bool visible, bool isReset = false)
            {
                _ = visible;
                _ = isReset;
                return false;
            }

            public bool TrySetActive(bool active, bool isReset = false)
            {
                _ = active;
                _ = isReset;
                return false;
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
                return Array.Empty<IScopeNode>();
            }
        }
    }
}
