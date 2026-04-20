// Game.StateMachine.StateAnimationController.cs

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Animation;
using Game.Channel;
using Game.Common;
using UnityEngine;
using VContainer.Unity;

namespace Game.StateMachine
{
    /// <summary>
    /// StateMachine の状態変化を監視し、アニメーションを制御する、E
    /// </summary>
    /// <remarks>
    /// <para>更新トリガ:</para>
    /// <list type="bullet">
    ///   <item><see cref="IStateMachineReadOnly.MachineRevision"/> が変化した時点でルール再評価</item>
    ///   <item>State 変化 ↁERestartMode に応じて再開姁E/item>
    ///   <item>Pulse 発火 ↁERestartMode = OnPulse なら�E開姁E/item>
    /// </list>
    /// <para>Option 解決:</para>
    /// <para>
    /// 全ての Option 参�Eは <see cref="IStateMachineReadOnly.ResolveOption(string, string)"/> を通じて
    /// Local(currentLayer) ↁEGlobal の頁E��解決される、E
    /// </para>
    /// </remarks>
    public sealed class StateAnimationController : IDisposable, IScopeTickHandler, IStateAnimationTelemetry
    {
        const float FlipXDebounceSeconds = 0.06f;

        readonly IStateMachineReadOnly _stateMachine;
        readonly IAnimationSpriteHubService _animationHub;
        readonly IDynamicContext _dynamicContext;

        StateAnimationPreset _profile;
        uint _lastMachineRevision;
        string _lastCurrentState;
        uint _lastPulseCount;
        StateAnimationRule _currentRule;
        bool _lastFlipX;
        bool _hasFlipX;

        bool _pendingFlipXActive;
        bool _pendingFlipXValue;
        float _pendingFlipXStartTime;

        CancellationTokenSource _cts;
        IAnimationSpriteChannelPlayer _currentPlayer;
        bool _disposed;
        int _telemetryVersion;
        string _lastEvaluationSummary = "Not evaluated yet.";
        readonly List<StateAnimationRuleTelemetryRow> _ruleTelemetryRows = new();
        string _lastFlipDecisionDetail = "Not evaluated yet.";
        string _lastFlipConfiguredOptionValue = string.Empty;
        string _lastFlipDerivedOptionKey = string.Empty;
        string _lastFlipResolvedByDerivedKey = string.Empty;
        string _lastFlipResolvedByConfiguredKey = string.Empty;

        /// <summary>現在の Profile</summary>
        public StateAnimationPreset Profile => _profile;
        public int TelemetryVersion => _telemetryVersion;

        public StateAnimationController(
            IStateMachineReadOnly stateMachine,
            IScopeNode ownerScope,
            StateAnimationPreset profile,
            IAnimationSpriteHubService animationHub)
        {
            _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
            if (ownerScope == null)
                throw new ArgumentNullException(nameof(ownerScope));
            _profile = profile; // null 許容
            _animationHub = animationHub ?? throw new ArgumentNullException(nameof(animationHub));
            _dynamicContext = new SimpleDynamicContext(NullVarStore.Instance, ownerScope);
            // Initialize tracking state
            _lastMachineRevision = _stateMachine.MachineRevision;
            _lastCurrentState = _stateMachine.CurrentState;

            if (!string.IsNullOrEmpty(_lastCurrentState))
            {
                _lastPulseCount = _stateMachine.GetPulseCount(_lastCurrentState);
            }

            BumpTelemetry();
        }

        // ════════════════════════════════════════════════════════════════
        //  Profile Hot-Swap
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Profile を動皁E��差し替える、E
        /// 現在再生中のアニメーションを完�E停止し、新 Profile で即座に再評価・再生する、E
        /// </summary>
        /// <param name="profile">新しいプロファイル</param>
        /// <param name="restartImmediately">true の場合、現在の StateMachine 状態に対して即座に再生を開姁E/param>
        public void SetProfile(StateAnimationPreset profile, bool restartImmediately = true)
        {
            // 現在再生中のアニメーションを完�E停止�E�Eween 残留防止�E�E
            StopCurrentAnimationFully();

            // Profile 差し替ぁE
            _profile = profile;

            // キャチE��ュクリア
            _currentRule = null;

            if (restartImmediately && _profile != null)
            {
                // 現在の StateMachine 状態で即座に再評価・再生�E�Eick 征E��にしなぁE��E
                ForceEvaluateAndPlay();
            }

            BumpTelemetry();
        }

