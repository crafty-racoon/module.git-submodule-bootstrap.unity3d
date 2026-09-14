using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace CraftyRacoon.GitSubmoduleBootstrap.Editor.Tests
{
    internal sealed class GitSubmoduleSafetyTests
    {
        [Test]
        public void ExactMatchDoesNotRequireUpdate()
        {
            GitSubmodulePreflightResult result = CreateResult(GitSubmoduleSafetyState.Matched);
            Assert.That(result.RequiresUpdate, Is.False);
            Assert.That(result.CanMutate, Is.False);
        }

        [Test]
        public void MissingSubmoduleAllowsUpdate()
        {
            Assert.That(CreateResult(GitSubmoduleSafetyState.Missing).CanMutate, Is.True);
        }

        [Test]
        public void SafeForwardAllowsUpdate()
        {
            GitSubmoduleSafetyState state = GitSubmodulePreflight.ClassifyRelationExitCodes(0, 1);
            Assert.That(state, Is.EqualTo(GitSubmoduleSafetyState.SafeForward));
            Assert.That(CreateResult(state).CanMutate, Is.True);
        }

        [Test]
        public void DetachedLocalCommitAheadBlocksMutation()
        {
            GitSubmoduleSafetyEntry entry = new GitSubmoduleSafetyEntry("module", "C", "B", GitSubmoduleSafetyState.AheadOfTarget, true, false, "ahead");
            GitSubmodulePreflightResult result = CreateResult(entry);
            Assert.That(result.CanMutate, Is.False, "git submodule update must not execute");
            StringAssert.Contains("UNPROTECTED DETACHED COMMIT", result.BuildBlockedMessage());
        }

        [Test]
        public void BranchAheadBlocksMutation()
        {
            GitSubmoduleSafetyState state = GitSubmodulePreflight.ClassifyRelationExitCodes(1, 0);
            Assert.That(CreateResult(state).CanMutate, Is.False);
        }

        [Test]
        public void DivergedBlocksMutation()
        {
            GitSubmoduleSafetyState state = GitSubmodulePreflight.ClassifyRelationExitCodes(1, 1);
            Assert.That(CreateResult(state).CanMutate, Is.False);
        }

        [TestCase(GitSubmoduleSafetyState.Dirty)]
        [TestCase(GitSubmoduleSafetyState.Conflicted)]
        [TestCase(GitSubmoduleSafetyState.Unknown)]
        public void UnsafeWorkingTreeStateBlocksMutation(GitSubmoduleSafetyState state)
        {
            Assert.That(CreateResult(state).CanMutate, Is.False);
        }

        [Test]
        public void OneSafeAndOneUnsafeBlocksEntireUpdate()
        {
            List<GitSubmoduleSafetyEntry> entries = new List<GitSubmoduleSafetyEntry>
            {
                CreateEntry(GitSubmoduleSafetyState.SafeForward),
                CreateEntry(GitSubmoduleSafetyState.AheadOfTarget)
            };
            GitSubmodulePreflightResult result = new GitSubmodulePreflightResult(entries, Array.Empty<string>(), string.Empty);
            Assert.That(result.CanMutate, Is.False);
        }

        [Test]
        public void GitModulesLocalEditBlocksMutation()
        {
            List<string> problems = new List<string> { ".gitmodules contains local modifications." };
            GitSubmodulePreflightResult result = new GitSubmodulePreflightResult(new[] { CreateEntry(GitSubmoduleSafetyState.SafeForward) }, problems, string.Empty);
            Assert.That(result.CanMutate, Is.False);
        }

        [Test]
        public void StagedParentGitlinkBlocksMutation()
        {
            List<string> problems = new List<string> { "Parent gitlink has a staged local change." };
            GitSubmodulePreflightResult result = new GitSubmodulePreflightResult(new[] { CreateEntry(GitSubmoduleSafetyState.SafeForward) }, problems, string.Empty);
            Assert.That(result.CanMutate, Is.False);
        }

        [Test]
        public void StatusParserNamesPlusAsMismatchWithoutCallingItOutdated()
        {
            GitSubmoduleStatus status = GitSubmoduleStatus.Parse("+0123456789012345678901234567890123456789 Assets/Modules/example (heads/main)\n");
            Assert.That(status.Entries.Count, Is.EqualTo(1));
            Assert.That(status.Entries[0].Prefix, Is.EqualTo('+'));
        }

        private static GitSubmodulePreflightResult CreateResult(GitSubmoduleSafetyState state)
        {
            return CreateResult(CreateEntry(state));
        }

        private static GitSubmodulePreflightResult CreateResult(GitSubmoduleSafetyEntry entry)
        {
            return new GitSubmodulePreflightResult(new[] { entry }, Array.Empty<string>(), string.Empty);
        }

        private static GitSubmoduleSafetyEntry CreateEntry(GitSubmoduleSafetyState state)
        {
            return new GitSubmoduleSafetyEntry("module", "current", "target", state, false, true, state.ToString());
        }
    }
}
