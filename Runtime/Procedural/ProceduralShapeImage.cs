using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;
using UnityEngine.Scripting.APIUpdating;

[ExecuteAlways]
[AddComponentMenu("UI/Procedural Shape Image")]
[RequireComponent(typeof(CanvasRenderer))]
[MovedFrom(true, sourceAssembly: "MaxsusWorks.ProceduralUI")]
public sealed class ProceduralShapeImage : Image
{
    [SerializeField]
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

    [SerializeField] private bool _uniformRadius = true;
    [SerializeField, Min(0f)] private float _cornerRadius;
    [SerializeField, Min(2)] private int _cornerSegments = 8;
    [SerializeField] private bool _snapPointsToPixels = true;
    [SerializeField, Min(0f)] private float _borderWidth;
    [SerializeField, Min(0f)] private float _falloffDistance;
    [SerializeField]
    [Tooltip("Canvas Units scales with the UI like ProceduralImage. Screen Pixels keeps an absolute on-screen size.")]
    private ProceduralShapeMeasurementMode _measurementMode;

    [SerializeField] private bool _sliceHorizontal;
    [SerializeField] private ProceduralShapeHorizontalSliceMode _horizontalSliceMode;
    [SerializeField, Min(0f)] private float _horizontalCenterPixels;
    [SerializeField] private bool _sliceVertical;
    [SerializeField] private Vector2 _sliceReferenceSize;
    [SerializeField, Min(0f)] private float _sliceLeftPixels;
    [SerializeField, Min(0f)] private float _sliceRightPixels;
    [SerializeField, Min(0f)] private float _sliceBottomPixels;
    [SerializeField, Min(0f)] private float _sliceTopPixels;

    [Header("Scene Gizmos")]
    [SerializeField] private bool _showPointGizmos = true;
    [SerializeField] private Color _pointGizmoColor = new Color(0f, 0.85f, 1f, 1f);

    private readonly List<Vector2> _localPoints = new List<Vector2>(16);
    private readonly List<float> _radii = new List<float>(16);
    private readonly List<Vector2> _perimeter = new List<Vector2>(64);
    private readonly List<Vector2> _outer = new List<Vector2>(64);
    private readonly List<Vector2> _inner = new List<Vector2>(64);
    private readonly List<Vector2> _outerFade = new List<Vector2>(64);
    private readonly List<Vector2> _innerFade = new List<Vector2>(64);
    private readonly List<int> _indices = new List<int>(64);
    private readonly List<int> _triangles = new List<int>(192);
    private readonly VertexHelper _unclippedMesh = new VertexHelper();
    private readonly VertexHelper _imageFillMesh = new VertexHelper();
    private readonly List<UIVertex> _sourceTriangles = new List<UIVertex>(384);
    private readonly List<UIVertex> _maskTriangles = new List<UIVertex>(24);
    private readonly List<UIVertex> _clippedTriangles = new List<UIVertex>(384);
    private readonly List<UIVertex> _clipInput = new List<UIVertex>(8);
    private readonly List<UIVertex> _clipOutput = new List<UIVertex>(8);
    private ProceduralShapeTemplate _subscribedTemplate;

    public event Action GeometryChanged;

    public IReadOnlyList<ProceduralShapePoint> Points => _points;
    public ProceduralShapeTemplate PointTemplate => _pointTemplate;
    public bool HasPointOverrides => _pointTemplate != null && _hasPointOverrides;
    public bool ShowPointGizmos => _showPointGizmos;
    public Color PointGizmoColor => _pointGizmoColor;

    public bool UniformRadius
    {
        get => _uniformRadius;
        set { _uniformRadius = value; GeometryWasChanged(); }
    }

    public float CornerRadius
    {
        get => _cornerRadius;
        set { _cornerRadius = Mathf.Max(0f, value); _uniformRadius = true; GeometryWasChanged(); }
    }

    public int CornerSegments
    {
        get => _cornerSegments;
        set { _cornerSegments = Mathf.Max(2, value); GeometryWasChanged(); }
    }

