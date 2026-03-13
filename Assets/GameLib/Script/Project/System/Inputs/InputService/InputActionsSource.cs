using System;
using UnityEngine.InputSystem;
using VContainer;
using PlayerInputActions = PlayerInputAction;

namespace Game.Input
{
    /// <summary>
    /// 自動生成された PlayerInputActions の生成と有効化だけを担当。
    /// </summary>
    public sealed class InputActionsSource : IInputActionsSource, IDisposable
    {
        readonly PlayerInputActions _wrapper;
        public PlayerInputActions Actions => _wrapper;

        public InputActionsSource()
        {
            _wrapper = new PlayerInputActions();
            _wrapper.Enable();
            _wrapper.Locomotion.Enable();
            _wrapper.GameUI.Enable();
            _wrapper.GameAction.Enable();
        }

        public void Dispose()
        {
            _wrapper?.Dispose();
        }
    }
}
