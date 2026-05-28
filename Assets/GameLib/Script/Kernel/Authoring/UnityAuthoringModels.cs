#nullable enable

using System;

namespace Game.Kernel.Authoring
{
    public enum UnityAuthoringSourceKind
    {
        Unknown = 0,
        SceneObject = 10,
        PrefabAsset = 20,
        PrefabInstance = 30,
        PrefabVariant = 40,
        ScriptableObjectAsset = 50,
        GeneratedAsset = 60,
        CodeDefinedModule = 70,
        LegacyBridge = 90,
    }

    public enum UnityObjectLinkKind
    {
        Unknown = 0,
        Asset = 10,
        Scene = 20,
        Runtime = 30,
        Selection = 40,
    }

    public enum AuthoringComponentKind
    {
        Unknown = 0,
        Declaration = 10,
        Link = 20,
        Bridge = 30,
        ViewBinding = 40,
        DebugOnly = 50,
        LegacyAdapter = 90,
    }

    public readonly struct UnitySourceLocation : IEquatable<UnitySourceLocation>
    {
        readonly string? assetGuid;
        readonly string? assetPath;
        readonly string? scenePath;
        readonly string? gameObjectPath;
        readonly string? componentType;
        readonly string? propertyPath;

        public UnitySourceLocation(
            UnityAuthoringSourceKind kind,
            string? assetGuid,
            string? assetPath,
            long localFileId,
            string? scenePath,
            string? gameObjectPath,
            string? componentType,
            string? propertyPath)
        {
            if (kind == UnityAuthoringSourceKind.Unknown)
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unity authoring source locations must provide a defined source kind.");

            if (localFileId < 0)
                throw new ArgumentOutOfRangeException(nameof(localFileId), localFileId, "Unity authoring source locations must use a non-negative local file id.");

            Kind = kind;
            this.assetGuid = NormalizeOptionalString(assetGuid);
            this.assetPath = NormalizeOptionalString(assetPath);
            LocalFileId = localFileId;
            this.scenePath = NormalizeOptionalString(scenePath);
            this.gameObjectPath = NormalizeOptionalString(gameObjectPath);
            this.componentType = NormalizeOptionalString(componentType);
            this.propertyPath = NormalizeOptionalString(propertyPath);

            ValidateTraceability();
        }

        public UnityAuthoringSourceKind Kind { get; }

        public string? AssetGuid => assetGuid;

        public string? AssetPath => assetPath;

        public long LocalFileId { get; }

        public string? ScenePath => scenePath;

        public string? GameObjectPath => gameObjectPath;

        public string? ComponentType => componentType;

        public string? PropertyPath => propertyPath;

        public bool IsSpecified => Kind != UnityAuthoringSourceKind.Unknown && HasTraceability();

        public bool Equals(UnitySourceLocation other)
        {
            return Kind == other.Kind
                && StringComparer.Ordinal.Equals(assetGuid, other.assetGuid)
                && StringComparer.Ordinal.Equals(assetPath, other.assetPath)
                && LocalFileId == other.LocalFileId
                && StringComparer.Ordinal.Equals(scenePath, other.scenePath)
                && StringComparer.Ordinal.Equals(gameObjectPath, other.gameObjectPath)
                && StringComparer.Ordinal.Equals(componentType, other.componentType)
                && StringComparer.Ordinal.Equals(propertyPath, other.propertyPath);
        }

        public override bool Equals(object? obj)
        {
            return obj is UnitySourceLocation other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ GetNullableStringHashCode(assetGuid);
                hash = (hash * 397) ^ GetNullableStringHashCode(assetPath);
                hash = (hash * 397) ^ LocalFileId.GetHashCode();
                hash = (hash * 397) ^ GetNullableStringHashCode(scenePath);
                hash = (hash * 397) ^ GetNullableStringHashCode(gameObjectPath);
                hash = (hash * 397) ^ GetNullableStringHashCode(componentType);
                hash = (hash * 397) ^ GetNullableStringHashCode(propertyPath);
                return hash;
            }
        }

