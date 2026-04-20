// Game.Health.FixedHealthModifierRegistrySO.cs
//
// Scene 蜊倅ｽ阪〒蝗ｺ螳夐←逕ｨ縺吶ｋ HealthModifier 繧剃ｿ晄戟縺吶ｋ繝ｬ繧ｸ繧ｹ繝医Μ SO

using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Health
{
    /// <summary>
    /// Scene 蜊倅ｽ阪〒蝗ｺ螳夐←逕ｨ縺吶ｋ HealthModifier 繧剃ｿ晄戟縺吶ｋ繝ｬ繧ｸ繧ｹ繝医Μ SO縲・
    /// SceneLTS 縺ｨ蜷碁嚴螻､縺ｮ FixedSORegistryMB 縺九ｉ蜿ら・縺輔ｌ縲・
    /// HealthMB 蛻晄悄蛹匁凾縺ｫ閾ｪ蜍慕匳骭ｲ縺輔ｌ繧九・
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Health/FixedHealthModifierRegistry", fileName = "FixedHealthModifierRegistry")]
    public sealed class FixedHealthModifierRegistrySO : ScriptableObject
    {
        [LabelText("Fixed Modifiers")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        List<BaseHealthModifierSO> _modifiers = new();

        /// <summary>蝗ｺ螳・Modifier 繝ｪ繧ｹ繝茨ｼ郁ｪｭ縺ｿ蜿悶ｊ蟆ら畑・・/summary>
        public IReadOnlyList<BaseHealthModifierSO> Modifiers => _modifiers;

        /// <summary>
        /// 謖・ｮ・ModifierId 縺ｮ Modifier 繧貞叙蠕・
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
