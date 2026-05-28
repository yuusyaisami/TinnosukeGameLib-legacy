#nullable enable
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading;
using VContainer;
using Cysharp.Threading.Tasks;
using VNext = Game.Commands.VNext;
using Game.Common;
using Game;

namespace Game.UI
{
    public readonly struct UINodeHandle : IEquatable<UINodeHandle>
    {
        readonly int _value;

        public UINodeHandle(int value)
        {
            _value = value;
        }

        public int Value => _value;
        public bool IsValid => _value > 0;
        public static UINodeHandle Invalid => default;

        public bool Equals(UINodeHandle other) => _value == other._value;
        public override bool Equals(object? obj) => obj is UINodeHandle other && Equals(other);
        public override int GetHashCode() => _value;
        public override string ToString() => IsValid ? $"ui-node:{_value}" : "ui-node:invalid";

        public static bool operator ==(UINodeHandle left, UINodeHandle right) => left.Equals(right);
        public static bool operator !=(UINodeHandle left, UINodeHandle right) => !left.Equals(right);
    }

    public interface IUIHandleNode
    {
        UINodeHandle NodeHandle { get; }
    }

    public interface IUINodeGraphService
    {
        int NodeCount { get; }
        int Version { get; }

        UINodeHandle RegisterNode(
            IScopeNode owner,
            IUIElementState state,
            UIElementStateMB? sourceComponent,
            NavigationOverride? navigationOverride);

        void UnregisterNode(UINodeHandle handle, IScopeNode owner, UIElementStateMB? sourceComponent);
        bool TryGetHandle(IScopeNode owner, out UINodeHandle handle);
        bool TryGetOwner(UINodeHandle handle, out IScopeNode owner);
        bool TryGetState(UINodeHandle handle, out IUIElementState state);
        bool TryGetParentHandle(UINodeHandle handle, out UINodeHandle parent);
        bool TryGetRootHandle(UINodeHandle handle, out UINodeHandle root);
        bool TryGetDepth(UINodeHandle handle, out int depth);
        bool IsSameOrDescendant(UINodeHandle handle, UINodeHandle ancestor);
        bool TryGetNavigationTarget(UINodeHandle handle, NavigateDirection direction, out UINodeHandle target);
    }

    public interface IUINodeGraphTelemetry
    {
        int NodeCount { get; }
        int Version { get; }
        bool TryGetHandle(IScopeNode owner, out UINodeHandle handle);
        bool TryGetOwner(UINodeHandle handle, out IScopeNode owner);
        bool TryGetParentHandle(UINodeHandle handle, out UINodeHandle parent);
        bool TryGetRootHandle(UINodeHandle handle, out UINodeHandle root);
        bool TryGetDepth(UINodeHandle handle, out int depth);
        bool IsSameOrDescendant(UINodeHandle handle, UINodeHandle ancestor);
        bool TryGetNavigationTarget(UINodeHandle handle, NavigateDirection direction, out UINodeHandle target);
    }

    public sealed class UINodeGraphService : IUINodeGraphService, IUINodeGraphTelemetry
    {
        sealed class NodeRecord
        {
            public NodeRecord(UINodeHandle handle, IScopeNode owner, IUIElementState state, UIElementStateMB? sourceComponent)
            {
                Handle = handle;
                Owner = owner;
                State = state;
                SourceComponent = sourceComponent;
            }

            public UINodeHandle Handle { get; }
            public IScopeNode Owner { get; }
            public IUIElementState State { get; set; }
            public UIElementStateMB? SourceComponent { get; set; }
            public UINodeHandle ParentHandle { get; set; }
            public UINodeHandle RootHandle { get; set; }
            public int Depth { get; set; }
            public UINodeHandle UpHandle { get; set; }
            public UINodeHandle DownHandle { get; set; }
            public UINodeHandle LeftHandle { get; set; }
            public UINodeHandle RightHandle { get; set; }

            public void ClearNavigation()
            {
                UpHandle = UINodeHandle.Invalid;
                DownHandle = UINodeHandle.Invalid;
                LeftHandle = UINodeHandle.Invalid;
                RightHandle = UINodeHandle.Invalid;
            }

            public bool TryGetNavigationTarget(NavigateDirection direction, out UINodeHandle target)
            {
                target = direction switch
                {
                    NavigateDirection.Up => UpHandle,
                    NavigateDirection.Down => DownHandle,
                    NavigateDirection.Left => LeftHandle,
                    NavigateDirection.Right => RightHandle,
                    _ => UINodeHandle.Invalid,
                };

                return target.IsValid;
            }

            public void SetNavigationTarget(NavigateDirection direction, UINodeHandle target)
            {
                switch (direction)
                {
                    case NavigateDirection.Up:
                        UpHandle = target;
                        break;
                    case NavigateDirection.Down:
                        DownHandle = target;
                        break;
                    case NavigateDirection.Left:
                        LeftHandle = target;
                        break;
                    case NavigateDirection.Right:
                        RightHandle = target;
                        break;
                }
            }

            public void RemoveNavigationTarget(UINodeHandle target)
            {
                if (UpHandle == target)
                    UpHandle = UINodeHandle.Invalid;
                if (DownHandle == target)
                    DownHandle = UINodeHandle.Invalid;
                if (LeftHandle == target)
                    LeftHandle = UINodeHandle.Invalid;
                if (RightHandle == target)
                    RightHandle = UINodeHandle.Invalid;
            }
        }

        readonly struct PendingNavigationLink
        {
            public PendingNavigationLink(UINodeHandle sourceHandle, NavigateDirection direction)
            {
                SourceHandle = sourceHandle;
                Direction = direction;
            }

            public UINodeHandle SourceHandle { get; }
            public NavigateDirection Direction { get; }
        }

        readonly Dictionary<IScopeNode, NodeRecord> _recordsByOwner = new(ReferenceEqualityComparer<IScopeNode>.Instance);
        readonly List<NodeRecord?> _recordsByHandle = new();
        readonly Dictionary<UIElementStateMB, UINodeHandle> _handlesByComponent = new();
        readonly Dictionary<UIElementStateMB, List<PendingNavigationLink>> _pendingByTargetComponent = new();
        int _nextHandleValue = 1;
        int _nodeCount;
        int _version;

        public int NodeCount => _nodeCount;
        public int Version => _version;

