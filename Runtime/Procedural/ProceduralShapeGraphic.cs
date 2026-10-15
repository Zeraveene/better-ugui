using System;
using System.Collections.Generic;
using Zeraveene.Shared.Inspector;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "MaxsusWorks.ProceduralUI")]
public abstract partial class ProceduralShapeGraphic : MaskableGraphic, ICanvasRaycastFilter
{
    #region Inspector

    [Header("Fill")]
    [SerializeField] private ProceduralShapeRenderType _renderType;
    [SerializeField, ShowIf(nameof(IsFilled))] private Image.FillMethod _fillMethod;
    [SerializeField, Range(0f, 1f), ShowIf(nameof(IsFilled))] private float _fillAmount = 1f;
    [SerializeField, ShowIf(nameof(ShowHorizontalOrigin))] private Image.OriginHorizontal _fillOriginHorizontal;
    [SerializeField, ShowIf(nameof(ShowVerticalOrigin))] private Image.OriginVertical _fillOriginVertical;
    [SerializeField, ShowIf(nameof(ShowRadial90Origin))] private Image.Origin90 _fillOrigin90;
    [SerializeField, ShowIf(nameof(ShowRadial180Origin))] private Image.Origin180 _fillOrigin180;
    [SerializeField, ShowIf(nameof(ShowRadial360Origin))] private Image.Origin360 _fillOrigin360;
    [SerializeField, ShowIf(nameof(IsRadialFill))] private bool _fillClockwise = true;

    [FoldoutGroup("Corners + Precision", expanded: false), SerializeField, Min(0f)] private float _borderWidth;
    [FoldoutGroup("Corners + Precision"), SerializeField, Min(0f)] private float _falloffDistance;
    [FoldoutGroup("Corners + Precision"), SerializeField]
    [Tooltip("Canvas Units scales with the UI like ProceduralImage. Screen Pixels keeps an absolute on-screen size.")]
    private ProceduralShapeMeasurementMode _measurementMode;

    #endregion

    #region Callback

    public event Action GeometryChanged;

    #endregion

    #region Runtime Data
    private readonly List<Vector2> _perimeter = new List<Vector2>(64);
    private readonly List<Vector2> _outer = new List<Vector2>(64);
    private readonly List<Vector2> _inner = new List<Vector2>(64);
    private readonly List<Vector2> _borderOuterFade = new List<Vector2>(64);
    private readonly List<Vector2> _borderOuter = new List<Vector2>(64);
    private readonly List<Vector2> _borderInner = new List<Vector2>(64);
    private readonly List<Vector2> _borderInnerFade = new List<Vector2>(64);
    private readonly List<List<Vector2>> _visiblePerimeters = new List<List<Vector2>>(16);
    private readonly List<Vector2> _dashClipInput = new List<Vector2>(64);
    private readonly List<Vector2> _dashClipOutput = new List<Vector2>(64);
    private readonly List<Vector2> _roundedDash = new List<Vector2>(64);
    private readonly List<float> _dashCornerRadii = new List<float>(64);
    private const int MaxDashCount = 1024;
    #endregion

    #region Properties

    public ProceduralShapeRenderType RenderType
    {
        get => _renderType;
        set
        {
            _renderType = value;
            SetVerticesDirty();
        }
    }

    public Image.FillMethod FillMethod
    {
        get => _fillMethod;
        set
        {
            _fillMethod = value;
            SetVerticesDirty();
        }
    }

    public float FillAmount
    {
        get => _fillAmount;
        set
        {
            _fillAmount = Mathf.Clamp01(value);
            SetVerticesDirty();
        }
    }

    public int FillOrigin
    {
        get
        {
            switch (_fillMethod)
            {
                case Image.FillMethod.Horizontal: return (int)_fillOriginHorizontal;
                case Image.FillMethod.Vertical: return (int)_fillOriginVertical;
                case Image.FillMethod.Radial90: return (int)_fillOrigin90;
                case Image.FillMethod.Radial180: return (int)_fillOrigin180;
                default: return (int)_fillOrigin360;
            }
        }
        set
        {
            switch (_fillMethod)
            {
                case Image.FillMethod.Horizontal:
                    _fillOriginHorizontal = (Image.OriginHorizontal)Mathf.Clamp(value, 0, 1);
                    break;
                case Image.FillMethod.Vertical:
                    _fillOriginVertical = (Image.OriginVertical)Mathf.Clamp(value, 0, 1);
                    break;
                case Image.FillMethod.Radial90:
                    _fillOrigin90 = (Image.Origin90)Mathf.Clamp(value, 0, 3);
                    break;
                case Image.FillMethod.Radial180:
                    _fillOrigin180 = (Image.Origin180)Mathf.Clamp(value, 0, 3);
                    break;
                default:
                    _fillOrigin360 = (Image.Origin360)Mathf.Clamp(value, 0, 3);
                    break;
            }
            SetVerticesDirty();
        }
    }

