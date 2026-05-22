#nullable enable
using System;
using Game.Input;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using System.Collections.Generic;
using VNext = Game.Commands.VNext;

namespace Game.Movement
{
    public sealed class UserInputAdapterMB : MonoBehaviour, IScopeInstaller
    {
        [FoldoutGroup("Adapter")]
        [SerializeField]
        InputConsumerPriority _consumerPriority = InputConsumerPriority.Gameplay;

        [FoldoutGroup("Adapter")]
        [SerializeField]
        [Tooltip("Higher values override lower adapters.")]
        int _directionPriority = InputDirectionAdapterPriority.User;

        [FoldoutGroup("Direction Settings")]
        [SerializeField, InlineProperty, HideLabel]
        InputDirectionAdapterSettings _directionSettings = InputDirectionAdapterSettings.Default;

        [FoldoutGroup("Input Sources")]
        [SerializeField]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = true)]
        List<InputDirectionSourceConfig> _sources = new();

        [FoldoutGroup("Commands")]
        [SerializeField, InlineProperty, HideLabel]
        VNext.CommandListData _onInputCommands = new();
        [FoldoutGroup("Debug")]
        [SerializeField]
        InputDirectionDebugView _debugView = new InputDirectionDebugView();


        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            var sources = BuildSources();
            builder.Register<UserInputAdapter>(RuntimeLifetime.Singleton)
                .As<IUserInputAdapter>()
                .As<IInputDirectionAdapter>()
                .As<IInputMovementAdapter>()
                .As<IInputDirectionSettingsAdapter>()
                .As<IInputDirectionTelemetry>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<IDisposable>()
                .WithParameter(_consumerPriority)
                .WithParameter(_directionPriority)
                .WithParameter(_directionSettings)
                .WithParameter(sources)
                .WithParameter(_onInputCommands ?? new VNext.CommandListData());

            builder.RegisterInstance(_debugView);
            builder.RegisterBuildCallback(container =>
            {
                if (_debugView != null && container.TryResolve<IInputDirectionTelemetry>(out var telemetry))
                {
                    _debugView.Bind(telemetry);
                }
            });
        }

        List<IInputDirectionSource> BuildSources()
        {
            var list = new List<IInputDirectionSource>();
            if (_sources == null || _sources.Count == 0)
            {
                list.Add(new MoveInputDirectionSource(new MoveInputSourceSettings(InputDirectionEmitMode.Continuous, true)));
                return list;
            }

            for (int i = 0; i < _sources.Count; i++)
            {
                var s = _sources[i];
                switch (s.Type)
                {
                    case InputDirectionSourceType.Move:
                        list.Add(new MoveInputDirectionSource(new MoveInputSourceSettings(s.EmitMode, s.ConsumeInput)));
                        break;
                    case InputDirectionSourceType.Swipe:
                        list.Add(new SwipeInputDirectionSource(new SwipeInputSourceSettings(
                            s.EmitMode,
                            s.ConsumeInput,
                            s.MinDistance,
                            s.Scale <= 0f ? 1f : s.Scale,
                            s.Normalize,
                            s.MaxMagnitude)));
                        break;
                }
            }

            if (list.Count == 0)
                list.Add(new MoveInputDirectionSource(new MoveInputSourceSettings(InputDirectionEmitMode.Continuous, true)));

            return list;
        }
    }
}

