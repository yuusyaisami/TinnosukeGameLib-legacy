using UnityEngine;
using Game;
using VContainer;
using VContainer.Unity;
using System;

namespace Game.Input
{
    public class InputMB : MonoBehaviour, IFeatureInstaller
    {
        [SerializeField] InputDefaultOptionAsset defaultOption;

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            // 繝・ヵ繧ｩ繝ｫ繝医が繝励す繝ｧ繝ｳ・郁ｪｭ縺ｿ蜿悶ｊ蟆ら畑・・
            if (defaultOption != null)
            {
                builder.RegisterInstance<IInputDefaultOption>(defaultOption);
            }

            // 螳滄圀縺ｫ蛻ｩ逕ｨ縺輔ｌ繧九檎樟蝨ｨ蛟､縲阪ｒ邂｡逅・☆繧九し繝ｼ繝薙せ
            builder.Register<InputOptionService>(RuntimeLifetime.Singleton)
                .As<IInputOption>()
                .AsSelf();

            // 菴弱Ξ繝吶Ν・唔nputActions 繝ｩ繝・ヱ繝ｼ
            builder.Register<InputActionsSource>(RuntimeLifetime.Singleton)
                .As<IInputActionsSource>()
                .As<IDisposable>();

            // 荳ｭ繝ｬ繝吶Ν・壹せ繧ｭ繝ｼ繝
            builder.Register<ControlSchemeService>(RuntimeLifetime.Singleton)
                .As<IControlSchemeService>()
                .As<IScopeTickHandler>()
                .As<IDisposable>();

            // 荳ｭ繝ｬ繝吶Ν・壹・繧､繝ｳ繧ｿ
            builder.Register<PointerService>(RuntimeLifetime.Singleton)
                .As<IPointerService>()
                .As<IScopeTickHandler>();


            // 荳ｭ繝ｬ繝吶Ν・壹さ繝ｳ繧ｷ繝･繝ｼ繝槭・繝ｫ繝ｼ繝・ぅ繝ｳ繧ｰ
            builder.Register<InputRouter>(RuntimeLifetime.Singleton)
                .As<IInputRouter>()
                .As<IScopeTickHandler>();

            // 繝悶Ο繝・け讖滓ｧ具ｼ医ワ繝ｳ繝峨Ν譁ｹ蠑擾ｼ・
            builder.Register<InputBlocker>(RuntimeLifetime.Singleton)
                .As<IInputBlocker>();
        }
    }
}
