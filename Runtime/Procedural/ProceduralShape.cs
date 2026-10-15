using System;
using System.Collections.Generic;
using Zeraveene.Shared.Inspector;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Scripting.APIUpdating;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[AddComponentMenu("UI/Procedural Shape")]
[RequireComponent(typeof(CanvasRenderer))]
[MovedFrom(true, "", "MaxsusWorks.ProceduralUI", "ProceduralShape")]
public sealed class ProceduralShape : ProceduralShapeGraphic
{
    #region Inspector

    [Header("Shape")]
    [SerializeField, FormerlySerializedAs("_uniformRadius")]
    private ProceduralShapeMode _shapeMode = ProceduralShapeMode.Uniform;

    [Header("Normalized corners (0..1)")]
    [SerializeField, FormerlySerializedAs("bottomLeft"), HideIf(nameof(IsCircle))]
    private Vector2 _bottomLeft = new Vector2(0f, 0f);
    [SerializeField, FormerlySerializedAs("topLeft"), HideIf(nameof(IsCircle))]
    private Vector2 _topLeft = new Vector2(0f, 1f);
    [SerializeField, FormerlySerializedAs("topRight"), HideIf(nameof(IsCircle))]
    private Vector2 _topRight = new Vector2(1f, 1f);
    [SerializeField, FormerlySerializedAs("bottomRight"), HideIf(nameof(IsCircle))]
    private Vector2 _bottomRight = new Vector2(1f, 0f);

    [FoldoutGroup("Corners + Precision", expanded: false), SerializeField, Min(0f), ShowIf(nameof(IsUniform))] private float _cornerRadius;
    [FoldoutGroup("Corners + Precision"), SerializeField, Min(0f), ShowIf(nameof(IsNonUniform))] private float _radiusBottomLeft;
    [FoldoutGroup("Corners + Precision"), SerializeField, Min(0f), ShowIf(nameof(IsNonUniform))] private float _radiusTopLeft;
    [FoldoutGroup("Corners + Precision"), SerializeField, Min(0f), ShowIf(nameof(IsNonUniform))] private float _radiusTopRight;
    [FoldoutGroup("Corners + Precision"), SerializeField, Min(0f), ShowIf(nameof(IsNonUniform))] private float _radiusBottomRight;
    [FoldoutGroup("Corners + Precision"), SerializeField, Min(2), HideIf(nameof(IsCircle))] private int _cornerSegments = 8;
    [FoldoutGroup("Corners + Precision"), SerializeField, FormerlySerializedAs("snapCornersToPixels"), HideIf(nameof(IsCircle))]
    private bool _snapCornersToPixels = true;

    [Header("Dashes")]
    [SerializeField] private bool _dashesEnabled;
    [SerializeField, ShowIf(nameof(_dashesEnabled))] private ProceduralShapeDashDirection _dashDirection = ProceduralShapeDashDirection.Vertical;
    [SerializeField, Range(0f, 1f), ShowIf(nameof(_dashesEnabled))] private float _dashLengthNormalized = 0.1f;
    [SerializeField, Range(0f, 1f), ShowIf(nameof(_dashesEnabled))] private float _dashGapNormalized = 0.1f;
    [SerializeField, Range(0f, 1f), ShowIf(nameof(_dashesEnabled))] private float _dashRoundness;

    #endregion

    #region Runtime Data

    private const int CircleSegments = 64;
    private const int MinCircleSegments = 12;
    private const float CircleMaxChordErrorPixels = 0.25f;
    private readonly List<Vector2> _corners = new List<Vector2>(4);
    private readonly List<float> _radii = new List<float>(4);

    #endregion

    #region Properties

    public ProceduralShapeMode ShapeMode
    {
        get => _shapeMode;
        set
        {
            _shapeMode = value;
            GeometryWasChanged();
        }
    }

    public Vector2 BottomLeft
    {
        get => _bottomLeft;
        set
        {
            _bottomLeft = Clamp01(value);
            GeometryWasChanged();
        }
    }

    public Vector2 TopLeft
    {
        get => _topLeft;
        set
        {
            _topLeft = Clamp01(value);
            GeometryWasChanged();
        }
    }

    public Vector2 TopRight
    {
        get => _topRight;
        set
        {
            _topRight = Clamp01(value);
            GeometryWasChanged();
        }
    }