    public bool SnapPointsToPixels
    {
        get => _snapPointsToPixels;
        set { _snapPointsToPixels = value; GeometryWasChanged(); }
    }

    public float BorderWidth
    {
        get => _borderWidth;
        set { _borderWidth = Mathf.Max(0f, value); GeometryWasChanged(); }
    }

    public float FalloffDistance
    {
        get => _falloffDistance;
        set { _falloffDistance = Mathf.Max(0f, value); GeometryWasChanged(); }
    }

    public ProceduralShapeMeasurementMode MeasurementMode
    {
        get => _measurementMode;
        set
        {
            if (_measurementMode == value) return;
            _measurementMode = value;
            GeometryWasChanged();
        }
    }

    public bool SliceHorizontal
    {
        get => _sliceHorizontal;
        set { _sliceHorizontal = value; EnsureSliceReferenceSize(); GeometryWasChanged(); }
    }

    public bool SliceVertical
    {
        get => _sliceVertical;
        set { _sliceVertical = value; EnsureSliceReferenceSize(); GeometryWasChanged(); }
    }

    public ProceduralShapeHorizontalSliceMode HorizontalSliceMode
    {
        get => _horizontalSliceMode;
        set { _horizontalSliceMode = value; ClampSliceBorders(); GeometryWasChanged(); }
    }

    public float HorizontalCenterPixels
    {
        get => _horizontalCenterPixels;
        set { _horizontalCenterPixels = Mathf.Max(0f, value); ClampSliceBorders(); GeometryWasChanged(); }
    }

    public Vector2 SliceReferenceSize => _sliceReferenceSize;

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

    public void CaptureSliceReferenceSize()
    {
        Rect rect = GetDrawingRect();
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
        Rect rect = GetDrawingRect();
        float sourceWidth = Mathf.Max(0f, _sliceReferenceSize.x);
        float destinationWidth = Mathf.Max(0f, rect.width);
        float x = _sliceHorizontal && _horizontalSliceMode == ProceduralShapeHorizontalSliceMode.CenterBand
            ? ProceduralShapeSliceUtility.MapCenterBandAxis(normalized.x, sourceWidth, destinationWidth, _horizontalCenterPixels)
            : ProceduralShapeSliceUtility.MapSlicedAxis(normalized.x, sourceWidth, destinationWidth, _sliceLeftPixels, _sliceRightPixels, _sliceHorizontal);
        float y = ProceduralShapeSliceUtility.MapSlicedAxis(
            normalized.y, Mathf.Max(0f, _sliceReferenceSize.y), Mathf.Max(0f, rect.height),
            _sliceBottomPixels, _sliceTopPixels, _sliceVertical);
        return new Vector2(rect.xMin + x, rect.yMin + y);
    }

    public Vector2 LocalToNormalizedPoint(Vector2 local)
    {
        Rect rect = GetDrawingRect();
        float sourceWidth = Mathf.Max(0f, _sliceReferenceSize.x);
        float destinationWidth = Mathf.Max(0f, rect.width);
        float x = _sliceHorizontal && _horizontalSliceMode == ProceduralShapeHorizontalSliceMode.CenterBand
            ? ProceduralShapeSliceUtility.UnmapCenterBandAxis(local.x - rect.xMin, sourceWidth, destinationWidth, _horizontalCenterPixels)
            : ProceduralShapeSliceUtility.UnmapSlicedAxis(local.x - rect.xMin, sourceWidth, destinationWidth, _sliceLeftPixels, _sliceRightPixels, _sliceHorizontal);
        float y = ProceduralShapeSliceUtility.UnmapSlicedAxis(
            local.y - rect.yMin, Mathf.Max(0f, _sliceReferenceSize.y), Mathf.Max(0f, rect.height),
            _sliceBottomPixels, _sliceTopPixels, _sliceVertical);
        return Clamp01(new Vector2(x, y));
    }

