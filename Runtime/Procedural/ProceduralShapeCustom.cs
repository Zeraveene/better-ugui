using System;
using System.Collections.Generic;
using Zeraveene.Shared.Inspector;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum ProceduralShapeHorizontalSliceMode
{
    EdgeBorders,
    CenterBand
}

[ExecuteAlways]
[AddComponentMenu("UI/Procedural Shape Custom")]
[RequireComponent(typeof(CanvasRenderer))]
[MovedFrom(true, sourceAssembly: "MaxsusWorks.ProceduralUI")]
public sealed class ProceduralShapeCustom : ProceduralShapeGraphic
{
    #region Inspector

    [FoldoutGroup("Normalized Points", expanded: false), SerializeField]
    private List<ProceduralShapePoint> _points = new List<ProceduralShapePoint>
    {
        new ProceduralShapePoint(new Vector2(0f, 0f)),
        new ProceduralShapePoint(new Vector2(0f, 1f)),
        new ProceduralShapePoint(new Vector2(1f, 1f)),
        new ProceduralShapePoint(new Vector2(1f, 0f))
    };

    [SerializeField, HideInInspector] private ProceduralShapeTemplate _pointTemplate;
    [SerializeField, HideInInspector] private bool _hasPointOverrides;
    [SerializeField, HideInInspector] private ProceduralShapeTemplate _pointTemplateState;

    [FoldoutGroup("Corners + Precision", expanded: false), SerializeField] private bool _uniformRadius = true;
    [FoldoutGroup("Corners + Precision"), SerializeField, Min(0f), ShowIf(nameof(_uniformRadius))] private float _cornerRadius;
    [FoldoutGroup("Corners + Precision"), SerializeField, Min(2)] private int _cornerSegments = 8;
    [FoldoutGroup("Corners + Precision"), SerializeField] private bool _snapPointsToPixels = true;

    [FoldoutGroup("Slicing", expanded: false), SerializeField] private bool _sliceHorizontal;
    [FoldoutGroup("Slicing"), SerializeField] private ProceduralShapeHorizontalSliceMode _horizontalSliceMode;
    [FoldoutGroup("Slicing"), SerializeField, Min(0f)] private float _horizontalCenterPixels;
    [FoldoutGroup("Slicing"), SerializeField] private bool _sliceVertical;
    [FoldoutGroup("Slicing"), SerializeField] private Vector2 _sliceReferenceSize;
    [FoldoutGroup("Slicing"), SerializeField, Min(0f)] private float _sliceLeftPixels;
    [FoldoutGroup("Slicing"), SerializeField, Min(0f)] private float _sliceRightPixels;
    [FoldoutGroup("Slicing"), SerializeField, Min(0f)] private float _sliceBottomPixels;
    [FoldoutGroup("Slicing"), SerializeField, Min(0f)] private float _sliceTopPixels;

    [Header("Scene Gizmos")]
    [SerializeField] private bool _showPointGizmos = true;
    [SerializeField] private Color _pointGizmoColor = new Color(0f, 0.85f, 1f, 1f);

    #endregion

    #region Runtime Data

    private readonly List<Vector2> _localPoints = new List<Vector2>(8);
    private readonly List<float> _radii = new List<float>(8);
    private ProceduralShapeTemplate _subscribedTemplate;

    #endregion

    #region Properties

    public IReadOnlyList<ProceduralShapePoint> Points => _points;
    public ProceduralShapeTemplate PointTemplate => _pointTemplate;
    public bool HasPointOverrides => _pointTemplate != null && _hasPointOverrides;

    public bool UniformRadius
    {
        get => _uniformRadius;
        set
        {
            _uniformRadius = value;
            GeometryWasChanged();
        }
    }

    public float CornerRadius
    {
        get => _cornerRadius;
        set
        {
            _cornerRadius = Mathf.Max(0f, value);
            _uniformRadius = true;
            GeometryWasChanged();
        }
    }

