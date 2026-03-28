#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using VContainer;

namespace Game.Channel
{
    public sealed class Light2DChannelPresetRuntime :
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        readonly Light2DChannelDef _definition;

        Light2DPreset _baseSourcePreset = new();
        Light2DPreset _currentSourcePreset = new();
        Light2DPlayerPreset _basePlayerPreset = new();
        Light2DPlayerPreset _currentPlayerPreset = new();
        List<Light2DEffectEntry> _baseEffects = new();
        List<Light2DEffectEntry> _currentEffects = new();

        public Light2DPlayerPreset CurrentPlayerPreset => _currentPlayerPreset;
        public IReadOnlyList<Light2DEffectEntry> CurrentEffects => _currentEffects;

        public event Action? OnPlayerPresetChanged;
        public event Action? OnEffectsChanged;

        public Light2DChannelPresetRuntime(Light2DChannelDef definition)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = isReset;

            var vars = ResolveVars(scope);
            var context = new SimpleDynamicContext(vars, scope);
            var resolved = _definition.SourcePreset.GetOrDefault(context, new Light2DPreset());
            ApplyResolvedSourcePreset(resolved?.CreateRuntimeCopy() ?? new Light2DPreset());
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;

            _baseSourcePreset = new Light2DPreset();
            _currentSourcePreset = new Light2DPreset();
            _basePlayerPreset = new Light2DPlayerPreset();
            _currentPlayerPreset = new Light2DPlayerPreset();
            _baseEffects = new List<Light2DEffectEntry>();
            _currentEffects = new List<Light2DEffectEntry>();
        }

        public bool SwapSourcePreset(Light2DPreset? preset)
        {
            if (preset == null)
                return false;

            ApplyResolvedSourcePreset(preset.CreateRuntimeCopy());
            OnPlayerPresetChanged?.Invoke();
            OnEffectsChanged?.Invoke();
            return true;
        }

        public bool SwapPlayerPreset(Light2DPlayerPreset? preset)
        {
            if (preset == null)
                return false;

            _basePlayerPreset = preset.CreateRuntimeCopy();
            _currentPlayerPreset = _basePlayerPreset.CreateRuntimeCopy();
            OnPlayerPresetChanged?.Invoke();
            return true;
        }

        public bool MutatePlayerPreset(Light2DPlayerRuntimeMutation? mutation)
        {
            if (mutation == null || !mutation.HasAnyMutation())
                return false;

            _currentPlayerPreset.ApplyMutation(mutation);
            OnPlayerPresetChanged?.Invoke();
            return true;
        }

        public bool ReplaceEffect(
            string effectId,
            Light2DEffectPresetBase? preset,
            int priority,
            Light2DEffectBlendMode blendMode,
            bool enabled)
        {
            if (preset == null)
                return false;

            var normalizedId = NormalizeEffectId(effectId);
            ReplaceEffectInList(_baseEffects, normalizedId, preset, priority, blendMode, enabled);
            ReplaceEffectInList(_currentEffects, normalizedId, preset, priority, blendMode, enabled);
            NormalizeOrders(_baseEffects);
            NormalizeOrders(_currentEffects);
            OnEffectsChanged?.Invoke();
            return true;
        }

        public bool MutateEffect(string effectId, Light2DEffectRuntimeMutationBase? mutation)
        {
            if (mutation == null || !mutation.HasAnyMutation())
                return false;

            if (!TryGetEffect(_currentEffects, effectId, out var effect) || effect?.Preset == null)
                return false;

            if (!effect.Preset.ApplyMutation(mutation))
                return false;

            OnEffectsChanged?.Invoke();
            return true;
        }

        public bool SetEffectEnabled(string effectId, bool enabled)
        {
            if (!TryGetEffect(_currentEffects, effectId, out var effect) || effect == null)
                return false;

            effect.SetEnabled(enabled);
            OnEffectsChanged?.Invoke();
            return true;
        }

        public bool RemoveEffect(string effectId)
        {
            var removed = RemoveEffectFromList(_currentEffects, effectId);
            if (!removed)
                return false;

            NormalizeOrders(_currentEffects);
            OnEffectsChanged?.Invoke();
            return true;
        }

        public bool ResetRuntimeOverrides(bool resetPlayerPreset, bool resetEffects)
        {
            var changed = false;

            if (resetPlayerPreset)
            {
                _currentPlayerPreset = _basePlayerPreset.CreateRuntimeCopy();
                changed = true;
                OnPlayerPresetChanged?.Invoke();
            }

            if (resetEffects)
            {
                _currentEffects = CloneEffects(_baseEffects);
                NormalizeOrders(_currentEffects);
                changed = true;
                OnEffectsChanged?.Invoke();
            }

            return changed;
        }

        public void Tick(float deltaTime)
        {
            if (_currentEffects.Count == 0)
                return;

            for (var i = 0; i < _currentEffects.Count; i++)
                _currentEffects[i].ElapsedTime += deltaTime;
        }

        void ApplyResolvedSourcePreset(Light2DPreset preset)
        {
            _baseSourcePreset = preset.CreateRuntimeCopy();
            _currentSourcePreset = _baseSourcePreset.CreateRuntimeCopy();
            _basePlayerPreset = _baseSourcePreset.PlayerPreset.CreateRuntimeCopy();
            _currentPlayerPreset = _basePlayerPreset.CreateRuntimeCopy();
            _baseEffects = CloneEffects(_baseSourcePreset.DefaultEffects);
            _currentEffects = CloneEffects(_baseEffects);
            NormalizeOrders(_baseEffects);
            NormalizeOrders(_currentEffects);
        }

        static void ReplaceEffectInList(
            List<Light2DEffectEntry> effects,
            string effectId,
            Light2DEffectPresetBase preset,
            int priority,
            Light2DEffectBlendMode blendMode,
            bool enabled)
        {
            if (TryGetEffect(effects, effectId, out var existing) && existing != null)
            {
                existing.SetRuntimeValues(effectId, preset, priority, blendMode, enabled);
                return;
            }

            var entry = new Light2DEffectEntry();
            entry.SetRuntimeValues(effectId, preset, priority, blendMode, enabled);
            effects.Add(entry);
        }

        static bool RemoveEffectFromList(List<Light2DEffectEntry> effects, string effectId)
        {
            var normalizedId = NormalizeEffectId(effectId);
            for (var i = effects.Count - 1; i >= 0; i--)
            {
                if (!string.Equals(effects[i].EffectId, normalizedId, StringComparison.Ordinal))
                    continue;

                effects.RemoveAt(i);
                return true;
            }

            return false;
        }

        static bool TryGetEffect(List<Light2DEffectEntry> effects, string effectId, out Light2DEffectEntry? effect)
        {
            var normalizedId = NormalizeEffectId(effectId);
            for (var i = 0; i < effects.Count; i++)
            {
                var candidate = effects[i];
                if (!string.Equals(candidate.EffectId, normalizedId, StringComparison.Ordinal))
                    continue;

                effect = candidate;
                return true;
            }

            effect = null;
            return false;
        }

        static List<Light2DEffectEntry> CloneEffects(IReadOnlyList<Light2DEffectEntry> source)
        {
            var result = new List<Light2DEffectEntry>(source.Count);
            for (var i = 0; i < source.Count; i++)
            {
                var entry = source[i];
                if (entry == null)
                    continue;

                var clone = entry.CreateRuntimeCopy();
                clone.Order = result.Count;
                result.Add(clone);
            }

            return result;
        }

        static void NormalizeOrders(List<Light2DEffectEntry> effects)
        {
            for (var i = 0; i < effects.Count; i++)
                effects[i].Order = i;
        }

        static IVarStore ResolveVars(IScopeNode scope)
        {
            if (scope?.Resolver != null &&
                scope.Resolver.TryResolve<IVarStore>(out var vars) &&
                vars != null)
            {
                return vars;
            }

            return NullVarStore.Instance;
        }

        static string NormalizeEffectId(string effectId)
        {
            return string.IsNullOrWhiteSpace(effectId) ? "default" : effectId.Trim();
        }
    }
}
