using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Scripting.APIUpdating;

public enum UIGradientSource
{
    Preset,
    Local
}

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Graphic))]
[AddComponentMenu("UI/Effects/UI Gradient")]
[MovedFrom(true, sourceAssembly: "MaxsusWorks.ProceduralUI")]
public sealed class UIGradientEffect : BaseMeshEffect
{
    #region Inspector
    [SerializeField] private UIGradientSource _source = UIGradientSource.Preset;
    [SerializeField] private UIGradientPreset _preset;
    [SerializeField] private UIGradientBlendMode _blendMode;
    [SerializeField] private bool _includeAlpha = true;
    [SerializeField] private UIGradientSettings _localSettings = new UIGradientSettings();
    #endregion

    #region Runtime Data
    private readonly List<float> _cutTimes = new List<float>(8);
    private readonly List<UIVertex> _triangles = new List<UIVertex>(24);
    private readonly List<UIVertex> _splitTriangles = new List<UIVertex>(48);
    private readonly List<VertexSample> _lowerPolygon = new List<VertexSample>(4);
    private readonly List<VertexSample> _upperPolygon = new List<VertexSample>(4);
    private RectTransform _rectTransform;
    private TMP_Text _tmpText;
    private UIGradientPreset _subscribedPreset;
    private UIGradientPreset _blendTarget;
    private UIGradientPreset _subscribedBlendTarget;
    private float _presetBlend;
    #endregion

    #region Properties
    public UIGradientSource Source => _source;
    public UIGradientPreset Preset => _preset;
    public bool IncludeAlpha
    {
        get => _includeAlpha;
        set
        {
            if (_includeAlpha == value)
                return;

            _includeAlpha = value;
            Refresh();
        }
    }
    public UIGradientBlendMode BlendMode
    {
        get => _blendMode;
        set
        {
            if (_blendMode == value)
                return;

            _blendMode = value;
            Refresh();
        }
    }
    public UIGradientSettings ActiveSettings => GetActiveSettings();
    public UIGradientSettings LocalSettings => _localSettings;
    #endregion

    #region Public Methods
    public void SetPreset(UIGradientPreset preset)
    {
        if (_preset == preset && _source == UIGradientSource.Preset && _blendTarget == null)
            return;

        _blendTarget = null;
        _presetBlend = 0f;
        _preset = preset;
        _source = UIGradientSource.Preset;
        RefreshSubscription();
        Refresh();
    }

    /// <summary>Crossfades rendered preset outputs without editing either asset or LocalSettings.</summary>
    public void SetPresetBlend(UIGradientPreset from, UIGradientPreset to, float progress)
    {
        if (from == null) throw new System.ArgumentNullException(nameof(from));
        if (to == null) throw new System.ArgumentNullException(nameof(to));
        progress = float.IsNaN(progress) ? 0f : Mathf.Clamp01(progress);
        if (progress <= 0f || from == to) { SetPreset(from); return; }
        if (progress >= 1f) { SetPreset(to); return; }
        _source = UIGradientSource.Preset;
        _preset = from;
        _blendTarget = to;
        _presetBlend = progress;
        RefreshSubscription();
        Refresh();
    }

    public void UseLocalSettings(UIGradientSettings settings = null)
    {
        _blendTarget = null;
        _presetBlend = 0f;
        EnsureLocalSettingsBinding();
        if (settings != null)
            _localSettings.CopyFrom(settings);

        _source = UIGradientSource.Local;
        RefreshSubscription();
        Refresh();
    }

    public void EditFromPreset()
    {
        if (_preset == null)
            return;

        UseLocalSettings(_preset.Settings);
    }

    public void ReverseDirection()
    {
        GetActiveSettings().ReverseDirection();
        Refresh();
    }

    public void Refresh()
    {
        // A preset may still have a managed callback briefly after Unity destroys
        // the component's native object (for example during editor/Undo changes).
        if (this == null)
            return;

        CacheComponents();
        graphic?.SetVerticesDirty();

        if (_tmpText != null && _tmpText.isActiveAndEnabled)
            _tmpText.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: false);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Canvas.ForceUpdateCanvases();
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            UnityEditor.SceneView.RepaintAll();
        }
