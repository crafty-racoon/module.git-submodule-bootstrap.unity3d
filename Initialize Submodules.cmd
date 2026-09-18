@echo off
setlocal EnableExtensions DisableDelayedExpansion
title Git Submodule Initialization

pushd "%~dp0" >nul || (
    echo [Git Submodules] ERROR: Could not enter the repository root.
    pause
    exit /b 1
)

echo [Git Submodules] Repository root: %CD%
echo.

where git.exe >nul 2>nul
if errorlevel 1 (
    echo [Git Submodules] ERROR: git.exe was not found on PATH.
    goto :fail
)

git rev-parse --show-toplevel >nul 2>nul
if errorlevel 1 (
    echo [Git Submodules] ERROR: This script must be located in a Git working tree.
    goto :fail
)

if not exist ".git" (
    echo [Git Submodules] ERROR: No .git metadata was found.
    goto :fail
)

if not exist ".gitmodules" (
    echo [Git Submodules] No .gitmodules file was found.
    echo No submodule initialization is required.
    goto :success
)

git diff --quiet --ignore-cr-at-eol HEAD -- ".gitmodules"
if errorlevel 2 (
    echo [Git Submodules] ERROR: Could not verify .gitmodules against parent HEAD.
    goto :fail
)
if errorlevel 1 (
    echo [Git Submodules] ERROR: .gitmodules has semantic local modifications.
    echo Commit or revert it before initializing dependencies.
    goto :fail
)

echo [Git Submodules] Verifying parent HEAD submodule pointers...
for /f "tokens=1,*" %%A in ('git config --file ".gitmodules" --get-regexp "path$"') do (
    git diff --cached --quiet HEAD -- "%%B"
    if errorlevel 2 (
        echo [Git Submodules] ERROR: Could not verify the staged submodule pointer for %%B.
        goto :fail
    )
    if errorlevel 1 (
        echo [Git Submodules] ERROR: Staged submodule pointer differs from the parent HEAD for %%B.
        echo Commit or unstage the pointer change before initializing dependencies.
        goto :fail
    )
)

git submodule status >nul 2>nul
if errorlevel 1 (
    echo [Git Submodules] ERROR: Could not inspect submodule status.
    goto :fail
)

git submodule status | findstr /b /c:"+" /c:"U" >nul
if not errorlevel 1 (
    echo [Git Submodules] ERROR: A top-level submodule does not match the parent HEAD pointer.
    echo Resolve the submodule state manually before running this script.
    goto :fail
)

git submodule status | findstr /b /c:"-" >nul
if errorlevel 1 (
    echo [Git Submodules] No missing top-level submodules were found.
    echo Initialization is not required.
    goto :success
)

git submodule status | findstr /b /c:" " >nul
if not errorlevel 1 (
    echo [Git Submodules] ERROR: The repository has a mixed submodule state.
    echo Some top-level submodules are initialized while others are missing.
    goto :fail
)

echo [Git Submodules] Initializing all submodules recursively...
git submodule update --init --recursive
if errorlevel 1 (
    echo [Git Submodules] ERROR: git submodule update failed.
    goto :fail
)

git submodule status --recursive | findstr /b /c:"-" /c:"+" /c:"U" >nul
if not errorlevel 1 (
    echo [Git Submodules] ERROR: Verification found an unresolved submodule state.
    goto :fail
)

echo [Git Submodules] Initialization completed successfully.

:success
echo.
echo [Git Submodules] Done.
popd
pause
exit /b 0

:fail
echo.
echo [Git Submodules] Initialization was not completed. No forced Git operation was used.
popd
pause
exit /b 1
