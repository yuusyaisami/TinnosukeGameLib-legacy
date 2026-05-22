// Game.Common.FixedSORegistryMB.cs
//
// 固宁ESO レジストリめEDI コンチE��に登録する MB
// SceneLTS と同階層に配置し、FeatureInstaller 経由でインスタンス登録

using Game.Health;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Common
{
    /// <summary>
    /// 固宁ESO レジストリめEDI コンチE��に登録する MB、E
    /// SceneLTS と同階層に配置される想定、E
    /// </summary>
    public sealed class FixedSORegistryMB : MonoBehaviour, IScopeInstaller
    {
        [Header("Health")]
        [LabelText("Fixed Health Modifier Registry")]
        [Tooltip("Scene 固定�E HealthModifier レジストリ")]
        [SerializeField]
        FixedHealthModifierRegistrySO _healthModifierRegistry;

        // 封E��の拡張用に他�Eレジストリも追加可能
        // [Header("StatusEffect")]
        // [SerializeField]
        // FixedStatusEffectRegistrySO _statusEffectRegistry;

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            // FixedHealthModifierRegistrySO をインスタンス登録
            if (_healthModifierRegistry != null)
            {
                builder.RegisterInstance(_healthModifierRegistry)
                    .As<FixedHealthModifierRegistrySO>();
            }

            // 封E��の拡張用
            // if (_statusEffectRegistry != null)
            // {
            //     builder.RegisterInstance(_statusEffectRegistry)
            //         .As<FixedStatusEffectRegistrySO>();
            // }
        }
    }
}