        /// <summary>
        /// 強制皁E��ルール再評価して再生を開始する！Eick 征E��なし）、E
        /// </summary>
        void ForceEvaluateAndPlay()
        {
            var currentState = _stateMachine.CurrentState;
            var currentLayer = _stateMachine.CurrentLayer;

            // Pulse トラチE��ング更新
            _lastCurrentState = currentState;
            _lastPulseCount = !string.IsNullOrEmpty(currentState)
                ? _stateMachine.GetPulseCount(currentState)
                : 0;

            // Revision 同期
            _lastMachineRevision = _stateMachine.MachineRevision;

            // ルール評価
            var matchedRule = EvaluateRules(currentState, currentLayer);

            if (matchedRule == null)
            {
                _currentRule = null;
                BumpTelemetry();
                return;
            }

            _currentRule = matchedRule;
            PlayAnimation(matchedRule, currentLayer);
            BumpTelemetry();
        }

        /// <summary>
        /// 毎フレーム呼び出す。Revision を監視して忁E��に応じてアニメーション更新、E
        /// </summary>
        public void Tick()
        {
            if (_disposed || _profile == null)
                return;

            // FlipX の確定だけ�E MachineRevision に依存せず毎フレーム軽量に処琁E��る、E
            // Option のチャタリングで flipX が往復しても、一定時間安定するまで反映しなぁE��E
            ProcessPendingFlipX();

            var currentRevision = _stateMachine.MachineRevision;
            if (currentRevision == _lastMachineRevision)
                return;

            _lastMachineRevision = currentRevision;

            var currentState = _stateMachine.CurrentState;
            var currentLayer = _stateMachine.CurrentLayer;
            bool stateChanged = _lastCurrentState != currentState;

            // Check for Pulse
            bool pulseFired = false;
            if (!stateChanged && !string.IsNullOrEmpty(currentState))
            {
                var pulseCount = _stateMachine.GetPulseCount(currentState);
                if (pulseCount != _lastPulseCount)
                {
                    pulseFired = true;
                    _lastPulseCount = pulseCount;
                }
            }
            else if (stateChanged)
            {
                // Reset pulse tracking on state change
                _lastPulseCount = !string.IsNullOrEmpty(currentState)
                    ? _stateMachine.GetPulseCount(currentState)
                    : 0;
            }

            _lastCurrentState = currentState;

            // Evaluate rules
            var matchedRule = EvaluateRules(currentState, currentLayer);

            if (matchedRule == null)
            {
                // No matching rule ↁEstop current animation fully (Tween 残留防止)
                StopCurrentAnimationFully();
                _currentRule = null;
                _hasFlipX = false;
                _pendingFlipXActive = false;
                BumpTelemetry();
                return;
            }

            // Determine if restart is needed
            bool shouldRestart = false;

            if (_currentRule != matchedRule)
            {
                // Rule changed ↁErestart
                shouldRestart = true;
            }
            else if (stateChanged && matchedRule.RestartMode != AnimationRestartMode.Never)
            {
                // State changed (and not Never mode) ↁErestart
                shouldRestart = true;
            }
            else if (pulseFired && matchedRule.RestartMode == AnimationRestartMode.OnPulse)
            {
                // Pulse fired with OnPulse mode ↁErestart
                shouldRestart = true;
            }

            if (shouldRestart)
            {
                _currentRule = matchedRule;
                PlayAnimation(matchedRule, currentLayer);
                BumpTelemetry();
                return;
            }

            // Option 変化のみでルールが同一の場合も、FlipX は再評価して即時反映する、E
            ApplyFlipXIfChanged(matchedRule, currentLayer);
        }