    public Vector4 GetDisplayedSliceBorders()
    {
        Rect rect = GetDrawingRect();
        ProceduralShapeSliceUtility.GetScaledBorders(Mathf.Max(0f, rect.width), _sliceLeftPixels, _sliceRightPixels, out float left, out float right);
        ProceduralShapeSliceUtility.GetScaledBorders(Mathf.Max(0f, rect.height), _sliceBottomPixels, _sliceTopPixels, out float bottom, out float top);
        return new Vector4(left, bottom, right, top);
    }

    public float GetDisplayedHorizontalCenterPixels() => Mathf.Min(Mathf.Max(0f, GetDrawingRect().width), _horizontalCenterPixels);

    public void SetPoints(IList<ProceduralShapePoint> points)
    {
        if (points == null) throw new ArgumentNullException(nameof(points));
        if (points.Count < 3) throw new ArgumentException("A procedural shape requires at least three points.", nameof(points));
        BeginPointOverride();
        _points.Clear();
        for (int i = 0; i < points.Count; i++) _points.Add(new ProceduralShapePoint(points[i].Position, points[i].Radius));
        GeometryWasChanged();
    }

    public void SetPoints(IList<Vector2> points)
    {
        if (points == null) throw new ArgumentNullException(nameof(points));
        if (points.Count < 3) throw new ArgumentException("A procedural shape requires at least three points.", nameof(points));
        BeginPointOverride();
        _points.Clear();
        for (int i = 0; i < points.Count; i++) _points.Add(new ProceduralShapePoint(points[i]));
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
        if (index < 0 || index > _points.Count) throw new ArgumentOutOfRangeException(nameof(index));
        BeginPointOverride();
        _points.Insert(index, new ProceduralShapePoint(point.Position, point.Radius));
        GeometryWasChanged();
    }

    public void RemovePoint(int index)
    {
        ValidateIndex(index);
        if (_points.Count <= 3) throw new InvalidOperationException("A procedural shape requires at least three points.");
        BeginPointOverride();
        _points.RemoveAt(index);
        GeometryWasChanged();
    }

    public void SetPointTemplate(ProceduralShapeTemplate pointTemplate)
    {
        if (_pointTemplate == pointTemplate) return;
        UnsubscribeFromTemplate();
        _pointTemplate = pointTemplate;
        _pointTemplateState = pointTemplate;
        _hasPointOverrides = false;
        SubscribeToTemplate();
        if (_pointTemplate != null) CopyPointsFromTemplate();
        GeometryWasChanged();
    }

    public void MarkPointOverrides()
    {
        if (_pointTemplate == null || _hasPointOverrides) return;
        _hasPointOverrides = true;
        GeometryWasChanged();
    }

    public void ResetPointsFromTemplate()
    {
        if (_pointTemplate == null) return;
        _hasPointOverrides = false;
        CopyPointsFromTemplate();
        GeometryWasChanged();
    }

