# Better uGUI

`com.zeraveene.better-ugui` extends Unity uGUI with reusable features for game interfaces. The current modules provide resolution-independent procedural graphics, mesh gradients driven by reusable presets, built-in view/button transitions, and bundled Mob Sakai UIEffect / UI Particle.

Canvas particle rendering and Mob Sakai SoftMaskForUGUI 3.6.5 are included. See [soft masking](SoftMask.md).

## Requirements

- Unity 6000.0 or newer (verified on 6000.3).
- Unity UI with TextMeshPro: `com.unity.ugui` 2.0.0 (declared as a package dependency; TMP ships inside it in Unity 6).
- Project TMP Essential Resources (`Window > TextMeshPro > Import TMP Essential Resources`) if you use text.

## Installation

Pick one:

- **Tarball:** `Window > Package Manager > + > Install package from tarball...` and pick `com.zeraveene.better-ugui-4.2.2.tgz`.
- **Embedded copy:** copy the `com.zeraveene.better-ugui` folder into your project's `Packages/` folder. Unity picks it up without a manifest entry.
- **Git URL (recommended):** `Window > Package Manager > + > Install package from git URL...`, then enter `https://github.com/zeraveene/better-ugui.git#v4.2.2`. Private repositories require Git authentication on each machine.

Remove any older copy of these scripts (for example from `Assets/_ProjectData/Scripts/UI/` or an older `UI-Tools.unitypackage` import) before installing. The scripts keep their original GUIDs, so existing scenes and prefabs rebind to the package automatically, but two copies of the same classes will not compile.

## Assemblies

| Assembly | Contents | References |
| --- | --- | --- |
| `Zeraveene.BetterUGUI` | Runtime components (`Runtime/`) | `UnityEngine.UI`, `Unity.TextMeshPro` |
| `Zeraveene.BetterUGUI.Editor` | Inspectors and editor utilities (`Editor/`) | runtime assembly, `UnityEngine.UI`, `UnityEditor.UI`, `Unity.TextMeshPro` |

The two Better uGUI assemblies are auto-referenced, so scripts in Assembly-CSharp see the types directly. Your own asmdefs must add `Zeraveene.BetterUGUI` to their references.

Bundled module assemblies are `Coffee.UIEffect`, `Coffee.UIEffect.Editor`, `Coffee.UIParticle`, `Coffee.UIParticle.Editor`, `Coffee.SoftMaskForUGUI`, and `Coffee.SoftMaskForUGUI.Editor`. Their runtime assemblies are auto-referenced. Custom asmdefs must explicitly reference the modules they use. Optional `Coffee.UIEffect.Timeline` / `.Editor` assemblies compile only when Timeline is installed. `Coffee.SoftMaskForUGUI.URP` activates only when URP is installed. See [effects and particles](EffectsAndParticles.md).

## Components

| Component | Namespace | Purpose |
| --- | --- | --- |
| `ProceduralShape` | global | Rounded rectangle / per-corner radii / circle Graphic with border, falloff, dashes and fill (Simple/Filled). |
| `ProceduralShapeCustom` | global | Arbitrary polygon from points, with per-point radius, templates and slicing. |
| `ProceduralShapeTemplate` | global | ScriptableObject holding a reusable custom point set. |
| `ProceduralShapeImage` | global | `Image` subclass that clips a sprite to a procedural shape. |
| `ProceduralShapeOutline` | global | Separate outline Graphic that follows a source shape; independently colored. |
| `ProceduralShapeCustomAnimation` | global | Built-in animation of custom shape points (editor preview included). |
| `UIGradientEffect` | global | Mesh effect coloring any Graphic (including TMP text) from a `UIGradientPreset`. |
| `UIGradientPreset` | global | Shared gradient settings: SingleColor, Linear, Corner, MultiStop. |
| `UIBase` / `UIButtonBase` | `Zeraveene.Shared` | View lifecycle (Initialize/Show/Hide) with fade/slide/scale transitions. |
| `UITransitionPreset` | `Zeraveene.Shared` | Shared transition settings for `UIBase`. |
| `UIViewSequence` | `Zeraveene.Shared` | Sequenced view transforms. |
| `ButtonScaler` | `Zeraveene.SDK.UIEffect` | Hover/press scale feedback through the shared animation runner. |
| Inspector attributes | `Zeraveene.Shared.Inspector` | Lightweight grouping/conditional/button attributes used by the package inspectors. |

Guides:

- [UICreationStructure-Package.md](UICreationStructure-Package.md): visual composition rules.
- [CodeStructure-Package.md](CodeStructure-Package.md): controller integration pattern.

## Samples

The package ships no demo scenes. UIEffect presets and Unity 6 TMP effect shaders are included directly. Create gradient and transition presets in your own project folders (`Assets > Create`, or from code) and reference them from the components.

