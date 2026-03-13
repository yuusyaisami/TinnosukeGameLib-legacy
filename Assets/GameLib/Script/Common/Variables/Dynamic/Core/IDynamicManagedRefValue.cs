// Game.Common.IDynamicManagedRefValue
//
// DynamicValue<T> の ManagedRef literal/asset source 候補として
// 自動登録されるための opt-in marker interface。

namespace Game.Common
{
    /// <summary>
    /// この interface を実装したクラスは、
    /// <see cref="DynamicManagedRefSourceCatalog"/> によって
    /// DynamicValue の literal source 候補として自動認識される。
    /// <para>
    /// BaseProfileData 派生型は暗黙的に対象となるため、
    /// この interface は非 Profile 系の Preset に使用する。
    /// </para>
    /// </summary>
    public interface IDynamicManagedRefValue
    {
    }
}
