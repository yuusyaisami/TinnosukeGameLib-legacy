#nullable enable
using System;
using VContainer;
using System.Collections.Generic;
using Game.MaterialFx;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Channel
{
    /// <summary>
    /// AnimationSpriteHub に紐づく Player の MaterialFx 状態を Inspector で可視化するデバッガ。
    /// - 指定した ChannelTag の IAnimationSpriteChannelPlayer.MaterialFx を取得
    /// - IMaterialFxTelemetry から現在の Stack/Layer 情報をスナップショット表示
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnimationSpriteHubMaterialFxDebuggerMB : MonoBehaviour, IScopeAcquireHandler, IScopeReleaseHandler
    {
        [Header("Target")]
        [SerializeField]
        string channelTag = string.Empty;

        [Header("Snapshot")]
        [SerializeField, ReadOnly]
        string status = "(not acquired)";

        [SerializeField, ListDrawerSettings(ShowFoldout = true, ShowPaging = false)]
        List<MaterialFxStackTelemetry> stacks = new();

        Game.IScopeNode? _scope;
        IAnimationSpriteHubService? _hub;

        public void OnAcquire(Game.IScopeNode scope, bool isReset)
        {
            _scope = scope;
            status = "(acquired)";

            if (scope.Resolver != null && scope.Resolver.TryResolve<IAnimationSpriteHubService>(out var hub) && hub != null)
            {
                _hub = hub;
                status = "(hub resolved)";
            }
            else
            {
                _hub = null;
                status = "(hub not found)";
            }
        }

        public void OnRelease(Game.IScopeNode scope, bool isReset)
        {
            _hub = null;
            _scope = null;
            status = "(released)";
            stacks.Clear();
        }

        [Button]
        void Refresh()
        {
            stacks.Clear();

            if (!Application.isPlaying)
            {
                status = "(playmode only)";
                return;
            }

            if (_hub == null)
            {
                status = "(hub not resolved)";
                return;
            }

            if (string.IsNullOrWhiteSpace(channelTag))
            {
                status = "(channel tag not set)";
                return;
            }

            var tag = channelTag;
            if (!_hub.TryGetPlayer(tag, out var player) || player == null)
            {
                status = $"(player not found: '{tag}')";
                return;
            }

            var fx = player.MaterialFx;
            if (fx == null)
            {
                status = "(player.MaterialFx is null)";
                return;
            }

            if (fx is not IMaterialFxTelemetry telemetry)
            {
                status = "(MaterialFxTelemetry not available on this service)";
                return;
            }

            telemetry.GetSnapshot(stacks);
            status = $"(ok) stacks={stacks.Count} telemetryId='{telemetry.TelemetryId}'";
        }

        [Button]
        void ClearSnapshot()
        {
            stacks.Clear();
            status = "(cleared)";
        }
    }
}
