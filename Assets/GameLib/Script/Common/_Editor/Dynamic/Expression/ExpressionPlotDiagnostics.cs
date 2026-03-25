#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Game.Common.Editor
{
    public enum ExpressionPlotDiagnosticSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
    }

    [Serializable]
    public readonly struct ExpressionPlotDiagnostic
    {
        public readonly ExpressionPlotDiagnosticSeverity Severity;
        public readonly string Message;

        public ExpressionPlotDiagnostic(ExpressionPlotDiagnosticSeverity severity, string message)
        {
            Severity = severity;
            Message = message ?? string.Empty;
        }
    }

    public sealed class ExpressionPlotDiagnostics
    {
        readonly List<ExpressionPlotDiagnostic> _items = new();

        public IReadOnlyList<ExpressionPlotDiagnostic> Items => _items;

        public bool HasError
        {
            get
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    if (_items[i].Severity == ExpressionPlotDiagnosticSeverity.Error)
                        return true;
                }

                return false;
            }
        }

        public void Clear()
        {
            _items.Clear();
        }

        public void AddInfo(string message)
        {
            _items.Add(new ExpressionPlotDiagnostic(ExpressionPlotDiagnosticSeverity.Info, message));
        }

        public void AddWarning(string message)
        {
            _items.Add(new ExpressionPlotDiagnostic(ExpressionPlotDiagnosticSeverity.Warning, message));
        }

        public void AddError(string message)
        {
            _items.Add(new ExpressionPlotDiagnostic(ExpressionPlotDiagnosticSeverity.Error, message));
        }
    }
}
#endif
