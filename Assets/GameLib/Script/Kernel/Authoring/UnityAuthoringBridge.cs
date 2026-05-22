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

        public static KernelUnityObjectLinkIR ToPlanLink(in UnityObjectLink link, KernelSourceLocationId source)
        {
            return new KernelUnityObjectLinkIR(link.Kind.ToString(), link.SourceGuid, link.LocalFileId, link.DebugName, source);
        }
    }
}