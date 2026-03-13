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

        public void InstallFeature(IContainerBuilder builder, IScopeNode owner)
        {
            // デフォルトオプション（読み取り専用）
            if (defaultOption != null)
            {
                builder.RegisterInstance<IInputDefaultOption>(defaultOption);
            }

            // 実際に利用される「現在値」を管理するサービス
            builder.Register<InputOptionService>(Lifetime.Singleton)
                .As<IInputOption>()
                .AsSelf();

            // 低レベル：InputActions ラッパー
            builder.Register<InputActionsSource>(Lifetime.Singleton)
                .As<IInputActionsSource>()
                .As<IDisposable>();

            // 中レベル：スキーム
            builder.Register<ControlSchemeService>(Lifetime.Singleton)
                .As<IControlSchemeService>()
                .As<ITickable>()
                .As<IDisposable>();

            // 中レベル：ポインタ
            builder.Register<PointerService>(Lifetime.Singleton)
                .As<IPointerService>()
                .As<ITickable>();


            // 中レベル：コンシューマールーティング
            builder.Register<InputRouter>(Lifetime.Singleton)
                .As<IInputRouter>()
                .As<ITickable>();

            // ブロック機構（ハンドル方式）
            builder.Register<InputBlocker>(Lifetime.Singleton)
                .As<IInputBlocker>();
        }
    }
}
