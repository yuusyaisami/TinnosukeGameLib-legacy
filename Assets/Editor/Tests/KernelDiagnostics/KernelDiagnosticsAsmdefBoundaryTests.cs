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
        public void KernelAsmdefs_DoNotReferenceLegacyOrCommonLtsAssemblies()
        {
            string[] asmdefPaths =
            {
                "Assets/GameLib/Script/Kernel/Abstractions/GameLib.Kernel.Abstractions.asmdef",
                "Assets/GameLib/Script/Kernel/Boot/GameLib.Kernel.Boot.asmdef",
                "Assets/GameLib/Script/Kernel/Diagnostics/Core/GameLib.Kernel.Diagnostics.asmdef",
                "Assets/GameLib/Script/Kernel/Diagnostics/Unity/GameLib.Kernel.Diagnostics.Unity.asmdef",
                "Assets/GameLib/Script/Kernel/Generation/GameLib.Kernel.Generation.asmdef",
                "Assets/GameLib/Script/Kernel/IR/GameLib.Kernel.IR.asmdef",
                "Assets/GameLib/Script/Kernel/Layers/Composition/GameLib.Kernel.Layers.Composition.asmdef",
                "Assets/GameLib/Script/Kernel/Layers/Core/GameLib.Kernel.Layers.Core.asmdef",
                "Assets/GameLib/Script/Kernel/Layers/Quarantine/GameLib.Kernel.Layers.Quarantine.asmdef",
                "Assets/GameLib/Script/Kernel/Layers/Unity/GameLib.Kernel.Layers.Unity.asmdef",
                "Assets/GameLib/Script/Kernel/Validation/GameLib.Kernel.Validation.asmdef",
                "Assets/Editor/Tests/KernelDiagnostics/GameLib.Tests.Kernel.Editor.asmdef",
            };

            string[] forbiddenReferenceMarkers =
            {
                "Common.LTS",
                "RuntimeLifetimeScope",
                "BaseLifetimeScope",
                ".Legacy",
                "Legacy.",
            };

            for (int asmdefIndex = 0; asmdefIndex < asmdefPaths.Length; asmdefIndex++)
            {
                string asmdefPath = asmdefPaths[asmdefIndex];
                AsmdefModel asmdef = LoadAsmdef(asmdefPath);
                string[] references = asmdef.references ?? Array.Empty<string>();
                for (int referenceIndex = 0; referenceIndex < references.Length; referenceIndex++)
                {
                    string reference = references[referenceIndex] ?? string.Empty;
                    for (int markerIndex = 0; markerIndex < forbiddenReferenceMarkers.Length; markerIndex++)
                    {
                        string marker = forbiddenReferenceMarkers[markerIndex];
                        Assert.That(
                            reference.IndexOf(marker, StringComparison.OrdinalIgnoreCase),
                            Is.LessThan(0),
                            $"{asmdefPath} references forbidden legacy assembly marker '{marker}' via '{reference}'.");
                    }
                }
            }
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
            public string[]? optionalUnityReferences;
            public bool noEngineReferences;
        }
    }
}
