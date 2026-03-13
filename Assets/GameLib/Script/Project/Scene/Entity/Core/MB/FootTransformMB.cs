// Game.Entity
// ================================================================================
// FootTransformMB - Entity 足位置/ Z オフセット処理専用 MonoBehaviour
// ================================================================================
//
// 【概要】
// FootTransformMB はコンポーネントの足位置（ローカル/ワールド）を管理し、
// Y 高度に応じた Z オフセットを自動的に適用します。
// 主に FootWorldPosition と Z オフセットを提供し、Targeting/コマンドから
// 再利用される軽量なヘルパーとして設計されています。
// ================================================================================

using UnityEngine;
using Sirenix.OdinInspector;

namespace Game.Entity
{
    [ExecuteAlways]
    public class FootTransformMB : MonoBehaviour
    {
        [SerializeField]
        public float radius = 0.5f;
        [SerializeField]
        public float offsetZ = 0f;
        // ================================================================================
        // Foot Settings
        // ================================================================================

        [BoxGroup("Foot")]
        [LabelText("Root Foot Offset")]
        [SerializeField]
        private Vector2 rootFoot = Vector2.zero;

        [BoxGroup("Foot/Debug")]
        [LabelText("Show Foot Gizmo")]
        [SerializeField]
        private bool showFootGizmo = true;

        [BoxGroup("Foot/Debug")]
        [LabelText("Foot World Pos")]
        [ShowInInspector]
        [ReadOnly]
        public Vector3 FootWorldPosition => transform != null
            ? transform.TransformPoint(new Vector3(rootFoot.x, rootFoot.y, 0f))
            : Vector3.zero;

        // ================================================================================
        // Z Offset
        // ================================================================================

        public float OffsetZ
        {
            get => offsetZ;
            set
            {
                offsetZ = value;
                UpdateZBasedOnFoot();
            }
        }

        void Awake()
        {
            UpdateZBasedOnFoot();
        }

        void OnValidate()
        {
            UpdateZBasedOnFoot();
        }

        void LateUpdate()
        {
            if (transform == null) return;
            UpdateZBasedOnFoot();
        }

        void UpdateZBasedOnFoot()
        {
            var footWorld = transform.TransformPoint(new Vector3(rootFoot.x, rootFoot.y, 0f));
            float runtimeOffsetZ = footWorld.y * 0.01f;
            float targetZ = runtimeOffsetZ + offsetZ;
            if (!Mathf.Approximately(transform.position.z, targetZ))
            {
                transform.position = new Vector3(transform.position.x, transform.position.y, targetZ);
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (!showFootGizmo)
                return;

            var worldFoot = transform.TransformPoint(new Vector3(rootFoot.x, rootFoot.y, 0f));
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(worldFoot, 0.02f);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, worldFoot);
            UnityEditor.Handles.Label(worldFoot + (Vector3.up * 0.02f), $"Foot ({worldFoot.x:F2},{worldFoot.y:F2})");
        }
#endif
    }
}
