// Game.Animation.ChannelDefs

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Channel
{
    /// <summary>
    /// 全チャネル Hub 共通のベース。
    /// - 現在登録されている ChannelDef 一覧
    /// - 後からチャネルを追加／削除
    /// - タグから ChannelDef を取得
    /// </summary>
    public interface IChannelHubService
    {
        /// <summary>この Hub が保持しているチャネル定義の一覧（基底クラス）。</summary>
        IReadOnlyList<ChannelDefBase> ChannelDefs { get; }

        /// <summary>タグから ChannelDef を取得。</summary>
        bool TryGetChannelDef(string tag, out ChannelDefBase def);

        /// <summary>
        /// チャネルを登録。
        /// overwrite=false のとき、同じ tag が既にあれば false を返す。
        /// </summary>
        bool RegisterChannel(ChannelDefBase def, bool overwrite = false);

        /// <summary>
        /// タグ指定でチャネルを削除。
        /// 成功したら true。
        /// </summary>
        bool UnregisterChannel(string tag);
    }
    /// <summary>
    /// すべてのチャネル定義の共通基底（tag だけ持つ）。
    /// </summary>
    [Serializable]
    public abstract class ChannelDefBase : IChannelIdentity
    {
        [SerializeField]
        [Tooltip("チャネル識別子")]
        string tag = "default";

        public string Tag => tag;

        public virtual void EnsureIntegrity(Component owner)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                tag = "default";
            }
        }
    }
}
