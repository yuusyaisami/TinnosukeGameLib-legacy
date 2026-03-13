#nullable enable
using System.Collections.Generic;
using UnityEngine;
using Game.Times;
using VContainer.Unity;

namespace Game.MaterialFx
{
    /// <summary>
    /// MaterialFxSystemService: 全 MaterialFxService の Tick 管理を行うシステム。
    /// VContainer などから Singleton として注入される想定。
    /// ILateTickable を実装し、VContainer の PlayerLoop から自動で呼び出される。
    /// </summary>
    public sealed class MaterialFxSystemService : IMaterialFxSystemService, ILateTickable
    {
        readonly List<IMaterialFxService> _services = new();
        readonly List<IMaterialFxService> _tickBuffer = new();
        bool _tickBufferDirty = true;

        public void Register(IMaterialFxService service)
        {
            if (service == null) return;
            if (!_services.Contains(service))
            {
                _services.Add(service);
                _tickBufferDirty = true;
            }
        }

        public void Unregister(IMaterialFxService service)
        {
            if (service == null) return;
            if (_services.Remove(service))
            {
                _tickBufferDirty = true;
            }
        }

        public void Tick(float deltaTime)
        {
            Tick(deltaTime, Time.unscaledDeltaTime);
        }

        public void Tick(float deltaTime, float unscaledDeltaTime)
        {
            // コピーして反復（Tick 中の Register/Unregister 対策）
            // 毎フレーム AddRange しない（Register/Unregister があったフレームのみ更新）
            if (_tickBufferDirty)
            {
                _tickBuffer.Clear();
                _tickBuffer.AddRange(_services);
                _tickBufferDirty = false;
            }

            foreach (var svc in _tickBuffer)
            {
                // サービスの TimeScaleBehavior に応じて deltaTime を選択
                var dt = svc.UseUnscaledTime ? unscaledDeltaTime : deltaTime;
                svc.Tick(dt);
            }
        }

        /// <summary>
        /// VContainer の ILateTickable インターフェース実装。
        /// LateUpdate タイミングで自動的に呼び出される。
        /// </summary>
        public void LateTick()
        {
            Tick(Time.deltaTime, Time.unscaledDeltaTime);
        }
    }

    /// <summary>
    /// Null 実装（テスト用）
    /// </summary>
    public sealed class NullMaterialFxSystemService : IMaterialFxSystemService
    {
        public static readonly NullMaterialFxSystemService Instance = new();
        NullMaterialFxSystemService() { }

        public void Register(IMaterialFxService service) { }
        public void Unregister(IMaterialFxService service) { }
        public void Tick(float deltaTime) { }
        public void Tick(float deltaTime, float unscaledDeltaTime) { }
    }
}
