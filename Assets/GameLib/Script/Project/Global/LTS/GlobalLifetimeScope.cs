using Game.Platform;

using UnityEngine;
namespace Game
{
    // 騾壼ｸｸ縺ｮ蜻ｼ縺ｳ蜃ｺ縺励ｈ繧翫ｂ譌ｩ縺丞・譛溷喧縺輔ｌ繧九ｈ縺・↓縺ｪ縺｣縺ｦ縺・∪縺吶・(order = -10)
    [RequireComponent(typeof(Game.Commands.CommandRunnerMB))]
    [RequireComponent(typeof(Game.Scalar.BaseScalarMB))]
    [RequireComponent(typeof(Game.Common.EventMB))] // Global Event Service
    public class GlobalLifetimeScope : RuntimeLifetimeScopeBase
    {
        static GlobalLifetimeScope _instance;

        // 蜊碑ｪｿ繝薙Ν繝峨↓蜿ょ刈縺励※隕ｪ・・latformLifetimeScope・峨・螳御ｺ・ｒ蠕・▽
        protected override bool UseBuildCoordinator => true;
        protected override bool IsBuildRoot => true;       // EnsureInScene縺九ｉ閾ｪ蜍輔ン繝ｫ繝・
        protected override bool AutoBuildOnAwake => true; // 
        protected override LifetimeScopeKind RequiredParentKind => LifetimeScopeKind.Platform;

        protected override void Awake()
        {
            if (_instance != null && _instance != this)
            {
                // 菴輔°縺ｮ逅・罰縺ｧ莠碁㍾逕滓・縺輔ｌ縺溷ｴ蜷医・閾ｪ蛻・ｒ豸医☆
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            base.Awake();
        }

        protected override void ConfigureBase(IRuntimeContainerBuilder builder)
        {
            // Global 繧ｹ繧ｳ繝ｼ繝怜崋譛峨・逋ｻ骭ｲ繧偵％縺薙↓譖ｸ縺・
            Game.LTSLog.Log("[GlobalLifetimeScope] Configuring Global scoped services.");
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void EnsureInScene()
        {
            // 隕ｪ繧ｹ繧ｳ繝ｼ繝励ｒ蜈医↓遒ｺ菫・
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

            // Resources/GlobalLifetimeScope.prefab 縺後≠繧後・縺昴ｌ繧剃ｽｿ縺・
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
