#nullable enable

namespace Game.TransformSystem
{
    /// <summary>
    /// 個々の演出機能（Shake / Follow / Preset / Scroll 等）の track interface。
    /// TransformTargetDirector が管理し、毎 frame Tick → WriteContribution で寄与を集める。
    /// </summary>
    public interface ITransformModifierTrack
    {
        /// <summary>毎 frame の更新。</summary>
        void Tick(float deltaTime);

        /// <summary>track がまだ生きているか。false になったら director が除去する。</summary>
        bool IsAlive { get; }

        /// <summary>合成時の優先度。高い方が優先。</summary>
        int Priority { get; }

        /// <summary>この track が寄与する property の flags。</summary>
        TransformContributionMask ContributedProperties { get; }

        /// <summary>accumulator へ寄与を書き込む。</summary>
        void WriteContribution(ref TransformPoseAccumulator accumulator);

        /// <summary>track を停止する。</summary>
        void Stop();

        /// <summary>track をリセットする。</summary>
        void Reset();
    }
}
