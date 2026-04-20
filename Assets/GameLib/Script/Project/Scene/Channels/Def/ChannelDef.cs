// Game.Animation.ChannelDefs

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Channel
{
    /// <summary>
    /// 蜈ｨ繝√Ε繝阪Ν Hub 蜈ｱ騾壹・繝吶・繧ｹ縲・
    /// - 迴ｾ蝨ｨ逋ｻ骭ｲ縺輔ｌ縺ｦ縺・ｋ ChannelDef 荳隕ｧ
    /// - 蠕後°繧峨メ繝｣繝阪Ν繧定ｿｽ蜉・丞炎髯､
    /// - 繧ｿ繧ｰ縺九ｉ ChannelDef 繧貞叙蠕・
    /// </summary>
    public interface IChannelHubService
    {
        /// <summary>縺薙・ Hub 縺御ｿ晄戟縺励※縺・ｋ繝√Ε繝阪Ν螳夂ｾｩ縺ｮ荳隕ｧ・亥渕蠎輔け繝ｩ繧ｹ・峨・/summary>
        IReadOnlyList<ChannelDefBase> ChannelDefs { get; }

        /// <summary>繧ｿ繧ｰ縺九ｉ ChannelDef 繧貞叙蠕励・/summary>
        bool TryGetChannelDef(string tag, out ChannelDefBase def);

        /// <summary>
        /// 繝√Ε繝阪Ν繧堤匳骭ｲ縲・
        /// overwrite=false 縺ｮ縺ｨ縺阪∝酔縺・tag 縺梧里縺ｫ縺ゅｌ縺ｰ false 繧定ｿ斐☆縲・
        /// </summary>
        bool RegisterChannel(ChannelDefBase def, bool overwrite = false);

        /// <summary>
        /// 繧ｿ繧ｰ謖・ｮ壹〒繝√Ε繝阪Ν繧貞炎髯､縲・
        /// 謌仙粥縺励◆繧・true縲・
        /// </summary>
        bool UnregisterChannel(string tag);
    }
    /// <summary>
    /// 縺吶∋縺ｦ縺ｮ繝√Ε繝阪Ν螳夂ｾｩ縺ｮ蜈ｱ騾壼渕蠎包ｼ・ag 縺縺第戟縺､・峨・
    /// </summary>
    [Serializable]
    public abstract class ChannelDefBase : IChannelIdentity
    {
        [SerializeField]
        [Tooltip("Inspector setting.")]
        string tag = "default";

        public string Tag => tag;

        public virtual void EnsureIntegrity(Component owner)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                tag = "default";
            }
        }
    }
}
