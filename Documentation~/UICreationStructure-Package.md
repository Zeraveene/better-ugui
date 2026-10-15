# UI Creation Structure — Better uGUI package

Use this alongside [CodeStructure-Package.md](CodeStructure-Package.md).

## Component map

Paths below are relative to the package root (`Packages/com.zeraveene.better-ugui/`).

| Role | Included implementation |
| --- | --- |
| Basic geometry | `Runtime/Procedural/ProceduralShape.cs` |
| Custom geometry/templates | `ProceduralShapeCustom.cs`, `ProceduralShapeTemplate.cs` in the same folder |
| Image-shaped geometry | `ProceduralShapeImage.cs` in the same folder |
| Separate outline | `ProceduralShapeOutline.cs` in the same folder |
| Geometry animation | `ProceduralShapeCustomAnimation.cs` in the same folder |
| Solid/gradient colors | `Runtime/Gradient/UIGradientEffect.cs` |
| Shared colors | `UIGradientPreset.cs`, `UIGradientSettings.cs` in the same folder |
| Button hover/press scale | `Runtime/ButtonScaler.cs` (`Zeraveene.SDK.UIEffect`) |
| View lifecycle/transitions | `Runtime/UIBase/UIBase.cs` (`Zeraveene.Shared`) |
| Shared transition settings | `UITransitionPreset.cs` in the same folder |
| Button view/red dot | `UIButtonBase.cs` in the same folder |

## Compose the view

```text
Canvas                          CanvasScaler + GraphicRaycaster
└── Panel_Feature                UIBase/controller + CanvasGroup
    └── UI                      RectTransform
        ├── Background          ProceduralShape + UIGradientEffect
        │   └── Outline         ProceduralShapeOutline + UIGradientEffect
        ├── Txt_Title           TextMeshProUGUI
        └── Content             layout container
            └── Btn_Action      Button + ProceduralShape + UIGradientEffect + ButtonScaler
                └── Txt_Label   TextMeshProUGUI
EventSystem                     input module matching project input settings
```

ProceduralShape is already a MaskableGraphic; do not add an Image on the same object as a second surface. Use Image for actual artwork. Outline.Source references the shape it follows. Disable raycasts on decoration and labels; enable them on the button's shape. Set Button.targetGraphic to that shape.

Use Uniform for common radius, NonUniform for independent corner radii, Circle for a circle. Corner points run bottom-left, top-left, top-right, bottom-right in normalized coordinates. RenderType.Filled exposes progress fill. BorderWidth creates an inward border; a separate outline supports independently styled color/thickness.

## Color and layout

Every UIGradientEffect must use Source=Preset with an assigned UIGradientPreset, including SingleColor. Reuse the same preset for the same visual role; put new presets in your own feature folder (never inside the package). Keep base Graphic color white. On softened mesh edges, preserve source alpha with IncludeAlpha=false or an appropriate Multiply blend. Runtime state changes select presets with SetPreset; do not mutate shared preset settings.

Use anchors/layout components for sizing; stretch backgrounds to their container. A background inside a layout group can use LayoutElement.IgnoreLayout. Repeated content is authored as a reusable prefab and placed as nested prefab instances. Text uses your project's TMP font assets. Production panels belong under an existing Canvas rather than each introducing a root Canvas; put constantly changing elements (timers, counters, animated bars) on a nested Canvas so they do not rebuild the whole panel.

## Lifecycle

Initialize UIBase explicitly after its initial layout is established. Override HandleInitialize in derived controllers and call base.HandleInitialize. Initialize(bool) itself is not virtual. Use Show/Hide for lifecycle; force controls repeated requests, not whether animation is skipped. SetTransitionPreset selects shared transitions. For application data and user intent, follow the DisplayModel and Action pattern in the companion code guide.

Animate only CanvasGroup alpha, scale and position. Do not tween mesh-rebuilding properties (radius, size, fill, border) every frame. Verify rendering and interaction in Play Mode at different aspect ratios before adopting a prefab.
