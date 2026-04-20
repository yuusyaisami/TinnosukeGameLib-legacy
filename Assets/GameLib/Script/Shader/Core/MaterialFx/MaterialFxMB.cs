#nullable enable
using Game.Animation;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.MaterialFx
{
    /// <summary>
    /// MaterialFx 縺ｮ蝓ｺ譛ｬ繧ｵ繝ｼ繝薙せ繧堤匳骭ｲ縺吶ｋ繧､繝ｳ繧ｹ繝医・繝ｩ繝ｼ縲・
    /// Compute / Noise Atlas 萓晏ｭ倥・謖√◆縺ｪ縺・・
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LifetimeScope))]
    public sealed class MaterialFxMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("MaterialFx")]
        [LabelText("Property Registry")]
        [SerializeField] MaterialFxPropertyRegistrySO? registrySO;

        [BoxGroup("Animation")]
        [LabelText("Empty Animation Data")]
        [Required]
        [SerializeField] AnimationData? emptyAnimationData;

        static AnimationData? s_emptyAnimationData;

        /// <summary>
        /// Empty AnimationData asset other systems can consume when they need a neutral animation.
        /// </summary>
        public static AnimationData EmptyAnimationData => s_emptyAnimationData ??= CreateFallbackEmptyAnimationData();

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            RegisterEmptyAnimationDataAsset();

            var registry = BuildRuntimeRegistry();
            builder.RegisterInstance<IMaterialFxPropertyRegistry>(registry);

            builder.Register<MaterialFxSystemService>(RuntimeLifetime.Singleton)
                .As<IMaterialFxSystemService>()
                .As<IScopeLateTickHandler>();

            builder.Register<IMaterialFxServiceFactory, MaterialFxServiceFactory>(RuntimeLifetime.Singleton);

            MaterialFxService.BaseMaterial = Resources.Load<Material>("Material/BaseMaterial");
        }

        void OnValidate()
        {
            RegisterEmptyAnimationDataAsset();
        }

        void Reset()
        {
            RegisterEmptyAnimationDataAsset();
        }

        void RegisterEmptyAnimationDataAsset()
        {
            if (emptyAnimationData != null)
                s_emptyAnimationData = emptyAnimationData;
        }

        IMaterialFxPropertyRegistry BuildRuntimeRegistry()
        {
            if (registrySO != null)
                return new MaterialFxPropertyRegistryRuntime(registrySO);

            Debug.LogWarning("[MaterialFxMB] PropertyRegistrySO is not assigned. Using fallback empty registry.");
            var fallbackSO = ScriptableObject.CreateInstance<MaterialFxPropertyRegistrySO>();
            return new MaterialFxPropertyRegistryRuntime(fallbackSO);
        }

        static AnimationData CreateFallbackEmptyAnimationData()
        {
            var fallback = ScriptableObject.CreateInstance<AnimationData>();
            fallback.animationName = "Empty AnimationData (fallback)";
            return fallback;
        }
    }
}
