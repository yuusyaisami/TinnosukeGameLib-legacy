#nullable enable

using KernelUnitySourceLocation = Game.Kernel.IR.UnitySourceLocation;
using KernelSourceLocationIR = Game.Kernel.IR.SourceLocationIR;
using KernelUnityObjectLinkIR = Game.Kernel.IR.UnityObjectLinkIR;
using KernelSourceLocationId = Game.Kernel.IR.SourceLocationId;

namespace Game.Kernel.Authoring
{
    public static class UnityAuthoringBridge
    {
        public static KernelSourceLocationIR ToKernelSourceLocation(in UnitySourceLocation sourceLocation)
        {
            if (sourceLocation.Kind == UnityAuthoringSourceKind.Unknown)
                throw new System.ArgumentOutOfRangeException(nameof(sourceLocation), sourceLocation.Kind, "Unity authoring source locations must be specified.");

            return new KernelSourceLocationIR(new KernelUnitySourceLocation(
                sourceLocation.AssetGuid,
                sourceLocation.AssetPath,
                sourceLocation.LocalFileId,
                sourceLocation.ScenePath,
                sourceLocation.GameObjectPath,
                sourceLocation.ComponentType,
                sourceLocation.PropertyPath));
        }

        public static Game.Kernel.Boot.UnityObjectLink ToRuntimeLink(in UnityObjectLink link)
        {
            return new Game.Kernel.Boot.UnityObjectLink(
                ToRuntimeLinkKind(link.Kind),
                link.SourceGuid,
                link.LocalFileId,
                link.RuntimeInstanceId,
                link.DebugName);
        }

        public static KernelUnityObjectLinkIR ToPlanLink(in UnityObjectLink link, KernelSourceLocationId source)
        {
            return new KernelUnityObjectLinkIR(ToRuntimeLinkKind(link.Kind).ToString(), link.SourceGuid, link.LocalFileId, link.DebugName, source);
        }

        static Game.Kernel.Boot.UnityObjectLinkKind ToRuntimeLinkKind(UnityObjectLinkKind kind)
        {
            return kind switch
            {
                UnityObjectLinkKind.Asset => Game.Kernel.Boot.UnityObjectLinkKind.Asset,
                UnityObjectLinkKind.Scene => Game.Kernel.Boot.UnityObjectLinkKind.Scene,
                UnityObjectLinkKind.Runtime => Game.Kernel.Boot.UnityObjectLinkKind.Runtime,
                UnityObjectLinkKind.Selection => Game.Kernel.Boot.UnityObjectLinkKind.Selection,
                UnityObjectLinkKind.Unknown => Game.Kernel.Boot.UnityObjectLinkKind.Unknown,
                _ => throw new System.ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported unity object link kind."),
            };
        }
    }
}