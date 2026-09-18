using NUnit.Framework;

namespace CraftyRacoon.GitSubmoduleBootstrap.Editor.Tests
{
    internal sealed class GitSubmoduleBootstrapScriptGeneratorTests
    {
        [Test]
        public void GeneratedScriptUsesStableRootRelativeBootstrap()
        {
            string script = GitSubmoduleBootstrapScriptGenerator.BuildScriptContent();
            StringAssert.Contains("pushd \"%~dp0\"", script);
            StringAssert.Contains("git submodule update --init --recursive", script);
            StringAssert.Contains("git submodule status --recursive", script);
        }

        [Test]
        public void GeneratedScriptBlocksAnOpenUnityProject()
        {
            string script = GitSubmoduleBootstrapScriptGenerator.BuildScriptContent();
            StringAssert.Contains("Temp\\UnityLockfile", script);
            StringAssert.Contains("[System.IO.FileShare]::None", script);
            StringAssert.Contains("Close Unity completely", script);
        }

        [Test]
        public void GeneratedScriptRefusesMixedTopLevelState()
        {
            string script = GitSubmoduleBootstrapScriptGenerator.BuildScriptContent();
            StringAssert.Contains("The repository has a mixed submodule state.", script);
            StringAssert.Contains("findstr /b /c:\" \" /c:\"+\" /c:\"U\"", script);
        }

        [Test]
        public void GeneratedScriptRefusesStagedSubmodulePointerChanges()
        {
            string script = GitSubmoduleBootstrapScriptGenerator.BuildScriptContent();
            StringAssert.Contains("git diff --cached --quiet HEAD -- \"%%B\"", script);
            StringAssert.Contains("Staged submodule pointer differs from the parent HEAD", script);
            StringAssert.Contains("Commit or unstage the pointer change", script);
        }

        [Test]
        public void GeneratedScriptIsDeterministicAndUsesCrLf()
        {
            string first = GitSubmoduleBootstrapScriptGenerator.BuildScriptContent();
            string second = GitSubmoduleBootstrapScriptGenerator.BuildScriptContent();
            Assert.That(second, Is.EqualTo(first));
            StringAssert.EndsWith("\r\n", first);
            Assert.That(first.Contains("\n") && !first.Contains("\r\n"), Is.False);
        }
    }
}
