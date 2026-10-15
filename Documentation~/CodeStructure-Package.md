# Code Structure — Better uGUI package

This guide maps the studio controller conventions to the package's tools; visual authoring is covered by [UICreationStructure-Package.md](UICreationStructure-Package.md).

## Architecture

- Initialize controllers explicitly through an orchestrator. Never put controller setup in Awake/Start.
- Pass dependencies through Initialize or explicit binding methods. Avoid singleton lookups.
- Present application data through SetData(DisplayModel model). UI does not fetch data or run game rules.
- Expose user intent through Action/Func callbacks. Cross-system signals belong to a dedicated event class.
- Group code into Inspector, Callback, Initialization, Public Methods, and Private Methods regions. Serialized private fields use `_camelCase`; public methods use PascalCase.

## Included APIs

`Zeraveene.Shared.UIBase` in `Runtime/UIBase/UIBase.cs` provides Initialize(bool force=false), Show(bool force=true), Hide(bool force=true), SetTransitionPreset(UITransitionPreset), and Action callbacks OnStartShow/OnStartHide/OnCompleteShow/OnCompleteHide. Extend it through protected HandleInitialize. `UIButtonBase` adds Button, onClick, and SetReddot(bool); initialize before accessing Button/onClick.

`UIGradientEffect` in `Runtime/Gradient/UIGradientEffect.cs` provides SetPreset(UIGradientPreset). It colors a Graphic on the same object and supports TMP text meshes. Geometry components under `Runtime/Procedural/` own mesh generation, rather than application data. `ButtonScaler` (`Runtime/ButtonScaler.cs`) owns hover/press scale through the built-in animation runner.

## Example controller pattern

This snippet is documentation, not a compiled script in the package. It only references tools in the package and Unity/uGUI types. An application orchestrator calls Initialize(), SetData(model), assigns OnConfirm, then Show(false).

```csharp
using System;
using Zeraveene.Shared;
using TMPro;
using UnityEngine;

public sealed class ConfirmDisplayModel
{
    public string Title;
    public bool CanConfirm;
    public UIGradientPreset Surface;
}

public sealed class UIConfirmExample : UIBase
{
    #region Inspector
    [SerializeField] private TextMeshProUGUI _title;
    [SerializeField] private UnityEngine.UI.Button _confirm;
    [SerializeField] private UIGradientEffect _surface;
    #endregion

    #region Callback
    public Action OnConfirm;
    #endregion

    #region Initialization
    protected override void HandleInitialize()
    {
        base.HandleInitialize();
        _confirm.onClick.RemoveListener(HandleConfirm);
        _confirm.onClick.AddListener(HandleConfirm);
    }
    #endregion

    #region Public Methods
    public void SetData(ConfirmDisplayModel model)
    {
        _title.text = model.Title;
        _confirm.interactable = model.CanConfirm;
        _surface.SetPreset(model.Surface);
    }
    #endregion

    #region Private Methods
    private void HandleConfirm() => OnConfirm?.Invoke();

    private void OnDestroy()
    {
        if (_confirm != null) _confirm.onClick.RemoveListener(HandleConfirm);
    }
    #endregion
}
```

## Dependencies and existing behavior

Inspector grouping, conditional fields and buttons use the package's own attributes in `Runtime/Inspector/` (no third-party inspector required). The shared `UIAnimationRunner` handles tool animations and transitions without a third-party tween dependency. uGUI/TMP provide rendering and interaction. Install these as described in [README.md](README.md). Your own asmdefs must reference `Zeraveene.BetterUGUI` (Assembly-CSharp sees it automatically).

Feel, Animancer, MEC, ACTK, UniTask, and Addressables are studio conventions, not dependencies of this package. Adopt them only when the receiving application actually has those libraries.

Existing ButtonScaler/UIViewSequence use Awake internally, and rendering components have Unity rendering callbacks. Keep that distinction from explicit initialization for newly written application controllers. UIBase audio event calls are currently commented out; its audio toggle alone does not play sounds. UIButtonWithHint is not included because it depends on a source-game utility.
