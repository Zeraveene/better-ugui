# Third-party notices

Better uGUI bundles these MIT-licensed projects by **mob-sakai**. Their copyright notices and full license texts are preserved beside their sources.

| Module | Upstream version | Pinned commit | License |
| --- | --- | --- | --- |
| [UIEffect](https://github.com/mob-sakai/UIEffect/tree/5.11.7/Packages/src) | 5.11.7 | `ff824c234121cd6dde7038c8e8ca4904d7f4d7f3` | [MIT](ThirdParty/UIEffect/LICENSE.md) |
| [SoftMaskForUGUI](https://github.com/mob-sakai/SoftMaskForUGUI/tree/3.6.5/Packages/src) | 3.6.5 | `4d00ad8c608bf42c8668fdf192130fc16e21a130` | [MIT](ThirdParty/SoftMaskForUGUI/LICENSE.md) |
| [UI Particle](https://github.com/mob-sakai/ParticleEffectForUGUI/tree/4.14.1) | 4.14.1 | `eec288e2a5cfd43743b70e9a8c397d5e5300e80a` | [MIT](ThirdParty/UIParticle/LICENSE.md) |

Included: runtime sources, editor inspectors, shaders, UIEffect presets (including the circle texture referenced by its detail preset), and UIEffect's Unity 6 TextMeshPro shader support. Upstream demos, legacy v4 components, and optional ShaderGraph integrations are not bundled.

Local adaptations:

- Package-relative shader includes, icon paths and built-in preset paths point into `com.zeraveene.better-ugui/ThirdParty/`.
- Correct the upstream `UIEffectIconIcon.png` attribute typo.
- Unity 6 TMP shader support is active inside `ThirdParty/UIEffect/Shaders/TextMeshPro/`. Optional shader registration preserves distinct upstream module identifiers inside the combined package; the importer skips shaders already bundled.
- Runtime/editor assembly definitions explicitly reference uGUI and TMP by assembly name. Timeline sources have separate, version-gated assemblies so Timeline remains optional.
- Editor-only using directives are guarded for player compilation.
- The upstream marker DLLs are replaced by source `AlwaysLinkAssembly` attributes; no precompiled plugins are distributed.

Upstream C# namespaces, component script GUIDs, preset GUIDs and shader names are retained. Do not install separate copies of UIEffect, UI Particle or SoftMaskForUGUI alongside Better uGUI: duplicate assemblies, classes, shaders and asset GUIDs would conflict. Remove the standalone package/copy when migrating. Preserve the supplied metadata when updating the bundled versions.

SoftMask adaptations in 4.2.0:

- Runtime, editor, icons, core shaders and Unity 6 TMP support are bundled; demo, Spine and ShaderGraph samples are omitted.
- Package resource paths and UIEffect's soft-mask shader includes resolve to the bundled location. The UIEffect build stripper recognizes bundled SoftMask.
- Optional shader registries use distinct original upstream identifiers even though both modules live inside Better uGUI.
- URP render-scale queries use the optional `Coffee.SoftMaskForUGUI.URP` assembly and a callback bridge; the core assembly has no URP dependency.
- Original metadata and AssemblyInfo are preserved; the marker DLL is replaced with an AlwaysLinkAssembly source attribute.
- Core mask-drawing shader variants are registered during settings initialization so clean builds do not depend on Editor preview history.
