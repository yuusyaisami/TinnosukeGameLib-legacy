#nullable enable
namespace Game.Spawn
{
    /// <summary>
    /// スポーン後のコンテキストを受け取るコンポーネント。
    /// Unit (RuntimeResolverMB / LTS Scope) にアタッチされる。
    /// </summary>
    public interface ISpawnContextConsumer
    {
        /// <summary>
        /// スポーン直後に呼ばれる。
        /// Active 化前に呼ばれることが保証される。
        /// </summary>
        void OnSpawnContextReceived(in UnitSpawnContext context);
    }
}