    public Vector2 BottomRight
    {
        get => _bottomRight;
        set
        {
            _bottomRight = Clamp01(value);
            GeometryWasChanged();
        }
    }

    public bool UniformRadius
    {
        get => _shapeMode == ProceduralShapeMode.Uniform;
        set
        {
            _shapeMode = value ? ProceduralShapeMode.Uniform : ProceduralShapeMode.NonUniform;
            GeometryWasChanged();
        }
    }

    public float CornerRadius
    {
        get => _cornerRadius;
        set
        {
            _cornerRadius = Mathf.Max(0f, value);
            _shapeMode = ProceduralShapeMode.Uniform;
            GeometryWasChanged();
        }
    }

    public float RadiusBottomLeft
    {
        get => _radiusBottomLeft;
        set
        {
            _radiusBottomLeft = Mathf.Max(0f, value);
            GeometryWasChanged();
        }
    }

    public float RadiusTopLeft
    {
        get => _radiusTopLeft;
        set
        {
            _radiusTopLeft = Mathf.Max(0f, value);
            GeometryWasChanged();
        }
    }

    public float RadiusTopRight
    {
        get => _radiusTopRight;
        set
        {
            _radiusTopRight = Mathf.Max(0f, value);
            GeometryWasChanged();
        }
    }

    public float RadiusBottomRight
    {
        get => _radiusBottomRight;
        set
        {
            _radiusBottomRight = Mathf.Max(0f, value);
            GeometryWasChanged();
        }
    }