        public override string ToString()
        {
            return "UnitySourceLocation(Kind=" + Kind + ", AssetGuid=" + FormatValue(assetGuid) + ", AssetPath=" + FormatValue(assetPath) + ", LocalFileId=" + LocalFileId + ", ScenePath=" + FormatValue(scenePath) + ", GameObjectPath=" + FormatValue(gameObjectPath) + ", ComponentType=" + FormatValue(componentType) + ", PropertyPath=" + FormatValue(propertyPath) + ")";
        }

        public static bool operator ==(UnitySourceLocation left, UnitySourceLocation right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(UnitySourceLocation left, UnitySourceLocation right)
        {
            return !left.Equals(right);
        }

        void ValidateTraceability()
        {
            switch (Kind)
            {
                case UnityAuthoringSourceKind.SceneObject:
                    if (string.IsNullOrEmpty(scenePath) || string.IsNullOrEmpty(gameObjectPath) || string.IsNullOrEmpty(componentType))
                        throw new ArgumentException("Scene object authoring sources must provide scene path, game object path, and component type.", nameof(Kind));

                    break;
                case UnityAuthoringSourceKind.PrefabInstance:
                    if (string.IsNullOrEmpty(scenePath) || string.IsNullOrEmpty(gameObjectPath))
                        throw new ArgumentException("Prefab instance authoring sources must provide scene path and game object path.", nameof(Kind));

                    if (!HasAny(assetGuid, assetPath))
                        throw new ArgumentException("Prefab instance authoring sources must retain prefab traceability.", nameof(Kind));

                    break;
                case UnityAuthoringSourceKind.PrefabAsset:
                case UnityAuthoringSourceKind.PrefabVariant:
                case UnityAuthoringSourceKind.ScriptableObjectAsset:
                    if (!HasAny(assetGuid, assetPath))
                        throw new ArgumentException("Asset-backed authoring sources must provide asset GUID or asset path.", nameof(Kind));

                    break;
                case UnityAuthoringSourceKind.GeneratedAsset:
                    if (!HasAny(assetGuid, assetPath, componentType))
                        throw new ArgumentException("Generated asset sources must provide asset or generator traceability.", nameof(Kind));

                    break;
                case UnityAuthoringSourceKind.CodeDefinedModule:
                    if (!HasAny(componentType, assetPath))
                        throw new ArgumentException("Code-defined module sources must provide a code or asset trace.", nameof(Kind));

                    break;
                case UnityAuthoringSourceKind.LegacyBridge:
                    if (!HasAny(assetGuid, assetPath, scenePath, componentType))
                        throw new ArgumentException("Legacy bridge sources must preserve at least one traceability field.", nameof(Kind));

                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unsupported Unity authoring source kind.");
            }

            if (!HasTraceability())
                throw new ArgumentException("Unity authoring source locations must provide at least one traceability field.", nameof(Kind));
        }

        bool HasTraceability()
        {
            return !string.IsNullOrEmpty(assetGuid)
                || !string.IsNullOrEmpty(assetPath)
                || !string.IsNullOrEmpty(scenePath)
                || !string.IsNullOrEmpty(gameObjectPath)
                || !string.IsNullOrEmpty(componentType)
                || !string.IsNullOrEmpty(propertyPath);
        }

        static bool HasAny(string? first, string? second)
        {
            return !string.IsNullOrEmpty(first) || !string.IsNullOrEmpty(second);
        }

        static bool HasAny(string? first, string? second, string? third)
        {
            return !string.IsNullOrEmpty(first) || !string.IsNullOrEmpty(second) || !string.IsNullOrEmpty(third);
        }

        static bool HasAny(string? first, string? second, string? third, string? fourth)
        {
            return !string.IsNullOrEmpty(first)
                || !string.IsNullOrEmpty(second)
                || !string.IsNullOrEmpty(third)
                || !string.IsNullOrEmpty(fourth);
        }

        static string? NormalizeOptionalString(string? value)
        {
            if (value == null)
                return null;

            string trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }

        static int GetNullableStringHashCode(string? value)
        {
            return value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);
        }

