#nullable enable
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class DiagnosticCodeTraceabilityTests
    {
        static readonly Regex DiagnosticCodePattern = new Regex("new\\s+DiagnosticCode\\(\\\"(?<code>[A-Z0-9_]+)\\\"\\)", RegexOptions.CultureInvariant);
        static readonly Regex StaticRulePattern = new Regex("\\\"(?<code>STATIC_RULE_[A-Z0-9_]+)\\\"", RegexOptions.CultureInvariant);
        static readonly Regex TypedIdentityPattern = new Regex("public\\s+readonly\\s+struct\\s+(?<code>(?:ModuleId|ServiceId|ScopeAuthoringId|ScopePlanId|CommandTypeId|CommandExecutorId|CommandPayloadSchemaId|ValueKeyId|ValueSchemaId|LifecycleStepId|RuntimeQueryId|SourceLocationId))\\b", RegexOptions.CultureInvariant);
        static readonly Regex SourceLocationModelPattern = new Regex("public\\s+readonly\\s+struct\\s+(?<code>(?:SourceLocationIR|UnitySourceLocation|LegacySourceLocation|GeneratedSourceLocation))\\b", RegexOptions.CultureInvariant);

        static readonly string[] ExplicitNonCatalogCodes =
        {
            "DIAG_A",
            "DIAG_B",
            "DIAG_C",
            "DIAG_FANOUT",
            "DIAG_PRIMARY",
            "DIAG_RESET",
            "DIAG_INFO",
            "DIAG_WARNING",
            "DIAG_ERROR",
            "DIAG_FATAL",
            "DIAG_TRACE",
            "DIAG_SESSION_BIND",
            "DIAG_CONTEXT_MISMATCH",
            "DIAG_INVALID_SEVERITY",
            "DIAG_INVALID_DOMAIN",
        };

        [Test]
        public void TraceabilityCatalog_ContainsEachInScopeCodeExactlyOnce()
        {
            TraceabilityEntry[] entries = LoadEntries();
            string[] extractedIdentifiers = ExtractCurrentIdentifiers();

            Assert.That(entries, Has.Length.EqualTo(extractedIdentifiers.Length));

            for (int i = 0; i < extractedIdentifiers.Length; i++)
            {
                int matchCount = 0;
                for (int j = 0; j < entries.Length; j++)
                {
                    if (string.Equals(entries[j].Identifier, extractedIdentifiers[i], StringComparison.Ordinal))
                        matchCount++;
                }

                Assert.That(matchCount, Is.EqualTo(1), "Catalog must contain exactly one row for identifier: " + extractedIdentifiers[i]);
            }
        }

        [Test]
        public void TraceabilityCatalog_IdentifierKindsMatchCurrentImplementationForms()
        {
            TraceabilityEntry[] entries = LoadEntries();
            ISet<string> typedIdentities = CollectTypedIdentityNames(Path.Combine(ProjectRootPath, "Assets", "GameLib", "Script", "Kernel", "IR", "KernelIRIdentities.cs"));
            ISet<string> sourceLocationModels = CollectSourceLocationModelNames(Path.Combine(ProjectRootPath, "Assets", "GameLib", "Script", "Kernel", "IR", "KernelIRSourceLocations.cs"));

            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Identifier.StartsWith("STATIC_RULE_", StringComparison.Ordinal))
                {
                    Assert.That(entries[i].IdentifierKind, Is.EqualTo("StaticRuleId"), "Static rule identifiers must declare StaticRuleId kind: " + entries[i].Identifier);
                }
                else if (typedIdentities.Contains(entries[i].Identifier))
                {
                    Assert.That(entries[i].IdentifierKind, Is.EqualTo("TypedIdentityPrimitive"), "Typed identity primitives must declare TypedIdentityPrimitive kind: " + entries[i].Identifier);
                }
                else if (sourceLocationModels.Contains(entries[i].Identifier))
                {
                    Assert.That(entries[i].IdentifierKind, Is.EqualTo("SourceLocationModel"), "Source location models must declare SourceLocationModel kind: " + entries[i].Identifier);
                }
                else
                {
                    Assert.That(entries[i].IdentifierKind, Is.EqualTo("DiagnosticCode"), "Diagnostic codes must declare DiagnosticCode kind: " + entries[i].Identifier);
                }
            }
        }

        [Test]
        public void TraceabilityCatalog_EntriesReferenceExistingFilesFailureMeaningAndSpecEvidence()
        {
            TraceabilityEntry[] entries = LoadEntries();

            for (int i = 0; i < entries.Length; i++)
            {
                Assert.That(entries[i].FailureMeaning, Is.Not.Empty, "Failure meaning must not be empty: " + entries[i].Identifier);
                Assert.That(entries[i].CurrentOwnerPaths, Is.Not.Empty, "Current owner paths must not be empty: " + entries[i].Identifier);
                Assert.That(entries[i].OwningSpecPaths, Is.Not.Empty, "Owning spec paths must not be empty: " + entries[i].Identifier);
                Assert.That(entries[i].SpecEvidenceTokens, Is.Not.Empty, "Spec evidence tokens must not be empty: " + entries[i].Identifier);

                for (int j = 0; j < entries[i].CurrentOwnerPaths.Length; j++)
                {
                    string ownerPath = ToAbsolutePath(entries[i].CurrentOwnerPaths[j]);
                    Assert.That(File.Exists(ownerPath), Is.True, "Missing owner file for identifier: " + entries[i].Identifier + " => " + entries[i].CurrentOwnerPaths[j]);
                    Assert.That(File.ReadAllText(ownerPath), Does.Contain(entries[i].Identifier), "Owner file must contain the mapped identifier: " + entries[i].Identifier + " => " + entries[i].CurrentOwnerPaths[j]);
                }

                List<string> specContents = new List<string>(entries[i].OwningSpecPaths.Length);
                for (int j = 0; j < entries[i].OwningSpecPaths.Length; j++)
                {
                    string specPath = ToAbsolutePath(entries[i].OwningSpecPaths[j]);
                    Assert.That(File.Exists(specPath), Is.True, "Missing spec file for identifier: " + entries[i].Identifier + " => " + entries[i].OwningSpecPaths[j]);
                    specContents.Add(File.ReadAllText(specPath));
                }

                for (int j = 0; j < entries[i].SpecEvidenceTokens.Length; j++)
                {
                    bool found = false;
                    for (int specIndex = 0; specIndex < specContents.Count; specIndex++)
                    {
                        if (specContents[specIndex].Contains(entries[i].SpecEvidenceTokens[j], StringComparison.Ordinal))
                        {
                            found = true;
                            break;
                        }
                    }

                    Assert.That(found, Is.True, "Spec evidence token must appear in at least one owning spec: " + entries[i].Identifier + " => " + entries[i].SpecEvidenceTokens[j]);
                }
            }
        }

        [Test]
        public void TraceabilityCatalog_VerifyingTestAnchorsResolveToExistingTestMethods()
        {
            TraceabilityEntry[] entries = LoadEntries();

            for (int i = 0; i < entries.Length; i++)
            {
                Assert.That(TestAnchorExists(entries[i].VerifyingTestAnchor), Is.True, "Missing verifying test anchor for identifier: " + entries[i].Identifier + " => " + entries[i].VerifyingTestAnchor);
            }
        }

        [Test]
        public void TraceabilityCatalog_DoesNotIncludeExplicitNonCatalogCodes()
        {
            string content = File.ReadAllText(CatalogPath);

            for (int i = 0; i < ExplicitNonCatalogCodes.Length; i++)
            {
                Assert.That(content, Does.Contain("- `" + ExplicitNonCatalogCodes[i] + "`"));
                Assert.That(content, Does.Not.Contain("| " + ExplicitNonCatalogCodes[i] + " |"));
            }
        }

        static TraceabilityEntry[] LoadEntries()
        {
            string[] lines = File.ReadAllLines(CatalogPath);
            List<TraceabilityEntry> entries = new List<TraceabilityEntry>();
            bool inCatalogTable = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                if (string.Equals(trimmed, "## Catalog", StringComparison.Ordinal))
                {
                    inCatalogTable = true;
                    continue;
                }

                if (!inCatalogTable)
                    continue;

                if (!trimmed.StartsWith("|", StringComparison.Ordinal))
                {
                    if (entries.Count > 0)
                        break;

                    continue;
                }

                if (trimmed.Contains("|---|", StringComparison.Ordinal) || trimmed.StartsWith("| Code |", StringComparison.Ordinal))
                    continue;

                string[] rawParts = trimmed.Split('|');
                List<string> cells = new List<string>(6);
                for (int partIndex = 1; partIndex < rawParts.Length - 1; partIndex++)
                {
                    cells.Add(rawParts[partIndex].Trim());
                }

                Assert.That(cells.Count, Is.EqualTo(8), "Catalog row must contain 8 columns: " + trimmed);
                entries.Add(new TraceabilityEntry(
                    identifier: cells[0],
                    identifierKind: cells[1],
                    currentOwnerPaths: SplitCellPaths(cells[2]),
                    owningSpecPaths: SplitCellPaths(cells[3]),
                    specEvidenceTokens: SplitCellPaths(cells[4]),
                    failureMeaning: cells[5],
                    verifyingTestAnchor: cells[6],
                    notes: cells[7]));
            }

            return entries.ToArray();
        }

        static string[] SplitCellPaths(string cell)
        {
            string[] parts = cell.Split(';');
            List<string> paths = new List<string>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                string value = parts[i].Trim();
                if (value.Length > 0)
                    paths.Add(value);
            }

            return paths.ToArray();
        }

        static string[] ExtractCurrentIdentifiers()
        {
            SortedSet<string> identifiers = new SortedSet<string>(StringComparer.Ordinal);

            CollectDiagnosticCodes(Path.Combine(ProjectRootPath, "Assets", "GameLib", "Script", "Kernel", "Diagnostics", "Core", "Service", "KernelDiagnosticService.cs"), identifiers);
            CollectDiagnosticCodes(Path.Combine(ProjectRootPath, "Assets", "Editor", "Tests", "KernelDiagnostics", "KernelDiagnosticsModelTests.cs"), identifiers);
            CollectDiagnosticCodes(Path.Combine(ProjectRootPath, "Assets", "Editor", "Tests", "KernelDiagnostics", "KernelTestArtifactWriterTests.cs"), identifiers);
            CollectStaticRuleIds(Path.Combine(ProjectRootPath, "Assets", "Editor", "Tests", "KernelDiagnostics", "KernelForbiddenPatternScanner.cs"), identifiers);
            CollectTypedIdentityIds(Path.Combine(ProjectRootPath, "Assets", "GameLib", "Script", "Kernel", "IR", "KernelIRIdentities.cs"), identifiers);
            CollectSourceLocationModelIds(Path.Combine(ProjectRootPath, "Assets", "GameLib", "Script", "Kernel", "IR", "KernelIRSourceLocations.cs"), identifiers);

            for (int i = 0; i < ExplicitNonCatalogCodes.Length; i++)
            {
                identifiers.Remove(ExplicitNonCatalogCodes[i]);
            }

            return identifiers.ToArray();
        }

        static void CollectDiagnosticCodes(string filePath, ISet<string> identifiers)
        {
            string content = File.ReadAllText(filePath);
            MatchCollection matches = DiagnosticCodePattern.Matches(content);
            for (int i = 0; i < matches.Count; i++)
            {
                identifiers.Add(matches[i].Groups["code"].Value);
            }
        }

        static void CollectStaticRuleIds(string filePath, ISet<string> identifiers)
        {
            string content = File.ReadAllText(filePath);
            MatchCollection matches = StaticRulePattern.Matches(content);
            for (int i = 0; i < matches.Count; i++)
            {
                identifiers.Add(matches[i].Groups["code"].Value);
            }
        }

        static void CollectTypedIdentityIds(string filePath, ISet<string> identifiers)
        {
            string content = File.ReadAllText(filePath);
            MatchCollection matches = TypedIdentityPattern.Matches(content);
            for (int i = 0; i < matches.Count; i++)
            {
                identifiers.Add(matches[i].Groups["code"].Value);
            }
        }

        static ISet<string> CollectTypedIdentityNames(string filePath)
        {
            SortedSet<string> identifiers = new SortedSet<string>(StringComparer.Ordinal);
            CollectTypedIdentityIds(filePath, identifiers);
            return identifiers;
        }

        static void CollectSourceLocationModelIds(string filePath, ISet<string> identifiers)
        {
            string content = File.ReadAllText(filePath);
            MatchCollection matches = SourceLocationModelPattern.Matches(content);
            for (int i = 0; i < matches.Count; i++)
            {
                identifiers.Add(matches[i].Groups["code"].Value);
            }
        }

        static ISet<string> CollectSourceLocationModelNames(string filePath)
        {
            SortedSet<string> identifiers = new SortedSet<string>(StringComparer.Ordinal);
            CollectSourceLocationModelIds(filePath, identifiers);
            return identifiers;
        }

        static bool TestAnchorExists(string anchor)
        {
            int separatorIndex = anchor.IndexOf('.');
            if (separatorIndex <= 0 || separatorIndex == anchor.Length - 1)
                return false;

            string fixtureName = anchor.Substring(0, separatorIndex);
            string methodName = anchor.Substring(separatorIndex + 1);
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
            {
                Type[] types;
                try
                {
                    types = assemblies[assemblyIndex].GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    List<Type> loadedTypes = new List<Type>(exception.Types.Length);
                    for (int typeIndex = 0; typeIndex < exception.Types.Length; typeIndex++)
                    {
                        if (exception.Types[typeIndex] != null)
                            loadedTypes.Add(exception.Types[typeIndex]!);
                    }

                    types = loadedTypes.ToArray();
                }

                for (int i = 0; i < types.Length; i++)
                {
                    if (!string.Equals(types[i].Name, fixtureName, StringComparison.Ordinal))
                        continue;

                    MethodInfo? method = types[i].GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (method != null)
                        return true;
                }
            }

            return false;
        }

        static string ToAbsolutePath(string workspaceRelativePath)
        {
            return Path.Combine(ProjectRootPath, workspaceRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        static string CatalogPath => Path.Combine(Application.dataPath, "Docs", "v2", "Index", "DiagnosticCodeTraceabilityCatalog.md");

        static string ProjectRootPath
        {
            get
            {
                DirectoryInfo? assetsDirectory = Directory.GetParent(Application.dataPath);
                if (assetsDirectory == null)
                    throw new InvalidOperationException("Unable to resolve project root.");

                return assetsDirectory.FullName;
            }
        }

        readonly struct TraceabilityEntry
        {
            public TraceabilityEntry(string identifier, string identifierKind, string[] currentOwnerPaths, string[] owningSpecPaths, string[] specEvidenceTokens, string failureMeaning, string verifyingTestAnchor, string notes)
            {
                Identifier = identifier;
                IdentifierKind = identifierKind;
                CurrentOwnerPaths = currentOwnerPaths;
                OwningSpecPaths = owningSpecPaths;
                SpecEvidenceTokens = specEvidenceTokens;
                FailureMeaning = failureMeaning;
                VerifyingTestAnchor = verifyingTestAnchor;
                Notes = notes;
            }

            public string Identifier { get; }

            public string IdentifierKind { get; }

            public string[] CurrentOwnerPaths { get; }

            public string[] OwningSpecPaths { get; }

            public string[] SpecEvidenceTokens { get; }

            public string FailureMeaning { get; }

            public string VerifyingTestAnchor { get; }

            public string Notes { get; }
        }
    }
}
