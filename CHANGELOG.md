# Changelog

All notable changes to this module are documented in this file.

## [Unreleased]

## [0.4.0] - 2026-09-18

### Added

- Added **Tools > Git Submodules > Generate Root Initialize Script** to create a deterministic `Initialize Submodules.cmd` in the adopting Unity project's root.
- The generated script blocks active Unity projects, validates Git metadata, refuses mixed submodule states and locally edited `.gitmodules`, and recursively initializes fresh clones without force/reset/clean operations.

## [0.3.0] - 2026-09-18

### Changed

- Restored Unity Package Manager metadata for installation from a Git URL.
- Updated installation guidance to use `Packages/manifest.json`.
- Converted the repository from a UPM package to a Unity source module.
- Replaced the ambiguous `Outdated` state with mismatch relation analysis.
- Added all-or-nothing preflight protection for dirty, conflicted, ahead, diverged, unknown, staged-gitlink, and locally edited `.gitmodules` states.
- Limited automatic mutation to missing submodules and proven clean forwards.
- Made automatic and manual updates share the same safety checks.
- Added diagnostics for unprotected detached commits.

## [0.2.0] - 2026-09-01

### Added

- A preflight status check that skips Git updates when all submodules already
  match the parent repository gitlinks.
- A temporary Unity utility window while missing or outdated submodules are
  initialized or updated.

### Changed

- Asset Database auto-refresh is suspended during the Git update and resumed
  before the final refresh, preventing partial imports while checkouts change.

## [0.1.0] - 2026-09-01

### Added

- Automatic once-per-session submodule initialization in the Unity Editor.
- Manual update and automatic-update toggle menu items.
- Non-blocking Git execution, non-interactive credential behavior, diagnostics,
  and Asset Database refresh.

