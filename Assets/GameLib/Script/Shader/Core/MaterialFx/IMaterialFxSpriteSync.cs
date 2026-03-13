#nullable enable
using UnityEngine;

namespace Game.MaterialFx
{
    /// <summary>
    /// SpriteRenderer のスプライト変更を MaterialFx 側へ通知し、_SpriteUVRect 等の同期を行う。
    /// </summary>
    public interface IMaterialFxSpriteSync
    {
        void NotifySpriteChanged(Sprite? sprite);
        void NotifyFlipChanged(bool flipX, bool flipY);
    }
}

