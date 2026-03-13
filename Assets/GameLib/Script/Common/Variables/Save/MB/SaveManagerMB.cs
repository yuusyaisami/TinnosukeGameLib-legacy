using System;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Save
{
    /// <summary>
    /// Registers SaveSystem v2 implementations into the current LifetimeScope.
    /// </summary>
    public sealed class SaveManagerMB : MonoBehaviour, IFeatureInstaller
    {
        [SerializeField]
        SaveManagerDebugView _debug = new SaveManagerDebugView();

        [ShowInInspector, ReadOnly]
        [FoldoutGroup("Debug Actions")]
        string DeleteAllSaveDataStatus { get; set; } = "(idle)";

        [NonSerialized]
        ISaveManager? _saveManager;

        bool IsDeleteActionAvailable
        {
            get
            {
#if UNITY_EDITOR
                return true;
#else
                return _saveManager != null;
#endif
            }
        }

        public bool DeleteAllSaveDataBeforeBuild => _debug != null && _debug.DeleteAllSaveDataBeforeBuild;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            // WebGLではFile I/Oの永続化タイミングが不安定になりやすいので、PlayerPrefsベースへ切替
#if UNITY_WEBGL && !UNITY_EDITOR
            builder.Register<PlayerPrefsSaveStore>(_ => new PlayerPrefsSaveStore(), Lifetime.Singleton)
                .As<ISaveStore>();

            builder.Register<WebGLSavePlatform>(_ => new WebGLSavePlatform(), Lifetime.Singleton)
                .As<ISavePlatform>();
#else
            builder.Register<FileSaveStore>(_ => new FileSaveStore(string.Empty), Lifetime.Singleton)
                .As<ISaveStore>();

            builder.Register<UnitySavePlatform>(_ => new UnitySavePlatform(), Lifetime.Singleton)
                .As<ISavePlatform>();
#endif

            builder.Register<UnityJsonSaveSerializer>(Lifetime.Singleton)
                .As<ISaveSerializer>();

            builder.Register<UnitySaveLogger>(Lifetime.Singleton)
                .As<ISaveLogger>();

            builder.Register<UnitySaveThreadGuard>(Lifetime.Singleton)
                .As<ISaveThreadGuard>();

            builder.Register<SaveBinderV2>(Lifetime.Singleton)
                .As<ISaveBinder>();

            builder.Register<DefaultSaveLayerPolicy>(Lifetime.Singleton)
                .As<ISaveLayerPolicy>();

            builder.Register<DefaultSaveBackupPolicy>(Lifetime.Singleton)
                .As<ISaveBackupPolicy>();

            builder.Register<SaveManager>(Lifetime.Singleton)
                .As<ISaveManager>();

            builder.RegisterInstance(_debug);
            builder.RegisterBuildCallback(container =>
            {
                if (!container.TryResolve<ISaveManager>(out var save))
                    return;
                _saveManager = save;
                if (!container.TryResolve<ISaveStore>(out var store))
                    return;
                if (!container.TryResolve<ISavePlatform>(out var platform))
                    return;
                if (!container.TryResolve<ISaveSerializer>(out var serializer))
                    return;

                _debug.Initialize(save, store, platform, serializer, this);
                _debug.RefreshNow();
            });
        }

        [FoldoutGroup("Debug Actions")]
        [Button(ButtonSizes.Large)]
        [GUIColor(0.85f, 0.35f, 0.35f)]
        [EnableIf(nameof(IsDeleteActionAvailable))]
        void DeleteAllPersistedSaveData()
        {
            if (_saveManager == null)
            {
                if (SaveManagerDebugView.TryDeletePersistedDataFromEditor(out var editorStatus))
                {
                    DeleteAllSaveDataStatus = editorStatus;
                    _debug.RefreshNow();
                    Debug.Log("[SaveManagerMB] Deleted persisted save data in editor.");
                    return;
                }

                DeleteAllSaveDataStatus = editorStatus;
                Debug.LogWarning($"[SaveManagerMB] Editor delete all save data failed: {editorStatus}");
                return;
            }

            var result = _saveManager.DeleteAllPersistedData();
            if (result.IsSuccess)
            {
                DeleteAllSaveDataStatus = "Deleted all persisted save data";
                _debug.RefreshNow();
                Debug.Log("[SaveManagerMB] Deleted all persisted save data.");
                return;
            }

            DeleteAllSaveDataStatus = $"{result.Error}: {result.Message}";
            Debug.LogWarning($"[SaveManagerMB] Delete all save data failed: {result.Error} - {result.Message}");
        }

        void OnDestroy()
        {
            _saveManager = null;
            _debug.Dispose();
        }
    }
}