        void ProcessPendingFlipX()
        {
            if (!_pendingFlipXActive)
                return;

            if (_currentPlayer == null || _currentRule == null)
            {
                _pendingFlipXActive = false;
                BumpTelemetry();
                return;
            }

            var currentLayer = _stateMachine.CurrentLayer;
            bool desired = DetermineFlipX(_currentRule, currentLayer);

            // 既に last と一致してぁE��なめEpending を破棁E
            if (_hasFlipX && desired == _lastFlipX)
            {
                _pendingFlipXActive = false;
                BumpTelemetry();
                return;
            }

            // Pending 途中で desired が変わったらめE��直ぁE
            if (desired != _pendingFlipXValue)
            {
                _pendingFlipXValue = desired;
                _pendingFlipXStartTime = Time.time;
                BumpTelemetry();
                return;
            }

            if (Time.time - _pendingFlipXStartTime < FlipXDebounceSeconds)
                return;

            _pendingFlipXActive = false;

            _lastFlipX = desired;
            _hasFlipX = true;
            _currentPlayer.SetFlipX(desired);
            BumpTelemetry();
        }

        /// <summary>
        /// ルールを優先度頁E��評価し、最初にマッチしたルールを返す、E
        /// </summary>
        StateAnimationRule EvaluateRules(string currentState, string currentLayer)
        {
            _ruleTelemetryRows.Clear();

            if (_profile == null)
            {
                _lastEvaluationSummary = "Profile is null.";
                BumpTelemetry();
                return null;
            }

            if (string.IsNullOrEmpty(currentState))
            {
                _lastEvaluationSummary = "CurrentState is empty.";
                BumpTelemetry();
                return null;
            }

            foreach (var rule in _profile.GetRulesByPriority())
            {
                if (rule == null)
                    continue;

                if (MatchesRule(rule, currentState, currentLayer, out var reason))
                {
                    _ruleTelemetryRows.Add(new StateAnimationRuleTelemetryRow(
                        rule.RuleHeader,
                        rule.Priority,
                        rule.StateKey ?? string.Empty,
                        rule.LayerKey ?? string.Empty,
                        rule.ChannelTag ?? string.Empty,
                        rule.ApplyFlipX,
                        rule.FlipXTrueOptionValue ?? string.Empty,
                        true,
                        "Matched"));

                    _lastEvaluationSummary = $"Matched rule: {rule.RuleHeader}";
                    BumpTelemetry();
                    return rule;
                }

                _ruleTelemetryRows.Add(new StateAnimationRuleTelemetryRow(
                    rule.RuleHeader,
                    rule.Priority,
                    rule.StateKey ?? string.Empty,
                    rule.LayerKey ?? string.Empty,
                    rule.ChannelTag ?? string.Empty,
                    rule.ApplyFlipX,
                    rule.FlipXTrueOptionValue ?? string.Empty,
                    false,
                    reason ?? "Not matched"));
            }

            _lastEvaluationSummary = "No rules matched current state/layer/options.";
            BumpTelemetry();
            return null;
        }

