# Soft masking

Better uGUI 4.2.0 bundles [Mob Sakai SoftMaskForUGUI 3.6.5](https://github.com/mob-sakai/SoftMaskForUGUI/tree/3.6.5). No additional package or Unity 6 TMP support sample import is required. Original namespaces and GUIDs are preserved. Do not install a standalone copy alongside Better uGUI.

## Setup

1. Add **Soft Mask** (`Coffee.UISoftMask.SoftMask`) to a parent with a Graphic. Procedural shapes provide mask geometry; sprite Images can provide texture alpha masks.
2. Place content under that parent. SoftMask automatically manages **SoftMaskable** components for descendant graphics.
3. Choose **SoftMasking** for gradual alpha masks, **AntiAliasing** for smoother hard edges, or **Normal** for standard stencil masking.
4. Adjust Softness Range and Down Sampling Rate. Enable Show Mask Graphic if the parent should also be visible.

For a feathered procedural mask, give the geometry an alpha falloff. Softness Range remaps existing alpha; it does not blur an opaque rectangle. UIGradientEffect presets supplying mask alpha need Include Alpha enabled.

Settings are under **Project Settings > UI > Soft Mask**. Soft Mask Enabled must be on for full soft masking. Custom asmdefs reference `Coffee.SoftMaskForUGUI`. URP support activates automatically when URP is installed; built-in renderer projects do not require URP.

## UIEffect and TMP

UIEffect and its included Unity 6 TMP shaders support SoftMask. Better uGUI preserves combined soft-mask shader variants during player builds and automatically registers the internal mask-drawing shaders. Runtime-only custom effect variants still use the upstream shader registry in Project Settings. Standard TMP shaders have dedicated bundled SoftMaskable variants. TMP Essential Resources remain a project requirement for fonts and shader includes. Custom shaders, ShaderGraph and Spine need compatible shader support; optional upstream support samples for ShaderGraph/Spine are not bundled.

UI Particle's ordinary Mask/RectMask2D support is unchanged. The bundled UI/Additive shader does not implement soft-mask sampling; keep topmost particles outside SoftMask containers. Unsupported custom shaders can fall back to a default soft-mask shader and change appearance.

## Cost and resolution

Installation does not add masks to existing scenes. Active full soft masks allocate buffers and render them when dirty. AntiAliasing mode avoids the full soft-mask buffer. Downsampling controls memory and quality.

Overlay canvases do not apply the gameplay camera's URP render scale to mask sampling. Camera/world-space canvases use the optional URP bridge. Dynamic resolution, XR, stacks and custom renderers need validation on target devices. Prefer Overlay UI when native-resolution interfaces are required.

See upstream documentation for nesting limits, masking shapes, inverse regions and alpha hit testing.

## License

MIT, copyright mob-sakai. See [Third Party Notices](../Third%20Party%20Notices.md) for the pinned commit and local changes.
