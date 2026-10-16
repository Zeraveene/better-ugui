# Changelog

All notable changes to this package are documented in this file.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the package uses [Semantic Versioning](https://semver.org/).

## [4.2.1] - 2026-10-16

### Added
- Simple `Zeraveene.BetterUGUI.UIAnimation` component with optional Fade and Scale foldouts, independent durations, OutQuad easing, and bool-returning Play/Stop methods.
- Inspector Play/Stop preview in Edit Mode, restoring original alpha and scale when stopped or deselected. Fade uses CanvasGroup first, then Image.

## [4.2.0] - 2026-10-16

### Added
- Bundled Mob Sakai SoftMaskForUGUI 3.6.5, including inspectors, masking shapes, shaders, MIT license and active Unity 6 TMP support.
- Optional URP integration for soft-mask render scaling without requiring URP in built-in renderer projects.
- Integration coverage for mask buffers, automatic child discovery, UIEffect/TMP shader composition and disabling masks.

### Fixed
- UIEffect shader stripping recognizes bundled SoftMask and retains combined shader variants in player builds.
- Core mask-drawing shaders are registered automatically, including in builds made without previewing masks in the Editor.
- Bundled modules retain distinct optional-shader registry identifiers so combined shaders resolve correctly.

## [4.1.1] - 2026-10-16

### Fixed
- Procedural shapes and outlines use the modern VertexHelper mesh-modifier path so UIEffect receives their geometry and does not make them disappear.
- Regression coverage checks that UIEffect writes valid UV masks on procedural shape meshes.

## [4.1.0] - 2026-10-16

### Added
- Bundled Mob Sakai UIEffect 5.11.7 and UI Particle 4.14.1 inside Better uGUI, including inspectors, shaders, UIEffect presets and MIT license notices.
- Unity 6 TextMeshPro UIEffect shaders are active without a support-sample import.
- Integration checks for shader/preset resolution, procedural/TMP effect materials and Overlay particle mesh/lifetime behavior.

### Changed
- Adapted package-relative resources for the combined package while preserving upstream namespaces, assemblies and asset GUIDs.
- Timeline integrations use optional assemblies; no Timeline package is required.
- ParticleSystem engine module is declared as a package dependency.

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