        /// <summary>
        /// ルールが現在の状態にマッチするかを判定する、E
        /// </summary>
        bool MatchesRule(StateAnimationRule rule, string currentState, string currentLayer, out string reason)
        {
            reason = "Matched";

            // State condition
            if (!string.IsNullOrEmpty(rule.StateKey) && rule.StateKey != currentState)
            {
                reason = $"StateKey mismatch (need='{rule.StateKey}')";
                return false;
            }

            // Layer condition
            if (!string.IsNullOrEmpty(rule.LayerKey) && rule.LayerKey != currentLayer)
            {
                reason = $"LayerKey mismatch (need='{rule.LayerKey}')";
                return false;
            }

            // Option conditions
            if (rule.ConditionMode == StateAnimationConditionMode.LegacyOptionConditions)
            {
                if (rule.OptionConditions.Count > 0)
                {
                    static bool EvaluateCondition(StateAnimationController self, string currentLayer, OptionCondition condition)
                    {
                        if (self == null || condition == null || string.IsNullOrEmpty(condition.OptionKey))
                            return true;
                        var resolvedValue = self._stateMachine.ResolveOption(currentLayer, condition.OptionKey);
                        var hasValue = !string.IsNullOrEmpty(resolvedValue);
                        return condition.Presence == OptionConditionPresence.IsSet
                            ? hasValue
                            : !hasValue;
                    }

                    switch (rule.OptionLogic)
                    {
                        case OptionConditionEvaluationMode.All:
                            foreach (var cond in rule.OptionConditions)
                            {
                                if (!EvaluateCondition(this, currentLayer, cond))
                                {
                                    reason = $"OptionCondition failed (key='{cond.OptionKey}', presence={cond.Presence})";
                                    return false;
                                }
                            }
                            break;
                        case OptionConditionEvaluationMode.Any:
                            var anyMatch = false;
                            foreach (var cond in rule.OptionConditions)
                            {
                                if (EvaluateCondition(this, currentLayer, cond))
                                {
                                    anyMatch = true;
                                    break;
                                }
                            }
                            if (!anyMatch)
                            {
                                reason = "OptionConditions (Any) did not match any condition.";
                                return false;
                            }
                            break;
                    }
                }
            }
            else if (rule.ConditionMode == StateAnimationConditionMode.DynamicBool)
            {
                bool conditionMatched = rule.DynamicOptionCondition.GetOrDefault(_dynamicContext, true);
                if (!conditionMatched)
                {
                    reason = "DynamicOptionCondition evaluated to false.";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// ルールに従ってアニメーションを�E生する、E
        /// </summary>
#nullable enable
        void PlayAnimation(StateAnimationRule rule, string currentLayer)
        {
            var preset = rule.GetAnimationPreset();
            if (!AnimationDataSource.TryGet(preset.animationA, out var clipA) || clipA == null)
                return;

            IAnimationData? clipB = null;
#nullable restore
            if (preset.playMode == Channel.AnimationPlayMode.CrossFade || preset.playMode == Game.Channel.AnimationPlayMode.OnceToLoop)
            {
                if (!AnimationDataSource.TryGet(preset.animationB, out clipB) || clipB == null)
                    return;
            }

            // Stop existing animation (fully)
            StopCurrentAnimationFully();

            // Determine FlipX
            bool flipX = DetermineFlipX(rule, currentLayer);
            _lastFlipX = flipX;
            _hasFlipX = true;
            _pendingFlipXActive = false;

            // Play animation via hub
            if (!_animationHub.TryGetPlayer(rule.ChannelTag, out var player))
                return;

            _currentPlayer = player;
            _cts = new CancellationTokenSource();
            player.SetUsePlaybackSpeedMultiplier(preset.usePlaybackSpeedMultiplier);

            player.PlayAsync(clipA, clipB, preset.playMode, flipX, _cts.Token).Forget();
            BumpTelemetry();
        }

        void ApplyFlipXIfChanged(StateAnimationRule rule, string currentLayer)
        {
            if (_currentPlayer == null || rule == null)
                return;

            bool flipX = DetermineFlipX(rule, currentLayer);
            if (_hasFlipX && _lastFlipX == flipX)
            {
                _pendingFlipXActive = false;
                BumpTelemetry();
                return;
            }

            // Flip の確定�EチE��ウンスで行う�E�短時間のチャタリング対策！E
            if (!_pendingFlipXActive || _pendingFlipXValue != flipX)
            {
                _pendingFlipXActive = true;
                _pendingFlipXValue = flipX;
                _pendingFlipXStartTime = Time.time;
                BumpTelemetry();
            }
        }

        /// <summary>
        /// FlipX を決定する、E
        /// </summary>
        /// <remarks>
        /// <para>仕槁E</para>
        /// <list type="bullet">
        ///   <item>ApplyFlipX = false ↁEflipX = false</item>
        ///   <item>ApplyFlipX = true &amp;&amp; FlipXTrueOptionValue is empty ↁEflipX = true (無条件)</item>
        ///   <item>FlipXTrueOptionValue ぁEOptionValue の場吁E
        ///     侁E Movement.Direction.Left。導�Eした OptionKey めEResolve し、値一致で true</item>
        ///   <item>FlipXTrueOptionValue ぁEOptionKey の場吁E
        ///     侁E Movement.Direction。現在値ぁEset されてぁE��ば true</item>
        /// </list>
        /// </remarks>
        bool DetermineFlipX(StateAnimationRule rule, string currentLayer)
        {
            _lastFlipConfiguredOptionValue = rule?.FlipXTrueOptionValue ?? string.Empty;
            _lastFlipDerivedOptionKey = string.Empty;
            _lastFlipResolvedByDerivedKey = string.Empty;
            _lastFlipResolvedByConfiguredKey = string.Empty;

            if (!rule.ApplyFlipX)
            {
                _lastFlipDecisionDetail = "ApplyFlipX is false.";
                return false;
            }

            // ApplyFlipX = true で FlipXTrueOptionValue が空 ↁE無条件 true
            if (string.IsNullOrWhiteSpace(rule.FlipXTrueOptionValue))
            {
                _lastFlipDecisionDetail = "ApplyFlipX is true and FlipXTrueOptionValue is empty => unconditional true.";
                return true;
            }

            var configured = rule.FlipXTrueOptionValue.Trim();
            _lastFlipConfiguredOptionValue = configured;

            // 1) OptionValue 持E��として判宁E
            //    configured = "Movement.Direction.Left" のようなフル OptionValue
            var optionKeyFromValue = StateKeyUtils.GetOptionKey(configured);
            _lastFlipDerivedOptionKey = optionKeyFromValue ?? string.Empty;
            if (!string.IsNullOrEmpty(optionKeyFromValue))
            {
                var resolvedValue = ResolveOptionForFlip(currentLayer, optionKeyFromValue);
                _lastFlipResolvedByDerivedKey = resolvedValue ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(resolvedValue) &&
                    string.Equals(resolvedValue.Trim(), configured, StringComparison.OrdinalIgnoreCase))
                {
                    _lastFlipDecisionDetail =
                        $"Matched by OptionValue compare. key='{optionKeyFromValue}', resolved='{resolvedValue}', configured='{configured}'.";
                    return true;
                }
            }

            // 2) OptionKey 持E��として判宁E
            //    configured = "Movement.Direction" のようなキーを直接持E��した場合、E
            //    そ�Eキーに現在値が存在すれば true、E
            var resolvedByKey = ResolveOptionForFlip(currentLayer, configured);
            _lastFlipResolvedByConfiguredKey = resolvedByKey ?? string.Empty;
            if (!string.IsNullOrEmpty(resolvedByKey))
            {
                _lastFlipDecisionDetail =
                    $"Matched by key presence. key='{configured}', resolved='{resolvedByKey}'.";
                return true;
            }

            _lastFlipDecisionDetail =
                $"No match. configured='{configured}', derivedKey='{_lastFlipDerivedOptionKey}', " +
                $"resolvedByDerived='{_lastFlipResolvedByDerivedKey}', resolvedByConfigured='{_lastFlipResolvedByConfiguredKey}'.";
            return false;
        }

        string ResolveOptionForFlip(string currentLayer, string optionKey)
        {
            if (string.IsNullOrEmpty(optionKey))
                return null;

            // First path: regular contract (current layer -> global).
            var resolved = _stateMachine.ResolveOption(currentLayer, optionKey);
            if (!string.IsNullOrEmpty(resolved))
                return resolved;

            // Important:
            // Direction options can be emitted as local options on their own owner layer
            // (e.g. "Movement.*" stored under layer "Movement"), while StateAnimation may be
            // evaluating on a different current layer. In that case, probe the owner layer once.
            if (!TryGetOptionOwnerLayer(optionKey, out var ownerLayer))
                return null;
            if (string.IsNullOrEmpty(ownerLayer) || string.Equals(ownerLayer, currentLayer, StringComparison.Ordinal))
                return null;

            return _stateMachine.GetLocalOption(ownerLayer, optionKey);
        }

        static bool TryGetOptionOwnerLayer(string optionKey, out string ownerLayer)
        {
            ownerLayer = null;
            if (string.IsNullOrEmpty(optionKey))
                return false;

            // OptionKey with nested path (e.g. Movement.Direction) -> owner layer is parent path (Movement).
            if (StateKeyUtils.SplitLayerAndLeaf(optionKey, out var maybeLayer, out _))
            {
                ownerLayer = maybeLayer;
                return !string.IsNullOrEmpty(ownerLayer);
            }

            // Single segment OptionKey (e.g. Movement) -> owner layer is itself.
            ownerLayer = optionKey;
            return true;
        }

        /// <summary>
        /// 現在再生中のアニメーションを停止する�E�ETS キャンセルのみ、軽量版�E�、E
        /// </summary>
        void StopCurrentAnimation()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
        }

        /// <summary>
        /// 現在再生中のアニメーションを完�E停止する�E�Elayer.Stop 含む、Tween 残留防止�E�、E
        /// </summary>
        void StopCurrentAnimationFully()
        {
            // CTS キャンセル
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            _pendingFlipXActive = false;

            // Player の Stop 呼び出し！EOTween 残留防止�E�E
            if (_currentPlayer != null)
            {
                _currentPlayer.Stop();
                _currentPlayer = null;
            }

            BumpTelemetry();
        }

        /// <summary>
        /// リソースを解放する、E
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            StopCurrentAnimationFully();
            BumpTelemetry();
        }

