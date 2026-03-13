using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Input
{
    /// <summary>
    /// どのレイヤのコンシューマをブロックするか。
    /// </summary>
    [Flags]
    public enum InputBlockScope
    {
        None = 0,
        System = 1 << 0,
        UI = 1 << 1,
        Gameplay = 1 << 2,

        All = System | UI | Gameplay
    }

    public interface IInputBlocker
    {
        /// <summary>何らかのスコープがブロック中なら true。</summary>
        bool IsBlocked();

        /// <summary>指定スコープのいずれかがブロック中なら true。</summary>
        bool IsBlocked(InputBlockScope scope);

        /// <summary>全スコープをブロックするトークンを取得。</summary>
        IDisposable Block(string reason = null);

        /// <summary>指定スコープをブロックするトークンを取得。</summary>
        IDisposable Block(InputBlockScope scope, string reason = null);

        /// <summary>すべてのブロックを強制解除（非常用 / デバッグ用）。</summary>
        void ForceClearAll();
    }


    /// <summary>
    /// ref-count 方式の入力ブロック管理。スコープごとにカウントを持つ。
    /// Block() でトークンを発行し、トークン Dispose で解除。
    /// </summary>
    public sealed class InputBlocker : IInputBlocker
    {
        // トークン id とスコープ
        readonly Dictionary<int, InputBlockScope> _active = new Dictionary<int, InputBlockScope>();
        int _nextId = 1;
        int _systemCount;
        int _uiCount;
        int _gameplayCount;

        public bool IsBlocked() => IsBlocked(InputBlockScope.All);

        public bool IsBlocked(InputBlockScope scope)
        {
            if (scope == InputBlockScope.None)
                return false;

            if ((scope & InputBlockScope.System) != 0 && _systemCount > 0) return true;
            if ((scope & InputBlockScope.UI) != 0 && _uiCount > 0) return true;
            if ((scope & InputBlockScope.Gameplay) != 0 && _gameplayCount > 0) return true;
            return false;
        }

        public IDisposable Block(string reason = null)
            => Block(InputBlockScope.All, reason);

        public IDisposable Block(InputBlockScope scope, string reason = null)
        {
            if (scope == InputBlockScope.None)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[InputBlocker] Block called with None scope. Ignored.");
#endif
                return EmptyToken.Instance;
            }

            var id = _nextId++;
            _active.Add(id, scope);
            AddScopeCounters(scope);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!string.IsNullOrEmpty(reason))
            {
                Debug.Log($"[InputBlocker] Blocked (id={id}, scope={scope}) reason={reason}");
            }
#endif

            return new BlockToken(this, id);
        }

        internal void Release(int id)
        {
            if (_active.TryGetValue(id, out var scope))
            {
                _active.Remove(id);
                RemoveScopeCounters(scope);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[InputBlocker] Released (id={id})");
#endif
            }
        }

        public void ForceClearAll()
        {
            if (_active.Count == 0)
                return;

            _active.Clear();
            _systemCount = 0;
            _uiCount = 0;
            _gameplayCount = 0;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[InputBlocker] ForceClearAll called. All input blocks cleared.");
#endif
        }

        void AddScopeCounters(InputBlockScope scope)
        {
            if ((scope & InputBlockScope.System) != 0) _systemCount++;
            if ((scope & InputBlockScope.UI) != 0) _uiCount++;
            if ((scope & InputBlockScope.Gameplay) != 0) _gameplayCount++;
        }

        void RemoveScopeCounters(InputBlockScope scope)
        {
            if ((scope & InputBlockScope.System) != 0) _systemCount = Math.Max(0, _systemCount - 1);
            if ((scope & InputBlockScope.UI) != 0) _uiCount = Math.Max(0, _uiCount - 1);
            if ((scope & InputBlockScope.Gameplay) != 0) _gameplayCount = Math.Max(0, _gameplayCount - 1);
        }

        sealed class BlockToken : IDisposable
        {
            readonly InputBlocker _owner;
            readonly int _id;
            bool _disposed;

            public BlockToken(InputBlocker owner, int id)
            {
                _owner = owner;
                _id = id;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _owner.Release(_id);
            }
        }

        /// <summary>
        /// scope=None での呼び出し用ダミートークン。
        /// </summary>
        sealed class EmptyToken : IDisposable
        {
            public static readonly EmptyToken Instance = new EmptyToken();
            public void Dispose() { }
        }
    }
}
