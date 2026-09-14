using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CraftyRacoon.GitSubmoduleBootstrap.Editor
{
    internal enum GitSubmoduleSafetyState
    {
        Matched,
        Missing,
        SafeForward,
        Dirty,
        AheadOfTarget,
        Diverged,
        Conflicted,
        Unknown
    }

    internal sealed class GitSubmoduleSafetyEntry
    {
        internal GitSubmoduleSafetyEntry(string path, string currentCommit, string targetCommit, GitSubmoduleSafetyState state, bool isDetached, bool hasDurableRef, string detail)
        {
            Path = path;
            CurrentCommit = currentCommit;
            TargetCommit = targetCommit;
            State = state;
            IsDetached = isDetached;
            HasDurableRef = hasDurableRef;
            Detail = detail;
        }

        internal string Path { get; }

        internal string CurrentCommit { get; }

        internal string TargetCommit { get; }

        internal GitSubmoduleSafetyState State { get; }

        internal bool IsDetached { get; }

        internal bool HasDurableRef { get; }

        internal string Detail { get; }

        internal bool BlocksUpdate => State == GitSubmoduleSafetyState.Dirty || State == GitSubmoduleSafetyState.AheadOfTarget || State == GitSubmoduleSafetyState.Diverged || State == GitSubmoduleSafetyState.Conflicted || State == GitSubmoduleSafetyState.Unknown;
    }

    internal sealed class GitSubmodulePreflightResult
    {
        internal GitSubmodulePreflightResult(IReadOnlyList<GitSubmoduleSafetyEntry> entries, IReadOnlyList<string> rootProblems, string fatalError)
        {
            Entries = entries;
            RootProblems = rootProblems;
            FatalError = fatalError;
        }

        internal IReadOnlyList<GitSubmoduleSafetyEntry> Entries { get; }

        internal IReadOnlyList<string> RootProblems { get; }

        internal string FatalError { get; }

        internal bool IsBlocked => !string.IsNullOrEmpty(FatalError) || RootProblems.Count > 0 || Entries.Any(IsBlockingEntry);

        internal int MissingCount => Entries.Count(IsMissingEntry);

        internal int SafeForwardCount => Entries.Count(IsSafeForwardEntry);

        internal bool RequiresUpdate => MissingCount > 0 || SafeForwardCount > 0;

        internal bool CanMutate => !IsBlocked && RequiresUpdate;

        internal string BuildBlockedMessage()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("[Git Submodules] Automatic update blocked. No submodule was changed.");
            if (!string.IsNullOrEmpty(FatalError))
            {
                builder.AppendLine(FatalError);
            }

            foreach (string problem in RootProblems)
            {
                builder.AppendLine();
                builder.AppendLine("Parent repository");
                builder.AppendLine("    " + problem);
            }

            foreach (GitSubmoduleSafetyEntry entry in Entries.Where(IsBlockingEntry))
            {
                builder.AppendLine();
                builder.AppendLine(entry.Path);
                builder.AppendLine("    " + entry.Detail);
                if (!string.IsNullOrEmpty(entry.CurrentCommit))
                {
                    builder.AppendLine("    Current HEAD: " + entry.CurrentCommit);
                }

                if (!string.IsNullOrEmpty(entry.TargetCommit))
                {
                    builder.AppendLine("    Parent gitlink: " + entry.TargetCommit);
                }

                if (entry.State == GitSubmoduleSafetyState.AheadOfTarget && entry.IsDetached && !entry.HasDurableRef)
                {
                    builder.AppendLine("    UNPROTECTED DETACHED COMMIT: create a branch or push the commit before syncing.");
                }
            }

            return builder.ToString().TrimEnd();
        }

        private static bool IsBlockingEntry(GitSubmoduleSafetyEntry entry)
        {
            return entry.BlocksUpdate;
        }

        private static bool IsMissingEntry(GitSubmoduleSafetyEntry entry)
        {
            return entry.State == GitSubmoduleSafetyState.Missing;
        }

        private static bool IsSafeForwardEntry(GitSubmoduleSafetyEntry entry)
        {
            return entry.State == GitSubmoduleSafetyState.SafeForward;
        }
    }

    internal sealed class GitSubmodulePreflight
    {
        private readonly GitCommandRunner commandRunner;

        internal GitSubmodulePreflight(GitCommandRunner commandRunner)
        {
            this.commandRunner = commandRunner;
        }

        internal GitSubmodulePreflightResult Analyze(string projectRoot)
        {
            GitCommandResult statusResult = commandRunner.Run(projectRoot, "submodule", "status", "--recursive");
            if (statusResult.ExitCode != 0)
            {
                string error = "Status check failed: " + CombineOutput(statusResult);
                return new GitSubmodulePreflightResult(Array.Empty<GitSubmoduleSafetyEntry>(), Array.Empty<string>(), error);
            }

            GitSubmoduleStatus status = GitSubmoduleStatus.Parse(statusResult.StandardOutput);
            List<string> rootProblems = InspectRootRepository(projectRoot, status.Entries);
            List<GitSubmoduleSafetyEntry> entries = new List<GitSubmoduleSafetyEntry>();
            foreach (GitSubmoduleStatusEntry statusEntry in status.Entries)
            {
                entries.Add(AnalyzeEntry(projectRoot, status.Entries, statusEntry));
            }

            return new GitSubmodulePreflightResult(entries, rootProblems, string.Empty);
        }

        private List<string> InspectRootRepository(string projectRoot, IReadOnlyList<GitSubmoduleStatusEntry> entries)
        {
            List<string> problems = new List<string>();
            GitCommandResult modulesStatus = commandRunner.Run(projectRoot, "status", "--porcelain=v1", "--untracked-files=all", "--", ".gitmodules");
            if (modulesStatus.ExitCode != 0)
            {
                problems.Add("Could not verify .gitmodules working-tree state: " + CombineOutput(modulesStatus));
            }
            else if (!string.IsNullOrWhiteSpace(modulesStatus.StandardOutput))
            {
                problems.Add(".gitmodules contains staged or unstaged local modifications.");
            }

            foreach (GitSubmoduleStatusEntry entry in entries)
            {
                if (FindOwningSubmodule(entries, entry.Path) != null)
                {
                    continue;
                }

                GitCommandResult stagedResult = commandRunner.Run(projectRoot, "diff", "--cached", "--quiet", "--", entry.Path);
                if (stagedResult.ExitCode == 1)
                {
                    problems.Add("Parent gitlink has a staged local change: " + entry.Path);
                }
                else if (stagedResult.ExitCode != 0)
                {
                    problems.Add("Could not verify parent gitlink state for " + entry.Path + ": " + CombineOutput(stagedResult));
                }
            }

            return problems;
        }

        private GitSubmoduleSafetyEntry AnalyzeEntry(string projectRoot, IReadOnlyList<GitSubmoduleStatusEntry> entries, GitSubmoduleStatusEntry entry)
        {
            string ownerPath = FindOwningSubmodule(entries, entry.Path);
            string ownerRoot = string.IsNullOrEmpty(ownerPath) ? projectRoot : Path.Combine(projectRoot, ToPlatformPath(ownerPath));
            string relativePath = string.IsNullOrEmpty(ownerPath) ? entry.Path : entry.Path.Substring(ownerPath.Length + 1);
            GitCommandResult targetResult = commandRunner.Run(ownerRoot, "ls-files", "--stage", "--", relativePath);
            string targetCommit = ParseGitlinkTarget(targetResult.StandardOutput);
            if (targetResult.ExitCode != 0 || string.IsNullOrEmpty(targetCommit))
            {
                return CreateEntry(entry, targetCommit, GitSubmoduleSafetyState.Unknown, false, false, "Could not determine the parent/index gitlink target.");
            }

            if (entry.IsConflicted)
            {
                return CreateEntry(entry, targetCommit, GitSubmoduleSafetyState.Conflicted, false, false, "The parent repository reports a conflicted gitlink.");
            }

            if (entry.IsMissing)
            {
                return CreateEntry(entry, targetCommit, GitSubmoduleSafetyState.Missing, false, false, "Submodule is not initialized.");
            }

            string submoduleRoot = Path.Combine(projectRoot, ToPlatformPath(entry.Path));
            GitCommandResult workingTreeResult = commandRunner.Run(submoduleRoot, "status", "--porcelain=v1", "--untracked-files=all");
            if (workingTreeResult.ExitCode != 0)
            {
                return CreateEntry(entry, targetCommit, GitSubmoduleSafetyState.Unknown, false, false, "Could not inspect the submodule working tree: " + CombineOutput(workingTreeResult));
            }

            if (!string.IsNullOrWhiteSpace(workingTreeResult.StandardOutput))
            {
                bool conflicted = ContainsConflict(workingTreeResult.StandardOutput);
                GitSubmoduleSafetyState dirtyState = conflicted ? GitSubmoduleSafetyState.Conflicted : GitSubmoduleSafetyState.Dirty;
                string detail = conflicted ? "Working tree contains unresolved conflicts." : "Working tree contains staged, unstaged, or untracked changes.";
                return CreateEntry(entry, targetCommit, dirtyState, false, false, detail);
            }

            GitCommandResult headResult = commandRunner.Run(submoduleRoot, "rev-parse", "HEAD");
            if (headResult.ExitCode != 0)
            {
                return CreateEntry(entry, targetCommit, GitSubmoduleSafetyState.Unknown, false, false, "Could not resolve current HEAD: " + CombineOutput(headResult));
            }

            string currentCommit = headResult.StandardOutput.Trim();
            bool isDetached = commandRunner.Run(submoduleRoot, "symbolic-ref", "-q", "HEAD").ExitCode != 0;
            bool hasDurableRef = HasDurableRef(submoduleRoot, currentCommit);
            GitSubmoduleSafetyState state = ClassifyRelation(currentCommit, targetCommit, submoduleRoot);
            string relationDetail = DescribeState(state);
            return new GitSubmoduleSafetyEntry(entry.Path, currentCommit, targetCommit, state, isDetached, hasDurableRef, relationDetail);
        }

        internal GitSubmoduleSafetyState ClassifyRelation(string currentCommit, string targetCommit, string submoduleRoot)
        {
            if (string.Equals(currentCommit, targetCommit, StringComparison.OrdinalIgnoreCase))
            {
                return GitSubmoduleSafetyState.Matched;
            }

            GitCommandResult currentIsAncestor = commandRunner.Run(submoduleRoot, "merge-base", "--is-ancestor", currentCommit, targetCommit);
            GitCommandResult targetIsAncestor = commandRunner.Run(submoduleRoot, "merge-base", "--is-ancestor", targetCommit, currentCommit);
            return ClassifyRelationExitCodes(currentIsAncestor.ExitCode, targetIsAncestor.ExitCode);
        }

        internal static GitSubmoduleSafetyState ClassifyRelationExitCodes(int currentIsAncestorExitCode, int targetIsAncestorExitCode)
        {
            if (currentIsAncestorExitCode == 0)
            {
                return GitSubmoduleSafetyState.SafeForward;
            }

            if (targetIsAncestorExitCode == 0)
            {
                return GitSubmoduleSafetyState.AheadOfTarget;
            }

            if (currentIsAncestorExitCode == 1 && targetIsAncestorExitCode == 1)
            {
                return GitSubmoduleSafetyState.Diverged;
            }

            return GitSubmoduleSafetyState.Unknown;
        }

        private bool HasDurableRef(string submoduleRoot, string currentCommit)
        {
            GitCommandResult result = commandRunner.Run(submoduleRoot, "for-each-ref", "--format=%(refname)", "--contains", currentCommit, "refs/heads", "refs/remotes", "refs/tags");
            return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput);
        }

        private static GitSubmoduleSafetyEntry CreateEntry(GitSubmoduleStatusEntry entry, string targetCommit, GitSubmoduleSafetyState state, bool isDetached, bool hasDurableRef, string detail)
        {
            string currentCommit = entry.IsMissing ? string.Empty : entry.CurrentCommit;
            return new GitSubmoduleSafetyEntry(entry.Path, currentCommit, targetCommit, state, isDetached, hasDurableRef, detail);
        }

        private static string FindOwningSubmodule(IReadOnlyList<GitSubmoduleStatusEntry> entries, string path)
        {
            string owner = null;
            foreach (GitSubmoduleStatusEntry candidate in entries)
            {
                if (string.Equals(candidate.Path, path, StringComparison.Ordinal) || !path.StartsWith(candidate.Path + "/", StringComparison.Ordinal))
                {
                    continue;
                }

                if (owner == null || candidate.Path.Length > owner.Length)
                {
                    owner = candidate.Path;
                }
            }

            return owner;
        }

        private static string ParseGitlinkTarget(string output)
        {
            string trimmed = output.Trim();
            if (!trimmed.StartsWith("160000 ", StringComparison.Ordinal))
            {
                return string.Empty;
            }

            int commitStart = "160000 ".Length;
            int commitEnd = trimmed.IndexOf(' ', commitStart);
            return commitEnd > commitStart ? trimmed.Substring(commitStart, commitEnd - commitStart) : string.Empty;
        }

        private static bool ContainsConflict(string porcelainOutput)
        {
            string[] lines = porcelainOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                if (line.Length < 2)
                {
                    continue;
                }

                string code = line.Substring(0, 2);
                if (code == "DD" || code == "AU" || code == "UD" || code == "UA" || code == "DU" || code == "AA" || code == "UU")
                {
                    return true;
                }
            }

            return false;
        }

        private static string DescribeState(GitSubmoduleSafetyState state)
        {
            switch (state)
            {
                case GitSubmoduleSafetyState.Matched:
                    return "Already matches the parent gitlink.";
                case GitSubmoduleSafetyState.SafeForward:
                    return "Current HEAD is an ancestor of the parent gitlink; safe forward is allowed.";
                case GitSubmoduleSafetyState.AheadOfTarget:
                    return "Current HEAD is ahead of the parent gitlink; automatic downgrade is forbidden.";
                case GitSubmoduleSafetyState.Diverged:
                    return "Current HEAD and the parent gitlink have diverged.";
                default:
                    return "Commit relationship could not be proven safe.";
            }
        }

        private static string CombineOutput(GitCommandResult result)
        {
            return (result.StandardOutput + Environment.NewLine + result.StandardError).Trim();
        }

        private static string ToPlatformPath(string path)
        {
            return path.Replace('/', Path.DirectorySeparatorChar);
        }
    }
}
