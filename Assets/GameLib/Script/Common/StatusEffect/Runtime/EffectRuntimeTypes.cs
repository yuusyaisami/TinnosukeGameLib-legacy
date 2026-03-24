namespace Game.StatusEffect
{
    public enum EffectLifetimeEndAction
    {
        None = 0,
        Remove = 10,
        Disable = 20,
    }

    public enum EffectCountExhaustedAction
    {
        None = 0,
        Remove = 10,
        Disable = 20,
        DisableUseOnly = 30,
    }
}
