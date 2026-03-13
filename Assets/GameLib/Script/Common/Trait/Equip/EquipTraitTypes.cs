#nullable enable

namespace Game.Trait
{
    /// <summary>
    /// EquipTraitHolder のスロット操作種別。
    /// </summary>
    public enum EquipTraitOp
    {
        Equip = 0,
        Unequip = 1,
    }

    /// <summary>
    /// Equip 対象の Trait をどのように指定するか。
    /// </summary>
    public enum EquipTraitTargetKind
    {
        /// <summary>TraitDefinitionSO を直接指定。</summary>
        ByDefinition = 0,

        /// <summary>TraitHolder 内の最初の Trait。</summary>
        First = 1,

        /// <summary>TraitHolder 内の最後の Trait。</summary>
        Last = 2,

        /// <summary>TraitHolder 内のインデックス指定。</summary>
        ByIndex = 3,

        /// <summary>TraitDefinition の DefinitionId 文字列で検索。</summary>
        ByDefinitionId = 4,
    }
}
