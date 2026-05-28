#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace TinnosukeGameLib.Tests.Editor
{
    public sealed class ForbiddenPatternRule
    {
        public ForbiddenPatternRule(string ruleId, string description, string token, Regex matcher, Func<string, int, string, string, bool>? allowMatch = null)
        {
            RuleId = ruleId ?? throw new ArgumentNullException(nameof(ruleId));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Token = token ?? throw new ArgumentNullException(nameof(token));
            Matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
            AllowMatch = allowMatch;
        }

        public string RuleId { get; }

        public string Description { get; }

        public string Token { get; }

        public Regex Matcher { get; }

        public Func<string, int, string, string, bool>? AllowMatch { get; }
    }

    internal readonly struct ForbiddenPatternViolation
    {
        public ForbiddenPatternViolation(string ruleId, string token, string filePath, int lineNumber, string lineText)
        {
            RuleId = ruleId;
            Token = token;
            FilePath = filePath;
            LineNumber = lineNumber;
            LineText = lineText;
        }

        public string RuleId { get; }

        public string Token { get; }

        public string FilePath { get; }

        public int LineNumber { get; }

        public string LineText { get; }
    }

    internal readonly struct DebugImportContext
    {
        public DebugImportContext(bool hasUsingStaticUnityDebug, string[] debugAliases)
        {
            HasUsingStaticUnityDebug = hasUsingStaticUnityDebug;
            DebugAliases = debugAliases ?? throw new ArgumentNullException(nameof(debugAliases));
        }

        public bool HasUsingStaticUnityDebug { get; }

        public string[] DebugAliases { get; }
    }

    internal static class KernelForbiddenPatternScanner
    {
        static readonly StringComparison PathComparison = StringComparison.OrdinalIgnoreCase;
        static readonly Regex UsingStaticUnityDebugRegex = new Regex(@"^\s*using\s+static\s+(?:global::)?UnityEngine\s*\.\s*Debug\s*;", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline);
        static readonly Regex UsingAliasUnityDebugRegex = new Regex(@"^\s*using\s+([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?:global::)?UnityEngine\s*\.\s*Debug\s*;", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline);

        public static string ProjectRootPath
        {
            get
            {
                DirectoryInfo? assetsDirectory = Directory.GetParent(Application.dataPath);
                if (assetsDirectory == null)
                    throw new InvalidOperationException("Unable to resolve the project root from Application.dataPath.");

                return assetsDirectory.FullName;
            }
        }

        public static string KernelRootPath => Path.Combine(ProjectRootPath, "Assets", "GameLib", "Script", "Kernel");

        public static IReadOnlyList<string> DefaultTargetRuntimeRoots => new[] { KernelRootPath };

        public static string ApprovedUnityLogSinkPath => Path.Combine(ProjectRootPath, "Assets", "GameLib", "Script", "Kernel", "Diagnostics", "Unity", "UnityLogDiagnosticSink.cs");

        public static ForbiddenPatternRule[] CreateDefaultRules()
        {
            ForbiddenPatternRule[] debugRules = CreateDebugRules();
            ForbiddenPatternRule[] forbiddenApiRules = CreateForbiddenApiRules();
            ForbiddenPatternRule[] allRules = new ForbiddenPatternRule[debugRules.Length + forbiddenApiRules.Length];
            Array.Copy(debugRules, 0, allRules, 0, debugRules.Length);
            Array.Copy(forbiddenApiRules, 0, allRules, debugRules.Length, forbiddenApiRules.Length);
            return allRules;
        }

        public static ForbiddenPatternRule[] CreateDebugRules()
        {
            return new[]
            {
                new ForbiddenPatternRule(
                    "STATIC_RULE_DEBUG_LOG_OUTSIDE_SINK",
                    "Debug.Log must not be used outside the approved Unity diagnostic sink.",
                    "Debug.Log",
                    new Regex(@"\bDebug\s*\.\s*Log\s*\(", RegexOptions.Compiled | RegexOptions.CultureInvariant),
                    IsAllowedUnityDiagnosticSinkMatch),
                new ForbiddenPatternRule(
                    "STATIC_RULE_DEBUG_LOG_ERROR_OUTSIDE_SINK",
                    "Debug.LogError must not be used outside the approved Unity diagnostic sink.",
                    "Debug.LogError",
                    new Regex(@"\bDebug\s*\.\s*LogError\s*\(", RegexOptions.Compiled | RegexOptions.CultureInvariant),
                    IsAllowedUnityDiagnosticSinkMatch),
                new ForbiddenPatternRule(
                    "STATIC_RULE_DEBUG_LOG_WARNING_OUTSIDE_SINK",
                    "Debug.LogWarning must not be used outside the approved Unity diagnostic sink.",
                    "Debug.LogWarning",
                    new Regex(@"\bDebug\s*\.\s*LogWarning\s*\(", RegexOptions.Compiled | RegexOptions.CultureInvariant),
                    IsAllowedUnityDiagnosticSinkMatch),
                new ForbiddenPatternRule(
                    "STATIC_RULE_DEBUG_LOG_EXCEPTION_OUTSIDE_SINK",
                    "Debug.LogException must not be used outside the approved Unity diagnostic sink.",
                    "Debug.LogException",
                    new Regex(@"\bDebug\s*\.\s*LogException\s*\(", RegexOptions.Compiled | RegexOptions.CultureInvariant),
                    IsAllowedUnityDiagnosticSinkMatch),
            };
        }

        public static ForbiddenPatternRule[] CreateForbiddenApiRules()
        {
            return new[]
            {
                new ForbiddenPatternRule(
                    "STATIC_RULE_RESOURCES_LOAD_IN_KERNEL_RUNTIME",
                    "Resources.Load must not be used in Kernel code paths.",
                    "Resources.Load",
                    new Regex(@"\bResources\s*\.\s*Load(?:Async)?\s*(?:<[^>]+>)?\s*\(", RegexOptions.Compiled | RegexOptions.CultureInvariant)),
                new ForbiddenPatternRule(
                    "STATIC_RULE_FIND_OBJECTS_BY_TYPE_IN_KERNEL_RUNTIME",
                    "FindObjectsByType must not be used in Kernel code paths.",
                    "FindObjectsByType",
                    new Regex(@"\b(?:[A-Za-z_][A-Za-z0-9_]*\s*\.\s*)*FindObjectsByType(?:<[^>]+>)?\s*\(", RegexOptions.Compiled | RegexOptions.CultureInvariant)),
                new ForbiddenPatternRule(
                    "STATIC_RULE_GET_COMPONENTS_IN_CHILDREN_IN_KERNEL_RUNTIME",
                    "GetComponentsInChildren must not be used in Kernel code paths.",
                    "GetComponentsInChildren",
                    new Regex(@"\bGetComponentsInChildren(?:<[^>]+>)?\s*\(", RegexOptions.Compiled | RegexOptions.CultureInvariant)),
                new ForbiddenPatternRule(
                    "STATIC_RULE_TRANSFORM_PARENT_SCOPE_INFERENCE_IN_KERNEL_RUNTIME",
                    "Transform.parent scope inference must not be used in Kernel code paths.",
                    "Transform.parent",
                    new Regex(@"\b(?:[A-Za-z_][A-Za-z0-9_]*\s*\.\s*)+parent\b", RegexOptions.Compiled | RegexOptions.CultureInvariant)),
            };
        }

        public static ForbiddenPatternRule[] CreateM12_1Rules()
        {
            ForbiddenPatternRule[] legacyAuthorityRules = CreateM12_1LegacyAuthorityRules();
            ForbiddenPatternRule[] forbiddenApiRules = CreateForbiddenApiRules();
            ForbiddenPatternRule[] allRules = new ForbiddenPatternRule[legacyAuthorityRules.Length + forbiddenApiRules.Length];
            Array.Copy(legacyAuthorityRules, 0, allRules, 0, legacyAuthorityRules.Length);
            Array.Copy(forbiddenApiRules, 0, allRules, legacyAuthorityRules.Length, forbiddenApiRules.Length);
            return allRules;
        }

        public static ForbiddenPatternRule[] CreateM12_1LegacyAuthorityRules()
        {
            return new[]
            {
                new ForbiddenPatternRule(
                    "M12_1_RULE_RUNTIME_TRYRESOLVE_USAGE",
                    "TryResolve must not participate in M12.1 runtime authority.",
                    "TryResolve",
                    new Regex(@"\b[A-Za-z_][A-Za-z0-9_]*\s*\.\s*TryResolve(?:<[^>]+>)?\s*\(", RegexOptions.Compiled | RegexOptions.CultureInvariant)),
                new ForbiddenPatternRule(
                    "M12_1_RULE_FEATURE_INSTALLER_USAGE",
                    "IFeatureInstaller must not participate in M12.1 runtime authority.",
                    "IFeatureInstaller",
                    new Regex(@"\bIFeatureInstaller\b", RegexOptions.Compiled | RegexOptions.CultureInvariant)),
                new ForbiddenPatternRule(
                    "M12_1_RULE_INSTALL_FEATURE_USAGE",
                    "InstallFeature must not participate in M12.1 runtime authority.",
                    "InstallFeature",
                    new Regex(@"\bInstallFeature\s*\(", RegexOptions.Compiled | RegexOptions.CultureInvariant)),
                new ForbiddenPatternRule(
                    "M12_1_RULE_PARENT_WALK_USAGE",
                    "Parent-based ownership repair must not participate in M12.1 runtime authority.",
                    "Parent",
                    new Regex(@"\b[A-Za-z_][A-Za-z0-9_]*\s*\.\s*Parent\b", RegexOptions.Compiled | RegexOptions.CultureInvariant)),
                new ForbiddenPatternRule(
                    "M12_1_RULE_SCOPE_LOOKUP_HELPER_USAGE",
                    "TryGetNearestScopeNode must not participate in M12.1 runtime authority.",
                    "TryGetNearestScopeNode",
                    new Regex(@"\bTryGetNearestScopeNode\s*\(", RegexOptions.Compiled | RegexOptions.CultureInvariant)),
            };
        }

        public static ForbiddenPatternViolation[] ScanKernelSources(ForbiddenPatternRule rule)
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));

            return ScanTargetRuntimeRoots(DefaultTargetRuntimeRoots, rule);
        }

        public static ForbiddenPatternViolation[] ScanTargetRuntimeRoots(IEnumerable<string> rootPaths, ForbiddenPatternRule rule)
        {
            return ScanTargetRoots(rootPaths, rule, includeEditorFiles: false, includeTestFiles: false);
        }

        public static ForbiddenPatternViolation[] ScanTargetRoots(IEnumerable<string> rootPaths, ForbiddenPatternRule rule, bool includeEditorFiles, bool includeTestFiles)
        {
            if (rootPaths == null)
                throw new ArgumentNullException(nameof(rootPaths));
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));

            List<string> filePaths = new List<string>();
            foreach (string rootPath in rootPaths)
            {
                if (string.IsNullOrWhiteSpace(rootPath))
                    throw new ArgumentException("Target runtime roots must not be blank.", nameof(rootPaths));

                if (!Directory.Exists(rootPath))
                    continue;

                filePaths.AddRange(Directory.GetFiles(rootPath, "*.cs", SearchOption.AllDirectories));
            }

            return ScanFiles(filePaths, rule, rootPaths, includeEditorFiles, includeTestFiles);
        }

        public static ForbiddenPatternViolation[] ScanFiles(IEnumerable<string> filePaths, ForbiddenPatternRule rule)
        {
            return ScanFiles(filePaths, rule, DefaultTargetRuntimeRoots, includeEditorFiles: false, includeTestFiles: false);
        }

        public static ForbiddenPatternViolation[] ScanFiles(IEnumerable<string> filePaths, ForbiddenPatternRule rule, IEnumerable<string> allowedRoots)
        {
            return ScanFiles(filePaths, rule, allowedRoots, includeEditorFiles: false, includeTestFiles: false);
        }

        public static ForbiddenPatternViolation[] ScanFiles(IEnumerable<string> filePaths, ForbiddenPatternRule rule, IEnumerable<string> allowedRoots, bool includeEditorFiles, bool includeTestFiles)
        {
            if (filePaths == null)
                throw new ArgumentNullException(nameof(filePaths));
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));
            if (allowedRoots == null)
                throw new ArgumentNullException(nameof(allowedRoots));

            List<ForbiddenPatternViolation> violations = new List<ForbiddenPatternViolation>();
            foreach (string filePath in filePaths)
            {
                if (!ShouldScanFile(filePath, allowedRoots, includeEditorFiles, includeTestFiles))
                    continue;

                violations.AddRange(ScanFile(filePath, rule));
            }

            return violations.ToArray();
        }

        public static ForbiddenPatternViolation[] ScanText(string virtualPath, string sourceText, ForbiddenPatternRule rule)
        {
            if (virtualPath == null)
                throw new ArgumentNullException(nameof(virtualPath));
            if (sourceText == null)
                throw new ArgumentNullException(nameof(sourceText));
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));

            List<ForbiddenPatternViolation> violations = new List<ForbiddenPatternViolation>();
            DebugImportContext debugImportContext = ParseDebugImportContext(sourceText);
            Regex effectiveMatcher = CreateEffectiveMatcher(rule, debugImportContext);
            bool inBlockComment = false;
            bool inVerbatimString = false;
            int rawStringDelimiterLength = 0;
            using (StringReader reader = new StringReader(sourceText))
            {
                string? line;
                int lineNumber = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    string normalizedLine = StripStringsAndComments(line, ref inBlockComment, ref inVerbatimString, ref rawStringDelimiterLength);
                    MatchCollection matches = effectiveMatcher.Matches(normalizedLine);
                    for (int i = 0; i < matches.Count; i++)
                    {
                        if (rule.AllowMatch != null && rule.AllowMatch(virtualPath, lineNumber, line, matches[i].Value))
                            continue;

                        violations.Add(new ForbiddenPatternViolation(rule.RuleId, rule.Token, virtualPath, lineNumber, line.Trim()));
                    }
                }
            }

            return violations.ToArray();
        }

        public static string FormatViolations(ForbiddenPatternRule rule, IReadOnlyList<ForbiddenPatternViolation> violations)
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));
            if (violations == null)
                throw new ArgumentNullException(nameof(violations));

            StringBuilder builder = new StringBuilder(512);
            builder.Append(rule.RuleId)
                .Append(": ")
                .Append(rule.Description)
                .AppendLine();

            for (int i = 0; i < violations.Count; i++)
            {
                ForbiddenPatternViolation violation = violations[i];
                builder.Append("- ")
                    .Append(violation.FilePath)
                    .Append(':')
                    .Append(violation.LineNumber)
                    .Append(" => ")
                    .Append(violation.LineText)
                    .AppendLine();
            }

            return builder.ToString();
        }

        static IEnumerable<ForbiddenPatternViolation> ScanFile(string filePath, ForbiddenPatternRule rule)
        {
            string fullPath = Path.GetFullPath(filePath);
            string sourceText = File.ReadAllText(fullPath);
            string relativePath = GetProjectRelativePath(fullPath);
            return ScanText(relativePath, sourceText, rule);
        }

        static bool ShouldScanFile(string filePath, IEnumerable<string> allowedRoots, bool includeEditorFiles, bool includeTestFiles)
        {
            string normalizedPath = Path.GetFullPath(filePath).Replace('\\', '/');
            if (!normalizedPath.EndsWith(".cs", PathComparison))
                return false;
            if (!includeEditorFiles && normalizedPath.Contains("/Editor/", PathComparison))
                return false;
            if (!includeTestFiles && normalizedPath.Contains("/Tests/", PathComparison))
                return false;

            foreach (string allowedRoot in allowedRoots)
            {
                string normalizedRoot = NormalizePath(Path.GetFullPath(allowedRoot)).TrimEnd('/');
                if (normalizedPath.Equals(normalizedRoot, PathComparison) || normalizedPath.StartsWith(normalizedRoot + "/", PathComparison))
                    return true;
            }

            return false;
        }

        static bool IsAllowedUnityDiagnosticSinkMatch(string filePath, int lineNumber, string lineText, string matchedToken)
        {
            _ = lineNumber;
            string normalizedPath = NormalizePath(filePath);
            if (!string.Equals(normalizedPath, NormalizePath(GetProjectRelativePath(ApprovedUnityLogSinkPath)), PathComparison)
                && !string.Equals(normalizedPath, NormalizePath(ApprovedUnityLogSinkPath), PathComparison))
            {
                return false;
            }

            string compactLine = Regex.Replace(lineText, @"\s+", string.Empty, RegexOptions.CultureInvariant);
            string compactToken = Regex.Replace(matchedToken, @"\s+", string.Empty, RegexOptions.CultureInvariant);
            string? debugMethodName = TryGetDebugMethodName(compactToken);
            if (debugMethodName == null)
                return false;

            string expectedArgumentName = string.Equals(debugMethodName, "LogException", PathComparison)
                ? "exception"
                : "message";

            if (compactToken.Contains(".", PathComparison))
            {
                string qualifiedPattern = string.Concat(@"^(?:(?:global::)?UnityEngine\.Debug|[A-Za-z_][A-Za-z0-9_]*)\.", debugMethodName, "\\(", expectedArgumentName, @"\);$");
                return Regex.IsMatch(compactLine, qualifiedPattern, RegexOptions.CultureInvariant);
            }

            return string.Equals(compactLine, string.Concat(debugMethodName, "(", expectedArgumentName, ");"), PathComparison);
        }

        static DebugImportContext ParseDebugImportContext(string sourceText)
        {
            bool hasUsingStaticUnityDebug = UsingStaticUnityDebugRegex.IsMatch(sourceText);
            MatchCollection aliasMatches = UsingAliasUnityDebugRegex.Matches(sourceText);
            if (aliasMatches.Count == 0)
                return new DebugImportContext(hasUsingStaticUnityDebug, Array.Empty<string>());

            string[] debugAliases = new string[aliasMatches.Count];
            for (int i = 0; i < aliasMatches.Count; i++)
                debugAliases[i] = aliasMatches[i].Groups[1].Value;

            return new DebugImportContext(hasUsingStaticUnityDebug, debugAliases);
        }

        static Regex CreateEffectiveMatcher(ForbiddenPatternRule rule, DebugImportContext debugImportContext)
        {
            string? debugMethodName = TryGetDebugMethodName(rule.Token);
            if (debugMethodName == null)
                return rule.Matcher;

            List<string> patterns = new List<string>(4)
            {
                string.Concat(@"(?<!\.)\bDebug\s*\.\s*", Regex.Escape(debugMethodName), @"\s*\("),
                string.Concat(@"\b(?:global::)?UnityEngine\s*\.\s*Debug\s*\.\s*", Regex.Escape(debugMethodName), @"\s*\("),
            };

            if (debugImportContext.DebugAliases.Length > 0)
            {
                StringBuilder aliasPatternBuilder = new StringBuilder(64);
                aliasPatternBuilder.Append(@"(?<!\.)\b(?:");
                for (int i = 0; i < debugImportContext.DebugAliases.Length; i++)
                {
                    if (i > 0)
                        aliasPatternBuilder.Append('|');

                    aliasPatternBuilder.Append(Regex.Escape(debugImportContext.DebugAliases[i]));
                }

                aliasPatternBuilder.Append(@")\s*\.\s*")
                    .Append(Regex.Escape(debugMethodName))
                    .Append(@"\s*\(");
                patterns.Add(aliasPatternBuilder.ToString());
            }

            if (debugImportContext.HasUsingStaticUnityDebug)
                patterns.Add(string.Concat(@"(?<!\.)\b", Regex.Escape(debugMethodName), @"\s*\("));

            return new Regex(string.Join("|", patterns), RegexOptions.Compiled | RegexOptions.CultureInvariant);
        }

        static string? TryGetDebugMethodName(string token)
        {
            if (token == null)
                return null;

            token = token.Trim();
            if (token.EndsWith("(", PathComparison))
                token = token.Substring(0, token.Length - 1);

            if (token.EndsWith("Debug.Log", PathComparison) || token.EndsWith("Log", PathComparison))
                return "Log";
            if (token.EndsWith("Debug.LogWarning", PathComparison) || token.EndsWith("LogWarning", PathComparison))
                return "LogWarning";
            if (token.EndsWith("Debug.LogError", PathComparison) || token.EndsWith("LogError", PathComparison))
                return "LogError";
            if (token.EndsWith("Debug.LogException", PathComparison) || token.EndsWith("LogException", PathComparison))
                return "LogException";

            return null;
        }

        static string GetProjectRelativePath(string fullPath)
        {
            string relativePath = Path.GetRelativePath(ProjectRootPath, fullPath);
            return relativePath.Replace('\\', '/');
        }

        static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }

        static string StripStringsAndComments(string line, ref bool inBlockComment, ref bool inVerbatimString, ref int rawStringDelimiterLength)
        {
            StringBuilder builder = new StringBuilder(line.Length);
            bool inString = false;
            bool inChar = false;
            bool escape = false;

            for (int i = 0; i < line.Length; i++)
            {
                char current = line[i];
                char next = i + 1 < line.Length ? line[i + 1] : '\0';

                if (rawStringDelimiterLength > 0)
                {
                    int closingQuoteCount = CountRepeatedCharacters(line, i, '"');
                    if (closingQuoteCount >= rawStringDelimiterLength)
                    {
                        for (int j = 0; j < rawStringDelimiterLength; j++)
                            builder.Append(' ');

                        i += rawStringDelimiterLength - 1;
                        rawStringDelimiterLength = 0;
                    }
                    else
                    {
                        builder.Append(' ');
                    }

                    continue;
                }

                if (inVerbatimString)
                {
                    if (current == '"')
                    {
                        if (next == '"')
                        {
                            builder.Append(' ');
                            builder.Append(' ');
                            i++;
                            continue;
                        }

                        inVerbatimString = false;
                    }

                    builder.Append(' ');
                    continue;
                }

                if (inBlockComment)
                {
                    if (current == '*' && next == '/')
                    {
                        inBlockComment = false;
                        i++;
                    }

                    continue;
                }

                if (!inString && !inChar && current == '/' && next == '*')
                {
                    inBlockComment = true;
                    i++;
                    continue;
                }

                if (!inString && !inChar && current == '/' && next == '/')
                    break;

                if (!inString && !inChar)
                {
                    int rawStringQuoteCount = CountRepeatedCharacters(line, i, '"');
                    if (rawStringQuoteCount >= 3)
                    {
                        rawStringDelimiterLength = rawStringQuoteCount;
                        for (int j = 0; j < rawStringQuoteCount; j++)
                            builder.Append(' ');

                        i += rawStringQuoteCount - 1;
                        continue;
                    }
                }

                if (inString)
                {
                    if (!escape && current == '"')
                        inString = false;

                    escape = !escape && current == '\\';
                    builder.Append(' ');
                    continue;
                }

                if (inChar)
                {
                    if (!escape && current == '\'')
                        inChar = false;

                    escape = !escape && current == '\\';
                    builder.Append(' ');
                    continue;
                }

                if (!inChar && current == '@' && next == '"')
                {
                    inVerbatimString = true;
                    builder.Append(' ');
                    builder.Append(' ');
                    i++;
                    continue;
                }

                if (!inChar && current == '$' && next == '"')
                {
                    inString = true;
                    escape = false;
                    builder.Append(' ');
                    builder.Append(' ');
                    i++;
                    continue;
                }

                if (!inChar && current == '$' && next == '@' && i + 2 < line.Length && line[i + 2] == '"')
                {
                    inVerbatimString = true;
                    builder.Append(' ');
                    builder.Append(' ');
                    builder.Append(' ');
                    i += 2;
                    continue;
                }

                if (!inChar && current == '@' && next == '$' && i + 2 < line.Length && line[i + 2] == '"')
                {
                    inVerbatimString = true;
                    builder.Append(' ');
                    builder.Append(' ');
                    builder.Append(' ');
                    i += 2;
                    continue;
                }

                if (current == '"')
                {
                    inString = true;
                    escape = false;
                    builder.Append(' ');
                    continue;
                }

                if (current == '\'')
                {
                    inChar = true;
                    escape = false;
                    builder.Append(' ');
                    continue;
                }

                builder.Append(current);
            }

            return builder.ToString();
        }

        static int CountRepeatedCharacters(string line, int startIndex, char value)
        {
            int count = 0;
            for (int i = startIndex; i < line.Length; i++)
            {
                if (line[i] != value)
                    break;

                count++;
            }

            return count;
        }
    }
}
