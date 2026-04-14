#nullable enable

using Game.Commands.VNext;
using UnityEngine;

namespace Game.Common
{
    public static class VarStoreEntryValueKindConverter
    {
        public delegate bool EntryConverter(in VarStorePayload.Entry entry, out DynamicVariant value);

        public static void Register(VarStorePayload.EntryValueKind kind, EntryConverter? converter)
        {
            _ = kind;
            _ = converter;
        }

        public static void RegisterTypeKey(string? typeKey, EntryConverter? converter, bool isManagedReferenceOutput)
        {
            _ = typeKey;
            _ = converter;
            _ = isManagedReferenceOutput;
        }

        public static bool TryConvertToVariant(in VarStorePayload.Entry entry, out DynamicVariant value)
        {
            return TryConvertToVariant(in entry, EmptyDynamicContext.Instance, out value);
        }

        public static bool TryConvertToVariant(in VarStorePayload.Entry entry, IDynamicContext? context, out DynamicVariant value)
        {
            var evaluated = entry.Value.Evaluate(context ?? EmptyDynamicContext.Instance);
            if (entry.Kind == VarStorePayload.EntryValueKind.Auto)
            {
                value = evaluated;
                return true;
            }

            return TryCoerce(entry.Kind, in evaluated, out value);
        }

        public static bool IsManagedReferenceOutput(in VarStorePayload.Entry entry)
            => entry.Kind == VarStorePayload.EntryValueKind.ManagedRef
               || entry.Kind == VarStorePayload.EntryValueKind.CommandListData
               || entry.Kind == VarStorePayload.EntryValueKind.Table;

        public static bool IsManagedReferenceKind(VarStorePayload.EntryValueKind kind)
        {
            return kind == VarStorePayload.EntryValueKind.ManagedRef
                || kind == VarStorePayload.EntryValueKind.CommandListData
                || kind == VarStorePayload.EntryValueKind.Table;
        }

        public static bool TryCoerceToKind(VarStorePayload.EntryValueKind kind, in DynamicVariant source, out DynamicVariant value)
        {
            if (kind == VarStorePayload.EntryValueKind.Auto)
            {
                value = source;
                return true;
            }

            return TryCoerce(kind, in source, out value);
        }

        static bool TryCoerce(VarStorePayload.EntryValueKind kind, in DynamicVariant source, out DynamicVariant value)
        {
            value = DynamicVariant.Null;

            switch (kind)
            {
                case VarStorePayload.EntryValueKind.Null:
                    value = DynamicVariant.Null;
                    return true;

                case VarStorePayload.EntryValueKind.Bool:
                    if (source.TryGet<bool>(out var b))
                    {
                        value = DynamicVariant.FromBool(b);
                        return true;
                    }
                    return false;

                case VarStorePayload.EntryValueKind.Int:
                    if (source.TryGet<int>(out var i))
                    {
                        value = DynamicVariant.FromInt(i);
                        return true;
                    }
                    return false;

                case VarStorePayload.EntryValueKind.Float:
                    if (source.TryGet<float>(out var f))
                    {
                        value = DynamicVariant.FromFloat(f);
                        return true;
                    }
                    return false;

                case VarStorePayload.EntryValueKind.String:
                    if (source.TryGet<string>(out var s))
                    {
                        value = DynamicVariant.FromString(s ?? string.Empty);
                        return true;
                    }
                    return false;

                case VarStorePayload.EntryValueKind.Vector2:
                    if (source.TryGet<Vector2>(out var v2))
                    {
                        value = DynamicVariant.FromVector2(v2);
                        return true;
                    }
                    return false;

                case VarStorePayload.EntryValueKind.Vector3:
                    if (source.TryGet<Vector3>(out var v3))
                    {
                        value = DynamicVariant.FromVector3(v3);
                        return true;
                    }
                    return false;

                case VarStorePayload.EntryValueKind.Vector4:
                    if (source.TryGet<Vector4>(out var v4))
                    {
                        value = DynamicVariant.FromVector4(v4);
                        return true;
                    }
                    return false;

                case VarStorePayload.EntryValueKind.Color:
                    if (source.TryGet<Color>(out var c))
                    {
                        value = DynamicVariant.FromColor(c);
                        return true;
                    }
                    return false;

                case VarStorePayload.EntryValueKind.UnityObject:
                    if (source.TryGet<Object>(out var obj))
                    {
                        value = DynamicVariant.FromUnityObject(obj);
                        return true;
                    }
                    return false;

                case VarStorePayload.EntryValueKind.ManagedRef:
                    if (source.Kind == ValueKind.ManagedRef && source.AsManagedRef != null)
                    {
                        value = DynamicVariant.FromManagedRef(source.AsManagedRef);
                        return value.Kind == ValueKind.ManagedRef;
                    }
                    return false;

                case VarStorePayload.EntryValueKind.CommandListData:
                    if (source.TryGet<CommandListData>(out var list) && list != null)
                    {
                        value = DynamicVariant.FromManagedRef(list);
                        return value.Kind == ValueKind.ManagedRef;
                    }
                    return false;

                case VarStorePayload.EntryValueKind.Table:
                    if (source.Kind == ValueKind.ManagedRef && source.AsManagedRef is Table table)
                    {
                        value = DynamicVariant.FromManagedRef(table);
                        return true;
                    }

                    if (source.Kind == ValueKind.ManagedRef && source.AsManagedRef is VarStorePayload legacyPayload)
                    {
                        value = DynamicVariant.FromManagedRef(Table.FromLegacy(legacyPayload));
                        return true;
                    }

                    return false;

                case VarStorePayload.EntryValueKind.Auto:
                    value = source;
                    return true;

                default:
                    return false;
            }
        }
    }
}
