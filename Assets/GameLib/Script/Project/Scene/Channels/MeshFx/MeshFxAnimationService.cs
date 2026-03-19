#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using DG.Tweening;
using Game.Commands;
using Game.Common;
using Game.MaterialFx;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Channel
{
    public enum MeshFxParameterBlendMode
    {
        Override = 0,
        Add = 1,
        Multiply = 2,
    }

    public enum MeshFxAnimationValueType
    {
        Float = 0,
        Int = 1,
        Bool = 2,
        Vector2 = 3,
        Vector3 = 4,
        Color = 5,
        Curve = 6,
    }

    [Serializable]
    public struct MeshFxAnimationValue
    {
        [LabelText("Type"), LabelWidth(64)]
        public MeshFxAnimationValueType Type;

        [LabelText("Float"), ShowIf("@Type == MeshFxAnimationValueType.Float")]
        public float FloatValue;

        [LabelText("Int"), ShowIf("@Type == MeshFxAnimationValueType.Int")]
        public int IntValue;

        [LabelText("Bool"), ShowIf("@Type == MeshFxAnimationValueType.Bool")]
        public bool BoolValue;

        [LabelText("Vector2"), ShowIf("@Type == MeshFxAnimationValueType.Vector2")]
        public Vector2 Vector2Value;

        [LabelText("Vector3"), ShowIf("@Type == MeshFxAnimationValueType.Vector3")]
        public Vector3 Vector3Value;

        [LabelText("Color"), ShowIf("@Type == MeshFxAnimationValueType.Color")]
        public Color ColorValue;

        [LabelText("Curve"), ShowIf("@Type == MeshFxAnimationValueType.Curve")]
        public AnimationCurve CurveValue;
    }

    [Serializable]
    public sealed class MeshFxParameterAnimationEntry
    {
        [LabelText("Parameter Path")]
        [Tooltip("例: mode, beamSettings.StartWidth, singleDirectionSettings.Length")]
        [ValueDropdown(nameof(GetParameterPathOptions))]
        public string Path = string.Empty;

        [InlineProperty]
        public MeshFxAnimationValue Value;

        [MinValue(0f)]
        public float DurationSeconds = 0f;

        public Ease Easing = Ease.Linear;

        public MeshFxParameterBlendMode BlendMode = MeshFxParameterBlendMode.Override;

        [LabelText("Wait For Completion")]
        public bool WaitForCompletion = true;

        static ValueDropdownList<string> GetParameterPathOptions()
        {
            return MeshFxParameterPathDropdownCatalog.GetOptions();
        }
    }

    [Serializable]
    public sealed class MeshFxMaterialAnimationEntry
    {
        [MaterialFxPropertyPicker]
        public string Key = string.Empty;

        public MaterialFxSerializedValue Value;

        public MaterialFxBlendMode BlendMode = MaterialFxBlendMode.Override;

        [MinValue(0f)]
        public float DurationSeconds = 0f;

        public Ease Easing = Ease.Linear;

        public int PriorityOffset = 0;

        public float LifetimeSeconds = -1f;
    }

    [Serializable]
    public sealed class MeshFxChannelAnimationClip
    {
        [LabelText("Channel Tag")]
        public string ChannelTag = "default";

        [LabelText("Context Tag")]
        public string ContextTag = "default";

        [LabelText("Clear Context Before Play")]
        public bool ClearContextBeforePlay = false;

        [LabelText("Material Base Priority")]
        public int MaterialBasePriority = 0;

        [ListDrawerSettings(ShowPaging = false, ShowFoldout = true)]
        public List<MeshFxParameterAnimationEntry> ParameterEntries = new();

        [ListDrawerSettings(ShowPaging = false, ShowFoldout = true)]
        public List<MeshFxMaterialAnimationEntry> MaterialEntries = new();
    }

    static class MeshFxParameterPathDropdownCatalog
    {
        static readonly BindingFlags Binding =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static readonly HashSet<string> AddedPaths = new(StringComparer.Ordinal);
        static ValueDropdownList<string>? s_options;

        public static ValueDropdownList<string> GetOptions()
        {
            if (s_options != null)
                return s_options;

            AddedPaths.Clear();
            var options = new List<(string Display, string Path)>(128);
            CollectLeafPaths(typeof(MeshFxChannelDef), string.Empty, options, depth: 0);

            options.Sort((a, b) => string.CompareOrdinal(a.Display, b.Display));

            var list = new ValueDropdownList<string>();
            for (int i = 0; i < options.Count; i++)
            {
                list.Add(options[i].Display, options[i].Path);
            }

            s_options = list;
            return s_options;
        }

        static void CollectLeafPaths(Type type, string prefix, List<(string Display, string Path)> dst, int depth)
        {
            if (depth > 4)
                return;

            var fields = type.GetFields(Binding);
            for (int i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                if (field.IsStatic)
                    continue;
                if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null)
                    continue;
                if (field.GetCustomAttribute<NonSerializedAttribute>() != null)
                    continue;

                var path = string.IsNullOrEmpty(prefix)
                    ? field.Name
                    : $"{prefix}.{field.Name}";

                var fieldType = field.FieldType;
                if (IsSupportedLeafType(fieldType))
                {
                    if (AddedPaths.Add(path))
                    {
                        dst.Add((BuildDisplayPath(path), path));
                    }
                    continue;
                }

                if (!ShouldRecurse(fieldType))
                    continue;

                CollectLeafPaths(fieldType, path, dst, depth + 1);
            }
        }

        static bool IsSupportedLeafType(Type type)
        {
            if (type == typeof(float) ||
                type == typeof(int) ||
                type == typeof(uint) ||
                type == typeof(bool) ||
                type == typeof(Vector2) ||
                type == typeof(Vector3) ||
                type == typeof(Color) ||
                type == typeof(AnimationCurve) ||
                type.IsEnum)
            {
                return true;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(DynamicValue<>))
            {
                var arg = type.GetGenericArguments()[0];
                return arg == typeof(float) || arg == typeof(Vector2) || arg == typeof(Vector3);
            }

            return false;
        }

        static bool ShouldRecurse(Type type)
        {
            if (type == typeof(string))
                return false;
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return false;
            if (type.IsArray || type.IsEnum)
                return false;
            if (type.IsGenericType)
                return false;
            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
                return false;
            if (!type.IsClass)
                return false;

            return true;
        }

        static string BuildDisplayPath(string parameterPath)
        {
            var segments = parameterPath.Split('.');
            if (segments.Length == 0)
                return parameterPath;

            var sb = new StringBuilder(64);
            sb.Append(ResolveGroup(segments[0]));
            for (int i = 0; i < segments.Length; i++)
            {
                sb.Append('/');
                sb.Append(ToDisplaySegment(segments[i]));
            }

            return sb.ToString();
        }

        static string ResolveGroup(string rootSegment)
        {
            return rootSegment switch
            {
                "enabledOnAcquire" => "Lifecycle",
                "playOnSpawn" => "Lifecycle",
                "updateIntervalFrames" => "Performance",
                "collisionUpdateIntervalFrames" => "Performance",
                "performanceTier" => "Performance",

                "mode" => "Shape",
                "beamSettings" => "Shape",
                "waveLineSettings" => "Shape",
                "ribbonSettings" => "Shape",
                "coneSettings" => "Shape",
                "arcSettings" => "Shape",

                "pathMode" => "Path",
                "singleDirectionSettings" => "Path",
                "scopeToScopeSettings" => "Path",
                "trajectoryTrackSettings" => "Path",

                "applyMaterialFx" => "Visual",
                "queueOffset" => "Visual",

                "collisionEnabled" => "Collision",
                "layerId" => "Collision",
                "hitMask" => "Collision",
                "setId" => "Collision",
                "collisionPathSource" => "Collision",
                "collisionApproximation" => "Collision",
                _ => "Other",
            };
        }

        static string ToDisplaySegment(string segment)
        {
            if (string.IsNullOrEmpty(segment))
                return segment;

            if (segment.EndsWith("Settings", StringComparison.Ordinal))
            {
                segment = segment[..^"Settings".Length];
            }

            if (string.Equals(segment, "collisionApproximation", StringComparison.Ordinal))
                return "Collision Approximation";

            var sb = new StringBuilder(segment.Length + 8);
            for (int i = 0; i < segment.Length; i++)
            {
                var c = segment[i];
                if (i == 0)
                {
                    sb.Append(char.ToUpperInvariant(c));
                    continue;
                }

                var prev = segment[i - 1];
                if ((char.IsUpper(c) && !char.IsUpper(prev)) ||
                    (char.IsDigit(c) && !char.IsDigit(prev)) ||
                    (!char.IsDigit(c) && char.IsDigit(prev)))
                {
                    sb.Append(' ');
                }

                sb.Append(c);
            }

            return sb.ToString();
        }
    }

    public interface IMeshFxAnimationService
    {
        bool Play(MeshFxChannelAnimationClip? clip);

        bool Play(
            string channelTag,
            string contextTag,
            IReadOnlyList<MeshFxParameterAnimationEntry>? parameterEntries,
            IReadOnlyList<MeshFxMaterialAnimationEntry>? materialEntries = null,
            bool clearContextBeforePlay = false,
            int materialBasePriority = 0);

        bool ClearContext(string channelTag, string contextTag);
        void ClearAll();
    }

    public sealed class MeshFxAnimationService :
        IMeshFxAnimationService,
        ITickable,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        enum ParameterValueKind
        {
            Float = 0,
            Int = 1,
            UInt = 2,
            Bool = 3,
            Vector2 = 4,
            Vector3 = 5,
            Color = 6,
            Enum = 7,
            DynamicFloat = 8,
            DynamicVector2 = 9,
            DynamicVector3 = 10,
            Curve = 11,
            Unsupported = 99,
        }

        readonly struct TrackKey : IEquatable<TrackKey>
        {
            public readonly string ChannelTag;
            public readonly string ContextTag;
            public readonly string Path;

            public TrackKey(string channelTag, string contextTag, string path)
            {
                ChannelTag = channelTag;
                ContextTag = contextTag;
                Path = path;
            }

            public bool Equals(TrackKey other)
            {
                return string.Equals(ChannelTag, other.ChannelTag, StringComparison.Ordinal) &&
                       string.Equals(ContextTag, other.ContextTag, StringComparison.Ordinal) &&
                       string.Equals(Path, other.Path, StringComparison.Ordinal);
            }

            public override bool Equals(object? obj) => obj is TrackKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    var h = 17;
                    h = h * 31 + ChannelTag.GetHashCode();
                    h = h * 31 + ContextTag.GetHashCode();
                    h = h * 31 + Path.GetHashCode();
                    return h;
                }
            }
        }

        sealed class ActiveTrack
        {
            public TrackKey Key;
            public IMeshFxChannelPlayer Player = null!;
            public MeshFxParameterAccessor Accessor = null!;
            public float DurationSeconds;
            public float ElapsedSeconds;
            public Ease Easing;
            public Func<float, object?> Evaluate = _ => null;
        }

        sealed class MeshFxParameterAccessor
        {
            static readonly BindingFlags Binding =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            readonly MemberInfo[] _members;

            public string Path { get; }
            public Type ValueType { get; }

            MeshFxParameterAccessor(string path, MemberInfo[] members, Type valueType)
            {
                Path = path;
                _members = members;
                ValueType = valueType;
            }

            public static bool TryCreate(string path, out MeshFxParameterAccessor accessor)
            {
                accessor = null!;

                if (string.IsNullOrWhiteSpace(path))
                    return false;

                var segments = path.Split('.');
                if (segments.Length == 0)
                    return false;

                var members = new List<MemberInfo>(segments.Length);
                var currentType = typeof(MeshFxChannelDef);

                for (int i = 0; i < segments.Length; i++)
                {
                    if (!TryFindMember(currentType, segments[i], out var member))
                        return false;

                    members.Add(member);
                    currentType = GetMemberType(member);
                    if (currentType == null)
                        return false;
                }

                accessor = new MeshFxParameterAccessor(path, members.ToArray(), currentType);
                return true;
            }

            public bool TryGetValue(object root, out object? value)
            {
                value = null;
                if (root == null || _members.Length == 0)
                    return false;

                object? current = root;
                for (int i = 0; i < _members.Length; i++)
                {
                    if (current == null)
                        return false;

                    current = GetMemberValue(_members[i], current);
                }

                value = current;
                return true;
            }

            public bool TrySetValue(object root, object? value)
            {
                if (root == null || _members.Length == 0)
                    return false;

                object? current = root;
                for (int i = 0; i < _members.Length - 1; i++)
                {
                    if (current == null)
                        return false;

                    var next = GetMemberValue(_members[i], current);
                    if (next == null)
                    {
                        var memberType = GetMemberType(_members[i]);
                        if (memberType == null || memberType.IsValueType)
                            return false;

                        var ctor = memberType.GetConstructor(Type.EmptyTypes);
                        if (ctor == null)
                            return false;

                        next = ctor.Invoke(Array.Empty<object>());
                        if (!SetMemberValue(_members[i], current, next))
                            return false;
                    }

                    current = next;
                }

                if (current == null)
                    return false;

                var leaf = _members[_members.Length - 1];
                var leafType = GetMemberType(leaf);
                if (leafType == null)
                    return false;

                if (!IsAssignableValue(leafType, value))
                    return false;

                return SetMemberValue(leaf, current, value);
            }

            static bool TryFindMember(Type type, string segment, out MemberInfo member)
            {
                member = null!;
                if (string.IsNullOrWhiteSpace(segment))
                    return false;

                var f = type.GetField(segment, Binding);
                if (f != null)
                {
                    member = f;
                    return true;
                }

                var fields = type.GetFields(Binding);
                for (int i = 0; i < fields.Length; i++)
                {
                    if (string.Equals(fields[i].Name, segment, StringComparison.OrdinalIgnoreCase))
                    {
                        member = fields[i];
                        return true;
                    }
                }

                var p = type.GetProperty(segment, Binding);
                if (p != null && p.GetIndexParameters().Length == 0 && p.CanRead)
                {
                    member = p;
                    return true;
                }

                var props = type.GetProperties(Binding);
                for (int i = 0; i < props.Length; i++)
                {
                    if (!props[i].CanRead || props[i].GetIndexParameters().Length != 0)
                        continue;
                    if (string.Equals(props[i].Name, segment, StringComparison.OrdinalIgnoreCase))
                    {
                        member = props[i];
                        return true;
                    }
                }

                return false;
            }

            static Type? GetMemberType(MemberInfo member)
            {
                if (member is FieldInfo f)
                    return f.FieldType;
                if (member is PropertyInfo p)
                    return p.PropertyType;
                return null;
            }

            static object? GetMemberValue(MemberInfo member, object owner)
            {
                if (member is FieldInfo f)
                    return f.GetValue(owner);
                if (member is PropertyInfo p && p.CanRead)
                    return p.GetValue(owner);
                return null;
            }

            static bool SetMemberValue(MemberInfo member, object owner, object? value)
            {
                if (member is FieldInfo f)
                {
                    if (!IsAssignableValue(f.FieldType, value))
                        return false;
                    f.SetValue(owner, value);
                    return true;
                }

                if (member is PropertyInfo p && p.CanWrite)
                {
                    if (!IsAssignableValue(p.PropertyType, value))
                        return false;
                    p.SetValue(owner, value);
                    return true;
                }

                return false;
            }

            static bool IsAssignableValue(Type targetType, object? value)
            {
                if (value == null)
                    return !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null;

                var valueType = value.GetType();
                if (targetType.IsAssignableFrom(valueType))
                    return true;

                if (targetType.IsEnum && (valueType == typeof(int) || valueType == typeof(float)))
                    return true;

                return false;
            }
        }

        const string DefaultTag = "default";
        const string DefaultContext = "default";

        readonly IMeshFxChannelHubService _meshFxHub;
        readonly IMaterialFxPropertyRegistry? _materialFxRegistry;

        readonly Dictionary<string, MeshFxParameterAccessor> _accessorCache = new(StringComparer.Ordinal);
        readonly HashSet<string> _warnedInvalidPaths = new(StringComparer.Ordinal);
        readonly HashSet<string> _warnedUnsupportedTypes = new(StringComparer.Ordinal);
        readonly HashSet<string> _warnedUnknownMaterialKeys = new(StringComparer.Ordinal);

        readonly List<ActiveTrack> _tracks = new();
        readonly Dictionary<TrackKey, int> _trackIndices = new();
        IScopeNode? _scope;
        static readonly IDynamicContext LiteralReadContext = new LiteralDynamicContext();

        public MeshFxAnimationService(
            IMeshFxChannelHubService meshFxHub,
            IMaterialFxPropertyRegistry? materialFxRegistry = null)
        {
            _meshFxHub = meshFxHub;
            _materialFxRegistry = materialFxRegistry;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _scope = scope;
            _tracks.Clear();
            _trackIndices.Clear();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _scope = null;
            _tracks.Clear();
            _trackIndices.Clear();
        }

        public void Tick()
        {
            if (_tracks.Count == 0)
                return;

            var dt = Mathf.Max(0f, Time.deltaTime);
            for (int i = _tracks.Count - 1; i >= 0; i--)
            {
                var track = _tracks[i];
                track.ElapsedSeconds += dt;

                var t = track.DurationSeconds <= 0f ? 1f : Mathf.Clamp01(track.ElapsedSeconds / track.DurationSeconds);
                var eased = track.DurationSeconds <= 0f
                    ? 1f
                    : DOVirtual.EasedValue(0f, 1f, t, track.Easing);

                var value = track.Evaluate(eased);
                if (value != null)
                {
                    _ = track.Accessor.TrySetValue(track.Player.Def, value);
                }

                if (t >= 1f)
                {
                    RemoveTrackAt(i);
                }
            }
        }

        public bool Play(MeshFxChannelAnimationClip? clip)
        {
            if (clip == null)
                return false;

            return Play(
                clip.ChannelTag,
                clip.ContextTag,
                clip.ParameterEntries,
                clip.MaterialEntries,
                clip.ClearContextBeforePlay,
                clip.MaterialBasePriority);
        }

        sealed class LiteralDynamicContext : IDynamicContext
        {
            public IVarStore Vars => NullVarStore.Instance;
            public IScopeNode Scope => null!;
            public IScopeNode? CommandRootScope => null;

            public IScopeNode ResolveOtherScope(CommandTargetIdentityFilter filter)
            {
                _ = filter;
                return null!;
            }
        }

        public bool Play(
            string channelTag,
            string contextTag,
            IReadOnlyList<MeshFxParameterAnimationEntry>? parameterEntries,
            IReadOnlyList<MeshFxMaterialAnimationEntry>? materialEntries = null,
            bool clearContextBeforePlay = false,
            int materialBasePriority = 0)
        {
            channelTag = NormalizeTag(channelTag);
            contextTag = NormalizeContext(contextTag);

            if (!_meshFxHub.TryGetPlayer(channelTag, out var player) || player == null)
                return false;

            if (clearContextBeforePlay)
            {
                ClearContextInternal(channelTag, contextTag, player, clearMaterial: true);
            }

            var applied = false;

            if (parameterEntries != null)
            {
                for (int i = 0; i < parameterEntries.Count; i++)
                {
                    if (TryApplyParameterEntry(channelTag, contextTag, player, parameterEntries[i]))
                        applied = true;
                }
            }

            if (materialEntries != null && materialEntries.Count > 0)
            {
                if (ApplyMaterialEntries(player, contextTag, materialEntries, materialBasePriority))
                    applied = true;
            }

            return applied;
        }

        public bool ClearContext(string channelTag, string contextTag)
        {
            channelTag = NormalizeTag(channelTag);
            contextTag = NormalizeContext(contextTag);

            IMeshFxChannelPlayer? player = null;
            _meshFxHub.TryGetPlayer(channelTag, out player);
            return ClearContextInternal(channelTag, contextTag, player, clearMaterial: true);
        }

        public void ClearAll()
        {
            _tracks.Clear();
            _trackIndices.Clear();
        }

        bool TryApplyParameterEntry(
            string channelTag,
            string contextTag,
            IMeshFxChannelPlayer player,
            MeshFxParameterAnimationEntry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.Path))
                return false;

            if (!TryGetAccessor(entry.Path, out var accessor))
                return false;

            if (!accessor.TryGetValue(player.Def, out var currentValue))
                currentValue = null;

            if (!TryBuildEvaluator(accessor.Path, accessor.ValueType, currentValue, entry, out var immediateValue, out var evaluator))
                return false;

            var duration = Mathf.Max(0f, entry.DurationSeconds);
            if (duration <= 0f || evaluator == null)
            {
                return accessor.TrySetValue(player.Def, immediateValue);
            }

            var key = new TrackKey(channelTag, contextTag, accessor.Path);
            var track = new ActiveTrack
            {
                Key = key,
                Player = player,
                Accessor = accessor,
                DurationSeconds = duration,
                ElapsedSeconds = 0f,
                Easing = entry.Easing,
                Evaluate = evaluator
            };

            if (_trackIndices.TryGetValue(key, out var existingIndex))
            {
                _tracks[existingIndex] = track;
            }
            else
            {
                _trackIndices[key] = _tracks.Count;
                _tracks.Add(track);
            }

            return true;
        }

        bool ApplyMaterialEntries(
            IMeshFxChannelPlayer player,
            string contextTag,
            IReadOnlyList<MeshFxMaterialAnimationEntry> entries,
            int basePriority)
        {
            if (entries.Count == 0)
                return false;
            if (_materialFxRegistry == null)
                return false;

            var applied = false;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (string.IsNullOrWhiteSpace(entry.Key))
                    continue;

                if (!_materialFxRegistry.TryGetValueType(entry.Key, out var valueType))
                {
                    if (_warnedUnknownMaterialKeys.Add(entry.Key))
                    {
                        Debug.LogWarning($"[MeshFxAnimation] Unknown MaterialFx key '{entry.Key}'.");
                    }
                    continue;
                }

                var typedValue = entry.Value.ToTypedValue(valueType, CreateDynamicContext());
                var ok = player.SetMaterialLayer(
                    entry.Key,
                    contextTag,
                    typedValue,
                    entry.BlendMode,
                    Mathf.Max(0f, entry.DurationSeconds),
                    entry.Easing,
                    basePriority + entry.PriorityOffset,
                    entry.LifetimeSeconds);

                if (ok)
                    applied = true;
            }

            return applied;
        }

        IDynamicContext? CreateDynamicContext()
        {
            if (_scope == null)
                return null;

            var resolver = _scope.Resolver;
            var vars = resolver != null && resolver.TryResolve<IVarStore>(out var resolvedVars) && resolvedVars != null
                ? resolvedVars
                : NullVarStore.Instance;
            return new SimpleDynamicContext(vars, _scope);
        }

        bool ClearContextInternal(
            string channelTag,
            string contextTag,
            IMeshFxChannelPlayer? player,
            bool clearMaterial)
        {
            var removed = false;
            for (int i = _tracks.Count - 1; i >= 0; i--)
            {
                var key = _tracks[i].Key;
                if (!string.Equals(key.ChannelTag, channelTag, StringComparison.Ordinal))
                    continue;
                if (!string.Equals(key.ContextTag, contextTag, StringComparison.Ordinal))
                    continue;

                RemoveTrackAt(i);
                removed = true;
            }

            if (clearMaterial && player != null)
            {
                if (player.ClearMaterialContext(contextTag))
                    removed = true;
            }

            return removed;
        }

        void RemoveTrackAt(int index)
        {
            var lastIndex = _tracks.Count - 1;
            if (index < 0 || index > lastIndex)
                return;

            var removingKey = _tracks[index].Key;
            _trackIndices.Remove(removingKey);

            if (index != lastIndex)
            {
                var last = _tracks[lastIndex];
                _tracks[index] = last;
                _trackIndices[last.Key] = index;
            }

            _tracks.RemoveAt(lastIndex);
        }

        bool TryGetAccessor(string path, out MeshFxParameterAccessor accessor)
        {
            if (_accessorCache.TryGetValue(path, out accessor!))
                return true;

            if (!MeshFxParameterAccessor.TryCreate(path, out accessor!))
            {
                if (_warnedInvalidPaths.Add(path))
                {
                    Debug.LogWarning($"[MeshFxAnimation] Invalid parameter path '{path}'.");
                }
                return false;
            }

            _accessorCache[path] = accessor;
            return true;
        }

        bool TryBuildEvaluator(
            string parameterPath,
            Type valueType,
            object? currentValue,
            MeshFxParameterAnimationEntry entry,
            out object? immediateValue,
            out Func<float, object?>? evaluator)
        {
            immediateValue = null;
            evaluator = null;

            var kind = ResolveParameterValueKind(valueType);
            if (kind == ParameterValueKind.Unsupported)
            {
                var key = valueType.FullName ?? valueType.Name;
                if (_warnedUnsupportedTypes.Add(key))
                {
                    Debug.LogWarning($"[MeshFxAnimation] Unsupported parameter type '{key}'.");
                }
                return false;
            }

            switch (kind)
            {
                case ParameterValueKind.Float:
                    {
                        var start = currentValue is float f ? f : 0f;
                        var requested = ExtractFloat(entry.Value);
                        var target = BlendFloat(start, requested, entry.BlendMode);
                        immediateValue = target;
                        evaluator = t => Mathf.LerpUnclamped(start, target, t);
                        return true;
                    }
                case ParameterValueKind.Int:
                {
                    var start = currentValue is int i ? i : 0;
                    var requested = ExtractInt(entry.Value);
                    var target = BlendInt(start, requested, entry.BlendMode);
                    immediateValue = target;
                    evaluator = t => Mathf.RoundToInt(Mathf.LerpUnclamped(start, target, t));
                    return true;
                }
                case ParameterValueKind.UInt:
                {
                    var start = currentValue is uint u ? u : 0u;
                    var requested = ExtractUInt(entry.Value);
                    var target = BlendUInt(start, requested, entry.BlendMode);
                    immediateValue = target;

                    var startD = (double)start;
                    var targetD = (double)target;
                    evaluator = t =>
                    {
                        var v = startD + (targetD - startD) * t;
                        if (v <= 0d)
                            return 0u;
                        if (v >= uint.MaxValue)
                            return uint.MaxValue;
                        return (uint)Math.Round(v);
                    };
                    return true;
                }
                case ParameterValueKind.Bool:
                {
                        var start = currentValue is bool b && b;
                        var target = ExtractBool(entry.Value);
                        immediateValue = target;
                        evaluator = t => t < 1f ? start : target;
                        return true;
                    }
                case ParameterValueKind.Vector2:
                    {
                        var start = currentValue is Vector2 v ? v : Vector2.zero;
                        var requested = ExtractVector2(entry.Value);
                        var target = BlendVector2(start, requested, entry.BlendMode);
                        immediateValue = target;
                        evaluator = t => Vector2.LerpUnclamped(start, target, t);
                        return true;
                    }
                case ParameterValueKind.Vector3:
                    {
                        var start = currentValue is Vector3 v ? v : Vector3.zero;
                        var requested = ExtractVector3(entry.Value);
                        var target = BlendVector3(start, requested, entry.BlendMode);
                        immediateValue = target;
                        evaluator = t => Vector3.LerpUnclamped(start, target, t);
                        return true;
                    }
                case ParameterValueKind.Color:
                    {
                        var start = currentValue is Color c ? c : Color.white;
                        var requested = ExtractColor(entry.Value);
                        var target = BlendColor(start, requested, entry.BlendMode);
                        immediateValue = target;
                        evaluator = t => Color.LerpUnclamped(start, target, t);
                        return true;
                    }
                case ParameterValueKind.Enum:
                    {
                        var underlyingStart = currentValue != null ? Convert.ToInt32(currentValue) : 0;
                        var requested = ExtractInt(entry.Value);
                        var enumValue = Enum.ToObject(valueType, requested);
                        immediateValue = enumValue;
                        evaluator = t => t < 1f ? Enum.ToObject(valueType, underlyingStart) : enumValue;
                        return true;
                    }
                case ParameterValueKind.DynamicFloat:
                    {
                        var start = TryGetDynamicFloat(currentValue, out var startValue)
                            ? startValue
                            : 0f;
                        var requested = ExtractFloat(entry.Value);
                        var target = BlendFloat(start, requested, entry.BlendMode);
                        immediateValue = DynamicValueExtensions.FromLiteral(target);
                        evaluator = t => DynamicValueExtensions.FromLiteral(Mathf.LerpUnclamped(start, target, t));
                        return true;
                    }
                case ParameterValueKind.DynamicVector2:
                    {
                        var start = TryGetDynamicVector2(currentValue, out var startValue)
                            ? startValue
                            : Vector2.zero;

                        if (IsSingleDirectionDirectionPath(parameterPath) &&
                            (entry.Value.Type == MeshFxAnimationValueType.Float || entry.Value.Type == MeshFxAnimationValueType.Int))
                        {
                            var startDegrees = DirectionToDegrees(start);
                            var requestedDegrees = ExtractFloat(entry.Value);
                            var targetDegrees = BlendFloat(startDegrees, requestedDegrees, entry.BlendMode);

                            immediateValue = DynamicValueExtensions.FromLiteral(DirectionFromDegrees(targetDegrees));
                            evaluator = t =>
                            {
                                var deg = Mathf.LerpUnclamped(startDegrees, targetDegrees, t);
                                return DynamicValueExtensions.FromLiteral(DirectionFromDegrees(deg));
                            };
                            return true;
                        }

                        var requested = ExtractVector2(entry.Value);
                        var target = BlendVector2(start, requested, entry.BlendMode);
                        immediateValue = DynamicValueExtensions.FromLiteral(target);
                        evaluator = t => DynamicValueExtensions.FromLiteral(Vector2.LerpUnclamped(start, target, t));
                        return true;
                    }
                case ParameterValueKind.DynamicVector3:
                    {
                        var start = TryGetDynamicVector3(currentValue, out var startValue)
                            ? startValue
                            : Vector3.zero;
                        var requested = ExtractVector3(entry.Value);
                        var target = BlendVector3(start, requested, entry.BlendMode);
                        immediateValue = DynamicValueExtensions.FromLiteral(target);
                        evaluator = t => DynamicValueExtensions.FromLiteral(Vector3.LerpUnclamped(start, target, t));
                        return true;
                    }
                case ParameterValueKind.Curve:
                    {
                        var start = currentValue as AnimationCurve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
                        var target = entry.Value.CurveValue ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
                        immediateValue = target;
                        evaluator = t => t < 1f ? start : target;
                        return true;
                    }
                default:
                    return false;
            }
        }

        static ParameterValueKind ResolveParameterValueKind(Type valueType)
        {
            if (valueType == typeof(float))
                return ParameterValueKind.Float;
            if (valueType == typeof(int))
                return ParameterValueKind.Int;
            if (valueType == typeof(uint))
                return ParameterValueKind.UInt;
            if (valueType == typeof(bool))
                return ParameterValueKind.Bool;
            if (valueType == typeof(Vector2))
                return ParameterValueKind.Vector2;
            if (valueType == typeof(Vector3))
                return ParameterValueKind.Vector3;
            if (valueType == typeof(Color))
                return ParameterValueKind.Color;
            if (valueType == typeof(AnimationCurve))
                return ParameterValueKind.Curve;
            if (valueType.IsEnum)
                return ParameterValueKind.Enum;

            if (valueType.IsGenericType &&
                valueType.GetGenericTypeDefinition() == typeof(DynamicValue<>))
            {
                var arg = valueType.GetGenericArguments()[0];
                if (arg == typeof(float))
                    return ParameterValueKind.DynamicFloat;
                if (arg == typeof(Vector2))
                    return ParameterValueKind.DynamicVector2;
                if (arg == typeof(Vector3))
                    return ParameterValueKind.DynamicVector3;
            }

            return ParameterValueKind.Unsupported;
        }

        static bool TryGetDynamicFloat(object? value, out float result)
        {
            result = 0f;
            if (value is not DynamicValue<float> dynamicValue)
                return false;
            if (!string.Equals(dynamicValue.SourceTypeName, "Literal", StringComparison.Ordinal))
                return false;
            return dynamicValue.TryGet(LiteralReadContext, out result);
        }

        static bool TryGetDynamicVector2(object? value, out Vector2 result)
        {
            result = Vector2.zero;
            if (value is not DynamicValue<Vector2> dynamicValue)
                return false;
            if (!string.Equals(dynamicValue.SourceTypeName, "Literal", StringComparison.Ordinal))
                return false;
            return dynamicValue.TryGet(LiteralReadContext, out result);
        }

        static bool TryGetDynamicVector3(object? value, out Vector3 result)
        {
            result = Vector3.zero;
            if (value is not DynamicValue<Vector3> dynamicValue)
                return false;
            if (!string.Equals(dynamicValue.SourceTypeName, "Literal", StringComparison.Ordinal))
                return false;
            return dynamicValue.TryGet(LiteralReadContext, out result);
        }

        static float BlendFloat(float current, float requested, MeshFxParameterBlendMode blendMode)
        {
            return blendMode switch
            {
                MeshFxParameterBlendMode.Add => current + requested,
                MeshFxParameterBlendMode.Multiply => current * requested,
                _ => requested,
            };
        }

        static int BlendInt(int current, int requested, MeshFxParameterBlendMode blendMode)
        {
            return blendMode switch
            {
                MeshFxParameterBlendMode.Add => current + requested,
                MeshFxParameterBlendMode.Multiply => current * requested,
                _ => requested,
            };
        }

        static uint BlendUInt(uint current, uint requested, MeshFxParameterBlendMode blendMode)
        {
            switch (blendMode)
            {
                case MeshFxParameterBlendMode.Add:
                {
                    var sum = (ulong)current + requested;
                    return sum >= uint.MaxValue ? uint.MaxValue : (uint)sum;
                }
                case MeshFxParameterBlendMode.Multiply:
                {
                    var mul = (ulong)current * requested;
                    return mul >= uint.MaxValue ? uint.MaxValue : (uint)mul;
                }
                default:
                    return requested;
            }
        }

        static Vector2 BlendVector2(Vector2 current, Vector2 requested, MeshFxParameterBlendMode blendMode)
        {
            return blendMode switch
            {
                MeshFxParameterBlendMode.Add => current + requested,
                MeshFxParameterBlendMode.Multiply => new Vector2(current.x * requested.x, current.y * requested.y),
                _ => requested,
            };
        }

        static Vector3 BlendVector3(Vector3 current, Vector3 requested, MeshFxParameterBlendMode blendMode)
        {
            return blendMode switch
            {
                MeshFxParameterBlendMode.Add => current + requested,
                MeshFxParameterBlendMode.Multiply => new Vector3(
                    current.x * requested.x,
                    current.y * requested.y,
                    current.z * requested.z),
                _ => requested,
            };
        }

        static Color BlendColor(Color current, Color requested, MeshFxParameterBlendMode blendMode)
        {
            return blendMode switch
            {
                MeshFxParameterBlendMode.Add => current + requested,
                MeshFxParameterBlendMode.Multiply => new Color(
                    current.r * requested.r,
                    current.g * requested.g,
                    current.b * requested.b,
                    current.a * requested.a),
                _ => requested,
            };
        }

        static float ExtractFloat(in MeshFxAnimationValue value)
        {
            return value.Type switch
            {
                MeshFxAnimationValueType.Float => value.FloatValue,
                MeshFxAnimationValueType.Int => value.IntValue,
                MeshFxAnimationValueType.Bool => value.BoolValue ? 1f : 0f,
                MeshFxAnimationValueType.Vector2 => value.Vector2Value.x,
                MeshFxAnimationValueType.Vector3 => value.Vector3Value.x,
                MeshFxAnimationValueType.Color => value.ColorValue.r,
                _ => 0f,
            };
        }

        static int ExtractInt(in MeshFxAnimationValue value)
        {
            return value.Type switch
            {
                MeshFxAnimationValueType.Int => value.IntValue,
                MeshFxAnimationValueType.Float => Mathf.RoundToInt(value.FloatValue),
                MeshFxAnimationValueType.Bool => value.BoolValue ? 1 : 0,
                MeshFxAnimationValueType.Vector2 => Mathf.RoundToInt(value.Vector2Value.x),
                MeshFxAnimationValueType.Vector3 => Mathf.RoundToInt(value.Vector3Value.x),
                MeshFxAnimationValueType.Color => Mathf.RoundToInt(value.ColorValue.r),
                _ => 0,
            };
        }

        static bool ExtractBool(in MeshFxAnimationValue value)
        {
            return value.Type switch
            {
                MeshFxAnimationValueType.Bool => value.BoolValue,
                MeshFxAnimationValueType.Int => value.IntValue != 0,
                MeshFxAnimationValueType.Float => !Mathf.Approximately(value.FloatValue, 0f),
                MeshFxAnimationValueType.Vector2 => !Mathf.Approximately(value.Vector2Value.sqrMagnitude, 0f),
                MeshFxAnimationValueType.Vector3 => !Mathf.Approximately(value.Vector3Value.sqrMagnitude, 0f),
                MeshFxAnimationValueType.Color => !Mathf.Approximately(value.ColorValue.maxColorComponent, 0f),
                _ => false,
            };
        }

        static uint ExtractUInt(in MeshFxAnimationValue value)
        {
            return value.Type switch
            {
                MeshFxAnimationValueType.Int => value.IntValue < 0 ? 0u : (uint)value.IntValue,
                MeshFxAnimationValueType.Float => value.FloatValue <= 0f ? 0u : (uint)Mathf.RoundToInt(value.FloatValue),
                MeshFxAnimationValueType.Bool => value.BoolValue ? 1u : 0u,
                MeshFxAnimationValueType.Vector2 => value.Vector2Value.x <= 0f ? 0u : (uint)Mathf.RoundToInt(value.Vector2Value.x),
                MeshFxAnimationValueType.Vector3 => value.Vector3Value.x <= 0f ? 0u : (uint)Mathf.RoundToInt(value.Vector3Value.x),
                MeshFxAnimationValueType.Color => value.ColorValue.r <= 0f ? 0u : (uint)Mathf.RoundToInt(value.ColorValue.r),
                _ => 0u,
            };
        }

        static Vector2 ExtractVector2(in MeshFxAnimationValue value)
        {
            return value.Type switch
            {
                MeshFxAnimationValueType.Vector2 => value.Vector2Value,
                MeshFxAnimationValueType.Vector3 => new Vector2(value.Vector3Value.x, value.Vector3Value.y),
                MeshFxAnimationValueType.Float => new Vector2(value.FloatValue, value.FloatValue),
                MeshFxAnimationValueType.Int => new Vector2(value.IntValue, value.IntValue),
                MeshFxAnimationValueType.Color => new Vector2(value.ColorValue.r, value.ColorValue.g),
                _ => Vector2.zero,
            };
        }

        static Vector3 ExtractVector3(in MeshFxAnimationValue value)
        {
            return value.Type switch
            {
                MeshFxAnimationValueType.Vector3 => value.Vector3Value,
                MeshFxAnimationValueType.Vector2 => new Vector3(value.Vector2Value.x, value.Vector2Value.y, 0f),
                MeshFxAnimationValueType.Float => new Vector3(value.FloatValue, value.FloatValue, value.FloatValue),
                MeshFxAnimationValueType.Int => new Vector3(value.IntValue, value.IntValue, value.IntValue),
                MeshFxAnimationValueType.Color => new Vector3(value.ColorValue.r, value.ColorValue.g, value.ColorValue.b),
                _ => Vector3.zero,
            };
        }

        static Color ExtractColor(in MeshFxAnimationValue value)
        {
            return value.Type switch
            {
                MeshFxAnimationValueType.Color => value.ColorValue,
                MeshFxAnimationValueType.Float => new Color(value.FloatValue, value.FloatValue, value.FloatValue, 1f),
                MeshFxAnimationValueType.Int => new Color(value.IntValue, value.IntValue, value.IntValue, 1f),
                MeshFxAnimationValueType.Vector2 => new Color(value.Vector2Value.x, value.Vector2Value.y, 0f, 1f),
                MeshFxAnimationValueType.Vector3 => new Color(value.Vector3Value.x, value.Vector3Value.y, value.Vector3Value.z, 1f),
                MeshFxAnimationValueType.Bool => value.BoolValue ? Color.white : Color.black,
                _ => Color.white,
            };
        }

        static string NormalizeTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return DefaultTag;
            return tag.Trim();
        }

        static string NormalizeContext(string contextTag)
        {
            if (string.IsNullOrWhiteSpace(contextTag))
                return DefaultContext;
            return contextTag.Trim();
        }

        static bool IsSingleDirectionDirectionPath(string parameterPath)
        {
            return string.Equals(parameterPath, "singleDirectionSettings.Direction", StringComparison.Ordinal);
        }

        static float DirectionToDegrees(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 1e-8f)
                return 0f;

            return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        }

        static Vector2 DirectionFromDegrees(float degrees)
        {
            var rad = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }
    }
}