    public bool FillClockwise
    {
        get => _fillClockwise;
        set
        {
            _fillClockwise = value;
            SetVerticesDirty();
        }
    }

    public float FalloffDistance
    {
        get => _falloffDistance;
        set
        {
            _falloffDistance = Mathf.Max(0f, value);
            GeometryWasChanged();
        }
    }

    // Zero renders a filled shape. Positive values render an inward border in pixels.
    public float BorderWidth
    {
        get => _borderWidth;
        set
        {
            _borderWidth = Mathf.Max(0f, value);
            GeometryWasChanged();
        }
    }

    #endregion

    #region Public Methods

    public void GetRoundedPerimeterLocal(List<Vector2> output)
    {
        if (output == null) throw new ArgumentNullException(nameof(output));
        BuildRoundedPerimeter(output);
        Clean(output);
        if (!IsSimple(output)) output.Clear();
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

    internal int GetVisiblePerimetersLocal(List<List<Vector2>> output)
    {
        if (output == null) throw new ArgumentNullException(nameof(output));

        BuildRoundedPerimeter(_perimeter);
        Clean(_perimeter);
        if (!IsSimple(_perimeter))
            return ClearContours(output);

        ProceduralShapeDashDirection direction;
        float dashLength;
        float dashGap;
        if (!TryGetDashPattern(out direction, out dashLength, out dashGap))
            return CopySingleContour(_perimeter, output);

        dashLength = Mathf.Clamp01(dashLength);
        dashGap = Mathf.Clamp01(dashGap);
        if (dashLength <= 0.000001f)
            return ClearContours(output);
        if (dashGap <= 0.000001f || dashLength >= 0.999999f)
            return CopySingleContour(_perimeter, output);

        float period = dashLength + dashGap;
        int requestedCount = Mathf.Max(1, Mathf.FloorToInt((1f + dashGap) / period));
        int count = Mathf.Min(requestedCount, MaxDashCount);
        if (requestedCount > MaxDashCount)
        {
            float scale = 1f / (count * dashLength + (count - 1) * dashGap);
            dashLength *= scale;
            dashGap *= scale;
            period = dashLength + dashGap;
        }

        float occupied = count * dashLength + (count - 1) * dashGap;
        float start = (1f - occupied) * 0.5f;
        Rect rect = rectTransform.rect;
        bool horizontal = direction == ProceduralShapeDashDirection.Horizontal;
        float axisMin = horizontal ? rect.xMin : rect.yMin;
        float axisLength = horizontal ? rect.width : rect.height;
        int visibleCount = 0;
        for (int i = 0; i < count; i++)
        {
            float normalizedMin = start + i * period;
            float min = axisMin + normalizedMin * axisLength;
            float max = min + dashLength * axisLength;
            List<Vector2> contour = GetContour(output, visibleCount);
            ClipToAxisBand(_perimeter, horizontal, min, max, contour);
            Clean(contour);
            RoundDashCuts(contour, horizontal, min, max, axisLength);
            Clean(contour);
            if (IsSimple(contour)) visibleCount++;
            else contour.Clear();
        }
        ClearContoursAfter(output, visibleCount);
        return visibleCount;
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        Vector2 point;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out point)) return false;
        int contourCount = GetVisiblePerimetersLocal(_visiblePerimeters);
        if (contourCount == 0) return false;
        float borderWidth = LocalPixels(_borderWidth);
        for (int i = 0; i < contourCount; i++)
        {
            List<Vector2> contour = _visiblePerimeters[i];
            if (!Contains(point, contour)) continue;
            if (borderWidth <= 0f) return true;
            Offset(contour, borderWidth, _borderInner);
            return !IsUsableInset(contour, _borderInner) || !Contains(point, _borderInner);
        }
        return false;
    }

    #endregion

    #region Protected Methods

    protected abstract void BuildRoundedPerimeter(List<Vector2> output);

    protected virtual bool TryGetDashPattern(
        out ProceduralShapeDashDirection direction,
        out float lengthNormalized,
        out float gapNormalized)
    {
        direction = ProceduralShapeDashDirection.Vertical;
        lengthNormalized = 1f;
        gapNormalized = 0f;
        return false;
    }

    protected virtual float GetDashRoundness() => 0f;

    protected virtual int GetDashCornerSegments() => 2;

    protected void GeometryWasChanged()
    {
        SetVerticesDirty();
        SetMaterialDirty();
        GeometryChanged?.Invoke();
    }

    protected static Vector2 Clamp01(Vector2 value) => new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));
    protected Vector2 PixelAdjustedPoint(Vector2 value) => PixelAdjustPoint(value);

    protected static void BuildRoundedPolygon(IList<Vector2> points, IList<float> radii, int segments, List<Vector2> output)
    {
        output.Clear();
        if (points == null || radii == null || points.Count < 3 || points.Count != radii.Count) return;
        segments = Mathf.Max(2, segments);
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 p = points[i];
            Vector2 previous = points[(i + points.Count - 1) % points.Count] - p;
            Vector2 next = points[(i + 1) % points.Count] - p;
            float previousLength = previous.magnitude;
            float nextLength = next.magnitude;
            float radius = Mathf.Max(0f, radii[i]);
            if (radius <= 0f || previousLength < 0.0001f || nextLength < 0.0001f)
            {
                output.Add(p);
                continue;
            }

            Vector2 previousDirection = previous / previousLength;
            Vector2 nextDirection = next / nextLength;
            float halfAngle = Mathf.Acos(Mathf.Clamp(Vector2.Dot(previousDirection, nextDirection), -1f, 1f)) * 0.5f;
            float sine = Mathf.Sin(halfAngle);
            float tangentFactor = Mathf.Tan(halfAngle);
            Vector2 bisector = previousDirection + nextDirection;
            if (sine < 0.0001f || tangentFactor < 0.0001f || bisector.sqrMagnitude < 0.0000001f)
            {
                output.Add(p);
                continue;
            }

            float tangent = Mathf.Min(radius / tangentFactor, previousLength * 0.5f, nextLength * 0.5f);
            float effectiveRadius = tangent * tangentFactor;
            Vector2 center = p + bisector.normalized * (effectiveRadius / sine);
            Vector2 startPoint = p + previousDirection * tangent;
            Vector2 endPoint = p + nextDirection * tangent;
            float start = Mathf.Atan2(startPoint.y - center.y, startPoint.x - center.x);
            float end = Mathf.Atan2(endPoint.y - center.y, endPoint.x - center.x);
            float delta = Mathf.Atan2(Mathf.Sin(end - start), Mathf.Cos(end - start));
            for (int segment = 0; segment <= segments; segment++)
            {
                float angle = start + delta * segment / segments;
                output.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * effectiveRadius);
            }
        }
    }

    #endregion

    #region Override

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_renderType == ProceduralShapeRenderType.Filled && _fillAmount < 0.001f) return;
        int contourCount = GetVisiblePerimetersLocal(_visiblePerimeters);
        if (contourCount == 0) return;
        Rect rect = rectTransform.rect;
        bool clipFill = _renderType == ProceduralShapeRenderType.Filled && _fillAmount < 0.999f;
        VertexHelper target = clipFill ? _unclippedMesh : vh;
        if (clipFill) target.Clear();
        float borderWidth = LocalPixels(_borderWidth);
        for (int i = 0; i < contourCount; i++)
        {
            List<Vector2> contour = _visiblePerimeters[i];
            if (borderWidth > 0f)
                PopulateBorderMesh(target, contour, rect, borderWidth, LocalPixels(_falloffDistance));
            else
                PopulateFilledMesh(target, contour, rect, LocalPixels(_falloffDistance));
        }
        if (clipFill) ApplyFill(target, vh, rect);
    }

    #endregion

    #region Mesh Generation

    private void PopulateFilledMesh(VertexHelper vh, List<Vector2> perimeter, Rect rect, float falloff)
    {
        if (falloff <= 0f)
        {
            AddFill(vh, perimeter, rect);
            return;
        }

        Offset(perimeter, -falloff * 0.5f, _outer);
        Offset(perimeter, falloff * 0.5f, _inner);
        int innerStart;
        if (!IsSimple(_inner) || !AddFill(vh, _inner, rect, out innerStart))
        {
            if (!AddFill(vh, perimeter, rect, out innerStart)) return;
            _inner.Clear();
            _inner.AddRange(perimeter);
            Offset(perimeter, -falloff, _outer);
        }

        Color32 opaque = color;
        Color32 transparent = opaque;
        transparent.a = 0;
        int count = Mathf.Min(_outer.Count, _inner.Count);
        int outerStart = AddLoop(vh, _outer, transparent, rect, count);
        AddRingTriangles(vh, outerStart, innerStart, count);
    }

    private void PopulateBorderMesh(VertexHelper vh, List<Vector2> perimeter, Rect rect, float borderWidth, float falloff)
    {
        Offset(perimeter, borderWidth, _borderInner);
        if (!IsUsableInset(perimeter, _borderInner))
        {
            PopulateFilledMesh(vh, perimeter, rect, falloff);
            return;
        }

        Color32 opaque = color;
        if (falloff <= 0f)
        {
            AddRing(vh, perimeter, _borderInner, opaque, opaque, rect);
            return;
        }

        Color32 transparent = opaque;
        transparent.a = 0;
        float halfFalloff = Mathf.Min(falloff * 0.5f, borderWidth * 0.5f);
        Offset(perimeter, -halfFalloff, _borderOuterFade);
        Offset(perimeter, halfFalloff, _borderOuter);
        Offset(perimeter, borderWidth - halfFalloff, _borderInner);
        Offset(perimeter, borderWidth + halfFalloff, _borderInnerFade);

        if (!IsUsableInset(perimeter, _borderInner) || !IsUsableInset(perimeter, _borderInnerFade))
        {
            PopulateFilledMesh(vh, perimeter, rect, falloff);
            return;
        }

        int count = Mathf.Min(
            Mathf.Min(_borderOuterFade.Count, _borderOuter.Count),
            Mathf.Min(_borderInner.Count, _borderInnerFade.Count));
        int outerFadeStart = AddLoop(vh, _borderOuterFade, transparent, rect, count);
        int outerStart = AddLoop(vh, _borderOuter, opaque, rect, count);
        int innerStart = AddLoop(vh, _borderInner, opaque, rect, count);
        int innerFadeStart = AddLoop(vh, _borderInnerFade, transparent, rect, count);
        AddRingTriangles(vh, outerFadeStart, outerStart, count);
        AddRingTriangles(vh, outerStart, innerStart, count);
        AddRingTriangles(vh, innerStart, innerFadeStart, count);
    }

    #endregion

    #region Override

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        GeometryWasChanged();
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        _borderWidth = Mathf.Max(0f, _borderWidth);
        _falloffDistance = Mathf.Max(0f, _falloffDistance);
        _fillAmount = Mathf.Clamp01(_fillAmount);
        base.OnValidate();
        GeometryWasChanged();
    }
