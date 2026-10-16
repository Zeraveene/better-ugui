using System;
using System.Collections.Generic;
using Zeraveene.Shared.Inspector;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.UI;

[ExecuteAlways]
[AddComponentMenu("UI/Procedural Shape Outline")]
[RequireComponent(typeof(CanvasRenderer))]
[MovedFrom(true, sourceNamespace: "", sourceAssembly: "MaxsusWorks.ProceduralUI", sourceClassName: "ProceduralShapeOutline")]
public sealed class ProceduralShapeOutline : MaskableGraphic
{
    private ProceduralShapeOutline()
    {
        useLegacyMeshGeneration = false;
    }

    #region Inspector

    [SerializeField, FormerlySerializedAs("source")] private ProceduralShapeGraphic _source;
    [SerializeField] private ProceduralShapeImage _imageSource;
    [SerializeField] private ProceduralShapeOutline _outlineSource;
    [SerializeField] private ProceduralShapeOutlineDirection _direction;
    [SerializeField, Min(0f), FormerlySerializedAs("thicknessPixels")] private float _thicknessPixels = 1f;
    [SerializeField, Min(0f)] private float _offsetFromEdgePixels;
    [SerializeField]
    [Tooltip("Canvas Units scales with the UI like ProceduralImage. Screen Pixels keeps an absolute on-screen size.")]
    private ProceduralShapeMeasurementMode _measurementMode;
    [SerializeField] private bool _followSourceFalloff;
    [SerializeField, Min(0f), HideIf(nameof(_followSourceFalloff))] private float _falloffDistance;
    [SerializeField] private bool _followSourceDashes;

    #endregion

    #region Runtime Data

    private ProceduralShapeGraphic _subscribedSource;
    private ProceduralShapeImage _subscribedImageSource;
    private ProceduralShapeOutline _subscribedOutlineSource;

    // Reused every rebuild so populating the mesh allocates nothing.
    private readonly List<Vector2> _inward = new List<Vector2>(32);
    private readonly List<Vector2> _outerFade = new List<Vector2>(32);
    private readonly List<Vector2> _outer = new List<Vector2>(32);
    private readonly List<Vector2> _inner = new List<Vector2>(32);
    private readonly List<Vector2> _innerFade = new List<Vector2>(32);
    private readonly List<Vector2> _completedEdgeInward = new List<Vector2>(32);
    private readonly List<List<Vector2>> _sourcePerimeters = new List<List<Vector2>>(16);

    #endregion

    #region Callback

    public event Action GeometryChanged;

    #endregion

    #region Properties

    public ProceduralShapeGraphic Source
    {
        get { return _source; }
        set
        {
            if (_source == value && _outlineSource == null && _imageSource == null) return;
            _source = value;
            if (value != null)
            {
                _outlineSource = null;
                _imageSource = null;
            }
            RefreshSubscription();
            SourceWasChanged();
        }
    }

    public ProceduralShapeImage ImageSource
    {
        get { return _imageSource; }
        set
        {
            if (_imageSource == value && _source == null && _outlineSource == null) return;
            _imageSource = value;
            if (value != null)
            {
                _source = null;
                _outlineSource = null;
            }
            RefreshSubscription();
            SourceWasChanged();
        }
    }

    public ProceduralShapeOutline OutlineSource
    {
        get { return _outlineSource; }
        set
        {
            if (_outlineSource == value && _source == null && _imageSource == null) return;
            _outlineSource = WouldCreateCycle(value) ? null : value;
            if (_outlineSource != null)
            {
                _source = null;
                _imageSource = null;
            }
            RefreshSubscription();
            SourceWasChanged();
        }
    }

    public float ThicknessPixels
    {
        get { return _thicknessPixels; }
        set
        {
            _thicknessPixels = Mathf.Max(0f, value);
            GeometryWasChanged();
        }
    }

    public ProceduralShapeOutlineDirection Direction
    {
        get { return _direction; }
        set
        {
            if (_direction == value) return;
            _direction = value;
            GeometryWasChanged();
        }
    }

    public float OffsetFromEdgePixels
    {
        get { return _offsetFromEdgePixels; }
        set
        {
            _offsetFromEdgePixels = Mathf.Max(0f, value);
            GeometryWasChanged();
        }
    }

    public bool FollowSourceFalloff
    {
        get { return _followSourceFalloff; }
        set
        {
            if (_followSourceFalloff == value) return;
            _followSourceFalloff = value;
            GeometryWasChanged();
        }
    }

    public float FalloffDistance
    {
        get { return _falloffDistance; }
        set
        {
            _falloffDistance = Mathf.Max(0f, value);
            _followSourceFalloff = false;
            GeometryWasChanged();
        }
    }

