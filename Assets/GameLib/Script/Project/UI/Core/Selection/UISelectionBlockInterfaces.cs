#nullable enable
using System;

namespace Game.UI
{
    [Flags]
    public enum UISelectionBlockMask
    {
        None = 0,
        Navigation = 1 << 0,
        Pointer = 1 << 1,
        All = Navigation | Pointer,
    }

    public interface IUISelectionBlockService
    {
        bool IsNavigationBlocked { get; }
        bool IsPointerBlocked { get; }

        IDisposable AcquireBlock(object owner, UISelectionBlockMask mask = UISelectionBlockMask.All);
    }
}
