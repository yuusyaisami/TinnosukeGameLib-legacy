#nullable enable
using System;
using System.Collections.Generic;

namespace Game.UI
{
    public enum ModalLayerTiePolicy
    {
        FirstCome = 10,
        SimultaneousAllowedLayers = 20,
    }

    public enum ModalLayerTopOrderEffect
    {
        None = 0,
        ForceLowerLayerVisibleFalse = 10,
        ForceLowerLayerInputInactive = 20,
    }

    public enum ModalLayerChangeKind
    {
        Unknown = 0,
        RegisterLayer = 10,
        SetDefaultRoot = 20,
        Push = 30,
        Pop = 40,
        PopTop = 50,
        ClearLayer = 60,
        ClearAll = 70,
        LayerConfigChanged = 80,
    }

    public enum ModalLayerRootInactiveReason
    {
        None = 0,
        NotActiveInLayer = 10,
        LayerSuppressedByOtherLayer = 20,
    }

    [Serializable]
    public struct ModalLayerPreset
    {
        public string LayerKey;
        public int Order;
        public ModalLayerTiePolicy TiePolicy;
        public bool AllowSimultaneousInputInSameOrder;
        public ModalLayerTopOrderEffect TopOrderEffect;
        public bool KeepNonActiveInLayerVisible;
        public bool KeepNonActiveInLayerInputActive;

        public static ModalLayerPreset Default(string layerKey)
        {
            return new ModalLayerPreset
            {
                LayerKey = string.IsNullOrWhiteSpace(layerKey) ? "default" : layerKey,
                Order = 0,
                TiePolicy = ModalLayerTiePolicy.FirstCome,
                AllowSimultaneousInputInSameOrder = false,
                TopOrderEffect = ModalLayerTopOrderEffect.None,
                KeepNonActiveInLayerVisible = false,
                KeepNonActiveInLayerInputActive = false,
            };
        }
    }

    public readonly struct ModalLayerResolvedState
    {
        public string LayerKey { get; }
        public int Order { get; }
        public bool HasAnyUI { get; }
        public IUIModalRoot? ActiveRoot { get; }
        public bool Visible { get; }
        public bool InputActive { get; }
        public bool IsTopOrderGroup { get; }
        public bool IsPrimaryInOrder { get; }
        public string SuppressedByLayerKey { get; }

        public ModalLayerResolvedState(
            string layerKey,
            int order,
            bool hasAnyUI,
            IUIModalRoot? activeRoot,
            bool visible,
            bool inputActive,
            bool isTopOrderGroup,
            bool isPrimaryInOrder,
            string suppressedByLayerKey)
        {
            LayerKey = layerKey ?? string.Empty;
            Order = order;
            HasAnyUI = hasAnyUI;
            ActiveRoot = activeRoot;
            Visible = visible;
            InputActive = inputActive;
            IsTopOrderGroup = isTopOrderGroup;
            IsPrimaryInOrder = isPrimaryInOrder;
            SuppressedByLayerKey = suppressedByLayerKey ?? string.Empty;
        }
    }

    public readonly struct ModalRootResolvedState
    {
        public string LayerKey { get; }
        public IUIModalRoot Root { get; }
        public bool IsActiveInLayer { get; }
        public bool Visible { get; }
        public bool InputActive { get; }
        public ModalLayerRootInactiveReason InactiveReason { get; }

        public ModalRootResolvedState(
            string layerKey,
            IUIModalRoot root,
            bool isActiveInLayer,
            bool visible,
            bool inputActive,
            ModalLayerRootInactiveReason inactiveReason)
        {
            LayerKey = layerKey ?? string.Empty;
            Root = root;
            IsActiveInLayer = isActiveInLayer;
            Visible = visible;
            InputActive = inputActive;
            InactiveReason = inactiveReason;
        }
    }

    public readonly struct ModalLayerStatesChangedContext
    {
        public IReadOnlyList<ModalLayerResolvedState> PreviousLayers { get; }
        public IReadOnlyList<ModalLayerResolvedState> CurrentLayers { get; }
        public IReadOnlyList<ModalRootResolvedState> PreviousRoots { get; }
        public IReadOnlyList<ModalRootResolvedState> CurrentRoots { get; }
        public string CauseLayerKey { get; }
        public ModalLayerChangeKind ChangeKind { get; }
        public UIModalStackChangeType ChangeType { get; }

        public ModalLayerStatesChangedContext(
            IReadOnlyList<ModalLayerResolvedState> previousLayers,
            IReadOnlyList<ModalLayerResolvedState> currentLayers,
            IReadOnlyList<ModalRootResolvedState> previousRoots,
            IReadOnlyList<ModalRootResolvedState> currentRoots,
            string causeLayerKey,
            ModalLayerChangeKind changeKind,
            UIModalStackChangeType changeType)
        {
            PreviousLayers = previousLayers;
            CurrentLayers = currentLayers;
            PreviousRoots = previousRoots;
            CurrentRoots = currentRoots;
            CauseLayerKey = causeLayerKey ?? string.Empty;
            ChangeKind = changeKind;
            ChangeType = changeType;
        }
    }

    public interface IModalStackChannelHubService
    {
        IReadOnlyList<ModalLayerResolvedState> LayerStates { get; }
        IReadOnlyList<ModalRootResolvedState> RootStates { get; }
        IUIModalRoot? CurrentInputRoot { get; }

        void RegisterLayer(ModalLayerPreset preset);
        bool TryGetLayerState(string layerKey, out ModalLayerResolvedState state);
        bool TryGetRootState(IScopeNode owner, out ModalRootResolvedState state);

        void SetDefaultRoot(string layerKey, IUIModalRoot? root, UIModalStackChangeType changeType = UIModalStackChangeType.Normal);
        void PushModal(string layerKey, IUIModalRoot root, ModalOptions options = default, UIModalStackChangeType changeType = UIModalStackChangeType.Normal);
        bool PopModal(string layerKey, IUIModalRoot root, UIModalStackChangeType changeType = UIModalStackChangeType.Normal);
        IUIModalRoot? PopTop(string layerKey, UIModalStackChangeType changeType = UIModalStackChangeType.Normal);
        void ClearLayer(string layerKey, UIModalStackChangeType changeType = UIModalStackChangeType.Normal);
        void ClearAll(UIModalStackChangeType changeType = UIModalStackChangeType.Normal);

        event Action<ModalLayerStatesChangedContext>? OnLayerStatesChanged;
    }

    public interface IModalStackChannelTelemetry
    {
        IReadOnlyList<ModalLayerResolvedState> LayerStates { get; }
        IReadOnlyList<ModalRootResolvedState> RootStates { get; }
        IUIModalRoot? CurrentInputRoot { get; }
        event Action<ModalLayerStatesChangedContext>? OnLayerStatesChanged;
    }
}