    public ProceduralShapeMeasurementMode MeasurementMode
    {
        get { return _measurementMode; }
        set
        {
            if (_measurementMode == value) return;
            _measurementMode = value;
            GeometryWasChanged();
        }
    }

    public bool FollowSourceDashes
    {
        get { return _followSourceDashes; }
        set
        {
            if (_followSourceDashes == value) return;
            _followSourceDashes = value;
            GeometryWasChanged();
        }
    }

    #endregion

    #region Override

    protected override void OnEnable()
    {
        base.OnEnable();
        TryAssignSourceFromParent();
        RefreshSubscription();
        SetVerticesDirty();
    }

    protected override void OnDisable()
    {
        Unsubscribe();
        base.OnDisable();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (!HasSource || _thicknessPixels <= 0f)
            return;

        int contourCount = TryGetSourcePerimetersLocal(_sourcePerimeters, 0);
        if (contourCount == 0)
            return;

        Color32 opaque = color;
        Color32 transparent = opaque;
        transparent.a = 0;
        bool hasFalloff = GetFalloffDistance() > 0f;

        for (int contourIndex = 0; contourIndex < contourCount; contourIndex++)
        {
            BuildLoops(_sourcePerimeters[contourIndex]);
            int count = _outer.Count;
            if (count < 3) continue;

            if (hasFalloff)
            {
                int outerFadeStart = AddLoop(vh, _outerFade, transparent, count);
                int outerStart = AddLoop(vh, _outer, opaque, count);
                int innerStart = AddLoop(vh, _inner, opaque, count);
                int innerFadeStart = AddLoop(vh, _innerFade, transparent, count);
                AddRingTriangles(vh, outerFadeStart, outerStart, count);
                AddRingTriangles(vh, outerStart, innerStart, count);
                AddRingTriangles(vh, innerStart, innerFadeStart, count);
            }
            else
            {
                int outerStart = AddLoop(vh, _outer, opaque, count);
                int innerStart = AddLoop(vh, _inner, opaque, count);
                AddRingTriangles(vh, outerStart, innerStart, count);
            }
        }
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        GeometryWasChanged();
    }

    protected override void OnTransformParentChanged()
    {
        base.OnTransformParentChanged();
        TryAssignSourceFromParent();
        RefreshSubscription();
        SetVerticesDirty();
    }

#if UNITY_EDITOR
    protected override void Reset()
    {
        base.Reset();
        TryAssignSourceFromParent();
        RefreshSubscription();
        SetAllDirty();
    }

    protected override void OnValidate()
    {
        _thicknessPixels = Mathf.Max(0f, _thicknessPixels);
        _offsetFromEdgePixels = Mathf.Max(0f, _offsetFromEdgePixels);
        _falloffDistance = Mathf.Max(0f, _falloffDistance);
        if (WouldCreateCycle(_outlineSource))
            _outlineSource = null;
        if (_outlineSource != null)
        {
            _source = null;
            _imageSource = null;
        }
        else if (_imageSource != null)
        {
            _source = null;
        }
        base.OnValidate();
        TryAssignSourceFromParent();
        RefreshSubscription();
        SetAllDirty();
        GeometryChanged?.Invoke();
    }
#endif

    #endregion

    #region Private Methods

    // Builds the outline on the selected side of the source perimeter. Offset moves
    // the complete ring farther in that direction before thickness is applied.
    private void BuildLoops(List<Vector2> sourcePerimeter)
    {
        _inward.Clear();
        _outerFade.Clear();
        _outer.Clear();
        _inner.Clear();
        _innerFade.Clear();

        int count = sourcePerimeter.Count;
        if (count < 3)
            return;

        _outer.AddRange(sourcePerimeter);

        bool counterClockwise = SignedArea(_outer) > 0f;

        float thickness = GetLocalPixels(_thicknessPixels);
        float offsetFromEdge = GetLocalPixels(_offsetFromEdgePixels);
        float halfFalloff = Mathf.Min(GetLocalPixels(GetFalloffDistance()) * 0.5f, thickness * 0.5f);

        for (int i = 0; i < count; i++)
            _inward.Add(InwardMiter(_outer, i, count, counterClockwise));

        for (int i = 0; i < count; i++)
        {
            Vector2 inward = _inward[i];
            Vector2 edge = _outer[i];
            Vector2 outerEdge;
            Vector2 innerEdge;
            if (_direction == ProceduralShapeOutlineDirection.Outward)
            {
                innerEdge = edge - inward * offsetFromEdge;
                outerEdge = edge - inward * (offsetFromEdge + thickness);
            }
            else
            {
                outerEdge = edge + inward * offsetFromEdge;
                innerEdge = edge + inward * (offsetFromEdge + thickness);
            }

            _outerFade.Add(outerEdge - inward * halfFalloff);
            _outer[i] = outerEdge + inward * halfFalloff;
            _inner.Add(innerEdge - inward * halfFalloff);
            _innerFade.Add(innerEdge + inward * halfFalloff);
        }
    }

