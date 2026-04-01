// Game.Common.ExpressionFunctionRegistry.cs
//
// Expression で使用する関数群のレジストリ。
// 例: Random(), Min(a,b), Max(a,b), Sin(x), Cos(x), SinOut(t)
//
// 追加方法:
// - Register("FuncName", minArgs, maxArgs, impl)
// - impl は DynamicVariant[] を受け取り DynamicVariant を返す

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Common
{
    public static class ExpressionFunctionRegistry
    {
        public readonly struct FunctionDef
        {
            public readonly int MinArgs;
            public readonly int MaxArgs;
            public readonly Func<DynamicVariant[], DynamicVariant> Impl;

            public FunctionDef(int minArgs, int maxArgs, Func<DynamicVariant[], DynamicVariant> impl)
            {
                MinArgs = minArgs;
                MaxArgs = maxArgs;
                Impl = impl;
            }
        }

        static readonly Dictionary<string, List<FunctionDef>> Functions
            = new Dictionary<string, List<FunctionDef>>(StringComparer.Ordinal);

        static bool _initialized;
        static string _inspectorTooltipCache;

        static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            RegisterMathfLikeFunctions();

            // Easing: SinOut(t) = sin(t * PI/2), t: 0..1
            Register("SinOut", 1, 1, args =>
            {
                var t = Mathf.Clamp01(ExpressionHelper.AsNumber(args[0]));
                return DynamicVariant.FromFloat(Mathf.Sin(t * Mathf.PI * 0.5f));
            });
        }

        static void RegisterMathfLikeFunctions()
        {
            Register("Random", 0, 0, _ => DynamicVariant.FromFloat(UnityEngine.Random.value));
            Register("Random", 2, 2, args =>
            {
                var a = ExpressionHelper.AsNumber(args[0]);
                var b = ExpressionHelper.AsNumber(args[1]);
                return DynamicVariant.FromFloat(UnityEngine.Random.Range(a, b));
            });

            Register("Min", 1, 32, args =>
            {
                var min = ExpressionHelper.AsNumber(args[0]);
                for (int i = 1; i < args.Length; i++)
                    min = Mathf.Min(min, ExpressionHelper.AsNumber(args[i]));
                return DynamicVariant.FromFloat(min);
            });

            Register("Max", 1, 32, args =>
            {
                var max = ExpressionHelper.AsNumber(args[0]);
                for (int i = 1; i < args.Length; i++)
                    max = Mathf.Max(max, ExpressionHelper.AsNumber(args[i]));
                return DynamicVariant.FromFloat(max);
            });

            Register("Abs", 1, 1, args => DynamicVariant.FromFloat(Mathf.Abs(ExpressionHelper.AsNumber(args[0]))));
            Register("Sign", 1, 1, args => DynamicVariant.FromFloat(Mathf.Sign(ExpressionHelper.AsNumber(args[0]))));
            Register("Floor", 1, 1, args => DynamicVariant.FromFloat(Mathf.Floor(ExpressionHelper.AsNumber(args[0]))));
            Register("Ceil", 1, 1, args => DynamicVariant.FromFloat(Mathf.Ceil(ExpressionHelper.AsNumber(args[0]))));
            Register("Round", 1, 1, args => DynamicVariant.FromFloat(Mathf.Round(ExpressionHelper.AsNumber(args[0]))));
            Register("Round", 2, 2, args =>
            {
                var value = ExpressionHelper.AsNumber(args[0]);
                var digits = Mathf.Clamp(Mathf.RoundToInt(ExpressionHelper.AsNumber(args[1])), 0, 6);
                var factor = Mathf.Pow(10f, digits);
                return DynamicVariant.FromFloat(Mathf.Round(value * factor) / factor);
            });

            Register("TimeHMS", 1, 5, args =>
            {
                var totalSeconds = Mathf.Max(0f, ExpressionHelper.AsNumber(args[0]));
                var showHours = args.Length >= 2 ? ExpressionHelper.AsNumber(args[1]) != 0f : true;
                var showMinutes = args.Length >= 3 ? ExpressionHelper.AsNumber(args[2]) != 0f : true;
                var showSeconds = args.Length >= 4 ? ExpressionHelper.AsNumber(args[3]) != 0f : true;
                var padMinutesSeconds = args.Length >= 5 ? ExpressionHelper.AsNumber(args[4]) != 0f : false;

                var totalInt = Mathf.FloorToInt(totalSeconds);
                var hours = totalInt / 3600;
                var minutes = (totalInt % 3600) / 60;
                var seconds = totalInt % 60;

                var parts = new List<string>(3);

                if (showHours)
                    parts.Add($"{hours}h");

                if (showMinutes)
                {
                    var minText = (padMinutesSeconds && showHours)
                        ? minutes.ToString("00")
                        : minutes.ToString();
                    parts.Add($"{minText}m");
                }

                if (showSeconds)
                {
                    var secText = (padMinutesSeconds && showHours)
                        ? seconds.ToString("00")
                        : seconds.ToString();
                    parts.Add($"{secText}s");
                }

                if (parts.Count == 0)
                    return DynamicVariant.FromString("0s");

                return DynamicVariant.FromString(string.Join(" ", parts));
            });
            Register("RoundToInt", 1, 1, args => DynamicVariant.FromInt(Mathf.RoundToInt(ExpressionHelper.AsNumber(args[0]))));

            Register("Sin", 1, 1, args => DynamicVariant.FromFloat(Mathf.Sin(ExpressionHelper.AsNumber(args[0]))));
            Register("Cos", 1, 1, args => DynamicVariant.FromFloat(Mathf.Cos(ExpressionHelper.AsNumber(args[0]))));
            Register("Tan", 1, 1, args => DynamicVariant.FromFloat(Mathf.Tan(ExpressionHelper.AsNumber(args[0]))));
            Register("Asin", 1, 1, args => DynamicVariant.FromFloat(Mathf.Asin(ExpressionHelper.AsNumber(args[0]))));
            Register("Acos", 1, 1, args => DynamicVariant.FromFloat(Mathf.Acos(ExpressionHelper.AsNumber(args[0]))));
            Register("Atan", 1, 1, args => DynamicVariant.FromFloat(Mathf.Atan(ExpressionHelper.AsNumber(args[0]))));
            Register("Atan2", 2, 2, args => DynamicVariant.FromFloat(Mathf.Atan2(ExpressionHelper.AsNumber(args[0]), ExpressionHelper.AsNumber(args[1]))));
            Register("Deg2Rad", 1, 1, args => DynamicVariant.FromFloat(ExpressionHelper.AsNumber(args[0]) * Mathf.Deg2Rad));
            Register("Rad2Deg", 1, 1, args => DynamicVariant.FromFloat(ExpressionHelper.AsNumber(args[0]) * Mathf.Rad2Deg));

            Register("Pow", 2, 2, args => DynamicVariant.FromFloat(Mathf.Pow(ExpressionHelper.AsNumber(args[0]), ExpressionHelper.AsNumber(args[1]))));
            Register("Sqrt", 1, 1, args => DynamicVariant.FromFloat(Mathf.Sqrt(ExpressionHelper.AsNumber(args[0]))));
            Register("Exp", 1, 1, args => DynamicVariant.FromFloat(Mathf.Exp(ExpressionHelper.AsNumber(args[0]))));
            Register("Log", 1, 1, args => DynamicVariant.FromFloat(Mathf.Log(ExpressionHelper.AsNumber(args[0]))));
            Register("Log", 2, 2, args => DynamicVariant.FromFloat(Mathf.Log(ExpressionHelper.AsNumber(args[0]), ExpressionHelper.AsNumber(args[1]))));
            Register("Log10", 1, 1, args => DynamicVariant.FromFloat(Mathf.Log10(ExpressionHelper.AsNumber(args[0]))));

            Register("Clamp", 3, 3, args =>
            {
                var value = ExpressionHelper.AsNumber(args[0]);
                var min = ExpressionHelper.AsNumber(args[1]);
                var max = ExpressionHelper.AsNumber(args[2]);
                return DynamicVariant.FromFloat(Mathf.Clamp(value, min, max));
            });
            Register("Clamp01", 1, 1, args => DynamicVariant.FromFloat(Mathf.Clamp01(ExpressionHelper.AsNumber(args[0]))));

            Register("Lerp", 3, 3, args =>
            {
                var a = ExpressionHelper.AsNumber(args[0]);
                var b = ExpressionHelper.AsNumber(args[1]);
                var t = ExpressionHelper.AsNumber(args[2]);
                return DynamicVariant.FromFloat(Mathf.Lerp(a, b, t));
            });
            Register("LerpUnclamped", 3, 3, args =>
            {
                var a = ExpressionHelper.AsNumber(args[0]);
                var b = ExpressionHelper.AsNumber(args[1]);
                var t = ExpressionHelper.AsNumber(args[2]);
                return DynamicVariant.FromFloat(Mathf.LerpUnclamped(a, b, t));
            });
            Register("LerpAngle", 3, 3, args =>
            {
                var a = ExpressionHelper.AsNumber(args[0]);
                var b = ExpressionHelper.AsNumber(args[1]);
                var t = ExpressionHelper.AsNumber(args[2]);
                return DynamicVariant.FromFloat(Mathf.LerpAngle(a, b, t));
            });
            Register("InverseLerp", 3, 3, args =>
            {
                var a = ExpressionHelper.AsNumber(args[0]);
                var b = ExpressionHelper.AsNumber(args[1]);
                var value = ExpressionHelper.AsNumber(args[2]);
                return DynamicVariant.FromFloat(Mathf.InverseLerp(a, b, value));
            });

            Register("MoveTowards", 3, 3, args =>
            {
                var current = ExpressionHelper.AsNumber(args[0]);
                var target = ExpressionHelper.AsNumber(args[1]);
                var maxDelta = ExpressionHelper.AsNumber(args[2]);
                return DynamicVariant.FromFloat(Mathf.MoveTowards(current, target, maxDelta));
            });
            Register("MoveTowardsAngle", 3, 3, args =>
            {
                var current = ExpressionHelper.AsNumber(args[0]);
                var target = ExpressionHelper.AsNumber(args[1]);
                var maxDelta = ExpressionHelper.AsNumber(args[2]);
                return DynamicVariant.FromFloat(Mathf.MoveTowardsAngle(current, target, maxDelta));
            });

            Register("SmoothStep", 3, 3, args =>
            {
                var from = ExpressionHelper.AsNumber(args[0]);
                var to = ExpressionHelper.AsNumber(args[1]);
                var t = ExpressionHelper.AsNumber(args[2]);
                return DynamicVariant.FromFloat(Mathf.SmoothStep(from, to, t));
            });
            Register("Gamma", 3, 3, args =>
            {
                var value = ExpressionHelper.AsNumber(args[0]);
                var absmax = ExpressionHelper.AsNumber(args[1]);
                var gamma = ExpressionHelper.AsNumber(args[2]);
                return DynamicVariant.FromFloat(Mathf.Gamma(value, absmax, gamma));
            });

            Register("Repeat", 2, 2, args =>
            {
                var t = ExpressionHelper.AsNumber(args[0]);
                var length = ExpressionHelper.AsNumber(args[1]);
                return DynamicVariant.FromFloat(Mathf.Repeat(t, length));
            });
            Register("PingPong", 2, 2, args =>
            {
                var t = ExpressionHelper.AsNumber(args[0]);
                var length = ExpressionHelper.AsNumber(args[1]);
                return DynamicVariant.FromFloat(Mathf.PingPong(t, length));
            });
            Register("DeltaAngle", 2, 2, args =>
            {
                var current = ExpressionHelper.AsNumber(args[0]);
                var target = ExpressionHelper.AsNumber(args[1]);
                return DynamicVariant.FromFloat(Mathf.DeltaAngle(current, target));
            });

            Register("PerlinNoise", 2, 2, args =>
            {
                var x = ExpressionHelper.AsNumber(args[0]);
                var y = ExpressionHelper.AsNumber(args[1]);
                return DynamicVariant.FromFloat(Mathf.PerlinNoise(x, y));
            });
            Register("Approximately", 2, 2, args =>
            {
                var a = ExpressionHelper.AsNumber(args[0]);
                var b = ExpressionHelper.AsNumber(args[1]);
                return DynamicVariant.FromBool(Mathf.Approximately(a, b));
            });

            Register("Vec2", 2, 2, args =>
            {
                var x = ExpressionHelper.AsNumber(args[0]);
                var y = ExpressionHelper.AsNumber(args[1]);
                return DynamicVariant.FromVector2(new Vector2(x, y));
            });

            Register("Dot", 2, 2, args =>
            {
                if (!ExpressionHelper.TryAsVector2(args[0], out var a) || !ExpressionHelper.TryAsVector2(args[1], out var b))
                    return DynamicVariant.FromFloat(0f);

                return DynamicVariant.FromFloat(Vector2.Dot(a, b));
            });

            Register("Cross", 2, 2, args =>
            {
                if (!ExpressionHelper.TryAsVector2(args[0], out var a) || !ExpressionHelper.TryAsVector2(args[1], out var b))
                    return DynamicVariant.FromFloat(0f);

                return DynamicVariant.FromFloat(a.x * b.y - a.y * b.x);
            });

            Register("Magnitude2", 1, 1, args =>
            {
                if (!ExpressionHelper.TryAsVector2(args[0], out var value))
                    return DynamicVariant.FromFloat(0f);

                return DynamicVariant.FromFloat(value.magnitude);
            });

            Register("SqrMagnitude2", 1, 1, args =>
            {
                if (!ExpressionHelper.TryAsVector2(args[0], out var value))
                    return DynamicVariant.FromFloat(0f);

                return DynamicVariant.FromFloat(value.sqrMagnitude);
            });

            Register("Normalize2", 1, 1, args =>
            {
                if (!ExpressionHelper.TryAsVector2(args[0], out var value))
                    return DynamicVariant.FromVector2(Vector2.zero);

                return DynamicVariant.FromVector2(value.sqrMagnitude <= 0.000001f ? Vector2.zero : value.normalized);
            });

            Register("Perp", 1, 1, args =>
            {
                if (!ExpressionHelper.TryAsVector2(args[0], out var value))
                    return DynamicVariant.FromVector2(Vector2.zero);

                return DynamicVariant.FromVector2(new Vector2(-value.y, value.x));
            });

            Register("Project2", 2, 2, args =>
            {
                if (!ExpressionHelper.TryAsVector2(args[0], out var vector) || !ExpressionHelper.TryAsVector2(args[1], out var onto))
                    return DynamicVariant.FromVector2(Vector2.zero);

                var denom = onto.sqrMagnitude;
                if (denom <= 0.000001f)
                    return DynamicVariant.FromVector2(Vector2.zero);

                var scalar = Vector2.Dot(vector, onto) / denom;
                return DynamicVariant.FromVector2(onto * scalar);
            });

            Register("Reflect2", 2, 2, args =>
            {
                if (!ExpressionHelper.TryAsVector2(args[0], out var direction) || !ExpressionHelper.TryAsVector2(args[1], out var normal))
                    return DynamicVariant.FromVector2(Vector2.zero);

                return DynamicVariant.FromVector2(Vector2.Reflect(direction, normal));
            });

            Register("ClampMagnitude2", 2, 2, args =>
            {
                if (!ExpressionHelper.TryAsVector2(args[0], out var value))
                    return DynamicVariant.FromVector2(Vector2.zero);

                var maxLength = Mathf.Max(0f, ExpressionHelper.AsNumber(args[1]));
                return DynamicVariant.FromVector2(Vector2.ClampMagnitude(value, maxLength));
            });

            Register("Lerp2", 3, 3, args =>
            {
                if (!ExpressionHelper.TryAsVector2(args[0], out var a) || !ExpressionHelper.TryAsVector2(args[1], out var b))
                    return DynamicVariant.FromVector2(Vector2.zero);

                var t = ExpressionHelper.AsNumber(args[2]);
                return DynamicVariant.FromVector2(Vector2.Lerp(a, b, t));
            });

            Register("MoveTowards2", 3, 3, args =>
            {
                if (!ExpressionHelper.TryAsVector2(args[0], out var current) || !ExpressionHelper.TryAsVector2(args[1], out var target))
                    return DynamicVariant.FromVector2(Vector2.zero);

                var maxDelta = Mathf.Max(0f, ExpressionHelper.AsNumber(args[2]));
                return DynamicVariant.FromVector2(Vector2.MoveTowards(current, target, maxDelta));
            });

            Register("Angle2", 2, 2, args =>
            {
                if (!ExpressionHelper.TryAsVector2(args[0], out var from) || !ExpressionHelper.TryAsVector2(args[1], out var to))
                    return DynamicVariant.FromFloat(0f);

                return DynamicVariant.FromFloat(Vector2.Angle(from, to));
            });

            Register("SignedAngle2", 2, 2, args =>
            {
                if (!ExpressionHelper.TryAsVector2(args[0], out var from) || !ExpressionHelper.TryAsVector2(args[1], out var to))
                    return DynamicVariant.FromFloat(0f);

                return DynamicVariant.FromFloat(Vector2.SignedAngle(from, to));
            });
        }

        public static void Register(string name, int minArgs, int maxArgs, Func<DynamicVariant[], DynamicVariant> impl)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Function name is null/empty", nameof(name));
            if (impl == null) throw new ArgumentNullException(nameof(impl));
            if (minArgs < 0 || maxArgs < minArgs) throw new ArgumentOutOfRangeException(nameof(minArgs));

            if (!Functions.TryGetValue(name, out var defs) || defs == null)
            {
                defs = new List<FunctionDef>(2);
                Functions[name] = defs;
            }

            for (int i = defs.Count - 1; i >= 0; i--)
            {
                var existing = defs[i];
                if (existing.MinArgs == minArgs && existing.MaxArgs == maxArgs)
                    defs.RemoveAt(i);
            }

            defs.Add(new FunctionDef(minArgs, maxArgs, impl));
            _inspectorTooltipCache = null;
        }

        public static DynamicVariant Invoke(string name, DynamicVariant[] args)
        {
            EnsureInitialized();

            if (!Functions.TryGetValue(name, out var defs) || defs == null || defs.Count == 0)
                throw new InvalidOperationException($"Unknown function '{name}'");

            var count = args?.Length ?? 0;
            for (int i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                if (count < def.MinArgs || count > def.MaxArgs)
                    continue;

                return def.Impl(args ?? Array.Empty<DynamicVariant>());
            }

            var expected = BuildExpectedArgRange(defs);
            throw new InvalidOperationException($"Function '{name}' expects {expected} args, got {count}");

        }

        public static string GetInspectorFunctionTooltip()
        {
            EnsureInitialized();
            if (!string.IsNullOrEmpty(_inspectorTooltipCache))
                return _inspectorTooltipCache;

            var entries = new List<string>(Functions.Count);
            foreach (var pair in Functions)
            {
                var ranges = BuildExpectedArgRange(pair.Value);
                entries.Add($"{pair.Key}({ranges})");
            }
            entries.Sort(StringComparer.Ordinal);

            _inspectorTooltipCache =
                "Functions: " + string.Join(", ", entries) +
                "\nExample: Clamp(Sin(t) * 10, -3, 3)";

            return _inspectorTooltipCache;
        }

        static string BuildExpectedArgRange(List<FunctionDef> defs)
        {
            if (defs == null || defs.Count == 0)
                return "unknown";

            if (defs.Count == 1)
            {
                var d = defs[0];
                return d.MinArgs == d.MaxArgs ? d.MinArgs.ToString() : $"{d.MinArgs}..{d.MaxArgs}";
            }

            var ranges = new string[defs.Count];
            for (int i = 0; i < defs.Count; i++)
            {
                var d = defs[i];
                ranges[i] = d.MinArgs == d.MaxArgs ? d.MinArgs.ToString() : $"{d.MinArgs}..{d.MaxArgs}";
            }
            return string.Join(" or ", ranges);
        }
    }
}
