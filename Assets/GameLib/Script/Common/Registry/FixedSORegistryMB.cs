// Game.Common.FixedSORegistryMB.cs
//
// 固定 SO レジストリを DI コンテナに登録する MB
// SceneLTS と同階層に配置し、FeatureInstaller 経由でインスタンス登録

using Game.Health;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Common
{
    /// <summary>
    /// 固定 SO レジストリを DI コンテナに登録する MB。
    /// SceneLTS と同階層に配置される想定。
    /// </summary>
    public sealed class FixedSORegistryMB : MonoBehaviour, IFeatureInstaller
    {
        [Header("Health")]
        [LabelText("Fixed Health Modifier Registry")]
        [Tooltip("Scene 固定の HealthModifier レジストリ")]
        [SerializeField]
        FixedHealthModifierRegistrySO _healthModifierRegistry;

        // 将来の拡張用に他のレジストリも追加可能
        // [Header("StatusEffect")]
        // [SerializeField]
        // FixedStatusEffectRegistrySO _statusEffectRegistry;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            // FixedHealthModifierRegistrySO をインスタンス登録
            if (_healthModifierRegistry != null)
            {
                builder.RegisterInstance(_healthModifierRegistry)
                    .As<FixedHealthModifierRegistrySO>();
            }

            // 将来の拡張用
            // if (_statusEffectRegistry != null)
            // {
            //     builder.RegisterInstance(_statusEffectRegistry)
            //         .As<FixedStatusEffectRegistrySO>();
            // }
        }
    }
}
