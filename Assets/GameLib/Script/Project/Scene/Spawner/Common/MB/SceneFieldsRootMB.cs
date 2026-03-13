// Game.Scene
using Game.Spawn;
using UnityEngine;

namespace Game.Scene
{

    public interface ISceneFieldsRoot
    {
        Transform FieldsRoot { get; }
    }
    /// <summary>
    /// シーンフィールドのルート管理用 MonoBehaviour。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneFieldsRootMB : MonoBehaviour, ISceneFieldsRoot
    {
        [SerializeField] Transform fieldsRoot;
        [SerializeField] string fieldsContainerName = "Fields";

        public Transform FieldsRoot => fieldsRoot;

        void Reset()
        {
            gameObject.name = "Scene-Fields";
            EnsureChild(ref fieldsRoot, fieldsContainerName);
        }

        void Awake()
        {
            EnsureChild(ref fieldsRoot, fieldsContainerName);
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
                    child.SetParent(transform, false);
                }

                slot = child;
            }

            slot.localPosition = Vector3.zero;
        }
    }
}
