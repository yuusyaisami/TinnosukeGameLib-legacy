#nullable enable
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Game.NoiseProducer
{
    [Serializable]
    public sealed class NoiseChannelDefinition
    {
        [FoldoutGroup("Channel")]
        [SerializeField] string _channelId = string.Empty;

        [FoldoutGroup("Channel")]
        [SerializeField] string _outputTag = string.Empty;

        [FoldoutGroup("Channel/Output")]
        [SerializeField] Vector2Int _resolution = new(256, 256);

        [FoldoutGroup("Channel/Output")]
        [SerializeField] GraphicsFormat _format = GraphicsFormat.R8G8B8A8_UNorm;

        [FoldoutGroup("Channel/Output")]
        [SerializeField] FilterMode _filterMode = FilterMode.Bilinear;

        [FoldoutGroup("Channel/Output")]
        [SerializeField] TextureWrapMode _wrapMode = TextureWrapMode.Repeat;

        [FoldoutGroup("Channel/Output")]
        [SerializeField] Color _clearColor = Color.black;

        [FoldoutGroup("Channel/Output")]
        [SerializeField] bool _autoPublish = true;

        [FoldoutGroup("Channel/Time")]
        [SerializeField] float _timeScale = 1f;

        [FoldoutGroup("Channel/Time")]
        [Tooltip("0 以下でループ無効")]
        [SerializeField] float _loopSeconds;

        [FoldoutGroup("Channel")]
        [SerializeField] int _seed;

        [FoldoutGroup("Stages")]
        [ListDrawerSettings(ShowFoldout = true)]
        [SerializeField] List<NoiseStageDefinition> _stages = new();

        [FoldoutGroup("Parameters")]
        [ListDrawerSettings(ShowFoldout = true)]
        [SerializeField] List<NoiseParameterDefinition> _parameters = new();

        // ── Accessors ───────────────────────────────────────────

        public string ChannelId => _channelId;
        public string OutputTag => _outputTag;
        public Vector2Int Resolution => _resolution;
        public GraphicsFormat Format => _format;
        public FilterMode FilterMode => _filterMode;
        public TextureWrapMode WrapMode => _wrapMode;
        public Color ClearColor => _clearColor;
        public bool AutoPublish => _autoPublish;
        public float TimeScale => _timeScale;
        public float LoopSeconds => _loopSeconds;
        public int Seed => _seed;
        public IReadOnlyList<NoiseStageDefinition> Stages => _stages;
        public IReadOnlyList<NoiseParameterDefinition> Parameters => _parameters;

        // ── Validation ──────────────────────────────────────────

        public Vector2Int ClampedResolution
            => new(
                Mathf.Clamp(_resolution.x, 1, 2048),
                Mathf.Clamp(_resolution.y, 1, 2048));

        public bool HasTimeReactiveStage
        {
            get
            {
                for (int i = 0; i < _stages.Count; i++)
                {
                    if (_stages[i].Enabled && _stages[i].IsTimeReactive)
                        return true;
                }
                return false;
            }
        }
    }
}