        public StateAnimationTelemetrySnapshot GetTelemetrySnapshot()
        {
            var now = Time.time;
            var pendingElapsed = _pendingFlipXActive
                ? Mathf.Max(0f, now - _pendingFlipXStartTime)
                : 0f;

            var rows = new List<StateAnimationRuleTelemetryRow>(_ruleTelemetryRows);
            return new StateAnimationTelemetrySnapshot(
                version: _telemetryVersion,
                machineRevision: _stateMachine.MachineRevision,
                currentState: _stateMachine.CurrentState ?? string.Empty,
                currentLayer: _stateMachine.CurrentLayer ?? string.Empty,
                profileName: string.Empty,
                evaluationSummary: _lastEvaluationSummary ?? string.Empty,
                activeRuleHeader: _currentRule != null ? _currentRule.RuleHeader : string.Empty,
                activeRulePriority: _currentRule != null ? _currentRule.Priority : 0,
                activeRuleChannelTag: _currentRule != null ? _currentRule.ChannelTag : string.Empty,
                hasCurrentPlayer: _currentPlayer != null,
                hasFlipX: _hasFlipX,
                lastFlipX: _lastFlipX,
                pendingFlipXActive: _pendingFlipXActive,
                pendingFlipXValue: _pendingFlipXValue,
                pendingFlipXElapsedSeconds: pendingElapsed,
                activeRuleApplyFlipX: _currentRule != null && _currentRule.ApplyFlipX,
                activeRuleFlipXTrueOptionValue: _currentRule != null ? _currentRule.FlipXTrueOptionValue : string.Empty,
                flipDecisionDetail: _lastFlipDecisionDetail ?? string.Empty,
                flipConfiguredOptionValue: _lastFlipConfiguredOptionValue ?? string.Empty,
                flipDerivedOptionKey: _lastFlipDerivedOptionKey ?? string.Empty,
                flipResolvedByDerivedKey: _lastFlipResolvedByDerivedKey ?? string.Empty,
                flipResolvedByConfiguredKey: _lastFlipResolvedByConfiguredKey ?? string.Empty,
                rules: rows);
        }

        void BumpTelemetry()
        {
            if (_telemetryVersion == int.MaxValue)
                _telemetryVersion = 1;
            else
                _telemetryVersion++;
        }
    }
}