    public void UpdatePointTemplate()
    {
        if (_pointTemplate == null) throw new InvalidOperationException("No procedural shape template is assigned.");
        var positions = new List<Vector2>(_points.Count);
        for (int i = 0; i < _points.Count; i++) positions.Add(_points[i].Position);
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

    public void GetRoundedPerimeterLocal(List<Vector2> output)
    {
        if (output == null) throw new ArgumentNullException(nameof(output));
        BuildRoundedPerimeter(output);
        ProceduralShapeGeometryUtility.Clean(output);
        if (!ProceduralShapeGeometryUtility.IsSimple(output)) output.Clear();
    }

    public override bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (!base.IsRaycastLocationValid(screenPoint, eventCamera)) return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out Vector2 point)) return false;
        GetRoundedPerimeterLocal(_perimeter);
        if (!ProceduralShapeGeometryUtility.Contains(point, _perimeter)) return false;
        float border = LocalPixels(_borderWidth);
        if (border <= 0f) return true;
        ProceduralShapeGeometryUtility.Offset(_perimeter, border, _inner);
        return !ProceduralShapeGeometryUtility.IsUsableInset(_perimeter, _inner) ||
               !ProceduralShapeGeometryUtility.Contains(point, _inner);
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        if (type == Type.Filled && fillAmount < 0.001f) return;
        GetRoundedPerimeterLocal(_perimeter);
        if (_perimeter.Count < 3) return;

        Rect drawingRect = GetDrawingRect();
        VertexHelper target = type == Type.Filled && fillAmount < 0.999f ? _unclippedMesh : vertexHelper;
        if (target != vertexHelper) target.Clear();
        float border = LocalPixels(_borderWidth);
        if (border > 0f) PopulateBorderMesh(target, drawingRect, border, LocalPixels(_falloffDistance));
        else PopulateFilledMesh(target, drawingRect, LocalPixels(_falloffDistance));
        if (target != vertexHelper) ApplyImageFill(target, vertexHelper);
    }

    protected override void OnEnable()
    {
        NormalizeImageType();
        base.OnEnable();
        RegisterDirtyVerticesCallback(HandleImageVerticesDirty);
        RefreshTemplateSubscription();
        if (_pointTemplate != null && !_hasPointOverrides) CopyPointsFromTemplate();
    }

    protected override void OnDisable()
    {
        UnregisterDirtyVerticesCallback(HandleImageVerticesDirty);
        UnsubscribeFromTemplate();
        base.OnDisable();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        GeometryWasChanged();
    }

    protected override void OnDestroy()
    {
        _unclippedMesh.Dispose();
        _imageFillMesh.Dispose();
        base.OnDestroy();
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        NormalizeImageType();
        RefreshTemplateSubscription();
        _points ??= new List<ProceduralShapePoint>();
        while (_points.Count < 3) _points.Add(DefaultPoint(_points.Count));
        for (int i = 0; i < _points.Count; i++) _points[i] = new ProceduralShapePoint(_points[i].Position, _points[i].Radius);
        _cornerRadius = Mathf.Max(0f, _cornerRadius);
        _cornerSegments = Mathf.Max(2, _cornerSegments);
        _borderWidth = Mathf.Max(0f, _borderWidth);
        _falloffDistance = Mathf.Max(0f, _falloffDistance);
        EnsureSliceReferenceSize();
        ClampSliceBorders();
        base.OnValidate();
        GeometryWasChanged();
    }
