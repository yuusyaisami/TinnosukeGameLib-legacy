// Game.Profile.IScopeBindingRegistry.cs
//
// Profile Registry インターフェース定義

using System;
using System.Collections.Generic;
using Game.Save;
using UnityEngine;

namespace Game.Profile
{
    /// <summary>
    /// Profile 定義を型ベースで管理するレジストリ。
    /// IProfileDefinition の ProfileType をキーとして登録・解決する。
    /// </summary>
    public interface IScopeBindingRegistry
    {
        // ================================================================
        // Definition-centric API (推奨)
        // ================================================================

        /// <summary>
        /// Profile 定義を登録する（SO/SerializeReference 両対応）。
        /// </summary>
        void SetProfileDefinition(IProfileDefinition profile);

        /// <summary>
        /// 指定した ProfileType の定義が登録されているかチェック。
        /// </summary>
        bool HasDefinition(Type profileType);

        /// <summary>
        /// Profile 定義を型で解決する（非ジェネリック）。
        /// </summary>
        bool TryResolveDefinition(Type profileType, out IProfileDefinition profile);

        /// <summary>
        /// Profile 定義をジェネリックに解決する。
        /// typeof(T) をキーとして登録された IProfileDefinition を T にキャストして返す。
        /// </summary>
        bool TryResolveDefinition<T>(out T profile) where T : class, IProfileDefinition;

        /// <summary>
        /// レジストリのバージョン。Profile が追加/更新されるたびにインクリメントされる。
        /// 変更検知用に使用。
        /// </summary>
        int Version { get; }
    }

    /// <summary>
    /// Profile に対する Runtime 情報。
    /// バインディングの適用済み状態などを追跡。
    /// </summary>
    public interface IProfileRuntime
    {
        /// <summary>プロファイルの型</summary>
        System.Type ProfileType { get; }

        /// <summary>プロファイルインスタンス</summary>
        IProfileDefinition Profile { get; }

        /// <summary>バインディングが適用済みかどうか</summary>
        bool IsBindingsApplied { get; }

        /// <summary>登録時のバージョン</summary>
        int RegisteredVersion { get; }

        /// <summary>収集された Save エントリ</summary>
        IReadOnlyList<BindingSaveEntry> SaveEntries { get; }

        /// <summary>SaveEntry をクリア（内部用）</summary>
        void ClearSaveEntries();

        /// <summary>SaveEntry を追加（内部用）</summary>
        void AddSaveEntry(BindingSaveEntry entry);

        /// <summary>バインディング適用状態を設定（内部用）</summary>
        void SetBindingsApplied(bool value);
    }

    public sealed class ProfileDefinitionRuntime : IProfileRuntime
    {
        public System.Type ProfileType { get; }
        public IProfileDefinition Profile { get; }
        public bool IsBindingsApplied { get; set; }
        public int RegisteredVersion { get; }

        readonly List<BindingSaveEntry> _saveEntries = new();
        public IReadOnlyList<BindingSaveEntry> SaveEntries => _saveEntries;

        public ProfileDefinitionRuntime(IProfileDefinition profile, int registeredVersion)
        {
            Profile = profile ?? throw new System.ArgumentNullException(nameof(profile));
            ProfileType = profile.ProfileType ?? profile.GetType();
            RegisteredVersion = registeredVersion;
        }

        public void AddSaveEntry(BindingSaveEntry entry)
        {
            _saveEntries.Add(entry);
        }

        public void ClearSaveEntries()
        {
            _saveEntries.Clear();
        }

        public void SetBindingsApplied(bool value)
        {
            IsBindingsApplied = value;
        }
    }
}
