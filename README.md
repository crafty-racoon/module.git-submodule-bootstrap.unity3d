# Git Submodule Bootstrap

Unity Editor source module that safely synchronizes Git submodules to the
adopting repository's recorded gitlinks when doing so cannot discard or hide
local work.

This repository is currently intended for module integration only. Add it as a
Git submodule at:

```text
Assets/Modules/module.git-submodule-bootstrap.unity3d
```

## Fresh clones and the bootstrap boundary

This module cannot initialize itself. Its scripts do not exist in a fresh
working tree until Git checks out this submodule. Before opening Unity, use one
of these commands:

```bash
git clone --recurse-submodules <repository-url>
```

or, from an existing clone:

```bash
git submodule update --init Assets/Modules/module.git-submodule-bootstrap.unity3d
```

After this module is present, opening Unity performs one safety-checked startup
sync for the remaining submodules.

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

## Requirements

- Unity 6 or newer.
- `git` must be available on the process `PATH` inherited by Unity.
- Existing credential helpers may provide stored credentials. Background Git commands disable terminal and credential-manager interaction.
