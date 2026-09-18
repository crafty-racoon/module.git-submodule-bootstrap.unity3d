using System;
using System.IO;
using NUnit.Framework;

namespace CraftyRacoon.GitSubmoduleBootstrap.Editor.Tests
{
    internal sealed class GitRepositoryLocatorTests
    {
        [Test]
        public void FindsRepositoryRootAboveNestedUnityProject()
        {
            string temporaryRoot = Path.Combine(Path.GetTempPath(), "GitSubmoduleBootstrapTests", Guid.NewGuid().ToString("N"));
            string repositoryRoot = Path.Combine(temporaryRoot, "Repo");
            string unityProjectRoot = Path.Combine(repositoryRoot, "NarrativeRuntime");
            Directory.CreateDirectory(Path.Combine(repositoryRoot, ".git"));
            Directory.CreateDirectory(Path.Combine(unityProjectRoot, "Assets"));

            try
            {
                string actual = GitRepositoryLocator.FindRepositoryRoot(unityProjectRoot);
                Assert.That(actual, Is.EqualTo(repositoryRoot));
            }
            finally
            {
                Directory.Delete(temporaryRoot, true);
            }
        }

        [Test]
        public void ReturnsDotWhenUnityProjectIsRepositoryRoot()
        {
            string path = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "GitSubmoduleBootstrapTests", "SameRoot"));
            string relativePath = GitRepositoryLocator.GetRelativeDescendantPath(path, path);
            Assert.That(relativePath, Is.EqualTo("."));
        }

        [Test]
        public void ReturnsNestedUnityProjectPathRelativeToRepositoryRoot()
        {
            string repositoryRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "GitSubmoduleBootstrapTests", "Repo"));
            string unityProjectRoot = Path.Combine(repositoryRoot, "NarrativeRuntime");
            string relativePath = GitRepositoryLocator.GetRelativeDescendantPath(repositoryRoot, unityProjectRoot);
            Assert.That(relativePath, Is.EqualTo("NarrativeRuntime"));
        }
    }
}
