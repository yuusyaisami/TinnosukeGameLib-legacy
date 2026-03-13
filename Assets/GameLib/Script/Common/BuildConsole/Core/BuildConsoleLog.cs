#nullable enable
using System;
using UnityEngine;
using VContainer;

namespace Game.BuildConsole
{
    public static class BuildConsoleLog
    {
        static IBuildConsole? s_activeConsole;

        internal static void Bind(IBuildConsole console)
        {
            if (console != null)
            {
                s_activeConsole = console;
            }
        }

        internal static void Unbind(IBuildConsole console)
        {
            if (ReferenceEquals(s_activeConsole, console))
            {
                s_activeConsole = null;
            }
        }

        public static bool TryResolve(IScopeNode? scope, out IBuildConsole? console)
        {
            var current = scope;
            while (current != null)
            {
                var resolver = current.Resolver;
                if (resolver != null &&
                    resolver.TryResolve<IBuildConsole>(out var resolved) &&
                    resolved != null)
                {
                    console = resolved;
                    return true;
                }
                current = current.Parent;
            }

            console = s_activeConsole;
            return console != null;
        }

        public static bool TryResolve(IObjectResolver? resolver, out IBuildConsole? console)
        {
            if (resolver != null &&
                resolver.TryResolve<IBuildConsole>(out var resolved) &&
                resolved != null)
            {
                console = resolved;
                return true;
            }

            if (resolver != null &&
                resolver.TryResolve<IScopeNode>(out var scope) &&
                scope != null)
            {
                return TryResolve(scope, out console);
            }

            console = s_activeConsole;
            return console != null;
        }

        public static void Log(string message, LogType logType = LogType.Log, string? stackTrace = null)
        {
            if (s_activeConsole != null)
            {
                s_activeConsole.Log(message, logType, stackTrace);
            }
        }

        public static void Scope(IScopeNode? scope, string message, LogType logType = LogType.Log, string? stackTrace = null)
        {
            if (TryResolve(scope, out var console) && console != null)
            {
                console.LogScope(scope, message, logType, stackTrace);
            }
        }

        public static void Resolver(IObjectResolver? resolver, string message, LogType logType = LogType.Log, string? stackTrace = null)
        {
            if (TryResolve(resolver, out var console) && console != null)
            {
                console.LogResolver(resolver, message, logType, stackTrace);
            }
        }
    }

    public static class BuildConsoleLogExtensions
    {
        public static void LogToConsole(this IScopeNode? scope, string message, LogType logType = LogType.Log, string? stackTrace = null)
        {
            BuildConsoleLog.Scope(scope, message, logType, stackTrace);
        }

        public static void LogToConsole(this IObjectResolver? resolver, string message, LogType logType = LogType.Log, string? stackTrace = null)
        {
            BuildConsoleLog.Resolver(resolver, message, logType, stackTrace);
        }
    }
}
