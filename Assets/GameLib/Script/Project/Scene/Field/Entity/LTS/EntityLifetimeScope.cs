// Game.Entity
// ================================================================================
// EntityLifetimeScope - Entity 用の LifetimeScope
// ================================================================================
//
// 【概要】
// Entity（キャラクター、敵、アイテムなど）を管理する LifetimeScope。
// 各 Entity ごとに独立した DI スコープを持ち、Entity 固有のサービスを登録できる。
//
// 【親スコープ】
// - 通常は FieldLifetimeScope または SceneLifetimeScope の子として存在
// - 協調ビルドに参加し、親のビルド完了後にビルドされる
//
// 【EntityRegistry への登録】
// - EntityIdentityService により自動的に登録される
// - ILTSIdentityService から Id/Category を取得し、インデックスに追加
// - スコープ破棄時に自動的に Unregister
//
// 【必須コンポーネント】
// - CommandRunnerMB: コマンドシステム
// - BaseScalarMB: スカラー値システム
// - EventMB: イベントシステム
// - ActionBlockMB: アクションブロック
// - FootTransformMB: Entity 足位置/ Z オフセット
// ================================================================================

using VContainer;
using VContainer.Unity;
using UnityEngine;
using Game.Input;
using Game.Profile;
using Game.Common;

namespace Game.Entity
{
    [RequireComponent(typeof(Commands.CommandRunnerMB))]
    [RequireComponent(typeof(Scalar.BaseScalarMB))]
    [RequireComponent(typeof(BlackboardMB))]
    [RequireComponent(typeof(Common.EventMB))]
    [RequireComponent(typeof(ActionBlockMB))]
    [RequireComponent(typeof(FootTransformMB))]
    [RequireComponent(typeof(ScopeBindingRegistryMB))]
    public sealed class EntityLifetimeScope : BaseLifetimeScope
    {
        // Entity は親(Field or Scene)の下でビルドされるのでルートではない
        protected override bool IsBuildRoot => false;

        // 協調ビルドには参加させる
        protected override bool UseBuildCoordinator => true;

        // 自動 Build は不要（親からの協調ビルド or Spawner が面倒を見る）
        protected override bool AutoBuildOnAwake => false;

        protected override void ConfigureBase(IContainerBuilder builder)
        {
            Game.LTSLog.Log("[EntityLifetimeScope] Configuring Entity scoped services.");

            // Entity 単位で欲しいサービス登録
            // builder.Register<EnemyBrain>(Lifetime.Scoped);
            // builder.RegisterInstance(this); // など
        }
    }
}