    public int CornerSegments
    {
        get => _cornerSegments;
        set
        {
            _cornerSegments = Mathf.Max(2, value);
            GeometryWasChanged();
        }
    }

    public bool SnapPointsToPixels
    {
        get => _snapPointsToPixels;
        set
        {
            _snapPointsToPixels = value;
            GeometryWasChanged();
        }
    }

    public bool ShowPointGizmos => _showPointGizmos;
    public Color PointGizmoColor => _pointGizmoColor;

    public bool SliceHorizontal
    {
        get => _sliceHorizontal;
        set
        {
            _sliceHorizontal = value;
            EnsureSliceReferenceSize();
            GeometryWasChanged();
        }
    }

    public bool SliceVertical
    {
        get => _sliceVertical;
        set
        {
            _sliceVertical = value;
            EnsureSliceReferenceSize();
            GeometryWasChanged();
        }
    }

    public ProceduralShapeHorizontalSliceMode HorizontalSliceMode
    {
        get => _horizontalSliceMode;
        set
        {
            _horizontalSliceMode = value;
            ClampSliceBorders();
            GeometryWasChanged();
        }
    }

    public float HorizontalCenterPixels
    {
        get => _horizontalCenterPixels;
        set
        {
            _horizontalCenterPixels = Mathf.Max(0f, value);
            ClampSliceBorders();
            GeometryWasChanged();
        }
    }

    public Vector2 SliceReferenceSize => _sliceReferenceSize;

    // Packed as left, bottom, right, top to match Unity's sprite border convention.
    public Vector4 SliceBorders
    {
        get => new Vector4(_sliceLeftPixels, _sliceBottomPixels, _sliceRightPixels, _sliceTopPixels);
        set
        {
            _sliceLeftPixels = Mathf.Max(0f, value.x);
            _sliceBottomPixels = Mathf.Max(0f, value.y);
            _sliceRightPixels = Mathf.Max(0f, value.z);
            _sliceTopPixels = Mathf.Max(0f, value.w);
            ClampSliceBorders();
            GeometryWasChanged();
        }
    }

    #endregion

    #region Public Methods

    public void CaptureSliceReferenceSize()
    {
        Rect rect = rectTransform.rect;
        _sliceReferenceSize = new Vector2(Mathf.Abs(rect.width), Mathf.Abs(rect.height));
        ClampSliceBorders();
        GeometryWasChanged();
    }

    public void SetSliceReferenceSize(Vector2 referenceSize)
    {
        _sliceReferenceSize = new Vector2(Mathf.Max(0f, referenceSize.x), Mathf.Max(0f, referenceSize.y));
        ClampSliceBorders();
        GeometryWasChanged();
    }

    public Vector2 NormalizedToLocalPoint(Vector2 normalized)
    {
        normalized = Clamp01(normalized);
        Rect rect = rectTransform.rect;
        float sourceWidth = Mathf.Max(0f, _sliceReferenceSize.x);
        float destinationWidth = Mathf.Max(0f, rect.width);
        float x = _sliceHorizontal && _horizontalSliceMode == ProceduralShapeHorizontalSliceMode.CenterBand
            ? ProceduralShapeSliceUtility.MapCenterBandAxis(
                normalized.x,
                sourceWidth,
                destinationWidth,
                _horizontalCenterPixels)
            : ProceduralShapeSliceUtility.MapSlicedAxis(
                normalized.x,
                sourceWidth,
                destinationWidth,
                _sliceLeftPixels,
                _sliceRightPixels,
                _sliceHorizontal);
        float y = ProceduralShapeSliceUtility.MapSlicedAxis(
            normalized.y,
            Mathf.Max(0f, _sliceReferenceSize.y),
            Mathf.Max(0f, rect.height),
            _sliceBottomPixels,
            _sliceTopPixels,
            _sliceVertical);
        return new Vector2(rect.xMin + x, rect.yMin + y);
    }

