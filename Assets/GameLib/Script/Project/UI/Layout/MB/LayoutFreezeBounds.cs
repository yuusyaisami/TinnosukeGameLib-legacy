using UnityEngine;
using Sirenix.OdinInspector;

namespace Game.Layout
{
    /// <summary>
    /// Explicitly freezes the last calculated Root-local rect for a contributor.
    /// Used for exceptional cases only; implicit freezing is discouraged.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LayoutFreezeBounds : MonoBehaviour
    {
        [BoxGroup("State")]
        [ReadOnly]
        [ShowInInspector]
        bool _hasFrozen;

        [BoxGroup("State")]
        [ReadOnly]
        [ShowInInspector]
        Rect _frozenRect;

        public bool TryGetFrozenRect(out Rect rect)
        {
            rect = _frozenRect;
            return _hasFrozen;
        }

        public void SetFrozenRect(Rect rect)
        {
            _frozenRect = rect;
            _hasFrozen = true;
        }

        public void Clear()
        {
            _hasFrozen = false;
            _frozenRect = default;
        }

        void OnDisable()
        {
            // When disabling freeze, allow recalculation.
            _hasFrozen = false;
        }
    }
}
