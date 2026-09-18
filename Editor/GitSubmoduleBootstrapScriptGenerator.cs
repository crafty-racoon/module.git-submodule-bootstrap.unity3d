namespace CraftyRacoon.GitSubmoduleBootstrap.Editor
{
    internal static class GitSubmoduleBootstrapScriptGenerator
    {
        internal const string GeneratedFileName = "Initialize Submodules.cmd";

        internal static string BuildScriptContent()
        {
            string[] lines =
            {
                "@echo off",
                "setlocal EnableExtensions DisableDelayedExpansion",
                "title Git Submodule Initialization",
                "",
                "pushd \"%~dp0\" >nul || (",
                "    echo [Git Submodules] ERROR: Could not enter the script directory.",
                "    pause",
                "    exit /b 1",
                ")",
                "",
                "where git.exe >nul 2>nul",
                "if errorlevel 1 (",
                "    echo [Git Submodules] ERROR: git.exe was not found on PATH.",
                "    goto :fail",
                ")",
                "",
                "set \"GIT_ROOT=\"",
                "for /f \"delims=\" %%I in ('git rev-parse --show-toplevel 2^>nul') do set \"GIT_ROOT=%%I\"",
                "if not defined GIT_ROOT (",
                "    echo [Git Submodules] ERROR: This script is not inside a Git working tree.",
                "    goto :fail",
                ")",
                "",
                "cd /d \"%GIT_ROOT%\"",
                "echo [Git Submodules] Repository root: %CD%",
                "echo.",
                "",
                "if not exist \".gitmodules\" (",
                "    echo [Git Submodules] No .gitmodules file was found.",
                "    echo No submodule initialization is required.",
                "    goto :success",
                ")",
                "",
                "echo [Git Submodules] Initializing submodules recursively...",
                "git submodule update --init --recursive",
                "if errorlevel 1 (",
                "    echo [Git Submodules] ERROR: git submodule update failed.",
                "    goto :fail",
                ")",
                "",
                "echo [Git Submodules] Initialization completed successfully.",
                "",
                ":success",
                "echo.",
                "popd",
                "pause",
                "exit /b 0",
                "",
                ":fail",
                "echo.",
                "echo [Git Submodules] Initialization was not completed.",
                "popd",
                "pause",
                "exit /b 1"
            };

            return string.Join("\r\n", lines) + "\r\n";
        }
    }
}
