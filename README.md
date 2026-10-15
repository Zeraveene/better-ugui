# Better uGUI

Reusable extensions for Unity uGUI by Zeraveene.

## Available now

- Resolution-independent procedural shapes, rounded corners, borders, outlines and custom polygons.
- Reusable mesh gradient presets, including text gradients.
- Built-in panel transitions, view sequences and button scale feedback without an external tween dependency.

## Install through Git

In Unity, open **Window > Package Manager > + > Install package from git URL** and enter:

```text
https://github.com/zeraveene/better-ugui.git#v4.0.0
```

Requires Unity 6000.0+ and uGUI 2.0.0. Private repository access requires Git authentication on each machine. Commit your project's `Packages/manifest.json` and `Packages/packages-lock.json` so other machines can restore the same package version.

Package ID: `com.zeraveene.better-ugui`.

## Planned

- Soft masking.
- Particle system rendering inside a Canvas.

These features are not implemented yet.

## Documentation

- [Setup, API overview and migration](Documentation~/README.md)
- [UI composition](Documentation~/UICreationStructure-Package.md)
- [Controller integration](Documentation~/CodeStructure-Package.md)
- [Changelog](CHANGELOG.md)

## Development

This repository contains the UPM package at its root; it is not a Unity project. Develop and test it as an embedded package in a Unity test project under `Packages/com.zeraveene.better-ugui/`. Preserve all supplied `.meta` files when copying between the package repository and the test project. Commit and tag the package repository to publish a version that consuming projects can install.
