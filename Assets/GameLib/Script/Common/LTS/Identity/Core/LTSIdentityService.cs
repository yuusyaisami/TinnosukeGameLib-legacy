using VContainer.Unity;
using Game.Times;
using System;
using UnityEngine;

namespace Game
{
    public enum LifetimeScopeKind
    {
        None = 0, // 問題発生時用 これが設定されているときは注意
        Project, // library 可能な最上位Scope
        Platform, // プラットフォーム固有のグローバルScope (実績やSteam設定など)
        Global, // ゲームロジック系のグローバルScope (セーブデータなど)
        Scene, // 各シーンごとのScope (シーン切り替えで破棄/生成)
        Field, // ゲームフィールドごとのScope (フィールド切り替えで破棄/生成)
        Entity, // エンティティごとのScope (エンティティ生成/破棄で生成/破棄)
        UI,
        UIElement,
        Runtime, // ランタイムで生成されるScope
        // 将来追加: System, Debug, etc...
    }
    [Flags]
    public enum LifetimeScopeMask
    {
        None = 0,
        // NOTE:
        // - Mask bits are explicitly fixed (do not derive from LifetimeScopeKind numeric values).
        // - Keep current bit positions to avoid breaking existing serialized data.
        Project = 1 << 1,
        Platform = 1 << 2,
        Global = 1 << 3,
        Scene = 1 << 4,
        Field = 1 << 5,
        Entity = 1 << 6,
        UI = 1 << 7,
        UIElement = 1 << 8,
        Runtime = 1 << 9,

        All = Project | Platform | Global | Scene | Field | Entity | UI | UIElement | Runtime
    }

    // ベースライフタイムスコープごとに必ず存在するIdentity
    public sealed class LTSIdentityService : ILTSIdentityService, IStartable, IDisposable
    {
        readonly IScopeNode _scope;
        readonly IBaseLifetimeScopeRegistry _registry;

        public LifetimeScopeKind Kind { get; }
        public string Id { get; }
        public string Category { get; }
        public bool IsActive { get; set; }
        public TimeScaleBehavior TimeScaleBehavior { get; }
        public Transform SelfTransform { get; }
        public float Radius { get; }

        public LTSIdentityService(
            IScopeNode scope,
            LTSIdentityMB mb,
            IBaseLifetimeScopeRegistry registry)
        {
            _scope = scope;
            _registry = registry;

            Kind = mb.kind;
            Id = string.IsNullOrEmpty(mb.id) ? mb.gameObject.name : mb.id;
            SelfTransform = mb.transform;
            Category = mb.category;
            IsActive = mb.initiallyActive;
            TimeScaleBehavior = mb.timeScaleBehavior;
            Radius = mb.Radius;
        }

        public void Start()
        {
            // DI 完了後に登録されるので安全
            if (_scope is BaseLifetimeScope baseScope)
            {
                _registry.RegisterScope(baseScope, this);
            }
        }

        public void Dispose()
        {
            if (_scope is BaseLifetimeScope baseScope)
            {
                _registry.UnregisterScope(baseScope);
            }
        }
    }
}
