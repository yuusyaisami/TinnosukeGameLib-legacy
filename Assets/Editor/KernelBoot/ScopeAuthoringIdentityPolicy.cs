#nullable enable

using System;
using Game.Kernel.Boot;
using Game.Kernel.IR;

namespace TinnosukeGameLib.Editor.KernelBoot
{
    public static class ScopeAuthoringIdentityPolicy
    {
        public static ScopeAuthoringId AllocateNextAuthoringId(ScopeAuthoringRoot root)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            return root.AllocateNextScopeAuthoringId();
        }

        public static bool TryAssignMissingAuthoringId(ScopeAuthoringRoot root, ScopeAuthoringLink link, out ScopeAuthoringId assignedId, out string failureReason)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            if (link == null)
                throw new ArgumentNullException(nameof(link));

            if (link.HasScopeAuthoringId)
            {
                assignedId = link.ScopeAuthoringId;
                failureReason = "ScopeAuthoringId is already assigned.";
                return false;
            }

            assignedId = AllocateNextAuthoringId(root);
            link.SetAuthoringId(assignedId);
            failureReason = string.Empty;
            return true;
        }

        public static bool TryRegenerateAuthoringId(ScopeAuthoringRoot root, ScopeAuthoringLink link, out ScopeAuthoringId regeneratedId, out string failureReason)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            if (link == null)
                throw new ArgumentNullException(nameof(link));

            regeneratedId = AllocateNextAuthoringId(root);
            if (link.HasScopeAuthoringId && link.ScopeAuthoringId == regeneratedId)
                regeneratedId = new ScopeAuthoringId(checked(regeneratedId.Value + 1));

            link.SetAuthoringId(regeneratedId);
            root.RegisterExistingScopeAuthoringId(regeneratedId);
            failureReason = string.Empty;
            return true;
        }

        public static bool TryResolveDuplicateAuthoringId(
            ScopeAuthoringRoot root,
            ScopeAuthoringLink target,
            out ScopeAuthoringId resolvedId,
            out string failureReason)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            if (target == null)
                throw new ArgumentNullException(nameof(target));

            if (!target.HasScopeAuthoringId)
            {
                failureReason = "ScopeAuthoringId must exist before a duplicate can be resolved.";
                resolvedId = default;
                return false;
            }

            resolvedId = AllocateNextAuthoringId(root);
            if (resolvedId == target.ScopeAuthoringId)
                resolvedId = new ScopeAuthoringId(checked(resolvedId.Value + 1));

            target.SetAuthoringId(resolvedId);
            root.RegisterExistingScopeAuthoringId(resolvedId);
            failureReason = string.Empty;
            return true;
        }
    }
}