using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Sirenix.OdinInspector;
using Game.Commands;
using Game.Common;
using Game.MaterialFx;

namespace Game.Channel
{
    /// <summary>
    /// TextChannelHub 縺ｮ繧､繝ｳ繧ｹ繝医・繝ｩ繝ｼ MonoBehaviour縲・
    /// BaseLifetimeScope 縺ｫ驟咲ｽｮ縺励※菴ｿ逕ｨ縺吶ｋ縲・
    /// TextChannelDef 縺ｮ驟榊・繧・Inspector 縺ｧ險ｭ螳壹＠縲！TextChannelHubService 縺ｨ縺励※逋ｻ骭ｲ縺吶ｋ縲・
    /// </summary>
    public sealed class TextChannelHubMB : MonoBehaviour, IFeatureInstaller
    {
        [Header("Channel Definitions")]
        [SerializeField]
        [Tooltip("縺薙・繧ｹ繧ｳ繝ｼ繝励〒邂｡逅・☆繧九ユ繧ｭ繧ｹ繝医メ繝｣繝ｳ繝阪Ν縺ｮ螳夂ｾｩ")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        TextChannelDef[] _channelDefs = Array.Empty<TextChannelDef>();

        #region Debug View

        [FoldoutGroup("Debug")]
        [ShowInInspector, ReadOnly]
        [TableList(AlwaysExpanded = true, IsReadOnly = true)]
        List<TextChannelDebugEntry> _debugEntries = new();

        ITextChannelHubService _hubRef;

        [Serializable]
        public class TextChannelDebugEntry
        {
            [TableColumnWidth(100)]
            [LabelText("Tag")]
            public string Tag = "";

            [TableColumnWidth(80)]
            [LabelText("TextAnimator")]
            public bool HasTextAnimator;

            [TableColumnWidth(80)]
            [LabelText("Typewriter")]
            public bool HasTypewriter;
        }

        #endregion

        #region IFeatureInstaller

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            // TextChannelDef[] 繧偵ヱ繝ｩ繝｡繝ｼ繧ｿ縺ｨ縺励※貂｡縺・
            builder.Register<TextChannelHubService>(RuntimeLifetime.Singleton)
                   .WithParameter(_channelDefs)
                   .WithParameter(owner)
                   .As<ITextChannelHubService>()
                     .As<IChannelHubService>()
                     .As<IScopeAcquireHandler>()
                     .As<IScopeReleaseHandler>()
                     .As<IScopeTickHandler>();

            // 繝・ヰ繝・げ逕ｨ縺ｫ蜿ら・繧剃ｿ晄戟
            builder.RegisterBuildCallback(container =>
            {
                _hubRef = container.Resolve<ITextChannelHubService>();
                RefreshDebugInfo();
            });
        }

        #endregion

        #region Debug Methods

        [FoldoutGroup("Debug")]
        [Button("Refresh Debug Info", ButtonSizes.Medium)]
        [PropertyOrder(100)]
        void RefreshDebugInfo()
        {
            _debugEntries.Clear();

            if (_hubRef == null)
            {
                // Fallback: show from serialized defs
                foreach (var def in _channelDefs)
                {
                    if (def == null) continue;
                    _debugEntries.Add(new TextChannelDebugEntry
                    {
                        Tag = def.Tag,
                        HasTextAnimator = def.UseRichTextAnimator,
                        HasTypewriter = def.UseTypewriter,
                    });
                }
                return;
            }

            foreach (var player in _hubRef.Players)
            {
                _debugEntries.Add(new TextChannelDebugEntry
                {
                    Tag = player.Tag,
                    HasTextAnimator = player.SupportsRichAnimation,
                    HasTypewriter = player.SupportsTypewriter,
                });
            }
        }

        void OnValidate()
        {
            if (_hubRef != null)
                RefreshDebugInfo();
        }

        #endregion
    }
}
