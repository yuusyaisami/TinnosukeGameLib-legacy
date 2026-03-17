#nullable enable
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.TransformSystem
{
    /// <summary>
    /// target Transform ごとの director を管理するレジストリ。
    /// 同一 target に対して 1 つだけ director を持つ。
    /// </summary>
    public interface ITransformAnimationTargetRegistry
    {
        ITransformTargetDirector GetOrCreateDirector(Transform target);
        bool TryGetDirector(Transform target, out ITransformTargetDirector director);
        void TickAll(float deltaTime);
        void Clear();
    }

    public sealed class TransformAnimationTargetRegistryService : ITransformAnimationTargetRegistry, ITickable, IScopeReleaseHandler
    {
        readonly Dictionary<Transform, TransformTargetDirector> _directors = new();
        readonly ITransformAnimationOutputRegistry? _outputRegistry;

        public TransformAnimationTargetRegistryService(IObjectResolver resolver)
        {
            resolver.TryResolve(out _outputRegistry);
        }

        public ITransformTargetDirector GetOrCreateDirector(Transform target)
        {
            if (_directors.TryGetValue(target, out var existing))
            {
                TryBindRegisteredOutput(target, existing);
                return existing;
            }

            var output = ResolveOutput(target);
            var director = new TransformTargetDirector(
                target,
                output ?? new TransformAnimationOutput(),
                applyDirectly: output == null);
            _directors[target] = director;
            return director;
        }

        public bool TryGetDirector(Transform target, out ITransformTargetDirector director)
        {
            if (target != null && _directors.TryGetValue(target, out var d))
            {
                director = d;
                return true;
            }

            director = null!;
            return false;
        }

        public void TickAll(float deltaTime)
        {
            List<Transform>? toRemove = null;

            foreach (var kv in _directors)
            {
                if (kv.Key == null)
                {
                    var deadKey = kv.Key;
                    if (deadKey == null)
                        continue;

                    toRemove ??= new List<Transform>();
                    toRemove.Add(deadKey);
                    continue;
                }

                TryBindRegisteredOutput(kv.Key, kv.Value);
                kv.Value.Tick(deltaTime);

                if (!kv.Value.HasActiveTracks)
                {
                    toRemove ??= new List<Transform>();
                    toRemove.Add(kv.Key);
                }
            }

            if (toRemove != null)
            {
                for (int i = 0; i < toRemove.Count; i++)
                    _directors.Remove(toRemove[i]);
            }
        }

        public void Clear()
        {
            _directors.Clear();
        }

        public void Tick()
        {
            TickAll(Time.deltaTime);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            Clear();
        }

        TransformAnimationOutput? ResolveOutput(Transform target)
        {
            if (_outputRegistry != null && _outputRegistry.TryGetSink(target, out var sink))
                return sink.AnimationOutput;

            return null;
        }

        void TryBindRegisteredOutput(Transform target, TransformTargetDirector director)
        {
            var output = ResolveOutput(target);
            if (output == null)
                return;

            director.BindOutput(output);
        }
    }
}
