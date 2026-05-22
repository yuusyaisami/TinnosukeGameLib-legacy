#nullable enable

using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.UI
{
    [DisallowMultipleComponent]
    public sealed class UIDialogObjectMB : MonoBehaviour, Game.IScopeInstaller
    {
        [BoxGroup("Options")]
        [InlineProperty, HideLabel]
        [SerializeField]
        UIDialogObjectOptions options = new();

        public void InstallScopeServices(IRuntimeContainerBuilder builder, Game.IScopeNode scope)
        {
            options ??= new UIDialogObjectOptions();
            builder.RegisterInstance(options);

            builder.Register<DialogRuntimeObjectService>(RuntimeLifetime.Singleton)
                .As<IUIDialogRuntimeService>()
                .WithParameter(scope);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            options ??= new UIDialogObjectOptions();
        }
#endif
    }
}


