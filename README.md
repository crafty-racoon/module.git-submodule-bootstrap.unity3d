# Git Submodule Bootstrap

Unity Editor package that safely synchronizes Git submodules to the
adopting repository's recorded gitlinks when doing so cannot discard or hide
local work.

Install it with Unity Package Manager by adding this dependency to the adopting
project's `Packages/manifest.json`:

```json
"com.craftyracoon.git-submodule-bootstrap": "https://github.com/crafty-racoon/module.git-submodule-bootstrap.unity3d.git"
```

## Fresh clones and the bootstrap boundary

Unity Package Manager fetches this package before its Editor scripts run.
After the package is available, opening Unity performs one safety-checked
startup sync for the project's Git submodules.

## Safety model

The Unity updater and the bootstrap CMD have intentionally different responsibilities.

The Unity updater (**Tools > Git Submodules > Update Now** and the optional startup update) performs the strict repository-state preflight:

- Exact matches are left untouched.
- Missing submodules may be initialized.
- A clean checkout may move forward when its current commit is proven to be an ancestor of the recorded gitlink.
- Dirty, conflicted, ahead, diverged, unknown, staged-gitlink, and semantic `.gitmodules` changes block the entire update.
- No submodule is moved until every relevant submodule passes preflight.
- The updater never uses force, reset, clean, stash, rebase, or remote tracking.

`Initialize Submodules.cmd` is deliberately simpler. It is a fresh-clone bootstrap, not a Git state manager. It resolves the repository root and runs:

```text
git submodule update --init --recursive
```

The CMD does not inspect or repair the parent index, staged gitlinks, dirty submodules, or `.gitmodules` state. If a repository already contains active development changes, use the Unity updater or Git directly instead of treating the bootstrap CMD as a synchronization tool.

## Repository root convention

The bootstrap belongs to the Git repository, not to a specific Unity project.

Terminology:

- **Repository root**: the nearest ancestor directory that contains the adopting repository's `.git` metadata and `.gitmodules`.
- **Unity project root**: the directory that contains `Assets/`, `Packages/`, and `ProjectSettings/`.

Rules:

- `Initialize Submodules.cmd` is always generated at the repository root.
- All Git and submodule commands run with the repository root as their working directory.
- `.gitmodules` paths and parent gitlink pointers are interpreted relative to the repository root.
- A Unity project may be located directly at the repository root or in any descendant directory.
- Do not copy or move the generated initializer into the Unity project directory. Regenerate it with **Tools > Git Submodules > Generate Root Initialize Script** instead.
- The generated initializer should be committed with the adopting repository when fresh clones need a pre-Unity bootstrap path.

Example:

```text
RepositoryRoot/
├─ .git/
├─ .gitmodules
├─ Initialize Submodules.cmd
└─ NarrativeRuntime/
   ├─ Assets/
   ├─ Packages/
   └─ ProjectSettings/
```

This convention keeps Git ownership at the superproject boundary while allowing the Unity project layout to change independently.

## Root initialization script

This repository ships a generic `Initialize Submodules.cmd` at its own Git root, so direct clones or source downloads include the bootstrap script.

Use **Tools > Git Submodules > Generate Root Initialize Script** to generate or replace the same bootstrap at the adopting repository's Git root. The output location is always the resolved Git root, even when the Unity project lives in a nested directory such as `Repo/NarrativeRuntime/`.

The generated script is intentionally small: it verifies Git is available, resolves the repository root, asks Git for the actual `index.lock` path, refuses to run while that lock exists, and then runs `git submodule update --init --recursive`. This prevents the bootstrap from running against the transient index state of an unfinished clone, checkout, or Git LFS filter. Git itself reports checkout/authentication/conflict failures. More restrictive synchronization policy belongs to the Unity updater, not to the bootstrap script.

## Requirements

- Unity 6 or newer.
- `git` must be available on the process `PATH` inherited by Unity.
- Existing credential helpers may provide stored credentials. Background Git commands disable terminal and credential-manager interaction.
