#nullable enable

using KernelUnitySourceLocation = Game.Kernel.IR.UnitySourceLocation;
using KernelSourceLocationIR = Game.Kernel.IR.SourceLocationIR;
using KernelUnityObjectLinkIR = Game.Kernel.IR.UnityObjectLinkIR;
using KernelSourceLocationId = Game.Kernel.IR.SourceLocationId;
using KernelRuntimeUnityObjectLink = Game.Kernel.ScopeGraph.UnityObjectLink;
using KernelRuntimeUnityObjectLinkKind = Game.Kernel.ScopeGraph.UnityObjectLinkKind;

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

        public static KernelRuntimeUnityObjectLink ToRuntimeLink(in UnityObjectLink link)
        {
            return new KernelRuntimeUnityObjectLink(
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

        static KernelRuntimeUnityObjectLinkKind ToRuntimeLinkKind(UnityObjectLinkKind kind)
        {
            return kind switch
            {
                UnityObjectLinkKind.Asset => KernelRuntimeUnityObjectLinkKind.Asset,
                UnityObjectLinkKind.Scene => KernelRuntimeUnityObjectLinkKind.Scene,
                UnityObjectLinkKind.Runtime => KernelRuntimeUnityObjectLinkKind.Runtime,
                UnityObjectLinkKind.Selection => KernelRuntimeUnityObjectLinkKind.Selection,
                UnityObjectLinkKind.Unknown => KernelRuntimeUnityObjectLinkKind.Unknown,
                _ => throw new System.ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported unity object link kind."),
            };
        }
    }
}