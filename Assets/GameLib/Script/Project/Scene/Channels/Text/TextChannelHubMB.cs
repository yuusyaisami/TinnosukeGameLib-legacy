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
    /// TextChannelHub のインスト�Eラー MonoBehaviour、E
    /// Scope host に配置して使用する、E
    /// TextChannelDef の配�EめEInspector で設定し、ITextChannelHubService として登録する、E
    /// </summary>
    public sealed class TextChannelHubMB : MonoBehaviour, IScopeInstaller
    {
        [Header("Channel Definitions")]
        [SerializeField]
        [Tooltip("こ�Eスコープで管琁E��るテキストチャンネルの定義")]
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

        #region IScopeInstaller

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            // TextChannelDef[] をパラメータとして渡ぁE
            builder.Register<TextChannelHubService>(RuntimeLifetime.Singleton)
                   .WithParameter(_channelDefs)
                   .WithParameter(owner)
                   .As<ITextChannelHubService>()
                     .As<IChannelHubService>()
                     .As<IScopeAcquireHandler>()
                     .As<IScopeReleaseHandler>()
                     .As<IScopeTickHandler>();

            // チE��チE��用に参�Eを保持
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

