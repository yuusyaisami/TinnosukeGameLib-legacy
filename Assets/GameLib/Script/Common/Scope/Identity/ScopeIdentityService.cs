using Game.Times;
using System;
using UnityEngine;

namespace Game
{
    public enum LifetimeScopeKind
    {
        None = 0, // 蝠城｡檎匱逕滓凾逕ｨ 縺薙ｌ縺瑚ｨｭ螳壹＆繧後※縺・ｋ縺ｨ縺阪・豕ｨ諢・
        Project = 1, // library 蜿ｯ閭ｽ縺ｪ譛荳贋ｽ拘cope
        Platform = 2, // 繝励Λ繝・ヨ繝輔か繝ｼ繝蝗ｺ譛峨・繧ｰ繝ｭ繝ｼ繝舌ΝScope (螳溽ｸｾ繧Тteam險ｭ螳壹↑縺ｩ)
        Global = 3, // 繧ｲ繝ｼ繝繝ｭ繧ｸ繝・け邉ｻ縺ｮ繧ｰ繝ｭ繝ｼ繝舌ΝScope (繧ｻ繝ｼ繝悶ョ繝ｼ繧ｿ縺ｪ縺ｩ)
        Scene = 4, // 蜷・す繝ｼ繝ｳ縺斐→縺ｮScope (繧ｷ繝ｼ繝ｳ蛻・ｊ譖ｿ縺医〒遐ｴ譽・逕滓・)
        Field = 5, // 繧ｲ繝ｼ繝繝輔ぅ繝ｼ繝ｫ繝峨＃縺ｨ縺ｮScope (繝輔ぅ繝ｼ繝ｫ繝牙・繧頑崛縺医〒遐ｴ譽・逕滓・)
        Entity = 6, // 繧ｨ繝ｳ繝・ぅ繝・ぅ縺斐→縺ｮScope (繧ｨ繝ｳ繝・ぅ繝・ぅ逕滓・/遐ｴ譽・〒逕滓・/遐ｴ譽・
        UI = 7,
        UIElement = 8,
        Runtime = 9, // 繝ｩ繝ｳ繧ｿ繧､繝縺ｧ逕滓・縺輔ｌ繧鬼cope
        // 蟆・擂霑ｽ蜉: System, Debug, etc...
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

    // 繝吶・繧ｹ繝ｩ繧､繝輔ち繧､繝繧ｹ繧ｳ繝ｼ繝励＃縺ｨ縺ｫ蠢・★蟄伜惠縺吶ｋIdentity
    public sealed class ScopeIdentityService : IScopeIdentityService, IScopeAcquireHandler, IScopeReleaseHandler, IDisposable
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

        public ScopeIdentityService(
            IScopeNode scope,
            ScopeIdentityMB mb,
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

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _registry.RegisterScope(_scope, this);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _registry.UnregisterScope(_scope);
        }

        public void Dispose()
        {
            _registry.UnregisterScope(_scope);
        }
    }
}