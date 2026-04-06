#nullable enable
using UnityEngine;

namespace Game.UI
{
    [DisallowMultipleComponent]
    public sealed class TraitListChannelRuntimeDebugProbeMB : MonoBehaviour
    {
        string _channelTag = string.Empty;
        string _traitLabel = string.Empty;
        string _expectedRoot = string.Empty;
        string _initialParent = string.Empty;

        public void Configure(string channelTag, string traitLabel, string expectedRoot, string initialParent)
        {
            _channelTag = channelTag ?? string.Empty;
            _traitLabel = traitLabel ?? string.Empty;
            _expectedRoot = expectedRoot ?? string.Empty;
            _initialParent = initialParent ?? string.Empty;
        }

        void OnTransformParentChanged()
        {
            //Debug.Log(
            //    $"[TraitListChannel][ProbeParentChanged] tag='{_channelTag}' trait='{_traitLabel}' object='{DescribeTransform(transform)}' " +
            //    $"parent='{DescribeTransform(transform.parent)}' expectedRoot='{_expectedRoot}'");
        }

        void OnDisable()
        {
            //Debug.Log(
            //    $"[TraitListChannel][ProbeDisable] tag='{_channelTag}' trait='{_traitLabel}' object='{DescribeTransform(transform)}' " +
            //    $"parent='{DescribeTransform(transform.parent)}' activeSelf={gameObject.activeSelf} activeInHierarchy={gameObject.activeInHierarchy}");
        }

        void OnDestroy()
        {
            //Debug.Log(
            //    $"[TraitListChannel][ProbeDestroy] tag='{_channelTag}' trait='{_traitLabel}' object='{DescribeTransform(transform)}' " +
            //    $"parent='{DescribeTransform(transform.parent)}'");
        }

        static string DescribeTransform(Transform? target)
        {
            if (target == null)
                return "<null>";

            return $"{target.name} path='{BuildPath(target)}'";
        }

        static string BuildPath(Transform target)
        {
            var current = target;
            var path = current.name;
            while (current.parent != null)
            {
                current = current.parent;
                path = $"{current.name}/{path}";
            }

            return path;
        }
    }
}
