#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Commands;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.Spawn
{
    [ExecuteAlways]
    public sealed class SpawnLinePreviewMB : MonoBehaviour
    {
        [Header("Line")]
        [SerializeReference, InlineProperty]
        SpawnLineDefinition? _line;

        [Header("Preview")]
        [SerializeField, Min(1)]
        int _maxPoints = 128;

        [SerializeField, Min(0.001f)]
        float _scale = 1f;

        [SerializeField]
        bool _drawAlways;

        [SerializeField]
        Color _lineColor = new Color(0f, 1f, 1f, 0.8f);

        [SerializeField]
        Color _pointColor = new Color(1f, 0.9f, 0.2f, 0.9f);

        [SerializeField, Min(0f)]
        float _pointRadius = 0.05f;

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (_drawAlways)
                DrawPreview();
        }

        void OnDrawGizmosSelected()
        {
            if (!_drawAlways)
                DrawPreview();
        }

        void DrawPreview()
        {
            if (_line == null)
                return;

            var line = _line.Build(PreviewDynamicContext.Instance);
            var points = line.Points;
            if (points == null || points.Length == 0)
                return;

            int count = Mathf.Min(points.Length, Mathf.Max(1, _maxPoints));
            var origin = transform.position;
            var rot = transform.rotation;

            Gizmos.color = _lineColor;
            var prev = origin + rot * (points[0] * _scale);
            for (int i = 1; i < count; i++)
            {
                var p = origin + rot * (points[i] * _scale);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }

            Gizmos.color = _pointColor;
            for (int i = 0; i < count; i++)
            {
                var p = origin + rot * (points[i] * _scale);
                Gizmos.DrawSphere(p, _pointRadius);
            }
        }

        sealed class PreviewDynamicContext : IDynamicContext
        {
            public static readonly PreviewDynamicContext Instance = new();
            readonly PreviewScopeNode _scope = new();

            public IVarStore Vars => NullVarStore.Instance;
            public IScopeNode Scope => _scope;
            public IScopeNode? CommandRootScope => null;

            public IScopeNode ResolveOtherScope(CommandTargetIdentityFilter filter) => _scope;
        }

        sealed class PreviewScopeNode : IScopeNode
        {
            public IScopeNode? Parent => null;
            public ILTSIdentityService? Identity => null;
            public LifetimeScopeKind Kind => LifetimeScopeKind.None;
            public IObjectResolver? Resolver => null;
            public bool IsVisible => true;
            public bool IsActive => true;

            public bool TrySetVisible(bool visible, bool isReset = false) => false;
            public bool TrySetActive(bool active, bool isReset = false) => false;
            public UniTask SetActiveAsync(bool active, bool isReset = false, CancellationToken ct = default) => UniTask.CompletedTask;
            public IReadOnlyList<IScopeNode>? GetPathFromRoot() => null;
        }
#endif
    }
}
