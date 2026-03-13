#nullable enable
using System;
using System.Text;
using UnityEngine;

namespace Game.Save
{
    public sealed class UnityJsonSaveSerializer : ISaveSerializer
    {
        static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public SaveSerializeResult TrySerialize<T>(in T value)
        {
            try
            {
                // JsonUtility only supports classes/structs with serializable fields.
                var json = JsonUtility.ToJson(value, prettyPrint: false);
                if (string.IsNullOrEmpty(json))
                    return new SaveSerializeResult(SaveSerializerStatus.SerializeError, null, "JsonUtility.ToJson returned empty.");

                var bytes = Utf8NoBom.GetBytes(json);
                return new SaveSerializeResult(SaveSerializerStatus.Success, bytes);
            }
            catch (Exception ex)
            {
                return new SaveSerializeResult(SaveSerializerStatus.SerializeError, null, ex.Message);
            }
        }

        public SaveDeserializeResult TryDeserialize<T>(byte[] bytes, out T value)
        {
            value = default!;

            if (bytes == null || bytes.Length == 0)
                return new SaveDeserializeResult(SaveSerializerStatus.InvalidInput, "Bytes is null or empty.");

            try
            {
                var json = Utf8NoBom.GetString(bytes);
                if (string.IsNullOrEmpty(json))
                    return new SaveDeserializeResult(SaveSerializerStatus.DeserializeError, "UTF8 decode returned empty.");

                value = JsonUtility.FromJson<T>(json);
                return new SaveDeserializeResult(SaveSerializerStatus.Success);
            }
            catch (Exception ex)
            {
                value = default!;
                return new SaveDeserializeResult(SaveSerializerStatus.DeserializeError, ex.Message);
            }
        }
    }
}
