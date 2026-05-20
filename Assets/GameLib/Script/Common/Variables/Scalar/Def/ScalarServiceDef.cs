using System;
using System.Collections.Generic;

namespace Game.Scalar
{
    // ================================================================
    // Scalar Value Changed Event
    // ================================================================

    /// <summary>
    /// Scalar 値の変更イベント引数。
    /// </summary>
    public readonly struct ScalarValueChangedArgs
    {
        public readonly ScalarKey Key;
        public readonly float OldValue;
        public readonly float NewValue;

        public ScalarValueChangedArgs(ScalarKey key, float oldValue, float newValue)
        {
            Key = key;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }

    /// <summary>
    /// Scalar 値の変更イベントハンドラ。
    /// </summary>
    public delegate void ScalarValueChangedHandler(ScalarValueChangedArgs args);

    // ================================================================
    // Scalar Service Interfaces
    // ================================================================

    // 各スカラーサービスのインターフェイス定義（新API）
    public interface IProjectScalarService : IBaseScalarService, IScalarTelemetry { }
    public interface IPlatformScalarService : IBaseScalarService, IScalarTelemetry { }
    public interface IGlobalScalarService : IBaseScalarService, IScalarTelemetry { }
    public interface ISceneScalarService : IBaseScalarService, IScalarTelemetry { }
    public interface IEntityScalarService : IBaseScalarService, IScalarTelemetry { }
    public interface IFieldScalarService : IBaseScalarService, IScalarTelemetry { }
    public interface IUIScalarService : IBaseScalarService, IScalarTelemetry { }
    public interface IUIElementScalarService : IBaseScalarService, IScalarTelemetry { }
    public interface IRuntimeScalarService : IBaseScalarService, IScalarTelemetry { }

    public enum ScalarMulPhase
    {
        /// <summary>((Base+LocalBase) * PreMul) のフェーズ。</summary>
        PreAdd = 0,
        /// <summary>((Base+LocalBase+Add) * PostMul) のフェーズ。</summary>
        PostAdd = 1,
    }

    /// <summary>
    /// スカラーの基底値を提供するプロバイダ。
    /// </summary>
    public interface IScalarBaseline
    {
        bool TryGetBase(ScalarKey key, out float value);
    }

    /// <summary>
    /// モディファイアーの共通タグ。
    /// </summary>
    public interface IScalarModifier { }

    public interface IScalarAddModifier : IScalarModifier
    {
        void OnBeforeAdd(ref ScalarAddContext ctx);
    }

    public interface IScalarMulModifier : IScalarModifier
    {
        void OnBeforeMul(ref ScalarMulContext ctx);
    }

    public interface IScalarGetModifier : IScalarModifier
    {
        void OnAfterEvaluate(ref ScalarGetContext ctx);
    }

    /// <summary>
    /// スカラー値評価サービスの共通インターフェイス（新しいモジュラー構成）。
    /// </summary>
    public interface IBaseScalarService
    {
        // ================================================================
        // Value Access
        // ================================================================

        // ローカルのみ
        bool LocalTryGet(ScalarKey key, out float value);
        float LocalGet(ScalarKey key);
        float LocalGet(ScalarKey key, bool includeAllLayers, string layer = null);

        // 親フォールバック込み
        bool GlobalTryGet(ScalarKey key, out float value);
        float GlobalGet(ScalarKey key);
        float GlobalGet(ScalarKey key, bool includeAllLayers, string layer = null);

        // ================================================================
        // Modifiers
        // ================================================================

        /// <summary>
        /// ローカルサービスに対して Add を発行する（親へは影響しない）。
        /// </summary>
        ScalarHandle LocalAdd(
            ScalarKey key,
            string layer,
            float delta,
            float duration = -1f,
            object source = null,
            string tag = null);

        /// <summary>
        /// グローバル Add：ローカルに定義が無い場合は親サービスへフォールバックして Add を発行する。
        /// </summary>
        ScalarHandle GlobalAdd(
            ScalarKey key,
            string layer,
            float delta,
            float duration = -1f,
            object source = null,
            string tag = null);

        /// <summary>
        /// ローカルサービスに対して Mul を発行する（親へは影響しない）。
        /// </summary>
        ScalarHandle LocalMul(
            ScalarKey key,
            string layer,
            float factor,
            ScalarMulPhase phase,
            float duration = -1f,
            object source = null,
            string tag = null);

        /// <summary>
        /// グローバル Mul：ローカルに定義が無い場合は親サービスへフォールバックして Mul を発行する。
        /// </summary>
        ScalarHandle GlobalMul(
            ScalarKey key,
            string layer,
            float factor,
            ScalarMulPhase phase,
            float duration = -1f,
            object source = null,
            string tag = null);

        TMod ResolveMod<TMod>(ScalarKey key) where TMod : class, IScalarModifier;

        void SetLocalBase(ScalarKey key, float value);

        /// <summary>
        /// グローバル SetBase：ローカルに定義が無い場合は親サービスへフォールバックして SetLocalBase を行う。
        /// </summary>
        void SetGlobalBase(ScalarKey key, float value);
        void ClearAll(ScalarKey? key = null);

        // Ensure a ScalarKeyRuntime exists for the specified key using the given runtime config.
        // This allows external code to register a per-key runtime config (baseline/mod flags, clamp, etc.)
        void EnsureRuntime(ScalarKey key, ScalarRuntimeConfig config);

        /// <summary>
        /// Ensure a runtime exists and return it. This allows callers to directly inspect or modify the ScalarKeyRuntime.
        /// </summary>
        ScalarKeyRuntime EnsureAndGetRuntime(ScalarKey key, ScalarRuntimeConfig config);

        /// <summary>
        /// Try to resolve the runtime that exists for the given key.
        /// </summary>
        bool TryGetRuntime(ScalarKey key, out ScalarKeyRuntime runtime);

        /// <summary>
        /// Convenience: Set the baseline value for an existing runtime or create a runtime with default config.
        /// </summary>
        void SetRuntimeBaseline(ScalarKey key, float baseline);

        // ================================================================
        // Value Changed Event
        // ================================================================

        /// <summary>
        /// 指定キーの値変更を購読する。
        /// </summary>
        /// <param name="key">購読するキー</param>
        /// <param name="handler">変更時に呼ばれるハンドラ</param>
        /// <returns>購読解除用のトークン（Dispose で解除）</returns>
        IDisposable LocalSubscribe(ScalarKey key, ScalarValueChangedHandler handler);

        /// <summary>
        /// 指定キーの値変更を親スコープへフォールバックして購読する。
        /// </summary>
        /// <param name="key">購読するキー</param>
        /// <param name="handler">変更時に呼ばれるハンドラ</param>
        /// <returns>購読解除用のトークン（Dispose で解除）</returns>
        IDisposable GlobalSubscribe(ScalarKey key, ScalarValueChangedHandler handler);

        /// <summary>
        /// ローカルサービス内の全キーの値変更を購読する。
        /// </summary>
        /// <param name="handler">変更時に呼ばれるハンドラ</param>
        /// <returns>購読解除用のトークン（Dispose で解除）</returns>
        IDisposable LocalSubscribeAll(ScalarValueChangedHandler handler);
    }

    /// <summary>
    /// Add / Mul で生成されたエントリを保持するハンドル。Dispose で削除される。
    /// </summary>
    public sealed class ScalarHandle : IDisposable
    {
        internal Guid Id { get; }
        ScalarKeyRuntime _owner;

        internal ScalarHandle(ScalarKeyRuntime owner, Guid id)
        {
            _owner = owner;
            Id = id;
            _owner?.RegisterHandle(this);
        }

        public bool IsValid => _owner != null;

        public void Dispose()
        {
            var owner = _owner;
            if (owner == null)
                return;

            _owner = null;
            owner.RemoveHandle(this);
        }

        internal void Invalidate()
        {
            _owner = null;
        }

        /// <summary>
        /// このハンドルに紐づく値を更新する。
        /// </summary>
        public void SetValue(float value)
        {
            _owner?.SetHandleValue(Id, value);
        }
    }
}
