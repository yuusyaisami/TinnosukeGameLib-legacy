#nullable enable
using System;
using Game.Kernel.IR;
using Game.Kernel.ScopeGraph;
using UnityEngine;

namespace Game.Kernel.Boot
{
    [DisallowMultipleComponent]
    public sealed class UnityObjectLinkAuthoring : MonoBehaviour
    {
        [SerializeField]
        UnityObjectLinkKind kind = UnityObjectLinkKind.Unknown;

        [SerializeField]
        string sourceGuid = string.Empty;

        [SerializeField]
        long localFileId;

        [SerializeField]
        int runtimeInstanceId;

        [SerializeField]
        string debugName = string.Empty;

        public UnityObjectLinkKind Kind => kind;

        public string SourceGuid => sourceGuid;

        public long LocalFileId => localFileId;

        public int RuntimeInstanceId => runtimeInstanceId;

        public string DebugName => debugName;

        public static Game.Kernel.Authoring.AuthoringComponentKind ComponentKind => Game.Kernel.Authoring.AuthoringComponentKind.Link;

        public UnityObjectLink CreateRuntimeLink()
        {
            return UnityObjectLinkBridge.Create(kind, sourceGuid, localFileId, runtimeInstanceId, debugName);
        }

        public UnityObjectLinkIR CreatePlanLink(SourceLocationId source)
        {
            return UnityObjectLinkBridge.CreatePlanLink(kind, sourceGuid, localFileId, debugName, source);
        }

        void OnValidate()
        {
            if (sourceGuid != null)
                sourceGuid = sourceGuid.Trim();

            if (debugName != null)
                debugName = debugName.Trim();
        }
    }

    public static class UnityObjectLinkBridge
    {
        public static UnityObjectLinkIR CreatePlanLink(
            UnityObjectLinkKind kind,
            string? sourceGuid,
            long localFileId,
            string? debugName,
            SourceLocationId source)
        {
            return new UnityObjectLinkIR(kind.ToString(), sourceGuid, localFileId, debugName ?? string.Empty, source);
        }

        public static UnityObjectLink Create(
            UnityObjectLinkKind kind,
            string? sourceGuid,
            long localFileId,
            int runtimeInstanceId,
            string? debugName)
        {
            return new UnityObjectLink(kind, sourceGuid, localFileId, runtimeInstanceId, debugName);
        }
    }
}