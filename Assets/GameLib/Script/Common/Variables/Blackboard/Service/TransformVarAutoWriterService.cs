#nullable enable
using Game.Vars.Generated;
using UnityEngine;
using VContainer.Unity;

namespace Game.Common
{
    public sealed class TransformVarAutoWriterService : ITickable, IScopeAcquireHandler, IScopeReleaseHandler
    {
        readonly IBlackboardService _blackboard;
        readonly Transform _target;

        public TransformVarAutoWriterService(IBlackboardService blackboard, Transform target)
        {
            _blackboard = blackboard;
            _target = target;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;

            if (_target == null || _blackboard == null)
                return;

            WriteTransformVars();
            _target.hasChanged = false;
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;

            if (_blackboard == null)
                return;

            var vars = _blackboard.LocalVars;
            vars.TryUnset(VarIds.GameLib.Base.Transform.Position.Local.x);
            vars.TryUnset(VarIds.GameLib.Base.Transform.Position.Local.y);
            vars.TryUnset(VarIds.GameLib.Base.Transform.Position.Local.z);
            vars.TryUnset(VarIds.GameLib.Base.Transform.Position.World.x);
            vars.TryUnset(VarIds.GameLib.Base.Transform.Position.World.y);
            vars.TryUnset(VarIds.GameLib.Base.Transform.Position.World.z);
            vars.TryUnset(VarIds.GameLib.Base.Transform.Rotation.Local.x);
            vars.TryUnset(VarIds.GameLib.Base.Transform.Rotation.Local.y);
            vars.TryUnset(VarIds.GameLib.Base.Transform.Rotation.Local.z);
            vars.TryUnset(VarIds.GameLib.Base.Transform.Rotation.World.x);
            vars.TryUnset(VarIds.GameLib.Base.Transform.Rotation.World.y);
            vars.TryUnset(VarIds.GameLib.Base.Transform.Rotation.World.z);
        }

        public void Tick()
        {
            if (_target == null || _blackboard == null)
                return;

            if (!_target.hasChanged)
                return;

            _target.hasChanged = false;
            WriteTransformVars();
        }

        void WriteTransformVars()
        {
            var vars = _blackboard.LocalVars;
            var localPos = _target.localPosition;
            var worldPos = _target.position;
            var localRot = _target.localEulerAngles;
            var worldRot = _target.eulerAngles;

            vars.TrySetVariant(VarIds.GameLib.Base.Transform.Position.Local.x, DynamicVariant.FromFloat(localPos.x));
            vars.TrySetVariant(VarIds.GameLib.Base.Transform.Position.Local.y, DynamicVariant.FromFloat(localPos.y));
            vars.TrySetVariant(VarIds.GameLib.Base.Transform.Position.Local.z, DynamicVariant.FromFloat(localPos.z));
            vars.TrySetVariant(VarIds.GameLib.Base.Transform.Position.World.x, DynamicVariant.FromFloat(worldPos.x));
            vars.TrySetVariant(VarIds.GameLib.Base.Transform.Position.World.y, DynamicVariant.FromFloat(worldPos.y));
            vars.TrySetVariant(VarIds.GameLib.Base.Transform.Position.World.z, DynamicVariant.FromFloat(worldPos.z));
            vars.TrySetVariant(VarIds.GameLib.Base.Transform.Rotation.Local.x, DynamicVariant.FromFloat(localRot.x));
            vars.TrySetVariant(VarIds.GameLib.Base.Transform.Rotation.Local.y, DynamicVariant.FromFloat(localRot.y));
            vars.TrySetVariant(VarIds.GameLib.Base.Transform.Rotation.Local.z, DynamicVariant.FromFloat(localRot.z));
            vars.TrySetVariant(VarIds.GameLib.Base.Transform.Rotation.World.x, DynamicVariant.FromFloat(worldRot.x));
            vars.TrySetVariant(VarIds.GameLib.Base.Transform.Rotation.World.y, DynamicVariant.FromFloat(worldRot.y));
            vars.TrySetVariant(VarIds.GameLib.Base.Transform.Rotation.World.z, DynamicVariant.FromFloat(worldRot.z));
        }
    }
}
