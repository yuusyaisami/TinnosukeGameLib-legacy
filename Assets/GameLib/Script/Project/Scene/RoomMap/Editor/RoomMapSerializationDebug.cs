#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

public static class RoomMapSerializationDebug
{
    [MenuItem("Tools/RoomMap/Debug/Dump Selection Serialized Properties")]
    static void DumpSelectionSerializedProperties()
    {
        var obj = Selection.activeObject;
        if (obj == null)
        {
            Debug.Log("No selection.");
            return;
        }

        var so = new SerializedObject(obj);
        var it = so.GetIterator();

        var sb = new StringBuilder();
        sb.AppendLine($"[Serialized Dump] name={obj.name} type={obj.GetType().FullName}");

        bool hasTileEnum = false;

        if (it.NextVisible(true))
        {
            do
            {
                sb.AppendLine($"- {it.propertyPath} ({it.propertyType})");
                if (it.propertyPath == "TileEnum" || it.propertyPath == "tileEnum")
                    hasTileEnum = true;
            }
            while (it.NextVisible(false));
        }

        Debug.Log(sb.ToString(), obj);
        Debug.Log($"Has serialized TileEnum/tileEnum property? => {hasTileEnum}", obj);

        // direct check
        var p1 = so.FindProperty("TileEnum");
        var p2 = so.FindProperty("tileEnum");
        Debug.Log($"FindProperty(\"TileEnum\") => {(p1 == null ? "null" : p1.propertyType.ToString())}", obj);
        Debug.Log($"FindProperty(\"tileEnum\") => {(p2 == null ? "null" : p2.propertyType.ToString())}", obj);
    }
}
#endif
