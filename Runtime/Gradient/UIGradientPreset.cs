using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[CreateAssetMenu(
    fileName = "UIGradientPreset",
    menuName = "Summertime/UI/Gradient Preset",
    order = 1)]
[MovedFrom(true, sourceAssembly: "MaxsusWorks.ProceduralUI")]
public sealed class UIGradientPreset : ScriptableObject
{
    #region Inspector
    [SerializeField] private UIGradientSettings _settings = new UIGradientSettings();
    #endregion

    #region Callback
    public event Action Changed;
    #endregion

    #region Properties
    public UIGradientSettings Settings
    {
        get
        {
            EnsureSettingsBinding();
            return _settings;
        }
    }
    #endregion

    #region Initialization
    private void OnEnable()
    {
        EnsureSettingsBinding();
    }
    #endregion

    #region Public Methods
    public void SetSettings(UIGradientSettings settings)
    {
        EnsureSettingsBinding();
        _settings.CopyFrom(settings);
    }
    #endregion

    #region Editor
#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureSettingsBinding();
        _settings.NotifyChanged();
    }
#endif
    #endregion

    #region Private Methods
    private void EnsureSettingsBinding()
    {
        _settings ??= new UIGradientSettings();
        _settings.Changed -= NotifyChanged;
        _settings.Changed += NotifyChanged;
    }

    private void NotifyChanged()
    {
        Action handlers = Changed;
        if (handlers == null)
            return;

        foreach (Action handler in handlers.GetInvocationList())
        {
            // Unity objects can outlive their native component as managed event targets.
            // Remove those stale callbacks before invoking the remaining listeners.
            if (handler.Target is UnityEngine.Object target && target == null)
            {
                Changed -= handler;
                continue;
            }

            handler.Invoke();
        }
    }
    #endregion
}
