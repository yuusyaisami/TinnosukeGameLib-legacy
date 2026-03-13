#nullable enable

using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Flow
{
    /// <summary>
    /// コンパイル時のレポートと警告/エラーを格納します。
    /// <para>Flow のコンパイル結果をユーザーに提示するための構造体群です。</para>
    /// </summary>
    [Serializable]
    public sealed class FlowCompileReport
    {
        public enum Severity
        {
            Info = 0,
            Warning = 1,
            Error = 2,
        }

        [Serializable]
        public struct Entry
        {
            public Severity Level;
            public string Message;

            public Entry(Severity level, string message)
            {
                Level = level;
                Message = message ?? string.Empty;
            }

            public override string ToString() => $"{Level}: {Message}";
        }

        [SerializeField, ReadOnly]
        List<Entry> entries = new();

        /// <summary>収集されたエントリ一覧。</summary>
        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>エラーが一つでも含まれているかどうか。</summary>
        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < entries.Count; i++)
                    if (entries[i].Level == Severity.Error)
                        return true;
                return false;
            }
        }

        /// <summary>エントリをすべて削除します。</summary>
        public void Clear() => entries.Clear();

        /// <summary>情報エントリを追加します。</summary>
        public void Info(string message) => entries.Add(new Entry(Severity.Info, message));
        /// <summary>警告エントリを追加します。</summary>
        public void Warning(string message) => entries.Add(new Entry(Severity.Warning, message));
        /// <summary>エラーエントリを追加します。</summary>
        public void Error(string message) => entries.Add(new Entry(Severity.Error, message));

        /// <summary>複数行の文字列として出力します（デバッグ表示等に便利）。</summary>
        public string ToMultilineString()
        {
            if (entries.Count == 0)
                return string.Empty;
            var sb = new System.Text.StringBuilder(entries.Count * 32);
            for (int i = 0; i < entries.Count; i++)
            {
                sb.Append(entries[i].ToString());
                if (i + 1 < entries.Count) sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
