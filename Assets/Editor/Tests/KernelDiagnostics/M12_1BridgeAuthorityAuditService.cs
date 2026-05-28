#nullable enable

using System;
using System.Collections.Generic;
using System.IO;

namespace TinnosukeGameLib.Tests.Editor
{
    public sealed class M12_1BridgeAuthorityAuditFinding
    {
        public M12_1BridgeAuthorityAuditFinding(string ruleId, string token, string filePath, int lineNumber, string lineText, bool isEditorSurface)
        {
            RuleId = ruleId ?? throw new ArgumentNullException(nameof(ruleId));
            Token = token ?? throw new ArgumentNullException(nameof(token));
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            LineNumber = lineNumber;
            LineText = lineText ?? throw new ArgumentNullException(nameof(lineText));
            IsEditorSurface = isEditorSurface;
        }

        public string RuleId { get; }

        public string Token { get; }

        public string FilePath { get; }

        public int LineNumber { get; }

        public string LineText { get; }

        public bool IsEditorSurface { get; }
    }

    public sealed class M12_1BridgeAuthorityAuditReport
    {
        readonly string[] scannedRoots;
        readonly M12_1BridgeAuthorityAuditFinding[] findings;

        public M12_1BridgeAuthorityAuditReport(IReadOnlyList<string> scannedRoots, IReadOnlyList<M12_1BridgeAuthorityAuditFinding> findings)
        {
            this.scannedRoots = CloneRoots(scannedRoots);
            this.findings = CloneFindings(findings);
            Array.Sort(this.findings, CompareFindings);
            RuntimeFindingCount = CountFindings(isEditorSurface: false);
            EditorFindingCount = CountFindings(isEditorSurface: true);
        }

        public IReadOnlyList<string> ScannedRoots => scannedRoots;

        public IReadOnlyList<M12_1BridgeAuthorityAuditFinding> Findings => findings;

        public int FindingCount => findings.Length;

        public int RuntimeFindingCount { get; }

        public int EditorFindingCount { get; }

        public bool HasFindings => findings.Length > 0;

        public bool HasRuntimeFindings => RuntimeFindingCount > 0;

        public bool HasEditorFindings => EditorFindingCount > 0;

        static string[] CloneRoots(IReadOnlyList<string> roots)
        {
            if (roots == null)
                throw new ArgumentNullException(nameof(roots));

            if (roots.Count == 0)
                return Array.Empty<string>();

            string[] snapshot = new string[roots.Count];
            for (int index = 0; index < roots.Count; index++)
            {
                string? root = roots[index];
                if (string.IsNullOrWhiteSpace(root))
                    throw new ArgumentException("Scanned roots must not contain blank values.", nameof(roots));

                snapshot[index] = root.Trim();
            }

            return snapshot;
        }

        static M12_1BridgeAuthorityAuditFinding[] CloneFindings(IReadOnlyList<M12_1BridgeAuthorityAuditFinding> findings)
        {
            if (findings == null)
                throw new ArgumentNullException(nameof(findings));

            if (findings.Count == 0)
                return Array.Empty<M12_1BridgeAuthorityAuditFinding>();

            M12_1BridgeAuthorityAuditFinding[] snapshot = new M12_1BridgeAuthorityAuditFinding[findings.Count];
            for (int index = 0; index < findings.Count; index++)
                snapshot[index] = findings[index] ?? throw new ArgumentException("Audit findings must not contain null entries.", nameof(findings));

            return snapshot;
        }

        static int CompareFindings(M12_1BridgeAuthorityAuditFinding left, M12_1BridgeAuthorityAuditFinding right)
        {
            int result = StringComparer.OrdinalIgnoreCase.Compare(left.FilePath, right.FilePath);
            if (result != 0)
                return result;

            result = left.LineNumber.CompareTo(right.LineNumber);
            if (result != 0)
                return result;

            result = StringComparer.Ordinal.Compare(left.RuleId, right.RuleId);
            if (result != 0)
                return result;

            return StringComparer.Ordinal.Compare(left.Token, right.Token);
        }

        int CountFindings(bool isEditorSurface)
        {
            int count = 0;
            for (int index = 0; index < findings.Length; index++)
            {
                if (findings[index].IsEditorSurface == isEditorSurface)
                    count++;
            }

            return count;
        }
    }

    public static class M12_1BridgeAuthorityAuditService
    {
        public static M12_1BridgeAuthorityAuditReport BuildWorkspaceReport()
        {
            string[] runtimeRoots = CreateRuntimeRoots();
            string[] editorRoots = CreateEditorRoots();
            List<string> scannedRoots = new List<string>(runtimeRoots.Length + editorRoots.Length);
            scannedRoots.AddRange(runtimeRoots);
            scannedRoots.AddRange(editorRoots);

            List<M12_1BridgeAuthorityAuditFinding> findings = new List<M12_1BridgeAuthorityAuditFinding>();
            AppendFindings(findings, runtimeRoots, KernelForbiddenPatternScanner.CreateM12_1Rules(), includeEditorFiles: false, includeTestFiles: false, isEditorSurface: false);
            AppendFindings(findings, editorRoots, KernelForbiddenPatternScanner.CreateM12_1LegacyAuthorityRules(), includeEditorFiles: true, includeTestFiles: false, isEditorSurface: true);

            return new M12_1BridgeAuthorityAuditReport(scannedRoots, findings);
        }

        static void AppendFindings(
            List<M12_1BridgeAuthorityAuditFinding> findings,
            IReadOnlyList<string> roots,
            IReadOnlyList<ForbiddenPatternRule> rules,
            bool includeEditorFiles,
            bool includeTestFiles,
            bool isEditorSurface)
        {
            for (int ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
            {
                ForbiddenPatternRule rule = rules[ruleIndex];
                ForbiddenPatternViolation[] violations = KernelForbiddenPatternScanner.ScanTargetRoots(roots, rule, includeEditorFiles, includeTestFiles);
                for (int violationIndex = 0; violationIndex < violations.Length; violationIndex++)
                {
                    ForbiddenPatternViolation violation = violations[violationIndex];
                    findings.Add(new M12_1BridgeAuthorityAuditFinding(
                        violation.RuleId,
                        violation.Token,
                        violation.FilePath,
                        violation.LineNumber,
                        violation.LineText,
                        isEditorSurface));
                }
            }
        }

        static string[] CreateRuntimeRoots()
        {
            string projectRoot = KernelForbiddenPatternScanner.ProjectRootPath;
            return new[]
            {
                Path.Combine(projectRoot, "Assets", "GameLib", "Script", "Kernel"),
                Path.Combine(projectRoot, "Assets", "GameLib", "Script", "Common"),
                Path.Combine(projectRoot, "Assets", "GameLib", "Script", "Project"),
                Path.Combine(projectRoot, "Assets", "Game", "Scripts"),
            };
        }

        static string[] CreateEditorRoots()
        {
            string projectRoot = KernelForbiddenPatternScanner.ProjectRootPath;
            return new[]
            {
                Path.Combine(projectRoot, "Assets", "Editor", "KernelBoot"),
                Path.Combine(projectRoot, "Assets", "Editor", "KernelBootBridge"),
            };
        }
    }
}