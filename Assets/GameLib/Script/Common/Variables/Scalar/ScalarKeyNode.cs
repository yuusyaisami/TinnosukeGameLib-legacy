using System;
using UnityEngine;
using Game.Registry;

namespace Game.Scalar
{
    /// <summary>
    /// ScalarKey 縺ｮ繝弱・繝峨・
    /// </summary>
    [Serializable]
    public sealed class ScalarKeyNode : HierarchyNodeBase
    {
        [Tooltip("Inspector setting.")]
        [SerializeField] string explicitKey;

        [Tooltip("Inspector setting.")]
        [SerializeField] bool obsolete;

        [Tooltip("Inspector setting.")]
        [SerializeField] string[] tags;

        /// <summary>譏守､ｺ逧・↑繧ｭ繝ｼ・育ｩｺ縺ｪ繧芽・蜍慕函謌撰ｼ・/summary>
        public string ExplicitKey
        {
            get => explicitKey;
            set => explicitKey = value ?? string.Empty;
        }

        /// <summary>蟒・ｭ｢莠亥ｮ壹°縺ｩ縺・°</summary>
        public bool Obsolete
        {
            get => obsolete;
            set => obsolete = value;
        }

        /// <summary>讀懃ｴ｢逕ｨ繧ｿ繧ｰ</summary>
        public string[] Tags
        {
            get => tags ?? Array.Empty<string>();
            set => tags = value;
        }
    }
}