    // Averaged (mitered) inward normal at vertex i, derived from polygon winding.
    // The miter scale keeps the ring's perpendicular width constant across corners.
    private static Vector2 InwardMiter(List<Vector2> perimeter, int index, int count, bool counterClockwise)
    {
        Vector2 cur = perimeter[index];
        Vector2 prev = perimeter[(index + count - 1) % count];
        Vector2 next = perimeter[(index + 1) % count];

        Vector2 n1 = EdgeInwardNormal(prev, cur, counterClockwise);
        Vector2 n2 = EdgeInwardNormal(cur, next, counterClockwise);

        Vector2 miter = n1 + n2;
        if (miter.sqrMagnitude < 0.0000001f)
            return n1;

        miter.Normalize();

        // Clamp the miter length so very sharp corners don't produce long spikes.
        float cos = Mathf.Max(Vector2.Dot(miter, n1), 0.25f);
        return miter / cos;
    }

    private static Vector2 EdgeInwardNormal(Vector2 from, Vector2 to, bool counterClockwise)
    {
        Vector2 edge = to - from;
        if (edge.sqrMagnitude < 0.0000001f)
            return Vector2.zero;
        edge.Normalize();
        return counterClockwise
            ? new Vector2(-edge.y, edge.x)
            : new Vector2(edge.y, -edge.x);
    }

