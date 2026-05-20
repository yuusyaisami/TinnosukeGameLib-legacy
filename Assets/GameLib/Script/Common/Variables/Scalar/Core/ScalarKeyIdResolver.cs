using System;

namespace Game.Scalar
{
    public static class ScalarKeyIdResolver
    {
        public static int ResolveOrZero(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return 0;

            return TryResolve(name, out var keyId) ? keyId.Value : 0;
        }

        public static bool TryResolve(string name, out ScalarKeyId keyId)
        {
            keyId = default;

            if (string.IsNullOrWhiteSpace(name))
                return false;

            switch (name)
            {
                case "GameLib.Audio.Bgm.Volume":
                    keyId = new ScalarKeyId(10001);
                    return true;
                case "GameLib.Audio.Master.Volume":
                    keyId = new ScalarKeyId(10002);
                    return true;
                case "GameLib.Audio.Sfx.Volume":
                    keyId = new ScalarKeyId(10003);
                    return true;
                case "GameLib.Audio.System.Volume":
                    keyId = new ScalarKeyId(10004);
                    return true;
                case "GameLib.Audio.Voice.Volume":
                    keyId = new ScalarKeyId(10005);
                    return true;
                case "GameLib.Health.Current":
                    keyId = new ScalarKeyId(10006);
                    return true;
                case "GameLib.Health.Max":
                    keyId = new ScalarKeyId(10007);
                    return true;
                case "GameLib.Health.Ratio":
                    keyId = new ScalarKeyId(10008);
                    return true;
                case "GameLib.Health.Modifier.Critical.IncomingChance":
                    keyId = new ScalarKeyId(10009);
                    return true;
                case "GameLib.Health.Modifier.Critical.IncomingMultiplier":
                    keyId = new ScalarKeyId(10010);
                    return true;
                case "GameLib.Health.Modifier.Critical.OutgoingChance":
                    keyId = new ScalarKeyId(10011);
                    return true;
                case "GameLib.Health.Modifier.Critical.OutgoingMultiplier":
                    keyId = new ScalarKeyId(10012);
                    return true;
                case "GameLib.Health.Modifier.DamageReduction.Rate":
                    keyId = new ScalarKeyId(10013);
                    return true;
                case "GameLib.Health.Modifier.HealBoost.Rate":
                    keyId = new ScalarKeyId(10014);
                    return true;
                case "GameLib.Health.Modifier.Poison.DamagePerSecond":
                    keyId = new ScalarKeyId(10015);
                    return true;
                case "GameLib.Health.Modifier.Poison.TickInterval":
                    keyId = new ScalarKeyId(10016);
                    return true;
                case "GameLib.MapNodePlayer.CurrentNodeId":
                    keyId = new ScalarKeyId(10017);
                    return true;
                case "GameLib.MapNodePlayer.LayerIndex":
                    keyId = new ScalarKeyId(10018);
                    return true;
                case "GameLib.MapNodePlayer.NodeState":
                    keyId = new ScalarKeyId(10019);
                    return true;
                case "GameLib.MapNodePlayer.NodeType":
                    keyId = new ScalarKeyId(10020);
                    return true;
                case "GameLib.MapNodePlayer.TotalDepth":
                    keyId = new ScalarKeyId(10021);
                    return true;
                case "GameLib.MapNodePlayer.TotalWidth":
                    keyId = new ScalarKeyId(10022);
                    return true;
                case "GameLib.MapNodePlayer.WidthIndex":
                    keyId = new ScalarKeyId(10023);
                    return true;
                case "GameLib.Movement.DefaultSpeed":
                    keyId = new ScalarKeyId(10024);
                    return true;
                case "GameLib.Movement.SpeedMultiplier":
                    keyId = new ScalarKeyId(10025);
                    return true;
                case "GameLib.Movement.Input.Accel":
                    keyId = new ScalarKeyId(10026);
                    return true;
                case "GameLib.Movement.Input.BiasStrengthScalar":
                    keyId = new ScalarKeyId(10027);
                    return true;
                case "GameLib.Movement.Input.ClampMagnitudeScalar":
                    keyId = new ScalarKeyId(10028);
                    return true;
                case "GameLib.Movement.Input.Decel":
                    keyId = new ScalarKeyId(10029);
                    return true;
                case "GameLib.Movement.Input.WeightXMinusScalar":
                    keyId = new ScalarKeyId(10030);
                    return true;
                case "GameLib.Movement.Input.WeightXPlusScalar":
                    keyId = new ScalarKeyId(10031);
                    return true;
                case "GameLib.Movement.Input.WeightYMinusScalar":
                    keyId = new ScalarKeyId(10032);
                    return true;
                case "GameLib.Movement.Input.WeightYPlusScalar":
                    keyId = new ScalarKeyId(10033);
                    return true;
                case "GameLogic.NailProfile.Effect.MaxHitCount":
                    keyId = new ScalarKeyId(10034);
                    return true;
                default:
                    return false;
            }
        }
    }
}
