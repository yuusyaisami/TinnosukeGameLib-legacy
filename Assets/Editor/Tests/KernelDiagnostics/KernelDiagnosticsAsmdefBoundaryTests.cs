#nullable enable
using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class KernelDiagnosticsAsmdefBoundaryTests
    {
        static readonly string[] KernelAsmdefPaths =
        {
            "Assets/GameLib/Script/Kernel/Abstractions/GameLib.Kernel.Abstractions.asmdef",
            "Assets/GameLib/Script/Kernel/Authoring/GameLib.Kernel.Authoring.asmdef",
            "Assets/GameLib/Script/Kernel/Boot/GameLib.Kernel.Boot.asmdef",
            "Assets/GameLib/Script/Kernel/Contributions/GameLib.Kernel.Contributions.asmdef",
            "Assets/GameLib/Script/Kernel/Diagnostics/Core/GameLib.Kernel.Diagnostics.asmdef",
            "Assets/GameLib/Script/Kernel/Diagnostics/Unity/GameLib.Kernel.Diagnostics.Unity.asmdef",
            "Assets/GameLib/Script/Kernel/Generation/GameLib.Kernel.Generation.asmdef",
            "Assets/GameLib/Script/Kernel/IR/GameLib.Kernel.IR.asmdef",
            "Assets/GameLib/Script/Kernel/Validation/GameLib.Kernel.Validation.asmdef",
        };

        static readonly string[] EditorTestAsmdefPaths =
        {
            "Assets/Editor/Tests/KernelBoot/GameLib.Tests.Kernel.Boot.Editor.asmdef",
            "Assets/Editor/Tests/KernelDiagnostics/GameLib.Tests.Kernel.Editor.asmdef",
            "Assets/Editor/Tests/KernelDiagnostics/Support/GameLib.Tests.Kernel.Support.Editor.asmdef",
        };

        const string IntegrationPlayModeAsmdefPath = "Assets/Tests/Integration/PlayMode/GameLib.Tests.Integration.PlayMode.asmdef";

        [Test]
        public void DiagnosticsCoreAsmdef_IsPureCoreAndUnityFree()
        {
            AsmdefModel asmdef = LoadAsmdef("Assets/GameLib/Script/Kernel/Diagnostics/Core/GameLib.Kernel.Diagnostics.asmdef");

            Assert.That(asmdef.name, Is.EqualTo("GameLib.Kernel.Diagnostics"));
            Assert.That(asmdef.noEngineReferences, Is.True);
            Assert.That(asmdef.references ?? Array.Empty<string>(), Is.Empty);
        }

        [Test]
        public void DiagnosticsUnityAsmdef_ReferencesCoreAssembly()
        {
            AsmdefModel asmdef = LoadAsmdef("Assets/GameLib/Script/Kernel/Diagnostics/Unity/GameLib.Kernel.Diagnostics.Unity.asmdef");

            Assert.That(asmdef.name, Is.EqualTo("GameLib.Kernel.Diagnostics.Unity"));
            Assert.That(asmdef.noEngineReferences, Is.False);
            Assert.That(asmdef.references, Does.Contain("GameLib.Kernel.Diagnostics"));
        }

        [Test]
        public void KernelDiagnosticsEditorTestAsmdef_IsEditorOnlyAndNotAutoReferenced()
        {
            AsmdefModel asmdef = LoadAsmdef("Assets/Editor/Tests/KernelDiagnostics/GameLib.Tests.Kernel.Editor.asmdef");

            Assert.That(asmdef.name, Is.EqualTo("GameLib.Tests.Kernel.Editor"));
            Assert.That(asmdef.autoReferenced, Is.False);
            Assert.That(asmdef.includePlatforms, Is.EquivalentTo(new[] { "Editor" }));
            Assert.That(asmdef.references, Does.Contain("GameLib.Kernel.Diagnostics"));
            Assert.That(asmdef.references, Does.Contain("GameLib.Kernel.Diagnostics.Unity"));
            Assert.That(asmdef.optionalUnityReferences, Does.Contain("TestAssemblies"));
        }

        [Test]
        public void KernelAsmdefs_DoNotReferenceLegacyOrTestAssemblies()
        {
            foreach (string relativePath in KernelAsmdefPaths)
            {
                AsmdefModel asmdef = LoadAsmdef(relativePath);

                Assert.That(asmdef.name, Does.StartWith("GameLib.Kernel."), "Unexpected non-kernel asmdef in kernel boundary set: " + asmdef.name);

                foreach (string reference in asmdef.references ?? Array.Empty<string>())
                {
                    Assert.That(IsLegacyAssembly(reference), Is.False, asmdef.name + " may not reference legacy quarantine assembly " + reference + ".");
                    Assert.That(IsTestAssembly(reference), Is.False, asmdef.name + " may not reference test assembly " + reference + ".");
                }
            }
        }

        [TestCaseSource(nameof(EditorTestAsmdefPaths))]
        public void EditorTestAsmdefs_AreExplicitEditorOnlyTestAssemblies(string relativePath)
        {
            AsmdefModel asmdef = LoadAsmdef(relativePath);

            Assert.That(asmdef.name, Does.StartWith("GameLib.Tests."));
            Assert.That(asmdef.autoReferenced, Is.False, asmdef.name + " must stay non-auto-referenced.");
            Assert.That(asmdef.includePlatforms ?? Array.Empty<string>(), Is.EquivalentTo(new[] { "Editor" }), asmdef.name + " must stay editor-only.");
            Assert.That(asmdef.defineConstraints ?? Array.Empty<string>(), Does.Contain("UNITY_INCLUDE_TESTS"), asmdef.name + " must declare UNITY_INCLUDE_TESTS.");
            Assert.That(asmdef.optionalUnityReferences ?? Array.Empty<string>(), Does.Contain("TestAssemblies"), asmdef.name + " must declare TestAssemblies.");

            foreach (string reference in asmdef.references ?? Array.Empty<string>())
            {
                Assert.That(IsLegacyAssembly(reference), Is.False, asmdef.name + " must not hide legacy quarantine by referencing " + reference + ".");
            }
        }

        [Test]
        public void PlayModeTestAsmdef_IsExplicitTestOnlyAndDoesNotBackReferenceTests()
        {
            AsmdefModel asmdef = LoadAsmdef(IntegrationPlayModeAsmdefPath);

            Assert.That(asmdef.name, Is.EqualTo("GameLib.Tests.Integration.PlayMode"));
            Assert.That(asmdef.autoReferenced, Is.False);
            Assert.That(asmdef.defineConstraints ?? Array.Empty<string>(), Does.Contain("UNITY_INCLUDE_TESTS"));
            Assert.That(asmdef.optionalUnityReferences ?? Array.Empty<string>(), Does.Contain("TestAssemblies"));

            foreach (string reference in asmdef.references ?? Array.Empty<string>())
            {
                Assert.That(IsTestAssembly(reference), Is.False, asmdef.name + " must depend on production kernel APIs, not other test assemblies like " + reference + ".");
                Assert.That(IsLegacyAssembly(reference), Is.False, asmdef.name + " must not consume legacy quarantine assembly " + reference + ".");
            }
        }

        static bool IsLegacyAssembly(string assemblyName)
        {
            return assemblyName.StartsWith("GameLib.Legacy.", StringComparison.Ordinal)
                || string.Equals(assemblyName, "GameLib.Legacy", StringComparison.Ordinal);
        }

        static bool IsTestAssembly(string assemblyName)
        {
            return assemblyName.StartsWith("GameLib.Tests.", StringComparison.Ordinal);
        }

        static AsmdefModel LoadAsmdef(string relativePath)
        {
            string absolutePath = Path.Combine(ProjectRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.That(File.Exists(absolutePath), Is.True, "Missing asmdef: " + relativePath);
            string json = File.ReadAllText(absolutePath);
            AsmdefModel? asmdef = JsonUtility.FromJson<AsmdefModel>(json);
            Assert.That(asmdef, Is.Not.Null, "Failed to parse asmdef: " + relativePath);
            return asmdef!;
        }

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

        [Serializable]
        sealed class AsmdefModel
        {
            public string name = string.Empty;
            public string[]? references;
            public string[]? includePlatforms;
            public bool autoReferenced;
            public string[]? defineConstraints;
            public string[]? optionalUnityReferences;
            public bool noEngineReferences;
        }
    }
}