#endif

    protected override void OnDestroy()
    {
        _unclippedMesh.Dispose();
        base.OnDestroy();
    }

    #endregion

    #region Inspector Conditions

    private bool IsFilled => _renderType == ProceduralShapeRenderType.Filled;
    private bool ShowHorizontalOrigin => IsFilled && _fillMethod == Image.FillMethod.Horizontal;
    private bool ShowVerticalOrigin => IsFilled && _fillMethod == Image.FillMethod.Vertical;
    private bool ShowRadial90Origin => IsFilled && _fillMethod == Image.FillMethod.Radial90;
    private bool ShowRadial180Origin => IsFilled && _fillMethod == Image.FillMethod.Radial180;
    private bool ShowRadial360Origin => IsFilled && _fillMethod == Image.FillMethod.Radial360;
    private bool IsRadialFill => IsFilled && _fillMethod >= Image.FillMethod.Radial90;

    #endregion

    #region Mesh Helpers

    private static void AddVertex(VertexHelper vh, Vector2 position, Color32 vertexColor, Rect rect)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = vertexColor;
        vertex.uv0 = new Vector2(
            Mathf.InverseLerp(rect.xMin, rect.xMax, position.x),
            Mathf.InverseLerp(rect.yMin, rect.yMax, position.y));
        vh.AddVert(vertex);
    }

    private float LocalPixels(float pixels)
    {
        if (_measurementMode == ProceduralShapeMeasurementMode.CanvasUnits) return pixels;
        Canvas root = canvas != null ? canvas.rootCanvas : null;
        return root == null || root.renderMode == RenderMode.WorldSpace ? pixels : pixels / Mathf.Max(root.scaleFactor, 0.0001f);
    }
    #endregion
}