#endif

    private void BuildRoundedPerimeter(List<Vector2> output)
    {
        output.Clear();
        if (_points == null || _points.Count < 3) return;
        _localPoints.Clear();
        _radii.Clear();
        for (int i = 0; i < _points.Count; i++)
        {
            Vector2 local = NormalizedToLocalPoint(_points[i].Position);
            _localPoints.Add(_snapPointsToPixels ? PixelAdjustPoint(local) : local);
            _radii.Add(_uniformRadius ? _cornerRadius : _points[i].Radius);
        }
        ProceduralShapeGeometryUtility.BuildRoundedPolygon(_localPoints, _radii, _cornerSegments, output);
    }

    private void PopulateFilledMesh(VertexHelper vertexHelper, Rect rect, float falloff)
    {
        if (falloff <= 0f)
        {
            AddFill(vertexHelper, _perimeter, rect, color);
            return;
        }
        ProceduralShapeGeometryUtility.Offset(_perimeter, -falloff * 0.5f, _outer);
        ProceduralShapeGeometryUtility.Offset(_perimeter, falloff * 0.5f, _inner);
        if (!ProceduralShapeGeometryUtility.IsSimple(_inner) || !AddFill(vertexHelper, _inner, rect, color))
        {
            vertexHelper.Clear();
            if (!AddFill(vertexHelper, _perimeter, rect, color)) return;
            _inner.Clear();
            _inner.AddRange(_perimeter);
            ProceduralShapeGeometryUtility.Offset(_perimeter, -falloff, _outer);
        }
        Color32 opaque = color, transparent = opaque;
        transparent.a = 0;
        AddRing(vertexHelper, _outer, _inner, transparent, opaque, rect);
    }

    private void PopulateBorderMesh(VertexHelper vertexHelper, Rect rect, float border, float falloff)
    {
        ProceduralShapeGeometryUtility.Offset(_perimeter, border, _inner);
        if (!ProceduralShapeGeometryUtility.IsUsableInset(_perimeter, _inner))
        {
            PopulateFilledMesh(vertexHelper, rect, falloff);
            return;
        }
        Color32 opaque = color;
        if (falloff <= 0f)
        {
            AddRing(vertexHelper, _perimeter, _inner, opaque, opaque, rect);
            return;
        }
        Color32 transparent = opaque;
        transparent.a = 0;
        float halfFalloff = Mathf.Min(falloff * 0.5f, border * 0.5f);
        ProceduralShapeGeometryUtility.Offset(_perimeter, -halfFalloff, _outerFade);
        ProceduralShapeGeometryUtility.Offset(_perimeter, halfFalloff, _outer);
        ProceduralShapeGeometryUtility.Offset(_perimeter, border - halfFalloff, _inner);
        ProceduralShapeGeometryUtility.Offset(_perimeter, border + halfFalloff, _innerFade);
        if (!ProceduralShapeGeometryUtility.IsUsableInset(_perimeter, _inner) ||
            !ProceduralShapeGeometryUtility.IsUsableInset(_perimeter, _innerFade))
        {
            vertexHelper.Clear();
            PopulateFilledMesh(vertexHelper, rect, falloff);
            return;
        }
        AddRing(vertexHelper, _outerFade, _outer, transparent, opaque, rect);
        AddRing(vertexHelper, _outer, _inner, opaque, opaque, rect);
        AddRing(vertexHelper, _inner, _innerFade, opaque, transparent, rect);
    }

    private bool AddFill(VertexHelper vertexHelper, List<Vector2> points, Rect rect, Color32 vertexColor)
    {
        if (!ProceduralShapeGeometryUtility.Triangulate(points, _triangles, _indices)) return false;
        int start = vertexHelper.currentVertCount;
        for (int i = 0; i < points.Count; i++) AddVertex(vertexHelper, points[i], vertexColor, rect);
        for (int i = 0; i < _triangles.Count; i += 3)
            vertexHelper.AddTriangle(start + _triangles[i], start + _triangles[i + 1], start + _triangles[i + 2]);
        return true;
    }

    private void AddRing(VertexHelper vertexHelper, List<Vector2> outer, List<Vector2> inner, Color32 outerColor, Color32 innerColor, Rect rect)
    {
        int count = Mathf.Min(outer.Count, inner.Count);
        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            int start = vertexHelper.currentVertCount;
            AddVertex(vertexHelper, outer[i], outerColor, rect);
            AddVertex(vertexHelper, outer[next], outerColor, rect);
            AddVertex(vertexHelper, inner[next], innerColor, rect);
            AddVertex(vertexHelper, inner[i], innerColor, rect);
            vertexHelper.AddTriangle(start, start + 1, start + 2);
            vertexHelper.AddTriangle(start, start + 2, start + 3);
        }
    }

    private void AddVertex(VertexHelper vertexHelper, Vector2 position, Color32 vertexColor, Rect rect)
    {
        Sprite active = overrideSprite != null ? overrideSprite : sprite;
        Vector4 uv = active != null ? DataUtility.GetOuterUV(active) : new Vector4(0f, 0f, 1f, 1f);
        float normalizedX = Mathf.Abs(rect.width) > Mathf.Epsilon ? (position.x - rect.xMin) / rect.width : 0.5f;
        float normalizedY = Mathf.Abs(rect.height) > Mathf.Epsilon ? (position.y - rect.yMin) / rect.height : 0.5f;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = vertexColor;
        vertex.uv0 = new Vector2(Mathf.LerpUnclamped(uv.x, uv.z, normalizedX), Mathf.LerpUnclamped(uv.y, uv.w, normalizedY));
        vertexHelper.AddVert(vertex);
    }

    private void ApplyImageFill(VertexHelper source, VertexHelper destination)
    {
        _imageFillMesh.Clear();
        base.OnPopulateMesh(_imageFillMesh);
        _sourceTriangles.Clear();
        _maskTriangles.Clear();
        _clippedTriangles.Clear();
        source.GetUIVertexStream(_sourceTriangles);
        _imageFillMesh.GetUIVertexStream(_maskTriangles);
        for (int sourceIndex = 0; sourceIndex + 2 < _sourceTriangles.Count; sourceIndex += 3)
        for (int maskIndex = 0; maskIndex + 2 < _maskTriangles.Count; maskIndex += 3)
            ClipTriangle(
                _sourceTriangles[sourceIndex], _sourceTriangles[sourceIndex + 1], _sourceTriangles[sourceIndex + 2],
                _maskTriangles[maskIndex].position, _maskTriangles[maskIndex + 1].position, _maskTriangles[maskIndex + 2].position);
        destination.AddUIVertexTriangleStream(_clippedTriangles);
    }

    private void ClipTriangle(UIVertex a, UIVertex b, UIVertex c, Vector2 maskA, Vector2 maskB, Vector2 maskC)
    {
        _clipInput.Clear();
        _clipInput.Add(a); _clipInput.Add(b); _clipInput.Add(c);
        bool ccw = ProceduralShapeGeometryUtility.Cross(maskB - maskA, maskC - maskA) > 0f;
        ClipAgainstEdge(maskA, maskB, ccw);
        ClipAgainstEdge(maskB, maskC, ccw);
        ClipAgainstEdge(maskC, maskA, ccw);
        for (int i = 1; i + 1 < _clipInput.Count; i++)
        {
            _clippedTriangles.Add(_clipInput[0]);
            _clippedTriangles.Add(_clipInput[i]);
            _clippedTriangles.Add(_clipInput[i + 1]);
        }
    }

    private void ClipAgainstEdge(Vector2 edgeA, Vector2 edgeB, bool ccw)
    {
        _clipOutput.Clear();
        if (_clipInput.Count == 0) return;
        UIVertex previous = _clipInput[_clipInput.Count - 1];
        float previousDistance = SignedEdgeDistance(previous.position, edgeA, edgeB, ccw);
        for (int i = 0; i < _clipInput.Count; i++)
        {
            UIVertex current = _clipInput[i];
            float currentDistance = SignedEdgeDistance(current.position, edgeA, edgeB, ccw);
            bool previousInside = previousDistance >= -0.00001f;
            bool currentInside = currentDistance >= -0.00001f;
            if (previousInside != currentInside)
            {
                float interpolation = previousDistance / (previousDistance - currentDistance);
                _clipOutput.Add(LerpVertex(previous, current, interpolation));
            }
            if (currentInside) _clipOutput.Add(current);
            previous = current;
            previousDistance = currentDistance;
        }
        _clipInput.Clear();
        _clipInput.AddRange(_clipOutput);
    }

    private static float SignedEdgeDistance(Vector3 point, Vector2 a, Vector2 b, bool ccw)
    {
        float distance = ProceduralShapeGeometryUtility.Cross(b - a, (Vector2)point - a);
        return ccw ? distance : -distance;
    }

    private static UIVertex LerpVertex(UIVertex a, UIVertex b, float interpolation)
    {
        UIVertex result = a;
        result.position = Vector3.LerpUnclamped(a.position, b.position, interpolation);
        result.normal = Vector3.LerpUnclamped(a.normal, b.normal, interpolation);
        result.tangent = Vector4.LerpUnclamped(a.tangent, b.tangent, interpolation);
        result.color = Color32.LerpUnclamped(a.color, b.color, interpolation);
        result.uv0 = Vector4.LerpUnclamped(a.uv0, b.uv0, interpolation);
        result.uv1 = Vector4.LerpUnclamped(a.uv1, b.uv1, interpolation);
        result.uv2 = Vector4.LerpUnclamped(a.uv2, b.uv2, interpolation);
        result.uv3 = Vector4.LerpUnclamped(a.uv3, b.uv3, interpolation);
        return result;
    }

    private Rect GetDrawingRect()
    {
        Rect rect = GetPixelAdjustedRect();
        Sprite active = overrideSprite != null ? overrideSprite : sprite;
        if (!preserveAspect || active == null || rect.width <= 0f || rect.height <= 0f) return rect;
        float spriteRatio = active.rect.width / Mathf.Max(active.rect.height, Mathf.Epsilon);
        float rectRatio = rect.width / rect.height;
        if (spriteRatio > rectRatio)
        {
            float oldHeight = rect.height;
            rect.height = rect.width / spriteRatio;
            rect.y += (oldHeight - rect.height) * rectTransform.pivot.y;
        }
        else
        {
            float oldWidth = rect.width;
            rect.width = rect.height * spriteRatio;
            rect.x += (oldWidth - rect.width) * rectTransform.pivot.x;
        }
        return rect;
    }

    private float LocalPixels(float pixels)
    {
        if (_measurementMode == ProceduralShapeMeasurementMode.CanvasUnits) return pixels;
        Canvas root = canvas != null ? canvas.rootCanvas : null;
        return root == null || root.renderMode == RenderMode.WorldSpace ? pixels : pixels / Mathf.Max(root.scaleFactor, 0.0001f);
    }

    private void NormalizeImageType()
    {
        if (type != Type.Simple && type != Type.Filled) type = Type.Simple;
    }

    private void GeometryWasChanged()
    {
        SetVerticesDirty();
        SetMaterialDirty();
    }

    private void HandleImageVerticesDirty() => GeometryChanged?.Invoke();

    private void ValidateIndex(int index)
    {
        if (index < 0 || index >= _points.Count) throw new ArgumentOutOfRangeException(nameof(index));
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
        if (_pointTemplateState == _pointTemplate) return;
        _pointTemplateState = _pointTemplate;
        _hasPointOverrides = false;
        if (_pointTemplate != null) CopyPointsFromTemplate();
    }

    private void SubscribeToTemplate()
    {
        _subscribedTemplate = _pointTemplate;
        if (_subscribedTemplate != null) _subscribedTemplate.Changed += HandleTemplateChanged;
    }

    private void UnsubscribeFromTemplate()
    {
        if (_subscribedTemplate != null) _subscribedTemplate.Changed -= HandleTemplateChanged;
        _subscribedTemplate = null;
    }

    private void HandleTemplateChanged()
    {
        if (_hasPointOverrides) return;
        CopyPointsFromTemplate();
        GeometryWasChanged();
    }

    private void CopyPointsFromTemplate()
    {
        if (_pointTemplate == null) return;
        IReadOnlyList<Vector2> positions = _pointTemplate.NormalizedPoints;
        _points.Clear();
        for (int i = 0; i < positions.Count; i++) _points.Add(new ProceduralShapePoint(positions[i]));
    }

    private void EnsureSliceReferenceSize()
    {
        Rect rect = GetDrawingRect();
        if (_sliceReferenceSize.x <= 0f) _sliceReferenceSize.x = Mathf.Abs(rect.width);
        if (_sliceReferenceSize.y <= 0f) _sliceReferenceSize.y = Mathf.Abs(rect.height);
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

    private static void Swap(ref float first, ref float second)
    {
        float value = first;
        first = second;
        second = value;
    }

    private static Vector2 Clamp01(Vector2 value) => new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));

    private static ProceduralShapePoint DefaultPoint(int index)
    {
        switch (index)
        {
            case 0: return new ProceduralShapePoint(new Vector2(0f, 0f));
            case 1: return new ProceduralShapePoint(new Vector2(0f, 1f));
            case 2: return new ProceduralShapePoint(new Vector2(1f, 1f));
            default: return new ProceduralShapePoint(new Vector2(1f, 0f));
        }
    }
}
