#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Channel;
using Game.Commands.VNext;
using Game.Common;
using Game.Fire;
using Game.Movement;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.DI
{
    [Serializable]
    public class SpawnPatternRuntimeTemplatePreset : BaseRuntimeTemplatePreset
    {
    }

    [Serializable]
    public sealed class ParticleRuntimeTemplatePreset : BaseRuntimeTemplatePreset
    {
        [Header("Movement Init")]
        [SerializeField]
        bool configureInputMovement = true;

        [SerializeField, ShowIf(nameof(configureInputMovement))]
        bool inputMovementEnabled = true;

        [SerializeField]
        bool configureHoming;

        [SerializeField, ShowIf(nameof(configureHoming))]
        bool homingEnabled;

        [SerializeField, ShowIf(nameof(configureHoming))]
        HomingBlendParams homingBlendParams = HomingBlendParams.Default;

        [Header("Animation Init")]
        [SerializeField]
        bool configureAnimation;

        [SerializeField, ShowIf(nameof(configureAnimation))]
        string animationChannelTag = "default";

        [SerializeField, ShowIf(nameof(configureAnimation))]
        AnimationSpritePreset animationPreset = new();

        [Header("Commands")]
        [SerializeField]
        bool runOnAcquireCommands;

        [SerializeField, ShowIf(nameof(runOnAcquireCommands))]
        CommandListData onAcquireCommands = new();

        [Header("Debug")]
        [SerializeField]
        bool debugLogOnAcquire;

        public override void OnAcquire(IScopeNode scope, RuntimeIdentityData identity)
        {
            if (scope == null)
                return;

            var resolver = scope.Resolver;
            if (resolver == null)
                return;

            resolver.TryResolve(out IInputMovementService? inputMove);
            if (inputMove != null)
            {
                if (configureInputMovement && inputMove is IEnabledService enabledSvc)
                    enabledSvc.SetEnabled(inputMovementEnabled);

                if (configureHoming && inputMove.Homing != null)
                {
                    inputMove.Homing.HomingLayer.Set("ParticleTemplate", homingEnabled);

                    if (inputMove.Homing is IHomingMovementConfigurable configurable)
                        configurable.SetBlendParams(homingBlendParams);
                }

                if (debugLogOnAcquire)
                {
                    Debug.Log(
                        $"[ParticleRuntimeTemplatePreset] OnAcquire scope={scope.Identity?.Id ?? "(no-id)"} kind={scope.Kind} " +
                        $"configureInputMovement={configureInputMovement} inputEnabled={inputMovementEnabled}");
                }
            }
            else if (debugLogOnAcquire)
            {
                Debug.LogWarning(
                    $"[ParticleRuntimeTemplatePreset] OnAcquire scope={scope.Identity?.Id ?? "(no-id)"} kind={scope.Kind} " +
                    "IInputMovementService resolve failed.");
            }

            resolver.TryResolve(out IAnimationSpriteHubService? animHub);
            var player = animHub != null && animHub.TryGetPlayer(animationChannelTag, out var p) ? p : null;
            if (configureAnimation && player != null)
            {
                UniTask.Void(async () =>
                {
                    try
                    {
                        await player.PlayPresetAsync(animationPreset, CancellationToken.None);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                });
            }

            if (runOnAcquireCommands)
                TryRunCommands(scope, resolver);
        }

        void TryRunCommands(IScopeNode scope, IObjectResolver resolver)
        {
            if (onAcquireCommands == null || onAcquireCommands.Count == 0)
                return;

            resolver.TryResolve(out ICommandRunner? runner);
            if (runner == null)
                return;

            var ctx = new CommandContext(scope, new VarStore(), runner);
            UniTask.Void(async () =>
            {
                try
                {
                    await runner.ExecuteListAsync(onAcquireCommands, ctx, CancellationToken.None, ctx.Options);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            });
        }
    }

    [Serializable]
    public sealed class FirePatternRuntimeTemplatePreset : BaseRuntimeTemplatePreset
    {
        [Header("Fire Pattern Override")]
        [SerializeReference, InlineProperty]
        [Tooltip("When set, overrides the FirePatternServiceMB input patterns before ISpawnContextConsumer is invoked.")]
        BaseFirePattern? overrideFirePattern;

        [Header("Animation Init")]
        [SerializeField]
        bool configureAnimation;

        [SerializeField, ShowIf(nameof(configureAnimation))]
        string animationChannelTag = "default";

        [SerializeField, ShowIf(nameof(configureAnimation))]
        AnimationSpritePreset animationPreset = new();

        [Header("Commands")]
        [SerializeField]
        bool runOnAcquireCommands;

        [SerializeField, ShowIf(nameof(runOnAcquireCommands))]
        CommandListData onAcquireCommands = new();

        public override void OnAcquire(IScopeNode scope, RuntimeIdentityData identity)
        {
            if (scope == null)
                return;

            var resolver = scope.Resolver;
            if (resolver == null)
                return;

            if (overrideFirePattern != null &&
                resolver.TryResolve<IFirePatternOverrideReceiver>(out var receiver) &&
                receiver != null)
            {
                receiver.SetOverridePattern(overrideFirePattern);
            }

            resolver.TryResolve(out IAnimationSpriteHubService? animHub);
            var player = animHub != null && animHub.TryGetPlayer(animationChannelTag, out var p) ? p : null;
            if (configureAnimation && player != null)
            {
                UniTask.Void(async () =>
                {
                    try
                    {
                        await player.PlayPresetAsync(animationPreset, CancellationToken.None);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                });
            }

            if (runOnAcquireCommands)
                TryRunCommands(scope, resolver);
        }

        void TryRunCommands(IScopeNode scope, IObjectResolver resolver)
        {
            if (onAcquireCommands == null || onAcquireCommands.Count == 0)
                return;

            resolver.TryResolve(out ICommandRunner? runner);
            if (runner == null)
                return;

            var ctx = new CommandContext(scope, new VarStore(), runner);
            UniTask.Void(async () =>
            {
                try
                {
                    await runner.ExecuteListAsync(onAcquireCommands, ctx, CancellationToken.None, ctx.Options);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            });
        }
    }

    [CreateAssetMenu(menuName = "Game/Runtime/Runtime Template Preset/Particle", fileName = "ParticleRuntimeTemplatePreset")]
    public sealed class ParticleRuntimeTemplatePresetAssetSO : ScriptableObject
    {
        [SerializeReference]
        public ParticleRuntimeTemplatePreset? preset = new();

        public ParticleRuntimeTemplatePreset? Preset => preset;

        void OnEnable()
        {
            EnsurePreset();
        }

        void OnValidate()
        {
            EnsurePreset();
        }

        void EnsurePreset()
        {
            var changed = false;
            if (preset == null)
            {
                preset = new ParticleRuntimeTemplatePreset();
                changed = true;
            }

            changed |= preset.EnsureTemplateId(name);

#if UNITY_EDITOR
            if (changed)
                UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }

    [CreateAssetMenu(menuName = "Game/Runtime/Runtime Template Preset/FirePattern", fileName = "FirePatternRuntimeTemplatePreset")]
    public sealed class FirePatternRuntimeTemplatePresetAssetSO : ScriptableObject
    {
        [SerializeReference]
        public FirePatternRuntimeTemplatePreset? preset = new();

        public FirePatternRuntimeTemplatePreset? Preset => preset;

        void OnEnable()
        {
            EnsurePreset();
        }

        void OnValidate()
        {
            EnsurePreset();
        }

        void EnsurePreset()
        {
            var changed = false;
            if (preset == null)
            {
                preset = new FirePatternRuntimeTemplatePreset();
                changed = true;
            }

            changed |= preset.EnsureTemplateId(name);

#if UNITY_EDITOR
            if (changed)
                UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }

    [CreateAssetMenu(menuName = "Game/Runtime/Runtime Template Preset/SpawnPattern", fileName = "SpawnPatternRuntimeTemplatePreset")]
    public sealed class SpawnPatternRuntimeTemplatePresetAssetSO : ScriptableObject
    {
        [SerializeReference]
        public SpawnPatternRuntimeTemplatePreset? preset = new();

        public SpawnPatternRuntimeTemplatePreset? Preset => preset;

        void OnEnable()
        {
            EnsurePreset();
        }

        void OnValidate()
        {
            EnsurePreset();
        }

        void EnsurePreset()
        {
            var changed = false;
            if (preset == null)
            {
                preset = new SpawnPatternRuntimeTemplatePreset();
                changed = true;
            }

            changed |= preset.EnsureTemplateId(name);

#if UNITY_EDITOR
            if (changed)
                UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}
