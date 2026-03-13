using UnityEngine;
using Sirenix.OdinInspector;
using Game.Scalar;

namespace Game.Entity.Editor
{
    // Tiny example component demonstrating how a `ScalarKey` field will automatically show a dropdown in the inspector.
    // You no longer need to write an expression on the attribute — declaring the ScalarKey type is enough.
    public class StatKeyPickerExampleMB : MonoBehaviour
    {
        [LabelText("Scalar Key")]
        public ScalarKey picked;

        [ShowInInspector, ReadOnly]
        public string PickedLabel => picked.Name;
    }
}
