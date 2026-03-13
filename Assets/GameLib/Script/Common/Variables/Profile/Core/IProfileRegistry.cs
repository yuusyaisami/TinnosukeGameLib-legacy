// Game.Profile.IProfileRegistry.cs
//
// Profile Registry インターフェース定義

using System.Collections.Generic;
using Game.Save;
using UnityEngine;

namespace Game.Profile
{
    /// <summary>
    /// Profile SO を型ベースで管理するレジストリ。
    /// 各 ProfileSO の型をキーとして解決する。
    /// </summary>
    public interface IProfileRegistry
    {
        /// <summary>
        /// Profile 定義を登録する（SO/SerializeReference 両対応）。
        /// </summary>
        void SetProfileDefinition(IProfileDefinition profile);

        /// <summary>
        /// Profile SO を登録する（非ジェネリック）。
        /// Pool / Template など、型が動的なケース向け。
        /// </summary>
        void SetProfileSO(BaseProfileSO profile);

        /// <summary>
        /// Profile SO を登録する。
        /// 同じ型の既存プロファイルは上書きされる。
        /// </summary>
        /// <typeparam name="T">ProfileSO の型</typeparam>
        /// <param name="profile">登録するプロファイル</param>
        void SetProfileSO<T>(T profile) where T : BaseProfileSO;

        /// <summary>
        /// Profile SO を型で解決する。
        /// </summary>
        /// <typeparam name="T">ProfileSO の型</typeparam>
        /// <param name="profile">解決されたプロファイル</param>
        /// <returns>存在すれば true</returns>
        bool TryResolve<T>(out T profile) where T : BaseProfileSO;

        /// <summary>
        /// 指定した型の Profile SO が登録されているかチェック。
        /// </summary>
        /// <typeparam name="T">ProfileSO の型</typeparam>
        bool HasProfile<T>() where T : BaseProfileSO;

        /// <summary>
        /// Profile SO を取得する。存在しなければ null を返す。
        /// </summary>
        /// <typeparam name="T">ProfileSO の型</typeparam>
        T Resolve<T>() where T : BaseProfileSO;

        /// <summary>
        /// Profile 定義を型で解決する。
        /// </summary>
        bool TryResolveDefinition(System.Type profileType, out IProfileDefinition profile);

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
        IReadOnlyList<ProfileSaveEntry> SaveEntries { get; }

        /// <summary>SaveEntry をクリア（内部用）</summary>
        void ClearSaveEntries();

        /// <summary>SaveEntry を追加（内部用）</summary>
        void AddSaveEntry(ProfileSaveEntry entry);

        /// <summary>バインディング適用状態を設定（内部用）</summary>
        void SetBindingsApplied(bool value);
    }

    /// <summary>
    /// Profile Runtime の具象実装
    /// </summary>
    public sealed class ProfileRuntime<T> : IProfileRuntime where T : BaseProfileSO
    {
        public System.Type ProfileType { get; }
        public IProfileDefinition Profile { get; }
        public T TypedProfile { get; }
        public bool IsBindingsApplied { get; set; }
        public int RegisteredVersion { get; }

        readonly List<ProfileSaveEntry> _saveEntries = new();
        public IReadOnlyList<ProfileSaveEntry> SaveEntries => _saveEntries;

        public ProfileRuntime(T profile, int registeredVersion)
        {
            ProfileType = typeof(T);
            Profile = profile;
            TypedProfile = profile;
            IsBindingsApplied = false;
            RegisteredVersion = registeredVersion;
        }

        /// <summary>
        /// SaveEntry を追加
        /// </summary>
        public void AddSaveEntry(ProfileSaveEntry entry)
        {
            _saveEntries.Add(entry);
        }

        /// <summary>
        /// SaveEntry をクリア
        /// </summary>
        public void ClearSaveEntries()
        {
            _saveEntries.Clear();
        }

        /// <summary>
        /// バインディング適用状態を設定
        /// </summary>
        public void SetBindingsApplied(bool value)
        {
            IsBindingsApplied = value;
        }
    }

    public sealed class ProfileDefinitionRuntime : IProfileRuntime
    {
        public System.Type ProfileType { get; }
        public IProfileDefinition Profile { get; }
        public bool IsBindingsApplied { get; set; }
        public int RegisteredVersion { get; }

        readonly List<ProfileSaveEntry> _saveEntries = new();
        public IReadOnlyList<ProfileSaveEntry> SaveEntries => _saveEntries;

        public ProfileDefinitionRuntime(IProfileDefinition profile, int registeredVersion)
        {
            Profile = profile ?? throw new System.ArgumentNullException(nameof(profile));
            ProfileType = profile.ProfileType ?? profile.GetType();
            RegisteredVersion = registeredVersion;
        }

        public void AddSaveEntry(ProfileSaveEntry entry)
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