    public Vector2 LocalToNormalizedPoint(Vector2 local)
    {
        Rect rect = rectTransform.rect;
        float sourceWidth = Mathf.Max(0f, _sliceReferenceSize.x);
        float destinationWidth = Mathf.Max(0f, rect.width);
        float x = _sliceHorizontal && _horizontalSliceMode == ProceduralShapeHorizontalSliceMode.CenterBand
            ? ProceduralShapeSliceUtility.UnmapCenterBandAxis(
                local.x - rect.xMin,
                sourceWidth,
                destinationWidth,
                _horizontalCenterPixels)
            : ProceduralShapeSliceUtility.UnmapSlicedAxis(
                local.x - rect.xMin,
                sourceWidth,
                destinationWidth,
                _sliceLeftPixels,
                _sliceRightPixels,
                _sliceHorizontal);
        float y = ProceduralShapeSliceUtility.UnmapSlicedAxis(
            local.y - rect.yMin,
            Mathf.Max(0f, _sliceReferenceSize.y),
            Mathf.Max(0f, rect.height),
            _sliceBottomPixels,
            _sliceTopPixels,
            _sliceVertical);
        return Clamp01(new Vector2(x, y));
    }

    public Vector4 GetDisplayedSliceBorders()
    {
        Rect rect = rectTransform.rect;
        ProceduralShapeSliceUtility.GetScaledBorders(Mathf.Max(0f, rect.width), _sliceLeftPixels, _sliceRightPixels, out float left, out float right);
        ProceduralShapeSliceUtility.GetScaledBorders(Mathf.Max(0f, rect.height), _sliceBottomPixels, _sliceTopPixels, out float bottom, out float top);
        return new Vector4(left, bottom, right, top);
    }

    public float GetDisplayedHorizontalCenterPixels()
    {
        return Mathf.Min(Mathf.Max(0f, rectTransform.rect.width), _horizontalCenterPixels);
    }

    public void SetPoints(IList<ProceduralShapePoint> points)
    {
        if (points == null)
            throw new ArgumentNullException(nameof(points));
        if (points.Count < 3)
            throw new ArgumentException("A procedural shape requires at least three points.", nameof(points));
        BeginPointOverride();
        _points.Clear();
        for (int i = 0; i < points.Count; i++)
            _points.Add(new ProceduralShapePoint(points[i].Position, points[i].Radius));
        GeometryWasChanged();
    }

    public void SetPoints(IList<Vector2> points)
    {
        if (points == null)
            throw new ArgumentNullException(nameof(points));
        if (points.Count < 3)
            throw new ArgumentException("A procedural shape requires at least three points.", nameof(points));
        BeginPointOverride();
        _points.Clear();
        for (int i = 0; i < points.Count; i++)
            _points.Add(new ProceduralShapePoint(points[i]));
        GeometryWasChanged();
    }

    public void SetPoint(int index, Vector2 position)
    {
        ValidateIndex(index);
        BeginPointOverride();
        ProceduralShapePoint point = _points[index];
        point.Position = position;
        _points[index] = point;
        GeometryWasChanged();
    }

    public void SetPointRadius(int index, float radius)
    {
        ValidateIndex(index);
        BeginPointOverride();
        ProceduralShapePoint point = _points[index];
        point.Radius = radius;
        _points[index] = point;
        _uniformRadius = false;
        GeometryWasChanged();
    }

