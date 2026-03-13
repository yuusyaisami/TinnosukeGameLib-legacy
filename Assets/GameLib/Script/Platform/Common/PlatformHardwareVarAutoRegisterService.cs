using Game.Common;
using Game.Vars.Generated;
using UnityEngine;

namespace Game.Platform
{
    public sealed class PlatformHardwareVarAutoRegisterService : IScopeAcquireHandler, IScopeReleaseHandler
    {
        readonly IPlatformBlackboardService _blackboard;

        public PlatformHardwareVarAutoRegisterService(IPlatformBlackboardService blackboard)
        {
            _blackboard = blackboard;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;

            var vars = _blackboard.LocalVars;

            var platform = Application.platform;
            bool isWebGL = platform == RuntimePlatform.WebGLPlayer;
            bool isWindows = platform == RuntimePlatform.WindowsPlayer || platform == RuntimePlatform.WindowsEditor;
            bool isNative = !isWebGL;

            vars.TrySetVariant(VarIds.GameLib.Platform.Hardware.Category.IsHardwareNative, DynamicVariant.FromBool(isNative));
            vars.TrySetVariant(VarIds.GameLib.Platform.Hardware.Kind.IsWebGL, DynamicVariant.FromBool(isWebGL));
            vars.TrySetVariant(VarIds.GameLib.Platform.Hardware.Kind.IsWindows, DynamicVariant.FromBool(isWindows));
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;

            var vars = _blackboard.LocalVars;
            vars.TryUnset(VarIds.GameLib.Platform.Hardware.Category.IsHardwareNative);
            vars.TryUnset(VarIds.GameLib.Platform.Hardware.Kind.IsWebGL);
            vars.TryUnset(VarIds.GameLib.Platform.Hardware.Kind.IsWindows);
        }
    }
}
