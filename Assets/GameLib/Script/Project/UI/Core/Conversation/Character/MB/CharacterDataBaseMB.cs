#nullable enable

using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Conversation
{
    [DisallowMultipleComponent]
    public sealed class CharacterDataBaseMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("CharacterDataBase")]
        [LabelText("Scope Mode")]
        [Tooltip("CharacterDataBase の想定運用スコープです。既定は Scene。")]
        [SerializeField]
        CharacterDataBaseScopeMode _scopeMode = CharacterDataBaseScopeMode.Scene;

        [BoxGroup("CharacterDataBase")]
        [LabelText("Definitions")]
        [ListDrawerSettings(DefaultExpandedState = true, ShowFoldout = true, DraggableItems = true)]
        [SerializeField]
        List<CharacterDataBaseDefinition> _definitions = new() { new CharacterDataBaseDefinition() };

        ICharacterDataBaseService? _service;

        public CharacterDataBaseScopeMode ScopeMode => _scopeMode;
        public IReadOnlyList<CharacterDataBaseDefinition> Definitions => _definitions;
        public ICharacterDataBaseService? Service => _service;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<CharacterDataBaseService>(Lifetime.Singleton)
                .WithParameter(scope)
                .WithParameter(this)
                .As<ICharacterDataBaseService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            builder.RegisterBuildCallback(resolver =>
            {
                if (resolver.TryResolve<ICharacterDataBaseService>(out var service) && service != null)
                    _service = service;
            });
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            _definitions ??= new List<CharacterDataBaseDefinition>();
            if (_definitions.Count == 0)
                _definitions.Add(new CharacterDataBaseDefinition());
        }
#endif
    }
}
