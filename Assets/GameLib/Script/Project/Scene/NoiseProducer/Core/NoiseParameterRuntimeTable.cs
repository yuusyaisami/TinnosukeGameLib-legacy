#nullable enable
using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Game.NoiseProducer
{
    /// <summary>
    /// parameterKey 単位で layerTag ごとの値を管理するランタイムテーブル。
    /// last-writer-wins 方式で最終値を解決する。
    /// </summary>
    public sealed class NoiseParameterRuntimeTable
    {
        sealed class ParameterEntry
        {
            public readonly NoiseParameterDefinition Definition;
            public NoiseParameterValue CurrentValue;
            public readonly Dictionary<string, LayerEntry> Layers = new(StringComparer.Ordinal);
            public bool Dirty;

            public ParameterEntry(NoiseParameterDefinition definition)
            {
                Definition = definition;
                CurrentValue = definition.GetDefaultValue();
                Dirty = true;
            }
        }

        sealed class LayerEntry
        {
            public readonly string LayerTag;
            public NoiseParameterValue Value;
            public Tween? Tween;

            public LayerEntry(string layerTag, NoiseParameterValue value)
            {
                LayerTag = layerTag;
                Value = value;
            }
        }

        readonly Dictionary<string, ParameterEntry> _entries = new(StringComparer.Ordinal);

        public int Count => _entries.Count;

        public bool HasAnimating
        {
            get
            {
                foreach (var entry in _entries.Values)
                {
                    foreach (var layer in entry.Layers.Values)
                    {
                        if (layer.Tween is { active: true })
                            return true;
                    }
                }
                return false;
            }
        }

        // ── Setup ───────────────────────────────────────────────

        public void Initialize(IReadOnlyList<NoiseParameterDefinition> definitions)
        {
            Clear();
            for (int i = 0; i < definitions.Count; i++)
            {
                var def = definitions[i];
                if (string.IsNullOrEmpty(def.ParameterKey)) continue;
                _entries[def.ParameterKey] = new ParameterEntry(def);
            }
        }

        public void Clear()
        {
            foreach (var entry in _entries.Values)
            {
                foreach (var layer in entry.Layers.Values)
                {
                    layer.Tween?.Kill();
                    layer.Tween = null;
                }
                entry.Layers.Clear();
            }
            _entries.Clear();
        }

        // ── Write ───────────────────────────────────────────────

        public bool TryWrite(in NoiseParameterWriteRequest request)
        {
            if (!_entries.TryGetValue(request.Address.ParameterKey, out var entry))
                return false;

            var layerTag = request.Address.LayerTag;
            if (string.IsNullOrEmpty(layerTag))
                return false;

            if (!entry.Layers.TryGetValue(layerTag, out var layer))
            {
                layer = new LayerEntry(layerTag, request.Value);
                entry.Layers[layerTag] = layer;
            }

            // Kill existing tween
            if (layer.Tween != null)
            {
                layer.Tween.Kill();
                layer.Tween = null;
            }

            if (request.Duration <= 0f)
            {
                layer.Value = request.Value;
                entry.Dirty = true;
                ResolveValue(entry);
                return true;
            }

            // Animated write
            var targetValue = request.Value;
            var ease = request.Ease == Ease.Unset ? Ease.Linear : request.Ease;

            switch (entry.Definition.ValueKind)
            {
                case NoiseParameterValueKind.Float:
                    {
                        var capturedLayer = layer;
                        var capturedEntry = entry;
                        capturedLayer.Tween = DOTween
                            .To(() => capturedLayer.Value.FloatValue,
                                v =>
                                {
                                    capturedLayer.Value = NoiseParameterValue.Float(v);
                                    capturedEntry.Dirty = true;
                                },
                                targetValue.FloatValue,
                                request.Duration)
                            .SetEase(ease)
                            .OnComplete(() =>
                            {
                                capturedLayer.Tween = null;
                                ResolveValue(capturedEntry);
                            });
                        break;
                    }
                case NoiseParameterValueKind.Vector2:
                    {
                        var capturedLayer = layer;
                        var capturedEntry = entry;
                        var from = capturedLayer.Value.Vector2Value;
                        var to = targetValue.Vector2Value;
                        capturedLayer.Tween = DOTween
                            .To(() => 0f, t =>
                            {
                                var v = Vector2.Lerp(from, to, t);
                                capturedLayer.Value = NoiseParameterValue.Vec2(v);
                                capturedEntry.Dirty = true;
                            }, 1f, request.Duration)
                            .SetEase(ease)
                            .OnComplete(() =>
                            {
                                capturedLayer.Value = targetValue;
                                capturedLayer.Tween = null;
                                ResolveValue(capturedEntry);
                            });
                        break;
                    }
                case NoiseParameterValueKind.Color:
                    {
                        var capturedLayer = layer;
                        var capturedEntry = entry;
                        var from = capturedLayer.Value.ColorValue;
                        var to = targetValue.ColorValue;
                        capturedLayer.Tween = DOTween
                            .To(() => 0f, t =>
                            {
                                var c = Color.Lerp(from, to, t);
                                capturedLayer.Value = NoiseParameterValue.Col(c);
                                capturedEntry.Dirty = true;
                            }, 1f, request.Duration)
                            .SetEase(ease)
                            .OnComplete(() =>
                            {
                                capturedLayer.Value = targetValue;
                                capturedLayer.Tween = null;
                                ResolveValue(capturedEntry);
                            });
                        break;
                    }
                default:
                    layer.Value = targetValue;
                    entry.Dirty = true;
                    break;
            }

            ResolveValue(entry);
            return true;
        }

        public bool ClearLayer(in NoiseParameterAddress address)
        {
            if (!_entries.TryGetValue(address.ParameterKey, out var entry))
                return false;

            if (!entry.Layers.TryGetValue(address.LayerTag, out var layer))
                return false;

            layer.Tween?.Kill();
            layer.Tween = null;
            entry.Layers.Remove(address.LayerTag);
            entry.Dirty = true;
            ResolveValue(entry);
            return true;
        }

        // ── Read ────────────────────────────────────────────────

        public bool TryGetValue(string parameterKey, out NoiseParameterValue value)
        {
            if (_entries.TryGetValue(parameterKey, out var entry))
            {
                value = entry.CurrentValue;
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>dirty フラグを消費して、1 つでも dirty だったかを返す。</summary>
        public bool ConsumeAnyDirty()
        {
            bool any = false;
            foreach (var entry in _entries.Values)
            {
                if (entry.Dirty)
                {
                    any = true;
                    entry.Dirty = false;
                }
            }
            return any;
        }

        // ── Internal ────────────────────────────────────────────

        /// <summary>last-writer-wins で最終値を解決する。</summary>
        static void ResolveValue(ParameterEntry entry)
        {
            if (entry.Layers.Count == 0)
            {
                entry.CurrentValue = entry.Definition.GetDefaultValue();
                return;
            }

            // last-writer-wins: Dictionary の最後の要素を取る
            NoiseParameterValue resolved = entry.Definition.GetDefaultValue();
            foreach (var layer in entry.Layers.Values)
            {
                resolved = layer.Value;
            }
            entry.CurrentValue = resolved;
        }
    }
}
