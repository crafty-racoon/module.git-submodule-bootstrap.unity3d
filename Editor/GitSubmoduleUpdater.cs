using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CraftyRacoon.GitSubmoduleBootstrap.Editor
{
    [InitializeOnLoad]
    internal static class GitSubmoduleUpdater
    {
        private const string MenuRoot = "Tools/Git Submodules/";
        private const string UpdateMenuPath = MenuRoot + "Update Now";
        private const string AutoUpdateMenuPath = MenuRoot + "Update on Project Open";
        private const string GenerateRootInitializeScriptMenuPath = MenuRoot + "Generate Root Initialize Script";
        private const string SessionKey = "CraftyRacoon.GitSubmoduleBootstrap.StartupUpdateAttempted";
        private const string PreferencePrefix = "CraftyRacoon.GitSubmoduleBootstrap.AutoUpdate.";
        private const int MaximumLogLength = 12000;
        private static Task<GitSubmodulePreflightResult> preflightTask;
        private static Task<GitCommandResult> updateTask;
        private static string activeProjectRoot;
        private static bool activeRequestIsAutomatic;
        private static bool assetRefreshSuspended;
        private static GitSubmoduleUpdateWindow progressWindow;

        static GitSubmoduleUpdater()
        {
            EditorApplication.delayCall += RunStartupUpdate;
            EditorApplication.quitting += HandleEditorQuitting;
        }

        private static string UnityProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static string RepositoryRoot => GitRepositoryLocator.FindRepositoryRoot(UnityProjectRoot);
        private static string GitModulesPath => string.IsNullOrEmpty(RepositoryRoot) ? string.Empty : Path.Combine(RepositoryRoot, ".gitmodules");
        private static string GitMetadataPath => string.IsNullOrEmpty(RepositoryRoot) ? string.Empty : Path.Combine(RepositoryRoot, ".git");
        private static string AutoUpdatePreferenceKey => PreferencePrefix + Application.dataPath.Replace('\\', '/');
        private static bool AutoUpdateEnabled => EditorPrefs.GetBool(AutoUpdatePreferenceKey, true);
        private static bool IsGitWorkingTree => Directory.Exists(GitMetadataPath) || File.Exists(GitMetadataPath);

        private static void RunStartupUpdate()
        {
            bool alreadyAttempted = SessionState.GetBool(SessionKey, false);
            if (Application.isBatchMode || !AutoUpdateEnabled || alreadyAttempted || !File.Exists(GitModulesPath))
            {
                return;
            }

            SessionState.SetBool(SessionKey, true);
            StartUpdate(true);
        }

        [MenuItem(UpdateMenuPath, priority = 2000)]
        private static void UpdateNow()
        {
            StartUpdate(false);
        }

        [MenuItem(UpdateMenuPath, true)]
        private static bool ValidateUpdateNow()
        {
            return preflightTask == null && updateTask == null && File.Exists(GitModulesPath) && IsGitWorkingTree;
        }

        [MenuItem(AutoUpdateMenuPath, priority = 2001)]
        private static void ToggleAutoUpdate()
        {
            bool enabled = !AutoUpdateEnabled;
            EditorPrefs.SetBool(AutoUpdatePreferenceKey, enabled);
            Menu.SetChecked(AutoUpdateMenuPath, enabled);
            Debug.Log("[Git Submodules] Update on project open " + (enabled ? "enabled." : "disabled."));
        }

        [MenuItem(AutoUpdateMenuPath, true)]
        private static bool ValidateAutoUpdate()
        {
            Menu.SetChecked(AutoUpdateMenuPath, AutoUpdateEnabled);
            return true;
        }

        [MenuItem(GenerateRootInitializeScriptMenuPath, priority = 2002)]
        private static void GenerateRootInitializeScript()
        {
            string scriptPath = Path.Combine(RepositoryRoot, GitSubmoduleBootstrapScriptGenerator.GeneratedFileName);
            string generatedContent = GitSubmoduleBootstrapScriptGenerator.BuildScriptContent();
            if (File.Exists(scriptPath))
            {
                string existingContent = File.ReadAllText(scriptPath);
                if (string.Equals(NormalizeLineEndings(existingContent), NormalizeLineEndings(generatedContent), StringComparison.Ordinal))
                {
                    Debug.Log("[Git Submodules] Root initialization script is already up to date: " + scriptPath);
                    EditorUtility.RevealInFinder(scriptPath);
                    return;
                }

                bool replace = EditorUtility.DisplayDialog("Replace root initialization script?", GitSubmoduleBootstrapScriptGenerator.GeneratedFileName + " already exists and differs from the generated version.", "Replace", "Cancel");
                if (!replace)
                {
                    return;
                }
            }

            File.WriteAllText(scriptPath, generatedContent, new UTF8Encoding(false));
            Debug.Log("[Git Submodules] Generated root initialization script: " + scriptPath);
            EditorUtility.RevealInFinder(scriptPath);
        }

        [MenuItem(GenerateRootInitializeScriptMenuPath, true)]
        private static bool ValidateGenerateRootInitializeScript()
        {
            return File.Exists(GitModulesPath) && IsGitWorkingTree;
        }

        private static void StartUpdate(bool automatic)
        {
            if (preflightTask != null || updateTask != null)
            {
                if (!automatic)
                {
                    Debug.Log("[Git Submodules] A safety check or update is already running.");
                }

                return;
            }

            if (!File.Exists(GitModulesPath))
            {
                if (!automatic)
                {
                    Debug.LogWarning("[Git Submodules] No .gitmodules file was found at " + GitModulesPath + ".");
                }

                return;
            }

            if (!IsGitWorkingTree)
            {
                Debug.LogWarning("[Git Submodules] No Git repository root could be resolved above " + UnityProjectRoot + "; update skipped.");
                return;
            }

            activeProjectRoot = RepositoryRoot;
            activeRequestIsAutomatic = automatic;
            PreflightRequest request = new PreflightRequest(activeProjectRoot);
            preflightTask = Task.Run(request.Execute);
            EditorApplication.update += PollOperation;
        }

        private static void PollOperation()
        {
            if (preflightTask != null && preflightTask.IsCompleted)
            {
                GitSubmodulePreflightResult result = preflightTask.Result;
                preflightTask = null;
                CompletePreflight(result);
                return;
            }

            if (updateTask != null && updateTask.IsCompleted)
            {
                GitCommandResult result = updateTask.Result;
                updateTask = null;
                CompleteUpdate(result);
            }
        }

        private static void CompletePreflight(GitSubmodulePreflightResult result)
        {
            if (result.IsBlocked)
            {
                string message = result.BuildBlockedMessage();
                Debug.LogWarning(message);
                if (!activeRequestIsAutomatic)
                {
                    GitSubmoduleUpdateWindow.OpenBlocked(message);
                }

                FinishOperation();
                return;
            }

            if (!result.CanMutate)
            {
                if (!activeRequestIsAutomatic)
                {
                    Debug.Log("[Git Submodules] All submodules already match the parent repository gitlinks.");
                }

                FinishOperation();
                return;
            }

            Debug.Log("[Git Submodules] Preflight approved " + result.MissingCount + " missing and " + result.SafeForwardCount + " safe-forward submodule path(s); starting update.");
            progressWindow = GitSubmoduleUpdateWindow.OpenProgress(result.MissingCount, result.SafeForwardCount);
            AssetDatabase.DisallowAutoRefresh();
            assetRefreshSuspended = true;
            UpdateRequest request = new UpdateRequest(activeProjectRoot);
            updateTask = Task.Run(request.Execute);
        }

        private static void CompleteUpdate(GitCommandResult result)
        {
            CloseProgressWindow();
            ResumeAssetRefresh();
            string output = FormatOutput(result.StandardOutput, result.StandardError);
            if (result.ExitCode == 0)
            {
                Debug.Log("[Git Submodules] Safe update completed successfully." + output);
                FinishOperation();
                AssetDatabase.Refresh();
                return;
            }

            Debug.LogError("[Git Submodules] Update failed with exit code " + result.ExitCode + ". Authenticate Git or run the command manually, then use Tools > Git Submodules > Update Now." + output);
            FinishOperation();
        }

        private static void FinishOperation()
        {
            EditorApplication.update -= PollOperation;
            preflightTask = null;
            updateTask = null;
            activeProjectRoot = null;
            activeRequestIsAutomatic = false;
            CloseProgressWindow();
            ResumeAssetRefresh();
        }

        private static void CloseProgressWindow()
        {
            if (progressWindow == null)
            {
                return;
            }

            progressWindow.Close();
            progressWindow = null;
        }

        private static void ResumeAssetRefresh()
        {
            if (!assetRefreshSuspended)
            {
                return;
            }

            AssetDatabase.AllowAutoRefresh();
            assetRefreshSuspended = false;
        }

        private static void HandleEditorQuitting()
        {
            CloseProgressWindow();
            ResumeAssetRefresh();
        }

        private static string NormalizeLineEndings(string content)
        {
            return content.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        private static string FormatOutput(string standardOutput, string standardError)
        {
            string output = string.Join(Environment.NewLine, new[] { standardOutput.Trim(), standardError.Trim() }).Trim();
            if (string.IsNullOrEmpty(output))
            {
                return string.Empty;
            }

            if (output.Length > MaximumLogLength)
            {
                output = output.Substring(0, MaximumLogLength) + Environment.NewLine + "[output truncated]";
            }

            return Environment.NewLine + output;
        }

        private sealed class PreflightRequest
        {
            private readonly string projectRoot;

            internal PreflightRequest(string projectRoot)
            {
                this.projectRoot = projectRoot;
            }

            internal GitSubmodulePreflightResult Execute()
            {
                GitSubmodulePreflight preflight = new GitSubmodulePreflight(new GitCommandRunner());
                return preflight.Analyze(projectRoot);
            }
        }

        private sealed class UpdateRequest
        {
            private readonly string projectRoot;

            internal UpdateRequest(string projectRoot)
            {
                this.projectRoot = projectRoot;
            }

            internal GitCommandResult Execute()
            {
                GitCommandRunner runner = new GitCommandRunner();
                return runner.Run(projectRoot, "submodule", "update", "--init", "--recursive");
            }
        }
    }
}
