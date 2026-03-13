// Game.Health.FixedHealthModifierRegistrySO.cs
//
// Scene 単位で固定適用する HealthModifier を保持するレジストリ SO

using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Health
{
    /// <summary>
    /// Scene 単位で固定適用する HealthModifier を保持するレジストリ SO。
    /// SceneLTS と同階層の FixedSORegistryMB から参照され、
    /// HealthMB 初期化時に自動登録される。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Health/FixedHealthModifierRegistry", fileName = "FixedHealthModifierRegistry")]
    public sealed class FixedHealthModifierRegistrySO : ScriptableObject
    {
        [LabelText("Fixed Modifiers")]
        [Tooltip("Scene 内の全 Entity に適用される Modifier リスト（登録順）")]
        [SerializeField]
        List<BaseHealthModifierSO> _modifiers = new();

        /// <summary>固定 Modifier リスト（読み取り専用）</summary>
        public IReadOnlyList<BaseHealthModifierSO> Modifiers => _modifiers;

        /// <summary>
        /// 指定 ModifierId の Modifier を取得
        /// </summary>
        public bool TryGetModifier(string modifierId, out BaseHealthModifierSO modifier)
        {
            modifier = null;
            if (string.IsNullOrEmpty(modifierId))
                return false;

            for (int i = 0; i < _modifiers.Count; i++)
            {
                if (_modifiers[i] != null && _modifiers[i].ModifierId == modifierId)
                {
                    modifier = _modifiers[i];
                    return true;
                }
            }

            return false;
        }
    }
}
