using System;
using Game.Commands;
using UnityEngine;
using Game.Common;
namespace Game.Field
{
    [RequireComponent(typeof(Game.Commands.CommandRunnerMB))]
    [RequireComponent(typeof(Game.Scalar.BaseScalarMB))]
    [RequireComponent(typeof(Game.Common.EventMB))]
    public class FieldLifetimeScope : RuntimeLifetimeScopeBase
    {
        // Field 縺ｯ隕ｪ(Scene)縺ｮ荳九〒繝薙Ν繝峨＆繧後ｋ縺ｮ縺ｧ繝ｫ繝ｼ繝医〒縺ｯ縺ｪ縺・
        protected override bool IsBuildRoot => false;

        // 蜊碑ｪｿ繝薙Ν繝峨↓縺ｯ蜿ょ刈縺輔○繧・
        protected override bool UseBuildCoordinator => true;

        // 閾ｪ蜍・Build 縺ｯ荳崎ｦ・ｼ郁ｦｪ縺九ｉ縺ｮ蜊碑ｪｿ繝薙Ν繝・or Spawner 縺碁擇蛟偵ｒ隕九ｋ・・
        protected override bool AutoBuildOnAwake => false;
        protected override LifetimeScopeKind RequiredParentKind => LifetimeScopeKind.Scene;

        protected override void AwakeConfigure(IRuntimeContainerBuilder builder)
        {
            var commandRunner = GetComponent<CommandRunnerMB>();
            if (commandRunner == null)
                throw new InvalidOperationException($"{nameof(FieldLifetimeScope)} requires {nameof(CommandRunnerMB)}.");

            commandRunner.InstallRuntime(builder, this);
        }

        protected override void ConfigureBase(IRuntimeContainerBuilder builder)
        {
        }
    }

}
