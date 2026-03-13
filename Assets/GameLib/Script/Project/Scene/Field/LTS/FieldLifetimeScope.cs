using VContainer;
using VContainer.Unity;
using UnityEngine;
using Game.Common;
namespace Game.Field
{
    [RequireComponent(typeof(BlackboardMB))]
    [RequireComponent(typeof(Game.Commands.CommandRunnerMB))]
    [RequireComponent(typeof(Game.Scalar.BaseScalarMB))]
    [RequireComponent(typeof(Game.Common.EventMB))]
    [RequireComponent(typeof(BlackboardMB))]
    public class FieldLifetimeScope : BaseLifetimeScope
    {
        // Field は親(Scene)の下でビルドされるのでルートではない
        protected override bool IsBuildRoot => false;

        // 協調ビルドには参加させる
        protected override bool UseBuildCoordinator => true;

        // 自動 Build は不要（親からの協調ビルド or Spawner が面倒を見る）
        protected override bool AutoBuildOnAwake => false;
        protected override void ConfigureBase(IContainerBuilder builder)
        {
        }
    }

}
