#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Game.MaterialFx;
using UnityEngine;

namespace Game.Editor.Registry
{
    /// <summary>
    /// MaterialFxSenderKind ごとの行背景色設定。
    /// </summary>
    [Serializable]
    public class MaterialFxSenderColorEntry
    {
        public MaterialFxSenderKind Sender;
        public Color Color;
    }

    /// <summary>
    /// MaterialFx の Explorer / CodeGen 統合設定。
    /// </summary>
    [CreateAssetMenu(
        fileName = "MaterialFxSettings",
        menuName = "Game/Registry/Settings/MaterialFx Settings")]
    public sealed class MaterialFxSettings : RegistrySettingsBase
    {
        [Header("MaterialFx Sender Colors")]
        [Tooltip("Sender ごとの行背景色")]
        [SerializeField]
        List<MaterialFxSenderColorEntry> senderColors = new()
        {
            new() { Sender = MaterialFxSenderKind.BaseShader, Color = new Color(0.15f, 0.25f, 0.45f, 0.35f) }
        };

        [Header("Enum Catalog")]
        [Tooltip("EnumDefinition の一覧を持つカタログ SO")]
        [SerializeField]
        MaterialFxEnumCatalogSO enumCatalog;

        /// <summary>Sender ごとの行背景色リスト</summary>
        public IReadOnlyList<MaterialFxSenderColorEntry> SenderColors => senderColors;

        /// <summary>EnumDefinition カタログ</summary>
        public MaterialFxEnumCatalogSO EnumCatalog => enumCatalog;

        /// <summary>
        /// 指定した Sender の行背景色を取得する。
        /// </summary>
        public Color? GetSenderColor(MaterialFxSenderKind sender)
        {
            foreach (var entry in senderColors)
            {
                if (entry.Sender == sender)
                    return entry.Color;
            }
            return null;
        }

        void Reset()
        {
            windowTitle = "MaterialFx Property Explorer";
            namespaceName = "Game.MaterialFx.Generated";
            rootClassName = "MaterialFxKeys";
            outputPath = "Assets/GameLib/Script/Generated/MaterialFxKeys.g.cs";

            // デフォルトの Sender 色を設定
            senderColors = new List<MaterialFxSenderColorEntry>
            {
                new() { Sender = MaterialFxSenderKind.BaseShader, Color = new Color(0.15f, 0.25f, 0.45f, 0.35f) }
            };
        }
    }
}
#endif
