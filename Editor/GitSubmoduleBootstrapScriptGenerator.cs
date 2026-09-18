using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CraftyRacoon.GitSubmoduleBootstrap.Editor
{
    internal static class GitSubmoduleBootstrapScriptGenerator
    {
        internal const string GeneratedFileName = "Initialize Submodules.cmd";
        private const string GenerateMenuPath = "Tools/Git Submodules/Generate Root Initialize Script";

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static string GitModulesPath => Path.Combine(ProjectRoot, ".gitmodules");
        private static string GitMetadataPath => Path.Combine(ProjectRoot, ".git");
        private static bool IsGitWorkingTree => Directory.Exists(GitMetadataPath) || File.Exists(GitMetadataPath);

        [MenuItem(GenerateMenuPath, priority = 2010)]
        private static void GenerateRootInitializeScript()
        {
            string scriptPath = Path.Combine(ProjectRoot, GeneratedFileName);
            string generatedContent = BuildScriptContent();
            if (File.Exists(scriptPath))
            {
                string existingContent = File.ReadAllText(scriptPath);
                if (string.Equals(NormalizeLineEndings(existingContent), NormalizeLineEndings(generatedContent), StringComparison.Ordinal))
                {
                    Debug.Log("[Git Submodules] Root initialization script is already up to date: " + scriptPath);
                    EditorUtility.RevealInFinder(scriptPath);
                    return;
                }

                bool replace = EditorUtility.DisplayDialog("Replace root initialization script?", GeneratedFileName + " already exists and differs from the generated version.", "Replace", "Cancel");
                if (!replace)
                {
                    return;
                }
            }

            File.WriteAllText(scriptPath, generatedContent, new UTF8Encoding(false));
            Debug.Log("[Git Submodules] Generated root initialization script: " + scriptPath);
            EditorUtility.RevealInFinder(scriptPath);
        }

        [MenuItem(GenerateMenuPath, true)]
        private static bool ValidateGenerateRootInitializeScript()
        {
            return File.Exists(GitModulesPath) && IsGitWorkingTree;
        }

        internal static string BuildScriptContent()
        {
            string[] lines =
            {
                "@echo off",
                "setlocal EnableExtensions DisableDelayedExpansion",
                "title Git Submodule Initialization",
                "",
                "pushd \"%~dp0\" >nul || (",
                "    echo [Git Submodules] ERROR: Could not enter the project root.",
                "    pause",
                "    exit /b 1",
                ")",
                "",
                "echo [Git Submodules] Project root: %CD%",
                "echo.",
                "",
                "if not exist \"ProjectSettings\\ProjectVersion.txt\" (",
                "    echo [Git Submodules] ERROR: This script must be located in a Unity project root.",
                "    goto :fail",
                ")",
                "",
                "if not exist \".git\" (",
                "    echo [Git Submodules] ERROR: No .git metadata was found.",
                "    echo Clone the repository before running this script.",
                "    goto :fail",
                ")",
                "",
                "if not exist \".gitmodules\" (",
                "    echo [Git Submodules] ERROR: No .gitmodules file was found.",
                "    goto :fail",
                ")",
                "",
                "where git.exe >nul 2>nul",
                "if errorlevel 1 (",
                "    echo [Git Submodules] ERROR: git.exe was not found on PATH.",
                "    goto :fail",
                ")",
                "",
                "git rev-parse --show-toplevel >nul 2>nul",
                "if errorlevel 1 (",
                "    echo [Git Submodules] ERROR: The project root is not a Git working tree.",
                "    goto :fail",
                ")",
                "",
                "if exist \"Temp\\UnityLockfile\" (",
                "    where powershell.exe >nul 2>nul",
                "    if errorlevel 1 (",
                "        echo [Git Submodules] ERROR: PowerShell is required to verify the Unity project lock.",
                "        goto :fail",
                "    )",
                "",
                "    powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"$p = Join-Path (Get-Location) 'Temp\\UnityLockfile'; try { $s = [System.IO.File]::Open($p, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None); $s.Dispose(); exit 0 } catch { exit 1 }\"",
                "    if errorlevel 1 (",
                "        echo [Git Submodules] ERROR: This Unity project is currently open.",
                "        echo Close Unity completely, then run this script again.",
                "        goto :fail",
                "    )",
                ")",
                "",
                "git status --porcelain=v1 -- \".gitmodules\" | findstr . >nul",
                "if not errorlevel 1 (",
                "    echo [Git Submodules] ERROR: .gitmodules has local modifications.",
                "    echo Commit or revert it before initializing dependencies.",
                "    goto :fail",
                ")",
                "",
                "git submodule status >nul 2>nul",
                "if errorlevel 1 (",
                "    echo [Git Submodules] ERROR: Could not inspect submodule status.",
                "    goto :fail",
                ")",
                "",
                "git submodule status | findstr /b /c:\"-\" >nul",
                "if errorlevel 1 (",
                "    echo [Git Submodules] No missing top-level submodules were found.",
                "    echo Initialization is not required.",
                "    goto :success",
                ")",
                "",
                "git submodule status | findstr /b /c:\" \" /c:\"+\" /c:\"U\" >nul",
                "if not errorlevel 1 (",
                "    echo [Git Submodules] ERROR: The repository has a mixed submodule state.",
                "    echo Some top-level submodules are initialized while others are missing.",
                "    echo Use Unity Tools ^> Git Submodules ^> Update Now, or resolve the state manually.",
                "    goto :fail",
                ")",
                "",
                "echo [Git Submodules] Initializing all submodules recursively...",
                "git submodule update --init --recursive",
                "if errorlevel 1 (",
                "    echo [Git Submodules] ERROR: git submodule update failed.",
                "    goto :fail",
                ")",
                "",
                "git submodule status --recursive | findstr /b /c:\"-\" /c:\"+\" /c:\"U\" >nul",
                "if not errorlevel 1 (",
                "    echo [Git Submodules] ERROR: Verification found an unresolved submodule state.",
                "    goto :fail",
                ")",
                "",
                "echo [Git Submodules] Initialization completed successfully.",
                "",
                ":success",
                "echo.",
                "echo You can now open the project in Unity.",
                "popd",
                "pause",
                "exit /b 0",
                "",
                ":fail",
                "echo.",
                "echo [Git Submodules] Initialization was not completed. No forced Git operation was used.",
                "popd",
                "pause",
                "exit /b 1"
            };

            return string.Join("\r\n", lines) + "\r\n";
        }

        private static string NormalizeLineEndings(string content)
        {
            return content.Replace("\r\n", "\n").Replace('\r', '\n');
        }
    }
}
