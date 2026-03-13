#nullable enable
// Game.Movement
// ================================================================================
// MotionPreset / MotionRuntime - Motion の定義と実行時インスタンス
// ================================================================================
//
// 【概要】
// Motion の基底クラス。派生クラスで波・弧・螺旋などを実装する。
// MotionPreset は設定、MotionRuntime は実行時状態を保持。
//
// 【設計】
// - MotionPreset: SerializeReference でインライン/アセット両対応
// - MotionRuntime: 実行時状態（経過時間など）を保持
// - CreateRuntime() で Preset から Runtime を生成
// ================================================================================

using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Movement
{
    /// <summary>
    /// Motion の定義基底クラス。派生で波・弧・螺旋等を実装。
    /// </summary>
    [Serializable]
    public abstract class MotionPreset
    {
        [Header("Identification")]
        [LabelText("Stable Key")]
        [Tooltip("永続ID。ログ・デバッグ用")]
        public string StableKey = "";

        /// <summary>Runtime を生成</summary>
        public abstract MotionRuntime CreateRuntime();

        /// <summary>Stable Key を取得（空なら型名）</summary>
        public string GetStableKey() => string.IsNullOrEmpty(StableKey) ? GetType().Name : StableKey;

        public override string ToString() => GetStableKey();
    }

    /// <summary>
    /// MotionPreset をアセットとして保持する SO。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Movement/Motion Preset", fileName = "MotionPreset")]
    public sealed class MotionPresetAssetSO : ScriptableObject
    {
        [SerializeReference, InlineProperty, HideLabel]
        public MotionPreset? preset;

        public MotionPreset? Preset => preset;
    }

    /// <summary>
    /// Motion の実行時インスタンス。
    /// </summary>
    public abstract class MotionRuntime
    {
        MotionPreset? _source;
        float _elapsedTime;
        bool _initialized;

        /// <summary>ソース Preset</summary>
        public MotionPreset? Source => _source;

        /// <summary>経過時間</summary>
        public float ElapsedTime => _elapsedTime;

        /// <summary>初期化済みか</summary>
        public bool IsInitialized => _initialized;

        /// <summary>初期化</summary>
        internal void Initialize(MotionPreset source)
        {
            _source = source;
            _elapsedTime = 0f;
            _initialized = true;
            OnInitialize();
        }

        /// <summary>更新</summary>
        internal MotionOutput Tick(in MovementGuidanceFrame frame)
        {
            _elapsedTime += frame.DeltaTime;
            return OnTick(frame);
        }

        /// <summary>リセット（経過時間のみ）</summary>
        internal void ResetTime()
        {
            _elapsedTime = 0f;
            OnReset();
        }

        /// <summary>完全リセット</summary>
        internal void Clear()
        {
            _source = null;
            _elapsedTime = 0f;
            _initialized = false;
            OnClear();
        }

        // ================================================================
        // 派生でオーバーライド
        // ================================================================

        /// <summary>初期化時に呼ばれる</summary>
        protected virtual void OnInitialize() { }

        /// <summary>時間リセット時に呼ばれる</summary>
        protected virtual void OnReset() { }

        /// <summary>完全クリア時に呼ばれる</summary>
        protected virtual void OnClear() { }

        /// <summary>毎 Tick 呼ばれる。MotionOutput を返す</summary>
        protected abstract MotionOutput OnTick(in MovementGuidanceFrame frame);
    }
}