#endif
    }
    #endregion

    #region Override
    public override void ModifyMesh(VertexHelper vertexHelper)
    {
        if (!IsActive() || vertexHelper == null || _tmpText != null)
            return;

        UIGradientSettings settings = GetActiveSettings();
        if (settings.GradientType == UIGradientType.MultiStop)
            SubdivideAtGradientStops(vertexHelper, settings);
        if (_blendTarget != null && _blendTarget.Settings.GradientType == UIGradientType.MultiStop)
            SubdivideAtGradientStops(vertexHelper, _blendTarget.Settings);

        UIVertex vertex = default;
        int vertexCount = vertexHelper.currentVertCount;
        for (int i = 0; i < vertexCount; i++)
        {
            vertexHelper.PopulateUIVertex(ref vertex, i);
            vertex.color = ApplyGradient(vertex.position, vertex.color, settings);
            vertexHelper.SetUIVertex(vertex, i);
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        EnsureLocalSettingsBinding();
        CacheComponents();
        RefreshSubscription();
        SubscribeToText();
        Refresh();
    }

    protected override void OnDisable()
    {
        UnsubscribeFromPreset();
        _blendTarget = null;
        _presetBlend = 0f;
        UnsubscribeFromText();
        graphic?.SetVerticesDirty();
        base.OnDisable();
    }

    protected override void OnDestroy()
    {
        UnsubscribeFromPreset();
        UnsubscribeFromText();
        if (_localSettings != null)
            _localSettings.Changed -= Refresh;
        base.OnDestroy();
    }

    protected override void OnCanvasHierarchyChanged()
    {
        base.OnCanvasHierarchyChanged();
        CacheComponents();
        Refresh();
    }

    protected override void OnDidApplyAnimationProperties()
    {
        base.OnDidApplyAnimationProperties();
        Refresh();
    }
    #endregion

    #region Editor
#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        EnsureLocalSettingsBinding();
        _localSettings.NotifyChanged();
        CacheComponents();
        RefreshSubscription();

        if (isActiveAndEnabled)
            Refresh();
    }
