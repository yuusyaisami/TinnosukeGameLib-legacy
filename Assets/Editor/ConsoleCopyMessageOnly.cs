using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ConsoleCopyMessageOnly
{
    static string _before;
    static int _retry;

    [MenuItem("Tools/Console/Copy Selected Messages Only (strip stack)")]
    private static void CopySelectedMessagesOnly()
    {
        _before = GUIUtility.systemCopyBuffer ?? "";
        _retry = 0;

        // Unityの通常コピー（複数選択もここに依存）
        EditorApplication.ExecuteMenuItem("Edit/Copy");

        // クリップボード反映は1フレ遅れることがあるので遅延で剥がす
        EditorApplication.delayCall += TryStripAfterCopy;
    }

    [MenuItem("Tools/Console/Strip Stack Trace From Clipboard")]
    private static void StripFromClipboard()
    {
        var src = GUIUtility.systemCopyBuffer ?? "";
        if (string.IsNullOrEmpty(src))
        {
            Notify("Clipboard is empty");
            return;
        }

        var stripped = StripStackLines(src);
        GUIUtility.systemCopyBuffer = stripped;
        EditorGUIUtility.systemCopyBuffer = stripped;
        Notify($"Stripped. chars={stripped.Length}");
    }

    private static void TryStripAfterCopy()
    {
        _retry++;

        var now = GUIUtility.systemCopyBuffer ?? "";

        // まだコピー結果が入ってない（or 選択無しで変わってない）
        if ((string.IsNullOrEmpty(now) || now == _before) && _retry < 12)
        {
            EditorApplication.delayCall += TryStripAfterCopy;
            return;
        }

        if (string.IsNullOrEmpty(now) || now == _before)
        {
            Notify("Copy result not detected (no selection?)");
            return;
        }

        var stripped = StripStackLines(now);

        GUIUtility.systemCopyBuffer = stripped;
        EditorGUIUtility.systemCopyBuffer = stripped;

        Notify($"Copied message only. lines={stripped.Count(c => c == '\n') + 1}");
    }

    // 「スタックトレースっぽい行」を落とす（複数ログが連結されても成立する）
    private static string StripStackLines(string text)
    {
        var norm = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = norm.Split('\n');

        var sb = new StringBuilder(norm.Length);
        bool wroteAny = false;

        foreach (var raw in lines)
        {
            var line = raw;

            if (IsStackLine(line))
                continue;

            // 空行は詰めすぎない程度に残す（連続空行は1個に圧縮）
            if (line.Length == 0)
            {
                if (wroteAny && sb.Length > 0 && sb[sb.Length - 1] != '\n')
                    sb.Append('\n');
                continue;
            }

            if (wroteAny) sb.Append('\n');
            sb.Append(line);
            wroteAny = true;
        }

        return sb.ToString().TrimEnd('\n');
    }

    private static bool IsStackLine(string line)
    {
        if (string.IsNullOrEmpty(line)) return false;

        // Unity系
        if (line.StartsWith("UnityEngine.", StringComparison.Ordinal)) return true;
        if (line.StartsWith("UnityEditor.", StringComparison.Ordinal)) return true;

        // Odin/Sirenixや自作namespaceも、スタックとして出る行を落とす（"(at " を最優先）
        if (line.Contains(" (at ", StringComparison.Ordinal)) return true;

        // 例外の一般的な "at ..." 形式
        if (line.StartsWith("at ", StringComparison.Ordinal)) return true;
        if (line.StartsWith("(wrapper", StringComparison.Ordinal)) return true;

        // Consoleのコピーでよく混じる（Unityの内部処理）
        if (line.Contains("UnityEngine.GUIUtility:ProcessEvent", StringComparison.Ordinal)) return true;

        return false;
    }

    private static void Notify(string msg)
    {
        // Console を汚さない（Debug.Logしない）
        EditorWindow.focusedWindow?.ShowNotification(new GUIContent(msg));
    }
}