        public UINodeHandle RegisterNode(
            IScopeNode owner,
            IUIElementState state,
            UIElementStateMB? sourceComponent,
            NavigationOverride? navigationOverride)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));

            if (state == null)
                throw new ArgumentNullException(nameof(state));

            if (_recordsByOwner.TryGetValue(owner, out var existing))
            {
                var previousComponent = existing.SourceComponent;
                if (previousComponent != null && !ReferenceEquals(previousComponent, sourceComponent))
                    _handlesByComponent.Remove(previousComponent);

                existing.State = state;
                existing.SourceComponent = sourceComponent;
                if (sourceComponent != null)
                    _handlesByComponent[sourceComponent] = existing.Handle;

                existing.ClearNavigation();
                RemovePendingForSource(existing.Handle);
                ApplyNavigationOverride(existing, navigationOverride);
                RefreshHierarchyMetadata();
                ResolvePendingForTarget(sourceComponent, existing.Handle);
                _version++;
                return existing.Handle;
            }

            var handle = new UINodeHandle(_nextHandleValue++);
            var record = new NodeRecord(handle, owner, state, sourceComponent);
            _recordsByOwner.Add(owner, record);
            _recordsByHandle.Add(record);
            _nodeCount++;

            if (sourceComponent != null)
                _handlesByComponent[sourceComponent] = handle;

            ApplyNavigationOverride(record, navigationOverride);
            RefreshHierarchyMetadata();
            ResolvePendingForTarget(sourceComponent, handle);
            _version++;
            return handle;
        }

        public void UnregisterNode(UINodeHandle handle, IScopeNode owner, UIElementStateMB? sourceComponent)
        {
            if (!handle.IsValid)
                return;

            if (!TryGetRecord(handle, out var record))
                return;

            if (!ReferenceEquals(record.Owner, owner))
                return;

            _recordsByHandle[handle.Value - 1] = null;
            _recordsByOwner.Remove(owner);
            _nodeCount--;

            var resolvedComponent = record.SourceComponent ?? sourceComponent;
            if (resolvedComponent != null && _handlesByComponent.TryGetValue(resolvedComponent, out var componentHandle) && componentHandle == handle)
                _handlesByComponent.Remove(resolvedComponent);

            record.ClearNavigation();
            RemoveIncomingReferences(handle);
            RemovePendingForSource(handle);
            RefreshHierarchyMetadata();
            _version++;
        }

        public bool TryGetHandle(IScopeNode owner, out UINodeHandle handle)
        {
            if (owner != null && _recordsByOwner.TryGetValue(owner, out var record))
            {
                handle = record.Handle;
                return true;
            }

            handle = UINodeHandle.Invalid;
            return false;
        }

        public bool TryGetOwner(UINodeHandle handle, out IScopeNode owner)
        {
            if (TryGetRecord(handle, out var record))
            {
                owner = record.Owner;
                return true;
            }

            owner = null!;
            return false;
        }

        public bool TryGetState(UINodeHandle handle, out IUIElementState state)
        {
            if (TryGetRecord(handle, out var record))
            {
                state = record.State;
                return true;
            }

            state = null!;
            return false;
        }

        public bool TryGetParentHandle(UINodeHandle handle, out UINodeHandle parent)
        {
            parent = UINodeHandle.Invalid;
            if (!TryGetRecord(handle, out var record))
                return false;

            parent = record.ParentHandle;
            return parent.IsValid;
        }

        public bool TryGetRootHandle(UINodeHandle handle, out UINodeHandle root)
        {
            root = UINodeHandle.Invalid;
            if (!TryGetRecord(handle, out var record))
                return false;

            root = record.RootHandle.IsValid ? record.RootHandle : record.Handle;
            return true;
        }

        public bool TryGetDepth(UINodeHandle handle, out int depth)
        {
            depth = 0;
            if (!TryGetRecord(handle, out var record))
                return false;

            depth = record.Depth;
            return true;
        }

        public bool IsSameOrDescendant(UINodeHandle handle, UINodeHandle ancestor)
        {
            if (!handle.IsValid || !ancestor.IsValid)
                return false;

            if (!TryGetRecord(handle, out var record))
                return false;

            if (record.Handle == ancestor)
                return true;

            if (record.RootHandle != ancestor && !TryGetRecord(ancestor, out _))
                return false;

            var current = record.ParentHandle;
            while (current.IsValid)
            {
                if (current == ancestor)
                    return true;

                if (!TryGetRecord(current, out var currentRecord))
                    return false;

                current = currentRecord.ParentHandle;
            }

            return false;
        }

        public bool TryGetNavigationTarget(UINodeHandle handle, NavigateDirection direction, out UINodeHandle target)
        {
            if (TryGetRecord(handle, out var record) && record.TryGetNavigationTarget(direction, out target))
                return true;

            target = UINodeHandle.Invalid;
            return false;
        }

        void ApplyNavigationOverride(NodeRecord source, NavigationOverride? navigationOverride)
        {
            if (navigationOverride == null)
                return;

            ApplyNavigationLink(source, navigationOverride, NavigateDirection.Up);
            ApplyNavigationLink(source, navigationOverride, NavigateDirection.Down);
            ApplyNavigationLink(source, navigationOverride, NavigateDirection.Left);
            ApplyNavigationLink(source, navigationOverride, NavigateDirection.Right);
        }

        void ApplyNavigationLink(NodeRecord source, NavigationOverride navigationOverride, NavigateDirection direction)
        {
            var targetComponent = navigationOverride.GetOverrideElement(direction);
            if (targetComponent == null)
                return;

            if (_handlesByComponent.TryGetValue(targetComponent, out var targetHandle) && targetHandle.IsValid)
            {
                source.SetNavigationTarget(direction, targetHandle);
                return;
            }

            if (!_pendingByTargetComponent.TryGetValue(targetComponent, out var pending))
            {
                pending = new List<PendingNavigationLink>();
                _pendingByTargetComponent.Add(targetComponent, pending);
            }

            pending.Add(new PendingNavigationLink(source.Handle, direction));
        }

        void ResolvePendingForTarget(UIElementStateMB? sourceComponent, UINodeHandle targetHandle)
        {
            if (sourceComponent == null)
                return;

            if (!_pendingByTargetComponent.TryGetValue(sourceComponent, out var pending))
                return;

            for (int i = 0; i < pending.Count; i++)
            {
                var link = pending[i];
                if (TryGetRecord(link.SourceHandle, out var sourceRecord))
                    sourceRecord.SetNavigationTarget(link.Direction, targetHandle);
            }

            _pendingByTargetComponent.Remove(sourceComponent);
        }

        void RemoveIncomingReferences(UINodeHandle handle)
        {
            for (int i = 0; i < _recordsByHandle.Count; i++)
            {
                var record = _recordsByHandle[i];
                if (record == null)
                    continue;

                record.RemoveNavigationTarget(handle);
            }
        }

        void RemovePendingForSource(UINodeHandle sourceHandle)
        {
            List<UIElementStateMB>? emptyKeys = null;

            foreach (var pair in _pendingByTargetComponent)
            {
                var pending = pair.Value;
                for (int index = pending.Count - 1; index >= 0; index--)
                {
                    if (pending[index].SourceHandle == sourceHandle)
                        pending.RemoveAt(index);
                }

                if (pending.Count == 0)
                {
                    emptyKeys ??= new List<UIElementStateMB>();
                    emptyKeys.Add(pair.Key);
                }
            }

            if (emptyKeys == null)
                return;

            for (int i = 0; i < emptyKeys.Count; i++)
                _pendingByTargetComponent.Remove(emptyKeys[i]);
        }

        bool TryGetRecord(UINodeHandle handle, out NodeRecord record)
        {
            var index = handle.Value - 1;
            if (!handle.IsValid || index < 0 || index >= _recordsByHandle.Count)
            {
                record = null!;
                return false;
            }

            record = _recordsByHandle[index]!;
            return record != null;
        }

        void RefreshHierarchyMetadata()
        {
            for (int i = 0; i < _recordsByHandle.Count; i++)
            {
                var record = _recordsByHandle[i];
                if (record == null)
                    continue;

                var parentHandle = UINodeHandle.Invalid;
                var rootHandle = record.Handle;
                var depth = 0;

                for (var current = record.Owner.Parent; current != null; current = current.Parent)
                {
                    if (!_recordsByOwner.TryGetValue(current, out var ancestorRecord))
                        continue;

                    if (!parentHandle.IsValid)
                        parentHandle = ancestorRecord.Handle;

                    rootHandle = ancestorRecord.Handle;
                    depth++;
                }

                record.ParentHandle = parentHandle;
                record.RootHandle = rootHandle;
                record.Depth = depth;
            }
        }
    }

    // ================================================================
    // UIElementStateService - UIElement縺ｮ迥ｶ諷九→險ｭ螳壹ｒ邂｡逅・☆繧九し繝ｼ繝薙せ
    // ================================================================
    //
    // ## 讎りｦ・
    //
    // UIElementStateService縺ｯ縲ゞIElement縺ｮ莉･荳九ｒ邂｡逅・☆繧九し繝ｼ繝薙せ:
    //
    // 1. **Active迥ｶ諷・*: UI繧ｷ繧ｹ繝・Β縺ｨ縺励※縺ｮ譛牙柑/辟｡蜉ｹ迥ｶ諷・
    // 2. **Visible迥ｶ諷・*: UI繧ｷ繧ｹ繝・Β縺ｨ縺励※縺ｮ陦ｨ遉ｺ/髱櫁｡ｨ遉ｺ迥ｶ諷・
    // 3. **蠖薙◆繧雁愛螳啌ectTransform**: 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ繝ｻ繝昴う繝ｳ繧ｿ繝ｼ驕ｸ謚槭↓菴ｿ逕ｨ
    // 4. **繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ險ｭ螳・*: 譁ｹ蜷代が繝ｼ繝舌・繝ｩ繧､繝峨・∈謚槫庄閭ｽ縺九←縺・°
    //
    // ## 驥崎ｦ√↑險ｭ險域晄Φ
    //
    // UI繧ｷ繧ｹ繝・Β縺ｮActive縺ｯUI蛛ｴ縺ｮ繝輔Λ繧ｰ縺ｨ繧ｹ繧ｳ繝ｼ繝輸ctive縺ｮ蜷育ｮ励・
    // BaseLifetimeScope縺ｧ縺ｯGameObject縺ｮactive迥ｶ諷九′繧ｹ繧ｳ繝ｼ繝輸ctive縺ｫ縺ｪ繧九・
    // UI蛛ｴ縺ｯActive/Visible縺ｮ繝ｭ繧ｸ繝・け繧剃ｿ晄戟縺励ヾcope縺ｮ迥ｶ諷九→蜷域・縺励※蛻､譁ｭ縺吶ｋ縲・
    //
    // ## 蠖薙◆繧雁愛螳啌ectTransform縺ｫ縺､縺・※
    //
    // UIElement縺碁∈謚槫庄閭ｽ縺九←縺・°繧堤黄逅・噪縺ｫ蛻､螳壹☆繧九◆繧√↓菴ｿ逕ｨ縲・
    //
    // ### 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ譎・
    // - RectTransform縺ｮ荳ｭ蠢・ｽ咲ｽｮ繧貞渕貅悶↓譁ｹ蜷題ｨ育ｮ励ｒ陦後≧
    // - RectTransform縺ｮ鬆伜沺縺勲ask縺ｧ螟ｧ驛ｨ蛻・ｦ・ｏ繧後※縺・◆繧牙呵｣懊°繧蛾勁螟・
    //
    // ### 繝昴う繝ｳ繧ｿ繝ｼ・医・繧ｦ繧ｹ・画凾
    // - RectTransform縺ｮ鬆伜沺蜀・↓繝昴う繝ｳ繧ｿ繝ｼ縺後≠繧九°縺ｧ蛻､螳・
    // - 隍・焚縺ｮRectTransform縺瑚ｨｭ螳壹＆繧後※縺・ｋ蝣ｴ蜷医√＞縺壹ｌ縺九↓蜷ｫ縺ｾ繧後※縺・ｌ縺ｰOK
    //
    // ### 縺ｪ縺懊Μ繧ｹ繝医°
    //
    // 隍・尅縺ｪ蠖｢迥ｶ縺ｮUIElement繧定｡ｨ迴ｾ縺吶ｋ縺溘ａ縲・
    // 萓九∴縺ｰ縲´蟄怜梛縺ｮUI縺ｯ2縺､縺ｮRectTransform縺ｧ蠖薙◆繧雁愛螳壹ｒ讒区・縺ｧ縺阪ｋ縲・
    //
    // ## 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ譁ｹ蜷代が繝ｼ繝舌・繝ｩ繧､繝峨↓縺､縺・※
    //
    // 騾壼ｸｸ縲√リ繝薙ご繝ｼ繧ｷ繝ｧ繝ｳ縺ｯ閾ｪ蜍戊ｨ育ｮ励＆繧後ｋ縺後・
    // 譏守､ｺ逧・↓縲御ｸ翫ｒ謚ｼ縺励◆繧峨％縺ｮUIElement縺ｸ縲阪ｒ謖・ｮ壹〒縺阪ｋ縲・
    //
    // 繧ｪ繝ｼ繝舌・繝ｩ繧､繝峨′險ｭ螳壹＆繧後※縺・ｋ譁ｹ蜷代・閾ｪ蜍戊ｨ育ｮ励ｈ繧雁━蜈医＆繧後ｋ縲・
    //
    // ## 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ驕ｸ謚樔ｸ榊庄縺ｫ縺､縺・※
    //
    // 荳驛ｨ縺ｮUIElement・・age縲仝indow縲￣anel縺ｪ縺ｩ・峨・
    // 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ縺ｧ驕ｸ謚槭＆繧後ｋ縺ｹ縺阪〒縺ｯ縺ｪ縺・・
    //
    // 縺薙ｌ繧峨・莉悶・UIElement繧貞桁蜷ｫ縺吶ｋ繧ｳ繝ｳ繝・リ縺ｧ縺ゅｊ縲・
    // 驕ｸ謚槫腰菴阪→縺励※縺ｯ讖溯・縺励↑縺・・
    //
    // ================================================================

    // ================================================================
    // UIElementStateChangedArgs: 迥ｶ諷句､画峩繧､繝吶Φ繝亥ｼ墓焚
    // ================================================================

    /// <summary>
    /// UIElement迥ｶ諷句､画峩譎ゅ・繧､繝吶Φ繝亥ｼ墓焚縲・
    /// 
    /// ## 逕ｨ騾・
    /// 
    /// - 螟夜Κ繧ｷ繧ｹ繝・Β縺ｸ縺ｮ迥ｶ諷句､画峩騾夂衍
    /// - 繧｢繝九Γ繝ｼ繧ｷ繝ｧ繝ｳ繧ｷ繧ｹ繝・Β縺ｨ縺ｮ騾｣謳ｺ
    /// - 繝・ヰ繝・げ/繝ｭ繧ｰ蜃ｺ蜉・
    /// </summary>
    public readonly struct UIElementStateChangedArgs
    {
        /// <summary>迥ｶ諷九ｒ謖√▽UIElement・・IElementLifetimeScope/RuntimeLifetimeScope・・/summary>
        public IScopeNode Owner { get; }

        /// <summary>螟画峩蜑阪・Active迥ｶ諷・/summary>
        public bool PreviousActive { get; }

        /// <summary>螟画峩蠕後・Active迥ｶ諷・/summary>
        public bool CurrentActive { get; }

        /// <summary>螟画峩蜑阪・Visible迥ｶ諷・/summary>
        public bool PreviousVisible { get; }

        /// <summary>螟画峩蠕後・Visible迥ｶ諷・/summary>
        public bool CurrentVisible { get; }

        /// <summary>Active迥ｶ諷九′螟画峩縺輔ｌ縺溘°縺ｩ縺・°</summary>
        public bool ActiveChanged => PreviousActive != CurrentActive;

        /// <summary>Visible迥ｶ諷九′螟画峩縺輔ｌ縺溘°縺ｩ縺・°</summary>
        public bool VisibleChanged => PreviousVisible != CurrentVisible;

        public UIElementStateChangedArgs(
            IScopeNode owner,
            bool previousActive,
            bool currentActive,
            bool previousVisible,
            bool currentVisible)
        {
            Owner = owner;
            PreviousActive = previousActive;
            CurrentActive = currentActive;
            PreviousVisible = previousVisible;
            CurrentVisible = currentVisible;
        }
    }

    // ================================================================
    // NavigationOverride: 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ譁ｹ蜷代・繧ｪ繝ｼ繝舌・繝ｩ繧､繝芽ｨｭ螳・
    // ================================================================

    /// <summary>
    /// 蜷・婿蜷代・繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ繧ｪ繝ｼ繝舌・繝ｩ繧､繝芽ｨｭ螳壹・
    /// 
    /// ## 逕ｨ騾・
    /// 
    /// 閾ｪ蜍戊ｨ育ｮ励↓繧医ｋ繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ繧剃ｸ頑嶌縺阪＠縲・
    /// 譏守､ｺ逧・↓縲後％縺ｮ譁ｹ蜷代ｒ謚ｼ縺励◆繧峨％縺ｮUIElement縺ｸ縲阪ｒ謖・ｮ壹☆繧九・
    /// 
    /// ## 險ｭ險・
    /// 
    /// null縺ｮ蝣ｴ蜷医・閾ｪ蜍戊ｨ育ｮ励↓繝輔か繝ｼ繝ｫ繝舌ャ繧ｯ縲・
    /// 險ｭ螳壹＆繧後※縺・ｋ蝣ｴ蜷医・縺昴・隕∫ｴ縺ｸ縺ｮ遘ｻ蜍輔ｒ隧ｦ縺ｿ繧・
    /// ・育ｧｻ蜍募・縺窟ctive=false縺ｪ繧臥ｧｻ蜍輔＠縺ｪ縺・ｼ峨・
    /// </summary>
    [Serializable]
    public sealed class NavigationOverride
    {
        /// <summary>
        /// 荳頑婿蜷代ｒ謚ｼ縺励◆縺ｨ縺阪・遘ｻ蜍募・縲・
        /// null縺ｮ蝣ｴ蜷医・閾ｪ蜍戊ｨ育ｮ励・
        /// UIElementStateMB 繧呈欠螳壹☆繧九・
        /// </summary>
        [Tooltip("Inspector setting.")]
        public UIElementStateMB? Up;

        /// <summary>
        /// 荳区婿蜷代ｒ謚ｼ縺励◆縺ｨ縺阪・遘ｻ蜍募・縲・
        /// null縺ｮ蝣ｴ蜷医・閾ｪ蜍戊ｨ育ｮ励・
        /// UIElementStateMB 繧呈欠螳壹☆繧九・
        /// </summary>
        [Tooltip("Inspector setting.")]
        public UIElementStateMB? Down;

        /// <summary>
        /// 蟾ｦ譁ｹ蜷代ｒ謚ｼ縺励◆縺ｨ縺阪・遘ｻ蜍募・縲・
        /// null縺ｮ蝣ｴ蜷医・閾ｪ蜍戊ｨ育ｮ励・
        /// UIElementStateMB 繧呈欠螳壹☆繧九・
        /// </summary>
        [Tooltip("Inspector setting.")]
        public UIElementStateMB? Left;

        /// <summary>
        /// 蜿ｳ譁ｹ蜷代ｒ謚ｼ縺励◆縺ｨ縺阪・遘ｻ蜍募・縲・
        /// null縺ｮ蝣ｴ蜷医・閾ｪ蜍戊ｨ育ｮ励・
        /// UIElementStateMB 繧呈欠螳壹☆繧九・
        /// </summary>
        [Tooltip("Inspector setting.")]
        public UIElementStateMB? Right;

        /// <summary>
        /// 謖・ｮ壽婿蜷代・繧ｪ繝ｼ繝舌・繝ｩ繧､繝峨ｒ蜿門ｾ励☆繧九・
        /// </summary>
        /// <param name="direction">蜿門ｾ励☆繧区婿蜷・/param>
        /// <returns>繧ｪ繝ｼ繝舌・繝ｩ繧､繝牙・縲Ｏull縺ｮ蝣ｴ蜷医・閾ｪ蜍戊ｨ育ｮ励ｒ菴ｿ逕ｨ縲・/returns>
        public IScopeNode? GetOverride(NavigateDirection direction)
        {
            var target = direction switch
            {
                NavigateDirection.Up => Up,
                NavigateDirection.Down => Down,
                NavigateDirection.Left => Left,
                NavigateDirection.Right => Right,
                _ => null
            };

            return ResolveScopeNode(target);
        }

        internal UIElementStateMB? GetOverrideElement(NavigateDirection direction)
        {
            return direction switch
            {
                NavigateDirection.Up => Up,
                NavigateDirection.Down => Down,
                NavigateDirection.Left => Left,
                NavigateDirection.Right => Right,
                _ => null
            };
        }

        /// <summary>
        /// 謖・ｮ壽婿蜷代↓繧ｪ繝ｼ繝舌・繝ｩ繧､繝峨′險ｭ螳壹＆繧後※縺・ｋ縺九←縺・°縲・
        /// </summary>
        public bool HasOverride(NavigateDirection direction)
        {
            return GetOverride(direction) != null;
        }

        static IScopeNode? ResolveScopeNode(UIElementStateMB? target)
        {
            if (target == null)
                return null;

            if (ScopeFeatureInstallerUtility.TryGetNearestScopeNode(target, includeInactive: true, out var owner))
                return owner;

            return null;
        }
    }

    // ================================================================
    // IUIElementState: UIElement迥ｶ諷九・隱ｭ縺ｿ蜿悶ｊInterface
    // ================================================================

    /// <summary>
    /// UIElement縺ｮ迥ｶ諷九ｒ隱ｭ縺ｿ蜿悶ｋ繧､繝ｳ繧ｿ繝ｼ繝輔ぉ繝ｼ繧ｹ縲・
    /// 
    /// ## 蠖ｹ蜑ｲ
    /// 
    /// 螟夜Κ繧ｷ繧ｹ繝・Β縺袈IElement縺ｮ迥ｶ諷九ｒ蜿門ｾ励☆繧九◆繧√・隱ｭ縺ｿ蜿悶ｊ蟆ら畑API縲・
    /// 迥ｶ諷九・螟画峩縺ｯIUIElementStateController繧帝壹§縺ｦ陦後≧縲・
    /// 
    /// ## 菴ｿ逕ｨ繧ｷ繝ｼ繝ｳ
    /// 
    /// - 驕ｸ謚槫・逅・凾縺ｮ繝輔ぅ繝ｫ繧ｿ繝ｪ繝ｳ繧ｰ
    /// - 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ蛟呵｣懊・邨槭ｊ霎ｼ縺ｿ
    /// - 謠冗判繧ｷ繧ｹ繝・Β縺ｨ縺ｮ騾｣謳ｺ
    /// - 螟夜Κ繧ｷ繧ｹ繝・Β縺九ｉ縺ｮ迥ｶ諷狗｢ｺ隱・
    /// </summary>
    public interface IUIElementState
    {
        // ----------------------------------------------------------------
        // Active/Visible迥ｶ諷・
        // ----------------------------------------------------------------

        /// <summary>
        /// 縺薙・UIElement縺窟ctive縺九←縺・°縲・
        /// 
        /// ## Active=false縺ｮ蝣ｴ蜷・
        /// 
        /// - 驕ｸ謚橸ｼ・elect・牙ｯｾ雎｡縺九ｉ髯､螟悶＆繧後ｋ
        /// - 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ蛟呵｣懊°繧蛾勁螟悶＆繧後ｋ
        /// - 蜈･蜉帙う繝吶Φ繝医ｒ蜿励￠蜿悶ｉ縺ｪ縺・
        /// - 縺溘□縺励；ameObject閾ｪ菴薙・active=true縺ｮ縺ｾ縺ｾ
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// 縺薙・UIElement縺瑚｡ｨ遉ｺ縺輔ｌ繧九°縺ｩ縺・°縲・
        /// 
        /// ## Visible=false縺ｮ蝣ｴ蜷・
        /// 
        /// - 邨ｶ蟇ｾ縺ｫ謠冗判縺輔ｌ縺ｪ縺・
        /// - Mask遲峨・莉悶・隕∝屏縺ｫ髢｢菫ゅ↑縺城撼陦ｨ遉ｺ
        /// - Active迥ｶ諷九↓縺ｯ蠖ｱ髻ｿ縺励↑縺・
        /// 
        /// ## 豕ｨ諢・
        /// 
        /// Visible=true縺ｧ繧ゆｻ悶・隕∝屏縺ｧ隕九∴縺ｪ縺上↑繧九％縺ｨ縺後≠繧・
        /// - Mask縺ｫ繧医ｋ驕ｮ阡ｽ
        /// - Canvas螟悶↓縺・ｋ
        /// - 莉悶・UI縺ｫ隕・ｏ繧後※縺・ｋ
        /// - 繧｢繝ｫ繝輔ぃ縺・
        /// </summary>
        bool IsVisible { get; }

        /// <summary>
        /// 縺薙・UIElement縺悟ｮ溯ｳｪ逧・↓Active縺九←縺・°縲・
        /// 
        /// ## 險育ｮ励Ο繧ｸ繝・け
        /// 
        /// 閾ｪ霄ｫ縺ｮActive迥ｶ諷九↓蜉縺医∬ｦｪ縺ｮActive迥ｶ諷九ｂ閠・・縺励◆邨先棡縲・
        /// 隕ｪ縺窟ctive=false縺ｪ繧峨∬・霄ｫ縺窟ctive=true縺ｧ繧Ｇalse繧定ｿ斐☆縲・
        /// 
        /// ## 逕ｨ騾・
        /// 
        /// 驕ｸ謚槫愛螳壹ｄ繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ蛻､螳壹〒縺ｯ縲√％縺ｮ繝励Ο繝代ユ繧｣繧剃ｽｿ逕ｨ縺吶ｋ縲・
        /// 隕ｪ縺窟ctive縺ｧ縺ｪ縺代ｌ縺ｰ蟄舌ｂ螳溯ｳｪ逧・↓Active縺ｧ縺ｯ縺ｪ縺・・
        /// </summary>
        bool IsEffectivelyActive { get; }

        /// <summary>
        /// 蜈･蜉帛女莉伜庄閭ｽ縺九←縺・°縲・
        ///
        /// Active/Visible 縺ｫ蜉縺医※縲´ifecycle 縺ｮ despawn 貍泌・荳ｭ縺九ｂ閠・・縺吶ｋ縲・
        /// </summary>
        bool AcceptsInput { get; }

        // ----------------------------------------------------------------
        // 蠖薙◆繧雁愛螳・
        // ----------------------------------------------------------------

        /// <summary>
        /// 蠖薙◆繧雁愛螳壹↓菴ｿ逕ｨ縺吶ｋRectTransform縺ｮ繝ｪ繧ｹ繝医・
        /// 
        /// ## 逕ｨ騾・
        /// 
        /// - 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ譎ゅ・霍晞屬繝ｻ譁ｹ蜷題ｨ育ｮ・
        /// - 繝昴う繝ｳ繧ｿ繝ｼ・医・繧ｦ繧ｹ・峨ヲ繝・ヨ繝・せ繝・
        /// - Mask驕ｮ阡ｽ邇・・險育ｮ・
        /// 
        /// ## 隍・焚險ｭ螳壹・諢丞袖
        /// 
        /// 隍・尅縺ｪ蠖｢迥ｶ縺ｮUIElement繧定｡ｨ迴ｾ縺吶ｋ縺溘ａ縺ｫ隍・焚縺ｮRectTransform繧定ｨｭ螳壼庄閭ｽ縲・
        /// 縺・★繧後°縺ｮRectTransform縺ｫ繝偵ャ繝医☆繧後・縲√◎縺ｮUIElement縺ｫ繝偵ャ繝医＠縺溘→縺ｿ縺ｪ縺吶・
        /// </summary>
        IReadOnlyList<RectTransform> HitTestRects { get; }


        /// <summary>
        /// 驕ｸ謚槫━蜈亥ｺｦ縲・
        /// 
        /// ## 逕ｨ騾・
        /// 
        /// 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ蛟呵｣懊′隍・焚縺ゅｋ蝣ｴ蜷医・蜆ｪ蜈磯・ｽ堺ｻ倥￠縲・
        /// 
        /// ## 謨ｰ蛟､縺ｮ諢丞袖
        /// 
        /// 謨ｰ蛟､縺悟､ｧ縺阪＞縺ｻ縺ｩ蜆ｪ蜈亥ｺｦ縺碁ｫ倥＞縲・
        /// 
        int SelectionOrder { get; }

        /// <summary>
        /// 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ蟆ら畑縺ｮ蜆ｪ蜈亥ｺｦ縲・
        /// 謨ｰ蛟､縺悟､ｧ縺阪＞縺ｻ縺ｩ蜆ｪ蜈医＆繧後ｋ縲・
        /// </summary>
        int NavigationSelectionOrder { get; }

        // ----------------------------------------------------------------
        // 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ險ｭ螳・
        // ----------------------------------------------------------------

        /// <summary>
        /// 縺薙・UIElement閾ｪ菴薙′驕ｸ謚槫ｯｾ雎｡縺ｫ縺ｪ繧後ｋ譚｡莉ｶ縲・
        /// false 縺ｮ蝣ｴ蜷医√・繧､繝ｳ繧ｿ繝ｼ/繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ/逶ｴ謗･驕ｸ謚槭・縺吶∋縺ｦ縺九ｉ髯､螟悶＆繧後ｋ縲・
        /// </summary>
        Game.Common.DynamicValue<bool> IsSelectable { get; }

        /// <summary>
        /// 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ・医く繝ｼ繝懊・繝・繧ｲ繝ｼ繝繝代ャ繝会ｼ蛾∈謚槫庄閭ｽ譚｡莉ｶ・・ynamicValue&lt;bool&gt;・峨・
        /// 
        /// 蜍慕噪縺ｫ驕ｸ謚槫庄閭ｽ諤ｧ繧定ｩ穂ｾ｡縺吶ｋ譚｡莉ｶ縲・
        /// Blackboard縲ヾcalar縲〃arStore縲・xpression 縺ｪ縺ｩ隍・焚縺ｮ蛟､貅舌ｒ繧ｵ繝昴・繝医・
        /// 
        /// ## false繧定ｿ斐☆蝣ｴ蜷・
        /// 
        /// - 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ縺ｫ繧医ｋ驕ｸ謚槫呵｣懊°繧牙ｮ悟・縺ｫ髯､螟悶＆繧後ｋ
        /// - 繝昴う繝ｳ繧ｿ繝ｼ・医・繧ｦ繧ｹ・峨↓繧医ｋ驕ｸ謚槭・蜿ｯ閭ｽ
        /// - Active迥ｶ諷九→縺ｯ迢ｬ遶九＠縺溯ｨｭ螳・
        /// 
        /// ## 逕ｨ騾・
        /// 
        /// Page縲仝indow縲￣anel縺ｪ縺ｩ縺ｮ繧ｳ繝ｳ繝・リ隕∫ｴ縺ｫ險ｭ螳壹・
        /// 縺薙ｌ繧峨・繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ縺ｧ逶ｴ謗･驕ｸ謚槭＆繧後ｋ縺ｹ縺阪〒縺ｯ縺ｪ縺・・
        /// </summary>
        Game.Common.DynamicValue<bool> IsNavigationSelectable { get; }

        /// <summary>
        /// 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ譁ｹ蜷代・繧ｪ繝ｼ繝舌・繝ｩ繧､繝芽ｨｭ螳壹・
        /// 
        /// ## 逕ｨ騾・
        /// 
        /// 閾ｪ蜍戊ｨ育ｮ励↓繧医ｋ繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ繧剃ｸ頑嶌縺阪＠縲・
        /// 譏守､ｺ逧・↑遘ｻ蜍募・繧呈欠螳壹☆繧九・
        /// 
        /// ## null縺ｮ蝣ｴ蜷・
        /// 
        /// 縺吶∋縺ｦ縺ｮ譁ｹ蜷代〒閾ｪ蜍戊ｨ育ｮ励ｒ菴ｿ逕ｨ縲・
        /// </summary>
        NavigationOverride? NavigationOverride { get; }

        // ----------------------------------------------------------------
        // 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ隧穂ｾ｡繝｡繧ｽ繝・ラ
        // ----------------------------------------------------------------

        /// <summary>
        /// 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ驕ｸ謚槫庄閭ｽ譚｡莉ｶ繧定ｩ穂ｾ｡縺吶ｋ縲・
        /// </summary>
        /// <returns>繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ驕ｸ謚槫庄閭ｽ縺ｪ蝣ｴ蜷・rue</returns>
        bool EvaluateIsSelectable();

        /// <summary>
        /// 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ驕ｸ謚槫庄閭ｽ譚｡莉ｶ繧定ｩ穂ｾ｡縺吶ｋ縲・
        /// </summary>
        /// <returns>繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ驕ｸ謚槫庄閭ｽ縺ｪ蝣ｴ蜷・rue</returns>
        bool EvaluateIsNavigationSelectable();

        // ----------------------------------------------------------------
        // 謇譛芽・
        // ----------------------------------------------------------------

        /// <summary>
        /// 縺薙・UIElement繧呈園譛峨☆繧紀ScopeNode縲・
        /// </summary>
        IScopeNode? Owner { get; }

        // ----------------------------------------------------------------
        // 繧､繝吶Φ繝・
        // ----------------------------------------------------------------

        /// <summary>
        /// 迥ｶ諷九′螟画峩縺輔ｌ縺溘→縺阪↓逋ｺ轣ｫ縺吶ｋ繧､繝吶Φ繝医・
        /// 
        /// ## 逋ｺ轣ｫ繧ｿ繧､繝溘Φ繧ｰ
        /// 
        /// - Active迥ｶ諷九′螟画峩縺輔ｌ縺溘→縺・
        /// - Visible迥ｶ諷九′螟画峩縺輔ｌ縺溘→縺・
        /// 
        /// ## 豕ｨ諢・
        /// 
        /// HitTestRects繧НavigationOverride縺ｮ螟画峩縺ｧ縺ｯ逋ｺ轣ｫ縺励↑縺・・
        /// </summary>
        event Action<UIElementStateChangedArgs>? OnStateChanged;
    }

    // ================================================================
    // IUIElementStateController: UIElement迥ｶ諷九・蛻ｶ蠕｡Interface
    // ================================================================

    /// <summary>
    /// UIElement縺ｮ迥ｶ諷九ｒ蛻ｶ蠕｡縺吶ｋ繧､繝ｳ繧ｿ繝ｼ繝輔ぉ繝ｼ繧ｹ縲・
    /// 
    /// ## 蠖ｹ蜑ｲ
    /// 
    /// UIElement縺ｮ迥ｶ諷九ｒ螟画峩縺吶ｋ縺溘ａ縺ｮAPI縲・
    /// IUIElementState繧堤ｶ呎価縺励∬ｪｭ縺ｿ蜿悶ｊ縺ｨ蛻ｶ蠕｡縺ｮ荳｡譁ｹ繧呈署萓帙・
    /// 
    /// ## 險ｭ險域婿驥・
    /// 
    /// 隱ｭ縺ｿ蜿悶ｊ縺ｨ蛻ｶ蠕｡繧貞・髮｢縺吶ｋ縺薙→縺ｧ縲・
    /// 螟夜Κ縺九ｉ縺ｮ荳肴ｭ｣縺ｪ迥ｶ諷句､画峩繧帝亟縺舌・
    /// 
    /// 騾壼ｸｸ縺ｮ繧ｷ繧ｹ繝・Β縺ｯIUIElementState縺ｮ縺ｿ繧貞盾辣ｧ縺励・
    /// 迥ｶ諷九ｒ螟画峩縺吶ｋ蠢・ｦ√′縺ゅｋ繧ｷ繧ｹ繝・Β縺ｮ縺ｿ縺後％縺ｮ繧､繝ｳ繧ｿ繝ｼ繝輔ぉ繝ｼ繧ｹ繧剃ｽｿ逕ｨ縲・
    /// </summary>
    public interface IUIElementStateController : IUIElementState
    {
        // ----------------------------------------------------------------
        // Active/Visible蛻ｶ蠕｡
        // ----------------------------------------------------------------

        /// <summary>
        /// Active迥ｶ諷九ｒ險ｭ螳壹☆繧九・
        /// 
        /// ## 蜉ｹ譫・
        /// 
        /// - true縺ｫ險ｭ螳・ 驕ｸ謚槫庄閭ｽ縺ｫ縺ｪ繧九∝・蜉帙ｒ蜿励￠莉倥￠繧・
        /// - false縺ｫ險ｭ螳・ 驕ｸ謚樔ｸ榊庄縺ｫ縺ｪ繧九∝・蜉帙ｒ蜿励￠莉倥￠縺ｪ縺・
        /// - OnStateChanged繧､繝吶Φ繝医′逋ｺ轣ｫ縺吶ｋ
        /// </summary>
        /// <param name="active">譁ｰ縺励＞Active迥ｶ諷・/param>
        void SetActive(bool active);

        /// <summary>
        /// Visible迥ｶ諷九ｒ險ｭ螳壹☆繧九・
        /// 
        /// ## 蜉ｹ譫・
        /// 
        /// - true縺ｫ險ｭ螳・ 謠冗判縺輔ｌ繧句庄閭ｽ諤ｧ縺後≠繧・
        /// - false縺ｫ險ｭ螳・ 邨ｶ蟇ｾ縺ｫ謠冗判縺輔ｌ縺ｪ縺・
        /// - OnStateChanged繧､繝吶Φ繝医′逋ｺ轣ｫ縺吶ｋ
        /// </summary>
        /// <param name="visible">譁ｰ縺励＞Visible迥ｶ諷・/param>
        void SetVisible(bool visible);

        /// <summary>
        /// Active迥ｶ諷九ｒ繝医げ繝ｫ縺吶ｋ縲・
        /// </summary>
        void ToggleActive();

        /// <summary>
        /// Visible迥ｶ諷九ｒ繝医げ繝ｫ縺吶ｋ縲・
        /// </summary>
        void ToggleVisible();
    }

    // ================================================================
    // UIElementStateService: 繝｡繧､繝ｳ螳溯｣・
    // ================================================================

    /// <summary>
    /// UIElement縺ｮ迥ｶ諷九ｒ邂｡逅・☆繧九し繝ｼ繝薙せ縲・
    /// 
    /// ## 逋ｻ骭ｲ譁ｹ豕・
    /// 
    /// UIElementStateMB繧帝壹§縺ｦLifetimeScope縺ｫ逋ｻ骭ｲ縺輔ｌ繧九・
    /// UIElementStateMB縺熊eatureInstaller縺ｨ縺励※讖溯・縺励・
    /// 縺薙・繧ｵ繝ｼ繝薙せ繧奪I繧ｳ繝ｳ繝・リ縺ｫ逋ｻ骭ｲ縺吶ｋ縲・
    /// 
    /// ## 雋ｬ蜍・
    /// 
    /// 1. Active/Visible迥ｶ諷九・菫晄戟縺ｨ螟画峩騾夂衍
    /// 2. IsEffectivelyActive縺ｮ險育ｮ暦ｼ郁ｦｪ縺ｮ迥ｶ諷九ｒ閠・・・・
    /// 3. 蠖薙◆繧雁愛螳夂畑RectTransform繝ｪ繧ｹ繝医・菫晄戟
    /// 4. 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ險ｭ螳壹・菫晄戟
    /// 
    /// ## 萓晏ｭ倬未菫・
    /// 
    /// 縺薙・繧ｵ繝ｼ繝薙せ縺ｯUIElementStateMB縺九ｉ蛻晄悄險ｭ螳壹ｒ蜿励￠蜿悶ｋ縲・
    /// 險ｭ螳壹・螟画峩縺ｯUIElementStateMB繧帝壹§縺ｦ陦後ｏ繧後・
    /// 縺薙・繧ｵ繝ｼ繝薙せ縺ｫ蜿肴丐縺輔ｌ繧九・
    /// </summary>
    public sealed class UIElementStateService : IUIElementStateController, IUIModalRoot, IUIHandleNode, IScopeAcquireHandler, IScopeReleaseHandler
    {
        // ----------------------------------------------------------------
        // 繝輔ぅ繝ｼ繝ｫ繝・
        // ----------------------------------------------------------------

        /// <summary>謇譛芽・せ繧ｳ繝ｼ繝・/summary>
        readonly IScopeNode _owner;

        /// <summary>Active迥ｶ諷・/summary>
        bool _isActive = true;

        /// <summary>Visible迥ｶ諷・/summary>
        bool _isVisible = true;

        /// <summary>蠖薙◆繧雁愛螳壹↓菴ｿ逕ｨ縺吶ｋRectTransform縺ｮ繝ｪ繧ｹ繝・/summary>
        readonly List<RectTransform> _hitTestRects = new();

        /// <summary>繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ縺ｧ驕ｸ謚槫庄閭ｽ縺九←縺・°</summary>
        /// <summary>縺薙・UIElement閾ｪ菴薙′驕ｸ謚槫庄閭ｽ縺九ｒ豎ｺ繧√ｋ譚｡莉ｶ・・ynamicValue<bool>・・/summary>
        Game.Common.DynamicValue<bool> _isSelectableCondition;

        /// <summary>繧ｭ繝｣繝・す繝･縺輔ｌ縺滄∈謚槫庄閭ｽ繝輔Λ繧ｰ</summary>
        bool _isSelectableCached = true;

        /// <summary>繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ縺ｧ驕ｸ謚槫庄閭ｽ縺九←縺・°繧呈ｱｺ繧√ｋ譚｡莉ｶ・・ynamicValue<bool>・・/summary>
        /// <summary>繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ縺ｧ驕ｸ謚槫庄閭ｽ縺九ｒ豎ｺ繧√ｋ譚｡莉ｶ・・ynamicValue<bool>・・/summary>
        Game.Common.DynamicValue<bool> _isNavigationSelectableCondition;

        /// <summary>繧ｭ繝｣繝・す繝･縺輔ｌ縺溘リ繝薙ご繝ｼ繧ｷ繝ｧ繝ｳ驕ｸ謚槫庄閭ｽ繝輔Λ繧ｰ</summary>
        bool _isNavigationSelectableCached = true;

        /// <summary>繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ譁ｹ蜷代・繧ｪ繝ｼ繝舌・繝ｩ繧､繝芽ｨｭ螳・/summary>
        NavigationOverride? _navigationOverride;

        /// <summary>驕ｸ謚槫━蜈亥ｺｦ</summary>
        int _selectionOrder = 0;

        /// <summary>繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ蟆ら畑蜆ｪ蜈亥ｺｦ</summary>
        int _navigationSelectionOrder = 0;

        /// <summary>驕ｸ謚樊凾縺ｫ螳溯｡後☆繧九さ繝槭Φ繝峨Μ繧ｹ繝・/summary>
        readonly VNext.CommandListData _onSelectedCommands;

        /// <summary>驕ｸ謚櫁ｧ｣髯､譎ゅ↓螳溯｡後☆繧九さ繝槭Φ繝峨Μ繧ｹ繝・/summary>
        readonly VNext.CommandListData _onDeselectedCommands;

        /// <summary>UISelectionService縺ｮ蜿ら・・磯∈謚樒屮隕也畑・・/summary>
        IUISelectionState? _selectionState;

        /// <summary>繧ｳ繝槭Φ繝牙ｮ溯｡檎畑Runner</summary>
        VNext.ICommandRunner? _commandRunner;

        /// <summary>蜑榊屓縺ｮ驕ｸ謚樒憾諷具ｼ郁・蛻・′驕ｸ謚槭＆繧後※縺・◆縺具ｼ・/summary>
        bool _wasSelected;

        /// <summary>繧ｳ繝槭Φ繝牙ｮ溯｡檎畑CancellationTokenSource</summary>
        CancellationTokenSource? _commandCts;

        /// <summary>隕ｪ縺ｮUIElementState繧ｭ繝｣繝・す繝･・・sEffectivelyActive譛驕ｩ蛹也畑・・/summary>
        IUIElementState? _cachedParentState;
        bool _parentStateCacheResolved;
        IScopeNode? _cachedParentScope;

        /// <summary>IsEffectivelyActive縺ｮ繧ｭ繝｣繝・す繝･</summary>
        bool _cachedEffectivelyActive;

        /// <summary>IsEffectivelyActive縺ｮDirty繝輔Λ繧ｰ</summary>
        bool _effectiveActiveDirty = true;

        /// <summary>Owner縺ｮActive迥ｶ諷九く繝｣繝・す繝･</summary>
        bool _lastOwnerActive;

        /// <summary>Lifecycle 縺ｮ despawn 迥ｶ諷句盾辣ｧ</summary>
        IScopeLifecycleService? _lifecycleService;

        /// <summary>UI graph registry</summary>
        IUINodeGraphService? _nodeGraph;

        /// <summary>Registered UI node handle</summary>
        UINodeHandle _nodeHandle;

        // ----------------------------------------------------------------
        // 繝励Ο繝代ユ繧｣ - Active/Visible
        // ----------------------------------------------------------------

        /// <inheritdoc/>
        public bool IsActive => _isActive && _owner.IsActive;

        /// <inheritdoc/>
        public bool IsVisible => _isVisible;

        /// <inheritdoc/>
        public IScopeNode? Owner => _owner;

        public UINodeHandle NodeHandle => _nodeHandle;

        /// <inheritdoc/>
        public bool AcceptsInput => IsVisible && IsEffectivelyActive && !IsLifecycleDespawning();

        string IUIModalRoot.ModalId => _owner.Identity?.SelfTransform != null
            ? _owner.Identity.SelfTransform.name
            : "(unknown)";

        bool IUIModalRoot.IsActive => IsEffectivelyActive;

        IScopeNode? IUIModalRoot.OwnerScope => _owner;

        UINodeHandle IUIModalRoot.OwnerHandle => _nodeHandle;

        bool IUIModalRoot.IsDescendant(IScopeNode? target)
        {
            if (target == null)
                return false;

            if (_nodeGraph != null && _nodeHandle.IsValid && _nodeGraph.TryGetHandle(target, out var targetHandle))
                return _nodeGraph.IsSameOrDescendant(targetHandle, _nodeHandle);

            var current = target;
            while (current != null)
            {
                if (ReferenceEquals(current, _owner))
                    return true;
                current = current.Parent;
            }

            return false;
        }

        bool IUIModalRoot.IsDescendant(UINodeHandle target)
        {
            if (!target.IsValid || _nodeGraph == null || !_nodeHandle.IsValid)
                return false;

            return _nodeGraph.IsSameOrDescendant(target, _nodeHandle);
        }

        /// <inheritdoc/>
        public bool IsEffectivelyActive
        {
            get
            {
                var ownerActive = _owner.IsActive;
                if (!_effectiveActiveDirty && _lastOwnerActive == ownerActive)
                    return _cachedEffectivelyActive;

                _lastOwnerActive = ownerActive;
                EnsureParentStateCache();

                var selfActive = _isActive && ownerActive;

                // 閾ｪ霄ｫ縺窟ctive縺ｧ縺ｪ縺代ｌ縺ｰfalse
                if (!selfActive)
                {
                    _cachedEffectivelyActive = false;
                    _effectiveActiveDirty = false;
                    return false;
                }

                if (_cachedParentState != null)
                    _cachedEffectivelyActive = _cachedParentState.IsEffectivelyActive && selfActive;
                else
                    _cachedEffectivelyActive = selfActive;

                _effectiveActiveDirty = false;
                return _cachedEffectivelyActive;
            }
        }

        // ----------------------------------------------------------------
        // 繝励Ο繝代ユ繧｣ - 蠖薙◆繧雁愛螳・
        // ----------------------------------------------------------------

        /// <inheritdoc/>
        public IReadOnlyList<RectTransform> HitTestRects => _hitTestRects;

        /// <inheritdoc/>
        public int SelectionOrder => _selectionOrder;

        /// <inheritdoc/>
        public int NavigationSelectionOrder => _navigationSelectionOrder;

        // ----------------------------------------------------------------
        // 繝励Ο繝代ユ繧｣ - 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ
        // ----------------------------------------------------------------

        /// <inheritdoc/>
        public Game.Common.DynamicValue<bool> IsSelectable => _isSelectableCondition;

        /// <inheritdoc/>
        public Game.Common.DynamicValue<bool> IsNavigationSelectable => _isNavigationSelectableCondition;

        /// <inheritdoc/>
        public NavigationOverride? NavigationOverride => _navigationOverride;

        // ----------------------------------------------------------------
        // 繧､繝吶Φ繝・
        // ----------------------------------------------------------------

        /// <inheritdoc/>
        public event Action<UIElementStateChangedArgs>? OnStateChanged;

        // ----------------------------------------------------------------
        // 驕ｸ謚槭う繝吶Φ繝医さ繝槭Φ繝・
        // ----------------------------------------------------------------

        /// <summary>
        /// 驕ｸ謚樊凾縺ｫ螳溯｡後☆繧九さ繝槭Φ繝峨Μ繧ｹ繝医・
        /// Set/Add/Remove/Swap 縺ｧ謫堺ｽ懷庄閭ｽ縲・
        /// </summary>
        public VNext.CommandListData OnSelectedCommands => _onSelectedCommands;

        /// <summary>
        /// 驕ｸ謚櫁ｧ｣髯､譎ゅ↓螳溯｡後☆繧九さ繝槭Φ繝峨Μ繧ｹ繝医・
        /// Set/Add/Remove/Swap 縺ｧ謫堺ｽ懷庄閭ｽ縲・
        /// </summary>
        public VNext.CommandListData OnDeselectedCommands => _onDeselectedCommands;

        // ----------------------------------------------------------------
        // 繧ｳ繝ｳ繧ｹ繝医Λ繧ｯ繧ｿ
        // ----------------------------------------------------------------

        /// <summary>
        /// 繧ｳ繝ｳ繧ｹ繝医Λ繧ｯ繧ｿ縲・
        /// 
        /// ## 繝代Λ繝｡繝ｼ繧ｿ
        /// 
        /// owner: 縺薙・繧ｵ繝ｼ繝薙せ繧呈戟縺､IScopeNode・・IElementLifetimeScope/RuntimeLifetimeScope・・
        /// </summary>
        /// <param name="owner">謇譛芽・・繧ｹ繧ｳ繝ｼ繝励ヮ繝ｼ繝・/param>
        public UIElementStateService(IScopeNode owner, IUIElementStateOptions options, IUISelectionState? selectionState, VNext.ICommandRunner commandRunner)
        {
            _owner = owner;

            // selectionState may not be registered in this scope at construction time.
            // Accept nullable and try to resolve later in OnAcquire if needed.
            _selectionState = selectionState;
            _commandRunner = commandRunner;

            // 蛻晄悄險ｭ螳壹ｒ蜿肴丐
            _isSelectableCondition = options.IsSelectable;
            _isNavigationSelectableCondition = options.IsNavigationSelectable;
            _navigationOverride = options.NavigationOverride;
            _onSelectedCommands = options.OnSelectedCommands ?? new VNext.CommandListData();
            _onDeselectedCommands = options.OnDeselectedCommands ?? new VNext.CommandListData();
            SetHitTestRects(options.HitTestRects);

            _selectionOrder = options.SelectionOrder;
            _navigationSelectionOrder = options.NavigationSelectionOrder;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _effectiveActiveDirty = true;
            EnsureParentStateCache();
            scope.TryResolveInAncestors<IScopeLifecycleService>(out _lifecycleService);

            if (_nodeGraph == null)
                scope.TryResolveInAncestors<IUINodeGraphService>(out _nodeGraph);

            if (_nodeGraph != null && !_nodeHandle.IsValid)
                _nodeHandle = _nodeGraph.RegisterNode(_owner, this, ResolveOwnerComponent(), _navigationOverride);

            // selectionState may not have been available at construction time; try resolve from scope's container
            if (_selectionState == null)
            {
                if (scope.Resolver != null && scope.Resolver.TryResolve<IUISelectionState>(out var ss))
                    _selectionState = ss;
            }

            // 蛻晄悄蛹門・逅・ 驕主悉縺ｮ雉ｼ隱ｭ繧貞､悶＠縺ｦ縺九ｉ蜀咲匳骭ｲ縺吶ｋ・亥､夐㍾逋ｻ骭ｲ繧帝亟縺撰ｼ・
            if (_selectionState != null)
            {
                _selectionState.OnSelectionChanged -= HandleSelectionChanged;
                _selectionState.OnSelectionChanged += HandleSelectionChanged;

                // 蛻晄悄驕ｸ謚樒憾諷九ｒ蜿肴丐
                _wasSelected = ReferenceEquals(_selectionState?.CurrentElement, _owner);
            }
        }

        public void OnRelease(IScopeNode scope, bool isDestroy)
        {
            // 繧ｯ繝ｪ繝ｼ繝ｳ繧｢繝・・蜃ｦ逅・ 雉ｼ隱ｭ繧定ｧ｣髯､
            if (_selectionState != null)
            {
                _selectionState.OnSelectionChanged -= HandleSelectionChanged;
            }

            if (_nodeGraph != null && _nodeHandle.IsValid)
            {
                _nodeGraph.UnregisterNode(_nodeHandle, _owner, ResolveOwnerComponent());
                _nodeHandle = UINodeHandle.Invalid;
            }

            UnbindParentState();
            _parentStateCacheResolved = false;
            _effectiveActiveDirty = true;
            _lifecycleService = null;
        }

        UIElementStateMB? ResolveOwnerComponent()
        {
            return _owner.Identity?.SelfTransform != null
                ? _owner.Identity.SelfTransform.GetComponent<UIElementStateMB>()
                : null;
        }




        /// <summary>
        /// 驕ｸ謚槫､画峩譎ゅ・繝上Φ繝峨Λ縲・
        /// 閾ｪ蛻・′驕ｸ謚槭＆繧後◆縺九・∈謚櫁ｧ｣髯､縺輔ｌ縺溘°繧貞愛螳壹＠縺ｦ繧ｳ繝槭Φ繝峨ｒ螳溯｡後☆繧九・
        /// </summary>
        void HandleSelectionChanged(IScopeNode? newSelection)
        {
            bool wasSelected = _wasSelected;
            bool isNowSelected = ReferenceEquals(newSelection, _owner);

            // 迥ｶ諷九′螟牙喧縺励※縺・↑縺・ｴ蜷医・菴輔ｂ縺励↑縺・
            if (wasSelected == isNowSelected)
            {
                return;
            }

            _wasSelected = isNowSelected;

            if (isNowSelected)
            {
                // 驕ｸ謚槭＆繧後◆
                ExecuteOnSelectedCommands().Forget();
            }
            else
            {
                // 驕ｸ謚櫁ｧ｣髯､縺輔ｌ縺・
                ExecuteOnDeselectedCommands().Forget();
            }
        }

        /// <summary>
        /// 驕ｸ謚樊凾繧ｳ繝槭Φ繝峨ｒ螳溯｡後☆繧九・
        /// </summary>
        async UniTaskVoid ExecuteOnSelectedCommands()
        {
            if (_commandRunner == null) return;
            if (_onSelectedCommands.Count == 0) return;

            // 譌｢蟄倥・螳溯｡後ｒ繧ｭ繝｣繝ｳ繧ｻ繝ｫ
            _commandCts?.Cancel();
            _commandCts?.Dispose();
            _commandCts = new CancellationTokenSource();

            var options = VNext.CommandRunOptions.Default;
            var ctx = new VNext.CommandContext(_owner, NullVarStore.Instance, _commandRunner, _owner, options);

            try
            {
                var result = await _commandRunner.ExecuteListAsync(_onSelectedCommands, ctx, _commandCts.Token, options);
                if (result.Status == VNext.CommandRunStatus.Error)
                    Debug.LogError($"[UIElementStateService] OnSelected command failed: {result.Message}");
            }
            catch (OperationCanceledException)
            {
                // 繧ｭ繝｣繝ｳ繧ｻ繝ｫ縺ｯ豁｣蟶ｸ邨ゆｺ・
            }
        }

        /// <summary>
        /// 驕ｸ謚櫁ｧ｣髯､譎ゅさ繝槭Φ繝峨ｒ螳溯｡後☆繧九・
        /// </summary>
        async UniTaskVoid ExecuteOnDeselectedCommands()
        {
            if (_commandRunner == null) return;
            if (_onDeselectedCommands.Count == 0) return;

            // 譌｢蟄倥・螳溯｡後ｒ繧ｭ繝｣繝ｳ繧ｻ繝ｫ
            _commandCts?.Cancel();
            _commandCts?.Dispose();
            _commandCts = new CancellationTokenSource();

            var options = VNext.CommandRunOptions.Default;
            var ctx = new VNext.CommandContext(_owner, NullVarStore.Instance, _commandRunner, _owner, options);

            try
            {
                var result = await _commandRunner.ExecuteListAsync(_onDeselectedCommands, ctx, _commandCts.Token, options);
                if (result.Status == VNext.CommandRunStatus.Error)
                    Debug.LogError($"[UIElementStateService] OnDeselected command failed: {result.Message}");
            }
            catch (OperationCanceledException)
            {
                // 繧ｭ繝｣繝ｳ繧ｻ繝ｫ縺ｯ豁｣蟶ｸ邨ゆｺ・
            }
        }

        // ----------------------------------------------------------------
        // Active/Visible蛻ｶ蠕｡
        // ----------------------------------------------------------------

        /// <inheritdoc/>
        public void SetActive(bool active)
        {
            if (_isActive == active) return;

            var prevActive = IsActive;
            _isActive = active;
            var currentActive = IsActive;

            _effectiveActiveDirty = true;

            NotifyStateChanged(prevActive, currentActive, _isVisible, _isVisible);

            Debug.Log($"[UIElementStateService] '{_owner.Identity?.SelfTransform.name}' Active changed: {prevActive} -> {active}");
        }

        /// <inheritdoc/>
        public void SetVisible(bool visible)
        {
            if (_isVisible == visible) return;

            var prevVisible = _isVisible;
            _isVisible = visible;

            _effectiveActiveDirty = true;

            NotifyStateChanged(_isActive, _isActive, prevVisible, _isVisible);

            //Debug.Log($"[UIElementStateService] '{_owner.Identity?.SelfTransform.name}' Visible changed: {prevVisible} -> {visible}");
        }

        /// <inheritdoc/>
        public void ToggleActive()
        {
            SetActive(!_isActive);
        }

        /// <inheritdoc/>
        public void ToggleVisible()
        {
            SetVisible(!_isVisible);
        }

        // ----------------------------------------------------------------
        // 險ｭ螳壹Γ繧ｽ繝・ラ・・B縺九ｉ蜻ｼ縺ｰ繧後ｋ・・
        // ----------------------------------------------------------------

        /// <summary>
        /// 蠖薙◆繧雁愛螳夂畑RectTransform繧定ｨｭ螳壹☆繧九・
        /// 
        /// ## 蜻ｼ縺ｳ蜃ｺ縺怜・
        /// 
        /// UIElementStateMB縺ｮInstallFeature縺ｧ蜻ｼ縺ｳ蜃ｺ縺輔ｌ繧九・
        /// Inspector險ｭ螳壹ｒ蜿肴丐縺吶ｋ縺溘ａ縺ｫ菴ｿ逕ｨ縲・
        /// </summary>
        /// <param name="rects">蠖薙◆繧雁愛螳夂畑RectTransform縺ｮ繝ｪ繧ｹ繝・/param>
        public void SetHitTestRects(IEnumerable<RectTransform>? rects)
        {
            _hitTestRects.Clear();

            if (rects == null) return;

            foreach (var rect in rects)
            {
                if (rect != null)
                {
                    _hitTestRects.Add(rect);
                }
            }
        }

        /// <summary>
        /// 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ驕ｸ謚槫庄閭ｽ譚｡莉ｶ繧定ｩ穂ｾ｡縺吶ｋ縲・
        /// DynamicValue<bool>縺ｮ蛟､貅舌↓蠢懊§縺ｦ縲∫樟蝨ｨ縺ｮ譚｡莉ｶ繧定ｩ穂ｾ｡縺吶ｋ縲・
        /// </summary>
        /// <returns>繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ驕ｸ謚槫庄閭ｽ縺ｪ蝣ｴ蜷・rue</returns>
        public bool EvaluateIsSelectable()
        {
            if (IsLifecycleDespawning())
            {
                _isSelectableCached = false;
                return false;
            }

            var varStore = _owner.Resolver?.TryResolve<IVarStore>(out var resolved) == true ? resolved : new VarStore();
            var context = new Game.Common.SimpleDynamicContext(varStore, _owner);
            if (_isSelectableCondition.TryGet(context, out var selectable))
            {
                _isSelectableCached = selectable;
                return _isSelectableCached;
            }

            return _isSelectableCached;
        }

        public bool EvaluateIsNavigationSelectable()
        {
            if (IsLifecycleDespawning())
            {
                _isNavigationSelectableCached = false;
                return false;
            }

            var varStore = _owner.Resolver?.TryResolve<IVarStore>(out var resolved) == true ? resolved : new VarStore();
            var context = new Game.Common.SimpleDynamicContext(varStore, _owner);
            if (_isNavigationSelectableCondition.TryGet(context, out var selectable))
            {
                _isNavigationSelectableCached = selectable;
                return _isNavigationSelectableCached;
            }

            return _isNavigationSelectableCached;
        }

        /// <summary>
        /// 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ驕ｸ謚槫庄閭ｽ繝輔Λ繧ｰ繧偵く繝｣繝・す繝･縺吶ｋ・井ｸｻ縺ｫUIElementStateMB縺九ｉ蜻ｼ縺ｰ繧後ｋ・峨・
        /// 
        /// ## 蜻ｼ縺ｳ蜃ｺ縺怜・
        /// 
        /// UIElementStateMB縺ｮInstallFeature縺ｧ蜻ｼ縺ｳ蜃ｺ縺輔ｌ繧九・
        /// </summary>
        // Removed malformed inspector attribute.
        public void SetNavigationSelectable(bool selectable)
        {
            _isNavigationSelectableCached = selectable;
        }

        /// <summary>
        /// 驕ｸ謚槫庄閭ｽ譚｡莉ｶ繧定ｨｭ螳壹☆繧九・
        /// </summary>
        public void SetSelectableCondition(Game.Common.DynamicValue<bool> condition)
        {
            _isSelectableCondition = condition;
        }

        /// <summary>
        /// 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ驕ｸ謚槫庄閭ｽ譚｡莉ｶ繧定ｨｭ螳壹☆繧九・
        /// </summary>
        public void SetNavigationSelectableCondition(Game.Common.DynamicValue<bool> condition)
        {
            _isNavigationSelectableCondition = condition;
        }

        /// <summary>
        /// 驕ｸ謚槫━蜈亥ｺｦ繧定ｨｭ螳壹☆繧九・
        /// </summary>
        public void SetSelectionOrder(int selectionOrder)
        {
            _selectionOrder = selectionOrder;
        }

        /// <summary>
        /// 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ蜆ｪ蜈亥ｺｦ繧定ｨｭ螳壹☆繧九・
        /// </summary>
        public void SetNavigationSelectionOrder(int navigationSelectionOrder)
        {
            _navigationSelectionOrder = navigationSelectionOrder;
        }

        /// <summary>
        /// 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ繧ｪ繝ｼ繝舌・繝ｩ繧､繝峨ｒ險ｭ螳壹☆繧九・
        /// 
        /// ## 蜻ｼ縺ｳ蜃ｺ縺怜・
        /// 
        /// UIElementStateMB縺ｮInstallFeature縺ｧ蜻ｼ縺ｳ蜃ｺ縺輔ｌ繧九・
        /// </summary>
        /// <param name="override">繧ｪ繝ｼ繝舌・繝ｩ繧､繝芽ｨｭ螳夲ｼ・ull縺ｧ閾ｪ蜍戊ｨ育ｮ励ｒ菴ｿ逕ｨ・・/param>
        public void SetNavigationOverride(NavigationOverride? @override)
        {
            _navigationOverride = @override;
        }

        // ----------------------------------------------------------------
        // 蜀・Κ繝｡繧ｽ繝・ラ
        // ----------------------------------------------------------------

        /// <summary>
        /// 迥ｶ諷句､画峩繧帝夂衍縺吶ｋ縲・
        /// </summary>
        void NotifyStateChanged(bool prevActive, bool currActive, bool prevVisible, bool currVisible)
        {
            var args = new UIElementStateChangedArgs(
                _owner,
                prevActive,
                currActive,
                prevVisible,
                currVisible
            );

            OnStateChanged?.Invoke(args);
        }

        void EnsureParentStateCache()
        {
            var parentScope = _owner.Parent;
            if (_parentStateCacheResolved && ReferenceEquals(parentScope, _cachedParentScope))
                return;

            UnbindParentState();
            _cachedParentScope = parentScope;
            _parentStateCacheResolved = true;

            if (parentScope != null)
            {
                var parentResolver = parentScope.Resolver;
                if (parentResolver != null && parentResolver.TryResolve<IUIElementState>(out var parentState) && parentState != null)
                {
                    _cachedParentState = parentState;
                    _cachedParentState.OnStateChanged -= HandleParentStateChanged;
                    _cachedParentState.OnStateChanged += HandleParentStateChanged;
                }
            }

            _effectiveActiveDirty = true;
        }

        void UnbindParentState()
        {
            if (_cachedParentState != null)
                _cachedParentState.OnStateChanged -= HandleParentStateChanged;

            _cachedParentState = null;
            _cachedParentScope = null;
        }

        void HandleParentStateChanged(UIElementStateChangedArgs args)
        {
            _effectiveActiveDirty = true;
        }

        bool IsLifecycleDespawning()
        {
            return _lifecycleService != null && _lifecycleService.IsDespawning;
        }
    }
}
