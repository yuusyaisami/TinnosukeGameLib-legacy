using Game.Platform;
using VContainer;
using VContainer.Unity;

using UnityEngine;
namespace Game
{
    // 通常の呼び出しよりも早く初期化されるようになっています。 (order = -10)
    [RequireComponent(typeof(Game.Commands.CommandRunnerMB))]
    [RequireComponent(typeof(Game.Scalar.BaseScalarMB))]
    [RequireComponent(typeof(Game.Common.EventMB))] // Global Event Service
    public class GlobalLifetimeScope : BaseLifetimeScope<PlatformLifetimeScope>
    {
        static GlobalLifetimeScope _instance;

        // 協調ビルドに参加して親（PlatformLifetimeScope）の完了を待つ
        protected override bool UseBuildCoordinator => true;
        protected override bool IsBuildRoot => true;       // EnsureInSceneから自動ビルド
        protected override bool AutoBuildOnAwake => true; // 

        protected override void Awake()
        {
            if (_instance != null && _instance != this)
            {
                // 何かの理由で二重生成された場合は自分を消す
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            base.Awake();
        }

        protected override void ConfigureBase(IContainerBuilder builder)
        {
            // Global スコープ固有の登録をここに書く
            Game.LTSLog.Log("[GlobalLifetimeScope] Configuring Global scoped services.");
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void EnsureInScene()
        {
            // 親スコープを先に確保
            ProjectLifetimeScope.EnsureExists();

            if (_instance != null)
                return;

            // If a GlobalLifetimeScope already exists in the scene, don't create another.
            var existing = UnityEngine.Object.FindObjectsByType<GlobalLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
            if (existing != null && existing.Length > 0)
                return;

            // Resources/GlobalLifetimeScope.prefab があればそれを使う
            var prefab = Resources.Load<GameObject>("Prefab/Global/GlobalLifetimeScope");
            if (prefab != null)
            {
                UnityEngine.Object.Instantiate(prefab);
                return;
            }

            var go = new GameObject("GlobalLifetimeScope");
            go.AddComponent<GlobalLifetimeScope>();
        }

    }
}
