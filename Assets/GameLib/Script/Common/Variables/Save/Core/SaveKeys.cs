// Game.Save.SaveKeys.cs
//
// Save key building (v2). No enum.ToString(), no silent sanitize.

#nullable enable
using System.Text.RegularExpressions;
using Game;
using UnityEngine.LowLevel;

namespace Game.Save
{
    public static class SaveKeys
    {
        static readonly Regex SegmentRegex = new Regex(@"^[a-zA-Z0-9_-]+$", RegexOptions.Compiled);

        public static bool TryBuildPayloadKey(
            int profileId,
            ScopeKey scopeKey,
            SaveLayer layer,
            out string key,
            out SaveResult error)
        {
            if (profileId < 0)
            {
                key = string.Empty;
                error = SaveResult.Failed(SaveError.InvalidKey, "ProfileId must be non-negative.");
                return false;
            }

            if (!TryValidateSegment(scopeKey.Id, out var segErr))
            {
                key = string.Empty;
                error = SaveResult.Failed(SaveError.InvalidKey, segErr);
                return false;
            }

            var kCode = GetKindCode(scopeKey.Kind);
            var lCode = GetLayerCode(layer);
            key = $"Save/Payload/{profileId}/{kCode}/{scopeKey.Id}/{lCode}/Data";
            error = SaveResult.Success();
            return true;
        }

        public static bool TryBuildBackupKey(
            int profileId,
            ScopeKey scopeKey,
            SaveLayer layer,
            int slot,
            out string key,
            out SaveResult error)
        {
            if (profileId < 0)
            {
                key = string.Empty;
                error = SaveResult.Failed(SaveError.InvalidKey, "ProfileId must be non-negative.");
                return false;
            }

            if (slot < 0)
            {
                key = string.Empty;
                error = SaveResult.Failed(SaveError.InvalidKey, "Slot must be non-negative.");
                return false;
            }

            if (!TryValidateSegment(scopeKey.Id, out var segErr))
            {
                key = string.Empty;
                error = SaveResult.Failed(SaveError.InvalidKey, segErr);
                return false;
            }

            var kCode = GetKindCode(scopeKey.Kind);
            var lCode = GetLayerCode(layer);
            key = $"Save/Backup/{profileId}/{kCode}/{scopeKey.Id}/{lCode}/Slot{slot}";
            error = SaveResult.Success();
            return true;
        }
        // すべての組み合わせを網羅すること
        public static string GetKindCode(LifetimeScopeKind kind) => kind switch
        {
            LifetimeScopeKind.Global => "G",
            LifetimeScopeKind.Platform => "PL",
            LifetimeScopeKind.Project => "P",
            LifetimeScopeKind.Scene => "S",
            LifetimeScopeKind.Entity => "E",
            LifetimeScopeKind.Field => "F",
            LifetimeScopeKind.UI => "U",
            LifetimeScopeKind.UIElement => "UE",
            LifetimeScopeKind.Runtime => "R",
            _ => "U",
        };

        public static string GetLayerCode(SaveLayer layer) => layer switch
        {
            SaveLayer.Global => "GG",
            SaveLayer.SystemSetting => "SS",
            SaveLayer.Profile => "PR",
            SaveLayer.Session => "SE",
            SaveLayer.GameLogic => "GL",
            _ => "UU",
        };

        public static bool TryValidateSegment(string input, out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                errorMessage = "ID cannot be empty or whitespace.";
                return false;
            }

            if (input.Length > 64)
            {
                errorMessage = $"ID '{input}' is too long (max 64 chars).";
                return false;
            }

            if (!SegmentRegex.IsMatch(input))
            {
                errorMessage = $"ID '{input}' contains invalid characters. Only a-z, A-Z, 0-9, -, _ are allowed.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
    }
}
