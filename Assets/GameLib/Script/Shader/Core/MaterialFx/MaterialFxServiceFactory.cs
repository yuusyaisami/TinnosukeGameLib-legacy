#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.MaterialFx
{
    public interface IMaterialFxServiceFactory
    {
        IMaterialFxService CreateForSpriteRenderer(SpriteRenderer renderer);
        IMaterialFxService CreateForRenderer(Renderer renderer);
        IMaterialFxService CreateForMaterial(Material materialInstance);
        IMaterialFxService CreateForGraphic(Graphic graphic);
        IMaterialFxService CreateForTmpText(TMP_Text tmpText);
    }
    /// <summary>
    /// MaterialFxService のインスタンスを生成するファクトリ。
    /// Renderer / Graphic から適切な Adapter を選択し、サービス群を構築する。
    /// </summary>
    public sealed class MaterialFxServiceFactory : IMaterialFxServiceFactory
    {
        readonly IMaterialFxPropertyRegistry _registry;
        readonly IMaterialFxSystemService _system;

        // IMPORTANT (パタパタ問題 / MPB競合の根本原因と対策):
        //
        // ■ 根本原因
        // MaterialFxService は「1ターゲット(=1 SpriteRenderer/Graphic)につき1つだけ存在する」前提です。
        // しかし CreateForSpriteRenderer/CreateForGraphic を呼び出す側が多重生成してしまうと、
        // 同一ターゲットに複数の MaterialFxService がぶら下がります。
        //
        // Unity の MaterialPropertyBlock(MPB) は additive ではなく overwrite です。
        // つまり Renderer.SetPropertyBlock は「この MPB が全て」という扱いになり、
        // 2つのサービスが別々の MPB を Apply すると、最後に Apply した側の内容だけが残ります。
        //
        // ■ 何をしたら起きるか（再発条件）
        // - 同一 SpriteRenderer/Graphic に対して CreateFor* を複数回呼ぶ
        //   （例: DI/LifetimeScope の二重生成、Prefab の二重初期化、Service を使い捨てにする実装、等）
        // - 古い Service を Dispose せずに残したまま、新しい Service を作る
        //   → Tick が複数走り、Apply の順序次第で Enabled/値が 0/1 に戻ったように見える（パタパタ）
        //
        // ■ 対策
        // Factory 側で target.GetInstanceID() をキーにして Service を共有し、
        // 呼び出し側には「Lease(借用)」を返します（refCount 方式）。
        // 呼び出し側は Lease を Dispose するだけでOKで、refCount が 0 の時だけ本体を Dispose します。
        // これにより「同一ターゲットに複数 Service が存在する」状態を構造的に防止します。
        readonly Dictionary<int, RefCountedService> _spriteRendererServices = new();
        readonly Dictionary<int, RefCountedService> _graphicServices = new();

        public MaterialFxServiceFactory(
            IMaterialFxPropertyRegistry registry,
            IMaterialFxSystemService system)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _system = system ?? throw new ArgumentNullException(nameof(system));
        }

        /// <summary>
        /// SpriteRenderer 用の MaterialFxService を作成
        /// </summary>
        public IMaterialFxService CreateForSpriteRenderer(SpriteRenderer renderer)
        {
            if (renderer == null) throw new ArgumentNullException(nameof(renderer));

            var key = renderer.GetInstanceID();
            if (_spriteRendererServices.TryGetValue(key, out var existing))
            {
                // 既に同一ターゲット用 Service がある場合は共有する（多重生成すると MPB の奪い合いになる）。
                existing.RefCount++;
                return new ServiceLease(existing, () => ReleaseSpriteRenderer(key));
            }

            var adapter = new SpriteRendererAdapter(renderer, _registry);
            var service = CreateService(adapter);
            var entry = new RefCountedService(service);
            _spriteRendererServices.Add(key, entry);
            // 呼び出し側へは Lease を返す（呼び出し側が Dispose しても、他の利用者が居れば Service 本体は生存させる）。
            return new ServiceLease(entry, () => ReleaseSpriteRenderer(key));
        }

        /// <summary>
        /// 汎用 Renderer 用の MaterialFxService を作成
        /// </summary>
        public IMaterialFxService CreateForRenderer(Renderer renderer)
        {
            var adapter = new RendererAdapter(renderer);
            return CreateService(adapter);
        }

        /// <summary>
        /// Material インスタンス用の MaterialFxService を作成
        /// </summary>
        public IMaterialFxService CreateForMaterial(Material materialInstance)
        {
            var adapter = new MaterialInstanceAdapter(materialInstance);
            return CreateService(adapter);
        }

        /// <summary>
        /// uGUI Graphic 用の MaterialFxService を作成
        /// </summary>
        public IMaterialFxService CreateForGraphic(Graphic graphic)
        {
            if (graphic == null) throw new ArgumentNullException(nameof(graphic));

            var key = graphic.GetInstanceID();
            if (_graphicServices.TryGetValue(key, out var existing))
            {
                // Graphic も SpriteRenderer 同様に多重 Service 化で適用が競合するため共有する。
                existing.RefCount++;
                return new ServiceLease(existing, () => ReleaseGraphic(key));
            }

            var adapter = new GraphicAdapter(graphic, _registry);
            var service = CreateService(adapter);
            var entry = new RefCountedService(service);
            _graphicServices.Add(key, entry);
            return new ServiceLease(entry, () => ReleaseGraphic(key));
        }

        /// <summary>
        /// TextMeshPro 用の MaterialFxService を作成
        /// </summary>
        public IMaterialFxService CreateForTmpText(TMP_Text tmpText)
        {
            var adapter = new TmpTextAdapter(tmpText);
            return CreateService(adapter);
        }

        IMaterialFxService CreateService(IMaterialFxTargetAdapter adapter)
        {
            var layer = new MaterialFxLayerService(_registry, adapter);
            var dispatch = new MaterialFxDispatchService(_registry, adapter);

            return new MaterialFxService(layer, dispatch, adapter, _registry, _system);
        }

        void ReleaseSpriteRenderer(int key)
        {
            if (!_spriteRendererServices.TryGetValue(key, out var entry))
                return;

            entry.RefCount--;
            if (entry.RefCount > 0)
                return;

            _spriteRendererServices.Remove(key);
            // 最後の Lease が Dispose されたタイミングでのみ本体を Dispose する。
            // ここで Dispose しないと「古い Service が Tick し続けて Apply する」状態が残り、パタパタの原因になる。
            entry.Service.Dispose();
        }

        void ReleaseGraphic(int key)
        {
            if (!_graphicServices.TryGetValue(key, out var entry))
                return;

            entry.RefCount--;
            if (entry.RefCount > 0)
                return;

            _graphicServices.Remove(key);
            entry.Service.Dispose();
        }

        sealed class RefCountedService
        {
            public readonly IMaterialFxService Service;
            public int RefCount;

            public RefCountedService(IMaterialFxService service)
            {
                Service = service;
                RefCount = 1;
            }
        }

        sealed class ServiceLease : IMaterialFxService, IMaterialFxSpriteSync, IMaterialFxTelemetry
        {
            readonly RefCountedService _entry;
            readonly System.Action _release;
            bool _disposed;

            public ServiceLease(RefCountedService entry, System.Action release)
            {
                _entry = entry;
                _release = release;
            }

            public bool UseUnscaledTime => _entry.Service.UseUnscaledTime;

            public string TelemetryId
            {
                get
                {
                    if (_entry.Service is IMaterialFxTelemetry t)
                        return t.TelemetryId;
                    return string.Empty;
                }
            }

            public void GetSnapshot(System.Collections.Generic.List<MaterialFxStackTelemetry> dst)
            {
                if (_entry.Service is IMaterialFxTelemetry t)
                {
                    t.GetSnapshot(dst);
                    return;
                }
                dst?.Clear();
            }

            public int GetActiveLayerCount(string stableKey, string contextTag = "")
            {
                return _entry.Service.GetActiveLayerCount(stableKey, contextTag);
            }

            public void SetLayer(string stableKey, string contextTag, MaterialFxTypedValue value,
                                 MaterialFxBlendMode blend = MaterialFxBlendMode.Override, int priority = 0, float lifetimeSeconds = -1f)
            {
                _entry.Service.SetLayer(stableKey, contextTag, value, blend, priority, lifetimeSeconds);
            }

            public void SetLayerFade(string stableKey, string contextTag, MaterialFxTypedValue value,
                                     float duration = 0f, DG.Tweening.Ease ease = DG.Tweening.Ease.Linear,
                                     MaterialFxBlendMode blend = MaterialFxBlendMode.Override, int priority = 0, float lifetimeSeconds = -1f)
            {
                _entry.Service.SetLayerFade(stableKey, contextTag, value, duration, ease, blend, priority, lifetimeSeconds);
            }

            public void SetLayerWeightFade(string stableKey, string contextTag, float targetWeight,
                                           float duration = 0f, DG.Tweening.Ease ease = DG.Tweening.Ease.Linear)
            {
                _entry.Service.SetLayerWeightFade(stableKey, contextTag, targetWeight, duration, ease);
            }

            public void RemoveLayer(string stableKey, string contextTag)
            {
                _entry.Service.RemoveLayer(stableKey, contextTag);
            }

            public void ClearContext(string contextTag)
            {
                _entry.Service.ClearContext(contextTag);
            }

            public void ApplyPreset(string contextTag, MaterialFxPresetSO preset, int priority = 0)
            {
                _entry.Service.ApplyPreset(contextTag, preset, priority);
            }

            public void ApplyPreset(string contextTag, System.Collections.Generic.IEnumerable<MaterialFxPresetEntry> entries, int priority = 0)
            {
                _entry.Service.ApplyPreset(contextTag, entries, priority);
            }

            public void FadeOutPreset(string contextTag, float duration, DG.Tweening.Ease ease = DG.Tweening.Ease.Linear)
            {
                _entry.Service.FadeOutPreset(contextTag, duration, ease);
            }

            public void Tick(float deltaTime)
            {
                _entry.Service.Tick(deltaTime);
            }

            public void Clear(string channelTag)
            {
                _entry.Service.Clear(channelTag);
            }

            public void NotifySpriteChanged(Sprite? sprite)
            {
                if (_entry.Service is IMaterialFxSpriteSync sync)
                {
                    sync.NotifySpriteChanged(sprite);
                }
            }

            public void NotifyFlipChanged(bool flipX, bool flipY)
            {
                if (_entry.Service is IMaterialFxSpriteSync sync)
                {
                    sync.NotifyFlipChanged(flipX, flipY);
                }
            }

            public void Dispose()
            {
                if (_disposed)
                    return;
                _disposed = true;
                _release();
            }

            // NOTE:
            // MaterialFxSystemService.Register は List.Contains を使って重複登録を避けています。
            // Lease は呼び出し毎に new されるため、参照比較のままだと「同じ本体 Service なのに別物扱い」になり、
            // 同一ターゲットが TickBuffer に重複して入る可能性が出ます（= Tick/Apply が多重化しうる）。
            // そのため Equals/GetHashCode を本体 Service に寄せて、重複登録を確実に抑止します。
            public override bool Equals(object? obj)
            {
                if (ReferenceEquals(this, obj))
                    return true;
                if (obj is ServiceLease other)
                    return ReferenceEquals(_entry.Service, other._entry.Service);
                return ReferenceEquals(_entry.Service, obj);
            }

            public override int GetHashCode()
            {
                return _entry.Service.GetHashCode();
            }
        }
    }

    /// <summary>
    /// VContainer などから Singleton として取得可能な Factory Provider。
    /// </summary>
    public static class MaterialFxServiceFactoryProvider
    {
        static MaterialFxServiceFactory? _instance;

        public static MaterialFxServiceFactory GetOrCreate(
            IMaterialFxPropertyRegistry registry,
            IMaterialFxSystemService system)
        {
            return _instance ??= new MaterialFxServiceFactory(registry, system);
        }

        public static void Reset()
        {
            _instance = null;
        }
    }
}
