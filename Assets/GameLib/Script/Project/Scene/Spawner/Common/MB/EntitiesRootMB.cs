// Game.Scene
using Game.Entity;
using UnityEngine;

namespace Game.Scene
{
    public interface IEntityRoot
    {
        Transform EntitiesRoot { get; }
        Transform RuntimeRoot { get; }
    }
    [DisallowMultipleComponent]
    public sealed class EntitiesRootMB : MonoBehaviour, IEntityRoot
    {
        [SerializeField] Transform entitiesRoot;
        [SerializeField] Transform runtimeRoot;

        public Transform EntitiesRoot => entitiesRoot;
        public Transform RuntimeRoot => runtimeRoot;

        void Reset()
        {
            gameObject.name = "EntitiesRoot";
            EnsureChild(ref entitiesRoot, "Entities");
            EnsureChild(ref runtimeRoot, "Runtimes");
        }

        void Awake()
        {
            EnsureChild(ref entitiesRoot, "Entities");
            EnsureChild(ref runtimeRoot, "Runtimes");
        }

        void EnsureChild(ref Transform slot, string childName)
        {
            if (slot == null)
            {
                var child = transform.Find(childName);
                if (child == null)
                {
                    var go = new GameObject(childName);
                    child = go.transform;
                    child.SetParent(transform, false); // localPosition = 0
                }

                slot = child;
            }

            // Folder は常に localPosition=0 を保証
            slot.localPosition = Vector3.zero;
        }
    }
}
