using System.Collections.Generic;
using UnityEngine;
using Game.Scalar;
using Game.Save;
using Game.Scalar.Generated;

namespace Game.Movement
{
    [CreateAssetMenu(menuName = "Game/Movement/MovementScalarDatabase", fileName = "MovementScalarDatabaseSO")]
    public sealed class MovementScalarDatabaseSO : BaseScalarDatabaseSO
    {
        [SerializeField]
        MovementProfileSO profile;

        [SerializeField]
        bool includeProfileDefault = true;

        public MovementProfileSO Profile => profile;

        public override IEnumerable<ScalarDatabaseEntry> GetEntries()
        {
            // Return explicit entries defined by base class first
            foreach (var e in base.GetEntries())
                yield return e;

            if (!includeProfileDefault)
                yield break;

            if (profile != null)
            {
                var key = new ScalarKey(ScalarKeys.GameLib.Movement.DefaultSpeed);
                if (!TryGetEntry(key, out _))
                {
                    var entry = new ScalarDatabaseEntry
                    {
                        Key = key,
                        BaseValue = profile.DefaultSpeedFallback,
                        UseEffectMod = false,
                        UseRoundMod = false,
                        RoundDigits = 0,
                        UseClampMod = false,
                        Clamp = default,
                        SaveEnabled = false,
                        SaveLayer = SaveLayer.Global
                    };
                    yield return entry;
                }
            }
        }
    }
}
