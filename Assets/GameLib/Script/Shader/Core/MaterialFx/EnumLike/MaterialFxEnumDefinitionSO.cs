using System;
using UnityEngine;
using Game.EnumLike;

namespace Game.MaterialFx
{
    // ================================================================
    // MaterialFxEnumDefinitionSO - MaterialFx specific EnumDefinition
    // ================================================================
    //
    // ## Overview
    //
    // MaterialFx specific enum-like definition.
    // Inherits from BaseEnumDefinitionSO for extensibility.
    //
    // ## Usage
    //
    // - FlashMode (Lerp / Add)
    // - BlendMode (Normal / Additive / Multiply)
    // - etc.
    //
    // ================================================================

    /// <summary>
    /// MaterialFx specific enum entry.
    /// Can be extended with MaterialFx-specific fields if needed.
    /// </summary>
    [Serializable]
    public class MaterialFxEnumEntry : BaseEnumEntry
    {
        // Add MaterialFx-specific fields here if needed in the future
        // e.g., shader keyword, default color, etc.
    }

    /// <summary>
    /// MaterialFx specific enum-like definition.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewMaterialFxEnum",
        menuName = "Game/MaterialFx/Enum Definition",
        order = 100)]
    public class MaterialFxEnumDefinitionSO : BaseEnumDefinitionSO<MaterialFxEnumEntry>
    {
        // Add MaterialFx-specific methods here if needed
    }
}
