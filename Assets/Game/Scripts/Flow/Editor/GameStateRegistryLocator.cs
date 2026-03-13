#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Actions.Editor
{
    /// <summary>
    /// GameStateRegistry / GameStateSettings を取得／生成するヘルパー。
    /// </summary>
    public static class GameStateRegistryLocator
    {
        public const string DefaultRegistryPath = "Assets/Game/ScriptableObjects/Flow/GameStateRegistry.asset";
        public const string DefaultSettingsPath = "Assets/Game/Config/GameStateSettings.asset";

        static GameStateRegistry _cachedRegistry;
        static GameStateSettings _cachedSettings;

        public static GameStateRegistry GetOrCreate()
        {
            if (_cachedRegistry != null)
                return _cachedRegistry;

            var guids = AssetDatabase.FindAssets("t:GameStateRegistry");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _cachedRegistry = AssetDatabase.LoadAssetAtPath<GameStateRegistry>(path);
            }

            if (_cachedRegistry == null)
            {
                _cachedRegistry = ScriptableObject.CreateInstance<GameStateRegistry>();
                EnsureDirectoryExists(DefaultRegistryPath);
                AssetDatabase.CreateAsset(_cachedRegistry, DefaultRegistryPath);
                PopulateDefaultsIfEmpty(_cachedRegistry);
                AssetDatabase.SaveAssets();
                Debug.Log($"[GameStateRegistryLocator] Created: {DefaultRegistryPath}");
            }

            return _cachedRegistry;
        }

        public static GameStateSettings GetOrCreateSettings()
        {
            if (_cachedSettings != null)
                return _cachedSettings;

            var guids = AssetDatabase.FindAssets("t:GameStateSettings");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _cachedSettings = AssetDatabase.LoadAssetAtPath<GameStateSettings>(path);
            }

            if (_cachedSettings == null)
            {
                _cachedSettings = ScriptableObject.CreateInstance<GameStateSettings>();
                EnsureDirectoryExists(DefaultSettingsPath);
                AssetDatabase.CreateAsset(_cachedSettings, DefaultSettingsPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[GameStateRegistryLocator] Created Settings: {DefaultSettingsPath}");
            }

            return _cachedSettings;
        }

        static void PopulateDefaultsIfEmpty(GameStateRegistry registry)
        {
            if (registry == null || registry.Count > 0)
                return;

            var defaultNode = registry.CreateLeaf(string.Empty, "Default");
            defaultNode.StateId = 0;
            defaultNode.Description = "初期状態";

            var map = registry.CreateLeaf(string.Empty, "MapSelection");
            map.Description = "マップ選択画面";

            var entry = registry.CreateLeaf(string.Empty, "GameEntry");
            entry.Description = "ゲーム開始の演出";

            var select = registry.CreateLeaf(string.Empty, "PlayerActionSelection");
            select.Description = "プレイヤーのアクション選択";

            var exec = registry.CreateLeaf(string.Empty, "PlayerActionExecution");
            exec.Description = "プレイヤーのアクション実行フェーズ";

            var enemy = registry.CreateLeaf(string.Empty, "EnemyActionExecution");
            enemy.Description = "敵のアクション実行フェーズ";

            var resolution = registry.CreateLeaf(string.Empty, "ActionResolution");
            resolution.Description = "アクションの解決フェーズ";

            var gameOver = registry.CreateLeaf(string.Empty, "GameOver");
            gameOver.Description = "ゲームオーバー画面";

            var victory = registry.CreateLeaf(string.Empty, "Victory");
            victory.Description = "勝利画面";

            var countdown = registry.CreateLeaf(string.Empty, "StartCountdown");
            countdown.Description = "ゲーム開始カウントダウン";

            EditorUtility.SetDirty(registry);
        }

        static void EnsureDirectoryExists(string assetPath)
        {
            var dir = Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}
#endif
