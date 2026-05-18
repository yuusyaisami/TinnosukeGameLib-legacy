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