        static string FormatValue(string? value)
        {
            return value == null ? "<null>" : value;
        }
    }

    public readonly struct UnityObjectLink : IEquatable<UnityObjectLink>
    {
        readonly string? sourceGuid;
        readonly string debugName;

        public UnityObjectLink(
            UnityObjectLinkKind kind,
            string? sourceGuid,
            long localFileId,
            int runtimeInstanceId,
            string? debugName)
        {
            if (kind == UnityObjectLinkKind.Unknown)
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unity object links must provide a defined kind.");

            if (localFileId < 0)
                throw new ArgumentOutOfRangeException(nameof(localFileId), localFileId, "Unity object links must use a non-negative local file id.");

            if (runtimeInstanceId < 0)
                throw new ArgumentOutOfRangeException(nameof(runtimeInstanceId), runtimeInstanceId, "Unity object links must use a non-negative runtime instance id.");

            if (sourceGuid != null && string.IsNullOrWhiteSpace(sourceGuid))
                throw new ArgumentException("Unity object link source GUIDs must be null or non-empty.", nameof(sourceGuid));

            string normalizedDebugName = NormalizeRequiredString(debugName, nameof(debugName));
            if (!string.IsNullOrEmpty(sourceGuid) && localFileId == 0)
                throw new ArgumentException("Unity object links with a source GUID must provide a positive local file id.", nameof(localFileId));

            Kind = kind;
            this.sourceGuid = NormalizeOptionalString(sourceGuid);
            LocalFileId = localFileId;
            RuntimeInstanceId = runtimeInstanceId;
            this.debugName = normalizedDebugName;
        }

        public UnityObjectLinkKind Kind { get; }

        public string? SourceGuid => sourceGuid;

        public long LocalFileId { get; }

        public int RuntimeInstanceId { get; }

        public string DebugName => debugName;

        public bool IsEmpty => Kind == UnityObjectLinkKind.Unknown
            && string.IsNullOrEmpty(sourceGuid)
            && LocalFileId == 0
            && RuntimeInstanceId == 0
            && string.IsNullOrEmpty(debugName);

        public bool Equals(UnityObjectLink other)
        {
            return Kind == other.Kind
                && StringComparer.Ordinal.Equals(sourceGuid, other.sourceGuid)
                && LocalFileId == other.LocalFileId
                && RuntimeInstanceId == other.RuntimeInstanceId
                && StringComparer.Ordinal.Equals(debugName, other.debugName);
        }

        public override bool Equals(object? obj)
        {
            return obj is UnityObjectLink other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ (sourceGuid == null ? 0 : StringComparer.Ordinal.GetHashCode(sourceGuid));
                hash = (hash * 397) ^ LocalFileId.GetHashCode();
                hash = (hash * 397) ^ RuntimeInstanceId.GetHashCode();
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(debugName);
                return hash;
            }
        }

        public override string ToString()
        {
            return "UnityObjectLink(Kind=" + Kind + ", SourceGuid=" + FormatValue(sourceGuid) + ", LocalFileId=" + LocalFileId + ", RuntimeInstanceId=" + RuntimeInstanceId + ", DebugName=" + debugName + ")";
        }

        public static bool operator ==(UnityObjectLink left, UnityObjectLink right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(UnityObjectLink left, UnityObjectLink right)
        {
            return !left.Equals(right);
        }

        static string NormalizeRequiredString(string? value, string parameterName)
        {
            if (value == null)
                throw new ArgumentNullException(parameterName);

            string trimmed = value.Trim();
            if (trimmed.Length == 0)
                throw new ArgumentException("Unity object links must provide a non-empty debug name.", parameterName);

            return trimmed;
        }

        static string? NormalizeOptionalString(string? value)
        {
            if (value == null)
                return null;

            string trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }

        static string FormatValue(string? value)
        {
            return value == null ? "<null>" : value;
        }
    }
}