using UnityEngine;
using Game;
using VContainer;
using VContainer.Unity;
using System;

namespace Game.Input
{
    public class InputMB : MonoBehaviour, IScopeInstaller
    {
        [SerializeField] InputDefaultOptionAsset defaultOption;

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            // チE��ォルトオプション�E�読み取り専用�E�E
            if (defaultOption != null)
            {
                builder.RegisterInstance<IInputDefaultOption>(defaultOption);
            }

            // 実際に利用される「現在値」を管琁E��るサービス
            builder.Register<InputOptionService>(RuntimeLifetime.Singleton)
                .As<IInputOption>()
                .AsSelf();

            // 低レベル�E�InputActions ラチE��ー
            builder.Register<InputActionsSource>(RuntimeLifetime.Singleton)
                .As<IInputActionsSource>()
                .As<IDisposable>();

            // 中レベル�E�スキーム
            builder.Register<ControlSchemeService>(RuntimeLifetime.Singleton)
                .As<IControlSchemeService>()
                .As<IScopeTickHandler>()
                .As<IDisposable>();

            // 中レベル�E��Eインタ
            builder.Register<PointerService>(RuntimeLifetime.Singleton)
                .As<IPointerService>()
                .As<IScopeTickHandler>();


            // 中レベル�E�コンシューマ�EルーチE��ング
            builder.Register<InputRouter>(RuntimeLifetime.Singleton)
                .As<IInputRouter>()
                .As<IScopeTickHandler>();

            // ブロチE��機構（ハンドル方式！E
            builder.Register<InputBlocker>(RuntimeLifetime.Singleton)
                .As<IInputBlocker>();
        }
    }
}