    public Vector4 Radii
    {
        get => new Vector4(Radius(0), Radius(1), Radius(2), Radius(3));
        set => SetRadii(value);
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

    public bool SnapCornersToPixels
    {
        get => _snapCornersToPixels;
        set
        {
            _snapCornersToPixels = value;
            GeometryWasChanged();
        }
    }

    public bool DashesEnabled
    {
        get => _dashesEnabled;
        set
        {
            if (_dashesEnabled == value) return;
            _dashesEnabled = value;
            GeometryWasChanged();
        }
    }

    public ProceduralShapeDashDirection DashDirection
    {
        get => _dashDirection;
        set
        {
            if (_dashDirection == value) return;
            _dashDirection = value;
            GeometryWasChanged();
        }
    }

    public float DashLengthNormalized
    {
        get => _dashLengthNormalized;
        set
        {
            _dashLengthNormalized = Mathf.Clamp01(value);
            GeometryWasChanged();
        }
    }

    public float DashGapNormalized
    {
        get => _dashGapNormalized;
        set
        {
            _dashGapNormalized = Mathf.Clamp01(value);
            GeometryWasChanged();
        }
    }

    public float DashRoundness
    {
        get => _dashRoundness;
        set
        {
            _dashRoundness = Mathf.Clamp01(value);
            GeometryWasChanged();
        }
    }

    private bool IsUniform => _shapeMode == ProceduralShapeMode.Uniform;
    private bool IsNonUniform => _shapeMode == ProceduralShapeMode.NonUniform;
    private bool IsCircle => _shapeMode == ProceduralShapeMode.Circle;

    #endregion

    #region Public Methods

    public void SetCorners(Vector2 bottomLeft, Vector2 topLeft, Vector2 topRight, Vector2 bottomRight)
    {
        _bottomLeft = Clamp01(bottomLeft);
        _topLeft = Clamp01(topLeft);
        _topRight = Clamp01(topRight);
        _bottomRight = Clamp01(bottomRight);
        GeometryWasChanged();
    }

    public void SetRadii(Vector4 value)
    {
        _radiusBottomLeft = Mathf.Max(0f, value.x);
        _radiusTopLeft = Mathf.Max(0f, value.y);
        _radiusTopRight = Mathf.Max(0f, value.z);
        _radiusBottomRight = Mathf.Max(0f, value.w);
        _shapeMode = ProceduralShapeMode.NonUniform;
        GeometryWasChanged();
    }

    public void FlipHorizontal()
    {
        if (IsCircle)
            return;

        Vector2 bottomLeft = _bottomLeft;
        Vector2 topLeft = _topLeft;
        Vector2 topRight = _topRight;
        Vector2 bottomRight = _bottomRight;
        _bottomLeft = ReflectHorizontal(bottomRight);
        _topLeft = ReflectHorizontal(topRight);
        _topRight = ReflectHorizontal(topLeft);
        _bottomRight = ReflectHorizontal(bottomLeft);

        Swap(ref _radiusBottomLeft, ref _radiusBottomRight);
        Swap(ref _radiusTopLeft, ref _radiusTopRight);
        GeometryWasChanged();
    }

    public void FlipVertical()
    {
        if (IsCircle)
            return;

        Vector2 bottomLeft = _bottomLeft;
        Vector2 topLeft = _topLeft;
        Vector2 topRight = _topRight;
        Vector2 bottomRight = _bottomRight;
        _bottomLeft = ReflectVertical(topLeft);
        _topLeft = ReflectVertical(bottomLeft);
        _topRight = ReflectVertical(bottomRight);
        _bottomRight = ReflectVertical(topRight);

        Swap(ref _radiusBottomLeft, ref _radiusTopLeft);
        Swap(ref _radiusBottomRight, ref _radiusTopRight);
        GeometryWasChanged();
    }

    public Vector2 GetNormalizedCorner(int index)
    {
        switch (index)
        {
            case 0: return _bottomLeft;
            case 1: return _topLeft;
            case 2: return _topRight;
            case 3: return _bottomRight;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public Vector2 GetLocalCorner(int index)
    {
        Rect rect = rectTransform.rect;
        Vector2 point = GetNormalizedCorner(index);
        Vector2 local = new Vector2(
            Mathf.Lerp(rect.xMin, rect.xMax, point.x),
            Mathf.Lerp(rect.yMin, rect.yMax, point.y));
        return _snapCornersToPixels ? PixelAdjustedPoint(local) : local;
    }

    public Vector3 GetWorldCorner(int index) => rectTransform.TransformPoint(GetLocalCorner(index));

    #endregion

    #region Override

    protected override void BuildRoundedPerimeter(List<Vector2> output)
    {
        if (IsCircle)
        {
            BuildCircle(output);
            return;
        }

        _corners.Clear();
        _radii.Clear();
        for (int i = 0; i < 4; i++)
        {
            _corners.Add(GetLocalCorner(i));
            _radii.Add(Radius(i));
        }
        BuildRoundedPolygon(_corners, _radii, _cornerSegments, output);
    }

    protected override bool TryGetDashPattern(
        out ProceduralShapeDashDirection direction,
        out float lengthNormalized,
        out float gapNormalized)
    {
        direction = _dashDirection;
        lengthNormalized = _dashLengthNormalized;
        gapNormalized = _dashGapNormalized;
        return _dashesEnabled;
    }

    protected override float GetDashRoundness() => _dashRoundness;

    protected override int GetDashCornerSegments() => _cornerSegments;

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        _bottomLeft = Clamp01(_bottomLeft);
        _topLeft = Clamp01(_topLeft);
        _topRight = Clamp01(_topRight);
        _bottomRight = Clamp01(_bottomRight);
        _cornerRadius = Mathf.Max(0f, _cornerRadius);
        _radiusBottomLeft = Mathf.Max(0f, _radiusBottomLeft);
        _radiusTopLeft = Mathf.Max(0f, _radiusTopLeft);
        _radiusTopRight = Mathf.Max(0f, _radiusTopRight);
        _radiusBottomRight = Mathf.Max(0f, _radiusBottomRight);
        _cornerSegments = Mathf.Max(2, _cornerSegments);
        _dashLengthNormalized = Mathf.Clamp01(_dashLengthNormalized);
        _dashGapNormalized = Mathf.Clamp01(_dashGapNormalized);
        _dashRoundness = Mathf.Clamp01(_dashRoundness);
        base.OnValidate();
    }
#endif

    #endregion

    #region Private Methods

#if UNITY_EDITOR
    [ButtonGroup("Flip"), Button("Flip Horizontal"), DisableIf(nameof(IsCircle)), PropertyOrder(100)]
    private void FlipHorizontalInInspector()
    {
        Undo.RecordObject(this, "Flip Procedural Shape Horizontal");
        FlipHorizontal();
        RecordInspectorChange();
    }

    [ButtonGroup("Flip"), Button("Flip Vertical"), DisableIf(nameof(IsCircle)), PropertyOrder(100)]
    private void FlipVerticalInInspector()
    {
        Undo.RecordObject(this, "Flip Procedural Shape Vertical");
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

    private static Vector2 ReflectHorizontal(Vector2 point) => new Vector2(1f - point.x, point.y);

    private static Vector2 ReflectVertical(Vector2 point) => new Vector2(point.x, 1f - point.y);

    private static void Swap(ref float first, ref float second)
    {
        float value = first;
        first = second;
        second = value;
    }

    private void BuildCircle(List<Vector2> output)
    {
        output.Clear();
        Rect rect = rectTransform.rect;
        float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
        int segments = GetCircleSegments(radius);
        for (int i = 0; radius > 0.0001f && i < segments; i++)
        {
            float angle = -Mathf.PI * 2f * i / segments;
            output.Add(rect.center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
    }

    // Fewest segments (a multiple of 4, so the circle still touches its rect) keeping the chord error
    // under CircleMaxChordErrorPixels on screen. Nothing rebuilds the mesh when the canvas scale factor or a
    // parent scale grows later, so it tessellates with headroom: as if the canvas scale factor were
    // max(2 x current, the CanvasScaler's largest reachable factor), times the shape's relative scale (never below 1).
    // World-space, missing, zero (clamped to 0.0001 by Canvas) or non-finite scale -> CircleSegments.
    private int GetCircleSegments(float radius)
    {
        Canvas root = canvas != null ? canvas.rootCanvas : null;
        if (root == null || root.renderMode == RenderMode.WorldSpace) return CircleSegments;
        float current = root.scaleFactor;
        if (!(current > 0.0001f) || float.IsInfinity(current)) return CircleSegments; // Canvas clamps 0 to 0.0001
        float scale = Mathf.Max(current * 2f, MaxCanvasScaleFactor(root, current));
        Vector3 shapeScale = rectTransform.lossyScale, canvasScale = root.transform.lossyScale;
        if (canvasScale.x != 0f && canvasScale.y != 0f)
            scale *= Mathf.Max(1f, Mathf.Max(Mathf.Abs(shapeScale.x / canvasScale.x), Mathf.Abs(shapeScale.y / canvasScale.y)));
        float pixels = radius * scale;
        if (pixels <= CircleMaxChordErrorPixels * 2f) return MinCircleSegments;
        float halfStep = Mathf.Acos(1f - CircleMaxChordErrorPixels / pixels);
        if (!(halfStep > 0f)) return CircleSegments;
        int segments = Mathf.CeilToInt(Mathf.PI / halfStep / 4f) * 4;
        return Mathf.Clamp(segments, MinCircleSegments, CircleSegments);
    }

    // Upper bound of a ScaleWithScreenSize CanvasScaler's factor: its formula applied with the largest
    // known screen side (current, monitor or display resolution) on both axes, so orientation swaps are
    // covered. Other scaler modes (and no scaler) return the current factor.
    private static float MaxCanvasScaleFactor(Canvas root, float current)
    {
        if (!root.TryGetComponent(out UnityEngine.UI.CanvasScaler scaler) || !scaler.enabled
            || scaler.uiScaleMode != UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize)
            return current;
        Vector2 reference = scaler.referenceResolution;
        if (!(reference.x > 0f) || !(reference.y > 0f)) return current;
        Resolution monitor = Screen.currentResolution;
        Display display = Display.main;
        float side = Mathf.Max(Mathf.Max(Screen.width, Screen.height), Mathf.Max(monitor.width, monitor.height));
        if (display != null) side = Mathf.Max(side, Mathf.Max(display.systemWidth, display.systemHeight));
        float x = side / reference.x, y = side / reference.y, max;
        switch (scaler.screenMatchMode)
        {
            case UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand: max = Mathf.Min(x, y); break;
            case UnityEngine.UI.CanvasScaler.ScreenMatchMode.Shrink: max = Mathf.Max(x, y); break;
            default: max = Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(x, 2f), Mathf.Log(y, 2f), scaler.matchWidthOrHeight)); break;
        }
        return max > current && !float.IsInfinity(max) ? max : current;
    }

    private float Radius(int index)
    {
        if (_shapeMode == ProceduralShapeMode.Uniform) return _cornerRadius;
        switch (index)
        {
            case 0: return _radiusBottomLeft;
            case 1: return _radiusTopLeft;
            case 2: return _radiusTopRight;
            case 3: return _radiusBottomRight;
            default: return 0f;
        }
    }

    #endregion
}
