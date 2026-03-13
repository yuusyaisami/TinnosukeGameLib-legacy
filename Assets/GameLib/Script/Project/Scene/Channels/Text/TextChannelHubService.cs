// Game.Channel.TextChannelHubService.cs

#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using VContainer.Unity;
using VNext = Game.Commands.VNext;
using Game.Common;
using Game.MaterialFx;
using Game.Times;
namespace Game.Channel
{


    public interface ITextChannelHubService : IChannelHubService
    {
        IReadOnlyList<ITextChannelPlayer> Players { get; }

        ITextChannelPlayer GetPlayer(string tag);
        bool TryGetPlayer(string tag, out ITextChannelPlayer player);
    }

    /// <summary>
    /// Text チャネルの Hub。
    /// - tag -> def/player の辞書
    /// - Register/Unregister 対応
    /// - Def/Player のスナップショット（列挙安定 & 余計なGC抑制）
    ///
    /// 注意：
    /// - Hub は「管理」だけ。TextAnimator の配線や BodyFx の結合は Factory/Player 側でやる。
    /// </summary>
    public sealed class TextChannelHubService : ITextChannelHubService
    {
        const string DefaultTag = "default";

        readonly Dictionary<string, ITextChannelPlayer> _players = new(StringComparer.Ordinal);
        readonly Dictionary<string, TextChannelDef> _defsByTag = new(StringComparer.Ordinal);

        // 登録順を保持（Dictionary の列挙順に依存しない）
        readonly List<ITextChannelPlayer> _playerList = new();
        readonly List<TextChannelDef> _defOrder = new();

        // IChannelHubService.ChannelDefs 用スナップショット
        readonly List<ChannelDefBase> _defsSnapshot = new();
        bool _defsDirty = true;

        readonly IScopeNode _ownerScope;
        readonly IMaterialFxServiceFactory? _materialFxFactory;
        readonly ILTSIdentityService? _identity;

        public IReadOnlyList<ITextChannelPlayer> Players => _playerList;
        public IScopeNode OwnerScope => _ownerScope;

        public IReadOnlyList<ChannelDefBase> ChannelDefs
        {
            get
            {
                if (_defsDirty)
                {
                    _defsSnapshot.Clear();
                    for (int i = 0; i < _defOrder.Count; i++)
                        _defsSnapshot.Add(_defOrder[i]);
                    _defsDirty = false;
                }
                return _defsSnapshot;
            }
        }

        public TextChannelHubService(
            TextChannelDef[] channelDefs,
            IScopeNode ownerScope,
            IMaterialFxServiceFactory? materialFxFactory = null,
            ILTSIdentityService? identity = null)
        {
            _ownerScope = ownerScope;
            _materialFxFactory = materialFxFactory; // nullable OK
            _identity = identity;                   // nullable OK

            if (channelDefs == null)
                return;

            for (int i = 0; i < channelDefs.Length; i++)
                RegisterChannelInternal(channelDefs[i], overwrite: false);
        }

        // ========= ITextChannelHubService =========

        public ITextChannelPlayer GetPlayer(string tag)
        {
            tag = NormalizeTag(tag);

            if (_players.TryGetValue(tag, out var player))
                return player;

            throw new KeyNotFoundException($"[TextChannelHub] Channel '{tag}' not found.");
        }

        public bool TryGetPlayer(string tag, out ITextChannelPlayer player)
        {
            tag = NormalizeTag(tag);
            return _players.TryGetValue(tag, out player);
        }

        // ========= IChannelHubService =========

        public bool TryGetChannelDef(string tag, out ChannelDefBase def)
        {
            tag = NormalizeTag(tag);

            if (_defsByTag.TryGetValue(tag, out var tdef))
            {
                def = tdef;
                return true;
            }

            def = default!;
            return false;
        }

        public bool RegisterChannel(ChannelDefBase def, bool overwrite = false)
        {
            if (def is not TextChannelDef textDef)
                return false;

            return RegisterChannelInternal(textDef, overwrite);
        }

        public bool UnregisterChannel(string tag)
        {
            tag = NormalizeTag(tag);
            return RemoveChannelInternal(tag);
        }

        // ========= internal =========

        bool RegisterChannelInternal(TextChannelDef def, bool overwrite)
        {
            if (def == null)
                return false;

            var tag = NormalizeTag(def.Tag);
            if (string.IsNullOrWhiteSpace(tag))
                return false;

            // 参照の自己修復（TMP_Text の自動取得など）
            var integrityOwner = _ownerScope.Identity?.SelfTransform ?? (_ownerScope as Component)?.transform;
            if (integrityOwner == null)
                return false;
            def.EnsureIntegrity(integrityOwner);

            var targetText = def.Text;
            if (targetText == null)
                return false;

            if (_players.ContainsKey(tag))
            {
                if (!overwrite)
                    return false;

                RemoveChannelInternal(tag);
            }

            // ★ここが "Text Player の生成点"
            // TextAnimator3.0 の自動アタッチ / typewriter / shake 等は Player 内に閉じ込める。
            // MaterialFx も同様に Player 側で「def の preset を見て bind」すべき。
            // LTS の TimeScaleBehavior を TextAnimator に適用
            var timeScaleBehavior = _identity?.TimeScaleBehavior ?? TimeScaleBehavior.Scaled;
            var textName = targetText.name;
            //Debug.Log($"[TextChannelHub] RegisterChannel: tag='{def.Tag}', def.Text={textName} timeScaleBehavior={timeScaleBehavior} scope={DescribeScope(_ownerScope)}");

            // Counter settings from def
            var counterSettings = new SetTextSettings
            {
                UseCounter = def.UseCounter,
                CounterEase = def.CounterEase,
                CounterDurationSeconds = def.CounterDurationSeconds,
                CounterUseUnscaledTime = def.CounterUseUnscaledTime,
            };

            var player = new TextChannelPlayer(
                def.Tag,
                targetText,
                def.UseRichTextAnimator,
                def.UseTypewriter,
                def.UseCounter,
                true,
                _ownerScope,
                timeScaleBehavior,
                _materialFxFactory,
                def.MaterialFxPresetEntries,
                counterSettings);

            _players[tag] = player;
            _playerList.Add(player);

            _defsByTag[tag] = def;
            _defOrder.Add(def);
            _defsDirty = true;

            return true;
        }

        bool RemoveChannelInternal(string tag)
        {
            if (!_players.TryGetValue(tag, out var player))
                return false;


            player.Dispose();


            _players.Remove(tag);
            _playerList.Remove(player);

            _defsByTag.Remove(tag);
            RemoveDefFromOrderByTag(tag);

            _defsDirty = true;
            return true;
        }

        void RemoveDefFromOrderByTag(string tag)
        {
            for (int i = _defOrder.Count - 1; i >= 0; i--)
            {
                var d = _defOrder[i];
                if (d != null && string.Equals(NormalizeTag(d.Tag), tag, StringComparison.Ordinal))
                    _defOrder.RemoveAt(i);
            }
        }

        static string NormalizeTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return DefaultTag;
            return tag;
        }

        static string DescribeScope(IScopeNode scope)
        {
            if (scope == null)
                return "<null>";
            if (scope is UnityEngine.Object unityObj && !unityObj)
                return "<destroyed>";
            var id = scope.Identity?.Id;
            if (!string.IsNullOrEmpty(id))
                return $"{id} ({scope.Kind})";
            return scope.GetType().Name;
        }
    }
}
