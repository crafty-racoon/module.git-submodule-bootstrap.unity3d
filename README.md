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

- The adopting repository's indexed gitlink is the desired revision.
- Exact matches are left untouched.
- Missing submodules may be initialized.
- A clean checkout may move forward when its current commit is proven to be an ancestor of the recorded gitlink.
- Dirty, conflicted, ahead, diverged, and unknown states block the entire update.
- No submodule is moved until every relevant submodule passes preflight.
- Detached commits are never moved backward automatically, including commits without a branch, remote-tracking branch, or tag.
- Local `.gitmodules` edits and staged parent gitlink changes block the update.
- The updater never uses force, reset, clean, stash, rebase, or remote tracking.

Automatic checks log an actionable warning when blocked. Manual checks at
**Tools > Git Submodules > Update Now** use the same safety rules and show the
full reason list. The per-project automatic check can be toggled at
**Tools > Git Submodules > Update on Project Open**.

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
- The generated script records the Unity project path only so it can check that project's `Temp/UnityLockfile` before mutating submodules.
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

Use **Tools > Git Submodules > Generate Root Initialize Script** to generate
`Initialize Submodules.cmd` in the adopting project's Git repository root.
Commit that generated file with the adopting project when fresh clones must be
bootstrapped before Unity can compile project code that depends on submodules.

The package resolves the nearest Git worktree root above the Unity project. The generated Windows script is always written there, so layouts such as `Repo/NarrativeRuntime/Assets` work without configuration. The script is deterministic and repository-root-relative. It verifies
that Git and `.gitmodules` are available, refuses to run while that Unity
project is actively open, refuses locally modified `.gitmodules`, verifies
that every top-level submodule gitlink in the index still matches the current
parent `HEAD`, and only performs a recursive initialization when every top-level
submodule is missing. This makes the generated script restore the submodule
revisions recorded by the checked-out parent commit rather than a staged pointer.
An already-initialized repository is a no-op; staged pointer changes and mixed
initialized/missing states are rejected and should be resolved explicitly.

## Requirements

- Unity 6 or newer.
- `git` must be available on the process `PATH` inherited by Unity.
- Existing credential helpers may provide stored credentials. Background Git commands disable terminal and credential-manager interaction.
