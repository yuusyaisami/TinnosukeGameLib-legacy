using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Save;
using Game.Kernel.IR;

namespace Game.Scalar
{
    public enum ScalarDeclarationSourceKind : byte
    {
        Unknown = 0,
        Database = 10,
        ProfileBinding = 20,
    }

    public enum ScalarDeclarationApplyPolicy : byte
    {
        UpdateBaseline = 10,
        ReplaceRuntime = 20,
        SkipIfExists = 30,
    }

    public readonly struct ScalarRuntimeConfigInput
    {
        public ScalarRuntimeConfigInput(
            float baseValue,
            bool useEffectMod,
            bool useRoundMod,
            int roundDigits,
            bool useClampMod,
            ScalarClamp clamp)
        {
            if (roundDigits < 0 || roundDigits > 6)
                throw new ArgumentOutOfRangeException(nameof(roundDigits), roundDigits, "Scalar declaration round digits must be between 0 and 6.");

            if (useClampMod && !TryNormalizeClamp(clamp, out clamp, out string failureReason))
                throw new ArgumentException(failureReason, nameof(clamp));

            BaseValue = baseValue;
            UseEffectMod = useEffectMod;
            UseRoundMod = useRoundMod;
            RoundDigits = roundDigits;
            UseClampMod = useClampMod;
            Clamp = useClampMod ? clamp : default;
        }

        public float BaseValue { get; }

        public bool UseEffectMod { get; }

        public bool UseRoundMod { get; }

        public int RoundDigits { get; }

        public bool UseClampMod { get; }

        public ScalarClamp Clamp { get; }

        public ScalarRuntimeConfig ToRuntimeConfig()
        {
            return new ScalarRuntimeConfig
            {
                BaseValue = BaseValue,
                UseEffectMod = UseEffectMod,
                UseRoundMod = UseRoundMod,
                RoundDigits = RoundDigits,
                UseClampMod = UseClampMod,
                Clamp = Clamp,
            };
        }

        public static bool TryCreate(
            float baseValue,
            bool useEffectMod,
            bool useRoundMod,
            int roundDigits,
            bool useClampMod,
            ScalarClamp clamp,
            out ScalarRuntimeConfigInput input,
            out string failureReason)
        {
            if (roundDigits < 0 || roundDigits > 6)
            {
                input = default;
                failureReason = "Scalar declaration round digits must be between 0 and 6.";
                return false;
            }

            if (useClampMod && !TryNormalizeClamp(clamp, out clamp, out failureReason))
            {
                input = default;
                return false;
            }

            input = new ScalarRuntimeConfigInput(baseValue, useEffectMod, useRoundMod, roundDigits, useClampMod, clamp);
            failureReason = string.Empty;
            return true;
        }

        static bool TryNormalizeClamp(ScalarClamp clamp, out ScalarClamp normalizedClamp, out string failureReason)
        {
            normalizedClamp = default;
            if (!clamp.TryCreateLiteralClamp(out normalizedClamp))
            {
                failureReason = "Scalar declaration inputs require literal clamp bounds. Dynamic clamp sources are unsupported in M9.2.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }
    }

    public readonly struct ScalarDeclarationInput
    {
        public ScalarDeclarationInput(
            ScalarBindingEndpoint endpoint,
            string keyName,
            ScalarRuntimeConfigInput runtimeConfig,
            ScalarDeclarationApplyPolicy applyPolicy,
            bool hasLocalBase,
            float localBaseValue,
            bool saveEnabled,
            SaveLayer saveLayer,
            ScalarDeclarationSourceKind declarationSourceKind,
            string stableId,
            string debugName,
            SourceLocationIR source)
        {
            if (!endpoint.IsValid)
                throw new ArgumentException("Scalar declarations require an explicit verified endpoint.", nameof(endpoint));

            if (string.IsNullOrWhiteSpace(keyName))
                throw new ArgumentException("Scalar declarations require a verified key name.", nameof(keyName));

            if (!Enum.IsDefined(typeof(ScalarDeclarationApplyPolicy), applyPolicy))
                throw new ArgumentOutOfRangeException(nameof(applyPolicy), applyPolicy, "Scalar declarations require a defined apply policy.");

            if (declarationSourceKind == ScalarDeclarationSourceKind.Unknown)
                throw new ArgumentOutOfRangeException(nameof(declarationSourceKind), declarationSourceKind, "Scalar declarations require a defined declaration source kind.");

            if (string.IsNullOrWhiteSpace(stableId))
                throw new ArgumentException("Scalar declarations require a stable identity.", nameof(stableId));

            if (string.IsNullOrWhiteSpace(debugName))
                throw new ArgumentException("Scalar declarations require a debug name.", nameof(debugName));

            if (!source.IsSpecified)
                throw new ArgumentException("Scalar declarations require specified source provenance.", nameof(source));

            Endpoint = endpoint;
            KeyName = keyName.Trim();
            RuntimeConfig = runtimeConfig;
            ApplyPolicy = applyPolicy;
            HasLocalBase = hasLocalBase;
            LocalBaseValue = hasLocalBase ? localBaseValue : 0f;
            SaveEnabled = saveEnabled;
            SaveLayer = saveLayer;
            DeclarationSourceKind = declarationSourceKind;
            StableId = stableId.Trim();
            DebugName = debugName.Trim();
            Source = source;
        }

        public ScalarBindingEndpoint Endpoint { get; }

        public string KeyName { get; }

        public ScalarRuntimeConfigInput RuntimeConfig { get; }

        public ScalarDeclarationApplyPolicy ApplyPolicy { get; }

        public bool HasLocalBase { get; }

        public float LocalBaseValue { get; }

        public bool SaveEnabled { get; }

        public SaveLayer SaveLayer { get; }

        public ScalarDeclarationSourceKind DeclarationSourceKind { get; }

        public string StableId { get; }

        public string DebugName { get; }

        public SourceLocationIR Source { get; }

        public ScalarKey ToScalarKey()
        {
            return new ScalarKey
            {
                Id = Endpoint.KeyId.Value,
                Name = KeyName,
            };
        }
    }

    public static class ScalarDeclarationProjection
    {
        public static bool TryCreateDatabaseDeclaration(
            ScalarDatabaseEntry entry,
            ScalarOwnerIdentity owner,
            SourceLocationIR source,
            out ScalarDeclarationInput declaration,
            out string failureReason)
        {
            return TryCreateDeclaration(
                owner,
                entry.Key,
                ScalarDeclarationApplyPolicy.ReplaceRuntime,
                entry.BaseValue,
                entry.UseEffectMod,
                entry.UseRoundMod,
                entry.RoundDigits,
                entry.UseClampMod,
                entry.Clamp,
                false,
                0f,
                entry.SaveEnabled,
                entry.SaveLayer,
                ScalarDeclarationSourceKind.Database,
                "database",
                entry.Key.Name,
                source,
                out declaration,
                out failureReason);
        }

        public static bool TryCreateDatabaseDeclarations(
            IEnumerable<ScalarDatabaseEntry> entries,
            ScalarOwnerIdentity owner,
            SourceLocationIR source,
            out ScalarDeclarationInput[] declarations,
            out string failureReason)
        {
            if (entries == null)
            {
                declarations = Array.Empty<ScalarDeclarationInput>();
                failureReason = string.Empty;
                return true;
            }

            List<ScalarDeclarationInput> results = new List<ScalarDeclarationInput>();
            HashSet<ScalarBindingEndpoint> seenEndpoints = new HashSet<ScalarBindingEndpoint>();

            foreach (ScalarDatabaseEntry entry in entries)
            {
                if (!TryCreateDatabaseDeclaration(entry, owner, source, out ScalarDeclarationInput declaration, out failureReason))
                {
                    declarations = Array.Empty<ScalarDeclarationInput>();
                    return false;
                }

                if (!seenEndpoints.Add(declaration.Endpoint))
                {
                    declarations = Array.Empty<ScalarDeclarationInput>();
                    failureReason = "Duplicate scalar declaration endpoint detected for owner '" + owner + "' and key '" + entry.Key.Name + "'.";
                    return false;
                }

                results.Add(declaration);
            }

            declarations = results.ToArray();
            failureReason = string.Empty;
            return true;
        }

        public static bool TryCreateDeclaration(
            ScalarOwnerIdentity owner,
            ScalarKey key,
            ScalarDeclarationApplyPolicy applyPolicy,
            float baseValue,
            bool useEffectMod,
            bool useRoundMod,
            int roundDigits,
            bool useClampMod,
            ScalarClamp clamp,
            bool hasLocalBase,
            float localBaseValue,
            bool saveEnabled,
            SaveLayer saveLayer,
            ScalarDeclarationSourceKind declarationSourceKind,
            string stableContext,
            string debugContext,
            SourceLocationIR source,
            out ScalarDeclarationInput declaration,
            out string failureReason)
        {
            if (!owner.IsValid)
            {
                declaration = default;
                failureReason = "Scalar declaration projection requires an explicit owner identity.";
                return false;
            }

            if (!key.IsVerified)
            {
                declaration = default;
                failureReason = "Scalar declaration projection requires a verified scalar key.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(key.Name))
            {
                declaration = default;
                failureReason = "Scalar declaration projection requires a verified key name.";
                return false;
            }

            if (!source.IsSpecified)
            {
                declaration = default;
                failureReason = "Scalar declaration projection requires specified source provenance.";
                return false;
            }

            if (!ScalarRuntimeConfigInput.TryCreate(baseValue, useEffectMod, useRoundMod, roundDigits, useClampMod, clamp, out ScalarRuntimeConfigInput runtimeConfig, out failureReason))
            {
                declaration = default;
                failureReason = "Scalar declaration projection for key '" + (key.Name ?? string.Empty) + "' failed: " + failureReason;
                return false;
            }

            ScalarBindingEndpoint endpoint = new ScalarBindingEndpoint(owner, key.KeyId);
            declaration = new ScalarDeclarationInput(
                endpoint,
                key.Name,
                runtimeConfig,
                applyPolicy,
                hasLocalBase,
                localBaseValue,
                saveEnabled,
                saveLayer,
                declarationSourceKind,
                BuildStableId(endpoint, declarationSourceKind, stableContext),
                BuildDebugName(declarationSourceKind, debugContext, key),
                source);
            failureReason = string.Empty;
            return true;
        }

        static string BuildStableId(ScalarBindingEndpoint endpoint, ScalarDeclarationSourceKind declarationSourceKind, string stableContext)
        {
            string context = NormalizeStableSegment(stableContext);
            string stableId = "scalar-decl:" + declarationSourceKind.ToString().ToLowerInvariant()
                + ":" + endpoint.Owner.Kind.ToString().ToLowerInvariant()
                + ":" + endpoint.Owner.OwnerId.Value
                + ":" + endpoint.KeyId.Value.ToString("D10");

            if (context.Length != 0)
                stableId += ":" + context;

            return stableId;
        }

        static string BuildDebugName(ScalarDeclarationSourceKind declarationSourceKind, string debugContext, ScalarKey key)
        {
            string keyName = string.IsNullOrWhiteSpace(key.Name) ? key.KeyId.Value.ToString() : key.Name.Trim();
            string context = NormalizeDebugLabel(debugContext, string.Empty);

            if (context.Length == 0)
                return declarationSourceKind + " scalar declaration [" + keyName + "]";

            return declarationSourceKind + " scalar declaration [" + keyName + "] from " + context;
        }

        static string NormalizeStableSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string trimmed = value.Trim();
            char[] buffer = new char[trimmed.Length];
            int writeIndex = 0;
            for (int index = 0; index < trimmed.Length; index++)
            {
                char current = trimmed[index];
                if (char.IsLetterOrDigit(current) || current == '-' || current == '_' || current == '.')
                {
                    buffer[writeIndex++] = current;
                    continue;
                }

                buffer[writeIndex++] = '-';
            }

            return new string(buffer, 0, writeIndex).Trim('-');
        }

        static string NormalizeDebugLabel(string primary, string secondary)
        {
            string left = string.IsNullOrWhiteSpace(primary) ? string.Empty : primary.Trim();
            string right = string.IsNullOrWhiteSpace(secondary) ? string.Empty : secondary.Trim();

            if (left.Length == 0)
                return right;

            if (right.Length == 0)
                return left;

            return left + "/" + right;
        }
    }

    public static class ScalarDeclarationRuntimeBridge
    {
        public static bool TryApplyDeclarations(
            IScalarRuntimeShell shell,
            IEnumerable<ScalarDeclarationInput> declarations,
            out string failureReason)
        {
            if (shell == null)
            {
                failureReason = "Scalar declaration runtime bridge requires an IScalarRuntimeShell instance.";
                return false;
            }

            if (declarations == null)
            {
                failureReason = string.Empty;
                return true;
            }

            if (declarations is IReadOnlyList<ScalarDeclarationInput> declarationList)
                return shell.TryInstallDeclarations(declarationList, out failureReason);

            return shell.TryInstallDeclarations(new List<ScalarDeclarationInput>(declarations), out failureReason);
        }

        public static bool TryApplyDeclarations(
            IBaseScalarService scalar,
            IEnumerable<ScalarDeclarationInput> declarations,
            out string failureReason)
        {
            if (scalar == null)
            {
                failureReason = "Scalar declaration runtime bridge requires an IBaseScalarService instance.";
                return false;
            }

            if (declarations == null)
            {
                failureReason = string.Empty;
                return true;
            }

            foreach (ScalarDeclarationInput declaration in declarations)
            {
                if (!TryApplyDeclaration(scalar, declaration, out failureReason))
                    return false;
            }

            failureReason = string.Empty;
            return true;
        }

        public static bool TryApplyDeclaration(
            IBaseScalarService scalar,
            ScalarDeclarationInput declaration,
            out string failureReason)
        {
            if (scalar == null)
            {
                failureReason = "Scalar declaration runtime bridge requires an IBaseScalarService instance.";
                return false;
            }

            ScalarKey key = declaration.ToScalarKey();
            ScalarRuntimeConfig runtimeConfig = declaration.RuntimeConfig.ToRuntimeConfig();

            switch (declaration.ApplyPolicy)
            {
                case ScalarDeclarationApplyPolicy.UpdateBaseline:
                    if (scalar.TryGetRuntime(key, out ScalarKeyRuntime runtime))
                    {
                        runtime.SetBaseline(declaration.RuntimeConfig.BaseValue);
                        ApplyLocalBaseIfRequested(runtime, declaration);
                    }
                    else
                    {
                        scalar.EnsureRuntime(key, runtimeConfig);
                        if (declaration.HasLocalBase && scalar.TryGetRuntime(key, out ScalarKeyRuntime ensuredRuntime))
                            ensuredRuntime.SetLocalBase(declaration.LocalBaseValue);
                    }
                    break;

                case ScalarDeclarationApplyPolicy.ReplaceRuntime:
                    scalar.EnsureRuntime(key, runtimeConfig);
                    if (declaration.HasLocalBase && scalar.TryGetRuntime(key, out ScalarKeyRuntime replacedRuntime))
                        replacedRuntime.SetLocalBase(declaration.LocalBaseValue);
                    break;

                case ScalarDeclarationApplyPolicy.SkipIfExists:
                    if (!scalar.TryGetRuntime(key, out _))
                        scalar.EnsureRuntime(key, runtimeConfig);
                    if (declaration.HasLocalBase && scalar.TryGetRuntime(key, out ScalarKeyRuntime skippedRuntime))
                        skippedRuntime.SetLocalBase(declaration.LocalBaseValue);
                    break;

                default:
                    failureReason = "Scalar declaration runtime bridge requires a defined apply policy.";
                    return false;
            }

            failureReason = string.Empty;
            return true;
        }

        static void ApplyLocalBaseIfRequested(ScalarKeyRuntime runtime, ScalarDeclarationInput declaration)
        {
            if (declaration.HasLocalBase)
                runtime.SetLocalBase(declaration.LocalBaseValue);
        }
    }

    /// <summary>
    /// Scalarに登録するエントリデータ。
    /// </summary>
    [Serializable]
    public struct ScalarDatabaseEntry
    {
        public ScalarKey Key;
        public float BaseValue;
        public bool UseEffectMod;
        public bool UseRoundMod;
        public int RoundDigits;
        public bool UseClampMod;
        public ScalarClamp Clamp;
        public bool SaveEnabled;
        public SaveLayer SaveLayer;
    }

    /// <summary>
    /// Scalarシステムに登録するデータ群を定義する基底ScriptableObject。
    /// 派生クラスで固定のエントリを追加することも可能。
    /// </summary>
    public class BaseScalarDatabaseSO : ScriptableObject
    {
        [SerializeField] protected List<ScalarDatabaseEntry> entries = new();

        public bool TryCreateScalarDeclarations(ScalarOwnerIdentity owner, SourceLocationIR source, out ScalarDeclarationInput[] declarations, out string failureReason)
        {
            return ScalarDeclarationProjection.TryCreateDatabaseDeclarations(GetEntries(), owner, source, out declarations, out failureReason);
        }

        /// <summary>
        /// 登録するエントリを取得する。派生クラスでオーバーライド可能。
        /// </summary>
        public virtual IEnumerable<ScalarDatabaseEntry> GetEntries()
        {
            return entries;
        }

        public bool TryGetEntry(ScalarKey key, out ScalarDatabaseEntry entry)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Key.Id == key.Id)
                {
                    entry = entries[i];
                    return true;
                }
            }

            entry = default;
            return false;
        }

        public IEnumerable<ScalarDatabaseEntry> EnumerateByLayer(SaveLayer layer)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].SaveLayer == layer)
                    yield return entries[i];
            }
        }

        public IEnumerable<ScalarDatabaseEntry> EnumerateSaveTargets(SaveLayer layer)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.SaveEnabled && e.SaveLayer == layer)
                    yield return e;
            }
        }
    }
}
