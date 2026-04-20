#nullable enable
using System.Collections.Generic;
using UnityEngine;
using Game.Times;
using VContainer.Unity;

namespace Game.MaterialFx
{
    /// <summary>
    /// MaterialFxSystemService: 蜈ｨ MaterialFxService 縺ｮ Tick 邂｡逅・ｒ陦後≧繧ｷ繧ｹ繝・Β縲・
    /// VContainer 縺ｪ縺ｩ縺九ｉ Singleton 縺ｨ縺励※豕ｨ蜈･縺輔ｌ繧区Φ螳壹・
    /// IScopeLateTickHandler 繧貞ｮ溯｣・＠縲〃Container 縺ｮ PlayerLoop 縺九ｉ閾ｪ蜍輔〒蜻ｼ縺ｳ蜃ｺ縺輔ｌ繧九・
    /// </summary>
    public sealed class MaterialFxSystemService : IMaterialFxSystemService, IScopeLateTickHandler
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
            // 繧ｳ繝斐・縺励※蜿榊ｾｩ・・ick 荳ｭ縺ｮ Register/Unregister 蟇ｾ遲厄ｼ・
            // 豈弱ヵ繝ｬ繝ｼ繝 AddRange 縺励↑縺・ｼ・egister/Unregister 縺後≠縺｣縺溘ヵ繝ｬ繝ｼ繝縺ｮ縺ｿ譖ｴ譁ｰ・・
            if (_tickBufferDirty)
            {
                _tickBuffer.Clear();
                _tickBuffer.AddRange(_services);
                _tickBufferDirty = false;
            }

            foreach (var svc in _tickBuffer)
            {
                // 繧ｵ繝ｼ繝薙せ縺ｮ TimeScaleBehavior 縺ｫ蠢懊§縺ｦ deltaTime 繧帝∈謚・
                var dt = svc.UseUnscaledTime ? unscaledDeltaTime : deltaTime;
                svc.Tick(dt);
            }
        }

        /// <summary>
        /// VContainer 縺ｮ IScopeLateTickHandler 繧､繝ｳ繧ｿ繝ｼ繝輔ぉ繝ｼ繧ｹ螳溯｣・・
        /// LateUpdate 繧ｿ繧､繝溘Φ繧ｰ縺ｧ閾ｪ蜍慕噪縺ｫ蜻ｼ縺ｳ蜃ｺ縺輔ｌ繧九・
        /// </summary>
        public void LateTick()
        {
            Tick(Time.deltaTime, Time.unscaledDeltaTime);
        }
    }

    /// <summary>
    /// Null 螳溯｣・ｼ医ユ繧ｹ繝育畑・・
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
