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
https://github.com/zeraveene/better-ugui.git#v4.2.0
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
