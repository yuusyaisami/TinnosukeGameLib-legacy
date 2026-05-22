#nullable enable
using UnityEngine;
using VContainer;
using Sirenix.OdinInspector;

namespace Game.UI
{
    public interface IUIInputNavigateObjectOptions
    {
        bool Enabled { get; }
        UIInputTrigger Trigger { get; }
        bool ResendInputOnSelect { get; }
    }

    /// <summary>
    /// 特定�E力で「�E刁E��選択させる」設定を持つコンポ�Eネント、E
    /// 実際の選択�E琁E�E UIInputNavigateManagerService が行う、E
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIInputNavigateObjectMB : MonoBehaviour, IScopeInstaller, IUIInputNavigateObjectOptions
    {
        [Header("Input")]
        [SerializeField] bool _enabled = true;

        [SerializeField]
        UIInputAction _action = UIInputAction.Cancel;

        [SerializeField]
        UIInputPhase _phase = UIInputPhase.Down;

        [Header("Behavior")]
        [SerializeField, ToggleLeft, LabelText("Resend Input After Select")]
        bool _resendInputOnSelect;

        [Header("Debug")]
        [SerializeField, ReadOnly]
        string _resolvedOwnerName = "";

        public bool Enabled => _enabled;
        public UIInputTrigger Trigger => new(_action, _phase);
        public bool ResendInputOnSelect => _resendInputOnSelect;

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<UIInputNavigateObjectService>(RuntimeLifetime.Singleton)
                .WithParameter(scope)
                .WithParameter<IUIInputNavigateObjectOptions>(this)
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            builder.RegisterBuildCallback(container =>
            {
                if (container.TryResolve<UIInputNavigateObjectService>(out var svc))
                {
                    _resolvedOwnerName = svc.Owner.Identity?.SelfTransform != null ? svc.Owner.Identity.SelfTransform.name : "";
                }
            });
        }

#if UNITY_EDITOR
        // (reserved)
#endif
    }
}