    private static float SignedArea(List<Vector2> points)
    {
        float area = 0f;
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 current = points[i];
            Vector2 next = points[(i + 1) % points.Count];
            area += current.x * next.y - current.y * next.x;
        }
        return area * 0.5f;
    }

    private float GetLocalPixels(float pixels)
    {
        if (_measurementMode == ProceduralShapeMeasurementMode.CanvasUnits)
            return pixels;

        Canvas rootCanvas = canvas != null ? canvas.rootCanvas : null;

        if (rootCanvas == null || rootCanvas.renderMode == RenderMode.WorldSpace)
            return pixels;

        return pixels / Mathf.Max(rootCanvas.scaleFactor, 0.0001f);
    }

    private float GetFalloffDistance()
    {
        return GetFalloffDistance(0);
    }

    private float GetFalloffDistance(int depth)
    {
        if (!_followSourceFalloff || depth > 64)
            return _falloffDistance;
        if (_outlineSource != null)
            return _outlineSource.GetFalloffDistance(depth + 1);
        if (_source != null) return _source.FalloffDistance;
        return _imageSource != null ? _imageSource.FalloffDistance : _falloffDistance;
    }

    private static int AddLoop(VertexHelper vh, List<Vector2> points, Color32 vertexColor, int count)
    {
        int start = vh.currentVertCount;
        for (int i = 0; i < count; i++) AddVertex(vh, points[i], vertexColor);
        return start;
    }

    private static void AddRingTriangles(VertexHelper vh, int outerStart, int innerStart, int count)
    {
        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            vh.AddTriangle(outerStart + i, outerStart + next, innerStart + next);
            vh.AddTriangle(outerStart + i, innerStart + next, innerStart + i);
        }
    }

    private static void AddVertex(VertexHelper vh, Vector2 position, Color32 vertexColor)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = vertexColor;
        vertex.uv0 = Vector2.zero;
        vh.AddVert(vertex);
    }

    private void RefreshSubscription()
    {
        if (_subscribedSource == _source && _subscribedImageSource == _imageSource && _subscribedOutlineSource == _outlineSource)
            return;

        Unsubscribe();

        _subscribedSource = _source;
        if (_subscribedSource != null)
            _subscribedSource.GeometryChanged += HandleSourceGeometryChanged;

        _subscribedImageSource = _imageSource;
        if (_subscribedImageSource != null)
            _subscribedImageSource.GeometryChanged += HandleSourceGeometryChanged;

        _subscribedOutlineSource = _outlineSource;
        if (_subscribedOutlineSource != null)
            _subscribedOutlineSource.GeometryChanged += HandleSourceGeometryChanged;
    }

    private void TryAssignSourceFromParent()
    {
        if (HasSource || transform.parent == null)
            return;

        _outlineSource = transform.parent.GetComponent<ProceduralShapeOutline>();
        if (_outlineSource == null)
        {
            _source = transform.parent.GetComponent<ProceduralShapeGraphic>();
            if (_source == null)
                _imageSource = transform.parent.GetComponent<ProceduralShapeImage>();
        }
    }

    private void Unsubscribe()
    {
        if (_subscribedSource != null)
            _subscribedSource.GeometryChanged -= HandleSourceGeometryChanged;
        if (_subscribedImageSource != null)
            _subscribedImageSource.GeometryChanged -= HandleSourceGeometryChanged;
        if (_subscribedOutlineSource != null)
            _subscribedOutlineSource.GeometryChanged -= HandleSourceGeometryChanged;

        _subscribedSource = null;
        _subscribedImageSource = null;
        _subscribedOutlineSource = null;
    }

    private void HandleSourceGeometryChanged()
    {
        GeometryWasChanged();
    }

    private int TryGetSourcePerimetersLocal(List<List<Vector2>> output, int depth)
    {
        return TryGetSourcePerimetersLocal(output, depth, _followSourceDashes);
    }

    private int TryGetSourcePerimetersLocal(List<List<Vector2>> output, int depth, bool followDashes)
    {
        if (depth > 64)
            return ClearContours(output);

        Transform sourceTransform;
        int contourCount;
        if (_outlineSource != null)
        {
            contourCount = _outlineSource.TryGetCompletedEdgesLocal(output, depth + 1, followDashes);
            sourceTransform = _outlineSource.transform;
        }
        else if (_source != null)
        {
            if (followDashes)
            {
                contourCount = _source.GetVisiblePerimetersLocal(output);
            }
            else
            {
                List<Vector2> contour = GetContour(output, 0);
                _source.GetRoundedPerimeterLocal(contour);
                contourCount = contour.Count >= 3 ? 1 : 0;
                ClearContoursAfter(output, contourCount);
            }
            sourceTransform = _source.transform;
        }
        else if (_imageSource != null)
        {
            List<Vector2> contour = GetContour(output, 0);
            _imageSource.GetRoundedPerimeterLocal(contour);
            contourCount = contour.Count >= 3 ? 1 : 0;
            ClearContoursAfter(output, contourCount);
            sourceTransform = _imageSource.transform;
        }
        else
        {
            return ClearContours(output);
        }

        for (int contourIndex = 0; contourIndex < contourCount; contourIndex++)
        {
            List<Vector2> contour = output[contourIndex];
            for (int i = 0; i < contour.Count; i++)
            {
                Vector3 world = sourceTransform.TransformPoint(contour[i]);
                contour[i] = rectTransform.InverseTransformPoint(world);
            }
        }

        return contourCount;
    }

    private int TryGetCompletedEdgesLocal(List<List<Vector2>> output, int depth, bool followDashes)
    {
        int contourCount = TryGetSourcePerimetersLocal(_sourcePerimeters, depth, followDashes);
        if (contourCount == 0)
            return ClearContours(output);

        float distance = GetLocalPixels(_offsetFromEdgePixels + _thicknessPixels);
        float direction = _direction == ProceduralShapeOutlineDirection.Outward ? -1f : 1f;
        for (int contourIndex = 0; contourIndex < contourCount; contourIndex++)
        {
            List<Vector2> source = _sourcePerimeters[contourIndex];
            List<Vector2> contour = GetContour(output, contourIndex);
            contour.AddRange(source);
            int count = contour.Count;
            bool counterClockwise = SignedArea(contour) > 0f;
            _completedEdgeInward.Clear();
            for (int i = 0; i < count; i++)
                _completedEdgeInward.Add(InwardMiter(contour, i, count, counterClockwise));
            for (int i = 0; i < count; i++)
                contour[i] += _completedEdgeInward[i] * (distance * direction);
        }

        ClearContoursAfter(output, contourCount);
        return contourCount;
    }

    private static List<Vector2> GetContour(List<List<Vector2>> output, int index)
    {
        while (output.Count <= index) output.Add(new List<Vector2>(16));
        output[index].Clear();
        return output[index];
    }

    private static int ClearContours(List<List<Vector2>> output)
    {
        ClearContoursAfter(output, 0);
        return 0;
    }

    private static void ClearContoursAfter(List<List<Vector2>> output, int activeCount)
    {
        for (int i = activeCount; i < output.Count; i++) output[i].Clear();
    }

    private bool HasSource => _outlineSource != null || _source != null || _imageSource != null;

    private bool WouldCreateCycle(ProceduralShapeOutline candidate)
    {
        for (int depth = 0; candidate != null && depth <= 64; depth++)
        {
            if (candidate == this)
                return true;
            candidate = candidate._outlineSource;
        }

        return candidate != null;
    }

    private void SourceWasChanged()
    {
        SetAllDirty();
        GeometryChanged?.Invoke();
    }

    private void GeometryWasChanged()
    {
        SetVerticesDirty();
        GeometryChanged?.Invoke();
    }

    #endregion
}