#endif
    #endregion

    #region Private Methods
    private UIGradientSettings GetActiveSettings()
    {
        EnsureLocalSettingsBinding();
        return _source == UIGradientSource.Preset && _preset != null
            ? _preset.Settings
            : _localSettings;
    }

    private void EnsureLocalSettingsBinding()
    {
        _localSettings ??= new UIGradientSettings();
        _localSettings.Changed -= Refresh;
        _localSettings.Changed += Refresh;
    }

    private void CacheComponents()
    {
        _rectTransform ??= transform as RectTransform;

        TMP_Text currentText = GetComponent<TMP_Text>();
        if (_tmpText == currentText)
            return;

        UnsubscribeFromText();
        _tmpText = currentText;
        SubscribeToText();
    }

    private void RefreshSubscription()
    {
        UIGradientPreset nextPreset = _source == UIGradientSource.Preset ? _preset : null;
        UIGradientPreset nextBlendTarget = _source == UIGradientSource.Preset ? _blendTarget : null;
        if (_subscribedPreset == nextPreset && _subscribedBlendTarget == nextBlendTarget)
            return;

        UnsubscribeFromPreset();
        _subscribedPreset = nextPreset;
        if (_subscribedPreset != null)
            _subscribedPreset.Changed += Refresh;
        _subscribedBlendTarget = nextBlendTarget;
        if (_subscribedBlendTarget != null)
            _subscribedBlendTarget.Changed += Refresh;
    }

    private void UnsubscribeFromPreset()
    {
        if (_subscribedPreset != null)
            _subscribedPreset.Changed -= Refresh;
        _subscribedPreset = null;
        if (_subscribedBlendTarget != null)
            _subscribedBlendTarget.Changed -= Refresh;
        _subscribedBlendTarget = null;
    }

    private void SubscribeToText()
    {
        if (_tmpText == null)
            return;

        _tmpText.OnPreRenderText -= ModifyTextMesh;
        _tmpText.OnPreRenderText += ModifyTextMesh;
    }

    private void UnsubscribeFromText()
    {
        if (_tmpText != null)
            _tmpText.OnPreRenderText -= ModifyTextMesh;
    }

    private void ModifyTextMesh(TMP_TextInfo textInfo)
    {
        if (!IsActive() || textInfo == null)
            return;

        UIGradientSettings settings = GetActiveSettings();
        int materialCount = Mathf.Min(textInfo.materialCount, textInfo.meshInfo.Length);

        for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
        {
            TMP_MeshInfo meshInfo = textInfo.meshInfo[materialIndex];
            Vector3[] vertices = meshInfo.vertices;
            Color32[] colors = meshInfo.colors32;
            int vertexCount = Mathf.Min(meshInfo.vertexCount, Mathf.Min(vertices?.Length ?? 0, colors?.Length ?? 0));

            for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
                colors[vertexIndex] = ApplyGradient(vertices[vertexIndex], colors[vertexIndex], settings);
        }
    }

    private Color32 ApplyGradient(Vector3 localPosition, Color32 sourceColor, UIGradientSettings settings)
    {
        Vector2 normalizedPosition = NormalizePosition(localPosition);
        Color gradientColor = settings.Evaluate(normalizedPosition);
        Color result = settings.Blend(sourceColor, gradientColor, _blendMode);
        if (_blendTarget != null)
        {
            UIGradientSettings targetSettings = _blendTarget.Settings;
            Color target = targetSettings.Blend(sourceColor, targetSettings.Evaluate(normalizedPosition), _blendMode);
            result = Color.Lerp(result, target, _presetBlend);
        }
        if (!_includeAlpha)
            result.a = sourceColor.a / 255f;

        return result;
    }

    private Vector2 NormalizePosition(Vector3 localPosition)
    {
        Rect rect = _rectTransform != null ? _rectTransform.rect : default;
        float x = Mathf.Abs(rect.width) > Mathf.Epsilon
            ? (localPosition.x - rect.xMin) / rect.width
            : 0.5f;
        float y = Mathf.Abs(rect.height) > Mathf.Epsilon
            ? (localPosition.y - rect.yMin) / rect.height
            : 0.5f;
        return new Vector2(x, y);
    }

    private void SubdivideAtGradientStops(VertexHelper vertexHelper, UIGradientSettings settings)
    {
        settings.GetMultiStopTimes(_cutTimes);
        if (_cutTimes.Count == 0)
            return;

        _triangles.Clear();
        vertexHelper.GetUIVertexStream(_triangles);

        for (int cutIndex = 0; cutIndex < _cutTimes.Count; cutIndex++)
        {
            float cut = _cutTimes[cutIndex];
            _splitTriangles.Clear();

            for (int triangleIndex = 0; triangleIndex + 2 < _triangles.Count; triangleIndex += 3)
            {
                SplitTriangle(
                    _triangles[triangleIndex],
                    _triangles[triangleIndex + 1],
                    _triangles[triangleIndex + 2],
                    cut,
                    settings);
            }

            _triangles.Clear();
            _triangles.AddRange(_splitTriangles);
        }

        vertexHelper.Clear();
        vertexHelper.AddUIVertexTriangleStream(_triangles);
    }

    private void SplitTriangle(
        UIVertex first,
        UIVertex second,
        UIVertex third,
        float cut,
        UIGradientSettings settings)
    {
        var firstSample = CreateSample(first, settings);
        var secondSample = CreateSample(second, settings);
        var thirdSample = CreateSample(third, settings);

        bool allLower = firstSample.Coordinate <= cut
            && secondSample.Coordinate <= cut
            && thirdSample.Coordinate <= cut;
        bool allUpper = firstSample.Coordinate >= cut
            && secondSample.Coordinate >= cut
            && thirdSample.Coordinate >= cut;

        if (allLower || allUpper)
        {
            _splitTriangles.Add(first);
            _splitTriangles.Add(second);
            _splitTriangles.Add(third);
            return;
        }

        _lowerPolygon.Clear();
        _upperPolygon.Clear();
        ClipEdge(firstSample, secondSample, cut);
        ClipEdge(secondSample, thirdSample, cut);
        ClipEdge(thirdSample, firstSample, cut);
        Triangulate(_lowerPolygon);
        Triangulate(_upperPolygon);
    }

    private VertexSample CreateSample(UIVertex vertex, UIGradientSettings settings)
    {
        float coordinate = settings.GetLinearCoordinate(NormalizePosition(vertex.position));
        return new VertexSample(vertex, coordinate);
    }

    private void ClipEdge(VertexSample current, VertexSample next, float cut)
    {
        bool currentLower = current.Coordinate <= cut;
        bool nextLower = next.Coordinate <= cut;

        if (currentLower)
            _lowerPolygon.Add(current);
        else
            _upperPolygon.Add(current);

        if (currentLower == nextLower)
            return;

        float denominator = next.Coordinate - current.Coordinate;
        float interpolation = Mathf.Abs(denominator) > Mathf.Epsilon
            ? Mathf.Clamp01((cut - current.Coordinate) / denominator)
            : 0f;
        var intersection = new VertexSample(
            LerpVertex(current.Vertex, next.Vertex, interpolation),
            cut);
        _lowerPolygon.Add(intersection);
        _upperPolygon.Add(intersection);
    }

    private void Triangulate(List<VertexSample> polygon)
    {
        for (int i = 1; i + 1 < polygon.Count; i++)
        {
            _splitTriangles.Add(polygon[0].Vertex);
            _splitTriangles.Add(polygon[i].Vertex);
            _splitTriangles.Add(polygon[i + 1].Vertex);
        }
    }

    private static UIVertex LerpVertex(UIVertex first, UIVertex second, float interpolation)
    {
        return new UIVertex
        {
            position = Vector3.LerpUnclamped(first.position, second.position, interpolation),
            normal = Vector3.LerpUnclamped(first.normal, second.normal, interpolation),
            tangent = Vector4.LerpUnclamped(first.tangent, second.tangent, interpolation),
            color = Color32.LerpUnclamped(first.color, second.color, interpolation),
            uv0 = Vector4.LerpUnclamped(first.uv0, second.uv0, interpolation),
            uv1 = Vector4.LerpUnclamped(first.uv1, second.uv1, interpolation),
            uv2 = Vector4.LerpUnclamped(first.uv2, second.uv2, interpolation),
            uv3 = Vector4.LerpUnclamped(first.uv3, second.uv3, interpolation)
        };
    }

    private readonly struct VertexSample
    {
        public readonly UIVertex Vertex;
        public readonly float Coordinate;

        public VertexSample(UIVertex vertex, float coordinate)
        {
            Vertex = vertex;
            Coordinate = coordinate;
        }
    }
    #endregion
}