## Creating presets

The component inspector actions for saving gradient, shape and transition presets open a save dialog starting in `Assets`. Choose a folder in your own project. Cancelling the dialog creates no folders or assets. Preset creation and component assignment support Undo and prefab overrides.

## Built-in animation

No external tween library is required. `Zeraveene.BetterUGUI.Animation` provides:

- `UIAnimationRunner`: one persistent, main-thread `Update` runner, disabled when idle.
- `UIAnimation`: a value-type record combining anchored position, rotation, scale and alpha.
- `UIAnimationHandle`: allocation-free `IsActive`, `Cancel()` and `Complete()` operations. Cancelling never invokes completion.
- `UIEase` / `UIEasing`: easing functions and optional `AnimationCurve` evaluation.

The runner starts with 64 records. Call `UIAnimationRunner.Reserve(maxConcurrentAnimations)` during loading to avoid capacity growth during interaction. Within reserved capacity, animation record creation and updates allocate no managed memory after initialization; creating the runner, growing capacity, user callbacks, and custom geometry generation are outside that guarantee. Shape-track and view-sequence buffers also grow on first use and are reused thereafter.

Panel transitions evaluate easing once for fade, slide and scale together. Animation records are removed in constant time. Disabling or destroying an owner cancels its animations without completion callbacks. Replacing a panel transition cancels the previous transition; zero-duration and transition-free operations complete synchronously. `Show`/`Hide` set final values immediately outside Play Mode.

Time is scaled by default. Custom `UIAnimation` records can set `UnscaledTime = true` for pause-menu effects. Editor shape previews use `EditorApplication.update`, restore authored points when stopped/deselected or entering Play Mode, and never create a runtime runner. Shape morphing remains an opt-in authoring feature; it can rebuild meshes and should not be used for routine panel transitions.

## Migrating from 1.x

Version 2 removes DOTween from both assemblies. Existing component GUIDs, serialized field paths and easing enum integers remain stable, including custom curves. Code using animation types must migrate:

- Replace `DG.Tweening.Ease` with `Zeraveene.BetterUGUI.Animation.UIEase` (`INTERNAL_Custom` becomes `Custom`).
- `ProceduralShapeCustomAnimation.PlayForward()` / `PlayReverse()` now return a `UIAnimationHandle`; use `.IsActive`, `.Cancel()` and `.Complete()` instead of DOTween APIs. An inactive/default handle represents immediate completion or no animation.
- Flash variants retain their enum integers and use linear/quad timing; specialized multi-flash behavior is not implemented. Use an `AnimationCurve` for bespoke panel easing.
- Interrupted transitions no longer invoke the old completion callback. Code depending on that callback should handle interruption explicitly.
- Project code using DOTween independently must migrate separately before removing its installation. This package does not delete dependencies from consuming projects.

The optional tests under `Tests/Editor` require Unity Test Framework and can be run in Test Runner. They are excluded from player assemblies.

## Migrating from 2.x to 3.x

The code namespace and assembly prefix is now `Zeraveene`:

| Previous prefix | New prefix |
| --- | --- |
| `MaxsusWorks.Shared` | `Zeraveene.Shared` |
| `MaxsusWorks.SDK.UIEffect` | `Zeraveene.SDK.UIEffect` |
| `MaxsusWorks.ProceduralUI` | `Zeraveene.BetterUGUI` |

Update imports and fully qualified names in your scripts, and change custom asmdef references to `Zeraveene.BetterUGUI`. Global procedural-shape and gradient types keep their existing names. Script and assembly asset GUIDs are preserved; migration attributes identify the preceding assembly and namespaces.

## Migrating from 3.x to Better uGUI 4.x

Remove the previous `com.zeraveene.procedural-ui` or `com.maxsusworks.procedural-ui` installation before installing `com.zeraveene.better-ugui`. Unity treats each identifier as a separate package, so keeping both installs creates duplicate scripts. Update any manifest dependencies and testables entries.

Replace assembly references `Zeraveene.ProceduralUI`, `Zeraveene.ProceduralUI.Editor` and `Zeraveene.ProceduralUI.Editor.Tests` with their `Zeraveene.BetterUGUI` equivalents. Change animation imports from `Zeraveene.ProceduralUI.Animation` to `Zeraveene.BetterUGUI.Animation`. The `Zeraveene.Shared` and `Zeraveene.SDK.UIEffect` namespaces and procedural component names are unchanged.

Keep the supplied `.meta` files when moving an embedded copy. Component script and assembly asset GUIDs are preserved. Existing `MovedFrom` attributes retain the original type identities; code references and serialized managed references naming the 3.x animation types need updating to their new namespace and assembly.
