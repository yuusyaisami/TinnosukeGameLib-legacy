// Assets/Game/Script/Core/Identity/LTSIdentityMB.cs
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Sirenix.OdinInspector;
using Game.Commands; // LifetimeScopeKind, CommandTargetIdentityFilter
using Game.DI;
using Game.Search;
using Game.Times;
using System;

namespace Game
{
    public interface ILTSIdentityService
    {
        LifetimeScopeKind Kind { get; }
        string Id { get; }
        string Category { get; }
        bool IsActive { get; set; }
        Transform SelfTransform { get; }

        /// <summary>
        /// DynamicObject として扱うときの「自身のサイズ」。検索半径ではなく、当たり判定の半径。
        /// </summary>
        float Radius { get; }

        /// <summary>
        /// この LTS 配下のコンポーネントが TimeScale の影響を受けるか。
        /// </summary>
        TimeScaleBehavior TimeScaleBehavior { get; }
    }
    public interface IHaveLifetimeScopeKind
    {
        LifetimeScopeKind Kind { get; }
    }

    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class LTSIdentityMB : MonoBehaviour, IFeatureInstaller, IHaveLifetimeScopeKind
    {
        [InfoBox("Scope kind is currently None. The component will periodically attempt to re-detect the lifetime scope until it's resolved.", InfoMessageType.Warning, VisibleIf = nameof(IsKindNone))]
        [LabelText("Scope Kind"), ReadOnly]
        public LifetimeScopeKind kind;
        public LifetimeScopeKind Kind => kind;

        [LabelText("Id")]
        public string id;

        [LabelText("Category")]
        public string category;

        [LabelText("Active")]
        public bool initiallyActive = true;

        [BoxGroup("TimeScale")]
        [LabelText("TimeScale Behavior")]
        [Tooltip("Scaled: Time.timeScale の影響を受ける（DOTween, UniTask, TextAnimator 等）\nUnscaled: Time.timeScale を無視する（ポーズ中も動く UI 等）")]
        public TimeScaleBehavior timeScaleBehavior = TimeScaleBehavior.Scaled;

        [BoxGroup("DynamicObject")]
        [LabelText("Register To Dynamic Registry")]
        [ShowIf(nameof(ShowDynamicRegistryOptions))]
        [SerializeField]
        bool registerToDynamicRegistry = true;

        [BoxGroup("DynamicObject")]
        [LabelText("Radius")]
        [Tooltip("DynamicObject として検索に使う『自身のサイズ』。検索半径ではありません。\n検索時は distance <= searchRadius + targetRadius でヒット判定します。")]
        [ShowIf(nameof(ShowDynamicRadiusOptions))]
        [MinValue(0f)]
        [SerializeField]
        float radius = 0f;


        public bool RegisterToDynamicRegistry => registerToDynamicRegistry;

        public float Radius => radius;

        public bool advanceOptions = false;

        [ShowIf(nameof(advanceOptions))]
        [LabelText("Retry interval (s)"), Tooltip("When the kind is None, the component will attempt to re-guess the kind every N seconds.")]
        [SerializeField]
        float retryIntervalSeconds = 0.5f;

        // Last time we tried to refresh kind. Use realtimeSinceStartup so it works in Edit-mode and Play-mode.
        double _lastRefreshAttemptTime = 0.0;

        // Exposed for Odin's VisibleIf and for convenience in the inspector
        public bool IsKindNone => kind == LifetimeScopeKind.None;
        bool ShowDynamicRegistryOptions => kind == LifetimeScopeKind.Entity || kind == LifetimeScopeKind.Runtime;
        bool ShowDynamicRadiusOptions => registerToDynamicRegistry && ShowDynamicRegistryOptions;

        void Reset()
        {
            kind = GuessKind();
            if (string.IsNullOrEmpty(id))
            {
                // Initialize once on component add.
                id = gameObject.name;
            }
        }

        void OnValidate()
        {
            var guessed = GuessKind();
            if (guessed != LifetimeScopeKind.None)
            {
                kind = guessed;
            }
        }

        void OnEnable()
        {
            if (string.IsNullOrEmpty(id))
            {
                id = gameObject.name;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
            // Ensure we also attempt to resolve when the component is enabled. This covers cases where
            // the LifetimeScope or other components are added after this one.
            // Keep the initial timestamp so the first attempt happens immediately.
            _lastRefreshAttemptTime = 0.0;
        }

        void Update()
        {
            // Only attempt periodic refresh if kind is None. This runs in play mode and (thanks to ExecuteAlways)
            // will also run in the editor for edit-time resolution.
            if (kind != LifetimeScopeKind.None)
                return;

            var now = Time.realtimeSinceStartup;
            if (now - _lastRefreshAttemptTime < retryIntervalSeconds)
                return;

            _lastRefreshAttemptTime = now;

            var changed = TryRefreshKind();
            if (changed)
            {
#if UNITY_EDITOR
                // Persist change to the scene/prefab when editing in the editor.
                try { UnityEditor.EditorUtility.SetDirty(this); } catch { }
#endif
            }
        }

        // Attempts a single refresh of the `kind` using GuessKind. Returns true if `kind` was updated to a non-None value.
        public bool TryRefreshKind()
        {
            var guessed = GuessKind();
            if (guessed != LifetimeScopeKind.None && guessed != kind)
            {
                kind = guessed;
                return true;
            }
            return false;
        }

        // Extracted mapping logic so it's easier to test and to keep `GuessKind` focused
        public static LifetimeScopeKind PredictKindFromType(Type t, LifetimeScopeKind current = LifetimeScopeKind.None)
        {
            if (t == null)
                return current;

            // Prefer explicit type checks in a deterministic order
            if (typeof(ProjectLifetimeScope).IsAssignableFrom(t))
                return LifetimeScopeKind.Project;

            if (typeof(Game.Platform.PlatformLifetimeScope).IsAssignableFrom(t))
                return LifetimeScopeKind.Platform;

            if (typeof(GlobalLifetimeScope).IsAssignableFrom(t))
                return LifetimeScopeKind.Global;

            if (typeof(Game.Scene.SceneLifetimeScope).IsAssignableFrom(t))
                return LifetimeScopeKind.Scene;

            if (typeof(Game.Field.FieldLifetimeScope).IsAssignableFrom(t))
                return LifetimeScopeKind.Field;

            if (typeof(Game.Entity.EntityLifetimeScope).IsAssignableFrom(t))
                return LifetimeScopeKind.Entity;

            if (typeof(Game.UI.UILifetimeScope).IsAssignableFrom(t))
                return LifetimeScopeKind.UI;

            if (typeof(Game.UI.UIElementLifetimeScope).IsAssignableFrom(t))
                return LifetimeScopeKind.UIElement;

            if (typeof(RuntimeLifetimeScope).IsAssignableFrom(t))
                return LifetimeScopeKind.Runtime;

            // Fallback to name-based heuristics (covers third-party or custom lifetime scope names)
            var name = t.Name;
            if (name.IndexOf("Runtime", StringComparison.OrdinalIgnoreCase) >= 0)
                return LifetimeScopeKind.Runtime;
            if (name.IndexOf("Project", StringComparison.OrdinalIgnoreCase) >= 0)
                return LifetimeScopeKind.Project;
            if (name.IndexOf("Platform", StringComparison.OrdinalIgnoreCase) >= 0)
                return LifetimeScopeKind.Platform;
            if (name.IndexOf("Global", StringComparison.OrdinalIgnoreCase) >= 0)
                return LifetimeScopeKind.Global;
            if (name.IndexOf("Scene", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Level", StringComparison.OrdinalIgnoreCase) >= 0)
                return LifetimeScopeKind.Scene;
            if (name.IndexOf("Field", StringComparison.OrdinalIgnoreCase) >= 0)
                return LifetimeScopeKind.Field;
            if (name.IndexOf("Entity", StringComparison.OrdinalIgnoreCase) >= 0)
                return LifetimeScopeKind.Entity;
            if (name.IndexOf("UI", StringComparison.OrdinalIgnoreCase) >= 0)
                return LifetimeScopeKind.UI;
            if (name.IndexOf("Element", StringComparison.OrdinalIgnoreCase) >= 0)
                return LifetimeScopeKind.UIElement;

            // As a last resort, return the current/default kind so we don't surprise consumers
            return current;
        }

        LifetimeScopeKind GuessKind()
        {
            var scope = GetComponent<LifetimeScope>();

            if (scope == null)
            {
                // Runtime scope の可能性
                var runtimeScope = GetComponent<RuntimeLifetimeScope>();
                if (runtimeScope != null)
                {
                    return LifetimeScopeKind.Runtime;
                }
                return kind;
            }

            var t = scope.GetType();

            return PredictKindFromType(t, kind);
        }

        public void InstallFeature(IContainerBuilder builder, IScopeNode owner)
        {
            // Only register this component instance when it is attached to the same GameObject
            // that represents the scope (prevents multiple child LTSIdentityMB components from
            // registering into the same container and causing conflicts).
            if (owner is Component ownerC && ownerC.gameObject == this.gameObject)
            {
                builder.RegisterComponent(this);
            }
            else
            {
                Debug.LogWarning($"{nameof(LTSIdentityMB)} on GameObject '{gameObject.name}' is not on the same GameObject as its owning LifetimeScope; it will not be registered into the container.", this);
            }

            // RuntimeLifetimeScope owns and registers its own RuntimeScopeIdentityService during ConfigureCore,
            // so avoid registering an ILTSIdentityService instance here for runtime scopes.
            if (owner is not RuntimeLifetimeScope)
            {
                builder.Register<LTSIdentityService>(Lifetime.Singleton)
                       .As<ILTSIdentityService>()
                       .AsSelf()
                       .As<IStartable>()  // 登録
                       .As<IDisposable>() // 解除
                       .WithParameter(owner);
            }

            if (registerToDynamicRegistry && (kind == LifetimeScopeKind.Entity || kind == LifetimeScopeKind.Runtime))
            {
                builder.Register<DynamicObjectAutoRegistrar>(Lifetime.Singleton)
                    .As<IScopeAcquireHandler>()
                    .As<IScopeReleaseHandler>()
                    .As<IDisposable>();
            }
        }
    }
}
