# Better uGUI

Reusable extensions for Unity uGUI by Zeraveene.

## Available now

- Resolution-independent procedural shapes, rounded corners, borders, outlines and custom polygons.
- Reusable mesh gradient presets, including text gradients.
- Built-in panel transitions, view sequences and button scale feedback without an external tween dependency.
- Bundled Mob Sakai UIEffect 5.11.7: filters, blur, dissolve, outlines, gradients, replicas and tweening, with Unity 6 TMP shaders.
- Bundled Mob Sakai UI Particle 4.14.1: particles and trails rendered directly inside any Canvas, including Screen Space Overlay.
- Bundled Mob Sakai SoftMaskForUGUI 3.6.5: soft masks, anti-aliased masking and masking shapes, with Unity 6 TMP support.

## Install through Git

In Unity, open **Window > Package Manager > + > Install package from git URL** and enter:

```text
https://github.com/zeraveene/better-ugui.git#v0.1.3
```

Requires Unity 6000.0+ and uGUI 2.0.0. Private repository access requires Git authentication on each machine. Commit your project's `Packages/manifest.json` and `Packages/packages-lock.json` so other machines can restore the same package version.

Package ID: `com.zeraveene.better-ugui`.

## Soft masking

Mob Sakai SoftMaskForUGUI 3.6.5 is included, with soft edges, anti-aliased masking and Unity 6 TMP support. See [soft masking](Documentation~/SoftMask.md).

## Included Mob Sakai modules

UIEffect, UI Particle and SoftMask are included in this package; no additional Coffee package installation is required. Add **UIEffect** to a Graphic, or **UI Particle** to the parent of an existing particle prefab. Keep an Overlay Canvas for full-resolution UI. Use the bundled `UI/Additive` material or `UI/Default` for particles; ordinary particle shaders retain upstream compatibility limitations.

Do not install standalone UIEffect/UI Particle/SoftMask alongside Better uGUI. Their original namespaces and asset GUIDs are retained for compatibility. Custom asmdefs reference `Coffee.UIEffect` / `Coffee.UIParticle` / `Coffee.SoftMaskForUGUI` when using those APIs.

See [effects and particles](Documentation~/EffectsAndParticles.md) and [third-party notices](Third%20Party%20Notices.md).

## Documentation

- [Setup, API overview and migration](Documentation~/README.md)
- [UI composition](Documentation~/UICreationStructure-Package.md)
- [Controller integration](Documentation~/CodeStructure-Package.md)
- [Changelog](CHANGELOG.md)

## Development

This repository contains the UPM package at its root; it is not a Unity project. Develop and test it as an embedded package in a Unity test project under `Packages/com.zeraveene.better-ugui/`. Preserve all supplied `.meta` files when copying between the package repository and the test project. Commit and tag the package repository to publish a version that consuming projects can install.

### Simple UI animation

Add **UI > Better uGUI > UI Animation** and assign **Fade Target**, **Scale Target** and **Position Target** inside their respective foldouts. Each channel uses its own target, or the attached object when its target is empty. Fade accepts a CanvasGroup or Graphic, or resolves those components on the assigned object (CanvasGroup takes priority). Enable the channels, set From/To and Duration, then press Play to preview. Defaults are fade 1 → 0, uniform scale 1 → 1.3, 0.3 seconds each and OutQuad easing. Channels play together with independent durations. Shared targets saved by versions 4.2.7 and 4.2.8 are migrated automatically, preserving any explicit Fade Target.

For entry and exit, configure From as the hidden state and To as the visible state. Call `Play()` for entry and `PlayReverse()` for exit. Separate UIAnimation components on controller objects can target the same panel when entry and exit need different settings. Activate the panel before entry and keep the controller enabled throughout playback.

The UI Toolkit inspector groups Fade, Scale, Position and Size into boxed foldouts with independent enable switches. In Scale, enable **Separate Axes** to edit XYZ From/To values, for example `(0, 1, 1)` to `(1, 1, 1)` for a horizontal reveal. With Separate Axes off, Scale uses the original uniform From/To values.

**Size** animates actual RectTransform width and height in local UI units, including stretched anchors. Assign **Size Target** or leave it empty to use this object's RectTransform. Enable **Animate Width**, **Animate Height**, or both, set their From/To values and a shared Duration. It supports OutQuad easing, Play Reverse and preview restoration. A Layout Group or Content Size Fitter controlling those dimensions can override the animated size.

Call `bool Play()` to restart from the configured values and `bool Stop()` to stop at the current values. Play returns false when disabled or no channel can run; Stop returns whether it interrupted playback. Playback is explicit, with no events or automatic playback. Edit Mode Stop/deselect restores the original values.

### UI pulse

Add **UI > Better uGUI > UI Pulse** for a repeating pulse. Choose **Alpha** (default) or **Scale** from Mode. Alpha automatically uses a CanvasGroup if present, otherwise the same object's `UnityEngine.UI.Graphic`, including procedural shapes, procedural images, standard Images and text. A CanvasGroup is optional. Scale animates uniform local scale.

Defaults: From 1, To 0.5, Duration 0.3 seconds per direction, InOutQuad easing with yoyo playback. Enable **Always Play** to start whenever the component is enabled in Play Mode, or call `bool Play()` / `bool Stop()` explicitly. Play restarts from From; Stop holds the current value. The inspector Play/Stop buttons also preview in Edit Mode and restore the original alpha and scale on stop or deselection.

Enable **Restart** to reset to the starting value after each pass. Choose the easing with **Ease**. For a repeating scale-up, set Mode to Scale, From to 0, To to 1, Duration to 0.3, Restart on and Ease to OutQuad.

Both `UIAnimation` and `UIPulse` expose `bool PlayReverse()` and an inspector **Play Reverse** button. Reverse restarts at the configured To values and animates toward From with the same duration and easing, without modifying settings. UIPulse uses the selected restart or ping-pong behavior from that reversed starting direction. `Play()` always restarts forward.