    public void InsertPoint(int index, ProceduralShapePoint point)
    {
        if (index < 0 || index > _points.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        BeginPointOverride();
        _points.Insert(index, new ProceduralShapePoint(point.Position, point.Radius));
        GeometryWasChanged();
    }

    public void RemovePoint(int index)
    {
        ValidateIndex(index);
        if (_points.Count <= 3)
            throw new InvalidOperationException("A procedural shape requires at least three points.");
        BeginPointOverride();
        _points.RemoveAt(index);
        GeometryWasChanged();
    }

    public void SetPointTemplate(ProceduralShapeTemplate pointTemplate)
    {
        if (_pointTemplate == pointTemplate)
            return;

        UnsubscribeFromTemplate();
        _pointTemplate = pointTemplate;
        _pointTemplateState = pointTemplate;
        _hasPointOverrides = false;
        SubscribeToTemplate();
        if (_pointTemplate != null)
            CopyPointsFromTemplate();
        GeometryWasChanged();
    }

    public void MarkPointOverrides()
    {
        if (_pointTemplate == null || _hasPointOverrides)
            return;
        _hasPointOverrides = true;
        GeometryWasChanged();
    }

    public void ResetPointsFromTemplate()
    {
        if (_pointTemplate == null)
            return;
        _hasPointOverrides = false;
        CopyPointsFromTemplate();
        GeometryWasChanged();
    }

    public void UpdatePointTemplate()
    {
        if (_pointTemplate == null)
            throw new InvalidOperationException("No procedural shape template is assigned.");

        var positions = new List<Vector2>(_points.Count);
        for (int i = 0; i < _points.Count; i++)
            positions.Add(_points[i].Position);
        _pointTemplate.SetPoints(positions);
        _hasPointOverrides = false;
        CopyPointsFromTemplate();
        GeometryWasChanged();
    }

    public void FlipHorizontal()
    {
        BeginPointOverride();
        for (int i = 0; i < _points.Count; i++)
        {
            ProceduralShapePoint point = _points[i];
            point.Position = new Vector2(1f - point.Position.x, point.Position.y);
            _points[i] = point;
        }

        Swap(ref _sliceLeftPixels, ref _sliceRightPixels);
        GeometryWasChanged();
    }

    public void FlipVertical()
    {
        BeginPointOverride();
        for (int i = 0; i < _points.Count; i++)
        {
            ProceduralShapePoint point = _points[i];
            point.Position = new Vector2(point.Position.x, 1f - point.Position.y);
            _points[i] = point;
        }

        Swap(ref _sliceBottomPixels, ref _sliceTopPixels);
        GeometryWasChanged();
    }

    #endregion

    #region Override

    protected override void BuildRoundedPerimeter(List<Vector2> output)
    {
        output.Clear();
        if (_points == null || _points.Count < 3)
            return;
        _localPoints.Clear();
        _radii.Clear();
        for (int i = 0; i < _points.Count; i++)
        {
            Vector2 local = NormalizedToLocalPoint(_points[i].Position);
            _localPoints.Add(_snapPointsToPixels ? PixelAdjustedPoint(local) : local);
            _radii.Add(_uniformRadius ? _cornerRadius : _points[i].Radius);
        }
        BuildRoundedPolygon(_localPoints, _radii, _cornerSegments, output);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        RefreshTemplateSubscription();
        if (_pointTemplate != null && !_hasPointOverrides)
            CopyPointsFromTemplate();
    }

    protected override void OnDisable()
    {
        UnsubscribeFromTemplate();
        base.OnDisable();
    }

    #endregion

    #region Private Methods

#if UNITY_EDITOR
    [ButtonGroup("Flip"), Button("Flip Horizontal"), PropertyOrder(100)]
    private void FlipHorizontalInInspector()
    {
        Undo.RecordObject(this, "Flip Procedural Shape Custom Horizontal");
        FlipHorizontal();
        RecordInspectorChange();
    }

    [ButtonGroup("Flip"), Button("Flip Vertical"), PropertyOrder(100)]
    private void FlipVerticalInInspector()
    {
        Undo.RecordObject(this, "Flip Procedural Shape Custom Vertical");
        FlipVertical();
        RecordInspectorChange();
    }

    private void RecordInspectorChange()
    {
        EditorUtility.SetDirty(this);
        PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        SceneView.RepaintAll();
    }
#endif

    private static void Swap(ref float first, ref float second)
    {
        float value = first;
        first = second;
        second = value;
    }

    private void ValidateIndex(int index)
    {
        if (index < 0 || index >= _points.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
    }

    private void BeginPointOverride()
    {
        if (_pointTemplate != null && !_hasPointOverrides)
        {
            CopyPointsFromTemplate();
            _hasPointOverrides = true;
        }
    }

    private void RefreshTemplateSubscription()
    {
        if (_subscribedTemplate != _pointTemplate)
        {
            UnsubscribeFromTemplate();
            SubscribeToTemplate();
        }

        if (_pointTemplateState == _pointTemplate)
            return;

        _pointTemplateState = _pointTemplate;
        _hasPointOverrides = false;
        if (_pointTemplate != null)
            CopyPointsFromTemplate();
    }

    private void SubscribeToTemplate()
    {
        _subscribedTemplate = _pointTemplate;
        if (_subscribedTemplate != null)
            _subscribedTemplate.Changed += HandleTemplateChanged;
    }

    private void UnsubscribeFromTemplate()
    {
        if (_subscribedTemplate != null)
            _subscribedTemplate.Changed -= HandleTemplateChanged;
        _subscribedTemplate = null;
    }

    private void HandleTemplateChanged()
    {
        if (_hasPointOverrides)
            return;
        CopyPointsFromTemplate();
        GeometryWasChanged();
    }

    private void CopyPointsFromTemplate()
    {
        if (_pointTemplate == null)
            return;

        IReadOnlyList<Vector2> positions = _pointTemplate.NormalizedPoints;
        _points.Clear();
        for (int i = 0; i < positions.Count; i++)
            _points.Add(new ProceduralShapePoint(positions[i]));
    }

    private void EnsureSliceReferenceSize()
    {
        Rect rect = rectTransform.rect;
        if (_sliceReferenceSize.x <= 0f)
            _sliceReferenceSize.x = Mathf.Abs(rect.width);
        if (_sliceReferenceSize.y <= 0f)
            _sliceReferenceSize.y = Mathf.Abs(rect.height);
        ClampSliceBorders();
    }

    private void ClampSliceBorders()
    {
        _sliceLeftPixels = Mathf.Max(0f, _sliceLeftPixels);
        _sliceRightPixels = Mathf.Max(0f, _sliceRightPixels);
        _sliceBottomPixels = Mathf.Max(0f, _sliceBottomPixels);
        _sliceTopPixels = Mathf.Max(0f, _sliceTopPixels);
        _horizontalCenterPixels = Mathf.Clamp(_horizontalCenterPixels, 0f, Mathf.Max(0f, _sliceReferenceSize.x));
        ProceduralShapeSliceUtility.ClampBorderPair(ref _sliceLeftPixels, ref _sliceRightPixels, _sliceReferenceSize.x);
        ProceduralShapeSliceUtility.ClampBorderPair(ref _sliceBottomPixels, ref _sliceTopPixels, _sliceReferenceSize.y);
    }

    #endregion

    #region Override

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        RefreshTemplateSubscription();
        if (_points == null)
            _points = new List<ProceduralShapePoint>();
        while (_points.Count < 3)
            _points.Add(DefaultPoint(_points.Count));
        for (int i = 0; i < _points.Count; i++)
            _points[i] = new ProceduralShapePoint(_points[i].Position, _points[i].Radius);
        _cornerRadius = Mathf.Max(0f, _cornerRadius);
        _cornerSegments = Mathf.Max(2, _cornerSegments);
        EnsureSliceReferenceSize();
        ClampSliceBorders();
        base.OnValidate();
    }
#endif

    #endregion

    #region Private Methods

    private static ProceduralShapePoint DefaultPoint(int index)
    {
        switch (index)
        {
            case 0: return new ProceduralShapePoint(new Vector2(0f, 0f));
            case 1: return new ProceduralShapePoint(new Vector2(0.5f, 1f));
            default: return new ProceduralShapePoint(new Vector2(1f, 0f));
        }
    }
    #endregion
}
