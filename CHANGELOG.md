# Changelog

All notable changes to this package are documented in this file.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the package uses [Semantic Versioning](https://semver.org/).

## [4.0.0] - 2026-10-15

### Changed
- Renamed the package to Better uGUI (`com.zeraveene.better-ugui`) with repository `zeraveene/better-ugui`.
- Renamed assemblies and animation namespaces to `Zeraveene.BetterUGUI`.
- Preserved component names and asset GUIDs. Update package dependencies, assembly references and animation imports when migrating from 3.x.
- Positioned procedural graphics, gradients and animations as the first modules of the broader uGUI extension package. Soft masking and Canvas particle support are planned, not included.

## [3.0.1] - 2026-10-15

### Changed
- Renamed the UPM identifier and embedded folder to `com.zeraveene.procedural-ui`.
- Updated installation examples, release tooling, package lock and authoring helper paths.
- Preserved asset GUIDs and legacy type migration attributes. Remove the previous package installation before installing under the new identifier.

## [3.0.0] - 2026-10-15

### Changed
- Renamed the namespace and assembly prefix from MaxsusWorks to Zeraveene throughout runtime, editor, tests and authoring helpers.
- Added type migration attributes and preserved script/assembly asset GUIDs.
- Updated documentation, release tooling and package author branding. The UPM identifier remains unchanged.

## [2.0.0] - 2026-10-15

### Added
- Shared animation runner with reusable struct records, stable handles, O(1) cancellation/removal, capacity reservation, scaled/unscaled clocks, easing and custom curves.
- Combined panel property animation; cached view-sequence and shape-track playback buffers.
- Unity tests for animation lifecycle, migration settings, playback and managed allocation behavior.

### Changed
- Panel, button, view sequence and custom shape animations use the built-in runner.
- Shape editor previews use EditorApplication.update and restore authored points on stop.
- Interrupted transitions cancel without firing stale completion callbacks; transition-free panels complete immediately.
- Animation API now exposes UIEase and UIAnimationHandle (see migration guide).
- Button release outside the hovered area returns to its original scale; BackToStart consistently uses anchored coordinates.

### Removed
- DOTween runtime/editor dependencies and assembly references. No external tween installation is required.

## [1.0.1] - 2026-10-15

### Fixed
- Preset save dialogs start in `Assets` instead of using the original project's `_ProjectData` paths.
- Cancelling preset creation no longer creates project folders.
- Gradient and transition preset creation participates in Undo; gradient preset assignment records prefab overrides.

### Added
- Repository release tool validates package metadata, assembly boundaries and asset GUIDs before creating a deterministic UPM tarball with a SHA-256 checksum.

## [1.0.0] - 2026-10-15

### Added
- Initial UPM package (`com.maxsusworks.procedural-ui`), converted from the in-house `Assets/_ProjectData/Scripts/UI` tools.
- Assembly definitions `MaxsusWorks.ProceduralUI` (runtime) and `MaxsusWorks.ProceduralUI.Editor` (editor).
- Convex fast path for procedural shape meshing.
- Adaptive circle tessellation (segment count scales with on-screen radius).

### Changed
- Script GUIDs are unchanged, so existing scenes, prefabs and presets rebind to the package.

### Removed
- Odin Inspector dependency; the inspectors use the package's own lightweight attributes (`MaxsusWorks.Shared.Inspector`).
- Example scenes, prefabs, presets and the old `.unitypackage` export tooling. The package ships no samples.
