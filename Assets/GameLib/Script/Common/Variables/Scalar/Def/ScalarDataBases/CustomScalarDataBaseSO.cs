using System.Collections.Generic;
using UnityEngine;

namespace Game.Scalar
{
    /// <summary>
    /// カスタムScalarデータベースのScriptableObject。
    /// </summary>
    [CreateAssetMenu(fileName = "CustomScalarDataBaseSO", menuName = "Game/Scalar/CustomScalarDataBaseSO")]
    public class CustomScalarDataBaseSO : BaseScalarDatabaseSO
    {
        /// <summary>
        /// 登録するエントリを取得する。
        /// </summary>
        public override IEnumerable<ScalarDatabaseEntry> GetEntries()
        {
            foreach (var entry in base.GetEntries())
            {
                yield return entry;
            }

            // ここにカスタムエントリを追加可能
            // yield return new ScalarDatabaseEntry { Key = new ScalarKey("CustomKey"), BaseValue = 1.0f, UseEffectMod = true, UseRoundMod = false, RoundDigits = 0, UseClampMod = false, Clamp = default };
        }
    }
}